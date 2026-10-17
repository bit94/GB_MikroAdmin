using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class RouterCertificatePolicyTests
{
    [Fact]
    public void Validate_rejects_a_missing_certificate()
    {
        Assert.False(RouterCertificatePolicy.Validate("router.local", null, SslPolicyErrors.None));
    }

    [Fact]
    public void Validate_rejects_a_name_mismatch_before_building_the_chain()
    {
        using var certificate = CreateCertificate("router.local", "router.local");

        Assert.False(RouterCertificatePolicy.Validate(
            "router.local", certificate, SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Validate_rejects_a_certificate_whose_chain_is_not_trusted()
    {
        // El SAN coincide, pero el certificado es autofirmado: la cadena debe rechazarlo.
        using var certificate = CreateCertificate("router.local", "router.local");

        Assert.False(RouterCertificatePolicy.Validate("router.local", certificate, SslPolicyErrors.None));
    }

    [Theory]
    [InlineData("router.local")]
    [InlineData("ROUTER.LOCAL")]
    [InlineData("192.168.88.1")]
    public void HostnameMatches_accepts_the_dns_and_ip_names_from_the_certificate(string host)
    {
        using var certificate = CreateCertificate("router.local", "router.local", "192.168.88.1");

        Assert.True(RouterCertificatePolicy.HostnameMatches(certificate, host));
    }

    [Theory]
    [InlineData("other.local")]
    [InlineData("192.168.88.2")]
    public void HostnameMatches_rejects_a_different_host(string host)
    {
        using var certificate = CreateCertificate("router.local", "router.local", "192.168.88.1");

        Assert.False(RouterCertificatePolicy.HostnameMatches(certificate, host));
    }

    [Fact]
    public void HostnameMatches_rejects_an_invalid_host()
    {
        using var certificate = CreateCertificate("router.local", "router.local");

        Assert.False(RouterCertificatePolicy.HostnameMatches(certificate, "no es un host"));
        Assert.False(RouterCertificatePolicy.HostnameMatches(certificate, string.Empty));
    }

    [Fact]
    public void HostnameMatches_accepts_a_single_wildcard_label()
    {
        using var certificate = CreateCertificate("example.local", "*.example.local");

        Assert.True(RouterCertificatePolicy.HostnameMatches(certificate, "wifi.example.local"));
        Assert.False(RouterCertificatePolicy.HostnameMatches(certificate, "a.b.example.local"));
    }

    private static X509Certificate2 CreateCertificate(string commonName, params string[] alternativeNames)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        var san = new SubjectAlternativeNameBuilder();
        foreach (var name in alternativeNames)
        {
            if (IPAddress.TryParse(name, out var address))
                san.AddIpAddress(address);
            else
                san.AddDnsName(name);
        }

        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    }
}
