using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using WebHost.Http11;
using WebHost.Http11.Context;

namespace WebHost;

public static class HttpConstants
{
    public const string Get = "GET";
    public const string Post = "POST";
    public const string Put = "PUT";
    public const string Delete = "DELETE";
    public const string Patch = "PATCH";
    public const string Head = "HEAD";
    public const string Options = "OPTIONS";
}

public sealed class WebHostApp
{
    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> with default SSL protocol set to HTTP/1.1.
    /// </summary>
    /// <returns>A new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> configured with the default SSL protocol.</returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder()
    {
        return CreateBuilder([SslApplicationProtocol.Http11]);
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> with a list of SSL application protocols, using the default handler configuration for HTTP/1.1.
    /// </summary>
    /// <param name="sslApplicationProtocols">A list of SSL application protocols to be used for the web host.</param>
    /// <returns>A new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> configured with the provided SSL application protocols.</returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder(
        List<SslApplicationProtocol> sslApplicationProtocols)
    {
        return new WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context>((app) =>
            new WebHostHttp11<Http11Context>(app,
                new Http11HandlerArgs(
                    false,
                    null!,
                    null!,
                    true)), sslApplicationProtocols);
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> using a custom handler factory and default SSL protocol set to HTTP/1.1.
    /// </summary>
    /// <param name="handlerFactory">A function to create a new instance of <see cref="WebHostHttp11{Http11Context}"/>.</param>
    /// <returns>A new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> configured with the provided handler factory and default SSL protocol.</returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder(
        Func<WebHostApp<Http11Context>, WebHostHttp11<Http11Context>> handlerFactory)
    {
        return CreateBuilder(handlerFactory, [SslApplicationProtocol.Http11]);
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> using a custom handler factory and a list of SSL application protocols.
    /// </summary>
    /// <param name="handlerFactory">A function to create a new instance of <see cref="WebHostHttp11{Http11Context}"/>.</param>
    /// <param name="sslApplicationProtocols">A list of SSL application protocols to be used for the web host.</param>
    /// <returns>A new instance of <see cref="WebHostBuilder{WebHostHttp11, Http11Context}"/> configured with the provided handler factory and SSL application protocols.</returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder(
        Func<WebHostApp<Http11Context>, WebHostHttp11<Http11Context>> handlerFactory,
        List<SslApplicationProtocol> sslApplicationProtocols)
    {
        return new WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context>(
            handlerFactory,
            sslApplicationProtocols);
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> with a custom handler factory and default SSL protocol set to HTTP/1.1.
    /// </summary>
    /// <typeparam name="THandler">The type of the HTTP handler.</typeparam>
    /// <typeparam name="TContext">The type of the context used by the handler.</typeparam>
    /// <param name="handlerFactory">A function to create a new instance of <see cref="THandler"/>.</param>
    /// <returns>A new instance of <see cref="WebHostBuilder{THandler, TContext}"/> configured with the provided handler factory and default SSL protocol.</returns>
    public static WebHostBuilder<THandler, TContext> CreateBuilder<THandler, TContext>(Func<WebHostApp<TContext>, THandler> handlerFactory)
        where THandler : IHttpHandler<TContext>
        where TContext : IContext
    {
        return CreateBuilder<THandler, TContext>(handlerFactory, [SslApplicationProtocol.Http11]);
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> using a custom handler factory and a list of SSL application protocols.
    /// </summary>
    /// <typeparam name="THandler">The type of the HTTP handler.</typeparam>
    /// <typeparam name="TContext">The type of the context used by the handler.</typeparam>
    /// <param name="handlerFactory">A function to create a new instance of <see cref="THandler"/>.</param>
    /// <param name="sslApplicationProtocols">A list of SSL application protocols to be used for the web host.</param>
    /// <returns>A new instance of <see cref="WebHostBuilder{THandler, TContext}"/> configured with the provided handler factory and SSL application protocols.</returns>
    public static WebHostBuilder<THandler, TContext> CreateBuilder<THandler, TContext>(
        Func<WebHostApp<TContext>, THandler> handlerFactory,
        List<SslApplicationProtocol> sslApplicationProtocols)
        where THandler : IHttpHandler<TContext>
        where TContext : IContext
    {
        return new WebHostBuilder<THandler, TContext>(handlerFactory, sslApplicationProtocols);
    }
}

/// <summary>
/// Represents the core application instance for a lightweight web host configured for a specific context.
/// </summary>
/// <typeparam name="TContext">
/// The type of the context passed through the pipeline during request processing. Must implement <see cref="IContext"/>.
/// </typeparam>
/// <remarks>
/// This class holds runtime configuration, registered routes, and internal state for a web application hosted using sockets.
/// </remarks>
public sealed partial class WebHostApp<TContext> where TContext : IContext
{
    #region Public Properties

    /// <summary>
    /// Gets or sets the TLS configuration used by the application, including certificate and protocol settings.
    /// </summary>
    public SslServerAuthenticationOptions SslServerAuthenticationOptions { get; set; } =
        new SslServerAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.None
        };

    /// <summary>
    /// Gets or sets the route mapping table that associates HTTP methods with encoded route strings.
    /// </summary>
    /// <remarks>
    /// Each entry maps an HTTP verb (e.g., GET, POST) to a set of route patterns associated with that method.
    /// </remarks>
    public Dictionary<string, HashSet<string>> EncodedRoutes { get; set; } = new()
    {
        { HttpConstants.Get, [] },
        { HttpConstants.Post, [] },
        { HttpConstants.Put, [] },
        { HttpConstants.Delete, [] },
        { HttpConstants.Patch, [] },
        { HttpConstants.Head, [] },
        { HttpConstants.Options, [] },
    };

    #endregion

    #region Internal Properties

    /// <summary>
    /// Gets or sets the IP address the application will bind to.
    /// </summary>
    internal IPAddress IpAddress { get; set; } = IPAddress.Parse("127.0.0.1");

    /// <summary>
    /// Gets or sets the port the application will listen on.
    /// </summary>
    internal int Port { get; set; } = 9001;

    /// <summary>
    /// Gets or sets the maximum number of pending TCP connections.
    /// </summary>
    internal int Backlog { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether TLS is enabled for the application.
    /// </summary>
    internal bool TlsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the logger factory used by the application.
    /// </summary>
    internal ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets the main application logger.
    /// </summary>
    internal ILogger? Logger { get; set; }

    /// <summary>
    /// Gets or sets the main connection-level handler responsible for managing the transport protocol.
    /// </summary>
    internal IHttpHandler<TContext> HttpHandler { get; set; } = null!;

    #endregion
}