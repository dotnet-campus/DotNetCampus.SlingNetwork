using System.Net;
using System.Net.Sockets;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Transports;
using DotNetCampus.SlingNetwork.Transports.Models;
using DotNetCampus.SlingNetwork.Transports.Security;

namespace DotNetCampus.SlingNetwork.ClientSide;

public class NatTestClientSession
{
    public required string SessionId { get; init; }

    public required IPAddress Server1Address { get; init; }

    public required int Server1Port1 { get; init; }

    public required int Server1Port2 { get; init; }

    public required string Server2Host { get; init; }

    public required IPAddress Server2Address { get; init; }

    public required ILogger Logger { get; init; }

    public NatTestClientSessionPhase Prepare()
    {
        var phase = new NatTestClientSessionPhase
        {
            Phase = NatTestPhase.Filtering,
            Session = this,
            UdpClient = UdpClientFactory.CreateNew(0),
            Report = NatTestReport.Empty,
        };
        return phase;
    }
}

public record NatTestClientSessionPhase
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    public required NatTestClientSession Session { get; init; }
    public required NatTestPhase Phase { get; init; }
    public required UdpClient UdpClient { get; init; }
    public required NatTestReport Report { get; init; }
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public async Task<NatTestClientSessionPhase> FilteringPhaseAsync()
    {
        if (Phase is not NatTestPhase.Filtering)
        {
            throw new InvalidOperationException("NAT test must be prepared before filtering phase.");
        }

        using var cts = new CancellationTokenSource();

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase1SClientSend,
            SessionId = Session.SessionId,
            AlternateServer = Session.Server2Host,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], cts.Token);

        var receivedPackets = await UdpClient.ReceiveUtilAllMatches(TimeSpan.FromSeconds(10), cts.Token,
            p => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase1RMainServerReply,
            p => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase11MainServerSend,
            p => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase12AlternateServerSend);
        var natTestPackets = receivedPackets.Select(x => x is { } p ? NatTestUdpPacket.TryParse(p) : null).ToList();

        NetworkPacketFilteringBehavior? filtering = (receivedPackets[0], receivedPackets[1], receivedPackets[2]) switch
        {
            (not null, not null, not null) => NetworkPacketFilteringBehavior.EndpointIndependent,
            (not null, not null, null) => NetworkPacketFilteringBehavior.AddressDependent,
            (not null, null, null) => NetworkPacketFilteringBehavior.AddressAndPortDependent,
            (not null, null, not null) => null, // 不符合 RFC5780 过滤模型，可能是丢包等因素导致，测试无效，重测
            _ => null, // 未收到主要回复，可能丢包严重，测试结果大概率不可信，重测
        };
        if (filtering is null)
        {
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        return this with
        {
            Phase = NatTestPhase.Mapping,
            Report = Report with
            {
                Filtering = filtering.Value,
                LocalEndPoint = (IPEndPoint)UdpClient.Client.LocalEndPoint!,
                PublicEndPoint = IPEndPoint.Parse(NatTestUdpPacket.TryParse(receivedPackets[0]!.Value)!.ClientPublicIPEndPoint!),
            },
        };
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await UdpClient.ReceiveAsync(ct);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue;
            }

            var packet = result.Buffer;
            var remote = result.RemoteEndPoint;

            if (!UdpPacketCrypto.TryDecrypt(packet, out var message))
            {
                continue;
            }

            Session.Logger.Info($"[Punch] Received from [{remote}]({packet.Length}): {message}");
        }
    }
}

public enum NatTestPhase
{
    Filtering,
    Mapping,
    Mapping2,
    Success,
    Failed,
}
