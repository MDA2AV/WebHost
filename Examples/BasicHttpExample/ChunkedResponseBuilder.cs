using System.Text;
using WebHost;
using WebHost.Extensions;

namespace BasicHttpExample;

/// <summary>
/// Builds and sends a chunked HTTP response.
/// Implements the IResponseBuilder interface to handle HTTP responses in a chunked transfer-encoding format.
/// </summary>
public class ChunkedResponseBuilder : IResponseBuilder
{
    /// <summary>
    /// Handles the HTTP response by sending a chunked response to the client.
    /// </summary>
    /// <param name="context">The context representing the client-server interaction.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    public async Task HandleAsync(IContext context, CancellationToken cancellationToken = default)
    {
        // The header for the chunked HTTP response, stored as a ReadOnlyMemory<byte>.
        // This includes the status line, content type, transfer-encoding, and connection headers.
        // Content: "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nTransfer-Encoding: chunked\r\nConnection: keep-alive\r\n\r\n
        var chunkedHeader = new ReadOnlyMemory<byte>(
        [
            0x48, 0x54, 0x54, 0x50, 0x2F, 0x31, 0x2E, 0x31, 0x20, 0x32, 0x30, 0x30, 0x20, 0x4F, 0x4B, 0x0D,
            0x0A, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x6E, 0x74, 0x2D, 0x54, 0x79, 0x70, 0x65, 0x3A, 0x20, 0x74,
            0x65, 0x78, 0x74, 0x2F, 0x68, 0x74, 0x6D, 0x6C, 0x0D, 0x0A, 0x54, 0x72, 0x61, 0x6E, 0x73, 0x66,
            0x65, 0x72, 0x2D, 0x45, 0x6E, 0x63, 0x6F, 0x64, 0x69, 0x6E, 0x67, 0x3A, 0x20, 0x63, 0x68, 0x75,
            0x6E, 0x6B, 0x65, 0x64, 0x0D, 0x0A, 0x43, 0x6F, 0x6E, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x6F, 0x6E,
            0x3A, 0x20, 0x6B, 0x65, 0x65, 0x70, 0x2D, 0x61, 0x6C, 0x69, 0x76, 0x65, 0x0D, 0x0A, 0x0D, 0x0A
        ]);

        // Flush the header
        //
        await context.SendAsync(chunkedHeader, cancellationToken);

        // Flush the chunks
        //
        var chunks = new[] { "Chunked ", "response ", "data." };

        foreach (var chunk in chunks)
        {
            var chunkBytes = Encoding.UTF8.GetBytes(chunk);
            var chunkSize = chunkBytes.Length.ToString("X"); // Hexadecimal size

            // Write chunk size, data, and CRLF
            await context.SendAsync($"{chunkSize}\r\n", cancellationToken);
            await context.SendAsync(chunkBytes, cancellationToken);
            await context.SendAsync("\r\n"u8.ToArray(), cancellationToken);
        }

        // 0\r\n\r\n terminator
        //
        await context.SendAsync(new ReadOnlyMemory<byte>([0x30, 0x0D, 0x0A, 0x0D, 0x0A]), cancellationToken);
    }
}