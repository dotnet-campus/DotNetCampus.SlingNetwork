using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;
using DotNetCampus.Logging;
using DotNetCampus.SlingNetwork.Applications.NatTest;
using DotNetCampus.SlingNetwork.Localizations;
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

        if (phase.Phase is NatTestPhase.Success or NatTestPhase.Failed)
        {
            await phase.FinishAsync();
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
                {LocalizedText.Current.NatTest.Report.Title}
                - {LocalizedText.Current.NatTest.Report.Success}: {LocalizedText.Current[$"Values.Boolean.{report.Success}"]}
                - {LocalizedText.Current.NatTest.Report.SessionId}: {report.SessionId}
                - {LocalizedText.Current.NatTest.Report.Mapping}: {LocalizedText.Current[$"Enums.{nameof(NatMappingBehavior)}.{report.Mapping}"]}
                - {LocalizedText.Current.NatTest.Report.Filtering}: {LocalizedText.Current[$"Enums.{nameof(NetworkPacketFilteringBehavior)}.{report.Filtering}"]}
                - {LocalizedText.Current.NatTest.Report.LegacyNatType}: {LocalizedText.Current[$"Enums.{nameof(Rfc3489NatType)}.{report.LegacyNatType}"]}
                - {LocalizedText.Current.NatTest.Report.ClientLocalEndPoint}: {report.ClientLocalEndPoint}
                - {LocalizedText.Current.NatTest.Report.ClientPublicEndPoint1}: {report.ClientPublicEndPoint}
                - {LocalizedText.Current.NatTest.Report.ClientPublicEndPoint2}: {report.ClientPublicEndPointToAlternateServerPort1}
                - {LocalizedText.Current.NatTest.Report.ClientPublicEndPoint3}: {report.ClientPublicEndPointToAlternateServerPort2?.ToString() ?? LocalizedText.Current.NatTest.Report.NotTested.ToString()}
                - {LocalizedText.Current.NatTest.Report.IsPublicEndPoint}: {LocalizedText.Current[$"Values.Boolean.{report.IsPublicEndPoint}"]}
                """
                : $"""
                {LocalizedText.Current.NatTest.Report.Title}
                - {LocalizedText.Current.NatTest.Report.Mapping}: {LocalizedText.Current[$"Enums.{nameof(NatMappingBehavior)}.{report.Mapping}"]}
                - {LocalizedText.Current.NatTest.Report.Filtering}: {LocalizedText.Current[$"Enums.{nameof(NetworkPacketFilteringBehavior)}.{report.Filtering}"]}
                - {LocalizedText.Current.NatTest.Report.IsPublicEndPoint}: {LocalizedText.Current[$"Values.Boolean.{report.IsPublicEndPoint}"]}
                """;
            app.Logger.Info(result);
            return 0;
        }

        app.Logger.Error("Not implemented");
        return 0;
    }
}
