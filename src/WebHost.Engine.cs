using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebHost;

public sealed partial class WebHostApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebHostApp"/> class with a default host builder configuration.
    /// </summary>
    /// <remarks>
    /// - Configures a default host builder using <see cref="Host.CreateDefaultBuilder"/>.
    /// - Registers a hosted service based on the current TLS settings in <see cref="SecurityOptions"/>.
    /// - If TLS is enabled, a TLS-enabled engine is created; otherwise, a plain engine is used.
    /// </remarks>
    private WebHostApp()
    {
        HostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService(sp => SecurityOptions.TlsEnabled
                    ? CreateTlsEnabledEngine()
                    : CreatePlainEngine());
            });
    }

    /// <summary>
    /// Creates a plain (non-TLS) engine to handle client connections.
    /// </summary>
    /// <returns>A new instance of the <see cref="Engine"/> class configured for plain connections.</returns>
    /// <remarks>
    /// The engine starts an asynchronous loop to accept and handle plain client connections using a socket.
    /// </remarks>
    private Engine CreatePlainEngine() =>
        new Engine(async stoppingToken =>
        {
            var socket = CreateListeningSocket();
            await RunAcceptLoopAsync(socket, HandlePlainClientAsync, stoppingToken);
        });

    /// <summary>
    /// Creates a TLS-enabled engine to handle secure client connections.
    /// </summary>
    /// <returns>A new instance of the <see cref="Engine"/> class configured for TLS connections.</returns>
    /// <remarks>
    /// The engine starts an asynchronous loop to accept and handle TLS client connections using a socket.
    /// </remarks>
    private Engine CreateTlsEnabledEngine() =>
        new Engine(async stoppingToken =>
        {
            var socket = CreateListeningSocket();
            await RunAcceptLoopAsync(socket, HandleTlsClientAsync, stoppingToken);
        });

    /// <summary>
    /// Creates and configures a listening socket for client connections.
    /// </summary>
    /// <returns>A configured <see cref="Socket"/> instance ready to accept connections.</returns>
    /// <remarks>
    /// - The socket uses the TCP protocol.
    /// - TCP Keep-Alive is enabled to maintain idle connections.
    /// - The socket is bound to the configured IP address and port, and starts listening with the specified backlog.
    /// </remarks>
    private Socket CreateListeningSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); // Enable TCP Keep-Alive

        socket.Bind(new IPEndPoint(_ipAddress, _port));
        socket.Listen(_backlog);

        _logger?.LogTrace("Created listening socket {SocketHash}", socket.GetHashCode());
        return socket;
    }

    /// <summary>
    /// Runs an asynchronous loop to accept and handle client connections.
    /// </summary>
    /// <param name="socket">The listening <see cref="Socket"/> instance.</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> to signal when the loop should stop.</param>
    /// <param name="clientHandler">
    /// A function to handle individual client connections, taking a <see cref="Socket"/> and a <see cref="CancellationToken"/> as parameters.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Continuously accepts incoming client connections while the application is running.
    /// - Spawns a new task to handle each client connection using the specified handler.
    /// </remarks>
    private async Task RunAcceptLoopAsync(Socket socket, Func<Socket, CancellationToken, Task> clientHandler, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await socket.AcceptAsync(stoppingToken);

                // Fire and forget
                //
                _ = Task.Run(async () =>
                {
                    _logger?.LogTrace("Handling client socket {ClientHash}", client.GetHashCode());
                    try
                    {
                        await clientHandler(client, stoppingToken);
                    }
                    catch(Exception ex)
                    {
                        _logger?.LogTrace("Client could not be handled: {Exception}", ex);
                    }
                    finally
                    {
                        _logger?.LogTrace("Disposing client socket {ClientHash}", client.GetHashCode());
                        client.Dispose(); // Ensure the client socket is disposed after use
                    }
                }, stoppingToken);
            }
        }
        finally
        {
            _logger?.LogTrace("Disposing listening socket {SocketHash}", socket.GetHashCode());
            socket.Dispose(); // Dispose of the listening socket when the loop exits
        }
    }

    /// <summary>
    /// Represents a lightweight background service that executes a specified asynchronous action.
    /// </summary>
    /// <param name="action">
    /// A function that takes a <see cref="CancellationToken"/> and returns a <see cref="Task"/>, representing the action to execute.
    /// </param>
    /// <remarks>
    /// This class inherits from <see cref="BackgroundService"/> and overrides <see cref="ExecuteAsync"/> to invoke the provided action.
    /// It simplifies running long-lived asynchronous tasks, such as server loops, within a hosted service.
    /// </remarks>
    private sealed class Engine(Func<CancellationToken, Task> action) : BackgroundService
    {
        /// <summary>
        /// Executes the provided asynchronous action when the service starts.
        /// </summary>
        /// <param name="stoppingToken">
        /// A <see cref="CancellationToken"/> that is triggered when the service is stopping.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => action(stoppingToken);
    }
}