using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using WebHost.Hpack;
using WebHost.Models;

namespace Benchmarking;


/*
 *|| Method                  | Mean     | Error   | StdDev  | Gen0   | Allocated |
   |------------------------ |---------:|--------:|--------:|-------:|----------:|
   | TestCreateHeadersFrame  | 224.1 ns | 1.00 ns | 0.78 ns | 0.0200 |     336 B |
   | TestCreateHeadersFrame2 | 284.1 ns | 1.47 ns | 1.30 ns | 0.0300 |     504 B |
   | TestCreateHeadersFrame3 | 224.9 ns | 1.75 ns | 1.46 ns | 0.0200 |     336 B |
   | TestCreateHeadersFrame4 | 226.8 ns | 3.77 ns | 3.34 ns | 0.0367 |     616 B |
   | TestCreateHeadersFrame5 | 261.3 ns | 5.06 ns | 6.58 ns | 0.0200 |     336 B |
   | TestCreateHeadersFrame6 | 238.0 ns | 1.56 ns | 1.39 ns | 0.0224 |     376 B |
   | TestCreateHeadersFrame7 | 239.1 ns | 1.17 ns | 1.04 ns | 0.0224 |     376 B |
 */


[MemoryDiagnoser] // Tracks memory usage
public class HeaderEncodingBenchmark
{
    private Http2Context _context;
    private List<HeaderField> _headers;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize mock context and headers
        _context = new Http2Context(null!)
        {
            Request = new Http2Request([""], "route", "GET", "params", 1 ),
            Encoder = new Encoder()
        };
        _headers = [
            new HeaderField { Name = ":status", Value = "200", Sensitive = false },
            new HeaderField { Name = "content-type", Value = "text/plain", Sensitive = false },
            new HeaderField { Name = "content-length", Value = "5", Sensitive = false }
        ];
    }

    
    [Benchmark]
    public void TestCreateHeadersFrame()
    {
        _context.CreateHeadersFrame(_headers);
    }

    
    [Benchmark]
    public void TestCreateHeadersFrame2()
    {
        _context.CreateHeadersFrame2(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame3()
    {
        _context.CreateHeadersFrame3(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame4()
    {
        _context.CreateHeadersFrame4(_headers);
    }
    

    [Benchmark]
    public void TestCreateHeadersFrame5()
    {
        _context.CreateHeadersFrame5(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame6()
    {
        _context.CreateHeadersFrame6(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame7()
    {
        _context.CreateHeadersFrame7(_headers);
    }
}

public static partial class HeaderEncodingBenchmarkExtensions
{
    public static byte[] CreateHeadersFrame(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var buffer = new byte[256];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        // Calculate the total frame size
        var frameLength = 9 + result.UsedBytes; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 256 // Adjust the limit based on your needs
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((frameLength >> 16) & 0xFF);
        frameSpan[1] = (byte)((frameLength >> 8) & 0xFF);
        frameSpan[2] = (byte)(frameLength & 0xFF);

        // Add the type
        frameSpan[3] = 0x01; // HEADERS frame

        // Add the flags
        frameSpan[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frameSpan[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frameSpan[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frameSpan[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frameSpan[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload (only the used bytes)
        buffer.AsSpan(0, result.UsedBytes).CopyTo(frameSpan.Slice(9));

        return buffer;
    }

    public static void CreateHeadersFrame2(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var buffer = new byte[256];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        var request = Unsafe.As<Http2Request>(context.Request);

        var payload = buffer[..result.UsedBytes];

        var frame = new List<byte>
        {
            // Add the length (24 bits)
            (byte)((payload.Length >> 16) & 0xFF),
            (byte)((payload.Length >> 8) & 0xFF),
            (byte)(payload.Length & 0xFF),
            // Add the type
            0x01,
            // Add the flags
            0x04,
            // Add the stream ID (31 bits, MSB is reserved and must be 0)
            (byte)((request.StreamId >> 24) & 0x7F),
            (byte)((request.StreamId >> 16) & 0xFF),
            (byte)((request.StreamId >> 8) & 0xFF),
            (byte)(request.StreamId & 0xFF)
        };

        // Add the payload
        frame.AddRange(payload);
    }

    public static void CreateHeadersFrame3(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        // Use an ArraySegment<byte> to work with APIs expecting arrays
        byte[] tempArray = new byte[256];
        var bufferSegment = new ArraySegment<byte>(tempArray);

        // Encode the headers into the buffer
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        // Use the actual encoded size for the buffer
        ReadOnlySpan<byte> encodedBuffer = bufferSegment.AsSpan(0, result.UsedBytes);

        // Calculate the total frame size
        var frameLength = 9 + encodedBuffer.Length; // 9 bytes for header + payload length

        // Use stackalloc for the HTTP/2 frame
        Span<byte> frameSpan = frameLength <= 256
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((frameLength >> 16) & 0xFF);
        frameSpan[1] = (byte)((frameLength >> 8) & 0xFF);
        frameSpan[2] = (byte)(frameLength & 0xFF);

        // Add the type
        frameSpan[3] = 0x01; // HEADERS frame

        // Add the flags
        frameSpan[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frameSpan[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frameSpan[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frameSpan[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frameSpan[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload
        encodedBuffer.CopyTo(frameSpan.Slice(9));
    }

    public static void CreateHeadersFrame4(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        // Allocate a temporary buffer for encoding headers
        Memory<byte> buffer = new byte[256];
        var bufferSegment = new ArraySegment<byte>(buffer.ToArray()); // Temporary for compatibility with Encoder

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        // Use only the portion of the buffer that contains encoded data
        ReadOnlyMemory<byte> encodedBuffer = buffer.Slice(0, result.UsedBytes);

        // Calculate the total frame size
        var frameLength = 9 + encodedBuffer.Length; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 256
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((encodedBuffer.Length >> 16) & 0xFF);
        frameSpan[1] = (byte)((encodedBuffer.Length >> 8) & 0xFF);
        frameSpan[2] = (byte)(encodedBuffer.Length & 0xFF);

        // Add the type
        frameSpan[3] = 0x01; // HEADERS frame

        // Add the flags
        frameSpan[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frameSpan[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frameSpan[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frameSpan[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frameSpan[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload (only the used bytes)
        encodedBuffer.Span.CopyTo(frameSpan.Slice(9));
    }

    public static void CreateHeadersFrame5(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        // Use stack-allocated buffer for encoding headers
        Span<byte> buffer = stackalloc byte[256];

        // Encode the headers directly into the stack buffer
        var result = context.Encoder.EncodeInto(new ArraySegment<byte>(buffer.ToArray()), headers);

        var payloadLength = result.UsedBytes;
        var frameLength = 9 + payloadLength; // 9 bytes for header + payload

        // Use stackalloc for the frame if within limits
        Span<byte> frameSpan = frameLength <= 256
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((payloadLength >> 16) & 0xFF);
        frameSpan[1] = (byte)((payloadLength >> 8) & 0xFF);
        frameSpan[2] = (byte)(payloadLength & 0xFF);

        // Add the type
        frameSpan[3] = 0x01; // HEADERS frame

        // Add the flags
        frameSpan[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frameSpan[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frameSpan[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frameSpan[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frameSpan[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload (only the used bytes)
        buffer.Slice(0, payloadLength).CopyTo(frameSpan.Slice(9));
    }

    public static byte[] CreateHeadersFrame6(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        // Use a stack-allocated buffer for encoding headers
        Span<byte> buffer = stackalloc byte[256];

        // Encode the headers directly into the stack buffer
        var result = context.Encoder.EncodeInto(new ArraySegment<byte>(buffer.ToArray()), headers);

        var payloadLength = result.UsedBytes;
        var frameLength = 9 + payloadLength; // 9 bytes for header + payload

        // Allocate a heap-based array for the frame to return
        byte[] frame = new byte[frameLength];

        // Add the length (24 bits)
        frame[0] = (byte)((payloadLength >> 16) & 0xFF);
        frame[1] = (byte)((payloadLength >> 8) & 0xFF);
        frame[2] = (byte)(payloadLength & 0xFF);

        // Add the type
        frame[3] = 0x01; // HEADERS frame

        // Add the flags
        frame[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frame[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frame[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frame[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frame[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload (only the used bytes)
        buffer.Slice(0, payloadLength).CopyTo(frame.AsSpan(9));

        // Return the constructed frame
        return frame;
    }

    public static ReadOnlyMemory<byte> CreateHeadersFrame7(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        // Use a stack-allocated buffer for encoding headers
        Span<byte> buffer = stackalloc byte[256];

        // Encode the headers directly into the stack buffer
        var result = context.Encoder.EncodeInto(new ArraySegment<byte>(buffer.ToArray()), headers);

        var payloadLength = result.UsedBytes;
        var frameLength = 9 + payloadLength; // 9 bytes for header + payload

        // Allocate a heap-based array for the frame to return
        byte[] frame = new byte[frameLength];

        // Add the length (24 bits)
        frame[0] = (byte)((payloadLength >> 16) & 0xFF);
        frame[1] = (byte)((payloadLength >> 8) & 0xFF);
        frame[2] = (byte)(payloadLength & 0xFF);

        // Add the type
        frame[3] = 0x01; // HEADERS frame

        // Add the flags
        frame[4] = 0x04; // END_HEADERS

        // Add the stream ID (31 bits, MSB is reserved and must be 0)
        var request = Unsafe.As<Http2Request>(context.Request);
        frame[5] = (byte)((request.StreamId >> 24) & 0x7F); // MSB is reserved
        frame[6] = (byte)((request.StreamId >> 16) & 0xFF);
        frame[7] = (byte)((request.StreamId >> 8) & 0xFF);
        frame[8] = (byte)(request.StreamId & 0xFF);

        // Add the payload (only the used bytes)
        buffer.Slice(0, payloadLength).CopyTo(frame.AsSpan(9));

        // Return the constructed frame as ReadOnlyMemory<byte>
        return new ReadOnlyMemory<byte>(frame);
    }
}