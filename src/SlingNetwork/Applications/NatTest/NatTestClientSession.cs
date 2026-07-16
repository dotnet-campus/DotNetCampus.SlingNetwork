using System.Net;
using System.Net.Sockets;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Transports;
using DotNetCampus.SlingNetwork.Transports.Models;
using DotNetCampus.SlingNetwork.Transports.Security;

namespace DotNetCampus.SlingNetwork.Applications.NatTest;

public class NatTestClientSession
{
    public required string SessionId { get; init; }

    public required IPAddress Server1Address { get; init; }

    public required int Server1Port1 { get; init; }

    public required int Server1Port2 { get; init; }

    public required string Server2Url { get; init; }

    public required IPAddress Server2Address { get; init; }

    public required ILogger Logger { get; init; }

    public required int PacketRepeatCount { get; init; }

    public required int PacketRepeatDelayMilliseconds { get; init; }

    public NatTestClientSessionPhase Prepare()
    {
        var phase = new NatTestClientSessionPhase
        {
            Phase = NatTestPhase.Filtering,
            Session = this,
            UdpClient = UdpClientFactory.CreateNew(0),
            Report = NatTestReport.Empty with
            {
                SessionId = SessionId,
            },
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
        var receiver = new UdpPacketReceiver(UdpClient, Session.Logger, $"[NAT-TEST][{Session.SessionId[..8]}]", TimeSpan.FromSeconds(10));

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase1SClientSend,
            SessionId = Session.SessionId,
            AlternateServerUrl = Session.Server2Url,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        var remoteEndPoint = new IPEndPoint(Session.Server1Address, Session.Server1Port1);
        Session.Logger.Info($"[NAT-TEST][{Session.SessionId[..8]}] UDP {NatTestUdpPacketHeader.Phase1SClientSend.ToHeaderString()} to {remoteEndPoint}");
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], remoteEndPoint, cts.Token);

        var receivedPackets = await receiver.ReceiveUtilAllMatches(cts.Token,
            (ep, p) => MatchesPacket(
                ep, p, NatTestUdpPacketHeader.Phase1RMainServerReply,
                new IPEndPoint(Session.Server1Address, Session.Server1Port1)),
            (ep, p) => MatchesPacket(
                ep, p, NatTestUdpPacketHeader.Phase11MainServerSend,
                new IPEndPoint(Session.Server1Address, Session.Server1Port2)),
            (ep, p) => MatchesPacket(
                ep, p, NatTestUdpPacketHeader.Phase12AlternateServerSend,
                Session.Server2Address));

