using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Reflection;
using System.Text;
using WebHost;
using WebHost.Extensions;
using WebHost.Http11.Context;
using WebHost.Http11.Extensions;
using Xunit.Abstractions;

namespace Tests;

public class CreateWebHostServer(ITestOutputHelper testOutputHelper)
{
    //[Fact]
    public async Task CreateSimpleHttpApp()
    {
        var builder = CreateBuilder();
        await builder.Build().StartAsync();

        await Task.Delay(500);

        // Create an HttpClient instance
        using var client = new HttpClient();
        try
        {
            // Define the request URL
            const string url = "http://localhost:9001/route";

            // Send an HTTP GET request
            var response = await client.GetAsync(url);

            testOutputHelper.WriteLine($"Response: {response.StatusCode} {await response.Content.ReadAsStreamAsync()}");

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Read the response content as a string
            var responseBody = await response.Content.ReadAsStringAsync();

            // Output the response
            testOutputHelper.WriteLine($"Response from {url}:");
            testOutputHelper.WriteLine(responseBody);

            Assert.Equal("Hello from WebHost!", responseBody);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        catch (HttpRequestException e)
        {
            testOutputHelper.WriteLine($"Request error: {e.Message}");
        }
    }

    public WebHostApp.WebHostBuilder CreateBuilder()
    {
        var builder = WebHostApp.CreateBuilder();

        builder.App.HostBuilder
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Trace); // Set the minimum log level
            });

        builder
            .AddHandlers(Assembly.GetExecutingAssembly())
            .SetEndpoint("127.0.0.1", 9001)
            .MapGet("/route", scope => async context =>
            {
                const string content = "Hello from WebHost!";
                var response =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html\r\n" +
                    $"Content-Length: {Encoding.UTF8.GetByteCount(content)}\r\n" +
                    "Connection: keep-alive\r\n\r\n" +
                    content;

                await context.SendAsync(response);
            })
            .UseMiddleware(scope => async (context, next) =>
            {
                var logger = scope.GetRequiredService<ILogger<Program>>();

                logger.LogDebug("Executing..");

                // Wrap the endpoint in a try catch for global error handling
                //
                try
                {
                    await next(context);
                }
                // In case a ServiceException type was caught, the status code is known to be used on the http response
                //
                /*
                catch (ServiceException serviceEx)
                {
                    logger.LogError("ServiceException was caught and being handled:{Message}", serviceEx.Message);

                    var message = serviceEx.Message;

                    context.Response = new HttpResponseMessage((HttpStatusCode)serviceEx.StatusCode)
                    {
                        Content = new StringContent(message, Encoding.UTF8, "text/pain"),
                    };
                    context.Response.Headers.ConnectionClose = false;
                    context.Response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(message);

                    await context.SendAsync(await context.Response.ToBytes());
                }
                */
                // In case a regular exception is caught, assume the http response status code to be 500
                //
                catch (Exception ex)
                {
                    logger.LogError("Exception was caught and being handled:{Message}", ex.Message);

                    var message = ex.Message;

                    context.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent(message, Encoding.UTF8, "text/pain"),
                    };
                    context.Response.Headers.ConnectionClose = false;
                    context.Response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(message);

                    await context.SendAsync(await context.Response.ToBytes());
                }
            });

        return builder;
    }
}