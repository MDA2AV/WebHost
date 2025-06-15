using Microsoft.Extensions.Logging;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security;

namespace WebHost;

public sealed partial class WebHostApp<TContext>
{
    /// <summary>
    /// Handles a client connection using plain (non-TLS) communication.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <param name="stoppingToken">
    /// A <see cref="CancellationToken"/> to signal when the operation should stop.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method is used for plain, unencrypted client communication.
    /// </remarks>
    private async Task HandlePlainClientAsync(Socket client, CancellationToken stoppingToken)
    {
        var stream = new NetworkStream(client);
        await HttpHandler.HandleClientAsync(stream, PipelineNoResponse, stoppingToken);
    }

    /// <summary>
    /// Handles a client connection using TLS (encrypted) communication.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <param name="stoppingToken">
    /// A <see cref="CancellationToken"/> to signal when the operation should stop.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Performs the TLS handshake to secure the connection.
    /// - Validates the server certificate and optionally validates the client certificate.
    /// - If the TLS handshake succeeds, the connection is processed/>.
    /// - If the TLS handshake fails, an error message is sent to the client before the connection is closed.
    /// </remarks>
    /// <exception cref="SecurityException">
    /// Thrown if the server certificate is null, as TLS cannot be established without it.
    /// </exception>
    private async Task HandleTlsClientAsync(Socket client, CancellationToken stoppingToken)
    {
        if (SslServerAuthenticationOptions.ServerCertificate is null)
        {
            throw new SecurityException("SecurityOptions.ServerCertificate is null");
        }

        // Create and configure the SSL stream for the client connection
        //
        // TODO: Investigate leaveInnerStreamOpen flag
        await using var sslStream = new SslStream(new NetworkStream(client),
                                                  false,
                                                  SslServerAuthenticationOptions.RemoteCertificateValidationCallback);

        try
        {
            // Perform the TLS handshake
            //
            await sslStream.AuthenticateAsServerAsync(SslServerAuthenticationOptions, stoppingToken);
        }
        catch (Exception ex) when (HandleTlsException(ex))
        {
            // Unified handling of TLS failure message.
            //
            await SendTlsFailureMessageAsync(client);
        }

        // Handle the client connection securely
        //
        await HttpHandler.HandleClientAsync(
            sslStream,
            PipelineNoResponse,
            stoppingToken);
    }

    /// <summary>
    /// Handles logging and classification of TLS handshake exceptions.
    /// </summary>
    /// <param name="ex">The exception to process.</param>
    /// <returns>Always returns <c>true</c> to continue exception filtering.</returns>
    private bool HandleTlsException(Exception ex)
    {
        switch (ex)
        {
            case AuthenticationException authEx:
                Logger?.LogTrace("TLS Handshake failed due to authentication error: {Message}", authEx.Message);
                break;
            case InvalidOperationException invalidOpEx:
                Logger?.LogTrace("TLS Handshake failed due to socket error: {Message}", invalidOpEx.Message);
                break;
            default:
                Logger?.LogTrace("Unexpected error during TLS Handshake: {Message}", ex.Message);
                break;
        }

        return true; // Ensure the exception is always caught
    }

    /// <summary>
    /// Sends a failure message to the client when the TLS handshake fails and closes the connection.
    /// </summary>
    /// <param name="client">The <see cref="Socket"/> representing the client connection.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// - Attempts to notify the client of the TLS handshake failure by sending a predefined message.
    /// - Any errors during the send operation are ignored to ensure the connection is closed gracefully.
    /// - The client connection is always closed, regardless of whether the send operation succeeds or fails.
    /// </remarks>
    private static async Task SendTlsFailureMessageAsync(Socket client)
    {
        var messageBytes = "TLS Handshake failed. Closing connection."u8.ToArray();

        try
        {
            await client.SendAsync(messageBytes, SocketFlags.None);
        }
        catch
        {
            // Ignore errors while sending failure response
        }
        finally
        {
            client.Close();
        }
    }
}
