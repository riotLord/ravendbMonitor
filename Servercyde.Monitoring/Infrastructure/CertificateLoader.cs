using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Servercyde.Monitoring.Core.Infrastructure;

public static class CertificateLoader
{
    private static string _certificateDirectoryPath = "/var/ssl/private";
    internal static Func<byte[], string?, X509KeyStorageFlags, X509Certificate2> LoadPkcs12 = DefaultLoadPkcs12;
    internal static Func<string, string?, X509KeyStorageFlags, X509Certificate2Collection> LoadPkcs12CollectionFromFile = DefaultLoadPkcs12CollectionFromFile;

    public static void SetCertificateDirectoryPath(string value)
    {
        _certificateDirectoryPath = value;
    }

    public static X509Certificate2 Load(string thumbprint, string? password = null)
        => LoadWithDiagnostics(thumbprint, password).Certificate;

    public static CertificateLoadResult LoadWithDiagnostics(
        string thumbprint,
        string? password = null,
        ILogger? logger = null)
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
        if (cert != null)
        {
            var storeResult = new CertificateLoadResult(cert, "CurrentUserStore", "Store");
            LogCertificateLoaded(storeResult, logger);
            return storeResult;
        }

        var path = Path.Combine(_certificateDirectoryPath, $"{thumbprint}.p12");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Certificate with thumbprint {thumbprint} was not found");
        }

        return LoadFromFileWithFallback(path, password, logger);
    }

    public static X509Certificate2 LoadFromBase64(string certificateBase64, string? password = null)
        => LoadFromBase64WithDiagnostics(certificateBase64, password).Certificate;

    public static CertificateLoadResult LoadFromBase64WithDiagnostics(
        string certificateBase64,
        string? password = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(certificateBase64))
        {
            throw new ArgumentNullException(nameof(certificateBase64));
        }

        var rawBytes = Convert.FromBase64String(certificateBase64);
        return LoadPkcs12WithFallback(rawBytes, password, "Base64", logger);
    }

    internal static void ResetLoadersForTesting()
    {
        LoadPkcs12 = DefaultLoadPkcs12;
        LoadPkcs12CollectionFromFile = DefaultLoadPkcs12CollectionFromFile;
    }

    internal static CertificateLoadResult LoadPkcs12WithFallback(
        byte[] rawBytes,
        string? password,
        string source,
        ILogger? logger = null)
    {
        return LoadWithFallback(
            source,
            flag => LoadPkcs12(rawBytes, password, flag),
            logger);
    }

    internal static CertificateLoadResult LoadFromFileWithFallback(
        string path,
        string? password,
        ILogger? logger = null)
    {
        return LoadWithFallback(
            "File",
            flag => LoadPkcs12CollectionFromFile(path, password, flag)[0],
            logger);
    }

    private static CertificateLoadResult LoadWithFallback(
        string source,
        Func<X509KeyStorageFlags, X509Certificate2> loader,
        ILogger? logger)
    {
        Exception? lastException = null;

        foreach (var flag in GetImportFlags())
        {
            try
            {
                var certificate = loader(flag);
                var result = new CertificateLoadResult(certificate, flag.ToString(), source);
                LogCertificateLoaded(result, logger);
                return result;
            }
            catch (Exception ex) when (IsFallbackException(ex))
            {
                lastException = ex;
                logger?.LogWarning(
                    ex,
                    "Certificate import failed using storage mode {KeyStorageMode}; trying next fallback",
                    flag);
            }
        }

        throw new CryptographicException(
            $"Failed to load certificate from {source} using any supported key storage mode.",
            lastException);
    }

    private static IEnumerable<X509KeyStorageFlags> GetImportFlags()
    {
        yield return X509KeyStorageFlags.DefaultKeySet;
        yield return X509KeyStorageFlags.MachineKeySet;
        yield return X509KeyStorageFlags.EphemeralKeySet;
    }

    private static bool IsFallbackException(Exception ex)
        => ex is CryptographicException || ex is PlatformNotSupportedException;

    private static void LogCertificateLoaded(CertificateLoadResult result, ILogger? logger)
    {
        logger?.LogInformation(
            "Loaded RavenDB certificate from {CertificateSource} using {KeyStorageMode}. Thumbprint={Thumbprint}, Subject={Subject}, HasPrivateKey={HasPrivateKey}, NotAfter={NotAfter:o}",
            result.Source,
            result.KeyStorageMode,
            result.Certificate.Thumbprint,
            result.Certificate.Subject,
            result.Certificate.HasPrivateKey,
            result.Certificate.NotAfter);
    }

    private static X509Certificate2 DefaultLoadPkcs12(
        byte[] rawBytes,
        string? password,
        X509KeyStorageFlags flags)
        => X509CertificateLoader.LoadPkcs12(rawBytes, password, flags);

    private static X509Certificate2Collection DefaultLoadPkcs12CollectionFromFile(
        string path,
        string? password,
        X509KeyStorageFlags flags)
        => X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password, flags);
}

public sealed record CertificateLoadResult(
    X509Certificate2 Certificate,
    string KeyStorageMode,
    string Source);
