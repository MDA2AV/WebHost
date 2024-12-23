using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WebHost.Attributes;

namespace WebHost.Extensions;

/// <summary>
/// Provides extension methods for configuring and enhancing the functionality of <see cref="WebHostApp.WebHostBuilder"/>.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Maps a route to a request-handling delegate within the application.
    /// </summary>
    /// <param name="builder">The <see cref="WebHostApp.WebHostBuilder"/> instance being configured.</param>
    /// <param name="route">The route to associate with the delegate.</param>
    /// <param name="func">
    /// A function that takes an <see cref="IServiceProvider"/> and returns a delegate to handle requests
    /// for the specified route.
    /// </param>
    /// <remarks>
    /// - Registers a route-specific handler as a keyed scoped service.
    /// - Enables dynamic resolution of request handlers based on the route.
    /// </remarks>
    public static void Map(this WebHostApp.WebHostBuilder builder, string route,
        Func<IServiceProvider, Func<IContext, Task>> func)
    {
        builder.App.HostBuilder.ConfigureServices((context, services) =>
            services.AddKeyedScoped<Func<IContext, Task>>(route, (sp, key) => func(sp)));
    }

    /// <summary>
    /// Adds middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="WebHostApp.WebHostBuilder"/> instance being configured.</param>
    /// <param name="func">
    /// A function that takes an <see cref="IServiceProvider"/> and returns a middleware delegate,
    /// which processes an <see cref="IContext"/> and invokes the next middleware in the pipeline.
    /// </param>
    /// <remarks>
    /// - Registers the middleware as a scoped service.
    /// - Middleware is executed in the order it is registered.
    /// </remarks>
    public static void UseMiddleware(this WebHostApp.WebHostBuilder builder,
        Func<IServiceProvider, Func<IContext, Func<IContext, Task>, Task>> func)
    {
        builder.App.HostBuilder.ConfigureServices((context, services) =>
            services.AddScoped<Func<IContext, Func<IContext, Task>, Task>>(func));
    }

    /// <summary>
    /// Automatically discovers and registers handlers for request processing based on an assembly.
    /// </summary>
    /// <param name="builder">The <see cref="WebHostApp.WebHostBuilder"/> instance being configured.</param>
    /// <param name="assembly">The assembly to scan for handler implementations.</param>
    /// <returns>The configured <see cref="WebHostApp.WebHostBuilder"/> instance.</returns>
    /// <remarks>
    /// - Scans the provided assembly for classes implementing <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// - Registers discovered handlers as keyed services based on their associated <see cref="RouteAttribute"/>.
    /// - Handlers without a <see cref="RouteAttribute"/> are ignored, and a warning is logged.
    /// - Enables dynamic resolution of handlers based on routes.
    /// </remarks>
    public static WebHostApp.WebHostBuilder AddHandlers(this WebHostApp.WebHostBuilder builder, Assembly assembly)
    {
        builder.App.HostBuilder.ConfigureServices((context, services) =>
        {
            // Scan for all types implementing IRequestHandler<TRequest, TResponse>
            var handlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false })
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                    .Select(i => new { HandlerType = t, InterfaceType = i }))
                .ToList();

            foreach (var handler in handlerTypes)
            {
                // Check if the class has a RouteAttribute
                var routeAttribute = handler.HandlerType.GetCustomAttribute<RouteAttribute>();
                if (routeAttribute == null)
                {
                    Console.WriteLine($"Builder: [Handler {handler.HandlerType.Name} does not have a RouteAttribute and will not be registered.]");
                    continue;
                }

                var routeKey = routeAttribute.Route;

                // Register the handler as a keyed service
                services.AddKeyedScoped(handler.InterfaceType, routeKey, (sp, key) =>
                    ActivatorUtilities.CreateInstance(sp, handler.HandlerType));

                Console.WriteLine($"Builder: [Registered {handler.HandlerType} as {handler.InterfaceType} with key '{routeKey}']");
            }
        });

        return builder;
    }
}