using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Cli.Exceptions;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Localizations;
using DotNetCampus.SlingNetwork.ServerSide;
using DotNetCampus.SlingNetwork.ServerSide.ControlServices;
using DotNetCampus.SlingNetwork.ServerSide.Models;
using DotNetCampus.SlingNetwork.ServerSide.UdpServices;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("serve", Description = "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler<AppContext>
{
    [Option('l', "listen", ValueName = "ip:port", Description = "Command.ServeHandler.ControlListenEndPoints")]
    public IReadOnlyList<string>? ControlListenEndPoints { get; init; }

    [Option('a', "admin-listen", ValueName = "ip:port", Description = "Command.ServeHandler.AdminListenEndPoints")]
    public IReadOnlyList<string>? AdminListenEndPoints { get; init; }

    [Option('b', "partner-control-url", ValueName = "url", Description = "Command.ServeHandler.PartnerControlUrls")]
    public IReadOnlyList<string>? PartnerControlUrls { get; init; }

    [Option("public-udp-host", Description = "Command.ServeHandler.UdpHosts")]
    public IReadOnlyList<string>? PublicUdpHosts { get; init; }

    [Option('p', "udp-port-range", ValueName = "port|port1-port2", Description = "Command.ServeHandler.UdpPortRange")]
    public string? RawUdpPortRange
    {
        get => UdpPortRange.ToString();
        init => UdpPortRange = value is not null
            ? PortRange.TryParse(value, out var parsedPortRange)
                ? parsedPortRange
                : throw new CommandLineParseValueException(LocalizedText.Current.Command.ParseValueException.PortRange.ToString(value))
            : new PortRange(50000);
    }

    public PortRange UdpPortRange { get; private init; } = new PortRange(50000);

    public async Task<int> RunAsync(AppContext app)
    {
        var udpPort = UdpPortRange.Random();
        var serverContext = new ServerContext
        {
            App = app,
            UdpInfo = new ServerUdpInfo
            {
                PortRange = UdpPortRange,
                Hosts = PublicUdpHosts ?? [],
                Port = udpPort,
            },
        };

        if (UdpPortRange.Count < 2)
        {
            app.Logger.Warn("At least two UDP port is required for NAT test usage.");
        }

        // 初始化服务。
        var punchService = new UdpPacketServer(serverContext);
        var apiHttpService = new ControlHttpServer(serverContext, this);

        // API 服务（http）。
        var signalingServiceTask = apiHttpService.Listen();

        // 打洞服务（udp）。
        var punchServiceTask = punchService.Listen();

        // 等待服务结束。
        await Task.WhenAll(signalingServiceTask, punchServiceTask);
        return 0;
    }
}
