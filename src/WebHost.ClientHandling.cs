using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebHost.Exceptions;
using WebHost.Models;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

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
    /// Creates a new <see cref="Context"/> for the client and processes the connection using <see cref="HandleClientAsyncXX"/>.
    /// This method is used for plain, unencrypted client communication.
    /// </remarks>
    private async Task HandlePlainClientAsync(Socket client, CancellationToken stoppingToken)
    {
        await HandleClientAsync1X(client, null, stoppingToken);
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
    /// - If the TLS handshake succeeds, the connection is processed using <see cref="HandleClientAsyncXX"/>.
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

        var protocol = string.Empty;

        try
        {
            // Perform the TLS handshake
            //
            var protocols = new List<SslApplicationProtocol>
            {
                SslApplicationProtocol.Http2,
                SslApplicationProtocol.Http11,
            };

            //SslClientAuthenticationOptions a = new SslClientAuthenticationOptions();

            var sslOptions = new SslServerAuthenticationOptions
            {
                ApplicationProtocols = protocols,
                ServerCertificate = SecurityOptions.ServerCertificate,
                EnabledSslProtocols = SslProtocols.Tls12,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                ClientCertificateRequired = true,
                RemoteCertificateValidationCallback = SecurityOptions.ClientCertificateValidation
            };

            await sslStream.AuthenticateAsServerAsync(sslOptions, stoppingToken);
            /*
            await sslStream.AuthenticateAsServerAsync(SecurityOptions.ServerCertificate,
                                                        clientCertificateRequired: true,
                                                        enabledSslProtocols: SslProtocols.Tls12,
                                                        checkCertificateRevocation: false);
            */

            protocol = sslStream.NegotiatedApplicationProtocol.ToString();
            Console.WriteLine($"Negotiated protocol: {protocol}");
        }
        catch (Exception ex) when (HandleTlsException(ex))
        {
            // Unified handling of TLS failure message.
            //
            await SendTlsFailureMessageAsync(client);
        }

        // Handle the client connection securely
        //
        var handler = protocol switch
        {
            "h2" => HandleClientAsync2(sslStream, stoppingToken),
            _ => HandleClientAsync1X(client, sslStream, stoppingToken)
        };

        await handler;
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
}
