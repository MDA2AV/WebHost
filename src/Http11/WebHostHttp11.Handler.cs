using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using WebHost.Http11.Context;
using WebHost.MemoryBuffers;
using WebHost.Protocol;

namespace WebHost.Http11;

public interface IHandlerArgs
{
    bool UseResources { get; }
    string ResourcesPath { get; }
    Assembly ResourcesAssembly { get; }
}

public record Http11HandlerArgs(
    bool UseResources,
    string ResourcesPath,
    Assembly ResourcesAssembly) : IHandlerArgs;

public partial class WebHostHttp11<TContext>(IHandlerArgs args) : IHttpHandler<TContext>
    where TContext : IContext, new()
{
    /// <summary>
    /// Defines the types of HTTP connection behaviors supported by the web host.
    /// </summary>
    private enum ConnectionType
    {
        /// <summary>
        /// The connection should be closed after the current request is processed.
        /// </summary>
        Close,

        /// <summary>
        /// The connection should be kept alive for potential subsequent requests.
        /// </summary>
        KeepAlive,

        /// <summary>
        /// The connection should be upgraded to a WebSocket connection.
        /// </summary>
        Websocket
    }

    /// <summary>
    /// Determines the connection behavior based on the HTTP request headers.
    /// </summary>
    /// <param name="headers">The raw HTTP headers string from the client request.</param>
    /// <returns>
    /// A <see cref="ConnectionType"/> enum value indicating how the connection should be handled:
    /// - KeepAlive: Connection should remain open after response
    /// - Websocket: Connection should be upgraded to WebSocket protocol
    /// - Close: Connection should be closed after response (default)
    /// </returns>
    /// <remarks>
    /// The method searches for specific connection-related headers in the following order:
    /// 1. "Connection: keep-alive" - Indicates the client wants to maintain the connection
    /// 2. "Connection: close" - Explicitly requests connection termination
    /// 3. "Upgrade: websocket" - Requests a protocol upgrade to WebSocket
    /// 
    /// If none of these headers are found, the default behavior is to close the connection.
    /// Header matching is case-insensitive.
    /// </remarks>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    private static ConnectionType GetConnectionType(string headers)
    {
        // Check for Keep-Alive connection header
        if (headers.IndexOf("Connection: keep-alive", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConnectionType.KeepAlive;

        // Check for explicit Connection: close header
        if (headers.IndexOf("Connection: close", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConnectionType.Close;

        // Check for WebSocket upgrade request
        // If not found, default to closing the connection
        return headers.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) >= 0
            ? ConnectionType.Websocket
            : ConnectionType.Close;
    }

    private static ConnectionType GetConnectionType2(string connectionType)
    {
        return connectionType switch
        {
            "keep-alive" => ConnectionType.KeepAlive,
            "close" => ConnectionType.Close,
            "websocket" => ConnectionType.Websocket,
            _ => ConnectionType.Close
        };
    }

    /// <summary>
    /// Handles a client connection by processing incoming HTTP/1.1 requests and executing the middleware pipeline.
    /// </summary>
    /// <param name="stream">The network stream for communication with the client.</param>
    /// <param name="pipeline"></param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the operation should stop.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method implements the core HTTP/1.1 request processing logic:
    /// 
    /// 1. Connection Lifecycle:
    ///    - Reads the initial headers from the client request
    ///    - Processes requests in a loop for persistent connections (keep-alive)
    ///    - Closes the connection based on connection header or after processing a non-keep-alive request
    ///    
    /// 2. Request Processing:
    ///    - Parses HTTP headers to extract the route, HTTP method, and other metadata
    ///    - Handles static file requests if the route points to a file and static file serving is enabled
    ///    - For non-file requests, extracts the request body and creates an Http11Request object
    ///    - Creates a scoped service provider for dependency injection
    ///    - Executes the middleware pipeline against the request context
    ///    
    /// 3. Special Cases:
    ///    - Handles WebSocket connection upgrades
    ///    - Validates request format and throws exceptions for invalid requests
    ///    
    /// The method supports HTTP/1.1 features including persistent connections, allowing
    /// multiple requests to be processed over the same TCP connection for improved performance.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// 
    /// <exception cref="FileLoadException"/>
    /// <exception cref="FileNotFoundException"/>
    /// 
    /// <exception cref="BadImageFormatException"/>
    /// 
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="OperationCanceledException"/>
    /// 
    /// <exception cref="EncoderFallbackException"/>
    /// <exception cref="DecoderFallbackException"/>
    /// 
    /// <exception cref="ArrayTypeMismatchException"/>
    /// 
    /// <exception cref="ObjectDisposedException"/>
    //public async Task HandleClientAsync(Stream stream, Func<TContext, Task<IResponse>> pipeline, CancellationToken stoppingToken)
    public async Task HandleClientAsync2(Stream stream, Func<TContext, Task> pipeline ,CancellationToken stoppingToken)
    {
        // Create a new context for this client connection
        var context = new TContext
        {
            Stream = stream,
        };

        var pipeReader = PipeReader.Create(stream,
            new StreamPipeReaderOptions(MemoryPool<byte>.Shared, leaveOpen: true, bufferSize: 65535));

        // Read the initial client request headers
        //var headers = await ExtractHeaders(pipeReader, stoppingToken);

#if NET9_0_OR_GREATER

        var headers = await ExtractHeadersAsync(pipeReader, stoppingToken);

#else

        var buffer = await pipeReader.ReadAsync(stoppingToken);
        var headers = ExtractHeadersSync(buffer.Buffer, out var consumed);

#endif


        // Loop to handle multiple requests for "keep-alive" connections
        while (headers != null)
        {
            // Determine the connection type based on headers
            //var connection = GetConnectionType(headers);

            var connection = headers.TryGetValue("Connection-Type", out var connectionValue)
                ? GetConnectionType2(connectionValue)
                : ConnectionType.Close;

            // Handle WebSocket upgrade requests
            if (connection == ConnectionType.Websocket)
                await SendHandshakeResponse(context, ToRawHeaderString(headers));

            // Split the headers into individual lines for processing
            //var headerEntries = headers.Split("\r\n");

            // Extract the URI and HTTP method from the request line

            var uri = headers.TryGetValue(":Request-Line", out var requestLine)
                ? TryExtractUri2(requestLine, out (string httpMethod, string uriParams) uriHeader)
                : throw new InvalidOperationException("Invalid request received.");

            /*var result = TryExtractUri2(headers, out (string, string) uriHeader);
            if (!result)
            {
                Console.WriteLine("Invalid request received, unable to parse route");
                throw new InvalidOperationException("Invalid request received, unable to parse route");
            }*/

            // Split the URI into route and query string parts
            var uriParams = uriHeader.uriParams.Split('?');

            // Check if the request is for a static file
            if (UseResources & IsRouteFile(uriParams[0]))
            {
                // Serve the static file from embedded resources
                await FlushResource(stream, uriParams);
            }
            else
            {
                // This is a dynamic request - extract the request body
                //var body = await ExtractBody(pipeReader, headers, stoppingToken);
                var body = new byte[] { 0x01 };

                // Build the complete HTTP request object with all parsed components
                context.Request = new Http11Request(
                    Headers: headers,
                    Body: body,
                    Route: uriParams[0],
                    QueryParameters: uriParams.Length > 1 ? uriParams[1] : string.Empty,
                    HttpMethod: uriHeader.httpMethod);


                await pipeline(context);

                headers.Dispose();
            }

            // For keep-alive connections, try to read the next request
            // Otherwise, exit the loop and close the connection
            if (connection == ConnectionType.KeepAlive)
            {
                //headers = await ExtractHeaders(pipeReader, stoppingToken);

#if NET9_0_OR_GREATER

                headers = await ExtractHeadersAsync(pipeReader, stoppingToken);

#else

                buffer = await pipeReader.ReadAsync(stoppingToken);
                headers = ExtractHeadersSync(buffer.Buffer, out var consumedAgain);
                consumed = consumedAgain;
#endif
            }
            else
            {
                break;
            }
        }
    }

    public async Task HandleClientAsync(Stream stream, Func<TContext, Task> pipeline, CancellationToken stoppingToken)
    {
        // Create a new context for this client connection
        var context = new TContext
        {
            Stream = stream,
        };

        var pipeReader = PipeReader.Create(stream,
            new StreamPipeReaderOptions(MemoryPool<byte>.Shared, leaveOpen: true, bufferSize: 65535));

        // Read the initial client request headers
        var headers = await ExtractHeaders(pipeReader, stoppingToken);

        // Loop to handle multiple requests for "keep-alive" connections
        while (headers != null)
        {
            // Determine the connection type based on headers
            var connection = GetConnectionType(headers);

            // Handle WebSocket upgrade requests
            if (connection == ConnectionType.Websocket)
                await SendHandshakeResponse(context, headers);

            // Split the headers into individual lines for processing
            var headerEntries = headers.Split("\r\n");

            // Extract the URI and HTTP method from the request line
            var result = TryExtractUri2(headerEntries[0], out (string httpMethod, string uriParams) uriHeader);
            if (!result)
            {
                Console.WriteLine("Invalid request received, unable to parse route");
                throw new InvalidOperationException("Invalid request received, unable to parse route");
            }

            // Split the URI into route and query string parts
            var uriParams = uriHeader.uriParams.Split('?');

            // Check if the request is for a static file
            if (UseResources & IsRouteFile(uriParams[0]))
            {
                // Serve the static file from embedded resources
                await FlushResource(stream, uriParams);
            }
            else
            {
                // This is a dynamic request - extract the request body
                //var body = await ExtractBody(pipeReader, headers, stoppingToken);
                var body = await ExtractBody(pipeReader, headers, stoppingToken);

                // Build the complete HTTP request object with all parsed components
                context.Request = new Http11Request(
                    Headers: null!,
                    Body: body,
                    Route: uriParams[0],
                    QueryParameters: uriParams.Length > 1 ? uriParams[1] : string.Empty,
                    HttpMethod: uriHeader.httpMethod);


                await pipeline(context);
            }

            // For keep-alive connections, try to read the next request
            // Otherwise, exit the loop and close the connection
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

    private static string ToRawHeaderString(PooledDictionary<string, string> headers)
    {
        // Estimate capacity: 32 headers × 40 chars average
        var builder = new StringBuilder(2048);

        // Write request line first
        if (headers.TryGetValue(":Request-Line", out var requestLine))
        {
            builder.Append(requestLine).Append("\r\n");
        }

        foreach (var (key, value) in headers)
        {
            if (key == ":Request-Line") continue;
            builder.Append(key).Append(": ").Append(value).Append("\r\n");
        }

        // Terminate header block
        builder.Append("\r\n");

        return builder.ToString();
    }
}