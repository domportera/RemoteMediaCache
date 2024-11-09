namespace RemoteMediaCache;
using CLIAlly;

class Program
{
    static int Main(string[] args)
    {
        var cliParser = CLIAlly.CommandLineParser.FromArgs(args, typeof(FileCacher), typeof(PseudoCacher));

        if (cliParser.PrintHelpIfRequested())
        {
            Console.WriteLine("Printed help and quit");
            return ExitCodeInfo.SuccessCode;
        }

        Console.WriteLine(cliParser.GetParseInfo());

        var exitCodeInfo = cliParser.TryInvokeCommands(true);
        Console.WriteLine($"Quitting application: ({exitCodeInfo.ExitCode}) {exitCodeInfo.Message}");
        return exitCodeInfo.ExitCode;
    }
}