using System.Buffers;
using System.Text;

namespace DotNetCampus.SlingNetwork.Transports.Security;

public readonly record struct UdpHeaderedKeyValuePacket
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public required string Header { get; init; }
    public required IReadOnlyDictionary<string, string?> Payload { get; init; }

    public static UdpHeaderedKeyValuePacket? TryParse(Span<byte> packet)
    {
        if (!UdpPacketCrypto.TryDecrypt(packet, out var plainTextString))
        {
            // 无法解密，解析失败
            return null;
        }

        var plainText = plainTextString.AsSpan();
        var separatorIndex = plainText.IndexOf('\n');
        if (separatorIndex < 0)
        {
            // 不含有效头，解析失败
            return null;
        }

        var header = plainText[..separatorIndex];
        var payload = new Dictionary<string, string?>();

        while (separatorIndex >= 0)
        {
            var nextSeparatorIndex = plainText[(separatorIndex + 1)..].IndexOf('\n') + separatorIndex + 1;
            var line = nextSeparatorIndex > separatorIndex
                ? plainText[(separatorIndex + 1)..nextSeparatorIndex]
                : plainText[(separatorIndex + 1)..];
            separatorIndex = nextSeparatorIndex > separatorIndex
                ? nextSeparatorIndex
                : -1;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
            {
                // 当前段不符合 Key=Value 格式
                return null;
            }
            var key = line[..equalsIndex];
            var value = line[(equalsIndex + 1)..];
            payload[key.ToString()] = value.ToString();
        }

        return new UdpHeaderedKeyValuePacket
        {
            Header = header.ToString(),
            Payload = payload,
        };
    }

    public IMemoryOwner<byte> ToPacketData(out int packetLength)
    {
        packetLength = GetPacketLength();
        var memory = MemoryPool<byte>.Shared.Rent(packetLength);
        FillInto(memory.Memory.Span);
        return memory;
    }

    public void FillInto(Span<byte> packet)
    {
        Span<char> plainText = stackalloc char[GetCharCount()];
        FillInto(plainText);
        UdpPacketCrypto.Encrypt(plainText, packet);
    }

    private void FillInto(Span<char> plainText)
    {
        Header.AsSpan().CopyTo(plainText[..Header.Length]);
        var currentIndex = Header.Length;
        foreach (var (key, value) in Payload)
        {
            if (value is null)
            {
                continue;
            }

            // 换行符 \n
            plainText[currentIndex] = '\n';

            // Key
            key.AsSpan().CopyTo(plainText.Slice(currentIndex + 1, key.Length));

            // 等号 =
            plainText[currentIndex + 1 + key.Length] = '=';

            // Value
            value.AsSpan().CopyTo(plainText.Slice(currentIndex + 1 + key.Length + 1, value.Length));

            // 更新索引
            currentIndex += key.Length + (value?.Length ?? 0) + 2;
        }
    }

    public int GetPacketLength()
    {
        var length = Utf8.GetByteCount(Header);
        foreach (var (key, value) in Payload)
        {
            if (value is null)
            {
                continue;
            }

            // 换行符 \n
            length += 1;

            // Key
            length += Utf8.GetByteCount(key);

            // 等号 =
            length += 1;

            // Value
            length += Utf8.GetByteCount(value);
        }
        return UdpPacketCrypto.GetPacketSize(length);
    }

    private int GetCharCount()
    {
        var count = Header.Length;
        foreach (var (key, value) in Payload)
        {
            if (value is null)
            {
                continue;
            }

            // 换行符 \n
            count += 1;

            // Key
            count += key.Length;

            // 等号 =
            count += 1;

            // Value
            count += value.Length;
        }
        return count;
    }
}
