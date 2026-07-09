using System.Security.Cryptography;
using System.Text;

namespace DotNetCampus.SlingNetwork.Services.Security;

internal class PeerIdentity(ECDsa key)
{
    private const string PrivateKeyFileName = "node.private.pem";

    public string PublicKey { get; } = key.ExportSubjectPublicKeyInfoPem();

    public static PeerIdentity LoadOrCreate()
    {
        var privatePath = Path.Combine(System.AppContext.BaseDirectory, PrivateKeyFileName);

        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        if (File.Exists(privatePath))
        {
            ecdsa.ImportFromPem(File.ReadAllText(privatePath));
            return new PeerIdentity(ecdsa);
        }

        Directory.CreateDirectory(System.AppContext.BaseDirectory);
        File.WriteAllText(privatePath, ecdsa.ExportECPrivateKeyPem());
        return new PeerIdentity(ecdsa);
    }

    public string GetFingerprint()
    {
        var bytes = Encoding.UTF8.GetBytes(PublicKey);
        return Convert.ToHexString(SHA256.HashData(bytes))[..16];
    }

    public byte[] Sign(string text)
    {
        return key.SignData(
            Encoding.UTF8.GetBytes(text),
            HashAlgorithmName.SHA256);
    }

    public static bool Verify(string publicKeyPem, string text, byte[] signature)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);

        return key.VerifyData(
            Encoding.UTF8.GetBytes(text),
            signature,
            HashAlgorithmName.SHA256);
    }
}
