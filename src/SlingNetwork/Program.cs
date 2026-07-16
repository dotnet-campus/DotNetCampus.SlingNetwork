using DotNetCampus.Cli;
using DotNetCampus.Cli.Exceptions;
using DotNetCampus.Logging;
using DotNetCampus.Logging.Writers;
using DotNetCampus.SlingNetwork.Cli;
using DotNetCampus.SlingNetwork.Localizations;
using DotNetCampus.SlingNetwork.Transports;

namespace DotNetCampus.SlingNetwork;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var appContext = AppContext.Initialize();

        try
        {
            await CommandLine.Parse(args)
                .AddHelpHandler(new HelpConfigurations
                {
                    // TODO: 命令行库，帮助内置的文本，需要能允许指定多语言
                    HelpTextLocalizer = LocalizeCommandLineHelp,
                })
                .ForState(appContext)
                .AddHandler<DefaultHandler>()
                .AddHandler<ServeHandler>()
                .AddHandler<NatTestHandler>()
                .AddHandler<ConnectHandler>()
                .RunAsync();
        }
        catch (CommandLineException ex)
        {
            appContext.Logger.Error(ex.Message);
        }
        catch (Exception ex)
        {
            appContext.Logger.Error(ex.ToString());
        }
    }

    private static string LocalizeCommandLineHelp(string key)
    {
        return LocalizedText.Current[key];
    }
}

public record AppContext
{
    public required CompositeLogger Logger { get; init; }

    public required TransportJsonContext JsonSerializer { get; init; }

    public required HttpClient HttpClient { get; init; }

    public static AppContext Initialize()
    {
        var logger = new LoggerBuilder()
            .AddConsoleLogger(clb =>
            {
                clb.WithFormat(LoggerConsoleFormatOptions.ColorfulConsole);
            })
            .Build();
        return new AppContext
        {
            Logger = logger,
            JsonSerializer = TransportJsonContext.Default,
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
            },
        };
    }
}
