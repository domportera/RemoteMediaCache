using System.Security.Cryptography;
using System.Text;

namespace RemoteMediaCache;

internal static class Utility
{
    public static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    public static bool IsNetworkPath(string path)
    {
        return path.StartsWith("//") || path.StartsWith(@"\\")
                                     || path.StartsWith("ftp://") || path.StartsWith("sftp://")
                                     || path.StartsWith("smb://")
                                     || IsHttpPath(path);
    }
    
    public static bool IsHttpPath(string path)
    {
        return path.StartsWith("http://") || path.StartsWith("https://") || path.StartsWith("www.");
    }

    public static string GetLocalCacheFileName(string remotePath)
    {
        remotePath = NormalizePathSeparators(remotePath);
        var hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(remotePath));
        return Convert.ToHexString(hashBytes);
    }
}