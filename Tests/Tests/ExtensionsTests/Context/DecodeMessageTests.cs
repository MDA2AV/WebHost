using System.Text;
using WebHost.Http11.Websockets;

namespace Tests.ExtensionsTests.Context;

public class WebSocketDecodeTests
{
    [Fact]
    public void DecodeMessage_WithUnmaskedFrame_ReturnsCorrectMessage()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("Hello");
        var frame = new byte[2 + payload.Length];
        frame[0] = 0x81; // Final frame, text data
        frame[1] = (byte)payload.Length; // Payload length
        Array.Copy(payload, 0, frame, 2, payload.Length);

        // Act
        var result = WebsocketUtilities.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void DecodeMessage_WithMaskedFrame_ReturnsCorrectMessage()
    {
        // Arrange
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
        var result = WebsocketUtilities.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void DecodeMessage_WithExtendedPayload_ReturnsCorrectMessage()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes(new string('A', 126));
        var frame = new byte[4 + payload.Length];
        frame[0] = 0x81; // Final frame, text data
        frame[1] = 126; // Extended payload length
        frame[2] = 0x00; // High byte of length
        frame[3] = 126; // Low byte of length
        Array.Copy(payload, 0, frame, 4, payload.Length);

        // Act
        var result = WebsocketUtilities.DecodeMessage(frame, frame.Length);

        // Assert
        Assert.Equal(new string('A', 126), result);
    }

    [Fact]
    public void DecodeMessage_WithIncompleteFrame_ThrowsArgumentException()
    {
        // Arrange
        var frame = new byte[] { 0x81, 0x80 }; // Incomplete masked frame
        var memoryFrame = new Memory<byte>(frame); // Wrap the frame as Memory<byte>

        // Act & Assert
        Assert.Throws<ArgumentException>(() => WebsocketUtilities.DecodeMessage(memoryFrame, frame.Length));
    }
}
