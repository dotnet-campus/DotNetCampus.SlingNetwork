using DotNetCampus.Cli;
using SlingNetwork.Cli;
using SlingNetwork.Localizations;

namespace SlingNetwork;

internal static class Program
{
    private static void Main(string[] args)
    {
        CommandLine.Parse(args)
            .AddHelpHandler(new HelpConfigurations
            {
                // TODO: 命令行库，帮助内置的文本，需要能允许指定多语言
                HelpTextLocalizer = LocalizeCommandLineHelp,
            })
            .AddHandler<DefaultHandler>()
            .AddHandler<ServeHandler>()
            .RunAsync();
    }

    private static string LocalizeCommandLineHelp(string key)
    {
        // TODO: 当设置为字典类型时，应该直接允许 [key] 而不需要强转。
        return ((LocalizedText.ImmutableLocalizedValues)LocalizedText.Current)[key];
    }
}
