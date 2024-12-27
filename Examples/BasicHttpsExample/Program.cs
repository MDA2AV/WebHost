using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebHost;
using WebHost.Extensions;
using System.Text.Json;
using WebHost.Exceptions;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Shared;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ILogger validationLogger = default;

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
            // Custom mutual TLS handling (also validating client certificate)
            //
            .UseTls(securityOptions =>
            {
                // Load certificate authority
                // This isn't required for standard TLS (not mutual TLS)
                //
                securityOptions.CaCertificate = CertificateLoader
                    .LoadCertificateFromResource(Assembly.GetExecutingAssembly(), "certAuth.crt");
                // Load server certificate
                //
                securityOptions.ServerCertificate = CertificateLoader
                    .LoadCertificateFromResource(Assembly.GetExecutingAssembly(),"server.pfx", "webhost");

                // Custom Client certification callback to validate client's certificate
                // This isn't required for standard TLS (not mutual TLS)
                //
                securityOptions.ClientCertificateValidation = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (validationLogger is null) return false;
                    if (certificate is null) return false;

                    validationLogger.LogCritical("Validating client certificate...");

                    chain = new X509Chain();
                    var chainPolicy = new X509ChainPolicy
                    {
                        VerificationFlags = X509VerificationFlags.NoFlag,
                        RevocationMode = X509RevocationMode.NoCheck,
                        TrustMode = X509ChainTrustMode.CustomRootTrust
                    };

                    chainPolicy.CustomTrustStore.Add(securityOptions.CaCertificate);
                    chain.ChainPolicy = chainPolicy;

                    var isChainValid = chain.Build(new X509Certificate2(certificate));
                    if (isChainValid)
                    {
                        validationLogger.LogCritical("Client certificate is valid and issued by trusted CA.");
                        return true;
                    }

                    validationLogger.LogCritical("Client certificate validation failed:");
                    foreach (var status in chain.ChainStatus)
                    {
                        validationLogger.LogCritical(" - {Status}: {StatusInformation}", status.Status, status.StatusInformation);
                    }
                    return false;
                };
            })
            .AddHandlers(Assembly.GetExecutingAssembly())
            .AddHandlers(Assembly.GetAssembly(typeof(TestHandler))!) // Passing the request handler type since it is on a different assembly
            .SetEndpoint("127.0.0.1", 9001)

            // Very basic example manually creating the http response and flushing it
            //
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

            // Basic example using a custom request handler to handle the endpoint and a custom response builder to send chunked data
            //
            .MapGet("/route2", scope => async context =>
            {
                // Printing to console received request data
                //
                Console.WriteLine($"Received HttpMethod: {context.Request.HttpMethod}");
                Console.WriteLine($"Received query params: {context.Request.QueryParameters}");
                foreach (var header in context.Request.Headers)
                {
                    Console.WriteLine($"Received header: {header}");

                }
                Console.WriteLine($"Received body: {context.Request.Body}");

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

            // Basic example using a custom request handler to handle the endpoint and System.Net.Http.HttpResponseMessage 
            // to build the http response
            //
            .MapGet("/route3", scope => async context =>
            {
                // Resolve the request handler
                //
                var handler = scope.GetRequiredKeyedService<IRequestHandler<ExampleQuery, bool>>("ExampleKey");

                // Handle
                //
                await handler.Handle(new ExampleQuery(), context.CancellationToken);

                // Respond
                //
                var exampleClass = new
                {
                    Name = "John",
                    Address = "World"
                };

                var jsonString = JsonSerializer.Serialize(exampleClass);

                context.Response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonString, Encoding.UTF8, "application/json"),
                };
                context.Response.Headers.ConnectionClose = false;
                context.Response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(jsonString);

                await context.SendAsync(await context.Response.ToBytes());
            })

            // Basic global error handling middleware example
            //
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

        var app = await builder.Build().StartAsync();

        var loggerFactory = app.InternalHost.Services.GetRequiredService<ILoggerFactory>();
        validationLogger = loggerFactory.CreateLogger("Validation");

        Console.WriteLine("[Running, press ENTER to finish.]");
        Console.ReadLine();
    }
}
