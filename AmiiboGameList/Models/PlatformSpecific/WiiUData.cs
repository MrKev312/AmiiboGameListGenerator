using System.Text.Json.Serialization;

namespace AmiiboGameList.Models.PlatformSpecific;

public class WiiUGameInfo
{
    [JsonPropertyName("Name")]
    public string[] Names { get; set; }

    [JsonPropertyName("Ids")]
    public string[] Ids { get; set; }
}