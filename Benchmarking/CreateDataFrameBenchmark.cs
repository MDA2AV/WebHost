using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using System.Text;
using WebHost.Hpack;
using WebHost.Models;

namespace Benchmarking;

/*
 *| Method                  | Mean     | Error    | StdDev   | Gen0   | Allocated |
   |------------------------ |---------:|---------:|---------:|-------:|----------:|
   | TestCreateHeadersFrame  | 32.96 ns | 0.373 ns | 0.312 ns | 0.0315 |     528 B |
   | TestCreateHeadersFrame2 | 90.83 ns | 0.823 ns | 0.729 ns | 0.0716 |    1200 B |
   | TestCreateHeadersFrame3 | 37.00 ns | 0.601 ns | 0.469 ns | 0.0315 |     528 B |
   | TestCreateHeadersFrame4 | 10.59 ns | 0.034 ns | 0.031 ns |      - |         - |
   | TestCreateHeadersFrame5 | 37.11 ns | 0.735 ns | 0.902 ns | 0.0315 |     528 B |

TestCreateHeadersFrame5 selected for now
 */

[MemoryDiagnoser] // Tracks memory usage
public class CreateDataFrameBenchmark
{
    private Http2Context _context;
    private List<HeaderField> _headers;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize mock context and headers
        _context = new Http2Context(null!)
        {
            Request = new Http2Request([""], "route", "GET", "params", 1),
            Encoder = new WebHost.Hpack.Encoder()
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
        _context.CreateDataFrame(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame2()
    {
        _context.CreateDataFrame2(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame3()
    {
        _context.CreateDataFrame3(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame4()
    {
        _context.CreateDataFrame4(_headers);
    }

    [Benchmark]
    public void TestCreateHeadersFrame5()
    {
        _context.CreateDataFrame5(_headers);
    }
}

public static partial class HeaderEncodingBenchmarkExtensions
{
    public static void CreateDataFrame(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var msg = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello"u8.ToArray();

        // Calculate the total frame size
        var frameLength = 9 + msg.Length; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 1024 // Adjust the limit based on your needs
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
        msg.AsSpan(0, msg.Length).CopyTo(frameSpan.Slice(9));
    }

    public static void CreateDataFrame2(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var msg = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello"u8.ToArray();
        
        var request = Unsafe.As<Http2Request>(context.Request);

        var frame = new List<byte>
        {
            // Add the length (24 bits)
            (byte)((msg.Length >> 16) & 0xFF),
            (byte)((msg.Length >> 8) & 0xFF),
            (byte)(msg.Length & 0xFF),
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
        frame.AddRange(msg);
    }

    public static void CreateDataFrame3(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        var msg = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello"u8.ToArray();

        // Use the actual encoded size for the buffer
        ReadOnlySpan<byte> encodedBuffer = msg.AsSpan(0, msg.Length);

        // Calculate the total frame size
        var frameLength = 9 + encodedBuffer.Length; // 9 bytes for header + payload length

        // Use stackalloc for the HTTP/2 frame
        Span<byte> frameSpan = frameLength <= 1024
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

    public static void CreateDataFrame4(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        ReadOnlySpan<byte> msg = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello"u8;

        // Calculate the total frame size
        var frameLength = 9 + msg.Length; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 1024
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((msg.Length >> 16) & 0xFF);
        frameSpan[1] = (byte)((msg.Length >> 8) & 0xFF);
        frameSpan[2] = (byte)(msg.Length & 0xFF);

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
        msg.CopyTo(frameSpan.Slice(9));
    }

    public static void CreateDataFrame5(this Http2Context context, IEnumerable<HeaderField> headers)
    {
        ReadOnlyMemory<byte> msg = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello"u8.ToArray();

        // Calculate the total frame size
        var frameLength = 9 + msg.Length; // 9 bytes for header + payload length

        // Use stackalloc for stack-allocated buffer
        Span<byte> frameSpan = frameLength <= 1024
            ? stackalloc byte[frameLength]
            : throw new InvalidOperationException("Payload too large for stack allocation.");

        // Add the length (24 bits)
        frameSpan[0] = (byte)((msg.Length >> 16) & 0xFF);
        frameSpan[1] = (byte)((msg.Length >> 8) & 0xFF);
        frameSpan[2] = (byte)(msg.Length & 0xFF);

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
        msg.Span.CopyTo(frameSpan.Slice(9));
    }
}