using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;

namespace AmiiboGameList
{
    class Program
    {
        /// <summary>
        /// The lazy instance of the AmiiboDataBase
        /// </summary>
        private static readonly Lazy<DBRootobjectInstance> lazy = new(() => new DBRootobjectInstance());

        /// <summary>
        /// Gets the instance of the AmiiboDataBase.
        /// </summary>
        /// <value>
        /// The instance of the AmiiboDataBase.
        /// </value>
        public static DBRootobjectInstance BRootobject => lazy.Value;

        private static readonly WebClient client = new();

        /// <summary>
        /// Mains this instance.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="XmlSerializer">typeof(Switchreleases)</exception>
        static void Main(string[] args)
        {
            ParseArguments(args, out string inputPath, out string outputPath);

            // Load Regex for removing copyrights, trademarks, etc.
            Regex rx = new(@"[®™]", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            // Load Amiibo data
            Debugger.Log("Loading Amiibo's");
            BRootobject.rootobject = JsonConvert.DeserializeObject<DBRootobject>(File.ReadAllText(inputPath).Trim());
            Dictionary<Hex, Games> export = new();

            foreach (KeyValuePair<Hex, DBAmiibo> entry in BRootobject.rootobject.amiibos)
            {
                entry.Value.ID = entry.Key;
            }
            // Make WebClient

            // Load Wii U games
            Debugger.Log("Loading WiiU games");
            Games.WiiUGames = JsonConvert.DeserializeObject<List<GameInfo>>(Properties.Resources.WiiU);

            // Load 3DS games
            Debugger.Log("Loading 3DS games");
            XmlSerializer serializer = new(typeof(DSreleases));
            byte[] byteArray = Encoding.UTF8.GetBytes(Properties.Resources.DS);
            MemoryStream stream = new(byteArray);
            Games.DSGames = ((DSreleases)serializer.Deserialize(stream)).release.ToList();
            stream.Dispose();

            // Load Switch games
            Debugger.Log("Loading Switch games");
            Games.SwitchGames = (Lookup<string, string>)JsonConvert.DeserializeObject<Dictionary<Hex, SwitchGame>>(client.DownloadString("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json"))
                // Make KeyValuePairs to turn into a Lookup and decode the HTML encoded name
                .Select(x => new KeyValuePair<string, string>(HttpUtility.HtmlDecode(x.Value.name), x.Value.id)).Where(y => y.Value != null)
                // Convert to Lookup for faster searching while allowing multiple values per key and apply regex
                .ToLookup(x => rx.Replace(x.Key, "").Replace('’', '\'').ToLower(), x => x.Value);
            Debugger.Log("Done loading!");

            // List to keep track of missing games
            Games.missingGames = new();

            // Counter to keep track of how many amiibos we've done
            int AmiiboCounter = 0;

            // Iterate over all Amiibo's and get game info
            Parallel.ForEach(BRootobject.rootobject.amiibos, (DBamiibo) =>
            {
                (Hex, Games) exportAmiibo = ParseAmiibo(DBamiibo, ref export, ref AmiiboCounter);
                export.Add(exportAmiibo.Item1, exportAmiibo.Item2);

                // Show which amiibo just got added
                AmiiboCounter++;
                Debugger.Log($"{ AmiiboCounter:D3}/{ BRootobject.rootobject.amiibos.Count } Done with { DBamiibo.Value.OriginalName } ({ DBamiibo.Value.amiiboSeries })", Debugger.DebugLevel.Verbose);
            });

            // Sort export object
            Hex[] KeyArray = export.Keys.ToArray();
            Array.Sort(KeyArray);
            AmiiboKeyValue SortedAmiibos = new();
            foreach (Hex key in KeyArray)
            {
                SortedAmiibos.amiibos.Add(key, export[key]);
            }

            // Write the SortedAmiibos to file as an tab-indented json
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(SortedAmiibos, Formatting.Indented).Replace("  ", "\t"));

            // Inform we're done
            Debugger.Log("\nDone generating the JSON!");

            // Show missing games
            if (Games.missingGames.Count != 0)
            {
                Debugger.Log("However, the following games couldn't find their titleids and thus couldn't be added:", Debugger.DebugLevel.Warn);
                foreach (string Game in Games.missingGames.Distinct())
                {
                    Debugger.Log("\t" + Game, Debugger.DebugLevel.Warn);
                }
            }

        }

