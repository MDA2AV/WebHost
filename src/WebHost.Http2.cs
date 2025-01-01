using System.Net.Security;

namespace WebHost;

public sealed partial class WebHostApp
{
    public async Task HandleHttp2Connection(SslStream sslStream)
    {
        Memory<byte> frameHeader = new byte[9];
        Memory<byte> preface = new byte[24];

        _ = await sslStream.ReadAsync(preface);
        if (!Http2Preface.SequenceEqual(preface.Span))
        {
            throw new Exception("Invalid HTTP/2 preface");
        }

        await sslStream.WriteAsync(SettingsFrame);
        _ = await sslStream.ReadAsync(frameHeader);
        await sslStream.WriteAsync(SettingsAckFrame);

        _ = await sslStream.ReadAsync(frameHeader);
        int length = (frameHeader.Span[0] << 16) | (frameHeader.Span[1] << 8) | frameHeader.Span[2];

        Memory<byte> headerPayload = new byte[length];
        _ = await sslStream.ReadAsync(headerPayload);
        await sslStream.WriteAsync(CreateHttp2Response());
    }

    private static ReadOnlyMemory<byte> CreateHttp2Response() =>
        new byte[]
        {
            0x00, 0x00, 0x01,           // Length
            0x01,                       // Type (HEADERS)
            0x05,                       // Flags (END_STREAM | END_HEADERS)
            0x00, 0x00, 0x00, 0x01,     // Stream ID
            0x88                        // HPACK encoded status 200
        };

    private static ReadOnlySpan<byte> Http2Preface =>
        "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8;

    private static ReadOnlyMemory<byte> SettingsFrame =>
        new byte[]
        {
            0x00, 0x00, 0x00,           // Length
            0x04,                       // Type (SETTINGS)
            0x00,                       // Flags
            0x00, 0x00, 0x00, 0x00      // Stream ID
        };

    private static ReadOnlyMemory<byte> SettingsAckFrame =>
        new byte[]
        {
            0x00, 0x00, 0x00,           // Length
            0x04,                       // Type (SETTINGS)
            0x01,                       // Flags (ACK)
            0x00, 0x00, 0x00, 0x00      // Stream ID
        };
}