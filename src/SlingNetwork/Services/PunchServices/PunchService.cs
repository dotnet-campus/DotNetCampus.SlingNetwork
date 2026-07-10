using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Services.Security;

namespace DotNetCampus.SlingNetwork.Services.PunchServices;

public class PunchService(ServerContext context)
{
    private readonly ConcurrentDictionary<string, Dictionary<string, PeerPunchInfo>> _punchingPeers = [];

    public async Task Listen()
    {
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
        {
            DualMode = true,
        };
        socket.Bind(new IPEndPoint(IPAddress.Any, context.UdpInfo.Port));

        Span<byte> buffer = new byte[1024 * 2];
        while (true)
        {
            EndPoint remote = new IPEndPoint(IPAddress.IPv6Any, 0);
            var length = socket.ReceiveFrom(buffer, ref remote);
            HandleUdpPacket(socket, buffer[..length], (IPEndPoint)remote);
        }
    }

    private void HandleUdpPacket(Socket socket, Span<byte> udpPacket, IPEndPoint remote)
    {
        if (!UdpPacketCrypto.TryDecrypt(udpPacket, out var receivedMessage))
        {
            context.Logger.Warn($"[Punch] Received from [{remote.Address}:{remote.Port}]: <Unknown>");
            return;
        }

        context.Logger.Info($"[Punch] Received from [{remote.Address}:{remote.Port}]: {receivedMessage}");
        if (!PeerPunchInfo.TryParse(receivedMessage, out var peerPunchInfo))
        {
            context.Logger.Warn($"[Punch] Parsing failed: {receivedMessage}");
            return;
        }

        // 立即回复当前消息，让客户端知道服务器正在正常工作。
        var replyMessage = $"[Reply] IPEndPoint={remote.Address}:{remote.Port}";
        var packet = UdpPacketCrypto.Encrypt(replyMessage);
        context.Logger.Info($"[Punch] Replying to [{remote.Address}:{remote.Port}]: {replyMessage}");
        socket.SendTo(packet, remote);

        // 更新端点信息。
        _punchingPeers.AddOrUpdate(peerPunchInfo.Group,
            _ => new Dictionary<string, PeerPunchInfo> { [peerPunchInfo.Peer] = peerPunchInfo with { IPEndPoint = remote } },
            (_, d) =>
            {
                d[peerPunchInfo.Peer] = peerPunchInfo with { IPEndPoint = remote };
                // 检查是否需要向所有端发起同时打洞指令。
                if (d.Count > 0)
                {
                    foreach (var (peer, info) in d)
                    {
                        var startMessage = $"[Start] {info}";
                        var startPacket = UdpPacketCrypto.Encrypt(startMessage);
                        context.Logger.Info($"[Start] Notify to [({remote.Address}:{remote.Port}]: {startMessage}");
                        socket.SendTo(startPacket, info.IPEndPoint!);
                    }
                }
                return d;
            });
    }
}

internal record PeerPunchInfo
{
    public required string Peer { get; init; }

    public required string Group { get; init; }

    public required string PublicKey { get; init; }

    public IPEndPoint? IPEndPoint { get; init; }

    public static bool TryParse(string message, [NotNullWhen(true)] out PeerPunchInfo? peerPunchInfo)
    {
        var parts = message.Split(';');
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex < 1)
            {
                continue;
            }
            var key = part.Substring(0, equalsIndex);
            var value = part.Substring(equalsIndex + 1);
            dictionary[key] = value;
        }
        var peer = dictionary.GetValueOrDefault(nameof(Peer));
        var publicKey = dictionary.GetValueOrDefault(nameof(PublicKey));
        var group = dictionary.GetValueOrDefault(nameof(Group));
        var ipEndPoint = dictionary.TryGetValue(nameof(IPEndPoint), out var ipEndPointText)
                         && IPEndPoint.TryParse(ipEndPointText, out var ipEndPointValue)
            ? ipEndPointValue
            : null;
        if (peer is not null && group is not null && publicKey is not null)
        {
            peerPunchInfo = new PeerPunchInfo
            {
                Peer = peer,
                Group = group,
                PublicKey = publicKey,
                IPEndPoint = ipEndPoint,
            };
            return true;
        }
        peerPunchInfo = null;
        return false;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Peer is { } peer)
        {
            builder.Append(nameof(Peer));
            builder.Append('=');
            builder.Append(peer);
            builder.Append(';');
        }
        if (Group is { } group)
        {
            builder.Append(nameof(Group));
            builder.Append('=');
            builder.Append(group);
            builder.Append(';');
        }
        if (PublicKey is { } publicKey)
        {
            builder.Append(nameof(PublicKey));
            builder.Append('=');
            builder.Append(publicKey);
            builder.Append(';');
        }
        if (IPEndPoint is { } ipEndPoint)
        {
            builder.Append(nameof(IPEndPoint));
            builder.Append('=');
            builder.Append(ipEndPoint);
            builder.Append(';');
        }
        return builder.ToString();
    }
}
