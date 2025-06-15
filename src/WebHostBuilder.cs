using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Security;
using System.Net;
using System.Reflection;
using WebHost.Attributes;
using WebHost.Protocol;
using WebHost.Protocol.Response;

namespace WebHost;

/// <summary>
/// Provides a builder for configuring and creating an instance of <see cref="WebHostApp{TContext}"/>.
/// </summary>
/// <typeparam name="THandler">The connection handler type responsible for managing protocol-specific client communication.</typeparam>
/// <typeparam name="TContext">The application context type used during request execution.</typeparam>
/// <remarks>
/// This builder enables fluent configuration of an application server, including IP bindings, TLS, middleware, and routing.
/// </remarks>
public sealed class WebHostBuilder<THandler, TContext>
    where THandler : IHttpHandler<TContext>
    where TContext : IContext
{
    /// <summary>
    /// Initializes a new instance using the specified handler factory.
    /// </summary>
    /// <param name="handlerFactory">Factory that creates an instance of <typeparamref name="THandler"/>.</param>
    public WebHostBuilder(Func<THandler> handlerFactory)
    {
        App.HttpHandler = handlerFactory();
        App.SslServerAuthenticationOptions.ApplicationProtocols = [SslApplicationProtocol.Http11];
    }

    /// <summary>
    /// Initializes a new instance using the specified handler factory and ALPN protocol list.
    /// </summary>
    /// <param name="handlerFactory">Factory that creates an instance of <typeparamref name="THandler"/>.</param>
    /// <param name="sslApplicationProtocols">A list of supported TLS ALPN protocols.</param>
    public WebHostBuilder(Func<THandler> handlerFactory, List<SslApplicationProtocol> sslApplicationProtocols)
    {
        App.HttpHandler = handlerFactory();
        App.SslServerAuthenticationOptions.ApplicationProtocols = sslApplicationProtocols;
    }

    /// <summary>
    /// Gets the underlying <see cref="WebHostApp{TContext}"/> being configured.
    /// </summary>
    public WebHostApp<TContext> App { get; } = new WebHostApp<TContext>();

    /// <summary>
    /// Finalizes and builds the configured <see cref="WebHostApp{TContext}"/>.
    /// </summary>
    public WebHostApp<TContext> Build()
    {
        App.InternalHost = App.HostBuilder.Build();
        App.LoggerFactory = App.InternalHost.Services.GetRequiredService<ILoggerFactory>();
        App.Logger = App.LoggerFactory.CreateLogger<WebHostApp<TContext>>();
        return App;
    }

    /// <summary>
    /// Enables TLS using the specified server authentication options.
    /// </summary>
    public WebHostBuilder<THandler, TContext> UseTls(SslServerAuthenticationOptions sslServerAuthenticationOptions)
    {
        App.TlsEnabled = true;
        App.SslServerAuthenticationOptions = sslServerAuthenticationOptions;
        return this;
    }

    /// <summary>
    /// Sets the IP address and port to which the server will bind.
    /// </summary>
    public WebHostBuilder<THandler, TContext> Endpoint(IPAddress ipAddress, int port)
    {
        App.IpAddress = ipAddress;
        App.Port = port;
        return this;
    }

    /// <summary>
    /// Sets the IP address (as a string) and port to which the server will bind.
    /// </summary>
    public WebHostBuilder<THandler, TContext> Endpoint(string ipAddress, int port)
    {
        App.IpAddress = IPAddress.Parse(ipAddress);
        App.Port = port;
        return this;
    }

    /// <summary>
    /// Sets the port to which the server will bind.
    /// </summary>
    public WebHostBuilder<THandler, TContext> Port(int port)
    {
        App.Port = port;
        return this;
    }

    /// <summary>
    /// Sets the backlog size for pending TCP connections.
    /// </summary>
    public WebHostBuilder<THandler, TContext> Backlog(int backlog)
    {
        App.Backlog = backlog;
        return this;
    }

    /// <summary>
    /// Registers request execution logic types found in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for types implementing <c>IRequestHandler&lt;,&gt;</c>.</param>
    /// <remarks>
    /// Only types marked with <see cref="KeyAttribute"/> are considered.
    /// </remarks>
    public WebHostBuilder<THandler, TContext> AddHandlers(Assembly assembly)
    {
        App.HostBuilder.ConfigureServices((_, services) =>
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false })
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                    .Select(i => new { HandlerType = t, InterfaceType = i }))
                .ToList();

            foreach (var handler in handlerTypes)
            {
                var keyAttribute = handler.HandlerType.GetCustomAttribute<KeyAttribute>();
                if (keyAttribute == null)
                {
                    Console.WriteLine($"Builder: [Type {handler.HandlerType.Name} does not have a KeyAttribute and will not be registered.]");
                    continue;
                }

                services.AddKeyedScoped(handler.InterfaceType, keyAttribute.Key,
                    (sp, _) => ActivatorUtilities.CreateInstance(sp, handler.HandlerType));
            }
        });

        return this;
    }

    /// <summary>
    /// Adds a middleware delegate to the processing pipeline.
    /// This overload supports middleware that returns <see cref="Task"/> and does not produce a response directly.
    /// </summary>
    /// <param name="func">
    /// A factory delegate that resolves a middleware delegate using DI.
    /// The middleware delegate accepts a <typeparamref name="TContext"/> and a next delegate representing the remaining pipeline.
    /// </param>
    public WebHostBuilder<THandler, TContext> UseMiddleware(Func<IServiceProvider, Func<TContext, Func<TContext, Task>, Task>> func)
    {
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddScoped<Func<TContext, Func<TContext, Task>, Task>>(func));
        return this;
    }
    /// <summary>
    /// Adds a middleware delegate to the processing pipeline.
    /// This overload supports middleware that returns <see cref="Task{IResponse}"/>.
    /// </summary>
    /// <param name="func">
    /// A factory delegate that resolves a middleware delegate using DI.
    /// The middleware delegate accepts a <typeparamref name="TContext"/> and a next delegate representing the remaining pipeline.
    /// </param>
    public WebHostBuilder<THandler, TContext> UseMiddleware(Func<IServiceProvider, Func<TContext, Func<TContext, Task<IResponse>>, Task<IResponse>>> func)
    {
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddScoped<Func<TContext, Func<TContext, Task<IResponse>>, Task<IResponse>>>(func));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP GET requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/users").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapGet(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Get}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP GET requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/users").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapGet(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Get}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP GET requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPost(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Post].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Post}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP GET requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPost(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Post}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP PUT requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPut(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Put].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Put}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP PUT requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPut(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Put}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP DELETE requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapDelete(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Delete].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Delete}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP DELETE requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapDelete(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Delete}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP PATCH requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPatch(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Patch].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Patch}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP PATCH requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapPatch(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Patch}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP HEAD requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapHead(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Head].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Head}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP HEAD requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapHead(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Head}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }

    /// <summary>
    /// Registers a delegate for processing HTTP OPTIONS requests asynchronously.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapOptions(string route, Func<IServiceProvider, Func<TContext, Task>> func)
    {
        App.EncodedRoutes[HttpConstants.Options].Add(route);
        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Options}_{route}", (sp, key) => func(sp)));
        return this;
    }
    /// <summary>
    /// Registers a synchronous delegate for processing HTTP OPTIONS requests.
    /// The handler will be wrapped in a <see cref="Task"/> for compatibility with the async pipeline.
    /// </summary>
    /// <param name="route">The route template (e.g., "/resource").</param>
    /// <param name="func">A factory delegate that resolves a synchronous request handler using DI.</param>
    public WebHostBuilder<THandler, TContext> MapOptions(string route, Func<IServiceProvider, Action<TContext>> func)
    {
        App.EncodedRoutes[HttpConstants.Get].Add(route);

        Func<TContext, Task> AsyncFunc(IServiceProvider sp)
        {
            Action<TContext> action = func(sp);
            return context =>
            {
                action(context);
                return Task.CompletedTask;
            };
        }

        App.HostBuilder.ConfigureServices((_, services) =>
            services.AddKeyedScoped<Func<TContext, Task>>($"{HttpConstants.Options}_{route}", (sp, key) => AsyncFunc(sp)));
        return this;
    }
}