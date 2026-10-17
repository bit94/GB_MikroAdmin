using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Política de confianza del certificado del router. Falla cerrado: exige nombre coincidente,
/// cadena de confianza y consulta de revocación en línea. Se aplica al socket TLS que autentica
/// las credenciales, no a un handshake aparte.
/// </summary>
public static class RouterCertificatePolicy
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// Valida el certificado que entrega <c>SslStream</c>. Los errores de nombre y ausencia de
    /// certificado se rechazan de inmediato; el resto de la cadena se reconstruye aquí porque el
    /// handshake de tik4net no comprueba revocación.
    /// </summary>
    public static bool Validate(string host, X509Certificate? certificate, SslPolicyErrors errors)
    {
        if (certificate is null || string.IsNullOrWhiteSpace(host))
            return false;

        if ((errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
            return false;

        if (certificate is not X509Certificate2 certificate2)
        {
            using var copy = new X509Certificate2(certificate);
            return ValidateCore(copy, host);
        }

        return ValidateCore(certificate2, host);
    }

    internal static bool HostnameMatches(X509Certificate2 certificate, string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        try
        {
            return certificate.MatchesHostname(host, allowWildcards: true, allowCommonName: true);
        }
        catch (ArgumentException)
        {
            // Un host con formato inválido nunca debe considerarse coincidente.
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool ValidateCore(X509Certificate2 certificate, string host)
    {
        if (!HostnameMatches(certificate, host))
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthenticationOid));

        try
        {
            return chain.Build(certificate);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
