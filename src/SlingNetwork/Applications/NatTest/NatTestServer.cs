using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.ServerSide.ControlServices;
using DotNetCampus.SlingNetwork.Transports;
using DotNetCampus.SlingNetwork.Transports.Models;
using DotNetCampus.SlingNetwork.Transports.Security;

namespace DotNetCampus.SlingNetwork.Applications.NatTest;

public static class NatTestServer
{
    private const int UdpPacketRepeatCount = 4;
    private static readonly TimeSpan UdpPacketRepleatDelay = TimeSpan.FromMilliseconds(500);

    public static async Task ServerFilteringPhaseAsync(
        HttpClient httpClient, ILogger logger,
        string sessionId, int port1, int port2,
        CancellationToken cancellationToken)
    {
        using var udpClient1 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient1.Client.DualMode = true;
        udpClient1.Client.Bind(new IPEndPoint(IPAddress.Any, port1));

        using var udpClient2 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient2.Client.DualMode = true;
        udpClient2.Client.Bind(new IPEndPoint(IPAddress.Any, port2));

        // 第 1.2 轮
        var receivedPackets = await udpClient1.ReceiveUtilAllMatches(
            TimeSpan.FromMinutes(2), cancellationToken,
            (_, p) =>
                p.Payload.GetValueOrDefault(nameof(NatTestSession.SessionId)) == sessionId
                && NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase1SClientSend);
        if (receivedPackets[0] is not ({ } remoteEndPoint, { } receivedPacket))
        {
            // 客户端未在 NAT 测试请求后的一段时间内发出 NAT 探测包，本次 NAT 探测结束。
            logger.Warn("We do not received any NAT test packet after the client requested one. NAT test is aborted.");
            return;
        }

        if (NatTestUdpPacket.TryParse(receivedPacket) is not { AlternateServer: { } alternateServer })
        {
            // 收到的数据包不符合要求，丢弃，并放弃本次 NAT 探测。
            logger.Warn("Client NAT test packet is not valid. NAT test is aborted.");
            return;
        }

        await httpClient.PostAsJsonAsync($"https://{alternateServer}/api/v1/nat-test/forward", new NatTestForwardRequest
        {
            SessionId = sessionId,
            Address = remoteEndPoint.Address.ToString(),
            Port = remoteEndPoint.Port,
        }, TransportJsonContext.Default.NatTestForwardRequest, cancellationToken: cancellationToken);

        using var packetMemory = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase1RMainServerReply,
            SessionId = sessionId,
            ClientPublicIPEndPoint = remoteEndPoint.ToString(),
            AlternateServerPort1 = port1,
            AlternateServerPort2 = port2,
        }.ToUdpPacket().ToPacketData(out var packetLength);

        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory.Memory[..packetLength], remoteEndPoint, cancellationToken);
            await udpClient2.SendAsync(packetMemory.Memory[..packetLength], remoteEndPoint, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }
    }

    public static async Task ServerFilteringAndMappingPhaseAsync(
        ILogger logger,
        string sessionId, string clientPublicAddress, int clientPublicPort, int port1, int port2,
        CancellationToken cancellationToken)
    {
        using var udpClient1 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient1.Client.DualMode = true;
        udpClient1.Client.Bind(new IPEndPoint(IPAddress.Any, port1));

        using var udpClient2 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient2.Client.DualMode = true;
        udpClient2.Client.Bind(new IPEndPoint(IPAddress.Any, port2));

        // 第 1.2 轮
        var clientPublicEndPoint = new IPEndPoint(IPAddress.Parse(clientPublicAddress), clientPublicPort);
        using var packetMemory1 = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase12AlternateServerSend,
            SessionId = sessionId,
            ClientPublicIPEndPoint = clientPublicEndPoint.ToString(),
        }.ToUdpPacket().ToPacketData(out var packetLength1);

        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory1.Memory[..packetLength1], clientPublicEndPoint, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }

        // 第 2.2 轮
        var receivedPackets = await udpClient1.ReceiveUtilAllMatches(
            TimeSpan.FromMinutes(2), cancellationToken,
            (_, p) =>
                p.Payload.GetValueOrDefault(nameof(NatTestSession.SessionId)) == sessionId
                && NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase2SClientSend);
        if (receivedPackets[0] is not ({ } remoteEndPoint2, _))
        {
            // 客户端未在 NAT 测试第 1 轮的回包之后发出新轮的 NAT 探测包，本次 NAT 探测结束。
            logger.Warn("We do not received any NAT test packet after the client requested one. NAT test is aborted.");
            return;
        }

        using var packetMemory2 = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase2RAlternateServerSend,
            SessionId = sessionId,
            ClientPublicIPEndPoint = remoteEndPoint2.ToString(),
        }.ToUdpPacket().ToPacketData(out var packetLength2);

        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory2.Memory[..packetLength2], remoteEndPoint2, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }

        // 第 3.2 轮（可选）
        var receivedPackets3 = await udpClient1.ReceiveUtilAllMatches(
            TimeSpan.FromMinutes(2), cancellationToken,
            (_, p) =>
                p.Payload.GetValueOrDefault(nameof(NatTestSession.SessionId)) == sessionId
                && NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase3SClientSend);
        if (receivedPackets3[0] is not ({ } remoteEndPoint3, _))
        {
            // 客户端未在 NAT 测试第 2 轮的回包之后发出新轮的 NAT 探测包，本次 NAT 探测结束。
            logger.Info("We do not received any NAT test packet after the client requested one. NAT test is completed.");
            return;
        }

        using var packetMemory3 = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase3RAlternateServerSend,
            SessionId = sessionId,
            ClientPublicIPEndPoint = remoteEndPoint3.ToString(),
        }.ToUdpPacket().ToPacketData(out var packetLength3);

        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory3.Memory[..packetLength3], remoteEndPoint3, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }
    }
}
