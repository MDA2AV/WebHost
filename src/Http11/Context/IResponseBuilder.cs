namespace WebHost.Http11.Context;

/// <summary>
/// Defines a contract for building and handling responses in the application.
/// </summary>
public interface IResponseBuilder2
{
    /// <summary>
    /// Handles the response logic asynchronously based on the provided context.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current client connection and request details.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>
    /// - Implementations of this method should generate and send the response to the client using the context.
    /// - The method should handle any necessary response preparation, such as headers, body, and status codes.
    /// - Designed to be invoked as part of the request-response pipeline.
    /// </remarks>
    Task HandleAsync(IContext context, CancellationToken cancellationToken = default);
}