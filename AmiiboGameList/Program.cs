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
    public class Program
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
        private static string inputPath;
        private static string outputPath = @".\games_info.json";
        private static readonly Dictionary<Hex, Games> export = new();

        /// <summary>
        /// Mains this instance.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="XmlSerializer">typeof(Switchreleases)</exception>
        public static int Main(string[] args)
        {
            ParseArguments(args);

            // Load Regex for removing copyrights, trademarks, etc.
            Regex rx = new(@"[®™]", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            // Load amiibo data
            Debugger.Log("Loading amiibo");
            try
            {
                string amiiboJSON = default;
                if (string.IsNullOrEmpty(inputPath))
                {
                    Debugger.Log("Downloading amiibo database", Debugger.DebugLevel.Verbose);
                    try
                    {
                        amiiboJSON = client.DownloadString("https://raw.githubusercontent.com/N3evin/AmiiboAPI/master/database/amiibo.json");
                    }
                    catch (Exception e)
                    {
                        Debugger.Log("Error while downloading amiibo.json, please check internet:\n" + e.Message, Debugger.DebugLevel.Error);
                        Environment.Exit((int)Debugger.ReturnType.InternetError);
                    }
                }
                else
                {
                    amiiboJSON = File.ReadAllText(inputPath);
                }

                Debugger.Log("Processing amiibo database", Debugger.DebugLevel.Verbose);
                BRootobject.rootobject = JsonConvert.DeserializeObject<DBRootobject>(amiiboJSON);

                foreach (KeyValuePair<Hex, DBAmiibo> entry in BRootobject.rootobject.amiibos)
                {
                    entry.Value.ID = entry.Key;
                }
            }
            catch (Exception ex)
            {
                Debugger.Log("Error loading amiibo.json:\n" + ex.Message, Debugger.DebugLevel.Error);
                Environment.Exit((int)Debugger.ReturnType.DatabaseLoadingError);
            }

            // Load Wii U games
            Debugger.Log("Loading Wii U games");
            Debugger.Log("Processing Wii U database", Debugger.DebugLevel.Verbose);
            try
            {
                Games.WiiUGames = JsonConvert.DeserializeObject<List<GameInfo>>(Properties.Resources.WiiU);
            }
            catch (Exception ex)
            {
                Debugger.Log("Error loading Wii U games:\n" + ex.Message, Debugger.DebugLevel.Error);
                Environment.Exit((int)Debugger.ReturnType.DatabaseLoadingError);
            }

            // Load 3DS games
            Debugger.Log("Loading 3DS games");
            try
            {
                byte[] DSDatabase = default;
                try
                {
                    Debugger.Log("Downloading 3DS database", Debugger.DebugLevel.Verbose);
                    DSDatabase = client.DownloadData("http://3dsdb.com/xml.php");
                }
                catch (Exception ex)
                {
                    Debugger.Log("Error while downloading 3DS database, please check internet:\n" + ex.Message, Debugger.DebugLevel.Error);
                    Environment.Exit((int)Debugger.ReturnType.InternetError);
                }
                Debugger.Log("Processing 3DS database", Debugger.DebugLevel.Verbose);
                XmlSerializer serializer = new(typeof(DSreleases));
                using MemoryStream stream = new(DSDatabase);
                Games.DSGames = ((DSreleases)serializer.Deserialize(stream)).release.ToList();
            }
            catch (Exception ex)
            {
                Debugger.Log("Error loading 3DS games:\n" + ex.Message, Debugger.DebugLevel.Error);
                Environment.Exit((int)Debugger.ReturnType.DatabaseLoadingError);
            }

            // Load Switch games
            Debugger.Log("Loading Switch games");
            try
            {
                string BlawarDatabase = default;
                // Try loading the database
                Debugger.Log("Downloading Switch database", Debugger.DebugLevel.Verbose);
                try
                {
                    BlawarDatabase = client.DownloadString("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json");
                }
                catch (Exception ex)
                {
                    Debugger.Log("Error while downloading switch database, please check internet:\n" + ex.Message, Debugger.DebugLevel.Error);
                    Environment.Exit((int)Debugger.ReturnType.InternetError);
                }
                Debugger.Log("Processing Switch database", Debugger.DebugLevel.Verbose);
                // Parse the loaded JSON
                Games.SwitchGames = (Lookup<string, string>)JsonConvert.DeserializeObject<Dictionary<Hex, SwitchGame>>(BlawarDatabase)
                    // Make KeyValuePairs to turn into a Lookup and decode the HTML encoded name
                    .Select(x => new KeyValuePair<string, string>(HttpUtility.HtmlDecode(x.Value.name), x.Value.id)).Where(y => y.Value != null)
                    // Convert to Lookup for faster searching while allowing multiple values per key and apply regex
                    .ToLookup(x => rx.Replace(x.Key, "").Replace('’', '\'').ToLower(), x => x.Value);
            }
            catch (Exception ex)
            {
                Debugger.Log("Error loading Switch games:\n" + ex.Message, Debugger.DebugLevel.Error);
                Environment.Exit((int)Debugger.ReturnType.DatabaseLoadingError);
            }

            client.Dispose();
            Debugger.Log("Done loading!");

            // List to keep track of missing games
            Games.missingGames = new();

            // Counter to keep track of how many amiibo we've done
            int AmiiboCounter = 0;
            int TotalAmiibo = BRootobject.rootobject.amiibos.Count;

            Debugger.Log("Processing amiibo");
            // Iterate over all amiibo and get game info
            Parallel.ForEach(BRootobject.rootobject.amiibos, (DBamiibo) =>
            {
                Games exportAmiibo = default;
                try
                {
                    exportAmiibo = ParseAmiibo(DBamiibo.Value);
                }
                catch (WebException ex)
                {
                    Debugger.Log($"Internet error when processing {DBamiibo.Value.Name} ({DBamiibo.Value.OriginalName})\n{ex.Message}\n{DBamiibo.Value.URL}", Debugger.DebugLevel.Error);
                    Environment.Exit((int)Debugger.ReturnType.InternetError);
                }
                catch (Exception ex)
                {
                    Debugger.Log($"Unexpected error when processing {DBamiibo.Value.Name} ({DBamiibo.Value.OriginalName})\n{ex.Message}", Debugger.DebugLevel.Error);
                    Environment.Exit((int)Debugger.ReturnType.UnknownError);
                }

                export.Add(DBamiibo.Key, exportAmiibo);

                // Show which amiibo just got added
                AmiiboCounter++;
                Debugger.Log($"{AmiiboCounter:D3}/{TotalAmiibo} Done with {DBamiibo.Value.OriginalName} ({DBamiibo.Value.amiiboSeries})", Debugger.DebugLevel.Verbose);
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
                return (int)Debugger.ReturnType.SuccessWithErrors;
            }
            else
            {
                return (int)Debugger.ReturnType.Success;
            }

        }

        private static Games ParseAmiibo(DBAmiibo DBamiibo)
        {
            WebClient AmiiboClient = new();
            Games ExAmiibo = new();


            client.Encoding = Encoding.Unicode;
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(
                WebUtility.HtmlDecode(
                    AmiiboClient.DownloadString(DBamiibo.URL)
                    )
                );

            // Dispose of the WebClient because we don't need it anymore
            AmiiboClient.Dispose();

            // Get the games panel
            HtmlNodeCollection GamesPanel = htmlDoc.DocumentNode.SelectNodes("//*[@class='games panel']/a");
            if (GamesPanel.Count == 0)
            {
                Debugger.Log("No games found for " + DBamiibo.Name, Debugger.DebugLevel.Verbose);
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

                if (DBamiibo.Name == "Shadow Mewtwo")
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

            // Sort all gamelists
            ExAmiibo.gamesSwitch.Sort();
            ExAmiibo.gamesWiiU.Sort();
            ExAmiibo.games3DS.Sort();

            // Return the created amiibo
            return ExAmiibo;
        }

        private static void ParseArguments(string[] args)
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
                    sB.AppendLine("-i | -input {filepath} to specify input json location");
                    sB.AppendLine("-o | -output {filepath} to specify output json location");
                    sB.AppendLine("-l | -log {value} to set the logging level, can pick from verbose, info, warn, error or from 0 to 3 respectively");
                    sB.AppendLine("-h | -help to show this message");
                    Debugger.Log(sB.ToString());
                    Environment.Exit(0);
                }
            }

            Debugger.CurrentDebugLevel = Debugger.DebugLevel.Info;

            // Loop through arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                    case "-input":
                        if (File.Exists(args[i + 1]) || args.Contains("-i"))
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
        }
    }
}
