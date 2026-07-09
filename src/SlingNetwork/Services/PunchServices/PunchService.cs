using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SlingNetwork.Services.PunchServices;

public class PunchService
{
    public async Task Listen(int punchPort)
    {
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
        {
            DualMode = true,
        };
        socket.Bind(new IPEndPoint(IPAddress.Any, punchPort));

        Span<byte> buffer = new byte[1024 * 2];
        while (true)
        {
            EndPoint remote = new IPEndPoint(IPAddress.IPv6Any, 0);
            var length = socket.ReceiveFrom(buffer, ref remote);
            HandleUdpPacket(buffer[..length], (IPEndPoint)remote);
        }
    }

    private void HandleUdpPacket(Span<byte> udpPacket, IPEndPoint remote)
    {
        var message = Encoding.UTF8.GetString(udpPacket);
        Console.WriteLine($"Received UDP Packet from [{remote.Address}:{remote.Port}]: {message}");
    }
}
