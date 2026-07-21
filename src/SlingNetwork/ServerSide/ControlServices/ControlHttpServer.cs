using System.Net;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Cli;
using DotNetCampus.SlingNetwork.Transports;
using TouchSocket.Core;
using TouchSocket.Http;
using TouchSocket.Rpc;
using TouchSocket.Sockets;
using HttpClient = System.Net.Http.HttpClient;

namespace DotNetCampus.SlingNetwork.ServerSide.ControlServices;

public class ControlHttpServer(ServerContext context, ServeHandler serverInfo)
{
    public async Task Listen()
    {
        IPHost[] ipHosts = serverInfo.ControlListenEndPoints switch
        {
            null or [] => [new IPHost(IPAddress.Any, 80), new IPHost(IPAddress.IPv6Any, 80)],
            string[] array => IPHost.ParseIPHosts(array),
            _ => serverInfo.ControlListenEndPoints.Select(x => IPHost.ParseIPHosts([x])[0]).ToArray(),
        };

        var service = new HttpService();
        await service.SetupAsync(new TouchSocketConfig()
            .SetListenIPHosts(ipHosts)
            .ConfigureContainer(a =>
            {
                // a.AddConsoleLogger();
                a.RegisterSingleton<ServerContext>(_ => context);
                a.RegisterSingleton<ILogger>(_ => context.App.Logger);
                a.RegisterSingleton<HttpClient>(_ => context.App.HttpClient);
                a.RegisterSingleton<ServeHandler>(_ => serverInfo);
                a.AddRpcStore(store =>
                {
                    store.RegisterServer<IPWebApi>();
                    store.RegisterServer<NatTestWebApi>();
                    store.RegisterServer<PunchWebApi>();
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
                            serializerOptions.TypeInfoResolverChain.Insert(0, TransportJsonContext.Default);
                        });
                    });
                });
                a.UseDefaultHttpServicePlugin();
            }));
        await service.StartAsync();

        context.App.Logger.Debug($"Listening: http://[::]:{ipHosts[0].Port}");
    }
}
