using FluentAssertions;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests;

namespace Servercyde.Monitoring.Core.Tests.Infrastructure;
public class CertificateLoaderTests : TestFixture
{
    private const string CertificateThumbprint = "883D8A563352A56B56ABF340C2926B6D7217605D";

    [Fact]
    public void Load_EmptyThumbprint_ThrowsArgumentNullException()
    {
        Action act = () => CertificateLoader.Load(string.Empty);
        act.Should().Throw<ArgumentNullException>().WithParameterName("thumbprint");
    }

    [Fact]
    public void Load_CertificateNotFound_ThrowsInvalidOperationException()
    {        
        var thumbprint = "nonexistentthumbprint";
        Action act = () => CertificateLoader.Load(thumbprint);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Certificate with thumbprint {thumbprint} was not found");
    }

    [Fact]
    public void Load_ValidThumbprint_ReturnsCertificate()
    {
        CertificateLoader.SetCertificateDirectoryPath(CertificateDirectoryPath);
        var certificate = CertificateLoader.Load(CertificateThumbprint);
        certificate.Thumbprint.Should().BeEquivalentTo(CertificateThumbprint);
    }

    [Fact]
    public void LoadFromBase64_ValidCertificate_ReturnsCertificate()
    {
        var certificatePath = Path.Combine(CertificateDirectoryPath, $"{CertificateThumbprint}.p12");
        var certificateBase64 = Convert.ToBase64String(File.ReadAllBytes(certificatePath));

        var certificate = CertificateLoader.LoadFromBase64(certificateBase64);

        certificate.Thumbprint.Should().BeEquivalentTo(CertificateThumbprint);
    }
}