        private static (Hex, Games) ParseAmiibo(KeyValuePair<Hex, DBAmiibo> DBamiibo, ref Dictionary<Hex, Games> export, ref int AmiiboCounter)
        {
            WebClient AmiiboClient = new();
            Games ExAmiibo = new();
            string GameSeriesURL = DBamiibo.Value.amiiboSeries.ToLower();

            // Regex to cleanup url
            GameSeriesURL = Regex.Replace(GameSeriesURL, @"[!.]", "");
            GameSeriesURL = Regex.Replace(GameSeriesURL, @"[' ]", "-");

            // Start making the url
            string url = $"https://amiibo.life/amiibo/{ GameSeriesURL }/{ DBamiibo.Value.Name.Replace(" ", "-").ToLower() }";

            // Handle cat in getter for name
            // TODO: move to Amiibo.cs
            if (url.EndsWith("cat"))
            {
                url = url.Insert(url.LastIndexOf('/') + 1, "cat-")[..url.Length];
            }

            // If the amiibo is an animal crossing card, look name up on site and get the first link
            // TODO: error if no link is found
            if (DBamiibo.Value.type == "Card" && DBamiibo.Value.amiiboSeries == "Animal Crossing")
            {
                // Look amiibo up
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(
                    WebUtility.HtmlDecode(
                        AmiiboClient.DownloadString("https://amiibo.life/search?q=" + DBamiibo.Value.characterName)
                        )
                    );

                // Filter for card amiibos only and get url
                foreach (HtmlNode item in htmlDoc.DocumentNode.SelectNodes("//ul[@class='figures-cards small-block-grid-2 medium-block-grid-4 large-block-grid-4']/li"))
                {
                    if (item.ChildNodes[1].GetAttributeValue("href", string.Empty).Contains("cards"))
                    {
                        url = "https://amiibo.life" + item.ChildNodes[1].GetAttributeValue("href", string.Empty);
                        break;
                    }
                }
            }

            // Handle amiibos where gameseries is set to others
            switch (url)
            {
                case "https://amiibo.life/amiibo/others/super-mario-cereal":
                    url = "https://amiibo.life/amiibo/super-mario-cereal/super-mario-cereal";
                    break;

                case "https://amiibo.life/amiibo/others/solaire-of-astora":
                    url = "https://amiibo.life/amiibo/dark-souls/solaire-of-astora";
                    break;
            }
            try
            {
                client.Encoding = Encoding.Unicode;
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(
                    WebUtility.HtmlDecode(
                        AmiiboClient.DownloadString(url)
                        )
                    );

                // Dispose of the WebClient because we don't need it anymore
                AmiiboClient.Dispose();

                // Get the games panel
                HtmlNodeCollection GamesPanel = htmlDoc.DocumentNode.SelectNodes("//*[@class='games panel']/a");
                if (GamesPanel.Count == 0)
                {
                    Debugger.Log("No games found for " + DBamiibo.Value.Name, Debugger.DebugLevel.Verbose);
                }

                // Iterate over each game in the games panel
                Parallel.ForEach(GamesPanel, node =>
                {
                    // Get the name of the game
                    Game game = new()
                    {
                        gameName = node.SelectSingleNode(".//*[@class='name']/text()[normalize-space()]").InnerText.Trim().Replace("Poochy & ", "").Trim().Replace("Ace Combat Assault Horizon Legacy +", "Ace Combat Assault Horizon Legacy+").Replace("Power Pros", "Jikkyou Powerful Pro Baseball"),
                        gameID = new(),
                        amiiboUsage = new()
                    };

                    // Get the amiibo usages
                    foreach (HtmlNode amiiboUsage in node.SelectNodes(".//*[@class='features']/li"))
                    {
                        game.amiiboUsage.Add(new()
                        {
                            Usage = amiiboUsage.GetDirectInnerText().Trim(),
                            write = amiiboUsage.SelectSingleNode("em")?.InnerText == "(Read+Write)"
                        });
                    }

                    if (DBamiibo.Value.Name == "Shadow Mewtwo")
                    {
                        game.gameName = "Pokkén Tournament";
                    }

                    // Add game to the correct console and get correct titleid
                    Regex rgx = new("[^a-zA-Z0-9 -]");
                    switch (node.SelectSingleNode(".//*[@class='name']/span").InnerText.Trim().ToLower())
                    {
                        case "switch":
                            try
                            {
                                game.gameID = Games.SwitchGames[game.sanatizedGameName.ToLower()].ToList();
                                HtmlDocument htmlDoc = new();

                                if (game.gameID.Count == 0)
                                {
                                    game.gameID = game.sanatizedGameName switch
                                    {
                                        "Cyber Shadow" => new() { "0100C1F0141AA000" },
                                        "Jikkyou Powerful Pro Baseball" => new() { "0100E9C00BF28000" },
                                        "Super Kirby Clash" => new() { "01003FB00C5A8000" },
                                        "Shovel Knight Showdown" => new() { "0100B380022AE000" },
                                        "The Legend of Zelda: Skyward Sword HD" => new() { "01002DA013484000" },
                                        "Yu-Gi-Oh! Rush Duel Saikyo Battle Royale" => new() { "01003C101454A000" },
                                        _ => throw new Exception()
                                    };
                                }

                                game.gameID = game.gameID.Distinct().ToList();
                                ExAmiibo.gamesSwitch.Add(game);
                            }
                            catch
                            {
                                Games.missingGames.Add(game.gameName + " (Switch)");
                            }
                            break;
                        case "wii u":
                            try
                            {
                                string[] gameIDs = Games.WiiUGames.Find(WiiUGame => WiiUGame.Name.Contains(game.gameName, StringComparer.OrdinalIgnoreCase))?.Ids;
                                if (gameIDs?.Length == 0 || gameIDs == null)
                                {
                                    game.gameID = game.gameName switch
                                    {
                                        "Shovel Knight Showdown" => new() { "000500001016E100", "0005000010178F00", "0005000E1016E100", "0005000E10178F00", "0005000E101D9300" },
                                        _ => throw new Exception()
                                    };
                                }
                                else
                                {
                                    foreach (string ID in gameIDs)
                                    {
                                        game.gameID.Add(ID[..16]);
                                    }
                                }

                                game.gameID = game.gameID.Distinct().ToList();
                                ExAmiibo.gamesWiiU.Add(game);
                            }
                            catch
                            {
                                Games.missingGames.Add(game.gameName + " (Wii U)");
                            }
                            break;
                        case "3ds":
                            try
                            {
                                List<DSreleasesRelease> games = Games.DSGames.FindAll(DSGame => rgx.Replace(WebUtility.HtmlDecode(DSGame.name).ToLower(), "").Contains(rgx.Replace(game.gameName.ToLower(), "")));
                                if (games.Count == 0)
                                {
                                    game.gameID = game.gameName switch
                                    {
                                        "Style Savvy: Styling Star" => new() { "00040000001C2500" },
                                        "Metroid Prime: Blast Ball" => new() { "0004000000175300" },
                                        "Mini Mario & Friends amiibo Challenge" => new() { "000400000016C300", "000400000016C200" },
                                        "Team Kirby Clash Deluxe" => new() { "00040000001AB900", "00040000001AB800" },
                                        "Kirby's Extra Epic Yarn" => new() { "00040000001D1F00" },
                                        "Kirby's Blowout Blast" => new() { "0004000000196F00" },
                                        "BYE-BYE BOXBOY!" => new() { "00040000001B5400", "00040000001B5300" },
                                        "Azure Striker Gunvolt 2" => new() { "00040000001A6E00" },
                                        "niconico app" => new() { "0005000010116400" },
                                        _ => throw new Exception(),
                                    };
                                }
                                games.ForEach(DSGame =>
                                    game.gameID.Add(DSGame.titleid[..16]));

                                game.gameID = game.gameID.Distinct().ToList();
                                ExAmiibo.games3DS.Add(game);
                            }
                            catch
                            {
                                Games.missingGames.Add(game.gameName + " (3DS)");
                            }
                            break;
                        default:
                            break;
                    }
                });
            }
            catch (Exception e)
            {
                Debugger.Log(
                    $"Error caught:\n" +
                    $"{url}\n\t" +
                    $"{ e }", Debugger.DebugLevel.Error);
            }

            // Sort all gamelists
            ExAmiibo.gamesSwitch.Sort();
            ExAmiibo.gamesWiiU.Sort();
            ExAmiibo.games3DS.Sort();

            // Return the created amiibo
            return (DBamiibo.Key, ExAmiibo);
        }

