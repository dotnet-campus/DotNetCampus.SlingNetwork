using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SlingNetwork.Services.ApiHttpServices;

namespace SlingNetwork.Services;

[JsonSerializable(typeof(PunchInfo))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
    static AppJsonSerializerContext()
    {
        Default = new AppJsonSerializerContext(new JsonSerializerOptions(s_defaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
