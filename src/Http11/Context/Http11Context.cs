using Microsoft.Extensions.DependencyInjection;
using WebHost.Http11.Response;
using WebHost.Protocol;
using WebHost.Protocol.Response;

namespace WebHost.Http11.Context;


public class Http11Context : IContext
{
    public Stream Stream { get; set; } = null!;
    public IHttpRequest Request { get; set; } = null!;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IResponse Response { get; set; } = null!;

    public IResponseBuilder Respond()
    {
        Response = new Http11Response();
        return new ResponseBuilder(Response).Status(ResponseStatus.Ok);
    }

    public CancellationToken CancellationToken { get; set; }
}