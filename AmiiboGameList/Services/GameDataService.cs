using AmiiboGameList.Models;
using AmiiboGameList.Models.PlatformSpecific;
using AmiiboGameList.Utility;

using System.Text.RegularExpressions;
using System.Text.Json;
using System.Web;
using System.Xml.Serialization;

namespace AmiiboGameList.Services;

public partial class GameDataService(ILogger logger, IHttpService httpService)
{

    public List<WiiUGameInfo> WiiUGames { get; private set; } = [];
    public List<ThreeDsRelease> ThreeDsGames { get; private set; } = [];
    public ILookup<string, string> SwitchGames { get; private set; } = Enumerable.Empty<string>().ToLookup(k => k, v => v);

    private static readonly Regex NameCleanupRegex = MatchTrademarks();
    private static readonly Regex NonAlphaNumericDashRegex = MatchNonAlphaNumeric();

    public async Task LoadAllGameDataAsync()
    {
        await LoadWiiUGamesAsync();
        await LoadThreeDsGamesAsync();
        await LoadSwitchGamesAsync();
    }

    private async Task LoadWiiUGamesAsync()
    {
        logger.Log("Loading Wii U games...");
        try
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream("AmiiboGameList.Resources.WiiU.json");
            if (stream == null)
            {
                logger.Log("WiiU.json embedded resource not found.", LogLevel.Error);
                throw new FileNotFoundException("WiiU.json embedded resource not found.");
            }

            using StreamReader reader = new(stream);
            string wiiUJson = await reader.ReadToEndAsync();
            WiiUGames = JsonSerializer.Deserialize<List<WiiUGameInfo>>(wiiUJson) ?? [];
            logger.Log("Wii U games loaded.", LogLevel.Verbose);
        }
        catch (Exception ex)
        {
            logger.Log($"Error loading Wii U games: {ex.Message}", LogLevel.Error);
            throw new InvalidOperationException("Failed to load Wii U game data.", ex);
        }
    }

    private async Task LoadThreeDsGamesAsync()
    {
        logger.Log("Loading 3DS games...");
        try
        {
            logger.Log("Downloading 3DS database from 3dsdb.com...", LogLevel.Verbose);
            byte[] dsDatabaseBytes = await httpService.GetByteArrayAsync("http://3dsdb.com/xml.php");

            logger.Log("Processing 3DS database...", LogLevel.Verbose);
            XmlSerializer serializer = new(typeof(ThreeDsReleaseList));
            using MemoryStream stream = new(dsDatabaseBytes);
            ThreeDsReleaseList dsReleases = (ThreeDsReleaseList)serializer.Deserialize(stream);
            ThreeDsGames = dsReleases?.Releases?.ToList() ?? [];
            logger.Log("3DS games loaded.", LogLevel.Verbose);
        }
        catch (Exception ex)
        {
            logger.Log($"Error loading 3DS games: {ex.Message}", LogLevel.Error);
            throw new InvalidOperationException("Failed to load 3DS game data.", ex);
        }
    }

    private async Task LoadSwitchGamesAsync()
    {
        logger.Log("Loading Switch games...");
        try
        {
            logger.Log("Downloading Switch database from blawar/titledb...", LogLevel.Verbose);
            string blawarDbJson = await httpService.GetStringAsync("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json");

            logger.Log("Processing Switch database...", LogLevel.Verbose);
            Dictionary<string, SwitchGameInfo> switchGameDictionary = JsonSerializer.Deserialize<Dictionary<string, SwitchGameInfo>>(blawarDbJson);

            SwitchGames = switchGameDictionary
                .Where(kvp => kvp.Value.Id != null && kvp.Value.Name != null)
                .Select(kvp => new KeyValuePair<string, string>(
                    NameCleanupRegex.Replace(HttpUtility.HtmlDecode(kvp.Value.Name) ?? "", "").Replace('’', '\'').ToLowerInvariant(),
                    kvp.Value.Id
                ))
                .ToLookup(kvp => kvp.Key, kvp => kvp.Value);
            logger.Log("Switch games loaded.", LogLevel.Verbose);
        }
        catch (Exception ex)
        {
            logger.Log($"Error loading Switch games: {ex.Message}", LogLevel.Error);
            throw new InvalidOperationException("Failed to load Switch game data.", ex);
        }
    }

    public List<string> FindSwitchGameIds(string lookupName)
    {
        if (lookupName == "Shovel Knight")
            lookupName = "Shovel Knight: Treasure Trove"; // Special case due to game rebranding

        List<string> ids = [.. SwitchGames[lookupName.ToLowerInvariant()]];
        return ids.Count > 0
            ? [.. ids.Order().Distinct()]
            : lookupName switch
            {
                "Cyber Shadow" => ["0100C1F0141AA000"],
                "Jikkyou Powerful Pro Baseball" => ["0100E9C00BF28000"],
                "Shovel Knight Pocket Dungeon" => ["01006B00126EC000"],
                "Shovel Knight Showdown" => ["0100B380022AE000"],
                "Super Kirby Clash" => ["01003FB00C5A8000"],
                "The Legend of Zelda: Echoes of Wisdom" => ["01008CF01BAAC000"],
                "The Legend of Zelda: Skyward Sword HD" => ["01002DA013484000"],
                "Yu-Gi-Oh! Rush Duel Saikyo Battle Royale" => ["01003C101454A000"],
                _ => [],
            };
    }

    public List<string> FindWiiUGameIds(string lookupName)
    {
        WiiUGameInfo gameInfo = WiiUGames.Find(g =>
            g.Names.Any(n => n.Equals(lookupName, StringComparison.OrdinalIgnoreCase)) ||
            g.Names.Any(n => n.Contains(lookupName, StringComparison.OrdinalIgnoreCase)));

        return gameInfo?.Ids?.Length > 0
            ? [.. gameInfo.Ids.Select(id => id[..Math.Min(id.Length, 16)]).Order().Distinct()]
            : lookupName switch
            {
                "Shovel Knight Showdown" => ["000500001016E100", "0005000010178F00", "0005000E1016E100", "0005000E10178F00", "0005000E101D9300"],
                _ => [],
            };
    }

    public List<string> FindThreeDsGameIds(string lookupName)
    {
        string cleanedLookupName = NonAlphaNumericDashRegex.Replace(lookupName.ToLowerInvariant(), "");
        List<ThreeDsRelease> matchedGames = ThreeDsGames.FindAll(g =>
            NonAlphaNumericDashRegex.Replace(HttpUtility.HtmlDecode(g.Name ?? "").ToLowerInvariant(), "")
            .Contains(cleanedLookupName));

        return matchedGames.Count > 0
            ? [.. matchedGames.Select(g => g.TitleId[..Math.Min(g.TitleId.Length, 16)]).Order().Distinct()]
            : lookupName switch
            {
                "Style Savvy: Styling Star" => ["00040000001C2500"],
                "Metroid Prime: Blast Ball" => ["0004000000175300"],
                "Mini Mario & Friends amiibo Challenge" => ["000400000016C300", "000400000016C200"],
                "Team Kirby Clash Deluxe" => ["00040000001AB900", "00040000001AB800"],
                "Kirby's Extra Epic Yarn" => ["00040000001D1F00"],
                "Kirby's Blowout Blast" => ["0004000000196F00"],
                "BYE-BYE BOXBOY!" => ["00040000001B5400", "00040000001B5300"],
                "Azure Striker Gunvolt 2" => ["00040000001A6E00"],
                "niconico app" => ["0005000010116400"],
                _ => [],
            };
    }

    [GeneratedRegex(@"[®™]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MatchTrademarks();
    [GeneratedRegex("[^a-zA-Z0-9 -]", RegexOptions.Compiled)]
    private static partial Regex MatchNonAlphaNumeric();
}