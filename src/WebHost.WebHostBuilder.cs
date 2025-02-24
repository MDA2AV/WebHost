using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebHost.Models;
using System.Net;
using System.Reflection;

namespace WebHost;

public sealed partial class WebHostApp
{
    // Configuration fields
    //
    private IPAddress _ipAddress = IPAddress.Parse("127.0.0.1");
    private int _port = 9001;
    private int _backlog = 10;
    private bool _useStandardHttp11Version = true;

    private bool _useStaticFiles;
    private string _resourcesPath = string.Empty;
    private Assembly _resourcesAssembly = null!;

    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;

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
        /// Builds the WebHostApp instance with the current configuration.
        /// </summary>
        public WebHostApp Build()
        {
            App.InternalHost = App.HostBuilder.Build();

            App._loggerFactory = App.InternalHost.Services.GetRequiredService<ILoggerFactory>();
            App._logger = App._loggerFactory.CreateLogger<WebHostApp>();

            return App;
        }

        /// <summary>
        /// Configures TLS settings for the WebHostApp.
        /// </summary>
        public WebHostBuilder UseTls(Action<SecurityOptions> securityOptions)
        {
            App.SecurityOptions.TlsEnabled = true;

            securityOptions(App.SecurityOptions);

            return this;
        }

        /// <summary>
        /// Sets the IP address and port for the WebHostApp endpoint.
        /// </summary>
        public WebHostBuilder SetEndpoint(IPAddress ipAddress, int port)
        {
            App._ipAddress = ipAddress;
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the IP address (string) and port for the WebHostApp endpoint.
        /// </summary>
        public WebHostBuilder SetEndpoint(string ipAddress, int port)
        {
            App._ipAddress = IPAddress.Parse(ipAddress);
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the port for the WebHostApp.
        /// </summary>
        public WebHostBuilder SetPort(int port)
        {
            App._port = port;
            return this;
        }

        /// <summary>
        /// Sets the backlog size for the WebHostApp.
        /// </summary>
        public WebHostBuilder SetBacklog(int backlog)
        {
            App._backlog = backlog;
            return this;
        }

        /// <summary>
        /// Set webserver to use HTTP/0 custom version.
        /// </summary>
        public WebHostBuilder UseHttp11Multiplexed()
        {
            App._useStandardHttp11Version = false;
            return this;
        }

        /// <summary>
        /// Webserver will return files at a given resource path in the caller project.
        /// </summary>
        public WebHostBuilder AddStaticFiles(Assembly resourcesAssembly, string resourcesPath)
        {
            App._useStaticFiles = true;
            App._resourcesPath = resourcesPath;
            App._resourcesAssembly = resourcesAssembly;
            return this;
        }
    }
}