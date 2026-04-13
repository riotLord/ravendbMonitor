using System.Security.Cryptography.X509Certificates;

namespace Servercyde.Monitoring.Core.Infrastructure;

public static class CertificateLoader
{
    private static string _certificateDirectoryPath = "/var/ssl/private";

    public static void SetCertificateDirectoryPath(string value)
    {
        _certificateDirectoryPath = value;
    }

    public static X509Certificate2 Load(string thumbprint, string? password = null)
    {
       
        if (string.IsNullOrEmpty(thumbprint))
        {
            throw new ArgumentNullException(nameof(thumbprint));
        }

        using var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        certStore.Open(OpenFlags.ReadOnly);
        var certCollection =
            certStore.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                false);

        var cert = certCollection.OfType<X509Certificate2>().FirstOrDefault();
        if (cert == null)
        {
            var path = Path.Combine(_certificateDirectoryPath, $"{thumbprint}.p12");
            if (File.Exists(path))
            {
                cert = X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password, X509KeyStorageFlags.MachineKeySet)[0];
            }
            else
            {
                throw new InvalidOperationException(
                    $"Certificate with thumbprint {thumbprint} was not found");
            }
        }
        return cert;
    }

    public static X509Certificate2 LoadFromBase64(string certificateBase64, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(certificateBase64))
        {
            throw new ArgumentNullException(nameof(certificateBase64));
        }

        var rawBytes = Convert.FromBase64String(certificateBase64);
        return X509CertificateLoader.LoadPkcs12(rawBytes, password, X509KeyStorageFlags.MachineKeySet);
    }
}
