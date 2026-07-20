using DotNetCampus.Logging;

namespace DotNetCampus.SlingNetwork.Framework;

internal class EmptyLogger : ILogger
{
    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
