using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.WebApi;

namespace DotNetCampus.SlingNetwork.Services.ApiHttpServices;

[Router("ip")]
public class IPHttpServer : SingletonRpcServer
{
    [WebApi(Method = HttpMethodType.Get)]
    public string IP(IWebApiCallContext context)
    {
        if (context.Caller is IHttpSessionClient httpSessionClient)
        {
            return httpSessionClient.IP;
        }

        context.HttpContext.Response.StatusCode = 400;
        return "Cannot resolve client IP.";
    }
}
