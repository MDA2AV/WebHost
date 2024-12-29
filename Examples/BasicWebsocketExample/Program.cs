using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using System.Buffers;
using WebHost;
using WebHost.Enums;
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
            .MapGet("/websocket", scope => async (context) =>
            {
                var arrayPool = ArrayPool<byte>.Shared;
                var buffer = arrayPool.Rent(10000000);

                while (true)
                {
                    var receivedData = await context.WsReadAsync(buffer);

                    if (receivedData.Item2 == WsFrameType.Close)
                        break;

                    if (receivedData.Item1.IsEmpty)
                        break;

                    await context.WsSendAsync(receivedData.Item1, 0x01);
                }

                arrayPool.Return(buffer);
            });

        await builder.Build().StartAsync();

        Console.WriteLine("[Running, press ENTER to finish.]");
        Console.ReadLine();
    }
}
