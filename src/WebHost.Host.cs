using Microsoft.Extensions.Hosting;

namespace WebHost;

public sealed partial class WebHostApp
{
    /// <summary>
    /// Gets or sets the <see cref="IHostBuilder"/> used to configure and build the application host.
    /// </summary>
    /// <remarks>
    /// - The <see cref="HostBuilder"/> is responsible for configuring services, middleware, and other host settings.
    /// - This property allows for customization of the hosting environment before the application is built and started.
    /// </remarks>
    public IHostBuilder HostBuilder { get; set; }

    /// <summary>
    /// Gets or sets the built <see cref="IHost"/> instance that represents the running application host.
    /// </summary>
    /// <remarks>
    /// - The <see cref="InternalHost"/> is the result of building the <see cref="HostBuilder"/>.
    /// - This property provides access to the application's runtime services and infrastructure.
    /// - Initialized to <c>null!</c> and must be set during the application's build process.
    /// </remarks>
    public IHost InternalHost { get; set; } = null!;

    // Property to hold the encoded routes, initialized as an empty HashSet
    public Dictionary<string, HashSet<string>> EncodedRoutes { get; set; } = new()
    {
        { "GET", [] },
        { "POST", [] },
        { "PUT", [] },
        { "DELETE", [] },
        { "PATCH", [] },
        { "HEAD", [] },
        { "OPTIONS", [] },
    };

    /// <summary>
    /// Creates a new instance of the WebHostBuilder, which is used to configure and build a WebHostApp instance.
    /// </summary>
    /// <returns>A new <see cref="WebHostBuilder"/> instance for configuring the <see cref="WebHostApp"/>.</returns>
    public static WebHostBuilder CreateBuilder() => new WebHostBuilder();

    /// <summary>
    /// Starts the WebHostApp asynchronously by initializing and starting the underlying host.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. When complete, the <see cref="WebHostApp"/> instance is fully started.</returns>
    public async Task<WebHostApp> StartAsync()
    {
        await InternalHost.StartAsync();
        return this;
    }

    /// <summary>
    /// Runs the WebHostApp asynchronously, awaitable blocking operation.
    /// </summary>
    public async Task RunAsync()
    {
        await InternalHost.RunAsync();
    }

    /// <summary>
    /// Starts the WebHostApp synchronously by initializing and starting the underlying host.
    /// </summary>
    /// <returns>The current <see cref="WebHostApp"/> instance for method chaining or further interaction.</returns>
    public WebHostApp Start()
    {
        InternalHost.Start();
        return this;
    }

    /// <summary>
    /// Runs the WebHostApp synchronously, blocking operation.
    /// </summary>
    public void Run()
    {
        InternalHost.Run();
    }
}