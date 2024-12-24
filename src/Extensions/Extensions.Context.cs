using WebHost.Exceptions;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace WebHost.Extensions;

/// <summary>
/// Provides extension methods for handling responses and sending data within the application context.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Sends a response to the client using a specified <see cref="IResponseBuilder"/>.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current client connection.</param>
    /// <param name="responseBuilder">The <see cref="IResponseBuilder"/> used to build and handle the response.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullServiceException">
    /// Thrown if <paramref name="responseBuilder"/> is <c>null</c>.
    /// </exception>
    public static async Task Respond(this IContext context, IResponseBuilder responseBuilder, CancellationToken cancellationToken = default)
    {
        if (responseBuilder == null)
        {
            throw new ArgumentNullServiceException(nameof(responseBuilder));
        }

        await responseBuilder.HandleAsync(context, cancellationToken);
    }

    /// <summary>
    /// Sends a string response to the client.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current client connection.</param>
    /// <param name="response">The response as a string.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task SendAsync(this IContext context, string response, CancellationToken cancellationToken = default)
    {
        var responseBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(response));

        await SendAsync(context, responseBytes, cancellationToken);
    }

    /// <summary>
    /// Sends a binary response to the client using <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current client connection.</param>
    /// <param name="responseBytes">The response as a binary payload.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ServiceUnavailableServiceException">
    /// Thrown if neither <see cref="IContext.SslStream"/> nor <see cref="IContext.Socket"/> is available.
    /// </exception>
    public static async Task SendAsync(this IContext context, ReadOnlyMemory<byte> responseBytes, CancellationToken cancellationToken = default)
    {
        if (context.SslStream is not null)
        {
            await context.SslStream!.WriteAsync(responseBytes, cancellationToken);
            await context.SslStream.FlushAsync(cancellationToken);
            return;
        }

        if (context.Socket is null)
        {
            throw new ServiceUnavailableServiceException("[56235]Socket not found.");
        }

        await context.Socket!.SendAsync(responseBytes, cancellationToken);
    }

    /// <summary>
    /// Reads data from the context's underlying connection into the specified memory buffer.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current connection.</param>
    /// <param name="buffer">The <see cref="Memory{T}"/> buffer to store the incoming data.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to signal operation cancellation.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation, containing the number of bytes read.
    /// </returns>
    /// <exception cref="ServiceUnavailableServiceException">
    /// Thrown if neither <see cref="SslStream"/> nor <see cref="Socket"/> is available for reading.
    /// </exception>
    public static async Task<int> ReadAsync(this IContext context, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (context.SslStream is not null)
        {
            return await context.SslStream.ReadAsync(buffer, cancellationToken);
        }

        if (context.Socket is not null)
        {
            return await context.Socket.ReceiveAsync(buffer, cancellationToken);
        }

        throw new ServiceUnavailableServiceException("[56235]Socket not found.");
    }

    /// <summary>
    /// Reads a WebSocket message from the context's connection and decodes it as a string.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current connection.</param>
    /// <param name="buffer">The <see cref="Memory{T}"/> buffer to store the incoming data.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to signal operation cancellation.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation, containing the decoded WebSocket message.
    /// </returns>
    /// <exception cref="ConnectionClosedServiceException">
    /// Thrown if the connection is closed (no bytes are received).
    /// </exception>
    public static async Task<(int, string)> WsReadAsync(this IContext context, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var receivedBytes = await context.ReadAsync(buffer, cancellationToken);

        if (receivedBytes == 0)
        {
            return (receivedBytes, string.Empty);
        }

        return (receivedBytes, context.DecodeMessage(buffer, receivedBytes));
    }

    /// <summary>
    /// Sends a WebSocket message as a UTF-8 encoded string to the context's connection.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current connection.</param>
    /// <param name="payload">The message to send as a string.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to signal operation cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task WsSendAsync(this IContext context, string payload, CancellationToken cancellationToken = default)
    {
        // Send the response using the context
        await context.WsSendAsync(Encoding.UTF8.GetBytes(payload).AsMemory(), cancellationToken);
    }

    /// <summary>
    /// Sends a WebSocket message with the specified payload to the context's connection.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current connection.</param>
    /// <param name="payload">The payload to send as <see cref="ReadOnlyMemory{T}"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to signal operation cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task WsSendAsync(this IContext context, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        // Send the response using the context
        await context.SendAsync(BuildWsFrame(payload), cancellationToken);
    }

    /// <summary>
    /// Decodes a WebSocket message from the specified memory buffer.
    /// </summary>
    /// <param name="context">The <see cref="IContext"/> representing the current connection.</param>
    /// <param name="buffer">The <see cref="Memory{T}"/> containing the WebSocket frame to decode.</param>
    /// <param name="length">The number of bytes in the buffer to decode.</param>
    /// <returns>The decoded message as a string.</returns>
    public static string DecodeMessage(this IContext context, Memory<byte> buffer, int length)
    {
        var span = buffer.Span; // Access the Span<T> for working with the memory

        var payloadStart = 2;
        var payloadLength = span[1] & 0x7F;

        payloadStart = payloadLength switch
        {
            126 => 4,
            127 => 10,
            _ => payloadStart
        };

        var payload = span.Slice(payloadStart, payloadLength); // Slice the payload directly from the span

        return Encoding.UTF8.GetString(payload);
    }

    /// <summary>
    /// Builds a WebSocket frame from the specified payload.
    /// </summary>
    /// <param name="payload">The payload to include in the frame as <see cref="ReadOnlyMemory{T}"/>.</param>
    /// <returns>The WebSocket frame as <see cref="ReadOnlyMemory{T}"/>.</returns>
    private static ReadOnlyMemory<byte> BuildWsFrame(ReadOnlyMemory<byte> payload)
    {
        // Allocate the response memory dynamically
        var responseLength = 2 + payload.Length;
        var response = new Memory<byte>(new byte[responseLength]); // Memory-backed allocation

        // Construct the WebSocket frame
        var span = response.Span;
        span[0] = 0x81; // Final frame, text data
        span[1] = (byte)payload.Length; // Payload length

        // Copy the payload into the response memory
        payload.CopyTo(response[2..]);

        return response;
    }
}