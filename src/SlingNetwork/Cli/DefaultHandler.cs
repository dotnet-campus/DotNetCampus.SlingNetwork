using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace SlingNetwork.Cli;

[Command(Description = "Command.DefaultHandler.Description")]
internal class DefaultHandler : ICommandHandler
{
    [Option("foo", Description = "Command.DefaultHandler.Foo")]
    public required string? Foo { get; init; }

    public async Task<int> RunAsync()
    {
        return 0;
    }
}
