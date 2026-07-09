using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.SlingNetwork.Services.ApiHttpServices;

namespace DotNetCampus.SlingNetwork.Services;

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
