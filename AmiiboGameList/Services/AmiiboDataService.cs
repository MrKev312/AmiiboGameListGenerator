using AmiiboGameList.Models;
using AmiiboGameList.Utility;

using HtmlAgilityPack;

using System.Net;
using System.Text.RegularExpressions;

namespace AmiiboGameList.Services;

/// <summary>
/// Provides methods for retrieving and processing Amiibo data, including details about Amiibo characters, series,
/// figure types, and generating URLs for Amiibo-related resources.
/// </summary>
/// <remarks>This service interacts with an underlying Amiibo database and supports operations such as normalizing
/// Amiibo names, resolving character and series information, and generating URLs for Amiibo Life pages. It also
/// includes functionality for resolving specific Amiibo card URLs asynchronously.</remarks>
/// <param name="amiiboDb">An instance of <see cref="AmiiboJsonModel"/> representing the Amiibo database.</param>
/// <param name="httpService">An instance of <see cref="IHttpService"/> for making HTTP requests to retrieve Amiibo data.</param>
/// <param name="logger">An instance of <see cref="ILogger"/> for logging operations and errors.</param>
public partial class AmiiboDataService(AmiiboJsonModel amiiboDb, IHttpService httpService, ILogger logger)
{
    /// <summary>
    /// Retrieves detailed information about a specific Amiibo.
    /// </summary>
    /// <remarks>This method uses the provided <paramref name="amiiboEntry"/> to generate additional details
    /// about the Amiibo,  such as its normalized name and associated metadata. The returned object includes factory
    /// methods for  lazily computing certain properties, which may involve additional processing or data
    /// retrieval.</remarks>
    /// <param name="amiiboEntry">The Amiibo entry containing the basic information required to generate detailed data.</param>
    /// <returns>An <see cref="AmiiboDetails"/> object containing detailed information about the specified Amiibo,  including its
    /// normalized name, character, series, figure type, and related URL.</returns>
    public AmiiboDetails GetAmiiboDetails(AmiiboEntry amiiboEntry)
    {
        return new AmiiboDetails(
            amiiboEntry.Id,
            amiiboEntry.Name,
            normalizedNameFactory: () => NormalizeAmiiboName(amiiboEntry.Name),
            characterFactory: () => GetCharacterName(amiiboEntry.Id),
            amiiboSeriesFactory: () => GetAmiiboSeries(amiiboEntry.Id),
            figureTypeFactory: () => GetFigureType(amiiboEntry.Id),
            amiiboLifePageUrlFactory: () => GenerateAmiiboLifeUrl(GetAmiiboSeries(amiiboEntry.Id), GetFigureType(amiiboEntry.Id), GetCharacterName(amiiboEntry.Id), NormalizeAmiiboName(amiiboEntry.Name))
        );
    }

    private static string NormalizeAmiiboName(string originalName)
    {
        string returnName = originalName switch
        {
            "8-Bit Link" => "Link The Legend of Zelda",
            "8-Bit Mario Classic Color" => "Mario Classic Colors",
            "8-Bit Mario Modern Color" => "Mario Modern Colors",
            "Midna & Wolf Link" => "Wolf Link",
            "Toon Zelda - The Wind Waker" => "Zelda The Wind Waker",
            "Rosalina & Luma" => "Rosalina",
            "Zelda & Loftwing" => "Zelda & Loftwing - Skyward Sword",
            "Samus (Metroid Dread)" => "Samus",
            "E.M.M.I." => "E M M I",
            "Tatsuhisa “Luke” Kamijō" => "Tatsuhisa Luke kamijo",
            "Gakuto Sōgetsu" => "Gakuto Sogetsu",
            "E.Honda" => "E Honda",
            "A.K.I" => "A K I",
            _ => originalName
        };

        if (returnName.EndsWith(" - Cat"))
            returnName = $"Cat - {returnName[..^6]}";

        returnName = returnName.Replace("Slider", "");
        returnName = returnName.Replace("R.O.B.", "R O B");
        returnName = returnName.Replace(".", "");
        returnName = returnName.Replace("'", " ");
        returnName = returnName.Replace("\"", "");
        returnName = returnName.Replace(" & ", " ");
        returnName = returnName.Replace(" - ", " ");

        return returnName.Trim();
    }

