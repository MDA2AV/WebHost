using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace WebHost.Models;

/// <summary>
/// Represents security configuration options for the application, including TLS and certificate settings.
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether TLS (Transport Layer Security) is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable TLS; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool TlsEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the server's X.509 certificate for establishing secure connections.
    /// </summary>
    /// <remarks>
    /// - This certificate is used by the server to authenticate itself during the TLS handshake.
    /// - If <see cref="TlsEnabled"/> is <c>true</c>, this property must be set to a valid certificate.
    /// </remarks>
    public X509Certificate2? ServerCertificate { get; set; }

    /// <summary>
    /// Gets or sets the CA (Certificate Authority) certificate used to validate client certificates.
    /// </summary>
    /// <remarks>
    /// - This property is relevant when client certificate validation is required.
    /// - The CA certificate is used as part of the trust chain to validate client certificates.
    /// </remarks>
    public X509Certificate2? CaCertificate { get; set; }

    /// <summary>
    /// Gets or sets the callback method for validating client certificates during a secure connection.
    /// </summary>
    /// <value>
    /// A delegate of type <see cref="RemoteCertificateValidationCallback"/> that performs custom client certificate validation.
    /// </value>
    /// <remarks>
    /// - This callback is invoked during the TLS handshake to validate client certificates.
    /// - Must be set to a valid implementation if client certificate validation is required.
    /// </remarks>
    public RemoteCertificateValidationCallback ClientCertificateValidation { get; set; } = null!;
}