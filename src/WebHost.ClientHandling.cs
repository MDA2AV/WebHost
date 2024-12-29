using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebHost.Exceptions;
using WebHost.Extensions;
using WebHost.Models;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WebHost.Utils.HttpRequest;

namespace WebHost;

public sealed partial class WebHostApp
{
    /// <summary>
    /// Handles a client connection using plain (non-TLS) communication.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <param name="stoppingToken">
    /// A <see cref="CancellationToken"/> to signal when the operation should stop.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Creates a new <see cref="Context"/> for the client and processes the connection using <see cref="HandleClientAsync"/>.
    /// This method is used for plain, unencrypted client communication.
    /// </remarks>
    private async Task HandlePlainClientAsync(Socket client, CancellationToken stoppingToken)
    {
        await HandleClientAsync(client, null, stoppingToken);
    }

    /// <summary>
    /// Handles a client connection using TLS (encrypted) communication.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <param name="stoppingToken">
    /// A <see cref="CancellationToken"/> to signal when the operation should stop.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Performs the TLS handshake to secure the connection.
    /// - Validates the server certificate and optionally validates the client certificate.
    /// - If the TLS handshake succeeds, the connection is processed using <see cref="HandleClientAsync"/>.
    /// - If the TLS handshake fails, an error message is sent to the client before the connection is closed.
    /// </remarks>
    /// <exception cref="ServiceUnavailableServiceException">
    /// Thrown if the server certificate is null, as TLS cannot be established without it.
    /// </exception>
    private async Task HandleTlsClientAsync(Socket client, CancellationToken stoppingToken)
    {
        if (SecurityOptions.ServerCertificate is null)
        {
            throw new ServiceUnavailableServiceException("SecurityOptions.ServerCertificate is null");
        }

        // Create and configure the SSL stream for the client connection
        //
        await using var sslStream = new SslStream(new NetworkStream(client),
                                                  false,
                                                  SecurityOptions.ClientCertificateValidation);
        try
        {
            // Perform the TLS handshake
            //
            await sslStream.AuthenticateAsServerAsync(SecurityOptions.ServerCertificate,
                                                        clientCertificateRequired: true,
                                                        enabledSslProtocols: SslProtocols.Tls12,
                                                        checkCertificateRevocation: false);
        }
        catch (Exception ex) when (HandleTlsException(ex))
        {
            // Unified handling of TLS failure message.
            //
            await SendTlsFailureMessageAsync(client);
        }

        // Handle the client connection securely
        //
        await HandleClientAsync(client, sslStream, stoppingToken);
    }

    /// <summary>
    /// Handles logging and classification of TLS handshake exceptions.
    /// </summary>
    /// <param name="ex">The exception to process.</param>
    /// <returns>Always returns <c>true</c> to continue exception filtering.</returns>
    private bool HandleTlsException(Exception ex)
    {
        switch (ex)
        {
            case AuthenticationException authEx:
                _logger?.LogTrace("TLS Handshake failed due to authentication error: {Message}", authEx.Message);
                break;
            case InvalidOperationException invalidOpEx:
                _logger?.LogTrace("TLS Handshake failed due to socket error: {Message}", invalidOpEx.Message);
                break;
            default:
                _logger?.LogTrace("Unexpected error during TLS Handshake: {Message}", ex.Message);
                break;
        }

        return true; // Ensure the exception is always caught
    }

