using System.Globalization;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.SlingNetwork.Services.ApiHttpServices;
using DotNetCampus.SlingNetwork.Services.PunchServices;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("serve", Description = "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler<AppContext>
{
    private readonly ApiHttpService _apiHttpService = new ApiHttpService(); 
    private readonly PunchService _punchService = new PunchService(); 
    
    [Option('a', "api-url", ValueName = "url", Description = "Command.ServeHandler.ListenUrls")]
    public IReadOnlyList<string>? ListenUrls { get; set; }

    [Option("admin-url", ValueName = "url", Description = "Command.ServeHandler.AdminListenUrls")]
    public IReadOnlyList<string> AdminListenUrls { get; set; } = null!;

    [Option('p', "punch-port-range", ValueName = "number", Description = "Command.ServeHandler.PunchPortRange")]
    public string? PunchPortRange { get; set; }

    public async Task<int> RunAsync(AppContext state)
    {
        var signalingServiceTask = RunSignalingServiceAsync();
        var punchServiceTask = RunPunchServiceAsync();
        await Task.WhenAll(signalingServiceTask, punchServiceTask);
        return 0;
    }

    private Task RunSignalingServiceAsync()
    {
        return _apiHttpService.Listen(ListenUrls);
    }

    private Task RunPunchServiceAsync()
    {
        var punchPort = PunchPortRange is { } punchPortRange
                        && int.TryParse(punchPortRange, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;
        return _punchService.Listen(punchPort);
    }
}
