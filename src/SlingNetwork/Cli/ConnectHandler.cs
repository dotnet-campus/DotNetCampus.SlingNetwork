using System.Net;
using System.Net.Http.Json;
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
    [Option('c', "control-url", ValueName = "url", Description = "Command.ConnectHandler.ControlUrl")]
    public required string ControlUrl { get; init; }

    [Option('n', "peer-name", Description = "Command.ConnectHandler.PeerName")]
    public required string PeerName { get; init; }

    public async Task<int> RunAsync(AppContext app)
    {
        var punchInfo = await app.HttpClient.GetFromJsonAsync($"{ControlUrl}/api/v1/punch", app.JsonSerializer.PunchInfo);
        if (punchInfo == null)
        {
            app.Logger.Error($"[Punch] Control server is not available.");
            return 1;
        }
        var ipAddresses = await LookupIpAddressesAsync([..punchInfo.Hosts, new Uri(ControlUrl).Host]);
        var ipv4 = ipAddresses.First(x => x.AddressFamily == AddressFamily.InterNetwork);

        var punchServer = new IPEndPoint(ipv4, punchInfo.UdpPort);

        using var cts = new CancellationTokenSource();
        using var udp = new UdpClient(0);

        var receiveTask = ReceiveLoop(app.Logger, udp, cts.Token);
        var sendTask = SendLoop(app.Logger, udp, punchServer, cts.Token);

        await Task.WhenAll(receiveTask, sendTask);
        return 0;
    }

    private async Task<IReadOnlyList<IPAddress>> LookupIpAddressesAsync(IReadOnlyList<string> hosts)
    {
        var result = new List<IPAddress>();
        var ipAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in hosts)
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            foreach (var address in addresses)
            {
                if (ipAddresses.Add(address.ToString()))
                {
                    result.Add(address);
                }
            }
        }
        return result;
    }

    private async Task SendLoop(ILogger logger, UdpClient udp, IPEndPoint punchServer, CancellationToken ct)
    {
        var peerIdentity = PeerIdentity.LoadOrCreate();

        while (!ct.IsCancellationRequested)
        {
            var punchInfo = new PeerPunchInfo
            {
                Peer = PeerName,
                Group = "69de76b3-9f89-4731-89f5-9859fff379fc",
                PublicKey = peerIdentity.PublicKey,
            };

            var message = punchInfo.ToString();
            var packet = UdpPacketCrypto.Encrypt(message);

            await udp.SendAsync(packet, punchServer, ct);

            logger.Info($"[Punch] Sent to [{punchServer}]({packet.Length})");

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
