using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using WebHost.Hpack;

namespace WebHost.Models;

public interface IHttpRequest
{
    IEnumerable<string> Headers { get; init; }
    string Route { get; init; }
    string QueryParameters { get; init; }
    string HttpMethod { get; init; }
}

public record Http11Request(
    IEnumerable<string> Headers,
    string Body,
    string Route,
    string QueryParameters,
    string HttpMethod) : IHttpRequest;

public record Http2Request(
    IEnumerable<string> Headers, 
    string Route, 
    string HttpMethod, 
    string QueryParameters,
    int StreamId) : IHttpRequest;

public class Http11Context : IContext
{
    public Http11Context(Socket socket)
    {
        Socket = socket;
    }

    public Http11Context(SslStream sslStream)
    {
        SslStream = sslStream;
    }

    public Socket? Socket { get; set; }
    public SslStream? SslStream { get; set; }
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IHttpRequest Request { get; set; } = null!;
    public HttpResponseMessage Response { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}

public class Http2Context(SslStream sslStream) : IContext
{
    public Socket? Socket { get; set; }
    public SslStream? SslStream { get; set; } = sslStream;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IHttpRequest Request { get; set; } = null!;
    public BlockingCollection<FrameData> StreamBuffer { get; set; } = null!;
    public Encoder Encoder { get; set; } = null!;
    public Decoder Decoder { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}