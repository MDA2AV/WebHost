using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using WebHost;
using WebHost.Extensions;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ILogger? validationLogger = null;

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
                    .LoadCertificateFromResource(Assembly.GetExecutingAssembly(), "server.pfx", "webhost");

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
            .SetEndpoint("127.0.0.1", 9001)
            .MapGet("/websocket", scope => async (context) =>
            {
                var buffer = new Memory<byte>(new byte[1024]);

                while (true)
                {
                    var receivedData = await context.WsReadAsync(buffer);
                    if (receivedData.Item1 == 0)
                    {
                        break;
                    }

                    Console.WriteLine("WebSocket Message: " + receivedData.Item2);

                    if (receivedData.Item2.Equals("quit"))
                    {
                        break;
                    }

                    await context.WsSendAsync(receivedData.Item2);
                }
            });

        var app = await builder.Build().StartAsync();

        var loggerFactory = app.InternalHost.Services.GetRequiredService<ILoggerFactory>();
        validationLogger = loggerFactory.CreateLogger("Validation");

        Console.WriteLine("[Running, press ENTER to finish.]");
        Console.ReadLine();
    }
}