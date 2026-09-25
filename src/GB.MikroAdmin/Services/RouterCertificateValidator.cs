using System.Net.Security;
using System.Net.Sockets;
namespace GB.MikroAdmin.Services;
/// <summary>Falla cerrado: no admite certificados autofirmados, expirados ni cuyo SAN no coincida con el host.</summary>
public static class RouterCertificateValidator
{
    public static async Task ValidateAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, cancellationToken);
        await using var tls = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
         (_, _, _, errors) => errors == SslPolicyErrors.None);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        }, cancellationToken);
    }
}
