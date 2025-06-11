using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AmiiboGameList.Models;

public class GameCompatibility
{
	public List<GameTitle> SupportedGames3DS { get; set; }
	public List<GameTitle> SupportedGamesWiiU { get; set; }
	public List<GameTitle> SupportedGamesSwitch { get; set; }

	public GameCompatibility()
	{
		SupportedGames3DS = [];
		SupportedGamesWiiU = [];
		SupportedGamesSwitch = [];
	}
}

public partial class GameTitle : IComparable<GameTitle>
{
	private string _name;

	public string Name
	{
		get => _name;
		set
		{
			_name = value;
			LookupName = SanitizeNameForLookup(value);
		}
	}

	[JsonIgnore]
	public string LookupName { get; private set; }

	public List<string> GameIds { get; set; }
	public List<AmiiboUsageInfo> Usage { get; set; }

	public GameTitle()
	{
		GameIds = [];
		Usage = [];
	}

	private static string SanitizeNameForLookup(string gameName)
	{
		return gameName switch
		{
			// TODO: Consider moving these to a configuration or a dedicated service
			"The Legend of Zelda: Skyward Sword HD" => "The Legend of Zelda: Skyward Sword HD",
			"Mario + Rabbids: Kingdom Battle" => "Mario + Rabbids Kingdom Battle",
			"Little Nightmares: Complete Edition" => "Little Nightmares Complete Edition",
			_ => gameName
		};
	}

	public int CompareTo(GameTitle other)
	{
		return other == null ? 1 : string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
	}
}

public class AmiiboUsageInfo
{
	public string Description { get; set; }
	public bool IsWriteEnabled { get; set; }
}

public class AmiiboGameSet
{
	[JsonPropertyName("amiibos")]
	public Dictionary<string, GameCompatibility> Amiibos { get; set; } = [];
}