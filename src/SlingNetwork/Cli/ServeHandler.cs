using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace SlingNetwork.Cli;

[Command("serve", Description =  "Command.ServeHandler.Description")]
public class ServeHandler : ICommandHandler
{
    [Option('a', "api-url", ValueName = "url", Description = "Command.ServeHandler.ListenUrls")]
    public IReadOnlyList<string> ListenUrls { get; set; } = null!;

    [Option("admin-url", ValueName = "url", Description = "Command.ServeHandler.AdminListenUrls")]
    public IReadOnlyList<string> AdminListenUrls { get; set; } = null!;

    [Option('p', "punch-port-range", ValueName = "number", Description = "Command.ServeHandler.PunchPortRange")]
    public string? PunchPortRange { get; set; }

    public Task<int> RunAsync()
    {
        throw new NotImplementedException();
    }
}
