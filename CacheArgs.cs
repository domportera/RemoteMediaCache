using System.ComponentModel.DataAnnotations;
using CLIAlly;

namespace RemoteMediaCache;

public class CacheArgs : BaseCacheArgs
{
}

public abstract class BaseCacheArgs
{
    [Arg(-3), Path, Required] public string Path;
    [Arg(10)] public bool CacheNonNetworkPaths = false;
    [Arg(20)] public string? ForwardToCommand = null;
    [Arg(21)] public string? ForwardToCommandArguments = null;
}

public class PseudoCacheArgs : BaseCacheArgs
{
    
}