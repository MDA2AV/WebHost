using System.Text;

namespace WebHost.Http11.Extensions;

public static class HttpResponseExtensions
{
    /// <summary>
    /// Serializes an HttpResponseMessage into a raw HTTP response format as a ReadOnlyMemory.
    /// </summary>
    /// <param name="response">The HttpResponseMessage to serialize.</param>
    /// <returns>A ReadOnlyMemory representing the raw HTTP response, including the status line, headers, and body.</returns>
    /// <remarks>
    /// This method constructs the HTTP response manually, adhering to the HTTP protocol:
    /// - The status line is written first (e.g., "HTTP/1.1 200 OK").
    /// - Headers are added, separated by "\r\n".
    /// - An empty line ("\r\n") is inserted between headers and the body.
    /// - The response body is included last, encoded in UTF-8.
    /// This ensures the response is fully serialized and can be sent over a socket or saved as raw HTTP data.
    /// </remarks>
    /// <exception cref="OperationCanceledException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="EncoderFallbackException"/>
    /// <exception cref="OutOfMemoryException"/>
    public static async Task<ReadOnlyMemory<byte>> ToBytes(this HttpResponseMessage? response)
    {
        if (response is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        using var memoryStream = new MemoryStream();

        // Write the status line
        //
        var statusLine = $"HTTP/{response.Version.Major}.{response.Version.Minor} {(int)response.StatusCode} {response.ReasonPhrase}\r\n";
        var statusLineBytes = Encoding.UTF8.GetBytes(statusLine);
        await memoryStream.WriteAsync(statusLineBytes);

        // Write the headers
        //
        foreach (var header in response.Headers)
        {
            var headerLine = $"{header.Key}: {string.Join(", ", header.Value)}\r\n";
            var headerLineBytes = Encoding.UTF8.GetBytes(headerLine);
            await memoryStream.WriteAsync(headerLineBytes);
        }

        foreach (var header in response.Content.Headers)
        {
            var headerLine = $"{header.Key}: {string.Join(", ", header.Value)}\r\n";
            var headerLineBytes = Encoding.UTF8.GetBytes(headerLine);
            await memoryStream.WriteAsync(headerLineBytes);
        }

        // Write an empty line to separate headers from the body
        //
        var newlineBytes = Encoding.UTF8.GetBytes("\r\n");
        await memoryStream.WriteAsync(newlineBytes);

        // Write the body
        //
        if (response.Content == null)
        {
            return memoryStream.ToArray().AsMemory();
        }

        var bodyBytes = Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync());
        await memoryStream.WriteAsync(bodyBytes);

        // Return the serialized response as ReadOnlyMemory<byte>
        //
        return memoryStream.ToArray().AsMemory();
    }
}