using System.Text;

namespace WebHost.Utils;

public class ResponseHeaderBuilder
{
    public enum HeaderFormat
    {
        String,
        StringBuilder,
        Bytes
    }

    // Create HTTP header
    //
    public static T CreateHeader<T>(HeaderFormat headerFormat, IEnumerable<string> entries)
    {
        return headerFormat switch
        {
            HeaderFormat.String => CreateStringHeader(entries),
            HeaderFormat.StringBuilder => CreateStringBuilderHeader(entries),
            HeaderFormat.Bytes => CreateStringBuilderBytes(entries) as dynamic,
            _ => throw new NotImplementedException()
        };
    }

    // Build Header, return StringBuilder
    //
    private static StringBuilder CreateStringBuilderHeader(IEnumerable<string> entries)
        => entries.Aggregate(new StringBuilder(), (sb, entry) => sb.Append(entry));

    // Build Header, return string
    //
    private static string CreateStringHeader(IEnumerable<string> entries) =>
        entries.Aggregate(new StringBuilder(), (sb, entry) => sb.Append(entry)).ToString();

    // Build Header, return byte[]
    //
    private static byte[] CreateStringBuilderBytes(IEnumerable<string> entries) =>
        Encoding.UTF8.GetBytes(entries.Aggregate(new StringBuilder(), (sb, entry) => sb.Append(entry)).ToString());

    // Some examples
    //
    //
    public static string GetHeaderStatus(string version, string statusCode, string reasonPhrase) => $"HTTP/{version} {statusCode} {reasonPhrase}\r\n";
    public static string GetHeaderContentType(string contentType) => $"Content-Type: {contentType}\r\n";
    public static string GetHeaderContentLength(int contentLength) => $"Content-Length: {contentLength}\r\n";
    public static string GetHeaderChunked() => "Transfer-Encoding: chunked\r\n";
    public static string GetHeaderClosing(string purpose) => $"Connection: {purpose}\r\n\r\n";
}