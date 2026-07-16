using DotNetCampus.Logging;

namespace DotNetCampus.SlingNetwork.Framework;

internal static class TaskExceptionLogger
{
    public static async void LogAsyncException(this Task task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message, ex);
        }
    }
}
