using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace WebHost;

public sealed partial class WebHostApp
{
    public static async Task<(string, SequencePosition)> ExtractHeaders(PipeReader reader)
    {
        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            if (TryAdvanceTo(new SequenceReader<byte>(buffer), "\r\n\r\n"u8, out var position))
            {
                // Convert headers to string and return
                var res = Encoding.UTF8.GetString(buffer.Slice(0, position).ToArray());
                return (res, position);
            }

            // If delimiter not found, advance the reader to keep searching
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break; // End of stream
            }
        }

        throw new Exception("fds");
        //return (string.Empty, position); // No headers found
    }

    static bool TryAdvanceTo(SequenceReader<byte> reader, ReadOnlySpan<byte> delimiter, out SequencePosition position)
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
}
