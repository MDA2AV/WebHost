[![NuGet](https://img.shields.io/nuget/v/WebHost.svg)](https://www.nuget.org/packages/WebHost/)

# WebHost

WebHost is an ultra-lightweight web server framework for .NET, designed to handle HTTP, WebSocket, and secure TLS/mTLS communication. It provides a modular and extensible architecture, integrating seamlessly with .NET's `IHost` for dependency injection and middleware configuration.

# Purpose

Provide fully customizable and low level access to http request. This package was born from the need to run a .NET C# web server on any .NET supported platform, which is not possible using ASP.NET Core.

## Fully Native

No third party libraries used, the only dependencies are

Microsoft.Extensions.DependencyInjection.Abstractions (>= 9.0.0)

Microsoft.Extensions.Hosting (>= 9.0.0)

## Features

- **Flexible Hosting**: Supports both TLS and non-TLS connections.
- **WebSocket Support**: Implements WebSocket communication in compliance with RFC 6455.
- **Middleware Pipeline**: Fully configurable request handling pipeline.
- **Route Mapping**: Dynamically registers route handlers with attributes or fluent API.
- **Extensible Architecture**: Integrates with .NET's dependency injection for custom services and middleware.
- **Lightweight Design**: Minimal overhead with high-performance socket-based networking.

### HTTP Version support

- **HTTP/1.x**: Yes
- **HTTP/2.0**: In progress
- **HTTP/3.0**: Not planned yet

## Getting Started

### Prerequisites

- .NET SDK (version 8.0 or later recommended)

### Installation

Clone the repository and navigate to the project directory:

```bash
git clone <repository-url>
cd WebHost
```

### Examples

Refer to Examples folder for detailed usage examples!

### Usage

#### Create and Configure a WebHost

```csharp
var host = WebHostApp.CreateBuilder()
    .SetEndpoint("127.0.0.1", 9001)
    .UseTls(options =>
    {
        options.ServerCertificate = LoadCertificate();
    })
    .MapGet("/route", sp => async context =>
    {
        // Access http request params
        //
        Console.WriteLine($"Received HttpMethod: {context.Request.HttpMethod}");
        Console.WriteLine($"Received query params: {context.Request.QueryParameters}");
        foreach (var header in context.Request.Headers)
            Console.WriteLine($"Received header: {header}");
        Console.WriteLine($"Received body: {context.Request.Body}");

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
    .Build();

await host.RunAsync();

static X509Certificate2 LoadCertificate() {
    // Load your TLS certificate here
}
```

#### Middleware Example - Global Error Handling

```csharp
// Basic global error handling middleware example
//
builder.UseMiddleware(scope => async (context, next) =>
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
```

### WebSocket Example

```csharp
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
```

## Architecture

### Core Components

1. **`WebHostApp`**
   - The main entry point for configuring and starting the server.
   - Supports fluent API for configuration.

2. **`WebHostBuilder`**
   - Provides methods to configure endpoints, TLS settings, and middleware.

3. **Middleware Pipeline**
   - Processes requests through a dynamic pipeline of middleware components.

4. **WebSocket and TLS Support**
   - Handles WebSocket handshakes and messages.
   - Manages secure communication with TLS/mTLS.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests to improve the project.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.

## Acknowledgements

- Built with .NET and inspired by high-performance web hosting frameworks.

