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

        var serverAddresses = ConnectUrls.Select(IPAddress.Parse).ToList();
        var punchPort = PunchPort is { } punchPortArgument
                        && int.TryParse(punchPortArgument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;

        var client = new UdpClient(0);
        var receiveTask = Task.Run(() => Receive(app.Logger, client, serverAddresses));
        var sendTask = Task.Run(() => SendPackets(app.Logger, client, serverAddresses, punchPort));

        await Task.WhenAll(receiveTask, sendTask);
        return 0;
    }

    private void SendPackets(ILogger logger, UdpClient client, IReadOnlyList<IPAddress> serverAddresses, int punchPort)
    {
        var peerIdentity = PeerIdentity.LoadOrCreate();

        while (true)
        {
            foreach (var serverAddress in serverAddresses)
            {
                var punchInfo = new PeerPunchInfo
                {
                    Peer = PeerName,
                    Group = "69de76b3-9f89-4731-89f5-9859fff379fc",
                    PublicKey = peerIdentity.PublicKey,
                };
                var message = $"[Punch] {punchInfo}";
                var packet = UdpPacketCrypto.Encrypt(message);

                var ipEndPoint = new IPEndPoint(serverAddress, punchPort);
                logger.Info($"[Punch] Sending to [{ipEndPoint}]({packet.Length}): {message}");
                client.Send(packet, ipEndPoint);
            }

            Thread.Sleep(5000);
        }
    }

    private void Receive(ILogger logger, UdpClient client, List<IPAddress> serverAddresses)
    {
        while (true)
        {
            IPEndPoint? ipEndPoint = null;
            var packet = client.Receive(ref ipEndPoint);
            if (!UdpPacketCrypto.TryDecrypt(packet, out var message))
            {
                continue;
            }

            logger.Info($"[Punch] Received from [{ipEndPoint}]({packet.Length}): {message}");
        }
    }
}
