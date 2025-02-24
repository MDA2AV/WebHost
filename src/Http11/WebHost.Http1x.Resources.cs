using System.Buffers;
using System.Text;

namespace WebHost;

public sealed partial class WebHostApp
{
    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

    private static bool IsRouteFile(string route) => Path.HasExtension(route);

    /// <summary>
    /// Sends an HTTP response containing the content of a requested resource file.
    /// </summary>
    /// <param name="stream">The network stream to write the HTTP response to.</param>
    /// <param name="uriParams">An array of URI parameters where the first element is the requested file path.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <remarks>
    /// This method performs the following operations:
    /// 1. Extracts the file path from the URI parameters
    /// 2. Reads the embedded resource file asynchronously
    /// 3. Constructs and sends an appropriate HTTP response
    /// 4. Uses buffer pooling to minimize heap allocations
    /// 5. Returns a 404 response if the file is not found
    /// </remarks>
    private async Task FlushResource(Stream stream, string[] uriParams)
    {
        // Extract the file path and remove leading slash to match the resource naming convention
        var filePath = uriParams[0].TrimStart('/');

        // Asynchronously read the embedded file content into a pooled buffer
        var (fileBuffer, fileLength) = await ReadEmbeddedFile(filePath, _resourcesPath);
        try
        {
            if (fileBuffer != null)
            {
                // File found - prepare 200 OK response

                // Use a pre-sized string builder (initial capacity 128) to avoid reallocations
                // when constructing the HTTP response header
                var headerBuilder = new StringBuilder(128);
                headerBuilder.Append("HTTP/1.1 200 OK\r\n")                         // Status line
                            .Append("Content-Type: ").Append(GetContentType(filePath)).Append("\r\n") // Content type based on file extension
                            .Append("Content-Length: ").Append(fileLength).Append("\r\n")             // Exact content length
                            .Append("\r\n");                                                          // Empty line signifying end of headers

                // Get the header as a string for byte count calculation
                var headerString = headerBuilder.ToString();

                // Determine exact byte count needed for the header to avoid over-allocation
                var headerLength = Encoding.UTF8.GetByteCount(headerString);

                // Rent a buffer from the shared pool for the header bytes
                var headerBuffer = BufferPool.Rent(headerLength);
                try
                {
                    // Encode the header string directly into the rented buffer
                    Encoding.UTF8.GetBytes(headerString, headerBuffer);

                    // Send header first - use Memory slice to write only the needed bytes
                    // This avoids sending any extra space in the rented buffer
                    await stream.WriteAsync(headerBuffer.AsMemory(0, headerLength));

                    // Then send the file content - use Memory slice to write only the actual bytes
                    // The rented buffer may be larger than the file data
                    await stream.WriteAsync(fileBuffer.AsMemory(0, fileLength));

                    // Ensure all data is sent immediately
                    await stream.FlushAsync();
                }
                finally
                {
                    // Return the header buffer to the pool regardless of success or exceptions
                    BufferPool.Return(headerBuffer);
                }
            }
            else
            {
                // File not found - send 404 response
                const string notFoundResponse = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n";
                var notFoundBytes = Encoding.UTF8.GetBytes(notFoundResponse);

                // Write 404 response and flush the stream
                await stream.WriteAsync(notFoundBytes.AsMemory());
                await stream.FlushAsync();
            }
        }
        finally
        {
            // Return the file buffer to the pool if it exists
            // This ensures we don't leak memory even if an exception occurs
            if (fileBuffer != null)
            {
                BufferPool.Return(fileBuffer);
            }
        }
    }

    /// <summary>
    /// Determines the appropriate MIME content type based on a file's extension.
    /// </summary>
    /// <param name="filePath">The path of the file to determine the content type for.</param>
    /// <returns>
    /// A string containing the MIME content type corresponding to the file's extension.
    /// Returns "application/octet-stream" for unknown file types.
    /// </returns>
    /// <remarks>
    /// Supports common web file types including HTML, CSS, JavaScript, JSON, and image formats.
    /// The method converts the extension to lowercase before matching to ensure case-insensitivity.
    /// </remarks>
    private static string GetContentType(string filePath)
    {
        // Extract the file extension and convert to lowercase for case-insensitive matching
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Use pattern matching to return the appropriate MIME type based on the extension
        return extension switch
        {
            ".html" => "text/html",               // HTML documents
            ".css" => "text/css",                 // CSS stylesheets
            ".js" => "application/javascript",    // JavaScript files
            ".json" => "application/json",        // JSON data
            ".png" => "image/png",                // PNG images
            ".jpg" => "image/jpeg",               // JPEG images
            ".gif" => "image/gif",                // GIF images
            _ => "application/octet-stream",      // Default for unknown file types
        };
    }

    /// <summary>
    /// Reads an embedded file content from embedded resources into a rented buffer.
    /// The caller is responsible for returning the buffer to BufferPool after use.
    /// </summary>
    private async Task<(byte[]?, int)> ReadEmbeddedFile(string fileName, string resourceNamespace)
    {
        var resourceName = $"{resourceNamespace}.{fileName}";

        await using var stream = _resourcesAssembly.GetManifestResourceStream(resourceName.Replace('/', '.'));
        if (stream == null)
        {
            return (null, 0);
        }

        var length = (int)stream.Length;
        var buffer = BufferPool.Rent(length);

        _ = await stream.ReadAsync(buffer);

        return (buffer, length);
    }
}
