namespace Tests.ExtensionsTests.Context;

public class WebSocketFrameTests
{
    [Fact]
    public void BuildWsFrame_WithEmptyPayload_ReturnsCorrectFrame()
    {
        // Arrange
        var payload = ReadOnlyMemory<byte>.Empty;

        // Act
        var frame = BuildWsFrame(payload);

        // Assert
        Assert.Equal(2, frame.Length); // 2 bytes for header
        Assert.Equal(0x81, frame.Span[0]); // Final frame, text data
        Assert.Equal(0x00, frame.Span[1]); // Payload length
    }

    [Fact]
    public void BuildWsFrame_WithSmallPayload_ReturnsCorrectFrame()
    {
        // Arrange
        var payload = new ReadOnlyMemory<byte>([0x01, 0x02, 0x03]);

        // Act
        var frame = BuildWsFrame(payload);

        // Assert
        Assert.Equal(5, frame.Length); // 2 bytes for header + 3 bytes for payload
        Assert.Equal(0x81, frame.Span[0]); // Final frame, text data
        Assert.Equal(0x03, frame.Span[1]); // Payload length
        Assert.Equal(0x01, frame.Span[2]); // Payload byte 1
        Assert.Equal(0x02, frame.Span[3]); // Payload byte 2
        Assert.Equal(0x03, frame.Span[4]); // Payload byte 3
    }

    [Fact]
    public void BuildWsFrame_WithMaximumPayloadLength_ReturnsCorrectFrame()
    {
        // Arrange
        var maxPayloadLength = 125; // Simplification: WebSocket small payloads
        var payload = new ReadOnlyMemory<byte>(new byte[maxPayloadLength]);

        // Act
        var frame = BuildWsFrame(payload);

        // Assert
        Assert.Equal(2 + maxPayloadLength, frame.Length); // Header + payload
        Assert.Equal(0x81, frame.Span[0]); // Final frame, text data
        Assert.Equal(maxPayloadLength, frame.Span[1]); // Payload length
    }

    [Fact]
    public void BuildWsFrame_VerifiesPayloadIsCopiedCorrectly()
    {
        // Arrange
        var payload = new ReadOnlyMemory<byte>([0xAA, 0xBB, 0xCC]);

        // Act
        var frame = BuildWsFrame(payload);

        // Assert
        Assert.Equal(0xAA, frame.Span[2]); // Payload byte 1
        Assert.Equal(0xBB, frame.Span[3]); // Payload byte 2
        Assert.Equal(0xCC, frame.Span[4]); // Payload byte 3
    }

    // Method under test (replicated here for demonstration purposes)
    private static ReadOnlyMemory<byte> BuildWsFrame(ReadOnlyMemory<byte> payload)
    {
        var responseLength = 2 + payload.Length;
        var response = new Memory<byte>(new byte[responseLength]);

        var span = response.Span;
        span[0] = 0x81; // Final frame, text data
        span[1] = (byte)payload.Length; // Payload length

        payload.CopyTo(response[2..]);

        return response;
    }
}
