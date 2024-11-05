using System.Diagnostics;
using CLIAlly;

namespace RemoteMediaCache;

internal static class CommandRunner
{
    public static ExitCodeInfo Run(string command, string? forwardToArgs, string filePath)
    {
        string cliFriendlyPath;
        try
        {
            cliFriendlyPath = $"'{Path.GetFullPath(filePath)}'"; // wrap in single-quotes for safety
        }
        catch (Exception e)
        {
            return ExitCodeInfo.FromFailure($"Failed to get full path of '{filePath}'");
        }
        
        // check if string is formatted with {0} or {1}
        if(string.IsNullOrWhiteSpace(forwardToArgs))
        {
            forwardToArgs = cliFriendlyPath;
        }
        else if (forwardToArgs.Contains("{0}") || forwardToArgs.Contains("{1}"))
        {
            // replace {0} and {1} with the file path
            forwardToArgs = forwardToArgs.Replace("{0}", cliFriendlyPath);
            forwardToArgs = forwardToArgs.Replace("{1}", cliFriendlyPath);
        }
        else
        {
            // add the file path to the end of the command
            forwardToArgs += $" {filePath}";
        }
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = forwardToArgs,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            }
        };

        if (!process.Start())
        {
            return ExitCodeInfo.FromFailure("Process failed to start.");
        }
        
        process.WaitForExit();
        return new ExitCodeInfo(process.ExitCode, null);
    }
}