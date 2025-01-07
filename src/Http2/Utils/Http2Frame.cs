namespace WebHost.Http2.Utils;

public static class Http2Frame
{
    public static byte[] CreateHttp2Frame(byte type, byte flags, int streamId, byte[] payload)
    {
        var frame = new List<byte>
        {
            // Add the length (24 bits)
            (byte)((payload.Length >> 16) & 0xFF),
            (byte)((payload.Length >> 8) & 0xFF),
            (byte)(payload.Length & 0xFF),
            // Add the type
            type,
            // Add the flags
            flags,
            // Add the stream ID (31 bits, MSB is reserved and must be 0)
            (byte)((streamId >> 24) & 0x7F),
            (byte)((streamId >> 16) & 0xFF),
            (byte)((streamId >> 8) & 0xFF),
            (byte)(streamId & 0xFF)
        };

        // Add the payload
        frame.AddRange(payload);

        return frame.ToArray();
    }
}
