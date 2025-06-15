using WebHost.MemoryBuffers;

namespace WebHost;

/// <summary>
/// Represents a minimal abstraction of an HTTP request containing essential routing information.
/// </summary>
public interface IHttpRequest
{
    /// <summary>
    /// Gets the route or path portion of the HTTP request, typically used to determine the target endpoint.
    /// </summary>
    string Route { get; init; }

    /// <summary>
    /// Gets the HTTP method of the request (e.g., "GET", "POST", "PUT", "DELETE"), 
    /// which indicates the action to be performed on the resource.
    /// </summary>
    string HttpMethod { get; init; }

    /// <summary>
    /// Gets the raw binary body of the HTTP request, if any.
    /// For methods like POST or PUT, this may contain payload data such as JSON or form data.
    /// </summary>
    byte[]? Body { get; init; }

    /// <summary>
    /// Gets the query string parameters portion of the request URI, 
    /// typically represented as a URL-encoded string following the '?' in the URI.
    /// </summary>
    string QueryParameters { get; init; }

    /// <summary>
    /// Gets the collection of HTTP request headers as raw strings, typically in the format "HeaderName: HeaderValue".
    /// </summary>
    PooledDictionary<string, string> Headers { get; init; }
}