using WebHost.Hpack;
using WebHost.Models;

namespace WebHost.Http2.Extensions;

public static class Http2ContextExtensions
{
    public static byte[] EncodeFrame(this Http2Context context, Func<byte[], int, int, byte[]> builder, IEnumerable<HeaderField> headers)
    {
        // Allocate a buffer for the header block
        var buffer = new byte[1024];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        if (result.FieldCount == 0)
        {
            Console.WriteLine("Failed to encode headers: Buffer too small.");
            return [];
        }

        return builder(buffer, result.UsedBytes, ((Http2Request)context.Request).StreamId);
    }
}
