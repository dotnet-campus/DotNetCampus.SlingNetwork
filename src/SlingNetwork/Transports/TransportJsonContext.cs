using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.SlingNetwork.ServerSide.ControlServices;
using DotNetCampus.SlingNetwork.Transports.Models;

namespace DotNetCampus.SlingNetwork.Transports;

[JsonSerializable(typeof(NatTestSession))]
[JsonSerializable(typeof(PunchInfo))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
public partial class TransportJsonContext : JsonSerializerContext
{
    static TransportJsonContext()
    {
        Default = new TransportJsonContext(new JsonSerializerOptions(s_defaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
