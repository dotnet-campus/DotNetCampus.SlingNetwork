using TouchSocket.Core;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.Sockets;

namespace SlingNetwork.Services.ApiHttpServices;

public class ApiHttpService
{
    public async Task Listen(IReadOnlyList<string>? urls)
    {
        IPHost[] ipHosts = urls switch
        {
            null or [] => [new IPHost(80)],
            string[] array => IPHost.ParseIPHosts(array),
            _ => urls.Select(x => IPHost.ParseIPHosts([x])[0]).ToArray(),
        };

        var service = new HttpService();
        await service.SetupAsync(new TouchSocketConfig()
            .SetListenIPHosts(ipHosts)
            .ConfigureContainer(a =>
            {
                // a.AddConsoleLogger();
                a.AddRpcStore(store =>
                {
                    store.RegisterServer<ApiHttpServer>();
                });
            })
            .ConfigurePlugins(a =>
            {
                a.UseTcpSessionCheckClear();
                a.UseWebApi(webApiOptions =>
                {
                    webApiOptions.ConfigureConverter(converter =>
                    {
                        converter.AddSystemTextJsonSerializerFormatter(serializerOptions =>
                        {
                            serializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
                        });
                    });
                });
                a.UseDefaultHttpServicePlugin();
            }));
        await service.StartAsync();

        Console.WriteLine($"测试用地址: http://127.0.0.1:{ipHosts[0].Port}/api/v1/punch?publicKey=xxx");
    }
}
