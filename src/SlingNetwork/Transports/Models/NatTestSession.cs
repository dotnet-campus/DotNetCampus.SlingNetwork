using System.Text.Json.Serialization;

namespace DotNetCampus.SlingNetwork.Transports.Models;

public record NatTestSession
{
    public required string SessionId { get; init; }

    public required int Port1 { get; init; }

    public required int Port2 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AlternateServerList { get; init; }
}