using AmiiboGameList.Models;
using AmiiboGameList.Utility;

using HtmlAgilityPack;

using System.Net;

namespace AmiiboGameList.Services;

public class AmiiboInfoCollectorService(IHttpService httpService, ILogger logger, GameDataService gameDataService, AmiiboDataService amiiboDataService)
{
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