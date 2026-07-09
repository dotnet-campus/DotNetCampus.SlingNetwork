using DotNetCampus.Logging;
using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.Services.ApiHttpServices;

[Router("/api/v1/punch")]
public partial class ApiHttpServer(ILogger logger) : SingletonRpcServer
{
    [WebApi(Method = HttpMethodType.Get)]
    public PunchInfo Sum(string publicKey)
    {
        logger.Trace($"[Http] /api/v1/punch: publicKey={publicKey}");
        return new PunchInfo
        {
            PublicKey = publicKey,
        };
    }
}

public record PunchInfo
{
    public string? PublicKey { get; init; }
}
