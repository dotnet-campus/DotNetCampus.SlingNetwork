using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace SlingNetwork.Cli;

[Command("serve", Description = "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ServeHandler.ListenUrls")]
    public IReadOnlyList<string> ListenUrls { get; set; } = null!;

    [Option("admin-url", ValueName = "url", Description = "Command.ServeHandler.AdminListenUrls")]
    public IReadOnlyList<string> AdminListenUrls { get; set; } = null!;

    [Option('p', "punch-port-range", ValueName = "number", Description = "Command.ServeHandler.PunchPortRange")]
    public string? PunchPortRange { get; set; }

    public Task<int> RunAsync()
    {
        var punchPort = PunchPortRange is { } punchPortRange
                        && int.TryParse(punchPortRange, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;

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
