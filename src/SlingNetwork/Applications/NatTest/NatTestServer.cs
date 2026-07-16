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
#if DEBUG
    private const int UdpPacketRepeatCount = 1;
#else
    private const int UdpPacketRepeatCount = 4;
#endif
    private static readonly TimeSpan UdpPacketRepleatDelay = TimeSpan.FromMilliseconds(500);

    public static async Task ServerFilteringPhaseAsync(
        HttpClient httpClient, ILogger logger,
        string sessionId, int port1, int port2,
        CancellationToken cancellationToken)
    {
        using var udpClient1 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient1.Client.DualMode = true;
        udpClient1.Client.Bind(new IPEndPoint(IPAddress.Any, port1));
        var receiver1 = new UdpPacketReceiver(udpClient1, logger, $"[NAT-TEST][{sessionId}]", TimeSpan.FromMinutes(2));

        using var udpClient2 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient2.Client.DualMode = true;
        udpClient2.Client.Bind(new IPEndPoint(IPAddress.Any, port2));

        // 第 1.2 轮
        var receivedPackets = await receiver1.ReceiveUtilAllMatches(cancellationToken,
            (_, p) =>
                p.Payload.GetValueOrDefault(nameof(NatTestSession.SessionId)) == sessionId
                && NatTestUdpPacketHeader.ParseFromHeader(p.Header) is NatTestUdpPacketHeader.Phase1SClientSend);
        if (receivedPackets[0] is not ({ } remoteEndPoint, { } receivedPacket))
        {
            // 客户端未在 NAT 测试请求后的一段时间内发出 NAT 探测包，本次 NAT 探测结束。
            logger.Warn("We do not received any NAT test packet after the client requested one. NAT test is aborted.");
            return;
        }

        if (NatTestUdpPacket.TryParse(receivedPacket) is not { AlternateServerUrl: { } alternateServerUrl })
        {
            // 收到的数据包不符合要求，丢弃，并放弃本次 NAT 探测。
            logger.Warn("Client NAT test packet is not valid. NAT test is aborted.");
            return;
        }

        var httpResponse = await httpClient.PostAsJsonAsync($"{alternateServerUrl}/api/v1/nat-test/forward", new NatTestForwardRequest
        {
            SessionId = sessionId,
            Address = remoteEndPoint.Address.ToString(),
            Port = remoteEndPoint.Port,
        }, TransportJsonContext.Default.NatTestForwardRequest, cancellationToken: cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            // 备用服务器未按预期工作，放弃本次 NAT 探测。
            var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            logger.Warn($"""
                    Partner control server does not work correctly. NAT test is aborted.
                    Code: {httpResponse.StatusCode}({(int)httpResponse.StatusCode}) Response: {content}"
                    """);
            return;
        }
        var response = await httpResponse.Content.ReadFromJsonAsync(TransportJsonContext.Default.NatTestSession, cancellationToken);

        using var packetMemoryR = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase1RMainServerReply,
            SessionId = sessionId,
            ClientPublicIPEndPoint = remoteEndPoint.ToString(),
            AlternateServerPort1 = response!.Port1,
            AlternateServerPort2 = response.Port2,
        }.ToUdpPacket().ToPacketData(out var packetLengthR);

        using var packetMemoryS = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase11MainServerSend,
            SessionId = sessionId,
            ClientPublicIPEndPoint = remoteEndPoint.ToString(),
        }.ToUdpPacket().ToPacketData(out var packetLengthS);

        logger.Info($"[NAT-TEST][{sessionId}] UDP {NatTestUdpPacketHeader.Phase1RMainServerReply.ToHeaderString()} to {remoteEndPoint}");
        logger.Info($"[NAT-TEST][{sessionId}] UDP {NatTestUdpPacketHeader.Phase11MainServerSend.ToHeaderString()} to {remoteEndPoint}");
        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemoryR.Memory[..packetLengthR], remoteEndPoint, cancellationToken);
            await udpClient2.SendAsync(packetMemoryS.Memory[..packetLengthS], remoteEndPoint, cancellationToken);
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
        var receiver1 = new UdpPacketReceiver(udpClient1, logger, $"[NAT-TEST][{sessionId}]", TimeSpan.FromMinutes(2));

        using var udpClient2 = new UdpClient(AddressFamily.InterNetworkV6);
        udpClient2.Client.DualMode = true;
        udpClient2.Client.Bind(new IPEndPoint(IPAddress.Any, port2));
        var receiver2 = new UdpPacketReceiver(udpClient2, logger, $"[NAT-TEST][{sessionId}]", TimeSpan.FromMinutes(2));

        // 第 1.2 轮
        var clientPublicEndPoint = new IPEndPoint(IPAddress.Parse(clientPublicAddress), clientPublicPort);
        using var packetMemory1 = new NatTestUdpPacket
        {
            Header = NatTestUdpPacketHeader.Phase12AlternateServerSend,
            SessionId = sessionId,
            ClientPublicIPEndPoint = clientPublicEndPoint.ToString(),
        }.ToUdpPacket().ToPacketData(out var packetLength1);

        logger.Info($"[NAT-TEST][{sessionId}] UDP {NatTestUdpPacketHeader.Phase12AlternateServerSend.ToHeaderString()} to {clientPublicEndPoint}");
        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory1.Memory[..packetLength1], clientPublicEndPoint, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }

        // 第 2.2 轮
        var receivedPackets = await receiver1.ReceiveUtilAllMatches(cancellationToken,
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

        logger.Info($"[NAT-TEST][{sessionId}] UDP {NatTestUdpPacketHeader.Phase2RAlternateServerSend.ToHeaderString()} to {remoteEndPoint2}");
        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory2.Memory[..packetLength2], remoteEndPoint2, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }

        // 第 3.2 轮（可选）
        var receivedPackets3 = await receiver2.ReceiveUtilAllMatches(cancellationToken,
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

        logger.Info($"[NAT-TEST][{sessionId}] UDP {NatTestUdpPacketHeader.Phase3RAlternateServerSend.ToHeaderString()} to {remoteEndPoint3}");
        for (var i = 0; i < UdpPacketRepeatCount; i++)
        {
            await udpClient1.SendAsync(packetMemory3.Memory[..packetLength3], remoteEndPoint3, cancellationToken);
            await Task.Delay(UdpPacketRepleatDelay, cancellationToken);
        }
    }
}
