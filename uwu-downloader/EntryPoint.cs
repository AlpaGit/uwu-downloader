
using System.Text;
using System.Text.Json;

namespace uwu_downloader;

public class EntryPoint
{
    private Dictionary<string, string> _lastVersions = new Dictionary<string, string>();
    private readonly Configuration _config;

    private static readonly List<(string Game, string Release)> HandledGames =
    [
        ("dofus", "beta")
        // Use this one when the game will be released
        //("dofus", "main"),
    ];
    
    public EntryPoint(Configuration config)
    {
        _config = config;
    }
    
    /// <summary>
    /// It will only download the newest version of the game.
    /// </summary>
    public async Task Run()
    {
        Logger.Initialize(_config);
        
        using var httpClient = new HttpClient();
        
        if(System.IO.File.Exists("last_versions.json"))
            _lastVersions = JsonSerializer.Deserialize<Dictionary<string, string>>(await System.IO.File.ReadAllTextAsync("last_versions.json"))!;
        
        Logger.Info("Starting uwu-downloader");
        
        while (true)
        {
            try
            {
                var cytrusRawJson = await httpClient.GetStringAsync("https://cytrus.cdn.ankama.com/cytrus.json");
                var cytrus = JsonSerializer.Deserialize<Cytrus>(cytrusRawJson);

                if (cytrus == null)
                    continue;

                var newestVersions = GetNewestVersion(cytrus);
                
                foreach (var (game, release, version) in newestVersions)
                {
                    await Logger.WebhookInfo($"@everyone New version available of {CapitalizeSpan(game)} ({release}) {version}");
                    
                    if (!HandledGames.Contains((game, release)))
                        continue;
                    
                    Logger.Info($"Downloading newest version of {game} {release} {version}");

                    try
                    {
                        var downloader = new Downloader();
                        await downloader.DownloadGame(game, release, $"6.0_{version}", _config);
                    }
                    catch (Exception e)
                    {
                        Logger.Info($"Error: {e.Message}");
                    }
                }
                
                await System.IO.File.WriteAllTextAsync("last_versions.json", JsonSerializer.Serialize(_lastVersions, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e)
            {
                Logger.Info($"Error: {e.Message}");
                await Task.Delay(5000);
            }
            finally
            {
                await Task.Delay(1000);
            }
        }
    }
    
    private string CapitalizeSpan(ReadOnlySpan<char> span)
    {
        var sb = new StringBuilder(span.Length);
        
        for (var i = 0; i < span.Length; i++)
        {
            sb.Append(i == 0 ? char.ToUpper(span[i]) : char.ToLower(span[i]));
        }

        return sb.ToString();
    }
    
    public List<(string Game, string Release, string Version)> GetNewestVersion(Cytrus cytrus)
    {
        var result = new List<(string Game, string Release, string Version)>();
        
        foreach (var game in cytrus.Games)
        {
            if (!game.Value.Platforms.TryGetValue("windows", out var releases))
                continue;
            
            foreach (var (release, version) in releases)
            {
                var gameKey = $"{game.Key}_{release}";
                
                if (_lastVersions.TryGetValue(gameKey, out var lastVersion))
                {
                    if (version == lastVersion)
                    {
                        continue;
                    }
                }

                Logger.Info($"New version found for {game.Key}: {version}");
                _lastVersions[gameKey] = version;
                
                result.Add((game.Key, release, version.Replace("6.0_", "")));
            }
        }
        
        return result;
    }
    

}