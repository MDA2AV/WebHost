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
    internal int Backlog { get; set; } = 10;

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

    #region Static Builder Entry Points

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> using the specified handler factory.
    /// </summary>
    /// <typeparam name="THandler">
    /// The type implementing <see cref="IHttpHandler{TContext}"/> that manages client connections.
    /// </typeparam>
    /// <param name="handlerFactory">A delegate that produces an instance of <typeparamref name="THandler"/>.</param>
    /// <returns>
    /// A new instance of <see cref="WebHostBuilder{THandler, TContext}"/> to configure the application.
    /// </returns>
    public static WebHostBuilder<THandler, TContext> CreateBuilder<THandler>(Func<THandler> handlerFactory)
        where THandler : IHttpHandler<TContext> =>
        new WebHostBuilder<THandler, TContext>(handlerFactory);

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> with custom TLS ALPN protocols.
    /// </summary>
    /// <typeparam name="THandler">
    /// The type implementing <see cref="IHttpHandler{TContext}"/> that manages client connections.
    /// </typeparam>
    /// <param name="handlerFactory">A delegate that produces an instance of <typeparamref name="THandler"/>.</param>
    /// <param name="sslApplicationProtocols">The list of supported application-layer protocols (e.g., HTTP/1.1, HTTP/2).</param>
    /// <returns>A new web host builder instance.</returns>
    public static WebHostBuilder<THandler, TContext> CreateBuilder<THandler>(
        Func<THandler> handlerFactory,
        List<SslApplicationProtocol> sslApplicationProtocols)
        where THandler : IHttpHandler<TContext> =>
        new WebHostBuilder<THandler, TContext>(handlerFactory, sslApplicationProtocols);

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> preconfigured for HTTP/1.1 using <see cref="WebHostHttp11{TContext}"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="WebHostBuilder{THandler, TContext}"/> instance configured for HTTP/1.1 communication.
    /// </returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder() =>
        new WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context>(() =>
            new WebHostHttp11<Http11Context>(new Http11HandlerArgs
            {
                UseResources = false,
                ResourcesAssembly = null!,
                ResourcesPath = null!
            }));

    /// <summary>
    /// Creates a new instance of <see cref="WebHostBuilder{THandler, TContext}"/> for HTTP/1.1 with specified TLS ALPN protocols.
    /// </summary>
    /// <param name="sslApplicationProtocols">The ALPN protocols to support (e.g., HTTP/1.1, HTTP/2).</param>
    /// <returns>A web host builder for configuring the HTTP/1.1 server.</returns>
    public static WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context> CreateBuilder(
        List<SslApplicationProtocol> sslApplicationProtocols) =>
        new WebHostBuilder<WebHostHttp11<Http11Context>, Http11Context>(() =>
            new WebHostHttp11<Http11Context>(new Http11HandlerArgs
            {
                UseResources = false,
                ResourcesAssembly = null!,
                ResourcesPath = null!
            }), sslApplicationProtocols);

    #endregion
}