using Microsoft.Extensions.DependencyInjection;

namespace WebHost.Http11.Context;

public class Http11Context : IContext
{
    public Stream Stream { get; set; } = null!;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public HttpResponseMessage Response { get; set; } = null!;
    public IHttpRequest Request { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}