        private static void ParseArguments(string[] args, out string inputPath, out string outputPath)
        {
            // Make arguments lowercase
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = args[i].ToLowerInvariant();
            }
            if (args.Length != 0)
            {
                Debugger.Log($"Running with these arguments: {string.Join(' ', args)}\n");

                // Show help message
                if (args.Contains("-h") || args.Contains("-help"))
                {
                    StringBuilder sB = new();
                    sB.AppendLine("Usage:");
                    sB.AppendLine("-i | -input {filepath} to specify input json");
                    sB.AppendLine("-o | -output {filepath} to specify output json");
                    sB.AppendLine("-u | -update to automatically get the latest amiibo.json from github, if the -i parameter is specified this will be saved to that path");
                    sB.AppendLine("-l | -log {value} will set the logging level, can pick from verbose, info, warn, error or from 0 to 3 respectively");
                    sB.AppendLine("-h | -help shows this message");
                    Debugger.Log(sB.ToString());
                    Environment.Exit(0);
                }
            }

            // Set default values
            inputPath = @".\amiibo.json";
            outputPath = @".\games_info.json";
            bool update = false;
            Debugger.CurrentDebugLevel = Debugger.DebugLevel.Info;

            // Loop through arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                    case "-input":
                        if (File.Exists(args[i + 1]) || args.Contains("-i") || args.Contains("-update"))
                        {
                            inputPath = args[i + 1];
                            i++;
                            continue;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Input file '{args[i + 1]}' not found");
                        }
                    case "-o":
                    case "-output":
                        if (Directory.Exists(Path.GetDirectoryName(args[i + 1])))
                        {
                            outputPath = args[i + 1];
                            i++;
                            continue;
                        }
                        else
                        {
                            throw new DirectoryNotFoundException($"Input directory '{args[i + 1]}' not found");
                        }
                    case "-u":
                    case "-update":
                        update = true;
                        break;
                    case "-l":
                    case "-log":
                        ;
                        if (Enum.TryParse(args[i + 1], true, out Debugger.DebugLevel debugLevel))
                        {
                            Debugger.CurrentDebugLevel = debugLevel;
                            i++;
                            continue;
                        }
                        else
                        {
                            throw new ArgumentException($"Incorrect debug level passed: {args[i + 1]}");
                        }
                    default:
                        break;
                }
            }

            // If update is set, download latest amiibo.json
            if (update)
            {
                Debugger.Log("Downloading latest amiibo.json from github");
                using WebClient AmiiboJSONClient = new();
                AmiiboJSONClient.DownloadFile("https://raw.githubusercontent.com/N3evin/AmiiboAPI/master/database/amiibo.json", inputPath);
            }
        }
    }
}
