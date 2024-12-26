using System.Text;
using WebHost;
using WebHost.Extensions;

namespace Tests.ExtensionsTests.Context;

public class WebSocketDecodeTests
{
    [Fact]
    public void DecodeMessage_WithUnmaskedFrame_ReturnsCorrectMessage()
    {
        // Arrange
        var context = new WebHost.Models.Context(null!);
        var payload = Encoding.UTF8.GetBytes("Hello");
        var frame = new byte[2 + payload.Length];
        frame[0] = 0x81; // Final frame, text data
        frame[1] = (byte)payload.Length; // Payload length
        Array.Copy(payload, 0, frame, 2, payload.Length);

        // Act
        var result = context.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void DecodeMessage_WithMaskedFrame_ReturnsCorrectMessage()
    {
        // Arrange
        var context = new WebHost.Models.Context(null!);
        var payload = Encoding.UTF8.GetBytes("Hello");
        var maskKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var maskedPayload = new byte[payload.Length];
        for (var i = 0; i < payload.Length; i++)
        {
            maskedPayload[i] = (byte)(payload[i] ^ maskKey[i % 4]);
        }

        var frame = new byte[2 + 4 + maskedPayload.Length];
        frame[0] = 0x81; // Final frame, text data
        frame[1] = (byte)(0x80 | maskedPayload.Length); // Masked payload length
        Array.Copy(maskKey, 0, frame, 2, 4);
        Array.Copy(maskedPayload, 0, frame, 6, maskedPayload.Length);

        // Act
        var result = context.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void DecodeMessage_WithExtendedPayload_ReturnsCorrectMessage()
    {
        // Arrange
        var context = new WebHost.Models.Context(null!);
        var payload = Encoding.UTF8.GetBytes(new string('A', 126));
        var frame = new byte[4 + payload.Length];
        frame[0] = 0x81; // Final frame, text data
        frame[1] = 126; // Extended payload length
        frame[2] = 0x00; // High byte of length
        frame[3] = 126; // Low byte of length
        Array.Copy(payload, 0, frame, 4, payload.Length);

        // Act
        var result = context.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal(new string('A', 126), result);
    }

    [Fact]
    public void DecodeMessage_WithIncompleteFrame_ThrowsArgumentException()
    {
        // Arrange
        var context = new WebHost.Models.Context(null!);
        var frame = new byte[] { 0x81, 0x80 }; // Incomplete masked frame
        var memoryFrame = new Memory<byte>(frame); // Wrap the frame as Memory<byte>

        // Act & Assert
        Assert.Throws<ArgumentException>(() => context.DecodeMessage(memoryFrame, frame.Length));
    }

    /*
    // Mock context implementing the IContext interface
    private class MockContext : IContext
    {
        public string DecodeMessage(Memory<byte> buffer, int length)
        {
            var span = buffer.Span;

            // Check the MASK bit
            var isMasked = (span[1] & 0x80) != 0;

            // Extract payload length
            var payloadLength = span[1] & 0x7F;
            var payloadStart = 2;

            switch (payloadLength)
            {
                case 126:
                    payloadLength = (span[2] << 8) | span[3];
                    payloadStart = 4;
                    break;
                case 127:
                    payloadLength = (int)(
                        ((ulong)span[2] << 56) |
                        ((ulong)span[3] << 48) |
                        ((ulong)span[4] << 40) |
                        ((ulong)span[5] << 32) |
                        ((ulong)span[6] << 24) |
                        ((ulong)span[7] << 16) |
                        ((ulong)span[8] << 8) |
                        span[9]);
                    payloadStart = 10;
                    break;
            }

            var maskKey = Array.Empty<byte>();
            if (isMasked)
            {
                maskKey = span.Slice(payloadStart, 4).ToArray();
                payloadStart += 4;
            }

            var payload = span.Slice(payloadStart, payloadLength).ToArray();

            if (!isMasked)
            {
                return Encoding.UTF8.GetString(payload);
            }

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] ^= maskKey[i % 4];
            }

            return Encoding.UTF8.GetString(payload);
        }
    }
    */
}