    private string GetCharacterName(Hex amiiboId)
    {
        string characterKey = amiiboId.ToHexSlice(0, 4);
		if (!amiiboDb.Characters.TryGetValue(characterKey.ToLower(), out string characterName))
        {
            logger.Log($"Character key {characterKey} not found for Amiibo ID {amiiboId}.", LogLevel.Warn);
            return "Unknown Character";
        }

        return characterName switch
        {
            "Spork/Crackle" => "Spork",
            "OHare" => "O'Hare",
            _ => characterName,
        };
    }

    private string GetAmiiboSeries(Hex amiiboId)
    {
        string seriesKey = amiiboId.ToHexSlice(12, 2);
		if (!amiiboDb.AmiiboSeries.TryGetValue(seriesKey.ToLower(), out string amiiboSeries))
        {
            logger.Log($"Amiibo series key {seriesKey} not found for Amiibo ID {amiiboId}.", LogLevel.Warn);
            return "Unknown Series";
        }

        return amiiboSeries switch
        {
            "8-bit Mario" => "Super Mario Bros 30th Anniversary",
            "Legend Of Zelda" => "The Legend Of Zelda",
            "Monster Hunter" => "Monster Hunter Stories",
            "Monster Sunter Stories Rise" => "Monster Hunter Rise",
            "Skylanders" => "Skylanders Superchargers",
            "Super Mario Bros." => "Super Mario",
            "Xenoblade Chronicles 3" => "Xenoblade Chronicles",
            "Yu-Gi-Oh!" => "Yu-Gi-Oh! Rush Duel Saikyo Battle Royale",
            _ => amiiboSeries,
        };
    }

    private string GetFigureType(Hex amiiboId)
    {
        string typeKey = amiiboId.ToHexSlice(6, 2);
		if (!amiiboDb.Types.TryGetValue(typeKey.ToLower(), out string figureType))
        {
            logger.Log($"Figure type key {typeKey} not found for Amiibo ID {amiiboId}.", LogLevel.Warn);
            return "Unknown Type";
        }

        return figureType;
    }

    private static string GenerateAmiiboLifeUrl(string amiiboSeries, string figureType, string characterName, string normalizedName)
    {
        if (figureType == "Card" && amiiboSeries == "Animal Crossing")
        {
            return $"https://amiibo.life/search?q={WebUtility.UrlEncode(characterName)}";
        }

        switch (normalizedName.ToLower())
        {
            case "super mario cereal":
                return "https://amiibo.life/amiibo/super-mario-cereal/super-mario-cereal";
            case "solaire of astora":
                return "https://amiibo.life/amiibo/dark-souls/solaire-of-astora";
        }

        string gameSeriesUrlPart = amiiboSeries.ToLower();
        gameSeriesUrlPart = MatchExclamationPeriod().Replace(gameSeriesUrlPart, "");
        gameSeriesUrlPart = MatchApostropheSpace().Replace(gameSeriesUrlPart, "-");

        if (gameSeriesUrlPart == "street-fighter-6")
            gameSeriesUrlPart = "street-fighter-6-starter-set";

        string amiiboNameUrlPart = normalizedName.Replace(" ", "-").ToLower();

        string url = $"https://amiibo.life/amiibo/{gameSeriesUrlPart}/{amiiboNameUrlPart}";

        return url;
    }

    public async Task<string> ResolveAmiiboLifeCardUrlAsync(string characterName)
    {
        string searchUrl = $"https://amiibo.life/search?q={WebUtility.UrlEncode(characterName)}";
        try
        {
            string htmlContent = await httpService.GetStringAsync(searchUrl);
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(WebUtility.HtmlDecode(htmlContent));

            HtmlNodeCollection cardNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='figures-cards small-block-grid-2 medium-block-grid-4 large-block-grid-4']/li");
            if (cardNodes != null)
            {
                foreach (HtmlNode item in cardNodes)
                {
                    HtmlNode linkNode = item.SelectSingleNode("./a[@href]");
                    if (linkNode != null)
                    {
                        string href = linkNode.GetAttributeValue("href", string.Empty);
                        if (href.Contains("cards"))
                        {
                            return "https://amiibo.life" + href;
                        }
                    }
                }
            }

            logger.Log($"Could not find a specific card URL for '{characterName}' via search. Defaulting to search page.", LogLevel.Verbose);
        }
        catch (Exception ex)
        {
            logger.Log($"Error fetching/parsing card URL for '{characterName}': {ex.Message}", LogLevel.Error);
        }

        return searchUrl;
    }

    [GeneratedRegex(@"[!.]")]
    private static partial Regex MatchExclamationPeriod();
    [GeneratedRegex(@"[' ]")]
    private static partial Regex MatchApostropheSpace();
}