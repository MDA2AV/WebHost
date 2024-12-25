using Microsoft.Extensions.DependencyInjection;
using WebHost.Models;
using System.Net.Security;
using System.Net.Sockets;

namespace WebHost;

/// <summary>
/// Represents the context for a client connection, encapsulating the connection details, request, response, and dependency resolution.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Gets or sets the raw <see cref="Socket"/> associated with the client connection.
    /// </summary>
    /// <remarks>
    /// - Used for plain (non-TLS) communication.
    /// - May be <c>null</c> if the connection is using TLS.
    /// </remarks>
    Socket? Socket { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="SslStream"/> for secure (TLS) communication with the client.
    /// </summary>
    /// <remarks>
    /// - Used for encrypted communication.
    /// - May be <c>null</c> if the connection is not using TLS.
    /// </remarks>
    SslStream? SslStream { get; set; }

    /// <summary>
    /// Gets or sets the service scope for resolving scoped services during the lifecycle of the request.
    /// </summary>
    /// <value>
    /// An instance of <see cref="AsyncServiceScope"/> for managing scoped service lifetimes.
    /// </value>
    AsyncServiceScope Scope { get; set; }

    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> from the current service scope.
    /// </summary>
    /// <typeparam name="T">The type of the service to resolve.</typeparam>
    /// <returns>An instance of the resolved service.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service of type <typeparamref name="T"/> cannot be resolved.
    /// </exception>
    T Resolve<T>() where T : notnull;

    /// <summary>
    /// Gets or sets the HttpResponseMessage representing the HTTP response.
    /// </summary>
    /// <remarks>
    /// This property encapsulates the status code, headers, and content of an HTTP response.
    /// It can be used to configure or inspect the response in an HTTP client-server communication.
    /// </remarks>
    HttpResponseMessage Response { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Request"/> object representing the current client request.
    /// </summary>
    /// <remarks>
    /// - Contains details such as headers, body, route, and HTTP method.
    /// - Set during the request parsing phase and used throughout the request lifecycle.
    /// </remarks>
    Request Request { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="CancellationToken"/> for the current context.
    /// </summary>
    /// <remarks>
    /// - Used to monitor for cancellation requests, allowing graceful termination of operations.
    /// - Passed to all asynchronous operations initiated within the context.
    /// </remarks>
    CancellationToken CancellationToken { get; set; }
}