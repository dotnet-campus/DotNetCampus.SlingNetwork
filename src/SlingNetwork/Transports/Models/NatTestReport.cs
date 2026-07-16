using System.Net;

namespace DotNetCampus.SlingNetwork.Transports.Models;

public record NatTestReport
{
    public required bool Success { get; init; }
    public required string SessionId { get; init; }
    public required NatMappingBehavior Mapping { get; init; }
    public required NetworkPacketFilteringBehavior Filtering { get; init; }
    public required int AlternateServerPort1 { get; init; }
    public required int AlternateServerPort2 { get; init; }
    public required IPEndPoint ClientLocalEndPoint { get; init; }
    public required IPEndPoint ClientPublicEndPoint { get; init; }
    public required IPEndPoint ClientPublicEndPointToAlternateServerPort1 { get; init; }
    public required IPEndPoint? ClientPublicEndPointToAlternateServerPort2 { get; init; }
    public bool IsPublicEndPoint => Equals(
        ClientLocalEndPoint.Address.Normalize(),
        ClientPublicEndPoint.Address.Normalize());

    public static NatTestReport Empty { get; } = new NatTestReport
    {
        Success = false,
        SessionId = "",
        Mapping = NatMappingBehavior.EndpointIndependent,
        Filtering = NetworkPacketFilteringBehavior.EndpointIndependent,
        AlternateServerPort1 = 0,
        AlternateServerPort2 = 0,
        ClientLocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
        ClientPublicEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
        ClientPublicEndPointToAlternateServerPort1 = new IPEndPoint(IPAddress.Loopback, 0),
        ClientPublicEndPointToAlternateServerPort2 = null,
    };
}
