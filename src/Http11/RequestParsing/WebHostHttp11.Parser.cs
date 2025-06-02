using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;

namespace WebHost.Http11;

public sealed partial class WebHostHttp11<TContext>
{
    /// <summary>
    /// Attempts to extract the HTTP method and uri path from a collection of headers.
    /// </summary>
    /// <param name="headers">The collection of headers to parse.</param>
    /// <param name="route">
    /// When the method returns <c>true</c>, contains a tuple of the HTTP method (e.g., GET, POST) and the uri path (e.g., /api/resource?param=1).
    /// </param>
    /// <returns>
    /// <c>true</c> if the HTTP method and route path were successfully extracted; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="RegexMatchTimeoutException"/>
    public static bool TryExtractUri(IEnumerable<string>? headers, out (string, string) route)
    {
        if (headers == null)
        {
            System.Diagnostics.Debug.WriteLine("Headers is null");
            route = (null!, null!);
            return false;
        }

        const string pattern = @"^\s*(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(/[^\s]*)\s+HTTP/\d\.\d\s*$";

        route = default;

        var match = headers
            .Select(header => Regex.Match(header, pattern))
            .FirstOrDefault(m => m.Success);

        if (match is { Success: true })
        {
            var method = match.Groups[1].Value; // Extract HTTP method
            var path = match.Groups[2].Value;   // Extract URI including query parameters

            route = (method, path);

            return true;
        }

        Console.WriteLine($"Endpoint was not found.");
        return false;
    }

    /// <summary>
    /// Attempts to extract the HTTP method and uri path from a collection of headers.
    /// </summary>
    /// <param name="header">The collection of headers to parse.</param>
    /// <param name="route">
    /// When the method returns <c>true</c>, contains a tuple of the HTTP method (e.g., GET, POST) and the uri path (e.g., /api/resource?param=1).
    /// </param>
    /// <returns>
    /// <c>true</c> if the HTTP method and route path were successfully extracted; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="RegexMatchTimeoutException"/>
    public static bool TryExtractUri2(string? header, out (string, string) route)
    {
        if (header is null)
        {
            System.Diagnostics.Debug.WriteLine("Headers is null");
            route = (null!, null!);
            return false;
        }

        const string pattern = @"^\s*(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(/[^\s]*)\s+HTTP/\d\.\d\s*$";

        route = default;

        var match = Regex.Match(header, pattern);
        var result = match.Success;

        if (result)
        {
            var method = match.Groups[1].Value; // Extract HTTP method
            var path = match.Groups[2].Value;   // Extract URI including query parameters

            route = (method, path);

            return true;
        }

        Console.WriteLine($"Endpoint was not found.");
        return false;
    }

    /// <summary>
    /// Splits the raw HTTP request into headers and body.
    /// </summary>
    /// <param name="rawRequest">The raw HTTP request string.</param>
    /// <returns>
    /// A tuple containing the headers as an array of strings and the body as a string.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the raw request is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the raw request is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the raw request is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request is malformed, such as missing the header-body separator or having an invalid Content-Length header.
    /// </exception>
    public static (string[] Headers, string Body) SplitHeadersAndBody(string? rawRequest)
    {
        if (rawRequest is null)
        {
            throw new ArgumentNullException(nameof(rawRequest));
        }

        // Split into headers and body candidate based on the first \r\n\r\n
        var separatorIndex = rawRequest.IndexOf("\r\n\r\n", StringComparison.Ordinal);

        if (separatorIndex == -1)
        {
            throw new InvalidOperationException("Malformed HTTP request: No header-body separator found.");
        }

        // Extract headers part
        var headersPart = rawRequest.Substring(0, separatorIndex);
        var bodyCandidate = rawRequest.Substring(separatorIndex + 4); // Skip \r\n\r\n

        // Split headers into lines
        var headers = headersPart.Split("\r\n");

        // Find Content-Length header
        var contentLength = 0;
        foreach (var header in headers)
        {
            if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lengthValue = header["Content-Length:".Length..].Trim();

            if (!int.TryParse(lengthValue, out contentLength))
            {
                throw new InvalidOperationException("Invalid Content-Length header.");
            }

            break;
        }

        // Extract the body based on Content-Length
        var body = contentLength > 0 && bodyCandidate.Length >= contentLength
            ? bodyCandidate[..contentLength]
            : string.Empty;

        return (headers, body);
    }

