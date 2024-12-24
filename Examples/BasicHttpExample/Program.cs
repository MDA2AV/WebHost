using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;
using BasicHttpExample;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebHost;
using WebHost.Extensions;
using WebHost.Utils;

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
            .ConfigureServices((context, services) =>
            {
                services.AddScoped<ExampleService>();
            });

        builder.AddHandlers(Assembly.GetExecutingAssembly())
            .SetEndpoint("127.0.0.1", 9001)
            .Map("/route", scope => async context =>
            {
                // Resolve the request handler
                //
                var handler = scope.GetRequiredKeyedService<IRequestHandler<ExampleQuery, bool>>("ExampleKey");

                // Handle
                //
                await handler.Handle(new ExampleQuery(), context.CancellationToken);

                // Respond with a custom implementation for IResponseBuilder
                //
                await context.Respond(new ChunkedResponseBuilder());
            })
            .UseMiddleware(scope => async (context, next) =>
            {
                var logger = scope.GetRequiredService<ILogger<bool>>();

                logger.LogDebug("Executing..");

                // Resolve service and execute
                //
                await context.Resolve<ExampleService>().ExecuteAsync();

                // Wrap the endpoint in a try catch for global error handling
                //
                try
                {
                    await next(context);
                }
                catch (Exception ex)
                {
                    logger.LogError("Exception was caught and being handled:{Message}", ex.Message);
                }
            });

        await builder.Build().StartAsync();

        Console.WriteLine("[Running, press ENTER to finish.]");
        Console.ReadLine();
    }
}
