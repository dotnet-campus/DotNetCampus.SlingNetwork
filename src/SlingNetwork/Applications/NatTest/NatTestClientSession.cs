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
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], new IPEndPoint(Session.Server1Address, Session.Server1Port1), cts.Token);

        var receivedPackets = await UdpClient.ReceiveUtilAllMatches(TimeSpan.FromSeconds(10), cts.Token,
            (_, p) => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase1RMainServerReply,
            (_, p) => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase11MainServerSend,
            (_, p) => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase12AlternateServerSend);

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

        var natTestUdpPacket = NatTestUdpPacket.TryParse(receivedPackets[0]!.Value.UdpPacket)!;
        return this with
        {
            Phase = NatTestPhase.Mapping,
            Report = Report with
            {
                Filtering = filtering.Value,
                AlternateServerPort1 = natTestUdpPacket.AlternateServerPort1 ?? 0,
                AlternateServerPort2 = natTestUdpPacket.AlternateServerPort2 ?? 0,
                ClientLocalEndPoint = (IPEndPoint)UdpClient.Client.LocalEndPoint!,
                ClientPublicEndPoint = IPEndPoint.Parse(natTestUdpPacket.ClientPublicIPEndPoint!),
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

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase2SClientSend,
            SessionId = Session.SessionId,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], new IPEndPoint(Session.Server2Address, Report.AlternateServerPort1), cts.Token);

        var receivedPackets = await UdpClient.ReceiveUtilAllMatches(TimeSpan.FromSeconds(10), cts.Token,
            (_, p) => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase2RAlternateServerSend);
        var natTestPacket = receivedPackets.Select(x => x is { } p ? NatTestUdpPacket.TryParse(p.UdpPacket) : null).First();

        if (natTestPacket is null)
        {
            // 超时，可能 UDP 丢包严重或当前网络不通，应重测
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        var clientPublicEndPointToAlternateServer = IPEndPoint.Parse(NatTestUdpPacket.TryParse(receivedPackets[0]!.Value.UdpPacket)!.ClientPublicIPEndPoint!);
        // 相等说明映射为「端点无关」，否则进行第 3 轮测试
        if (Equals(Report.ClientPublicEndPoint, clientPublicEndPointToAlternateServer))
        {
            return this with
            {
                Phase = NatTestPhase.Success,
                Report = Report with
                {
                    Mapping = NatMappingBehavior.EndpointIndependent,
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
        if (Phase is not NatTestPhase.Mapping)
        {
            throw new InvalidOperationException("NAT test mapping phase must be done before mapping-2 phase.");
        }

        using var cts = new CancellationTokenSource();

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase3SClientSend,
            SessionId = Session.SessionId,
        }.ToUdpPacket().ToPacketData(out var packetLength);
        await UdpClient.SendAsync(packetMemory.Memory[..packetLength], new IPEndPoint(Session.Server2Address, Report.AlternateServerPort2), cts.Token);

        var receivedPackets = await UdpClient.ReceiveUtilAllMatches(TimeSpan.FromSeconds(10), cts.Token,
            (_, p) => NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase3RAlternateServerSend);
        var natTestPacket = receivedPackets.Select(x => x is { } p ? NatTestUdpPacket.TryParse(p.UdpPacket) : null).First();

        if (natTestPacket is null)
        {
            // 超时，可能 UDP 丢包严重或当前网络不通，应重测
            return this with
            {
                Phase = NatTestPhase.Failed,
            };
        }

        var clientPublicEndPointToAlternateServer = IPEndPoint.Parse(NatTestUdpPacket.TryParse(receivedPackets[0]!.Value.UdpPacket)!.ClientPublicIPEndPoint!);
        // 相等说明映射为「地址相关」，否则说明映射为「地址和端口均相关」
        if (Equals(Report.ClientPublicEndPoint, clientPublicEndPointToAlternateServer))
        {
            return this with
            {
                Phase = NatTestPhase.Success,
                Report = Report with
                {
                    Mapping = NatMappingBehavior.AddressDependent,
                    ClientPublicEndPointToAlternateServerPort2 = clientPublicEndPointToAlternateServer,
                },
            };
        }

        return this with
        {
            Phase = NatTestPhase.Mapping2,
            Report = Report with
            {
                Mapping = NatMappingBehavior.AddressAndPortDependent,
                ClientPublicEndPointToAlternateServerPort2 = clientPublicEndPointToAlternateServer,
            },
        };
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
