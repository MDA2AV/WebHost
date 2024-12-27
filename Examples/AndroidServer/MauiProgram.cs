using System.Reflection;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebHost;
using WebHost.Extensions;

namespace AndroidServer
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            var whbuilder = WebHostApp.CreateBuilder();

            whbuilder.App.HostBuilder
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Trace); // Set the minimum log level
                });

            whbuilder
                .AddHandlers(Assembly.GetExecutingAssembly())
                .SetEndpoint("127.0.0.1", 9001)

                // Very basic example manually creating the http response and flushing it
                //
                .Map("/route", scope => async context =>
                {
                    const string content = "Hello from WebHost!";
                    var response =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/html\r\n" +
                        $"Content-Length: {Encoding.UTF8.GetByteCount(content)}\r\n" +
                        "Connection: keep-alive\r\n\r\n" +
                        content;

                    await context.SendAsync(response);
                });

            whbuilder.Build().Start();

            return builder.Build();
        }
    }
}
