using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CLIAlly;
using NetJsonAOT;

namespace RemoteMediaCache;

internal static class PreferenceManager
{
    private const string DirectoryName = "RemoteMediaCache";
    private const string FileName = "settings.json";
    
    private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName);
    private static readonly string DefaultCacheDirectory = Path.Combine(SettingsFolder, "cache");
    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, FileName);
    private static readonly JsonSerializerOptions SerializerOptions;

    static PreferenceManager()
    {
        SerializerOptions = RuntimeJson.JsonSerializerOptions[typeof(CachePreferences)];
        SerializerOptions.WriteIndented = true;
    }

    [Command]
    public static ExitCodeInfo Settings(CachePreferences preferences)
    {
        if (!TryGetPreferences(out var currentPrefs))
        {
            return ExitCodeInfo.FromFailure($"Failed to load preferences for modification - you may want to delete or repair the settings file at '{SettingsFilePath}'");
        }

        bool hasChanged = false;
        if (!string.IsNullOrWhiteSpace(preferences.CacheDirectory))
        {
            currentPrefs.CacheDirectory = preferences.CacheDirectory;
            hasChanged = true;
        }
        
        if (preferences.MaxCacheSizeMB != null)
        {
            // validate numeric
            currentPrefs.MaxCacheSizeMB = preferences.MaxCacheSizeMB;
            hasChanged = true;
        }

        if (!hasChanged)
        {
            return ExitCodeInfo.FromSuccess();
        }
        
        // save
        if (!TrySavePreferences(currentPrefs, out var error))
        {
            return ExitCodeInfo.FromFailure(error);
        }
        
        return ExitCodeInfo.FromSuccess();
    }
    
    public static bool TryGetPreferences([NotNullWhen(true)] out CachePreferences? preferences)
    {
        var settingsPath = SettingsFilePath;
        if (!File.Exists(settingsPath))
        {
            preferences = new CachePreferences
            {
                CacheDirectory = DefaultCacheDirectory,
                MaxCacheSizeMB = 1024
            };

            TrySavePreferences(preferences, out _);

            return true;
        }

        try
        {
            var settingsJson = File.ReadAllText(settingsPath);
            preferences = JsonSerializer.Deserialize<CachePreferences>(settingsJson, SerializerOptions);
            return preferences != null;
        }
        catch (Exception e)
        {
            preferences = null;
            return false;
        }
    }

    private static bool TrySavePreferences(CachePreferences preferences, [NotNullWhen(false)] out string? error)
    {
        try
        {
            var settingsJson = JsonSerializer.Serialize(preferences, SerializerOptions);

            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(SettingsFilePath, settingsJson);
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }

        error = null;
        return true;
    }
}