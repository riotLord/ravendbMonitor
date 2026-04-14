using FluentAssertions;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Servercyde.Monitoring.Core.Tests.Infrastructure;

public class CertificateLoaderTests : TestFixture, IDisposable
{
    private const string CertificateThumbprint = "883D8A563352A56B56ABF340C2926B6D7217605D";

    public CertificateLoaderTests()
    {
        CertificateLoader.ResetLoadersForTesting();
        CertificateLoader.SetCertificateDirectoryPath(CertificateDirectoryPath);
    }

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
    public void Load_ValidThumbprint_ReturnsCertificateWithPrivateKey()
    {
        var certificate = CertificateLoader.Load(CertificateThumbprint);
        certificate.Thumbprint.Should().BeEquivalentTo(CertificateThumbprint);
        certificate.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadFromBase64_ValidCertificate_ReturnsCertificateWithPrivateKey()
    {
        var certificatePath = Path.Combine(CertificateDirectoryPath, $"{CertificateThumbprint}.p12");
        var certificateBase64 = Convert.ToBase64String(File.ReadAllBytes(certificatePath));

        var certificate = CertificateLoader.LoadFromBase64(certificateBase64);

        certificate.Thumbprint.Should().BeEquivalentTo(CertificateThumbprint);
        certificate.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadFromBase64_EmptyBase64_ThrowsArgumentNullException()
    {
        Action act = () => CertificateLoader.LoadFromBase64(string.Empty);
        act.Should().Throw<ArgumentNullException>().WithParameterName("certificateBase64");
    }

    [Fact]
    public void LoadFromBase64WithDiagnostics_Uses_DefaultKeySet_First()
    {
        var certificatePath = Path.Combine(CertificateDirectoryPath, $"{CertificateThumbprint}.p12");
        var certificateBase64 = Convert.ToBase64String(File.ReadAllBytes(certificatePath));
        var attemptedFlags = new List<X509KeyStorageFlags>();

        CertificateLoader.LoadPkcs12 = (rawBytes, password, flags) =>
        {
            attemptedFlags.Add(flags);
            return X509CertificateLoader.LoadPkcs12(rawBytes, password, flags);
        };

        var result = CertificateLoader.LoadFromBase64WithDiagnostics(certificateBase64);

        attemptedFlags.Should().ContainSingle()
            .Which.Should().Be(X509KeyStorageFlags.DefaultKeySet);
        result.KeyStorageMode.Should().Be(X509KeyStorageFlags.DefaultKeySet.ToString());
        result.Certificate.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadFromBase64WithDiagnostics_FallsBackTo_MachineKeySet_When_DefaultKeySet_Fails()
    {
        var certificatePath = Path.Combine(CertificateDirectoryPath, $"{CertificateThumbprint}.p12");
        var certificateBase64 = Convert.ToBase64String(File.ReadAllBytes(certificatePath));
        var attemptedFlags = new List<X509KeyStorageFlags>();

        CertificateLoader.LoadPkcs12 = (rawBytes, password, flags) =>
        {
            attemptedFlags.Add(flags);
            if (flags == X509KeyStorageFlags.DefaultKeySet)
            {
                throw new CryptographicException("Default import failed");
            }

            return X509CertificateLoader.LoadPkcs12(rawBytes, password, flags);
        };

        var result = CertificateLoader.LoadFromBase64WithDiagnostics(certificateBase64);

        attemptedFlags.Should().ContainInOrder(
            X509KeyStorageFlags.DefaultKeySet,
            X509KeyStorageFlags.MachineKeySet);
        result.KeyStorageMode.Should().Be(X509KeyStorageFlags.MachineKeySet.ToString());
        result.Certificate.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadWithDiagnostics_FallsBackTo_MachineKeySet_When_DefaultFileImport_Fails()
    {
        var attemptedFlags = new List<X509KeyStorageFlags>();

        CertificateLoader.LoadPkcs12CollectionFromFile = (path, password, flags) =>
        {
            attemptedFlags.Add(flags);
            if (flags == X509KeyStorageFlags.DefaultKeySet)
            {
                throw new CryptographicException("Default file import failed");
            }

            return X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password, flags);
        };

        var result = CertificateLoader.LoadWithDiagnostics(CertificateThumbprint);

        attemptedFlags.Should().ContainInOrder(
            X509KeyStorageFlags.DefaultKeySet,
            X509KeyStorageFlags.MachineKeySet);
        result.KeyStorageMode.Should().Be(X509KeyStorageFlags.MachineKeySet.ToString());
        result.Certificate.HasPrivateKey.Should().BeTrue();
    }

    public void Dispose()
    {
        CertificateLoader.ResetLoadersForTesting();
    }
}
