using AmiiboGameList.Models;
using AmiiboGameList.Utility;

using HtmlAgilityPack;

using System.Net;

namespace AmiiboGameList.Services;

/// <summary>
/// Provides functionality to collect and process game compatibility information for a given Amiibo.
/// </summary>
/// <remarks>This service interacts with external data sources to retrieve compatibility details for an Amiibo,
/// including supported games across various platforms. It processes the retrieved data and organizes it into a
/// structured format for further use. The service also logs relevant information and errors encountered during the data
/// collection process.</remarks>
/// <param name="httpService">An instance of <see cref="IHttpService"/> for making HTTP requests to retrieve Amiibo data.</param>
/// <param name="logger">An instance of <see cref="ILogger"/> for logging operations and errors.</param>
/// <param name="gameDataService">An instance of <see cref="GameDataService"/> for querying game IDs based on game names.</param>
/// <param name="amiiboDataService">An instance of <see cref="AmiiboDataService"/> for resolving Amiibo-specific URLs and details.</param>
public class AmiiboInfoCollectorService(IHttpService httpService, ILogger logger, GameDataService gameDataService, AmiiboDataService amiiboDataService)
{
    /// <summary>
    /// Collects game compatibility information for a given Amiibo and organizes it by platform.
    /// </summary>
    /// <remarks>This method retrieves game compatibility data by scraping the Amiibo's associated webpage. 
    /// If the Amiibo is an Animal Crossing card, a specific URL resolution is performed before scraping. The method
    /// handles cases where no compatibility information is found or where the HTML content  cannot be retrieved,
    /// logging relevant details in such scenarios.</remarks>
    /// <param name="amiiboDetails">The details of the Amiibo for which to collect game compatibility information.</param>
    /// <param name="missingGameTracker">A list to track games that could not be assigned to a specific platform.</param>
    /// <returns>A <see cref="GameCompatibility"/> object containing the games compatible with the specified Amiibo,  categorized
    /// by platform.</returns>
    public async Task<GameCompatibility> CollectGameCompatibilityAsync(AmiiboDetails amiiboDetails, List<string> missingGameTracker)
    {
        GameCompatibility gameCompatibility = new();
        string urlToScrape = amiiboDetails.AmiiboLifePageUrl;

        if (amiiboDetails.FigureType == "Card" && amiiboDetails.AmiiboSeries == "Animal Crossing")
        {
            urlToScrape = await amiiboDataService.ResolveAmiiboLifeCardUrlAsync(amiiboDetails.Character);
        }

        string htmlContent;
        try
        {
            htmlContent = await httpService.GetStringAsync(urlToScrape);
        }
        catch (Exception ex)
        {
            logger.Log($"Failed to download HTML from {urlToScrape} for Amiibo {amiiboDetails.NormalizedName}: {ex.Message}", LogLevel.Error);
            return gameCompatibility;
        }

        HtmlDocument htmlDoc = new();
        htmlDoc.LoadHtml(WebUtility.HtmlDecode(htmlContent));

        HtmlNodeCollection gameNodes = htmlDoc.DocumentNode.SelectNodes("//*[@class='games panel']/a");
        if (gameNodes == null || gameNodes.Count == 0)
        {
            logger.Log($"No game compatibility information found for {amiiboDetails.NormalizedName} on {urlToScrape}", LogLevel.Verbose);
            return gameCompatibility;
        }

        foreach (HtmlNode node in gameNodes)
        {
            GameTitle gameTitle = ParseGameNode(node, amiiboDetails.NormalizedName);
            if (gameTitle == null)
                continue;

            string platform = node.SelectSingleNode(".//*[@class='name']/span")?.InnerText.Trim().ToLowerInvariant();
            AssignGameToPlatform(gameTitle, platform, gameCompatibility, missingGameTracker);
        }

        SortGameLists(gameCompatibility);
        return gameCompatibility;
    }

    private static GameTitle ParseGameNode(HtmlNode node, string amiiboName)
    {
        HtmlNode nameNode = node.SelectSingleNode(".//*[@class='name']/text()[normalize-space()]");
        if (nameNode == null)
            return null;

        string gameName = nameNode.InnerText.Trim();
        gameName = gameName.Replace("Poochy & ", "").Trim();
        gameName = gameName.Replace("Ace Combat Assault Horizon Legacy +", "Ace Combat Assault Horizon Legacy+");
        gameName = gameName.Replace("Power Pros", "Jikkyou Powerful Pro Baseball");

        if (amiiboName == "Shadow Mewtwo" && gameName == "Pokkén Tournament DX")
        {
            gameName = "Pokkén Tournament";
        }

        GameTitle gameTitle = new() { Name = gameName };

        HtmlNodeCollection usageNodes = node.SelectNodes(".//*[@class='features']/li");
        if (usageNodes != null)
        {
            foreach (HtmlNode usageNode in usageNodes)
            {
                gameTitle.Usage.Add(new AmiiboUsageInfo
                {
                    Description = usageNode.GetDirectInnerText().Trim(),
                    IsWriteEnabled = usageNode.SelectSingleNode("em")?.InnerText == "(Read+Write)"
                });
            }

            gameTitle.Usage.Sort((x, y) => string.Compare(x.Description, y.Description, StringComparison.OrdinalIgnoreCase));
        }

        return gameTitle;
    }

    private void AssignGameToPlatform(GameTitle gameTitle, string platform, GameCompatibility compatibility, List<string> missingGameTracker)
    {
        List<string> gameIds;
        string platformForMissingLog;

        switch (platform)
        {
            case "switch":
                gameIds = gameDataService.FindSwitchGameIds(gameTitle.LookupName);
                platformForMissingLog = "Switch";
                if (gameIds.Count != 0)
                    compatibility.SupportedGamesSwitch.Add(gameTitle);
                break;
            case "wii u":
                gameIds = gameDataService.FindWiiUGameIds(gameTitle.LookupName);
                platformForMissingLog = "Wii U";
                if (gameIds.Count != 0)
                    compatibility.SupportedGamesWiiU.Add(gameTitle);
                break;
            case "3ds":
                gameIds = gameDataService.FindThreeDsGameIds(gameTitle.LookupName);
                platformForMissingLog = "3DS";
                if (gameIds.Count != 0)
                    compatibility.SupportedGames3DS.Add(gameTitle);
                break;
            default:
                logger.Log($"Unknown platform '{platform}' for game '{gameTitle.Name}'.", LogLevel.Warn);
                return;
        }

        if (gameIds != null && gameIds.Count != 0)
        {
            gameTitle.GameIds = gameIds;
        }
        else
        {
            lock (missingGameTracker)
            {
                missingGameTracker.Add($"{gameTitle.Name} ({platformForMissingLog})");
            }

            logger.Log($"Could not find Title ID for '{gameTitle.Name}' on platform '{platformForMissingLog}'.", LogLevel.Verbose);
        }
    }

    private static void SortGameLists(GameCompatibility compatibility)
    {
        compatibility.SupportedGames3DS.Sort();
        compatibility.SupportedGamesWiiU.Sort();
        compatibility.SupportedGamesSwitch.Sort();
    }
}