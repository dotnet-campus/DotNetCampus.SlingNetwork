using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using DotNetCampus.SlingNetwork.Transports.Security;

namespace DotNetCampus.SlingNetwork.Transports.Models;

/// <summary>
/// 在 NAT 探测任务全程进行 UDP 收发时的数据负载。
/// </summary>
public record NatTestUdpPacket
{
    /// <summary>
    /// NAT 探测任务中 UDP 包的头部。
    /// </summary>
    public required NatTestUdpPacketHeader Header { get; init; }

    /// <summary>
    /// 本次 NAT 测试的会话 Id。
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 备用控制服务器的地址。客户端希望主服务器向备用服务器申请 NAT 辅助测试，辅助客户端完成 NAT 类型探测任务。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AlternateServerUrl { get; init; }

    /// <summary>
    /// 客户端在公网上看起来的 IP:Port 端点。在不同控制服务器看来，客户端的 IP:Port 端点可能是不同的。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientPublicIPEndPoint { get; init; }

    /// <summary>
    /// 备用服务器配合客户端进行 NAT 类型探测任务时，同意公开的测试用端口 1。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AlternateServerPort1 { get; init; }

    /// <summary>
    /// 备用服务器配合客户端进行 NAT 类型探测任务时，同意公开的测试用端口 2。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AlternateServerPort2 { get; init; }

    public UdpHeaderedKeyValuePacket ToUdpPacket() => new()
    {
        Header = Header.ToHeaderString(),
        Payload = new Dictionary<string, string?>
        {
            [nameof(SessionId)] = SessionId,
            [nameof(AlternateServerUrl)] = AlternateServerUrl,
            [nameof(ClientPublicIPEndPoint)] = ClientPublicIPEndPoint,
            [nameof(AlternateServerPort1)] = AlternateServerPort1?.ToString(CultureInfo.InvariantCulture),
            [nameof(AlternateServerPort2)] = AlternateServerPort2?.ToString(CultureInfo.InvariantCulture),
        },
    };

    public static NatTestUdpPacket? TryParse(UdpHeaderedKeyValuePacket packet)
    {
        var header = NatTestUdpPacketHeader.ParseFromHeader(packet.Header);
        var sessionId = packet.Payload.GetValueOrDefault(nameof(SessionId));
        var alternateServerUrl = packet.Payload.GetValueOrDefault(nameof(AlternateServerUrl));
        var clientPublicIPEndPoint = packet.Payload.GetValueOrDefault(nameof(ClientPublicIPEndPoint));
        var alternateServerPort1String = packet.Payload.GetValueOrDefault(nameof(AlternateServerPort1));
        var alternateServerPort2String = packet.Payload.GetValueOrDefault(nameof(AlternateServerPort2));
        if (sessionId is null)
        {
            return null;
        }
        return new NatTestUdpPacket
        {
            Header = header,
            SessionId = sessionId,
            AlternateServerUrl = alternateServerUrl,
            ClientPublicIPEndPoint = clientPublicIPEndPoint,
            AlternateServerPort1 = int.TryParse(alternateServerPort1String, NumberStyles.Integer, CultureInfo.InvariantCulture, out var alternateServerPort1)
                ? alternateServerPort1
                : 0,
            AlternateServerPort2 = int.TryParse(alternateServerPort2String, NumberStyles.Integer, CultureInfo.InvariantCulture, out var alternateServerPort2)
                ? alternateServerPort2
                : 0,
        };
    }
}

public enum NatTestUdpPacketHeader
{
    Phase1SClientSend,
    Phase1RMainServerReply,
    Phase11MainServerSend,
    Phase12AlternateServerSend,
    Phase2SClientSend,
    Phase2RAlternateServerSend,
    Phase3SClientSend,
    Phase3RAlternateServerSend,
}

public static class NatTestUdpPacketHeaderExtensions
{
    extension(NatTestUdpPacketHeader header)
    {
        public string ToHeaderString() => header switch
        {
            NatTestUdpPacketHeader.Phase1SClientSend => "[NAT-1S]",
            NatTestUdpPacketHeader.Phase1RMainServerReply => "[NAT-1R]",
            NatTestUdpPacketHeader.Phase11MainServerSend => "[NAT-11]",
            NatTestUdpPacketHeader.Phase12AlternateServerSend => "[NAT-12]",
            NatTestUdpPacketHeader.Phase2SClientSend => "[NAT-2S]",
            NatTestUdpPacketHeader.Phase2RAlternateServerSend => "[NAT-2R]",
            NatTestUdpPacketHeader.Phase3SClientSend => "[NAT-3S]",
            NatTestUdpPacketHeader.Phase3RAlternateServerSend => "[NAT-3R]",
            _ => throw new InvalidEnumArgumentException(nameof(header), (int)header, typeof(NatTestUdpPacketHeader)),
        };

        public static NatTestUdpPacketHeader ParseFromHeader(string headerString) => headerString switch
        {
            "[NAT-1S]" => NatTestUdpPacketHeader.Phase1SClientSend,
            "[NAT-1R]" => NatTestUdpPacketHeader.Phase1RMainServerReply,
            "[NAT-11]" => NatTestUdpPacketHeader.Phase11MainServerSend,
            "[NAT-12]" => NatTestUdpPacketHeader.Phase12AlternateServerSend,
            "[NAT-2S]" => NatTestUdpPacketHeader.Phase2SClientSend,
            "[NAT-2R]" => NatTestUdpPacketHeader.Phase2RAlternateServerSend,
            "[NAT-3S]" => NatTestUdpPacketHeader.Phase3SClientSend,
            "[NAT-3R]" => NatTestUdpPacketHeader.Phase3RAlternateServerSend,
            _ => throw new ArgumentException($"Invalid NAT test header."),
        };
    }
}
