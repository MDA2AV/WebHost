using Microsoft.Extensions.DependencyInjection;

namespace WebHost;

/// <summary>
/// Represents the context for a client connection, encapsulating the connection details, request, response, and dependency resolution.
/// </summary>
public interface IContext
{
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
    /// Gets or sets the <see cref="CancellationToken"/> for the current context.
    /// </summary>
    /// <remarks>
    /// - Used to monitor for cancellation requests, allowing graceful termination of operations.
    /// - Passed to all asynchronous operations initiated within the context.
    /// </remarks>
    CancellationToken CancellationToken { get; set; }
}