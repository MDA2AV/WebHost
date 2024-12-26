using WebHost.Exceptions;
using WebHost.Utils;

namespace Tests.UtilsTests.HeaderParserTests;

public class HttpRequestParserTests
{
    [Fact]
    public void SplitHeadersAndBody_ValidRequest_ReturnsHeadersAndBody()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 5\r\n\r\nHello";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal(3, result.Headers.Length); // 3 headers including Content-Length
        Assert.Contains("Content-Length: 5", result.Headers);
        Assert.Equal("Hello", result.Body);
    }

    [Fact]
    public void SplitHeadersAndBody_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullServiceException>(() => RequestParser.SplitHeadersAndBody(null));
    }

    [Fact]
    public void SplitHeadersAndBody_NoHeaderBodySeparator_ThrowsInvalidOperationException()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => RequestParser.SplitHeadersAndBody(rawRequest));
        Assert.Equal("Malformed HTTP request: No header-body separator found.", exception.Message);
    }

    [Fact]
    public void SplitHeadersAndBody_InvalidContentLength_ThrowsInvalidOperationException()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost\r\nContent-Length: Invalid\r\n\r\nHello";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => RequestParser.SplitHeadersAndBody(rawRequest));
        Assert.Equal("Invalid Content-Length header.", exception.Message);
    }

    [Fact]
    public void SplitHeadersAndBody_MissingContentLength_ReturnsHeadersAndEmptyBody()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\nHello";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal(2, result.Headers.Length); // 2 headers excluding Content-Length
        Assert.Equal(string.Empty, result.Body); // No Content-Length, so body is empty
    }

    [Fact]
    public void SplitHeadersAndBody_BodyLengthLessThanContentLength_ReturnsEmptyBody()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 10\r\n\r\nShort";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal(3, result.Headers.Length);
        Assert.Contains("Content-Length: 10", result.Headers);
        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void SplitHeadersAndBody_EmptyRequest_ReturnsHeadersAndEmptyBody()
    {
        // Arrange
        var rawRequest = "\r\n\r\n";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal([""], result.Headers);
        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void SplitHeadersAndBody_OnlyHeaders_NoBody_ReturnsHeadersAndEmptyBody()
    {
        // Arrange
        var rawRequest = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal(2, result.Headers.Length);
        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public void SplitHeadersAndBody_LargeBody_ContentLengthMatches_ReturnsBody()
    {
        // Arrange
        var largeBody = new string('A', 1000);
        var rawRequest = $"POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: {largeBody.Length}\r\n\r\n{largeBody}";

        // Act
        var result = RequestParser.SplitHeadersAndBody(rawRequest);

        // Assert
        Assert.Equal(3, result.Headers.Length);
        Assert.Contains($"Content-Length: {largeBody.Length}", result.Headers);
        Assert.Equal(largeBody, result.Body);
    }
}
