using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
namespace WebHost.Models;

public record Http11Request(
    IEnumerable<string> Headers,
    byte[]? Body,
    string Route,
    string HttpMethod,
    string QueryParameters) : IHttpRequest;

public interface IHttpRequest
{
    IEnumerable<string> Headers { get; init; }
    byte[]? Body { get; init; }
    string Route { get; init; }
    string HttpMethod { get; init; }
    string QueryParameters { get; init; }
}

public class Context(Stream stream) : IContext
{
    public Stream Stream { get; set; } = stream;
    public PipeReader PipeReader { get; set; } = null!;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public HttpResponseMessage Response { get; set; } = null!;
    public IHttpRequest Request { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}