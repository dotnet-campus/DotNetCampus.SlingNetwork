using System.Net;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class IPWebApi : SingletonRpcServer
{
    [Router("ip")]
    [WebApi(Method = HttpMethodType.Get)]
    public string IP(IWebApiCallContext context)
    {
        // 当被 Nginx 代理后，需要通过 HTTP 头获取真实客户端 IP
        var rawIP = context.HttpContext.Request.Headers
            .Get("X-Real-IP")
            .First;

        if (IPAddress.TryParse(rawIP, out var forwardedIP))
        {
            return forwardedIP.ToString();
        }

        // 其他情况，视请求 IP 为客户端 IP
        if (context.Caller is IHttpSessionClient httpSessionClient)
        {
            return httpSessionClient.IP;
        }

        context.HttpContext.Response.StatusCode = 400;
        return "Cannot resolve client IP.";
    }
}
