using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Services.PunchServices;
using DotNetCampus.SlingNetwork.Services.Security;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("connect", Description = "Command.ConnectHandler.Description")]
public class ConnectHandler : ICommandHandler<AppContext>
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ConnectHandler.ConnectUrls")]
    public IReadOnlyList<string> ConnectUrls { get; init; } = null!;

    [Option('p', "punch-port", ValueName = "number", Description = "Command.ConnectHandler.PunchPort")]
    public string? PunchPort { get; init; }

    [Option('n', "peer-name", Description = "Command.ConnectHandler.PeerName")]
    public required string PeerName { get; init; }

    public async Task<int> RunAsync(AppContext app)
    {
        if (ConnectUrls.Count is 0)
        {
            app.Logger.Error("[Punch] At least one ip-address is required. eg. 127.0.0.1");
            return -1;
        }
        var punchPort = PunchPort is { } punchPortArgument
                        && int.TryParse(punchPortArgument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;

        var serverEndpoints = ConnectUrls
            .Select(IPAddress.Parse)
            .Select(ip => new IPEndPoint(ip, punchPort))
            .ToList();

        using var cts = new CancellationTokenSource();
        using var udp = new UdpClient(0);

        var receiveTask = ReceiveLoop(app.Logger, udp, cts.Token);
        var sendTask = SendLoop(app.Logger, udp, serverEndpoints, cts.Token);

        await Task.WhenAll(receiveTask, sendTask);
        return 0;
    }

    private async Task SendLoop(ILogger logger, UdpClient udp, IReadOnlyList<IPEndPoint> servers, CancellationToken ct)
    {
        var peerIdentity = PeerIdentity.LoadOrCreate();

        while (!ct.IsCancellationRequested)
        {
            foreach (var server in servers)
            {
                var punchInfo = new PeerPunchInfo
                {
                    Peer = PeerName,
                    Group = "69de76b3-9f89-4731-89f5-9859fff379fc",
                    PublicKey = peerIdentity.PublicKey,
                };

                var message = punchInfo.ToString();
                var packet = UdpPacketCrypto.Encrypt(message);

                await udp.SendAsync(packet, server, ct);

                logger.Info($"[Punch] Sent to [{server}]({packet.Length})");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task ReceiveLoop(ILogger logger, UdpClient udp, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await udp.ReceiveAsync(ct);
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

            logger.Info($"[Punch] Received from [{remote}]({packet.Length}): {message}");
        }
    }
}
