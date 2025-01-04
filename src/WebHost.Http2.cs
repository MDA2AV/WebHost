using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Http2.Hpack;
using static Http2.Hpack.DecoderExtensions;

namespace WebHost;

/*
 * Receive preface
 * Receive SETTINGS frame
 * Acknowledge the received SETTINGS frame
 * Send empty SETTINGS frame as server preface
 *
 *
 * Now server and client may begin exchanging frames..
 *
 * Frames are a 9 octect header followed bu a variable length payload
 *HTTP Frame {
     Length (24),
     Type (8),
   
     Flags (8),
   
     Reserved (1),
     Stream Identifier (31),
   
     Frame Payload (..),
   }


 */

public class FrameData
{
    public int PayloadLength { get; set; }
    public byte FrameType { get; set; }
    public byte Flags { get; set; }
    public int StreamId { get; set; }

    public byte[] Payload { get; set; }
    public byte[] FrameHeader { get; set; }
}

public sealed partial class WebHostApp
{
    private async Task<int> GetPreface(SslStream sslStream, Memory<byte> preface, CancellationToken cancellationToken)
    {
        var prefaceLength = await sslStream.ReadAsync(preface, cancellationToken);
        Console.WriteLine($"Preface payload: {BitConverter.ToString(preface.ToArray())}");
        if (!Http2Preface.SequenceEqual(preface.Span))
        {
            throw new Exception("Invalid HTTP/2 preface");
        }

        return prefaceLength;
    }

    public async Task<FrameData> GetFrame(SslStream sslStream)
    {
        var frameData = new FrameData
        {
            FrameHeader = new byte[9] // HTTP/2 frame header is always 9 bytes
        };

        var bytesRead = await sslStream.ReadAsync(frameData.FrameHeader, 0, frameData.FrameHeader.Length);

        if (bytesRead != 9)
        {
            Console.WriteLine("Failed to read frame header");
        }

        // Parse the frame header
        frameData.PayloadLength = (frameData.FrameHeader[0] << 16) | (frameData.FrameHeader[1] << 8) | frameData.FrameHeader[2];
        frameData.FrameType = frameData.FrameHeader[3];
        frameData.Flags = frameData.FrameHeader[4];
        frameData.StreamId = (frameData.FrameHeader[5] << 24) | (frameData.FrameHeader[6] << 16) | (frameData.FrameHeader[7] << 8) | frameData.FrameHeader[8];
        frameData.StreamId &= 0x7FFFFFFF; // Clear the reserved bit

        Console.WriteLine("-----");
        Console.WriteLine(
            $"Frame received: Type={frameData.FrameType}, Length={frameData.PayloadLength}, Flags={frameData.Flags}, StreamId={frameData.StreamId}");
        Console.WriteLine($"Frame payload: {BitConverter.ToString(frameData.FrameHeader)}");
        Console.WriteLine("-----");

        // Read the frame payload
        if (frameData.PayloadLength > 0)
        {
            frameData.Payload = new byte[frameData.PayloadLength];
            bytesRead = await sslStream.ReadAsync(frameData.Payload, 0, frameData.PayloadLength);
            if (bytesRead != frameData.PayloadLength)
            {
                Console.WriteLine("Failed to read full frame payload");
            }
        }

        return frameData;
    }

