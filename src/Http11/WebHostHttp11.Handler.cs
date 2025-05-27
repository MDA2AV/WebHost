using System.IO.Pipelines;
using System.Text;
using WebHost.Http11.Context;

namespace WebHost.Http11;

public partial class WebHostHttp11
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

    /// <summary>
    /// Handles a client connection by processing incoming HTTP/1.1 requests and executing the middleware pipeline.
    /// </summary>
    /// <param name="stream">The network stream for communication with the client.</param>
    /// <param name="pipeReader">A pipe reader for efficiently reading data from the client.</param>
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
    public async Task HandleClientAsync(Stream stream, PipeReader pipeReader, Func<IContext, Task> pipeline, CancellationToken stoppingToken)
    {
        // Create a new context for this client connection
        var context = new Http11Context
        {
            Stream = stream,
            PipeReader = pipeReader
        };

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
            var result = TryExtractUri2(headerEntries[0], out (string, string) uriHeader);
            if (!result)
            {
                Console.WriteLine("Invalid request received, unable to parse route");
                throw new InvalidOperationException("Invalid request received, unable to parse route");
            }

            // Split the URI into route and query string parts
            var uriParams = uriHeader.Item2.Split('?');

            // Check if the request is for a static file
            if (IsRouteFile(uriParams[0]) & UseResources)
            {
                // Serve the static file from embedded resources
                await FlushResource(stream, uriParams);
            }
            else
            {
                // This is a dynamic request - extract the request body
                var body = await ExtractBody(pipeReader, headers, stoppingToken);

                // Build the complete HTTP request object with all parsed components
                context.Request = new Http11Request(
                    Headers: headerEntries,
                    Body: body,
                    Route: uriParams[0],
                    QueryParameters: uriParams.Length > 1 ? uriParams[1] : string.Empty,
                    HttpMethod: uriHeader.Item1);

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
}