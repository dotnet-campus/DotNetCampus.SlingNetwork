using System.Globalization;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using SlingNetwork.ApiHttpServices;
using SlingNetwork.PunchServices;

namespace SlingNetwork.Cli;

[Command("serve", Description = "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ServeHandler.ListenUrls")]
    public IReadOnlyList<string>? ListenUrls { get; set; }

    [Option("admin-url", ValueName = "url", Description = "Command.ServeHandler.AdminListenUrls")]
    public IReadOnlyList<string> AdminListenUrls { get; set; } = null!;

    [Option('p', "punch-port-range", ValueName = "number", Description = "Command.ServeHandler.PunchPortRange")]
    public string? PunchPortRange { get; set; }

    public async Task<int> RunAsync()
    {
        var signalingServiceTask = RunSignalingServiceAsync();
        var punchServiceTask = RunPunchServiceAsync();
        await Task.WhenAll(signalingServiceTask, punchServiceTask);
        return 0;
    }

    private Task RunSignalingServiceAsync()
    {
        return new ApiHttpService().Listen(ListenUrls);
    }

    private Task RunPunchServiceAsync()
    {
        var punchPort = PunchPortRange is { } punchPortRange
                        && int.TryParse(punchPortRange, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;
        return new PunchService().Listen(punchPort);
    }
}
