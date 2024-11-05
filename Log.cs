namespace RemoteMediaCache;

internal static class Log
{
    public static void Info(string message)
    {
        Console.WriteLine(message);
    }
    
    public static void Exception(Exception exception)
    {
        Exception(exception, "Exception");
    }

    public static void Exception(Exception exception, string message)
    {
        var msg = exception.Message;
        #if DEBUG
        msg += '\n' + Environment.StackTrace;
        #endif
        Console.WriteLine(message + ": " + msg);
    }
}