using System.ComponentModel.DataAnnotations;
using CLIAlly;

namespace RemoteMediaCache;

public class PseudoCacher
{
    [Command(NameCaseSensitive = false)]
    private static void PseudoCache(PseudoCacheArgs args)
    {
        Console.Write("Running PseudoCache command...");
        var filePath = args.Path;

        if (!args.CacheNonNetworkPaths && !Utility.IsNetworkPath(filePath))
        {
            Log.Info($"Skipping preloading file '{filePath}' because it is not a network path.");
            return;
        }

        Log.Info($"Preloading file '{filePath}'...");

        FileStream fs = null;

        try
        {
            fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Log.Info("File stream opened.");

            if (!fs.CanRead)
            {
                throw new Exception("File stream is not readable.");
            }

            var fileSize = fs.Length;
            long pos = 0;
            var displayName = Utility.NormalizePathSeparators(filePath)
                    .Split(Path.DirectorySeparatorChar)
                    .Last() + $" ({(double)fileSize / 1024 / 1024:F2} MB)";

            var startTimeSeconds = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            // read 1kb at a time
            while (pos < fileSize)
            {
                var readCount = fs.Read(_throwawayBuffer, 0, BufferLength);
                pos += readCount;
                var percentComplete = (double)pos / fileSize;

                if (readCount == 0) continue;

                var currentTimeSeconds = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
                var elapsedSeconds = currentTimeSeconds - startTimeSeconds;
                var bytesPerSecond = pos / elapsedSeconds;

                var remainingBytes = fileSize - pos;
                var remainingSeconds = remainingBytes / bytesPerSecond;

                Log.Info(
                    $"Preloading file '{displayName}'... {percentComplete:P2} complete. {pos / 1024 / 1024:F2} MB read. {bytesPerSecond / 1024 / 1024:F2} MB/s. {remainingSeconds:F0} seconds remaining.");
            }

            Log.Info($"Successfully preloaded file '{filePath}'");
        }
        catch (FileNotFoundException e)
        {
            Log.Exception(e, $"Could not find file '{filePath}'.");
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to preload file {filePath}: {e.Message}");
        }
        finally
        {
            fs?.Dispose();
        }

        return;
    }

    private const int BufferLength = 1024 * 1024 * 64; // 64mb chunks
    private static readonly byte[] _throwawayBuffer = new byte[BufferLength];
}