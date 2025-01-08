using System.IO;
using System.Runtime.CompilerServices;
using WebHost.Hpack;
using WebHost.Models;

namespace WebHost.Http2.Extensions;

public static class Http2ContextExtensions
{
    public static byte[] CreateHeadersFrame(
        this Http2Context context, 
        Func<byte[], int, int, byte[]> frameBuilder, 
        IEnumerable<HeaderField> headers,
        int bufferLength = 1024)
    {
        // Allocate a buffer for the header block
        var buffer = new byte[bufferLength];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        if (result.FieldCount == 0)
        {
            Console.WriteLine("Failed to encode headers: Buffer too small.");
            return [];
        }

        return frameBuilder(buffer, result.UsedBytes, Unsafe.As<Http2Request>(context.Request).StreamId);
    }

    public static void Http2SendAsync(
        this Http2Context context,
        IEnumerable<HeaderField> headers,
        byte type, 
        byte flags, 
        int streamId, 
        ReadOnlySpan<byte> payload, 
        int stackSizeLimit = 1024,
        int bufferLength = 1024)
    {
        // Allocate a buffer for the header block
        var buffer = new byte[bufferLength];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        if (result.FieldCount == 0)
        {
            Console.WriteLine("Failed to encode headers: Buffer too small.");
            return;
        }

        // Calculate the total frame size
        var frameLength = 9 + payload.Length; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= stackSizeLimit // Adjust the limit based on your needs
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((payload.Length >> 16) & 0xFF);
        frameSpan[1] = (byte)((payload.Length >> 8) & 0xFF);
        frameSpan[2] = (byte)(payload.Length & 0xFF);

        // Add the type
        frameSpan[3] = type;

        // Add the flags
        frameSpan[4] = flags;

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        frameSpan[5] = (byte)((streamId >> 24) & 0x7F); // MSB is reserved
        frameSpan[6] = (byte)((streamId >> 16) & 0xFF);
        frameSpan[7] = (byte)((streamId >> 8) & 0xFF);
        frameSpan[8] = (byte)(streamId & 0xFF);

        // Add the payload
        payload.CopyTo(frameSpan.Slice(9));

    }
}