    public async Task HandleClientAsync2(SslStream sslStream, CancellationToken cancellationToken)
    {
        // Read the preface (24 bytes for HTTP/2.0 preface)
        var buffer = new byte[24];
        int bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

        if (bytesRead != 24)
        {
            Console.WriteLine("Invalid preface length");
            Console.WriteLine($"preface: {BitConverter.ToString(buffer)}");
            sslStream.Close();
            return;
        }

        // Validate the preface
        var preface = Encoding.ASCII.GetString(buffer);

        Console.WriteLine($"Received preface: {preface}");

        if (preface == "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")
        {
            Console.WriteLine("Received valid HTTP/2.0 preface");

            // Respond with SETTINGS frame
            var settingsFrame = new byte[]
            {
                0x00, 0x00, 0x00, // Length
                0x04, // Type (SETTINGS)
                0x00, // Flags
                0x00, 0x00, 0x00, 0x00, // Stream Identifier
            };

            await sslStream.WriteAsync(settingsFrame, 0, settingsFrame.Length);
            Console.WriteLine("Sent empty SETTINGS frame");
        }
        else
        {
            Console.WriteLine("Invalid HTTP/2.0 preface");
            sslStream.Close();
            return;
        }

        var frameDataTemp = await GetFrame(sslStream);

        await sslStream.WriteAsync(SettingsAckFrame.ToArray(), 0, SettingsAckFrame.Length, cancellationToken);
        Console.WriteLine("Sent SETTINGS ACK");

        var decoder = new Http2.Hpack.Decoder();
        var encoder = new Http2.Hpack.Encoder();

        while (true)
        {
            var frameData = await GetFrame(sslStream);

            switch (frameData.FrameType)
            {
                case 0x00:
                    string dataText = Encoding.UTF8.GetString(frameData.Payload);
                    Console.WriteLine($"DATA frame payload (text): {dataText}");

                    Console.WriteLine($"Responding to {frameData.StreamId}");
                    var encoded = EncodeFrame(encoder, frameData.StreamId);
                    // Write the HEADERS frame
                    Console.WriteLine("Sending response HEADER");
                    await sslStream.WriteAsync(encoded.Item1, 0, encoded.Item1.Length, cancellationToken);
                    await sslStream.FlushAsync(cancellationToken);

                    Console.WriteLine("Sending response DATA");
                    // Write the DATA frame
                    await sslStream.WriteAsync(encoded.Item2, 0, encoded.Item2.Length, cancellationToken);
                    await sslStream.FlushAsync(cancellationToken);
                    break;
                case 0x01:

                    Console.WriteLine($"Header contents: {BitConverter.ToString(frameData.Payload)}");

                    // Create a list to hold decoded headers
                    var headers = new List<HeaderField>();

                    // Decode the header block fragment
                    var result = decoder.DecodeHeaderBlockFragment(
                        new ArraySegment<byte>(frameData.Payload),
                        maxHeaderFieldsSize: 4096,
                        headers: headers
                    );

                    // Check the decoding result
                    if (result.Status == DecodeStatus.Success)
                    {
                        Console.WriteLine("Headers decoded successfully:");
                        foreach (var header in headers)
                        {
                            Console.WriteLine($"{header.Name}: {header.Value}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Decoding failed with status: {result.Status}");
                    }
                    break;
                case 0x04:
                    break;
                default:
                    Console.WriteLine($"Cannot deal with frame type {frameData.FrameType}");
                    break;
            }
        }
    }

    public async Task HandleClientAsync4(SslStream sslStream, CancellationToken cancellationToken)
    {
        
        Memory<byte> frameHeader = new byte[9];
        Memory<byte> preface = new byte[24];

        Console.WriteLine("Waiting..");
        var prefaceLength = await sslStream.ReadAsync(preface, cancellationToken);
        Console.WriteLine($"Preface payload: {BitConverter.ToString(preface.ToArray())}");
        if (!Http2Preface.SequenceEqual(preface.Span))
        {
            throw new Exception("Invalid HTTP/2 preface");
        }

        await sslStream.WriteAsync(SettingsFrame, cancellationToken);
        var bytesRead = await sslStream.ReadAsync(frameHeader, cancellationToken);
        Console.WriteLine($"Frame payload: {BitConverter.ToString(frameHeader.ToArray())}");
        if (bytesRead != 9)
        {
            Console.WriteLine("Failed to read frame header 0");
        }

        await sslStream.WriteAsync(SettingsAckFrame, cancellationToken);

        while (true)
        {
            bytesRead = await sslStream.ReadAsync(frameHeader, cancellationToken);
            Console.WriteLine($"Frame payload: {BitConverter.ToString(frameHeader.ToArray())}");
            if (bytesRead != 9)
            {
                Console.WriteLine("Failed to read frame header 1");
            }

            var fHeader = frameHeader.ToArray();

            // Parse the frame header
            int payloadLength = (fHeader[0] << 16) | (fHeader[1] << 8) | fHeader[2];
            byte frameType = fHeader[3];
            byte flags = fHeader[4];
            int streamId = (fHeader[5] << 24) | (fHeader[6] << 16) | (fHeader[7] << 8) | fHeader[8];
            streamId &= 0x7FFFFFFF; // Clear the reserved bit

            Console.WriteLine(
                $"Frame received: Type={frameType}, Length={payloadLength}, Flags={flags}, StreamId={streamId}");

            // Read the frame payload
            byte[] payload = null;
            if (payloadLength > 0)
            {
                payload = new byte[payloadLength];
                bytesRead = await sslStream.ReadAsync(payload, 0, payloadLength);

                if (bytesRead != payloadLength)
                {
                    Console.WriteLine("Failed to read full frame payload");
                    break;
                }
            }



            int length = (frameHeader.Span[0] << 16) | (frameHeader.Span[1] << 8) | frameHeader.Span[2];

            Memory<byte> headerPayload = new byte[length];
            _ = await sslStream.ReadAsync(headerPayload, cancellationToken);

           

            //await sslStream.WriteAsync(SettingsAckFrame, cancellationToken);
            bytesRead = await sslStream.ReadAsync(frameHeader, cancellationToken);
            Console.WriteLine($"Frame payload: {BitConverter.ToString(frameHeader.ToArray())}");

            //await sslStream.WriteAsync(CreateHttp2Response(), cancellationToken);
            //await sslStream.FlushAsync(cancellationToken);
            // Send headers and data frames
        }
    }

    public async Task HandleClientAsync6(SslStream sslStream, CancellationToken cancellationToken)
    {
        Console.WriteLine("Client connected");

        while (true)
        {

            // Read the preface (24 bytes for HTTP/2.0 preface)
            var buffer = new byte[24];
            int bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead != 24)
            {
                Console.WriteLine("Invalid preface length");
                Console.WriteLine($"preface: {BitConverter.ToString(buffer)}");
                sslStream.Close();
                return;
            }

            // Validate the preface
            var preface = Encoding.ASCII.GetString(buffer);

            Console.WriteLine($"Received preface: {preface}");

            if (preface == "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")
            {
                Console.WriteLine("Received valid HTTP/2.0 preface");

                // Respond with SETTINGS frame
                var settingsFrame = new byte[]
                {
                    0x00, 0x00, 0x00, // Length
                    0x04, // Type (SETTINGS)
                    0x00, // Flags
                    0x00, 0x00, 0x00, 0x00, // Stream Identifier
                };

                await sslStream.WriteAsync(settingsFrame, 0, settingsFrame.Length);
                Console.WriteLine("Sent SETTINGS frame");
            }
            else
            {
                Console.WriteLine("Invalid HTTP/2.0 preface");
                sslStream.Close();
                return;
            }

            bool breakLoop = false;
            // Read subsequent frames
            while (!breakLoop)
            {
                var frameHeader = new byte[9]; // HTTP/2 frame header is always 9 bytes
                bytesRead = await sslStream.ReadAsync(frameHeader, 0, frameHeader.Length);

                if (bytesRead != 9)
                {
                    Console.WriteLine("Failed to read frame header");
                    break;
                }

                // Parse the frame header
                int payloadLength = (frameHeader[0] << 16) | (frameHeader[1] << 8) | frameHeader[2];
                byte frameType = frameHeader[3];
                byte flags = frameHeader[4];
                int streamId = (frameHeader[5] << 24) | (frameHeader[6] << 16) | (frameHeader[7] << 8) | frameHeader[8];
                streamId &= 0x7FFFFFFF; // Clear the reserved bit

                Console.WriteLine(
                    $"Frame received: Type={frameType}, Length={payloadLength}, Flags={flags}, StreamId={streamId}");

                // Read the frame payload
                byte[] payload = null;
                if (payloadLength > 0)
                {
                    Console.WriteLine("Payload length is > 0");
                    payload = new byte[payloadLength];
                    bytesRead = await sslStream.ReadAsync(payload, 0, payloadLength);

                    if (bytesRead != payloadLength)
                    {
                        Console.WriteLine("Failed to read full frame payload");
                        break;
                    }
                }

                switch (frameType)
                {
                    case 0x04: // SETTINGS frame
                        Console.WriteLine("Received SETTINGS frame");
                        Console.WriteLine($"SETTINGS frame payload: {BitConverter.ToString(frameHeader)}");
                        if ((flags & 0x1) == 0) // Check if ACK is not set
                        {
                            var settingsAckFrame = new byte[]
                            {
                                0x00, 0x00, 0x00, // Length
                                0x04, // Type (SETTINGS)
                                0x01, // Flags (ACK)
                                0x00, 0x00, 0x00, 0x00, // Stream Identifier
                            };
                            await sslStream.WriteAsync(settingsAckFrame, 0, settingsAckFrame.Length, cancellationToken);
                            Console.WriteLine("Sent SETTINGS ACK");
                        }
                        break;

                    case 0x01: // HEADERS frame
                        Console.WriteLine($"HEADERS frame payload: {BitConverter.ToString(payload)}");
                        var decoder = new Http2.Hpack.Decoder();

                        // Create a list to hold decoded headers
                        var headers = new List<HeaderField>();

                        // Decode the header block fragment
                        var result = decoder.DecodeHeaderBlockFragment(
                            new ArraySegment<byte>(payload),
                            maxHeaderFieldsSize: 4096,
                            headers: headers
                        );

                        // Check the decoding result
                        if (result.Status == DecodeStatus.Success)
                        {
                            Console.WriteLine("Headers decoded successfully:");
                            foreach (var header in headers)
                            {
                                Console.WriteLine($"{header.Name}: {header.Value}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Decoding failed with status: {result.Status}");
                        }
                        //Console.WriteLine($"HEADERS frame data: {Encoding.UTF8.GetString(payload)}");
                        break;

                    case 0x00: // DATA frame
                        string dataText = Encoding.UTF8.GetString(payload);
                        Console.WriteLine($"DATA frame payload (text): {dataText}");

                        // Respond with 200 OK
                        //await sslStream.WriteAsync(CreateHttp2Response(), cancellationToken);
                        //await sslStream.FlushAsync(cancellationToken);
                        //Console.WriteLine("200 OK sent");

                        breakLoop = true;

                        //Receive ACK
                        frameHeader = new byte[9]; // HTTP/2 frame header is always 9 bytes
                        bytesRead = await sslStream.ReadAsync(frameHeader, 0, frameHeader.Length);

                        break;

                    case 0x03:
                        Console.WriteLine($"Type 3 frame payload: {BitConverter.ToString(frameHeader)}");
                        break;
                    default:
                        Console.WriteLine($"Unhandled frame type: {frameType}");
                        break;
                }
            }
        }

        //sslStream.Close();
        //Console.WriteLine("Connection closed");
    }
    static byte[] CreateFrame(byte type, byte flags, int streamId, byte[] payload)
    {
        var frame = new List<byte>();

        // Add the length (24 bits)
        frame.Add((byte)((payload.Length >> 16) & 0xFF));
        frame.Add((byte)((payload.Length >> 8) & 0xFF));
        frame.Add((byte)(payload.Length & 0xFF));

        // Add the type
        frame.Add(type);

        // Add the flags
        frame.Add(flags);

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        frame.Add((byte)((streamId >> 24) & 0x7F));
        frame.Add((byte)((streamId >> 16) & 0xFF));
        frame.Add((byte)((streamId >> 8) & 0xFF));
        frame.Add((byte)(streamId & 0xFF));

        // Add the payload
        frame.AddRange(payload);

        return frame.ToArray();
    }
    private (byte[], byte[]) EncodeFrame(Http2.Hpack.Encoder encoder, int streamId)
    {
        // Create the headers
        var headers = new List<HeaderField>
        {
            new HeaderField { Name = ":status", Value = "200", Sensitive = false },
            new HeaderField { Name = "content-type", Value = "text/plain", Sensitive = false },
            new HeaderField { Name = "content-length", Value = "5", Sensitive = false } // Length of "Hello"
        };

        // Allocate a buffer for the header block
        var buffer = new byte[1024];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = encoder.EncodeInto(bufferSegment, headers);

        if (result.FieldCount == 0)
        {
            Console.WriteLine("Failed to encode headers: Buffer too small.");
            return ([], []);
        }

        Console.WriteLine($"Encoded {result.FieldCount} headers using {result.UsedBytes} bytes.");

        // Create the payload
        var payload = Encoding.UTF8.GetBytes("Hello");

        // Construct the HEADERS frame
        var headersFrame = CreateFrame(
            type: 0x01, // HEADERS frame
            flags: 0x04, // END_HEADERS
            streamId: streamId,
            payload: buffer[..result.UsedBytes] // Use only the used bytes
        );

        // Construct the DATA frame
        var dataFrame = CreateFrame(
            type: 0x00, // DATA frame
            flags: 0x01, // END_STREAM
            streamId: streamId,
            payload: payload
        );

        // Print the frames
        Console.WriteLine("HEADERS Frame:");
        Console.WriteLine(BitConverter.ToString(headersFrame));

        Console.WriteLine("\nDATA Frame:");
        Console.WriteLine(BitConverter.ToString(dataFrame));

        return (headersFrame, dataFrame);
    }

    private static readonly string[] StaticTable = new string[]
    {
        ":authority", ":method", ":path", ":scheme", ":status", "accept-charset", "accept-encoding",
        "accept-language", "accept-ranges", "accept", "access-control-allow-origin", "age",
        "allow", "authorization", "cache-control", "content-disposition", "content-encoding",
        "content-language", "content-length", "content-location", "content-range", "content-type",
        "cookie", "date", "etag", "expect", "expires", "from", "host", "if-match",
        "if-modified-since", "if-none-match", "if-range", "if-unmodified-since", "last-modified",
        "link", "location", "max-forwards", "proxy-authenticate", "proxy-authorization",
        "range", "referer", "refresh", "retry-after", "server", "set-cookie", "strict-transport-security",
        "transfer-encoding", "user-agent", "vary", "via", "www-authenticate"
    };
    private static void DecodeHpackHeaders(ReadOnlySpan<byte> encodedHeaders)
    {
        int index = 0;
        while (index < encodedHeaders.Length)
        {
            byte currentByte = encodedHeaders[index];

            if ((currentByte & 0x80) != 0)
            {
                // Indexed Header Field Representation
                int tableIndex = currentByte & 0x7F; // Mask the first bit
                if (tableIndex == 0 || tableIndex > StaticTable.Length)
                {
                    Console.WriteLine("Invalid indexed header index");
                    return;
                }

                Console.WriteLine($"Header: {StaticTable[tableIndex - 1]}");
                index++;
            }
            else if ((currentByte & 0x40) != 0)
            {
                // Literal Header Field with Incremental Indexing
                int nameLength = encodedHeaders[index + 1]; // Length of the name
                string name = Encoding.UTF8.GetString(encodedHeaders.Slice(index + 2, nameLength));

                int valueLengthIndex = index + 2 + nameLength;
                int valueLength = encodedHeaders[valueLengthIndex];
                string value = Encoding.UTF8.GetString(encodedHeaders.Slice(valueLengthIndex + 1, valueLength));

                Console.WriteLine($"Header: {name}, Value: {value}");

                index = valueLengthIndex + 1 + valueLength;
            }
            else
            {
                // Unsupported encoding type (e.g., Huffman)
                Console.WriteLine("Unsupported encoding type");
                return;
            }
        }
    }

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

    private static ReadOnlyMemory<byte> CreateHttp2Response() =>
        new byte[]
        {
            0x00, 0x00, 0x01,           // Length
            0x01,                       // Type (HEADERS)
            0x05,                       // Flags (END_STREAM | END_HEADERS)
            0x00, 0x00, 0x00, 0x01,     // Stream ID
            0x88                        // HPACK encoded status 200
        };
}