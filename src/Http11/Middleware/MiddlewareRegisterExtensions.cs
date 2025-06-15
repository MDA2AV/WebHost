using Microsoft.Extensions.DependencyInjection;
using WebHost.Http11.Context;
using WebHost.Protocol;

namespace WebHost.Http11.Middleware;

/// <summary>
/// Provides extension methods for registering middleware
/// into the <see cref="WebHostBuilder{THandler, TContext}"/> pipeline.
/// </summary>
public static class MiddlewareRegisterExtensions
{
    /// <summary>
    /// Adds response-writing middleware to the request processing pipeline.
    /// This middleware runs after the next delegate in the pipeline and finalizes the HTTP response by:
    /// <list type="bullet">
    /// <item>Writing the HTTP status line.</item>
    /// <item>Injecting standard and user-defined HTTP headers.</item>
    /// <item>Handling content-length or chunked encoding based on response state.</item>
    /// <item>Streaming the response body content if present.</item>
    /// </list>
    ///
    /// <para>
    /// It uses <see cref="ResponseMiddleware.HandleAsync(IContext, uint)"/> to write the complete HTTP/1.1 response to the output stream
    /// after all previous middleware and endpoint handlers have executed.
    /// </para>
    ///
    /// <para>
    /// This middleware is intended to be the last in the pipeline before the response is sent,
    /// ensuring headers and content are flushed once the request has been handled.
    /// </para>
    /// </summary>
    /// <param name="builder">
    /// The <see cref="WebHostBuilder{THandler, TContext}"/> instance to extend.
    /// </param>
    /// <returns>
    /// The same <see cref="WebHostBuilder{THandler, TContext}"/> instance, allowing method chaining.
    /// </returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> UseResponse(
        this WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> builder)
    {
        Func<IServiceProvider, Func<Http11Context, Func<Http11Context, Task>, Task>> func =
            scope => async (ctx, next) =>
            {
                await next(ctx);
                await ResponseMiddleware.HandleAsync(ctx);
            };

        builder.App.HostBuilder.ConfigureServices((_, services) =>
            services.AddScoped<Func<Http11Context, Func<Http11Context, Task>, Task>>(func));

        return builder;
    }
}