    /// <summary>
    /// Extracts HTTP headers from the pipe reader stream.
    /// </summary>
    /// <param name="reader">The pipe reader containing the incoming HTTP data.</param>
    /// <param name="stoppingToken">A cancellation token to stop the operation.</param>
    /// <returns>
    /// A string containing the HTTP headers if found, or null if the end of the stream is reached
    /// before headers are completely read.
    /// </returns>
    /// <remarks>
    /// This method reads from the pipe until it finds the standard HTTP header terminator sequence
    /// (two consecutive CRLF pairs, or "\r\n\r\n"). The method handles both complete headers in a
    /// single read and fragmented headers across multiple reads.
    /// 
    /// The pipe reader position is advanced to just after the header terminator, leaving
    /// any subsequent body data available for the next read operation.
    /// </remarks>
    /// <exception cref="OperationCanceledException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="DecoderFallbackException"/>
    public static async Task<string?> ExtractHeaders(PipeReader reader, CancellationToken stoppingToken)
    {
        // Continue reading until we find headers or reach end of stream
        while (true)
        {
            // Read from the pipe
            var result = await reader.ReadAsync(stoppingToken);
            var buffer = result.Buffer;

            // Try to find the header terminator sequence (\r\n\r\n)
            if (TryAdvanceTo(new SequenceReader<byte>(buffer), "\r\n\r\n"u8, out var position))
            {
                // Convert the header portion to a string
                var res = Encoding.UTF8.GetString(buffer.Slice(0, position).ToArray());

                // Advance the reader past the headers
                reader.AdvanceTo(position);

                return res;
            }

            // If delimiter not found in current buffer, mark everything as examined
            // but not consumed, so we can continue the search with more data
            reader.AdvanceTo(buffer.Start, buffer.End);

            // If we've reached the end of the stream without finding headers
            if (result.IsCompleted)
            {
                return null; // End of stream, no complete headers found
            }
        }
    }

    /// <summary>
    /// Reads and extracts a chunk of data from a <see cref="PipeReader"/> stream,
    /// following the chunked transfer encoding format.
    /// </summary>
    /// <param name="reader">The <see cref="PipeReader"/> to read data from.</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="byte"/> array containing the extracted chunk,
    /// an empty array (<c>[]</c>) if the final chunk ("0\r\n\r\n") is reached,
    /// or <c>null</c> if the stream ends before a valid chunk is found.
    /// </returns>
    /// <remarks>
    /// - This method continuously reads from the <paramref name="reader"/> until it finds a valid chunk.
    /// - It supports chunked transfer encoding, where each chunk is separated by a <c>"\r\n"</c> sequence.
    /// - The method detects the end of a chunked message when it encounters the last chunk indicator <c>"0\r\n\r\n"</c>.
    /// - If no complete chunk is found before the end of the stream, <c>null</c> is returned.
    /// </remarks>
    /// <exception cref="OperationCanceledException"/>
    public static async Task<byte[]?> ExtractChunk(PipeReader reader, CancellationToken stoppingToken)
    {
        while (true)
        {
            // Read from the pipe
            var result = await reader.ReadAsync(stoppingToken);
            var buffer = result.Buffer;

            // Check for last chunk (end of chunked transfer encoding)
            if (TryAdvanceTo(new SequenceReader<byte>(buffer), "0\r\n\r\n"u8, out var lastChunkPosition))
            {
                reader.AdvanceTo(lastChunkPosition); // Consume the final chunk indicator
                return []; // Signal last chunk read
            }

            // Try to find the header terminator sequence (\r\n) - read a chunk
            if (TryAdvanceTo(new SequenceReader<byte>(buffer), "\r\n"u8, out var position))
            {
                var chunk = buffer.Slice(0, position).ToArray();

                // Advance the reader past the headers
                reader.AdvanceTo(position);

                return chunk;
            }

            // If delimiter not found in current buffer, mark everything as examined
            // but not consumed, so we can continue the search with more data
            reader.AdvanceTo(buffer.Start, buffer.End);

            // If we've reached the end of the stream without finding headers
            if (result.IsCompleted)
            {
                return null; // End of stream, no complete headers found
            }
        }
    }

    /// <summary>
    /// Searches for a specific byte sequence in a ReadOnlySequence and advances to that position.
    /// </summary>
    /// <param name="reader">The sequence reader to search within.</param>
    /// <param name="delimiter">The byte sequence to find.</param>
    /// <param name="position">
    /// When this method returns, contains the position immediately after the delimiter
    /// if found; otherwise, the current position.
    /// </param>
    /// <returns>
    /// true if the delimiter was found; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method handles searching across multiple segments of a ReadOnlySequence,
    /// making it suitable for finding delimiters that might span segment boundaries.
    /// When the delimiter is found, the reader is advanced to the position immediately
    /// after the delimiter.
    /// </remarks>
    private static bool TryAdvanceTo(SequenceReader<byte> reader, ReadOnlySpan<byte> delimiter, out SequencePosition position)
    {
        // Start from the current position
        position = reader.Position;

        // Continue until we reach the end of the sequence
        while (!reader.End)
        {
            // Get the current unread span (current segment)
            var span = reader.UnreadSpan;

            // Try to find the delimiter in the current span
            var index = span.IndexOf(delimiter);
            if (index != -1)
            {
                // Delimiter found - calculate the position after the delimiter
                position = reader.Sequence.GetPosition(index + delimiter.Length, reader.Position);

                // Move the reader past the delimiter
                reader.Advance(index + delimiter.Length);

                return true;
            }

            // Move to the next segment if not found in the current span
            reader.Advance(span.Length);
        }

        // Delimiter not found in the entire sequence
        return false;
    }

