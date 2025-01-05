using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;

namespace WebHost.Models;

public record Request(IEnumerable<string> Headers,
                      string Body,
                      string Route,
                      string QueryParameters,
                      string HttpMethod);


public class Context : IContext
{
    public Context(Socket socket)
    {
        Socket = socket;
    }

    public Context(SslStream sslStream)
    {
        SslStream = sslStream;
    }

    public Socket? Socket { get; set; }
    public SslStream? SslStream { get; set; }
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public Request Request { get; set; } = null!;
    public HttpResponseMessage Response { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}

public class ContextH2(SslStream sslStream) : IContext
{
    public Socket? Socket { get; set; }
    public SslStream? SslStream { get; set; } = sslStream;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public HttpResponseMessage Response { get; set; } = null!;
    public Request Request { get; set; } = null!;
    public BlockingCollection<FrameData> StreamBuffer { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}