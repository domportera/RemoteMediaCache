using CLIAlly;

namespace RemoteMediaCache;

internal sealed class FileCacher
{
    [Command(IsDefaultCommand = true)]
    private static ExitCodeInfo Cache(CacheArgs args)
    {
        Console.WriteLine("Running Cache command.");
        var remotePath = args.Path;

        var hasForwardToCommand = !string.IsNullOrWhiteSpace(args.ForwardToCommand);

        if (!Utility.IsNetworkPath(remotePath) && !args.CacheNonNetworkPaths)
        {
            return hasForwardToCommand
                ? ExitCodeInfo.FromSuccess("Skipping caching file because it is not a network path.")
                : CommandRunner.Run(args.ForwardToCommand!, args.ForwardToCommandArguments, remotePath);
        }

        if (!File.Exists(remotePath))
        {
            return ExitCodeInfo.FromFailure("File does not exist.");
        }

        if (!PreferenceManager.TryGetPreferences(out var preferences))
        {
            return ExitCodeInfo.FromFailure("Failed to get preferences");
        }

        var localFilePath = GetLocalFilePathFor(remotePath, preferences);

        if (File.Exists(localFilePath))
        {
            return hasForwardToCommand
                ? ExitCodeInfo.FromSuccess("Skipping caching file because it already exists locally.")
                : CommandRunner.Run(args.ForwardToCommand!, args.ForwardToCommandArguments, localFilePath);
        }

        var cts = new CancellationTokenSource();
        using var writeWaitHandle = new AutoResetEvent(false);
        using var downloadWaitHandle = new AutoResetEvent(true);
        var downloadBuffer = new byte[BufferLength];
        var fileWriteArgs = new LocalFileArgs(localFilePath, writeWaitHandle, downloadWaitHandle,
            new byte[BufferLength], cts.Token);
        Log.Info($"Preloading file '{remotePath}'...");


        try
        {
            using var stream = Utility.IsHttpPath(remotePath) 
                ? new HttpClient().GetStreamAsync(remotePath).GetAwaiter().GetResult() 
                : new FileStream(remotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Log.Info("File stream opened.");

            if (!stream.CanRead)
            {
                throw new Exception("File stream is not readable.");
            }

            var writeThread = new Thread(() => WriteToFile(fileWriteArgs));
            writeThread.Start();

            var fileSize = stream.Length;
            long pos = 0;
            var displayName = Utility.NormalizePathSeparators(remotePath)
                    .Split(Path.DirectorySeparatorChar)
                    .Last() + $" ({(double)fileSize / 1024 / 1024:F2} MB)";

            var startTimeSeconds = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            // read 1kb at a time
            while (pos < fileSize)
            {
                var readCount = stream.Read(downloadBuffer, 0, BufferLength);
                pos += readCount;
                var percentComplete = (double)pos / fileSize;

                if (readCount == 0) continue;

                downloadWaitHandle.WaitOne();

                (downloadBuffer, fileWriteArgs.Buffer) = (fileWriteArgs.Buffer, downloadBuffer);
                fileWriteArgs.LatestBufferLength = readCount;

                writeWaitHandle.Set();

                var currentTimeSeconds = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
                var elapsedSeconds = currentTimeSeconds - startTimeSeconds;
                var bytesPerSecond = pos / elapsedSeconds;

                var remainingBytes = fileSize - pos;
                var remainingSeconds = remainingBytes / bytesPerSecond;

                Log.Info($"Preloading file '{displayName}'... {percentComplete:P2} complete. {pos / 1024 / 1024:F2} " +
                         $"MB read. {bytesPerSecond / 1024 / 1024:F2} MB/s. {remainingSeconds:F0} seconds remaining.");
            }

            Finalize();
            writeThread.Join();
            Log.Info($"Successfully preloaded file '{remotePath}'");
        }
        catch (FileNotFoundException e)
        {
            Log.Exception(e, $"Could not find file '{remotePath}'.");
            return ExitCodeInfo.FromFailure(e.Message);
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to preload file {remotePath}: {e.Message}");
            return ExitCodeInfo.FromFailure(e.Message);
        }
        finally
        {
            Finalize();
        }
        
        //count up existing files in cache directory
        var directoryInfo = new DirectoryInfo(preferences.CacheDirectory);
        var allFiles = directoryInfo.EnumerateFiles()
            .OrderByDescending(x => File.GetLastAccessTimeUtc(x.FullName))
            .ToList();

        var totalCountBytes = 0L;
        foreach (var file in allFiles)
        {
            totalCountBytes += file.Length;
        }
        
        var fileLimitBytes = preferences.MaxCacheSizeMB * 1024 * 1024;
        while (fileLimitBytes > totalCountBytes && allFiles.Count > 1)
        {
            var mostStaleFile = allFiles[^1];
            totalCountBytes -= mostStaleFile.Length;
            mostStaleFile.Delete();
        }

        if (!string.IsNullOrWhiteSpace(args.ForwardToCommand))
        {
            return CommandRunner.Run(args.ForwardToCommand, args.ForwardToCommandArguments, localFilePath);
        }

        return ExitCodeInfo.FromSuccess("Successfully preloaded file.");

        void Finalize()
        {
            cts.Cancel();
            cts.Dispose();
            writeWaitHandle.Set();
        }
    }

    private static string GetLocalFilePathFor(string remotePath, CachePreferences preferences)
    {
        var fileName = Path.GetFileName(remotePath);
        var remotePathHash = Utility.GetLocalCacheFileName(remotePath);
        const string fileNameFmt = "{0}_{1}";
        return Path.Combine(preferences.CacheDirectory, string.Format(fileNameFmt, fileName, remotePathHash));
    }

    private static void WriteToFile(LocalFileArgs args)
    {
        var token = args.Token;
        var waitHandle = args.WriteWaitHandle;
        var localFilePath = args.LocalFilePath;
        var downloadHandle = args.DownloadWaitHandle;
        try
        {
            var directory = Path.GetDirectoryName(localFilePath);
            Directory.CreateDirectory(directory!);

            using var fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

            waitHandle.WaitOne();
            while (!token.IsCancellationRequested)
            {
                fs.Write(args.Buffer, 0, args.LatestBufferLength);
                downloadHandle.Set();
                waitHandle.WaitOne();
            }

            downloadHandle.Set();
        }
        catch (Exception e)
        {
            Log.Exception(e);
        }
        finally
        {
            downloadHandle.Set();
        }
    }


    private const int BufferLengthMb = 4;
    private const int BufferLength = 1024 * 1024 * BufferLengthMb;

    private sealed class LocalFileArgs(
        string localFilePath,
        AutoResetEvent writeWaitHandle,
        AutoResetEvent downloadWaitHandle,
        byte[] downloadBuffer,
        CancellationToken token)
    {
        public readonly string LocalFilePath = localFilePath;
        public readonly CancellationToken Token = token;
        public readonly AutoResetEvent WriteWaitHandle = writeWaitHandle;
        public readonly AutoResetEvent DownloadWaitHandle = downloadWaitHandle;
        public byte[] Buffer = downloadBuffer;
        public int LatestBufferLength;
    }
}