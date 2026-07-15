using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Models;

namespace DotNetCampus.SlingNetwork.Services;

public class ServerContext
{
    public required AppContext App { get; init; }

    public required ServerUdpInfo UdpInfo { get; init; }

    public ILogger Logger => App.Logger;
}

public record ServerUdpInfo
{
    public required PortRange PortRange { get; init; }

    public required IReadOnlyList<string> Hosts { get; init; }

    public required int Port { get; init; }
}
