using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using WebHost.Hpack;
using WebHost.Models;

namespace Benchmarking;


/*
 *| Method                  | Mean     | Error   | StdDev  | Gen0   | Allocated |
   |------------------------ |---------:|--------:|--------:|-------:|----------:|
   | TestCreateHeadersFrame  | 256.6 ns | 2.66 ns | 2.49 ns | 0.0658 |   1.08 KB |
   | TestCreateHeadersFrame2 | 304.9 ns | 3.31 ns | 2.58 ns | 0.0758 |   1.24 KB |
   | TestCreateHeadersFrame3 | 264.7 ns | 4.78 ns | 4.24 ns | 0.0658 |   1.08 KB |
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
}

public static class HeaderEncodingBenchmarkExtensions
{
    public static void CreateHeadersFrame(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var buffer = new byte[1024];
        var bufferSegment = new ArraySegment<byte>(buffer);

        // Encode the headers
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        // Calculate the total frame size
        var frameLength = 9 + result.UsedBytes; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 1024 // Adjust the limit based on your needs
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((result.UsedBytes >> 16) & 0xFF);
        frameSpan[1] = (byte)((result.UsedBytes >> 8) & 0xFF);
        frameSpan[2] = (byte)(result.UsedBytes & 0xFF);

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
    }

    public static void CreateHeadersFrame2(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var buffer = new byte[1024];
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
        Span<byte> buffer = stackalloc byte[1024]; // Stack-allocated buffer for header encoding

        // Use an ArraySegment<byte> to work with APIs expecting arrays
        byte[] tempArray = new byte[1024];
        var bufferSegment = new ArraySegment<byte>(tempArray);

        // Encode the headers into the buffer
        var result = context.Encoder.EncodeInto(bufferSegment, headers);

        // Use the actual encoded size for the buffer
        ReadOnlySpan<byte> encodedBuffer = bufferSegment.AsSpan(0, result.UsedBytes);

        // Calculate the total frame size
        var frameLength = 9 + encodedBuffer.Length; // 9 bytes for header + payload length

        // Use stackalloc for the HTTP/2 frame
        Span<byte> frameSpan = frameLength <= 1024
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

        // Add the payload
        encodedBuffer.CopyTo(frameSpan.Slice(9));
    }
}