namespace RemoteMediaCache;

[Serializable]
internal class CachePreferences
{
    public string CacheDirectory;
    public int? MaxCacheSizeMB;
}