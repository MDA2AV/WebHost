﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using WebHost.Exceptions;
using WebHost.Extensions;
using WebHost.Models;
using WebHost.Utils.HttpRequest;

namespace WebHost;

public sealed partial class WebHostApp
{
    private enum ConnectionType
    {
        Close,
        KeepAlive,
        Websocket
    }
    private static ConnectionType GetConnectionType(string headers)
    {
        // Check for Keep-Alive
        if (headers.IndexOf("Connection: keep-alive", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConnectionType.KeepAlive;

        // Check for Connection: close
        if (headers.IndexOf("Connection: close", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConnectionType.Close;

        // Check for WebSocket upgrade
        return headers.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) >= 0 
            ? ConnectionType.Websocket 
            : ConnectionType.Close;
    }
    /// <summary>
    /// Handles a client connection by processing incoming requests and executing the middleware pipeline.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="pipeReader"></param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the operation should stop.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Reads the client request and parses its headers, body, and route information.
    /// - Validates the request's structure and initializes the <see cref="Http11Request"/> property.
    /// - Creates a scoped service provider for each request and executes the middleware pipeline.
    /// - Handles "keep-alive" connections by continuing to process additional requests from the same client.
    /// - Closes the connection when the request does not include "keep-alive" or upon invalid input.
    /// </remarks>
    private async Task HandleClientAsync11(Stream stream, PipeReader pipeReader, CancellationToken stoppingToken)
    {
        var context = new Context(stream);

        // Read the initial client request
        //
        var headers = await ExtractHeaders(pipeReader, stoppingToken);

        // Loop to handle multiple requests for "keep-alive" connections
        //
        while (headers != null)
        {
            var connection = GetConnectionType(headers);

            if (connection == ConnectionType.Websocket)
                await SendHandshakeResponse(context, headers);

            var headerEntries = headers.Split("\r\n");

            // Split the request into headers and body
            //
            var body = await ExtractBody(pipeReader, headers, stoppingToken);

            // Try to extract the uri from the headers
            //
            var result = RequestParser.TryExtractUri2(headerEntries[0], out (string, string) uriHeader);
            if (!result)
            {
                _logger?.LogTrace("Invalid request received, unable to parse route");
                throw new InvalidOperationServiceException("Invalid request received, unable to parse route");
            }

            var uriParams = uriHeader.Item2.Split('?');

            // Populate the context with the parsed request information
            //
            context.Request = new Http11Request(Headers: headerEntries,
                                          Body: body,
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
            if (connection == ConnectionType.KeepAlive)
            {
                headers = await ExtractHeaders(pipeReader, stoppingToken);
            }
            else
            {
                break;
            }
        }
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
        var buffer = BufferPool.Rent(512);
        var receivedBytesNumber = await context.ReadAsync(buffer, cancellationToken: stoppingToken);

        if (receivedBytesNumber == 0)
        {
            _logger?.LogTrace("Client closed the connection.");
            return null;
        }

        var request = DecodeRequest(buffer.AsSpan(0, receivedBytesNumber));
        _logger?.LogTrace("Received: {Request}", request);

        return request;
    }
    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

    private static readonly ReadOnlyMemory<byte> WebsocketHandshakePrefix 
        = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> WebsocketHandshakeSuffix = "\r\n\r\n"u8.ToArray();

    /// <summary>
    /// Creates a WebSocket handshake response for an incoming WebSocket upgrade request.
    /// </summary>
    /// <param name="context"></param>
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
    private static async Task SendHandshakeResponse(IContext context, string request)
    {
        await context.SendAsync(WebsocketHandshakePrefix);
        await context.SendAsync(CreateAcceptKey(request));
        await context.SendAsync(WebsocketHandshakeSuffix);
    }

    private static string CreateAcceptKey(string request)
    {
        const string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

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
        return Convert.ToBase64String(
            SHA1.HashData(Encoding.UTF8.GetBytes(key.ToString() + magicString)));
    }
}