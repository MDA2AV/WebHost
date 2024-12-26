using System.Text;
using WebHost.Extensions;

namespace Tests.ExtensionsTests.HttpResponseMessage;

public class HttpResponseMessageExtensionsTests
{
    [Fact]
    public async Task ToBytes_NullResponse_ReturnsEmptyMemory()
    {
        // Arrange
        System.Net.Http.HttpResponseMessage? response = null;

        // Act
        var result = await response.ToBytes();

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task ToBytes_ResponseWithoutContent_ReturnsSerializedHeadersAndStatusLine()
    {
        // Arrange
        var response = new System.Net.Http.HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ReasonPhrase = "OK",
            Version = new Version(1, 1)
        };
        response.Headers.Add("Custom-Header", "HeaderValue");

        // Act
        var result = await response.ToBytes();
        var resultString = Encoding.UTF8.GetString(result.Span);

        // Assert
        Assert.Contains("HTTP/1.1 200 OK\r\n", resultString);
        Assert.Contains("Custom-Header: HeaderValue\r\n", resultString);
        Assert.EndsWith("\r\n\r\n", resultString);
    }

    [Fact]
    public async Task ToBytes_ResponseWithContent_ReturnsSerializedHeadersAndBody()
    {
        // Arrange
        var response = new System.Net.Http.HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ReasonPhrase = "OK",
            Version = new Version(1, 1),
            Content = new StringContent("Hello, World!", Encoding.UTF8, "text/plain"),
        };
        response.Headers.Add("Custom-Header", "HeaderValue");
        response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount("Hello, World!");

        // Act
        var result = await response.ToBytes();
        var resultString = Encoding.UTF8.GetString(result.Span);

        // Assert
        Assert.Contains("HTTP/1.1 200 OK\r\n", resultString);
        Assert.Contains("Custom-Header: HeaderValue\r\n", resultString);
        Assert.Contains("Content-Type: text/plain; charset=utf-8\r\n", resultString);
        Assert.Contains("Content-Length: 13\r\n", resultString);
        Assert.EndsWith("\r\n\r\nHello, World!", resultString);
    }

    [Fact]
    public async Task ToBytes_ResponseWithMultipleHeaders_ReturnsAllHeadersSerialized()
    {
        // Arrange
        var response = new System.Net.Http.HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ReasonPhrase = "OK",
            Version = new Version(1, 1)
        };
        response.Headers.Add("Header1", "Value1");
        response.Headers.Add("Header2", "Value2");

        // Act
        var result = await response.ToBytes();
        var resultString = Encoding.UTF8.GetString(result.Span);

        // Assert
        Assert.Contains("Header1: Value1\r\n", resultString);
        Assert.Contains("Header2: Value2\r\n", resultString);
    }

    [Fact]
    public async Task ToBytes_ResponseWithEmptyContent_ReturnsHeadersOnly()
    {
        // Arrange
        var response = new System.Net.Http.HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.NoContent,
            ReasonPhrase = "No Content",
            Version = new Version(1, 1),
            Content = new StringContent(string.Empty)
        };

        // Act
        var result = await response.ToBytes();
        var resultString = Encoding.UTF8.GetString(result.Span);

        // Assert
        Assert.Contains("HTTP/1.1 204 No Content\r\n", resultString);
        Assert.EndsWith("\r\n\r\n", resultString);
    }

    [Fact]
    public async Task ToBytes_ResponseWithLargeContent_ReturnsSerializedResponse()
    {
        // Arrange
        var largeContent = new string('A', 1000);
        var response = new System.Net.Http.HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ReasonPhrase = "OK",
            Version = new Version(1, 1),
            Content = new StringContent(largeContent, Encoding.UTF8, "text/plain")
        };
        response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(largeContent);

        // Act
        var result = await response.ToBytes();
        var resultString = Encoding.UTF8.GetString(result.Span);

        // Assert
        Assert.Contains("HTTP/1.1 200 OK\r\n", resultString);
        Assert.Contains($"Content-Length: {largeContent.Length}\r\n", resultString);
        Assert.EndsWith($"\r\n\r\n{largeContent}", resultString);
    }
}
