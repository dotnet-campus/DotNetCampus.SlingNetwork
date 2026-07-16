using System.Text.Json;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Applications.NatTest;
using DotNetCampus.SlingNetwork.Cli;
using DotNetCampus.SlingNetwork.Framework;
using DotNetCampus.SlingNetwork.Transports;
using DotNetCampus.SlingNetwork.Transports.Models;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;
using HttpClient = System.Net.Http.HttpClient;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class NatTestWebApi(ServeHandler serverInfo, HttpClient httpClient, ILogger logger) : SingletonRpcServer
{
    [Router("/api/v1/nat-test/new")]
    [WebApi(Method = HttpMethodType.Post)]
    public NatTestSession NatTest(IWebApiCallContext context,
        [FromQuery(Name = "repeat")] int packetRepeatCount = 1,
        [FromQuery(Name = "delay")] int packetRepeatDelayMilliseconds = 500)
    {
        var response = context.HttpContext.Response;

        if (serverInfo.PartnerControlUrls is null or [])
        {
            response.SetStatus(404, "Not Supported");
            response.SetContent("This control server does not support NAT test.");
            response.AnswerAsync();
            return null!;
        }

        var sessionId = Guid.NewGuid().ToString("D");
        Span<int> portPair = stackalloc int[2];
        serverInfo.UdpPortRange.RandomTo(portPair);

        logger.Info($"[NAT-TEST][{sessionId}] HTTP from {context.GetClientIP()}");
        NatTestServer.ServerFilteringPhaseAsync(httpClient, logger,
                sessionId, portPair[0], portPair[1],
                packetRepeatCount, packetRepeatDelayMilliseconds,
                CancellationToken.None)
            .LogAsyncException(logger);

        return new NatTestSession
        {
            SessionId = sessionId,
            Port1 = portPair[0],
            Port2 = portPair[1],
            AlternateServerList = serverInfo.PartnerControlUrls,
        };
    }

    [Router("/api/v1/nat-test/forward")]
    [WebApi(Method = HttpMethodType.Post)]
    public async Task<NatTestSession> Forward(IWebApiCallContext context /*, NatTestForwardRequest request*/,
        [FromQuery(Name = "repeat")] int packetRepeatCount = 1,
        [FromQuery(Name = "delay")] int packetRepeatDelayMilliseconds = 500)
    {
        var response = await context.HttpContext.Request.GetBodyAsync();
        var request = JsonSerializer.Deserialize(response, TransportJsonContext.Default.NatTestForwardRequest)!;

        Span<int> portPair = stackalloc int[2];
        serverInfo.UdpPortRange.RandomTo(portPair);

        logger.Info($"[NAT-TEST][{request.SessionId}] HTTP forwarded from {context.GetClientIP()}");
        NatTestServer.ServerFilteringAndMappingPhaseAsync(logger,
                request.SessionId, request.Address, request.Port, portPair[0], portPair[1],
                packetRepeatCount, packetRepeatDelayMilliseconds,
                CancellationToken.None)
            .LogAsyncException(logger);

        return new NatTestSession
        {
            SessionId = request.SessionId,
            Port1 = portPair[0],
            Port2 = portPair[1],
        };
    }
}

public record NatTestForwardRequest
{
    public required string SessionId { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }
}
