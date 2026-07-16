using DotNetCampus.SlingNetwork.Cli;
using DotNetCampus.SlingNetwork.Transports.Models;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class NatTestWebApi(ServeHandler serverInfo) : SingletonRpcServer
{
    [Router("/api/v1/nat-test/new")]
    [WebApi(Method = HttpMethodType.Post)]
    public NatTestSession NatTest(IWebApiCallContext context)
    {
        var response = context.HttpContext.Response;

        if (serverInfo.PartnerControlUrls is null or [])
        {
            response.SetStatus(404, "Not Supported");
            response.SetContent("This control server does not support NAT test.");
            response.AnswerAsync();
            return null!;
        }

        Span<int> portPair = stackalloc int[2];
        serverInfo.UdpPortRange.RandomTo(portPair);
        return new NatTestSession
        {
            SessionId = Guid.NewGuid().ToString("D"),
            Port1 = portPair[0],
            Port2 = portPair[1],
            AlternateServerList = serverInfo.PartnerControlUrls,
        };
    }

    [Router("/api/v1/nat-test/forward")]
    [WebApi(Method = HttpMethodType.Post)]
    public NatTestSession Forward(IWebApiCallContext context, NatTestForwardRequest request)
    {
        Span<int> portPair = stackalloc int[2];
        serverInfo.UdpPortRange.RandomTo(portPair);
        return new NatTestSession
        {
            SessionId = request.SessionId,
            Port1 = portPair[0],
            Port2 = portPair[1],
            AlternateServerList = serverInfo.PartnerControlUrls,
        };
    }
}

public record NatTestForwardRequest
{
    public required string SessionId { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }
}
