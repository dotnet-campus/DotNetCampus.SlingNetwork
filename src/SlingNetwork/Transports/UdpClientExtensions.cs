using System.Net;
using System.Net.Sockets;

namespace DotNetCampus.SlingNetwork.Transports;

public static class UdpClientExtensions
{
    public static async Task RepeatSendAsync(this UdpClient ucpClient,
        ReadOnlyMemory<byte> datagram, IPEndPoint endPoint,
        int repeatCount, int repeatDelayMilliseconds,
        CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < repeatCount; i++)
        {
            await ucpClient.SendAsync(datagram, endPoint, cancellationToken);
            if (i != repeatCount - 1)
            {
                await Task.Delay(repeatDelayMilliseconds, cancellationToken);
            }
        }
    }
}
