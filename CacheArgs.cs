using System.ComponentModel.DataAnnotations;
using CLIAlly;

namespace RemoteMediaCache;

public class CacheArgs : BaseCacheArgs
{
}

public abstract class BaseCacheArgs
{
    [Arg(0), Path, Required] public string Path;
    [Arg(1)] public bool CacheNonNetworkPaths = false;
    [Arg(2)] public string? ForwardToCommand = null;
    [Arg(3)] public string? ForwardToCommandArguments = null;
}

public class PseudoCacheArgs : BaseCacheArgs
{
    
}