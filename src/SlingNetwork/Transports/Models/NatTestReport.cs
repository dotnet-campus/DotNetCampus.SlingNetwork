using System.Net;

namespace DotNetCampus.SlingNetwork.Transports.Models;

public record NatTestReport
{
    public required bool? Success { get; init; }
    public required string SessionId { get; init; }
    public required NatMappingBehavior Mapping { get; init; }
    public required NetworkPacketFilteringBehavior Filtering { get; init; }
    public required IPEndPoint LocalEndPoint { get; init; }
    public required IPEndPoint PublicEndPoint { get; init; }
    public required IPEndPoint AlternateServerPort1PublicEndPoint { get; init; }
    public required IPEndPoint AlternateServerPort2PublicEndPoint { get; init; }
    public bool IsPublicEndPoint => Equals(LocalEndPoint.Address, PublicEndPoint.Address);

    public static NatTestReport Empty { get; } = new NatTestReport
    {
        Success = null,
        SessionId = "",
        Mapping = NatMappingBehavior.EndpointIndependent,
        Filtering = NetworkPacketFilteringBehavior.EndpointIndependent,
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
        PublicEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
        AlternateServerPort1PublicEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
        AlternateServerPort2PublicEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
    };
}
