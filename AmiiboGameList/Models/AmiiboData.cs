using System.Text.Json.Serialization;

namespace AmiiboGameList.Models;

public class AmiiboJsonModel
{
    [JsonPropertyName("amiibo_series")]
    public Dictionary<string, string> AmiiboSeries { get; set; } = [];

    [JsonPropertyName("amiibos")]
    public Dictionary<string, AmiiboEntry> Amiibos { get; set; } = [];

    [JsonPropertyName("characters")]
    public Dictionary<string, string> Characters { get; set; } = [];

    [JsonPropertyName("game_series")]
    public Dictionary<string, string> GameSeries { get; set; } = [];

    [JsonPropertyName("types")]
    public Dictionary<string, string> Types { get; set; } = [];
}

public class AmiiboEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonIgnore]
    public Hex Id { get; set; } // Will be populated from the dictionary key
}