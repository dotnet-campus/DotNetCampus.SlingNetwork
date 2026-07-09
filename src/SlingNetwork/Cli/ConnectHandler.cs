using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("connect", Description = "Command.ConnectHandler.Description")]
public class ConnectHandler : ICommandHandler<AppContext>
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ConnectHandler.ConnectUrls")]
    public IReadOnlyList<string> ConnectUrls { get; set; } = null!;

    [Option('p', "punch-port", ValueName = "number", Description = "Command.ConnectHandler.PunchPort")]
    public string? PunchPort { get; set; }

    public Task<int> RunAsync(AppContext app)
    {
        var serverEndPoint = IPEndPoint.Parse(ConnectUrls[0]);
        var punchPort = PunchPort is { } punchPortArgument
                        && int.TryParse(punchPortArgument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;

        var client = new UdpClient(0);

        var message = "Hello"u8;
        Console.WriteLine($"Sending {message.Length} bytes");
        client.Send(message, serverEndPoint);

        return Task.FromResult(0);
    }
}
