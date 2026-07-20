using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace DotNetCampus.SlingNetwork.Cli;

[Command(Description = "Command.DefaultHandler.Description")]
internal class DefaultHandler : ICommandHandler<AppContext>
{
    [Option("foo", Description = "Command.DefaultHandler.Foo")]
    public required string? Foo { get; init; }

    public async Task<int> RunAsync(AppContext app)
    {
        return 0;
    }
}
