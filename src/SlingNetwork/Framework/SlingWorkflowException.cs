namespace DotNetCampus.SlingNetwork.Framework;

public class SlingWorkflowException : Exception
{
    public SlingWorkflowException()
    {
    }

    public SlingWorkflowException(string? message) : base(message)
    {
    }

    public SlingWorkflowException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
