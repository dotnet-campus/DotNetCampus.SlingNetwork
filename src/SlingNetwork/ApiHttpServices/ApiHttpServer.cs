using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace SlingNetwork.ApiHttpServices;

[Router("/api/v1/punch")]
public partial class ApiHttpServer : SingletonRpcServer
{
    [WebApi(Method = HttpMethodType.Get)]
    public PunchInfo Sum(string publicKey)
    {
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
