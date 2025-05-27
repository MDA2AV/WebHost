namespace WebHost.Http11.Context;

public record Http11Request(
    IEnumerable<string> Headers,
    byte[]? Body,
    string Route,
    string HttpMethod,
    string QueryParameters) : IHttpRequest;