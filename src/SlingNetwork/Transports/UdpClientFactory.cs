using System.Net;
using System.Net.Sockets;

namespace DotNetCampus.SlingNetwork.Transports;

public static class UdpClientFactory
{
    private const int SioUdpConnReset = -1744830452; // IOC_IN | IOC_VENDOR | 12

    public static UdpClient CreateNew(int port)
    {
        var udp = new UdpClient(port);
        DisableUdpConnectionReset(udp);
        return udp;
    }

    public static UdpClient CreateNew(int port, AddressFamily family)
    {
        var udp = new UdpClient(port, family);
        DisableUdpConnectionReset(udp);
        return udp;
    }

    public static UdpClient CreateNew(IPEndPoint localEP)
    {
        var udp = new UdpClient(localEP);
        DisableUdpConnectionReset(udp);
        return udp;
    }

    private static void DisableUdpConnectionReset(UdpClient udpClient)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        udpClient.Client.IOControl(
            (IOControlCode)SioUdpConnReset,
            [0, 0, 0, 0],
            null);
    }
}
