using System.Globalization;

namespace DotNetCampus.SlingNetwork.ServerSide.Models;

public readonly record struct PortRange(ushort Min, ushort Max)
{
    public PortRange() : this(0, 65535)
    {
    }

    public PortRange(ushort port) : this(port, port)
    {
    }

    public int Count => Max - Min + 1;

    public static PortRange Parse(ReadOnlySpan<char> text)
    {
        var index = text.IndexOf('-');
        if (index < 0)
        {
            var port = ushort.Parse(text);
            return new PortRange(port);
        }
        var min = ushort.Parse(text[..index]);
        var max = ushort.Parse(text[(index + 1)..]);
        return new PortRange(min, max);
    }

    public static bool TryParse(ReadOnlySpan<char> text, out PortRange result)
    {
        var index = text.IndexOf('-');
        if (index < 0)
        {
            if (!ushort.TryParse(text, out var port))
            {
                result = default;
                return false;
            }
            result = new PortRange(port);
            return true;
        }
        if (!ushort.TryParse(text[..index], out var min) || !ushort.TryParse(text[(index + 1)..], out var max))
        {
            result = default;
            return false;
        }
        result = new PortRange(min, max);
        return true;
    }

    public override string ToString()
    {
        return Max == Min
            ? Min.ToString(CultureInfo.InvariantCulture)
            : $"{Min}-{Max}";
    }

    public int Random()
    {
        return System.Random.Shared.Next(Min, Max + 1);
    }

    /// <summary>
    /// 传入一组建议的端口号。如果任一端口号是 0，则为此端口号随机生成一个新的端口号，直到所有的端口号都不相同且不为 0。
    /// </summary>
    /// <param name="ports"></param>
    /// <returns></returns>
    public bool RandomTo(Span<int> ports)
    {
        var count = Max - Min + 1;
        if (count < ports.Length || ports.Length <= 0)
        {
            return false;
        }

        ports[0] = UseOrRandom(ports[0]);
        for (var i = 1; i < ports.Length; i++)
        {
            var port = UseOrRandom(ports[i]);
            while (ports[..i].Contains(port))
            {
                port = System.Random.Shared.Next(Min, Max + 1);
            }
            ports[i] = port;
        }
        return true;
    }

    private int UseOrRandom(int port)
    {
        if (port is 0 || Min > port || Max < port)
        {
            // 如果建议的端口号无法满足要求，则随机生成一个。
            return System.Random.Shared.Next(Min, Max + 1);
        }
        return port;
    }
}
