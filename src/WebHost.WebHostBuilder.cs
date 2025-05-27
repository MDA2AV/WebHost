using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using WebHost.Http11;

namespace WebHost;

public sealed partial class WebHostApp
{
    #region Properties

    public SslServerAuthenticationOptions SslServerAuthenticationOptions { get; set; } =
        new SslServerAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.None
        };

    #endregion

    #region Fields

    private IPAddress _ipAddress = IPAddress.Parse("127.0.0.1");
    private int _port = 9001;
    private int _backlog = 10;
    private bool _tlsEnabled;

    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;

    private IHttpHandler _httpHandler = null!;

    #endregion

    /// <summary>
    /// Provides a builder for configuring and creating an instance of <see cref="WebHostApp"/>.
    /// </summary>
    /// <remarks>
    /// - The <see cref="WebHostBuilder"/> class encapsulates configuration logic for the <see cref="WebHostApp"/> instance.
    /// - It provides a fluent API for setting up the application, including endpoints, security options, and middleware.
    /// - The final configuration is applied when <see cref="Build"/> is called, which produces a fully configured <see cref="WebHostApp"/>.
    /// </remarks>
    public sealed class WebHostBuilder
    {
        // Reference to the WebHostApp instance being configured
        //
        public WebHostApp App { get; } = new WebHostApp();

        /// <summary>
        /// Builds and returns the fully configured <see cref="WebHostApp"/> instance.
        /// Initializes internal services, logger factory, and logger.
        /// </summary>
        public WebHostApp Build()
        {
            App.InternalHost = App.HostBuilder.Build();

            App._loggerFactory = App.InternalHost.Services.GetRequiredService<ILoggerFactory>();
            App._logger = App._loggerFactory.CreateLogger<WebHostApp>();

            return App;
        }

        /// <summary>
        /// Enables TLS and applies the specified TLS settings to the application.
        /// </summary>
        /// <param name="sslServerAuthenticationOptions">TLS configuration options to apply.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder UseTls(SslServerAuthenticationOptions sslServerAuthenticationOptions)
        {
            App._tlsEnabled = true;
            App.SslServerAuthenticationOptions = sslServerAuthenticationOptions;

            return this;
        }

        /// <summary>
        /// Sets the IP address and port the application will bind to.
        /// </summary>
        /// <param name="ipAddress">The IP address to bind to.</param>
        /// <param name="port">The port number to listen on.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetEndpoint(IPAddress ipAddress, int port)
        {
            App._ipAddress = ipAddress;
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the IP address (as a string) and port the application will bind to.
        /// </summary>
        /// <param name="ipAddress">The IP address to bind to as a string.</param>
        /// <param name="port">The port number to listen on.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetEndpoint(string ipAddress, int port)
        {
            App._ipAddress = IPAddress.Parse(ipAddress);
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the port the application will listen on.
        /// </summary>
        /// <param name="port">The port number to use.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetPort(int port)
        {
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the connection backlog size for incoming TCP connections.
        /// </summary>
        /// <param name="backlog">The maximum number of pending connections.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetBacklog(int backlog)
        {
            App._backlog = backlog;
            return this;
        }

        /// <summary>
        /// Sets the HTTP handler and the application protocol used for TLS negotiation.
        /// </summary>
        /// <param name="httpHandler">The HTTP handler to use for processing requests.</param>
        /// <param name="sslApplicationProtocol">The ALPN protocol to advertise during the TLS handshake.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetHttpHandler(IHttpHandler httpHandler, SslApplicationProtocol sslApplicationProtocol)
        {
            App._httpHandler = httpHandler;
            App.SslServerAuthenticationOptions.ApplicationProtocols = [sslApplicationProtocol];
            return this;
        }

        /// <summary>
        /// Sets a default HTTP/1.1 handler that optionally serves static resources from the provided path and assembly.
        /// </summary>
        /// <param name="useResources">Indicates whether static resources should be served.</param>
        /// <param name="resourcePath">The root path of the resources to serve.</param>
        /// <param name="resourceAssembly">The assembly where the resources are embedded.</param>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetDefaultHttpHandler(
            bool useResources, 
            string resourcePath, 
            Assembly resourceAssembly)
        {
            App._httpHandler = new WebHostHttp11(useResources, resourcePath, resourceAssembly);
            App.SslServerAuthenticationOptions.ApplicationProtocols = [SslApplicationProtocol.Http11];
            return this;
        }

        /// <summary>
        /// Sets a default HTTP/1.1 handler without static resource support.
        /// </summary>
        /// <returns>The current <see cref="WebHostBuilder"/> instance.</returns>
        public WebHostBuilder SetDefaultHttpHandler()
        {
            App._httpHandler = new WebHostHttp11(false, null!, null!);
            App.SslServerAuthenticationOptions.ApplicationProtocols = [SslApplicationProtocol.Http11];
            return this;
        }
    }
}