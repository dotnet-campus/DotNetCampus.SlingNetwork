using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.Cli;
using DotNetCampus.Logging;
using DotNetCampus.Logging.Writers;
using DotNetCampus.SlingNetwork.Cli;
using DotNetCampus.SlingNetwork.Localizations;
using DotNetCampus.SlingNetwork.Services.ApiHttpServices;

namespace DotNetCampus.SlingNetwork;

internal static class Program
{
    private static void Main(string[] args)
    {
        var appContext = AppContext.Initialize();

        CommandLine.Parse(args)
            .AddHelpHandler(new HelpConfigurations
            {
                // TODO: 命令行库，帮助内置的文本，需要能允许指定多语言
                HelpTextLocalizer = LocalizeCommandLineHelp,
            })
            .ForState(appContext)
            .AddHandler<DefaultHandler>()
            .AddHandler<ServeHandler>()
            .AddHandler<ConnectHandler>()
            .RunAsync();
    }

    private static string LocalizeCommandLineHelp(string key)
    {
        return LocalizedText.Current[key];
    }
}

public record AppContext
{
    public required CompositeLogger Logger { get; init; }

    public required AppJsonSerializerContext JsonSerializer { get; init; }

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
            JsonSerializer = AppJsonSerializerContext.Default,
        };
    }
}

[JsonSerializable(typeof(PunchInfo))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
    static AppJsonSerializerContext()
    {
        Default = new AppJsonSerializerContext(new JsonSerializerOptions(s_defaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
