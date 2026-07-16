using System.Net;
using System.Net.Sockets;
using DotNetCampus.Logging;

namespace DotNetCampus.SlingNetwork.Transports.Security;

internal class UdpPacketReceiver(UdpClient udpClient, ILogger logger, string loggerTag, TimeSpan timeout)
{
    public async Task<(IPEndPoint RemoteEndPoint, UdpHeaderedKeyValuePacket UdpPacket)?[]> ReceiveUtilAllMatches(
        CancellationToken cancellationToken,
        params Func<IPEndPoint, UdpHeaderedKeyValuePacket, bool>[] udpPacketMatchers)
    {
        var selfCts = new CancellationTokenSource();
        var timeoutCts = new CancellationTokenSource(timeout);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, selfCts.Token, timeoutCts.Token);
        var resultPackets = new (IPEndPoint RemoteEndPoint, UdpHeaderedKeyValuePacket UdpPacket)?[udpPacketMatchers.Length];

        while (!linkedCts.Token.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await udpClient.ReceiveAsync(linkedCts.Token);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue;
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            var remoteEndPoint = result.RemoteEndPoint;
            Memory<byte> receivedPacketMemory = result.Buffer;

            var receivedPacketValue = UdpHeaderedKeyValuePacket.TryParse(receivedPacketMemory.Span);
            if (receivedPacketValue is not { } receivedPacket)
            {
                continue;
            }

            logger.Info($"{loggerTag} UDP {receivedPacket.Header} from {remoteEndPoint}");
            for (var i = 0; i < udpPacketMatchers.Length; i++)
            {
                var matcher = udpPacketMatchers[i];
                if (matcher(remoteEndPoint, receivedPacket))
                {
                    resultPackets[i] = (remoteEndPoint, receivedPacket);
                }
            }

            if (resultPackets.All(x => x != null))
            {
                await selfCts.CancelAsync();
            }
        }

        return resultPackets;
    }
}
