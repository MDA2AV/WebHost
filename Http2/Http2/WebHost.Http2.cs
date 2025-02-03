using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using WebHost.Hpack;
using WebHost.Models;

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
    public bool IsEndStream { get; set; }

    public byte[] Payload { get; set; }
    public byte[] FrameHeader { get; set; }
}

public sealed partial class WebHostApp
{
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

    private static async Task<bool> ValidatePreface(SslStream sslStream, CancellationToken cancellationToken)
    {
        // Read the preface (24 bytes for HTTP/2.0 preface)
        var buffer = new byte[24];
        var bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

        if (bytesRead != 24)
        {
            Console.WriteLine("Invalid preface length");
            Console.WriteLine($"preface: {BitConverter.ToString(buffer)}");
            sslStream.Close();
            return false;
        }

        // Validate the preface
        var preface = Encoding.ASCII.GetString(buffer);

        Console.WriteLine($"Received preface: {preface}");

        if (preface == "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")
        {
            Console.WriteLine("Received valid HTTP/2.0 preface");
            return true;
        }

        Console.WriteLine("Invalid HTTP/2.0 preface");
        return false;
    }

    public async Task HandleClientAsync2x(SslStream sslStream, CancellationToken cancellationToken)
    {
        #region Connection Handshake
        // Handle client connection preface and send
        var validPreface = await ValidatePreface(sslStream, cancellationToken);

        if (!validPreface)
        {
            sslStream.Close();
            return;
        }

        // Respond with SETTINGS frame
        await sslStream.WriteAsync(SettingsFrame.ToArray(), 0, SettingsFrame.Length, cancellationToken);

        // Receive SETTINGS frame
        _ = await GetFrame(sslStream);

        // Send SETTINGS Ack frame
        await sslStream.WriteAsync(SettingsAckFrame.ToArray(), 0, SettingsAckFrame.Length, cancellationToken);
        #endregion

        var streamBuffers = new Dictionary<int, BlockingCollection<FrameData>>();

        var decoder = new Hpack.Decoder();
        var encoder = new Hpack.Encoder();


        while (!cancellationToken.IsCancellationRequested)
        {
            // New frame received!
            var frameData = await GetFrame(sslStream);

            if (!streamBuffers.ContainsKey(frameData.StreamId))
            {
                if (frameData.FrameType == 0x01)
                {
                    // New stream, must call the corresponding Pipeline to handle request

                    // Create queue for new stream
                    streamBuffers.Add(frameData.StreamId, new BlockingCollection<FrameData>());
                    
                    // Create a list to hold decoded headers
                    var headers = new List<HeaderField>();

                    // Decode the header block fragment
                    var result = decoder.DecodeHeaderBlockFragment(
                        new ArraySegment<byte>(frameData.Payload),
                        maxHeaderFieldsSize: 4096,
                        headers: headers
                    );

                    if (result.Status != DecoderExtensions.DecodeStatus.Success) // Could not decode headers
                        continue;

                    foreach (var header in headers)
                    {
                        Console.WriteLine($"{header.Name} {header.Value}");
                    }

                    var fullRoute = headers.FirstOrDefault(h => h.Name.Contains(":path")).Value.Split('?');
                    var httpMethod = headers.FirstOrDefault(h => h.Name.Contains(":method")).Value;

                    // Create a new context
                    var context = new Context(sslStream)
                    {
                        Request = new Http2Request(
                            headers
                                .Select(header => $"{header.Name}: {header.Value}")
                                .ToList(), 
                            fullRoute[0], 
                            httpMethod, 
                            fullRoute[1], 
                            frameData.StreamId),
                        Http2 = new Models.Http2
                        {
                            StreamBuffer = streamBuffers[frameData.StreamId],
                            Encoder = encoder,
                            Decoder = decoder
                        }
                    };

                    // Create a new scope for handling the request
                    //
                    _ = Task.Factory.StartNew(async endpointContext =>
                    {
                        await using var scope = InternalHost.Services.CreateAsyncScope();
                        context.Scope = scope;

                        // Retrieve and execute the middleware pipeline
                        //
                        var middleware = scope.ServiceProvider.GetServices<Func<IContext, Func<IContext, Task>, Task>>().ToList();

                        await Pipeline(context, 0, middleware);
                    }, context, cancellationToken);

                }

                // Ignore this case!
                continue;
            }

            // Received frame for existing stream, add it to the queue
            streamBuffers[frameData.StreamId].Add(frameData, cancellationToken);
            if ((frameData.Flags & 0x01) == 0x01) // End stream flag is set
            {
                streamBuffers[frameData.StreamId].CompleteAdding();
            }


            continue;
            switch (frameData.FrameType)
            {
                case 0x00:
                    // Data frame
                    break;
                case 0x01:
                    // Headers frame - new request!
                    break;
                case 0x02:
                    // Priority frame
                    break;
                case 0x03:
                    // Reset Stream - RST_STREAM
                    break;
                case 0x04:
                    // Settings frame
                    break;
                case 0x05:
                    // Push Promise frame
                    break;
                case 0x06:
                    // Ping frame
                    break;
                case 0x07:
                    // Go Away frame
                    break;
                case 0x08:
                    // Window Update frame
                    break;
                case 0x09:
                    // Continuation frame
                    break;
                default:
                    break;
            }
        }
    }

    public async Task HandleClientAsync2x2(SslStream sslStream, CancellationToken cancellationToken)
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

        var decoder = new Hpack.Decoder();
        var encoder = new Hpack.Encoder();

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
                    if (result.Status == DecoderExtensions.DecodeStatus.Success)
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

    public byte[] EncodeHttp2Header(Hpack.Encoder encoder, IEnumerable<HeaderField> headers, byte type, byte flags, int streamId)
    {
        // Allocate a buffer for the header block
        var buffer = new byte[1024];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = encoder.EncodeInto(bufferSegment, headers);

        if (result.FieldCount == 0)
        {
            Console.WriteLine("Failed to encode headers: Buffer too small.");
            return [];
        }

        // Construct the HEADERS frame
        var headersFrame = CreateFrame(
            type: 0x01, // HEADERS frame
            flags: 0x04, // END_HEADERS
            streamId: streamId,
            payload: buffer[..result.UsedBytes] // Use only the used bytes
        );

        return headersFrame;
    }

    private static byte[] CreateFrame(byte type, byte flags, int streamId, byte[] payload)
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
    public static (byte[], byte[]) EncodeFrame(Hpack.Encoder encoder, int streamId)
    {
        // Create the headers
        List<HeaderField> headers =
        [
            new HeaderField { Name = ":status", Value = "200", Sensitive = false },
            new HeaderField { Name = "content-type", Value = "text/plain", Sensitive = false },
            new HeaderField { Name = "content-length", Value = "5", Sensitive = false }
        ];

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

        // Construct the HEADERS frame
        var headersFrame = CreateFrame(
            type: 0x01, // HEADERS frame
            flags: 0x04, // END_HEADERS
            streamId: streamId,
            payload: buffer[..result.UsedBytes] // Use only the used bytes
        );

        // Create the payload
        var payload = "Hello"u8.ToArray();

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