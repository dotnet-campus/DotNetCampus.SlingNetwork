using System.Net;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class IPWebApi : SingletonRpcServer
{
    [Router("ip")]
    [WebApi(Method = HttpMethodType.Get)]
    public void IP(IWebApiCallContext context)
    {
        var response = context.HttpContext.Response;

        if (context.GetClientIP() is { } ipAddress)
        {
            response.SetStatus(200, "Success");
            response.SetContent(ipAddress.ToString());
        }
        else
        {
            response.SetStatus(400, "Error");
            response.SetContent("This server cannot resolve client IP.");
        }

        response.AnswerAsync();
    }
}

public static class IPWebApiExtensions
{
    public static IPAddress? GetClientIP(this IWebApiCallContext context)
    {
        // 当被 Nginx 代理后，需要通过 HTTP 头获取真实客户端 IP
        var rawIP = context.HttpContext.Request.Headers
            .Get("X-Real-IP")
            .First;

        if (IPAddress.TryParse(rawIP, out var forwardedIP))
        {
            return forwardedIP;
        }

        // 其他情况，视请求 IP 为客户端 IP
        if (context.Caller is IHttpSessionClient httpSessionClient)
        {
            return IPAddress.Parse(httpSessionClient.IP);
        }

        return null;
    }
}
