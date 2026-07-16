using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Applications.NatTest;
using DotNetCampus.SlingNetwork.Transports.Models;

namespace DotNetCampus.SlingNetwork.Cli;

[Command("nat-test", Description = "Command.NatTestHandler.Description")]
public class NatTestHandler : ICommandHandler<AppContext>
{
    [Option('c', "control-url", ValueName = "url", Description = "Command.NatTestHandler.ControlUrl")]
    public required string ControlUrl { get; init; }

    [Option('r', "repeat-count", ValueName = "number", Description = "Command.NatTestHandler.PacketRepeatCount")]
    public int? PacketRepeatCount { get; init; }

    [Option('d', "repeat-delay", ValueName = "milliseconds", Description = "Command.NatTestHandler.PacketRepeatDelay")]
    public int? PacketRepeatDelay { get; init; }

    [Option('v', "verbose", Description = "Command.NatTestHandler.Verbose")]
    public bool Verbose { get; init; }

    public async Task<int> RunAsync(AppContext app)
    {
        app.Logger.Info($"Requesting a NAT test to {ControlUrl}...");

        var client = new NatTestClient(app, PacketRepeatCount ?? 1, PacketRepeatDelay ?? 500);
        var sessionResult = await client.CreateNewAsync(ControlUrl, IpProtocol.IPv4);
        if (!sessionResult.IsSuccess)
        {
            app.Logger.Error(sessionResult.ErrorMessage);
            return 1;
        }

        var session = sessionResult.Value;
        app.Logger.Info($"NAT test session {session.SessionId} started.");
        var phase = session.Prepare();

        if (phase.Phase is NatTestPhase.Filtering)
        {
            phase = await phase.FilteringPhaseAsync();
        }

        if (phase.Phase is NatTestPhase.Mapping)
        {
            phase = await phase.MappingPhaseAsync();
        }

        if (phase.Phase is NatTestPhase.Mapping2)
        {
            phase = await phase.Mapping2PhaseAsync();
        }

        if (phase.Phase is NatTestPhase.Failed)
        {
            app.Logger.Warn("NAT test failed.");
            return 1;
        }

        if (phase.Phase is NatTestPhase.Success)
        {
            var report = phase.Report;
            var result = Verbose
                ? $"""
                NAT test result:
                - Success: {report.Success}
                - SessionId: {report.SessionId}
                - Mapping: {report.Mapping}
                - Filtering: {report.Filtering}
                - ClientLocalEndPoint: {report.ClientLocalEndPoint}
                - ClientPublicEndPoint1: {report.ClientPublicEndPoint}
                - ClientPublicEndPoint2: {report.ClientPublicEndPointToAlternateServerPort1}
                - ClientPublicEndPoint3: {report.ClientPublicEndPointToAlternateServerPort2}
                - IsPublicEndPoint: {report.IsPublicEndPoint}
                """
                : $"""
                NAT test result:
                - Mapping: {report.Mapping}
                - Filtering: {report.Filtering}
                - IsPublicEndPoint: {report.IsPublicEndPoint}
                """;
            app.Logger.Info(result);
            return 1;
        }

        app.Logger.Error("Not implemented");
        return 0;
    }
}
