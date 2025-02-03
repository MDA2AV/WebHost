using Microsoft.Extensions.DependencyInjection;
using WebHost.Models;

namespace WebHost;

/// <summary>
/// Represents the context for a client connection, encapsulating the connection details, request, response, and dependency resolution.
/// </summary>
public interface IContext
{
    Stream Stream { get; set; }
    Http11 Http11 { get; set; }
    IHttpRequest Request { get; set; }

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
    /// Gets or sets the <see cref="CancellationToken"/> for the current context.
    /// </summary>
    /// <remarks>
    /// - Used to monitor for cancellation requests, allowing graceful termination of operations.
    /// - Passed to all asynchronous operations initiated within the context.
    /// </remarks>
    CancellationToken CancellationToken { get; set; }
}