using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class PunchWebApi(ServerContext serverContext) : SingletonRpcServer
{
    [Router("/api/v1/punch")]
    [WebApi(Method = HttpMethodType.Get)]
    public PunchInfo Punch(IWebApiCallContext context, string publicKey)
    {
        return new PunchInfo
        {
            Hosts = serverContext.UdpInfo.Hosts,
            UdpPort = serverContext.UdpInfo.Port,
        };
    }
}

public record PunchInfo
{
    public required IReadOnlyList<string> Hosts { get; init; }

    public int UdpPort { get; init; }
}
