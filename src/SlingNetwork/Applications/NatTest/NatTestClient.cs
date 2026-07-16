using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DotNetCampus.SlingNetwork.Framework;
using DotNetCampus.SlingNetwork.Transports;
using DotNetCampus.SlingNetwork.Transports.Models;

namespace DotNetCampus.SlingNetwork.Applications.NatTest;

public class NatTestClient(AppContext app)
{
    public async Task<Result<NatTestClientSession>> CreateNewAsync(string controlUrl, IpProtocol ipProtocol = IpProtocol.IPv4)
    {
        var addressFamily = ipProtocol switch
        {
            IpProtocol.IPv4 => AddressFamily.InterNetwork,
            IpProtocol.IPv6 => AddressFamily.InterNetworkV6,
            _ => throw new InvalidEnumArgumentException($"Unknown {nameof(IpProtocol)}: {ipProtocol}", (int)ipProtocol, typeof(IpProtocol)),
        };

        var responseMessage = await app.HttpClient.PostAsync($"{controlUrl}/api/v1/nat-test/new", null);
        if (!responseMessage.IsSuccessStatusCode)
        {
            return Result.Failed($"Control server {controlUrl} is not available.");
        }
        var natTestSession = await responseMessage.Content.ReadFromJsonAsync(TransportJsonContext.Default.NatTestSession);
        if (natTestSession is null)
        {
            return Result.Failed($"Control server {controlUrl} nat test request failed.");
        }

        var server1Addresses = await LookupIpAddressesAsync([(controlUrl, new Uri(controlUrl).Host)]);
        var server1Address = server1Addresses.FirstOrDefault(x => x.IP.AddressFamily == addressFamily).IP;
        var server2Addresses = await LookupIpAddressesAsync(natTestSession.AlternateServerList?.Select(x => (x, new Uri(x).Host)).ToList() ?? []);
        var server2HostIP = server2Addresses.FirstOrDefault(x => x.IP.AddressFamily == addressFamily);

        if (server1Address is null)
        {
            return Result.Failed($"Control server {controlUrl} is not a {ipProtocol} control server.");
        }
        if (server2HostIP.IP is null)
        {
            return Result.Failed($"There is no alternate control server for {ipProtocol}.");
        }

        return Result.From(new NatTestClientSession
        {
            SessionId = natTestSession.SessionId,
            Server1Address = server1Address,
            Server1Port1 = natTestSession.Port1,
            Server1Port2 = natTestSession.Port2,
            Server2Url = server2HostIP.Url,
            Server2Address = server2HostIP.IP,
            Logger = app.Logger,
        });
    }

    private async Task<IReadOnlyList<(string Url, string Host, IPAddress IP)>> LookupIpAddressesAsync(IReadOnlyList<(string Url, string Host)> sources)
    {
        var result = new List<(string, string, IPAddress)>();
        var ipAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (url, host) in sources)
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            foreach (var address in addresses)
            {
                if (ipAddresses.Add(address.ToString()))
                {
                    result.Add((url, host, address));
                }
            }
        }
        return result;
    }
}
