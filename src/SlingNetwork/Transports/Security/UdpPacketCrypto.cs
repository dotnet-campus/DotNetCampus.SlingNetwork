using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCampus.SlingNetwork.Transports.Security;

internal static class UdpPacketCrypto
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static readonly ReadOnlyMemory<byte> Key = Convert.FromHexString(
        "0000000000000000000000000000000000000000000000000000000000000000");

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int PayloadLengthSize = 2;
    private static int HeaderSize => NonceSize + TagSize;

    public static byte[] Encrypt(string plainText)
    {
        Span<byte> packet = stackalloc byte[GetPacketSize(plainText)];
        Encrypt(plainText, packet);
        return packet.ToArray();
    }

    public static int GetPacketSize(string plainText)
    {
        return GetPacketSize(Utf8.GetByteCount(plainText));
    }

    public static int GetPacketSize(int plainTextByteCount)
    {
        return PickBucket(HeaderSize + PayloadLengthSize + plainTextByteCount);
    }

    /// <summary>
    /// 将明文加密成固定长度（几个档），报文格式为：[nonce][tag][encrypted(length+payload+padding)]。
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="packet"></param>
    /// <returns></returns>
    public static void Encrypt(ReadOnlySpan<char> plainText, Span<byte> packet)
    {
        // 1. 计算负载长度。
        var payloadLength = Utf8.GetByteCount(plainText);

        // 1. 预填充数据。
        var nonce = packet[..NonceSize];
        var tag = packet.Slice(NonceSize, TagSize);
        Span<byte> plain = stackalloc byte[packet.Length - HeaderSize];
        var cipher = packet[HeaderSize..];
        plain.Clear();
        packet[HeaderSize] = (byte)(payloadLength >> 8);
        packet[HeaderSize + 1] = (byte)payloadLength;
        RandomNumberGenerator.Fill(nonce);
        Utf8.GetBytes(plainText, plain[PayloadLengthSize..]);

        // 2. 加密。
        using var aes = new AesGcm(Key.Span, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
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

        var nonce = packet[..NonceSize];
        var tag = packet.Slice(NonceSize, TagSize);
        var cipher = packet[HeaderSize..];

        Span<byte> plain = stackalloc byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(Key.Span, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
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
