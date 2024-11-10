using System.Text.Json.Serialization;

namespace uwu_downloader;

public class Cytrus
{
    [JsonPropertyName("version")]
    public int Version { get; set; }
    
    [JsonPropertyName("games")]
    public Dictionary<string, CytrusGame> Games { get; set; } = new();
}

public class CytrusGame
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("platforms")]
    public Dictionary<string, Dictionary<string, string>> Platforms { get; set; } = new();
}

