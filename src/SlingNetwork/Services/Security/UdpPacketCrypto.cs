using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCampus.SlingNetwork.Services.Security;

internal static class UdpPacketCrypto
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static readonly ReadOnlyMemory<byte> Key = Convert.FromHexString(
        "0000000000000000000000000000000000000000000000000000000000000000");

    private const byte Version = 1;
    private const int VersionSize = sizeof(byte);
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int PayloadLengthSize = 2;
    private static int HeaderSize => VersionSize + NonceSize + TagSize;

    public static byte[] Encrypt(string plainText)
    {
        Span<byte> packet = stackalloc byte[GetPacketSize(plainText)];
        Encrypt(plainText, packet);
        return packet.ToArray();
    }

    public static int GetPacketSize(string plainText)
    {
        var payloadLength = Utf8.GetByteCount(plainText);
        return PickBucket(HeaderSize + PayloadLengthSize + payloadLength);
    }

    /// <summary>
    /// 将明文加密成固定长度（几个档），报文格式为：[version][nonce][tag][encrypted(length+payload+padding)]。
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="packet"></param>
    /// <returns></returns>
    public static void Encrypt(string plainText, Span<byte> packet)
    {
        // 1. 计算负载长度。
        var payloadLength = Utf8.GetByteCount(plainText);

        // 1. 预填充数据。
        packet[0] = Version;
        var nonce = packet.Slice(VersionSize, NonceSize);
        var tag = packet.Slice(VersionSize + NonceSize, TagSize);
        var plain = packet.Slice(HeaderSize);
        var cipher = plain;
        plain.Clear();
        packet[HeaderSize] = (byte)(payloadLength >> 8);
        packet[HeaderSize + 1] = (byte)payloadLength;
        RandomNumberGenerator.Fill(nonce);
        Utf8.GetBytes(plainText.AsSpan(), plain.Slice(PayloadLengthSize));

        // 2. 加密。
        Span<byte> plainCopy = stackalloc byte[plain.Length];
        plain.CopyTo(plainCopy);
        using var aes = new AesGcm(Key.Span, TagSize);
        aes.Encrypt(nonce, plainCopy, cipher, tag, packet[..VersionSize]);
    }

    public static bool TryDecrypt(ReadOnlySpan<byte> packet, [NotNullWhen(true)] out string? plainText)
    {
        plainText = null;

        if (packet.Length < HeaderSize + PayloadLengthSize)
        {
            return false;
        }

        if (!IsValidPacketSize(packet.Length))
        {
            return false;
        }

        if (packet[0] != Version)
        {
            return false;
        }

        var nonce = packet.Slice(VersionSize, NonceSize);
        var tag = packet.Slice(VersionSize + NonceSize, TagSize);
        var cipher = packet.Slice(HeaderSize);

        Span<byte> plain = stackalloc byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(Key.Span, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, packet[..VersionSize]);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var payloadLength = (plain[0] << 8) | plain[1];

        if (payloadLength < 0 || payloadLength > plain.Length - PayloadLengthSize)
        {
            return false;
        }

        plainText = Utf8.GetString(plain.Slice(PayloadLengthSize, payloadLength));

        return true;
    }

    private static int PickBucket(int length) => length switch
    {
        <= 64 => 64,
        <= 128 => 128,
        <= 256 => 256,
        <= 512 => 512,
        <= 1024 => 1024,
        _ => throw new InvalidOperationException($"Too large UDP packet."),
    };

    private static bool IsValidPacketSize(int length) => length switch
    {
        64 or 128 or 256 or 512 or 1024 => true,
        _ => false,
    };
}
