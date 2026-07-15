using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using DotNetCampus.SlingNetwork.Cli;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;
using HttpClient = System.Net.Http.HttpClient;

namespace DotNetCampus.SlingNetwork.Services.ApiHttpServices;

// [Router("/api/v1/nat-test")]
// public class NatTestHttpServer(HttpClient httpClient, ServeHandler serverInfo) : SingletonRpcServer
// {
//     [WebApi(Method = HttpMethodType.Get)]
//     public async Task<NatTestUdpProbeTasks> NatTest(IWebApiCallContext context, string publicKey)
//     {
//         var response = context.HttpContext.Response;
//
//         if (serverInfo.PartnerControlUrls is null or [])
//         {
//             response.SetStatus(404, "Created");
//             response.SetContent("This control server does not support NAT test.");
//             return null!;
//         }
//
//         await TryInitializeSelfIPsAsync(serverInfo.PeerControlUrls);
//
//         if (SelfIPs is not { } selfIPs || (selfIPs.IPv4 is null && selfIPs.IPv6 is null))
//         {
//             response.SetStatus(404, "Created");
//             response.SetContent("This control server cannot do NAT test for this moment.");
//             return null!;
//         }
//
//         #region 这里没写完
//
//         return new NatTestUdpProbeTasks
//         {
//             IPv4Task =
//             [
//                 new NatTestUdpProbeTask
//                 {
//                     TargetIPEndpoint = IPEndPoint.Parse(serverInfo.Stun2Url), // 自己 stun 服务器的
//                     Utf8PacketPayload = "",
//                 },
//                 new NatTestUdpProbeTask
//                 {
//                     TargetIPEndpoint = IPEndPoint.Parse(serverInfo.Stun2Url), // 备用 stun 服务器的
//                     Utf8PacketPayload = "",
//                 },
//             ],
//         };
//
//         #endregion
//     }
// }
//
// public record NatTestUdpProbeTasks
// {
//     [JsonPropertyName("tasks")]
//     [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//     public required IReadOnlyList<NatTestUdpProbeTask>? Tasks { get; init; }
// }
//
// public record NatTestUdpProbeTask
// {
//     [JsonPropertyName("target")]
//     public string Foo { get; init; }
//
//     [JsonPropertyName("payload")]
//     public required string Utf8PacketPayload { get; init; }
// }
