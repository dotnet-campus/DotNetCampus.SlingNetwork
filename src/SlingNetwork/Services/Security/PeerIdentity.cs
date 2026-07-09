using System.Security.Cryptography;

namespace DotNetCampus.SlingNetwork.Services.Security;

internal class PeerIdentity(ECDsa key)
{
    private const string PrivateKeyFileName = "node.private.pem";

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

    public string ExportPublicKey()
    {
        return key.ExportSubjectPublicKeyInfoPem();
    }
}
