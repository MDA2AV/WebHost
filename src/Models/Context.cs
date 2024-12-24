using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Net.Sockets;

namespace WebHost.Models;

public record Request(IEnumerable<string> Headers,
                      string Body,
                      string Route,
                      string HttpMethod);


public class Context(Socket socket) : IContext
{
    public Socket? Socket { get; set; } = socket;
    public SslStream? SslStream { get; set; }
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public HttpResponseMessage HttpResponseMessage { get; set; } = null!;
    public Request Request { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}