using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Shared;

public static class CertificateLoader
{
    /// <summary>
    /// Load a certificate (.pfx or .crt) from embedded resources.
    /// </summary>
    public static X509Certificate2 LoadCertificateFromResource(Assembly assembly, string resourceName, string? password = null)
    {
        // Find the embedded resource
        var fullResourceName = $"{assembly.GetName().Name}.{resourceName}";
        using var resourceStream = assembly.GetManifestResourceStream(fullResourceName);

        if (resourceStream == null)
            throw new ArgumentNullException($"Resource '{fullResourceName}' not found.");

        // Read the resource into a byte array
        using var memoryStream = new MemoryStream();
        resourceStream.CopyTo(memoryStream);
        var certificateBytes = memoryStream.ToArray();

        // Load the certificate (supporting both PFX and CRT formats)
        return string.IsNullOrEmpty(password)
            ? new X509Certificate2(certificateBytes)
            : new X509Certificate2(certificateBytes, password);
    }
}