    /// <summary>
    /// Handles a client connection by processing incoming requests and executing the middleware pipeline.
    /// </summary>
    /// <param name="sslStream"></param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the operation should stop.</param>
    /// <param name="client"></param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Reads the client request and parses its headers, body, and route information.
    /// - Validates the request's structure and initializes the <see cref="Request"/> property.
    /// - Creates a scoped service provider for each request and executes the middleware pipeline.
    /// - Handles "keep-alive" connections by continuing to process additional requests from the same client.
    /// - Closes the connection when the request does not include "keep-alive" or upon invalid input.
    /// </remarks>
    private async Task HandleClientAsync(Socket client, SslStream? sslStream, CancellationToken stoppingToken)
    {
        var context = new Context(client)
        {
            SslStream = sslStream,
        };

        // Read the initial client request
        //
        var request = await GetClientRequest(context, stoppingToken);

        // Loop to handle multiple requests for "keep-alive" connections
        //
        while (request != null)
        {
            if (request.Contains("Upgrade: websocket"))
            {
                await SendHandshakeResponse(context, request);
            }

            // Split the request into headers and body
            //
            (string[], string) requestData = RequestParser.SplitHeadersAndBody(request);

            // Try to extract the uri from the headers
            //
            var result = RequestParser.TryExtractUri(headers: requestData.Item1, out (string, string) uriHeader);
            if (!result)
            {
                _logger?.LogTrace("Invalid request received, unable to parse route");
                throw new InvalidOperationServiceException("Invalid request received, unable to parse route");
            }

            var uriParams = uriHeader.Item2.Split('?');

            // Populate the context with the parsed request information
            //
            context.Request = new Request(Headers: requestData.Item1,
                                          Body: requestData.Item2,
                                          Route: uriParams[0],
                                          QueryParameters: uriParams.Length > 1 ? uriParams[1] : string.Empty,
                                          HttpMethod: uriHeader.Item1);

            // Create a new scope for handling the request
            //
            await using (var scope = InternalHost.Services.CreateAsyncScope())
            {
                context.Scope = scope;

                // Retrieve and execute the middleware pipeline
                //
                var middleware = scope.ServiceProvider.GetServices<Func<IContext, Func<IContext, Task>, Task>>().ToList();

                await Pipeline(context, 0, middleware);
            }

            // Handle "keep-alive" connections
            //
            if (request.Contains("Connection: keep-alive"))
            {
                request = await GetClientRequest(context, stoppingToken); // Read the next request
            }
            else
            {
                break; // Exit the loop if the connection is not "keep-alive"
            }
        }
    }

    /// <summary>
    /// Pipeline recursively executes registered middleware by order or registration. Last middleware element wraps the endpoint. 
    /// All elements are resolved within the context.Scope.IServiceProvider.
    /// </summary>
    /// <exception cref="InvalidOperationServiceException"></exception>
    public Task Pipeline(IContext context, int index, IList<Func<IContext, Func<IContext, Task>, Task>> middleware)
    {
        if (index < middleware.Count)
        {
            return middleware[index](context, async (ctx) => await Pipeline(ctx, index + 1, middleware));
        }

        var decodedRoute = MatchEndpoint(EncodedRoutes, context.Request.Route);

        var endpoint = context.Scope.ServiceProvider
            .GetRequiredKeyedService<Func<IContext, Task>>($"{context.Request.HttpMethod}_{decodedRoute}");

        return endpoint is null
            ? throw new InvalidOperationServiceException("Unable to find the Invoke method on the resolved service.")
            : endpoint.Invoke(context);

    }

    // Property to hold the encoded routes, initialized as an empty HashSet
    public HashSet<string> EncodedRoutes { get; set; } = [];

    /// <summary>
    /// Matches a given input string against a set of encoded routes.
    /// </summary>
    /// <param name="hashSet">A HashSet containing route patterns.</param>
    /// <param name="input">The input string to match against the patterns.</param>
    /// <returns>The first matching route pattern from the HashSet, or null if no match is found.</returns>
    public static string? MatchEndpoint(HashSet<string> hashSet, string input)
    {
        // Iterate through each route pattern in the HashSet
        // Convert the pattern to a regex and check if the input matches
        return (from entry in hashSet
                let pattern = ConvertToRegex(entry) // Convert route pattern to regex
                where Regex.IsMatch(input, pattern) // Check if input matches the regex
                select entry) // Select the matching pattern
            .FirstOrDefault(); // Return the first match or null if no match is found
    }

