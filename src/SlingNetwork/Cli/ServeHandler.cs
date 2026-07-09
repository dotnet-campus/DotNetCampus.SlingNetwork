using System.Globalization;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.SlingNetwork.Services.ApiHttpServices;
using DotNetCampus.SlingNetwork.Services.PunchServices;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("serve", Description = "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler<AppContext>
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ServeHandler.ListenUrls")]
    public IReadOnlyList<string>? ListenUrls { get; set; }

    [Option("admin-url", ValueName = "url", Description = "Command.ServeHandler.AdminListenUrls")]
    public IReadOnlyList<string> AdminListenUrls { get; set; } = null!;

    [Option('p', "punch-port-range", ValueName = "number", Description = "Command.ServeHandler.PunchPortRange")]
    public string? PunchPortRange { get; set; }

    public async Task<int> RunAsync(AppContext app)
    {
        // 初始化服务。
        var apiHttpService = new ApiHttpService(app);
        var punchService = new PunchService(app);

        // API 服务（http）。
        var signalingServiceTask = apiHttpService.Listen(ListenUrls);

        // 打洞服务（udp）。
        var punchPort = PunchPortRange is { } punchPortRange
                        && int.TryParse(punchPortRange, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPunchPort)
            ? parsedPunchPort
            : 50000;
        var punchServiceTask = punchService.Listen(punchPort);

        // 等待服务结束。
        await Task.WhenAll(signalingServiceTask, punchServiceTask);
        return 0;
    }
}
