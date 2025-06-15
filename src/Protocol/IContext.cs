using Microsoft.Extensions.DependencyInjection;
using WebHost.Protocol.Response;

namespace WebHost.Protocol;

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

    /// <summary>
    /// Gets or sets the HTTP response to be sent back to the client.
    /// This property holds the response data, including status code, headers, and content.
    /// It is constructed and written to the stream after the request has been processed.
    /// </summary>
    IResponse Response { get; set; }
}