    /// <summary>
    /// Converts a route pattern with placeholders (e.g., ":id") into a regular expression.
    /// </summary>
    /// <param name="pattern">The route pattern to convert.</param>
    /// <returns>A regex string that matches the given pattern.</returns>
    public static string ConvertToRegex(string pattern)
    {
        // Replace placeholders like ":id" with a regex pattern that matches any non-slash characters
        var regexPattern = Regex.Replace(pattern, @":\w+", "[^/]+");

        // Add anchors to ensure the regex matches the entire input string
        regexPattern = $"^{regexPattern}$";

        return regexPattern;
    }

    /// <summary>
    /// Reads a client request from the provided context and decodes it as a UTF-8 string.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the client connection.</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the operation should stop.</param>
    /// <returns>
    /// A <see cref="Task"/> that resolves to the decoded request as a string, or <c>null</c> if the client connection is closed.
    /// </returns>
    /// <remarks>
    /// - Uses a buffer represented as <see cref="Memory{T}"/> for efficient memory management.
    /// - Handles both SSL and raw socket connections.
    /// - Logs the received request for debugging purposes.
    /// </remarks>
    private async Task<string?> GetClientRequest(IContext context, CancellationToken stoppingToken)
    {
        var buffer = new Memory<byte>(new byte[1024]);
        var receivedBytesNumber = await ReadFromClientAsync(context, buffer, stoppingToken);

        if (receivedBytesNumber == 0)
        {
            _logger?.LogTrace("Client closed the connection.");
            return null;
        }

        var request = DecodeRequest(buffer[..receivedBytesNumber]);
        _logger?.LogTrace("Received: {Request}", request);

        return request;
    }

    /// <summary>
    /// Reads data from the client using either an SSL stream or a raw socket.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the client connection.</param>
    /// <param name="buffer">A <see cref="Memory{T}"/> buffer to store the received data.</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the operation should stop.</param>
    /// <returns>
    /// A <see cref="Task"/> that resolves to the number of bytes received from the client.
    /// </returns>
    /// <remarks>
    /// - Determines the appropriate input stream (SSL or raw socket) from the context.
    /// - Throws an exception if neither stream is available.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no valid client stream is available for reading.</exception>
    private static async Task<int> ReadFromClientAsync(IContext context, Memory<byte> buffer, CancellationToken stoppingToken)
    {
        if (context.SslStream is not null)
        {
            return await context.SslStream.ReadAsync(buffer, stoppingToken);
        }

        if (context.Socket is not null)
        {
            return await context.Socket.ReceiveAsync(buffer, SocketFlags.None, stoppingToken);
        }

        throw new InvalidOperationException("No valid client stream available for reading.");
    }

    /// <summary>
    /// Decodes a buffer of received bytes into a UTF-8 string.
    /// </summary>
    /// <param name="buffer">A <see cref="ReadOnlyMemory{T}"/> containing the data to decode.</param>
    /// <returns>The decoded request as a UTF-8 string.</returns>
    /// <remarks>
    /// - Uses the <see cref="Encoding.UTF8"/> class to decode the buffer.
    /// - Ensures the original memory buffer is not modified by working with <see cref="ReadOnlyMemory{T}"/>.
    /// </remarks>
    private static string DecodeRequest(ReadOnlyMemory<byte> buffer)
    {
        return Encoding.UTF8.GetString(buffer.Span);
    }

    /// <summary>
    /// Sends a failure message to the client when the TLS handshake fails and closes the connection.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Attempts to notify the client of the TLS handshake failure by sending a predefined message.
    /// - Any errors during the send operation are ignored to ensure the connection is closed gracefully.
    /// - The client connection is always closed, regardless of whether the send operation succeeds or fails.
    /// </remarks>
    private static async Task SendTlsFailureMessageAsync(Socket client)
    {
        var messageBytes = "TLS Handshake failed. Closing connection."u8.ToArray();

        try
        {
            await client.SendAsync(messageBytes, SocketFlags.None);
        }
        catch
        {
            // Ignore errors while sending failure response
        }
        finally
        {
            client.Close();
        }
    }

