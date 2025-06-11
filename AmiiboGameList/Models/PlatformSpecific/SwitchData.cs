using System.Text.Json.Serialization;

namespace AmiiboGameList.Models.PlatformSpecific;

public class SwitchGameInfo
{
	[JsonPropertyName("id")]
	public string Id { get; set; }

	[JsonPropertyName("name")]
	public string Name { get; set; }
}