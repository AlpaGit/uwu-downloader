namespace uwu_downloader;

/// <summary>
/// Used keys:
///     WEBHOOK_URL
///     POST_DOWNLOAD
///     FRAGMENTS (csv)
/// </summary>
public class Configuration
{
    private const string ConfigFile = ".env";
    
    public void Load()
    {
        if (!System.IO.File.Exists(ConfigFile))
        {
            Logger.Info("Configuration file not found.");
            return;
        }

        var lines = System.IO.File.ReadAllLines(ConfigFile);
        foreach (var line in lines)
        {
            var parts = line.Split("=");
            if (parts.Length != 2)
            {
                Logger.Info($"Invalid configuration line: {line}");
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }
    }
    
    public string Get(string key)
    {
        return Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }
}