        var phase1Reply = receivedPackets[0] is { UdpPacket: var phase1ReplyPacket }
            ? NatTestUdpPacket.TryParse(phase1ReplyPacket)
            : null;
        if (phase1Reply is null)
        {
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        if (receivedPackets[2] is { RemoteEndPoint: { } alternateServerEndPoint }
            && alternateServerEndPoint.Port != phase1Reply.AlternateServerPort1)
        {
            receivedPackets[2] = null;
        }

        var discoveredReport = Report with
        {
            AlternateServerPort1 = phase1Reply.AlternateServerPort1 ?? 0,
            AlternateServerPort2 = phase1Reply.AlternateServerPort2 ?? 0,
            ClientLocalEndPoint = (IPEndPoint)UdpClient.Client.LocalEndPoint!,
            ClientPublicEndPoint = IPEndPoint.Parse(phase1Reply.ClientPublicIPEndPoint!).Normalize(),
        };

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
                Report = discoveredReport,
            };
        }

        return this with
        {
            Phase = NatTestPhase.Mapping,
            Report = discoveredReport with
            {
                Filtering = filtering.Value,
            },
        };
    }

    public async Task<NatTestClientSessionPhase> MappingPhaseAsync()
    {
        if (Phase is not NatTestPhase.Mapping)
        {
            throw new InvalidOperationException("NAT test filtering phase must be done before mapping phase.");
        }

        using var cts = new CancellationTokenSource();
        var receiver = new UdpPacketReceiver(UdpClient, Session.Logger, $"[NAT-TEST][{Session.SessionId[..8]}]", TimeSpan.FromSeconds(10));

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase2SClientSend,
            SessionId = Session.SessionId,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        var remoteEndPoint = new IPEndPoint(Session.Server2Address, Report.AlternateServerPort1);
        Session.Logger.Info($"[NAT-TEST][{Session.SessionId[..8]}] UDP {NatTestUdpPacketHeader.Phase2SClientSend.ToHeaderString()} to {remoteEndPoint}");
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], remoteEndPoint, cts.Token);

        var receivedPackets = await receiver.ReceiveUtilAllMatches(cts.Token,
            (ep, p) => MatchesPacket(ep, p, NatTestUdpPacketHeader.Phase2RAlternateServerSend, remoteEndPoint));
        var natTestPacket = receivedPackets[0] is { UdpPacket: var receivedPacket }
            ? NatTestUdpPacket.TryParse(receivedPacket)
            : null;

        if (natTestPacket is null)
        {
            // 超时，可能 UDP 丢包严重或当前网络不通，应重测
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        var clientPublicEndPointToAlternateServer = IPEndPoint.Parse(natTestPacket.ClientPublicIPEndPoint!).Normalize();
        // 相等说明映射为「端点无关」，否则进行第 3 轮测试
        if (Equals(Report.ClientPublicEndPoint, clientPublicEndPointToAlternateServer))
        {
            return this with
            {
                Phase = NatTestPhase.Success,
                Report = Report with
                {
                    Success = true,
                    Mapping = NatMappingBehavior.EndpointIndependent,
                    ClientPublicEndPointToAlternateServerPort1 = clientPublicEndPointToAlternateServer,
                    ClientPublicEndPointToAlternateServerPort2 = null,
                },
            };
        }

        return this with
        {
            Phase = NatTestPhase.Mapping2,
            Report = Report with
            {
                ClientPublicEndPointToAlternateServerPort1 = clientPublicEndPointToAlternateServer,
            },
        };
    }

    public async Task<NatTestClientSessionPhase> Mapping2PhaseAsync()
    {
        if (Phase is not NatTestPhase.Mapping2)
        {
            throw new InvalidOperationException("NAT test mapping phase must be done before mapping-2 phase.");
        }

        using var cts = new CancellationTokenSource();
        var receiver = new UdpPacketReceiver(UdpClient, Session.Logger, $"[NAT-TEST][{Session.SessionId[..8]}]", TimeSpan.FromSeconds(10));

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase3SClientSend,
            SessionId = Session.SessionId,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        var remoteEndPoint = new IPEndPoint(Session.Server2Address, Report.AlternateServerPort2);
        Session.Logger.Info($"[NAT-TEST][{Session.SessionId[..8]}] UDP {NatTestUdpPacketHeader.Phase3SClientSend.ToHeaderString()} to {remoteEndPoint}");
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], remoteEndPoint, cts.Token);

        var receivedPackets = await receiver.ReceiveUtilAllMatches(cts.Token,
            (ep, p) => MatchesPacket(ep, p, NatTestUdpPacketHeader.Phase3RAlternateServerSend, remoteEndPoint));
        var natTestPacket = receivedPackets[0] is { UdpPacket: var receivedPacket }
            ? NatTestUdpPacket.TryParse(receivedPacket)
            : null;

        if (natTestPacket is null)
        {
            // 超时，可能 UDP 丢包严重或当前网络不通，应重测
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        var clientPublicEndPointToAlternateServer = IPEndPoint.Parse(natTestPacket.ClientPublicIPEndPoint!).Normalize();
        // 相等说明映射为「地址相关」，否则说明映射为「地址和端口均相关」
        if (Equals(Report.ClientPublicEndPointToAlternateServerPort1, clientPublicEndPointToAlternateServer))
        {
            return this with
            {
                Phase = NatTestPhase.Success,
                Report = Report with
                {
                    Success = true,
                    Mapping = NatMappingBehavior.AddressDependent,
                    ClientPublicEndPointToAlternateServerPort2 = clientPublicEndPointToAlternateServer,
                },
            };
        }

        return this with
        {
            Phase = NatTestPhase.Success,
            Report = Report with
            {
                Success = true,
                Mapping = NatMappingBehavior.AddressAndPortDependent,
                ClientPublicEndPointToAlternateServerPort2 = clientPublicEndPointToAlternateServer,
            },
        };
    }

    public async Task FinishAsync()
    {
        using var cts = new CancellationTokenSource();
        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase4Finish,
            SessionId = Session.SessionId,
        }.ToUdpPacket().ToPacketData(out var packetLength);

        var server1EndPoint = new IPEndPoint(Session.Server1Address, Session.Server1Port1);
        Session.Logger.Info($"[NAT-TEST][{Session.SessionId[..8]}] UDP {NatTestUdpPacketHeader.Phase4Finish.ToHeaderString()} to {server1EndPoint}");
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], server1EndPoint, cts.Token);

        if (Report.AlternateServerPort1 > 0)
        {
            var server2EndPoint = new IPEndPoint(Session.Server2Address, Report.AlternateServerPort1);
            Session.Logger.Info($"[NAT-TEST][{Session.SessionId[..8]}] UDP {NatTestUdpPacketHeader.Phase4Finish.ToHeaderString()} to {server2EndPoint}");
            await UdpClient.SendAsync(packetMemory.Memory[..packetLength], server2EndPoint, cts.Token);
        }
    }

    private bool MatchesPacket(
        IPEndPoint remoteEndPoint,
        UdpHeaderedKeyValuePacket packet,
        NatTestUdpPacketHeader expectedHeader,
        IPEndPoint expectedRemoteEndPoint)
    {
        return MatchesPacketPayload(packet, expectedHeader)
               && Equals(remoteEndPoint.Normalize(), expectedRemoteEndPoint.Normalize());
    }

    private bool MatchesPacket(
        IPEndPoint remoteEndPoint,
        UdpHeaderedKeyValuePacket packet,
        NatTestUdpPacketHeader expectedHeader,
        IPAddress expectedRemoteAddress)
    {
        return MatchesPacketPayload(packet, expectedHeader)
               && Equals(
                   remoteEndPoint.Address.Normalize(),
                   expectedRemoteAddress.Normalize());
    }

    private bool MatchesPacketPayload(
        UdpHeaderedKeyValuePacket packet,
        NatTestUdpPacketHeader expectedHeader)
    {
        return packet.Header == expectedHeader.ToHeaderString()
               && packet.Payload.GetValueOrDefault(nameof(NatTestUdpPacket.SessionId)) == Session.SessionId;
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
