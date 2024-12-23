# WebHost

WebHost is an ultra-lightweight web server framework for .NET, designed to handle HTTP, WebSocket, and secure TLS/mTLS communication. It provides a modular and extensible architecture, integrating seamlessly with .NET's `IHost` for dependency injection and middleware configuration.

## Features

- **Flexible Hosting**: Supports both TLS and non-TLS connections.
- **WebSocket Support**: Implements WebSocket communication in compliance with RFC 6455.
- **Middleware Pipeline**: Fully configurable request handling pipeline.
- **Route Mapping**: Dynamically registers route handlers with attributes or fluent API.
- **Extensible Architecture**: Integrates with .NET's dependency injection for custom services and middleware.
- **Lightweight Design**: Minimal overhead with high-performance socket-based networking.

## Getting Started

### Prerequisites

- .NET SDK (version 7.0 or later recommended)

### Installation

Clone the repository and navigate to the project directory:

```bash
git clone <repository-url>
cd WebHost
```

### Usage

#### Create and Configure a WebHost

```csharp
var host = WebHostApp.CreateBuilder()
    .SetEndpoint("127.0.0.1", 8080)
    .UseTls(options =>
    {
        options.ServerCertificate = LoadCertificate();
    })
    .Map("/hello", sp => async context =>
    {
        await context.Respond("Hello, World!");
    })
    .Build();

await host.StartAsync();

static X509Certificate2 LoadCertificate() {
    // Load your TLS certificate here
}
```

#### Middleware Example

```csharp
builder.UseMiddleware(sp => async (context, next) =>
{
    Console.WriteLine("Request received");
    await next(context);
    Console.WriteLine("Response sent");
});
```

### WebSocket Example

```csharp
builder.Map("/ws", sp => async context =>
{
    var (length, message) = await context.WsReadAsync(new Memory<byte>(new byte[1024]));
    Console.WriteLine($"Received: {message}");

    await context.WsSendAsync($"Echo: {message}");
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

