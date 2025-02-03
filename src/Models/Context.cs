using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace WebHost.Models;

public record Http11Request(
    IEnumerable<string> Headers,
    string? Body,
    string Route,
    string HttpMethod,
    string QueryParameters,
    int StreamId = 0) : IHttpRequest;

public record Http2Request(
    IEnumerable<string> Headers, 
    string Route, 
    string HttpMethod, 
    string QueryParameters,
    int StreamId,
    string? Body = null!) : IHttpRequest;

public interface IHttpRequest
{
    IEnumerable<string> Headers { get; init; }
    string? Body { get; init; }
    string Route { get; init; }
    string HttpMethod { get; init; }
    string QueryParameters { get; init; }
    int StreamId { get; init; }
}

public class Http11
{
    public HttpResponseMessage Response { get; set; } = null!;
}

/*
public class Http2
{
    public BlockingCollection<FrameData> StreamBuffer { get; set; } = null!;
    public Encoder Encoder { get; set; } = null!;
    public Decoder Decoder { get; set; } = null!;
}
*/

public class Context(Stream stream) : IContext
{
    public Stream Stream { get; set; } = stream;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();

    public HttpResponseMessage Response { get; set; } = null!;
    public IHttpRequest Request { get; set; } = null!;

    public Http11 Http11 { get; set; } = null!;
    //public Http2 Http2 { get; set; } = null!;

    public CancellationToken CancellationToken { get; set; }
}

/*
public class Http11Context(Stream stream) : IContext
{
    public Stream Stream { get; set; } = stream;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IHttpRequest Request { get; set; } = null!;
    public HttpResponseMessage Response { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}

public class Http2Context(Stream stream) : IContext
{
    public Stream Stream { get; set; } = stream;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IHttpRequest Request { get; set; } = null!;
    public BlockingCollection<FrameData> StreamBuffer { get; set; } = null!;
    public Encoder Encoder { get; set; } = null!;
    public Decoder Decoder { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}*/