    /// <summary>
    /// Sends the WebSocket handshake response to the client based on the incoming request.
    /// </summary>
    /// <param name="context">The context representing the client connection.</param>
    /// <param name="request">The raw HTTP WebSocket upgrade request from the client.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private static async Task SendHandshakeResponse(IContext context, string request)
    {
        // Send the response
        await context.SendAsync(GenerateHandshakeResponse(request));
    }

    /// <summary>
    /// Creates a WebSocket handshake response for an incoming WebSocket upgrade request.
    /// </summary>
    /// <param name="request">
    /// The raw HTTP request string received from the client, which includes headers and the WebSocket key.
    /// </param>
    /// <returns>
    /// A <see cref="string"/> representing the HTTP response to complete the WebSocket handshake,
    /// containing the necessary headers to switch protocols.
    /// </returns>
    /// <remarks>
    /// This method processes the WebSocket upgrade request by:
    /// - Extracting the `Sec-WebSocket-Key` header from the request.
    /// - Generating the `Sec-WebSocket-Accept` value by concatenating the key with a standard magic GUID,
    ///   hashing the result using SHA-1, and encoding it in Base64.
    /// - Constructing an HTTP response with the required headers (`Upgrade`, `Connection`, and `Sec-WebSocket-Accept`).
    /// 
    /// The handshake follows the WebSocket protocol as defined in RFC 6455, Section 4.2.2.
    /// 
    /// Limitations:
    /// - Assumes the `request` parameter contains a complete HTTP request.
    /// - Does not validate other aspects of the request, such as HTTP method or version.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the `Sec-WebSocket-Key` header is not found in the request, indicating an invalid WebSocket upgrade request.
    /// </exception>
    private static ReadOnlyMemory<byte> GenerateHandshakeResponse(string request)
    {
        const string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        // Predefined byte arrays for fixed parts of the response
        var fixedResponsePart = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "u8;
        var responseTerminator = "\r\n\r\n"u8;

        // Extract the Sec-WebSocket-Key using ReadOnlySpan<char>
        var requestSpan = request.AsSpan();
        var keyLineStart = requestSpan.IndexOf("Sec-WebSocket-Key:".AsSpan(), StringComparison.OrdinalIgnoreCase);

        if (keyLineStart == -1)
        {
            throw new InvalidOperationException("Sec-WebSocket-Key not found in the request.");
        }

        var keyLine = requestSpan.Slice(keyLineStart + "Sec-WebSocket-Key:".Length);
        var keyEnd = keyLine.IndexOf("\r\n".AsSpan());
        if (keyEnd != -1)
        {
            keyLine = keyLine[..keyEnd];
        }

        var key = keyLine.Trim();

        // Generate the Sec-WebSocket-Accept header value
        var acceptKey = Convert.ToBase64String(
            SHA1.HashData(Encoding.UTF8.GetBytes(key.ToString() + magicString))
        );

        var acceptKeyBytes = Encoding.UTF8.GetBytes(acceptKey);

        // Allocate the response byte array
        var responseBytes = new byte[fixedResponsePart.Length + acceptKeyBytes.Length + responseTerminator.Length];

        // Build the response using Span<byte>
        var responseSpan = responseBytes.AsSpan();
        fixedResponsePart.CopyTo(responseSpan);
        acceptKeyBytes.CopyTo(responseSpan[fixedResponsePart.Length..]);
        responseTerminator.CopyTo(responseSpan[(fixedResponsePart.Length + acceptKeyBytes.Length)..]);

        return responseBytes;
    }
}