    /// <summary>
    /// Extracts the HTTP request body from the pipe reader stream based on the Content-Length header.
    /// </summary>
    /// <param name="reader">The pipe reader containing the request body data.</param>
    /// <param name="headers">The HTTP headers string containing the Content-Length header.</param>
    /// <param name="stoppingToken">A cancellation token to stop the operation.</param>
    /// <returns>
    /// A string containing the request body if Content-Length is valid and the body is read successfully;
    /// otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method:
    /// 1. Parses the Content-Length header to determine how many bytes to read
    /// 2. Attempts to read the body in a single operation if possible
    /// 3. Falls back to reading the body in fragments if necessary
    /// 4. Decodes the body bytes using UTF-8 encoding
    /// 
    /// If the connection closes before reading the complete body (based on Content-Length),
    /// an InvalidOperationException is thrown.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stream ends unexpectedly before reading the complete body.
    /// </exception>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="ArrayTypeMismatchException"/>
    /// <exception cref="OperationCanceledException"/>
    public static async Task<byte[]?> ExtractBody(PipeReader reader, string headers, CancellationToken stoppingToken)
    {
        // Try to get the Content-Length from headers
        if (!TryGetContentLength(headers, out var contentLength))
            return null; // No Content-Length header or invalid value

        // Request hasn't body
        if (contentLength == 0)
            return [];

        // Allocate a buffer to store the body
        var bodyBuffer = new byte[contentLength];
        var bytesRead = 0;

        // Read initial data from the PipeReader
        var result = await reader.ReadAsync(stoppingToken);
        var buffer = result.Buffer;

        // Optimize for common case: entire body available in one read
        if (buffer.Length >= contentLength)
        {
            // Copy the body data to our buffer
            buffer.Slice(0, contentLength).CopyTo(bodyBuffer);

            // Advance the reader past the body
            reader.AdvanceTo(buffer.GetPosition(contentLength));

            return bodyBuffer;
            //return Encoding.UTF8.GetString(bodyBuffer);
        }

        // Handle fragmented body (less common case)
        while (bytesRead < contentLength)
        {
            // Read more data from the PipeReader
            result = await reader.ReadAsync(stoppingToken);
            buffer = result.Buffer;

            // Calculate how much to read from current buffer
            var toRead = Math.Min(buffer.Length, contentLength - bytesRead);

            // Copy the data to our body buffer
            buffer.Slice(0, toRead).CopyTo(bodyBuffer.AsMemory(bytesRead).Span);
            bytesRead += (int)toRead;

            // Advance the PipeReader
            reader.AdvanceTo(buffer.GetPosition(toRead));

            // Check for premature end of stream
            if (result.IsCompleted && bytesRead < contentLength)
            {
                throw new InvalidOperationException("Unexpected end of stream while reading the body.");
            }
        }

        // Decode and return the complete body
        //return Encoding.UTF8.GetString(bodyBuffer);
        return bodyBuffer;
    }

    /// <summary>
    /// Attempts to extract the Content-Length value from HTTP headers.
    /// </summary>
    /// <param name="headers">The HTTP headers string to parse.</param>
    /// <param name="contentLength">
    /// When this method returns, contains the parsed Content-Length value if successful;
    /// otherwise, 0.
    /// </param>
    /// <returns>
    /// true if the Content-Length header was found and successfully parsed as an integer;
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method performs a case-insensitive search for the "Content-Length:" header
    /// and parses its value as an integer. The method handles headers with or without
    /// trailing CRLF sequence.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    private static bool TryGetContentLength(string headers, out int contentLength)
    {
        contentLength = 0;

        // Look for Content-Length header (case-insensitive)
        var contentLengthKey = "Content-Length:";
        var startIndex = headers.IndexOf(contentLengthKey, StringComparison.OrdinalIgnoreCase);

        if (startIndex == -1)
        {
            return false; // Header not found
        }

        // Move past the header name to the value
        startIndex += contentLengthKey.Length;

        // Find the end of the header value (next CRLF or end of string)
        var endIndex = headers.IndexOf("\r\n", startIndex);
        if (endIndex == -1)
        {
            // Handle case where Content-Length is the last header
            endIndex = headers.Length;
        }

        // Extract and trim the value
        var contentLengthValue = headers[startIndex..endIndex].Trim();

        // Try to parse the value as an integer
        return int.TryParse(contentLengthValue, out contentLength);
    }
}