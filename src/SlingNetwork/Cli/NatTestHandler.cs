using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.ClientSide;
using DotNetCampus.SlingNetwork.Transports.Models;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("nat-test", Description = "Command.NatTestHandler.Description")]
public class NatTestHandler : ICommandHandler<AppContext>
{
    [Option('c', "control-url", ValueName = "url", Description = "Command.NatTestHandler.ControlUrl")]
    public required string ControlUrl { get; init; }

    public async Task<int> RunAsync(AppContext app)
    {
        app.Logger.Info($"Requesting a NAT test to {ControlUrl}...");

        var client = new NatTestClient(app);
        var sessionResult = await client.CreateNewAsync(ControlUrl, IpProtocol.IPv4);
        if (!sessionResult.IsSuccess)
        {
            app.Logger.Error(sessionResult.ErrorMessage);
            return 1;
        }

        var session = sessionResult.Value;
        app.Logger.Info($"NAT test session {session.SessionId} started.");
        var phase = session.Prepare();

        phase = await phase.FilteringPhaseAsync();
        app.Logger.Info($"NAT filtering: Unknown");

        app.Logger.Error("Not implemented");
        return 0;
    }
}
