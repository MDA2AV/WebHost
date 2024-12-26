using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using WebHost;
using WebHost.Extensions;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebHostApp.CreateBuilder();

        builder.App.HostBuilder
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Trace); // Set the minimum log level
            })
            // Register scoped service
            //
            .ConfigureServices((_, services) =>
            {
                services.AddScoped<ExampleService>();
            });

        builder
            .SetEndpoint("127.0.0.1", 9001)
            .Map("/websocket", scope => async (context) =>
            {
                var logger = scope.GetRequiredService<ILogger<Program>>();

                var buffer = new Memory<byte>(new byte[1024]);

                while (true)
                {
                    var receivedData = await context.WsReadAsync(buffer);
                    if (receivedData.Item1 == 0)
                    {
                        break;
                    }

                    logger.LogInformation("WebSocket Message: {Message}", receivedData.Item2);

                    if (receivedData.Item2.Equals("quit"))
                    {
                        break;
                    }

                    await context.WsSendAsync(receivedData.Item2);
                }
            });

        await builder.Build().StartAsync();

        Console.WriteLine("[Running, press ENTER to finish.]");
        Console.ReadLine();
    }
}
