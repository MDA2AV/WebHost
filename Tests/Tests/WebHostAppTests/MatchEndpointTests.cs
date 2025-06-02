using WebHost;
using WebHost.Http11.Context;

namespace Tests.WebHostAppTests;

public class MatchEndpointTests
{
    [Fact]
    public void MatchEndpoint_ShouldReturnFirstMatchingPattern()
    {
        // Arrange
        var hashSet = new HashSet<string>
        {
            "/users/:id",
            "/products/:productId",
            "/orders/:orderId"
        };
        const string input = "/products/456";

        // Act
        var result = WebHostApp<Http11Context>.MatchEndpoint(hashSet, input);

        // Assert
        Assert.Equal("/products/:productId", result);
    }

    [Fact]
    public void MatchEndpoint_ShouldReturnNullIfNoMatchFound()
    {
        // Arrange
        var hashSet = new HashSet<string>
        {
            "/users/:id",
            "/products/:productId",
            "/orders/:orderId"
        };
        const string input = "/categories/123";

        // Act
        var result = WebHostApp<Http11Context>.MatchEndpoint(hashSet, input);

        // Assert
        Assert.Null(result);
    }
}

