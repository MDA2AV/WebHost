using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace WebHost;

public sealed partial class WebHostApp
{
    public static async Task<string?> ExtractHeaders(PipeReader reader, CancellationToken stoppingToken)
    {
        while (true)
        {
            var result = await reader.ReadAsync(stoppingToken);
            var buffer = result.Buffer;

            if (TryAdvanceTo(new SequenceReader<byte>(buffer), "\r\n\r\n"u8, out var position))
            {
                // Convert headers to string and return
                var res = Encoding.UTF8.GetString(buffer.Slice(0, position).ToArray());
                reader.AdvanceTo(position);
                return res;
            }

            // If delimiter not found, advance the reader to keep searching
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                return null; // End of stream
            }
        }
    }

    private static bool TryAdvanceTo(SequenceReader<byte> reader, ReadOnlySpan<byte> delimiter, out SequencePosition position)
    {
        position = reader.Position;

        // Iterate through the unread portion of the sequence
        while (!reader.End)
        {
            var span = reader.UnreadSpan;

            // Check if the delimiter fits within the current span
            var index = span.IndexOf(delimiter);
            if (index != -1)
            {
                // If found, calculate the position in the sequence
                position = reader.Sequence.GetPosition(index + delimiter.Length, reader.Position);
                reader.Advance(index + delimiter.Length); // Move past the delimiter
                return true;
            }

            // Move to the next segment if not found in the current span
            reader.Advance(span.Length);
        }

        // Delimiter not found
        return false;
    }

    public static async Task<string?> ExtractBody(PipeReader reader, string headers, CancellationToken stoppingToken)
    {
        if (!TryGetContentLength(headers, out var contentLength))
        {
            return null;
        }

        // Allocate a buffer to store the body
        var bodyBuffer = new byte[contentLength];
        var bytesRead = 0;

        // Read data from the PipeReader
        var result = await reader.ReadAsync(stoppingToken);
        var buffer = result.Buffer;

        // Check if one shot read
        if (buffer.Length == contentLength)
        {
            buffer.Slice(0, contentLength).CopyTo(bodyBuffer);
            reader.AdvanceTo(buffer.GetPosition(contentLength));
            return Encoding.UTF8.GetString(bodyBuffer);
        }

        // If fragmented body (should happen very rarely)
        while (bytesRead < contentLength)
        {
            // Read data from the PipeReader
            result = await reader.ReadAsync(stoppingToken);
            buffer = result.Buffer;

            // Read as much as possible from the current buffer
            var toRead = Math.Min(buffer.Length, contentLength - bytesRead);
            buffer.Slice(0, toRead).CopyTo(bodyBuffer.AsMemory(bytesRead).Span);
            bytesRead += (int)toRead;

            // Advance the PipeReader
            reader.AdvanceTo(buffer.GetPosition(toRead));

            // If the stream is completed but didn't finish reading the body
            if (result.IsCompleted && bytesRead < contentLength)
            {
                throw new InvalidOperationException("Unexpected end of stream while reading the body.");
            }
        }
        return Encoding.UTF8.GetString(bodyBuffer);
    }

    private static bool TryGetContentLength(string headers, out int contentLength)
    {
        contentLength = 0;

        // Check for Content-Length header in a case-insensitive manner
        var contentLengthKey = "Content-Length:";
        var startIndex = headers.IndexOf(contentLengthKey, StringComparison.OrdinalIgnoreCase);

        if (startIndex == -1)
        {
            return false;
        }

        // Extract the Content-Length value
        startIndex += contentLengthKey.Length;
        var endIndex = headers.IndexOf("\r\n", startIndex);

        if (endIndex == -1)
        {
            endIndex = headers.Length; // No newline; assume the end of the string
        }

        var contentLengthValue = headers[startIndex..endIndex].Trim();

        return int.TryParse(contentLengthValue, out contentLength);
    }
}
