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
        portPair[0] = request.SuggestedServerPort1;
        portPair[1] = request.SuggestedServerPort2;
        serverInfo.UdpPortRange.RandomTo(portPair);

        logger.Info($"[NAT-TEST][{request.SessionId}] HTTP forwarded from {context.GetClientIP()}");
        NatTestServer.ServerFilteringAndMappingPhaseAsync(logger,
                request.SessionId, request.ClientPublicAddress, request.ClientPublicPort, portPair[0], portPair[1],
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

/// <summary>
/// 控制服务器向备用控制服务器请求协助 NAT 探测时的请求参数。
/// </summary>
public record NatTestForwardRequest
{
    /// <summary>
    /// 本次 NAT 探测的 Id。
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 客户端在公网上暴露的 IP。备用控制服务器应向此 IP 发送 NAT 探测数据包。
    /// </summary>
    public required string ClientPublicAddress { get; init; }

    /// <summary>
    /// 客户端在公网上暴露的端口。备用控制服务器应向此端口发送 NAT 探测数据包。
    /// </summary>
    public required int ClientPublicPort { get; init; }

    /// <summary>
    /// 建议备用服务器协助 NAT 探测时使用此端口作为 1 号端口。备用服务器应优先分配此端口协助探测。
    /// </summary>
    public required int SuggestedServerPort1 { get; init; }

    /// <summary>
    /// 建议备用服务器协助 NAT 探测时使用此端口作为 2 号端口。备用服务器应优先分配此端口协助探测。
    /// </summary>
    public required int SuggestedServerPort2 { get; init; }
}
