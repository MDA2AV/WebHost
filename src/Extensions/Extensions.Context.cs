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

        return receivedBytes == 0 
            ? (receivedBytes, string.Empty) 
            : (receivedBytes, context.DecodeMessage(buffer, receivedBytes));
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
    /// Decodes a WebSocket frame received from a client by parsing the payload length,
    /// handling masking (if present), and extracting the decoded UTF-8 message content.
    /// </summary>
    /// <param name="context">
    /// The IContext instance, representing the current communication context.
    /// This parameter provides access to additional context-related utilities if needed.
    /// </param>
    /// <param name="buffer">
    /// A <see cref="Memory{byte}"/> object containing the raw WebSocket frame received from the client.
    /// The buffer holds the complete frame, including headers, masking key (if present), and payload data.
    /// </param>
    /// <param name="length">
    /// The number of bytes in the <paramref name="buffer"/> representing the received WebSocket frame.
    /// This value ensures that only the relevant portion of the buffer is processed.
    /// </param>
    /// <returns>
    /// A <see cref="string"/> containing the decoded UTF-8 message extracted from the WebSocket frame payload.
    /// If the frame is masked (as required for client-to-server frames), the payload is unmasked before decoding.
    /// </returns>
    /// <remarks>
    /// This method supports standard WebSocket frame structures, including:
    /// - Payload lengths up to 64 bits (with extended length fields for values >125).
    /// - Masked client frames, unmasking the payload using the XOR operation and the 4-byte masking key.
    /// 
    /// Limitations:
    /// - Assumes that the buffer contains a complete WebSocket frame.
    /// - Does not handle WebSocket control frames (e.g., Ping, Pong, or Close frames).
    /// 
    /// WebSocket frames consist of a header, an optional masking key (for client-to-server frames),
    /// and the payload. This method extracts and decodes the payload according to the WebSocket protocol.
    ///
    /// Reference: RFC 6455 (The WebSocket Protocol)
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the payload length exceeds the buffer's capacity, indicating an incomplete or invalid frame.
    /// </exception>
    public static string DecodeMessage(this IContext context, Memory<byte> buffer, int length)
    {
        var span = buffer.Span;

        // Validate minimum frame length
        //
        if (length < 2)
        {
            throw new ArgumentException("The frame is incomplete or invalid.", nameof(buffer));
        }

        // Check the MASK bit
        //
        var isMasked = (span[1] & 0x80) != 0;

        // Extract payload length
        //
        var payloadLength = span[1] & 0x7F;
        var payloadStart = 2;

        switch (payloadLength)
        {
            // Adjust for extended payload lengths
            //
            case 126:
                if (length < 4) throw new ArgumentException("The frame is incomplete or invalid.", nameof(buffer));
                payloadLength = (span[2] << 8) | span[3]; // 16-bit length
                payloadStart = 4;
                break;
            case 127:
                // 64-bit length, not common, typically used for very large payloads
                //
                if (length < 10) throw new ArgumentException("The frame is incomplete or invalid.", nameof(buffer));
                payloadLength = (int)(
                    ((ulong)span[2] << 56) |
                    ((ulong)span[3] << 48) |
                    ((ulong)span[4] << 40) |
                    ((ulong)span[5] << 32) |
                    ((ulong)span[6] << 24) |
                    ((ulong)span[7] << 16) |
                    ((ulong)span[8] << 8) |
                    span[9]);
                payloadStart = 10;
                break;
        }

        // Check if the total frame length is sufficient
        //
        if (length < payloadStart + payloadLength)
        {
            throw new ArgumentException("The frame is incomplete or invalid.", nameof(buffer));
        }

        // Extract the masking key if the MASK bit is set
        //
        var maskKey = Array.Empty<byte>();
        if (isMasked)
        {
            if (length < payloadStart + 4) throw new ArgumentException("The frame is incomplete or invalid.", nameof(buffer));
            maskKey = span.Slice(payloadStart, 4).ToArray();
            payloadStart += 4;
        }

        // Extract and decode the payload
        //
        var payload = span.Slice(payloadStart, payloadLength).ToArray();

        if (!isMasked)
        {
            return Encoding.UTF8.GetString(payload);
        }
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] ^= maskKey[i % 4]; // XOR with the masking key
        }

        // Return the decoded string
        //
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