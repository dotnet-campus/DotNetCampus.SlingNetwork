using System.Net;

namespace DotNetCampus.SlingNetwork.Transports;

public static class IPEndPointExtensions
{
    public static IPAddress Normalize(this IPAddress address)
    {
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
    }

    public static IPEndPoint Normalize(this IPEndPoint endPoint)
    {
        var normalizedAddress = endPoint.Address.Normalize();
        return Equals(normalizedAddress, endPoint.Address)
            ? endPoint
            : new IPEndPoint(normalizedAddress, endPoint.Port);
    }
}
