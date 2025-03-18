using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using WebHost.Models;

namespace WebHost;

/// <summary>
/// Represents the context for a client connection, encapsulating the connection details, request, response, and dependency resolution.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Gets or sets the <see cref="PipeReader"/> for reading incoming data from the client connection.
    /// </summary>
    /// <remarks>
    /// - The <see cref="PipeReader"/> provides an efficient, high-performance way to process incoming data using pipelines.
    /// - It enables asynchronous, buffered reading without requiring manual stream handling.
    /// - This can improve performance by reducing memory allocations and avoiding unnecessary copying of data.
    /// - Typically used for processing HTTP request bodies, WebSocket frames, or custom protocol parsing.
    /// - If the connection does not use a pipeline-based reading mechanism, this property may not be utilized.
    /// </remarks>
    PipeReader PipeReader { get; set; }

    /// <summary>
    /// Gets or sets the stream associated with the client connection.
    /// This stream is used for reading incoming data and writing outgoing data,
    /// serving as the underlying communication channel for the connection.
    /// </summary>
    Stream Stream { get; set; }

    /// <summary>
    /// Gets or sets the HTTP request for the current connection.
    /// This property contains all the details of the incoming HTTP request,
    /// such as the request method, headers, URI, and body.
    /// </summary>
    IHttpRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the HTTP response message for the current connection.
    /// This property is used to construct and send the HTTP response back to the client,
    /// including the status code, headers, and any response content.
    /// </summary>
    HttpResponseMessage Response { get; set; }

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