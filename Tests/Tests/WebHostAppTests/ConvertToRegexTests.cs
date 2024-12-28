using System.Text.RegularExpressions;
using WebHost;

namespace Tests.WebHostAppTests;

public class ConvertToRegexTests
{
    [Fact]
    public void ConvertToRegex_ShouldConvertPlaceholdersToRegex()
    {
        // Arrange
        var pattern = "/users/:id/details";

        // Act
        var regexPattern = WebHostApp.ConvertToRegex(pattern);

        // Assert
        Assert.Equal("^/users/[^/]+/details$", regexPattern);
    }

    [Theory]
    [InlineData("/users/:id", "/users/123", true)]
    [InlineData("/users/:id", "/users/abc", true)]
    [InlineData("/users/:id", "/users/123/details", false)]
    [InlineData("/products/:productId", "/products/456", true)]
    [InlineData("/products/:productId", "/orders/456", false)]
    public void ConvertToRegex_ShouldMatchInputCorrectly(string pattern, string input, bool isMatch)
    {
        // Arrange
        var regexPattern = WebHostApp.ConvertToRegex(pattern);

        // Act
        var match = Regex.IsMatch(input, regexPattern);

        // Assert
        Assert.Equal(isMatch, match);
    }
}