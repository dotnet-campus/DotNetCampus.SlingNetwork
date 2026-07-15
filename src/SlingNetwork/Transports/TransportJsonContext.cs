using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.SlingNetwork.Transports.Models;

namespace DotNetCampus.SlingNetwork.Transports;

[JsonSerializable(typeof(NatTestSession))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
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
