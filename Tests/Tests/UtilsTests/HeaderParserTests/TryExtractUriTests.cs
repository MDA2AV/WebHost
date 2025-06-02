using WebHost.Http11;
using WebHost.Http11.Context;

namespace Tests.UtilsTests.HeaderParserTests;

public class TryExtractUriTests
{
    [Fact]
    public void TryExtractUri_ValidGetRequest_ReturnsTrueAndExtractsRoute()
    {
        // Arrange
        var headers = new List<string> { "GET /api/resource?param=1 HTTP/1.1" };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.True(result);
        Assert.Equal("GET", route.Item1);
        Assert.Equal("/api/resource?param=1", route.Item2);
    }

    [Fact]
    public void TryExtractUri_ValidPostRequest_ReturnsTrueAndExtractsRoute()
    {
        // Arrange
        var headers = new List<string> { "POST /submit/data HTTP/1.1" };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.True(result);
        Assert.Equal("POST", route.Item1);
        Assert.Equal("/submit/data", route.Item2);
    }

    [Fact]
    public void TryExtractUri_MultipleHeadersWithValidRequest_ReturnsTrueAndExtractsFirstRoute()
    {
        // Arrange
        var headers = new List<string> {
            "Host: localhost",
            "GET /api/resource HTTP/1.1",
            "POST /submit/data HTTP/1.1"
        };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.True(result);
        Assert.Equal("GET", route.Item1);
        Assert.Equal("/api/resource", route.Item2);
    }

    [Fact]
    public void TryExtractUri_NoMatchingRequestLine_ReturnsFalse()
    {
        // Arrange
        var headers = new List<string> { "Host: localhost", "Content-Type: application/json" };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.False(result);
        Assert.Equal(default, route);
    }

    [Fact]
    public void TryExtractUri_InvalidHttpMethod_ReturnsFalse()
    {
        // Arrange
        var headers = new List<string> { "FOO /api/resource HTTP/1.1" };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.False(result);
        Assert.Equal(default, route);
    }

    [Fact]
    public void TryExtractUri_EmptyHeaders_ReturnsFalse()
    {
        // Arrange
        var headers = new List<string>();

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.False(result);
        Assert.Equal(default, route);
    }

    [Fact]
    public void TryExtractUri_NullHeaders_ReturnsFalse()
    {
        // Arrange
        IEnumerable<string>? headers = null;

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.False(result);
        Assert.Equal(default, route);
    }

    [Fact]
    public void TryExtractUri_ValidRequestWithWhitespaceAround_ReturnsTrueAndExtractsRoute()
    {
        // Arrange
        var headers = new List<string> { "  GET   /api/resource  HTTP/1.1   " };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.True(result);
        Assert.Equal("GET", route.Item1);
        Assert.Equal("/api/resource", route.Item2);
    }

    [Fact]
    public void TryExtractUri_InvalidHttpVersion_ReturnsFalse()
    {
        // Arrange
        var headers = new List<string> { "GET /api/resource HTTP/2.0" };

        // Act
        var result = WebHostHttp11<Http11Context>.TryExtractUri(headers, out var route);

        // Assert
        Assert.True(result);
        Assert.Equal("GET", route.Item1);
        Assert.Equal("/api/resource", route.Item2);
    }
}
