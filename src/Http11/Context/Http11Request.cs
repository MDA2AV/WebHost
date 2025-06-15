using WebHost.MemoryBuffers;

namespace WebHost.Http11.Context;

public record Http11Request(
    PooledDictionary<string, string> Headers,
    byte[]? Body,
    string Route,
    string HttpMethod,
    string QueryParameters) : IHttpRequest;