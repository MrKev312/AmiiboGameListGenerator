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
using System.Xml.Serialization;

namespace AmiiboGameList
{
    class Program
    {
        private static readonly Lazy<DBRootobjectInstance> lazy = new(() => new DBRootobjectInstance());

        public static DBRootobjectInstance BRootobject { get { return lazy.Value; } }

        static void Main()
        {
            // Check if amiibo.json is provided
            if (!File.Exists("amiibo.json"))
            {
                Console.WriteLine("Download and place the amiibo.json in the same folder as this exe");
                Console.ReadKey();
                return;
            }

            // Load Amiibo data
            BRootobject.rootobject = JsonConvert.DeserializeObject<DBRootobject>(File.ReadAllText(".\\amiibo.json").Trim());
            AmiiboKeyValue export = new();

            foreach (var entry in BRootobject.rootobject.amiibos)
            {
                entry.Value.ID = entry.Key;
            }

            // Load Wii U games
            List<GameInfo> WiiUGames = JsonConvert.DeserializeObject<List<GameInfo>>(Properties.Resources.WiiU);

            // Load 3DS games
            XmlSerializer serializer = new(typeof(DSreleases));
            byte[] byteArray = Encoding.UTF8.GetBytes(Properties.Resources.DS);
            MemoryStream stream = new(byteArray);
            List<DSreleasesRelease> DSGames = ((DSreleases)serializer.Deserialize(stream)).release.ToList();
            stream.Close();

            // Load Switch games
            WebClient client = new();
            client.Encoding = Encoding.UTF8;
            serializer = new XmlSerializer(typeof(Switchreleases));
            byteArray = Encoding.UTF8.GetBytes(client.DownloadString(@"http://nswdb.com/xml.php"));
            stream = new MemoryStream(byteArray);
            List<SwitchreleasesRelease> SwitchGames = ((Switchreleases)serializer.Deserialize(stream)).release.ToList();
            stream.Dispose();

            // List to keep track of missing games
            List<string> missingGames = new();

            // Counter to keep track of how many amiibos we've done
            int AmiiboCounter = 0;

            // Iterate over all Amiibo's and get game info
            Parallel.ForEach(BRootobject.rootobject.amiibos, DBamiibo =>
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
                    url = url.Insert(url.LastIndexOf('/') + 1, "cat-").Substring(0, url.Length);

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
                    foreach (var item in htmlDoc.DocumentNode.SelectNodes("//ul[@class='figures-cards small-block-grid-2 medium-block-grid-4 large-block-grid-4']/li"))
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
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No games found for " + DBamiibo.Value.Name);
                        return;
                    }

                    // Iterate over each game in the games panel
                    Parallel.ForEach(GamesPanel, node =>
                    {
                        // Get the name of the game
                        Game game = new()
                        {
                            gameName = node.SelectSingleNode(".//*[@class='name']/text()[normalize-space()]").InnerText.Trim().Replace("Poochy & ", "").Trim().Replace("Ace Combat Assault Horizon Legacy +", "Ace Combat Assault Horizon Legacy+").Replace("Power Pros", "Jikkyou Powerful Pro Baseball"),
                            gameID = new List<string>(),
                            amiiboUsage = new List<AmiiboUsage>()
                        };

                        // Get the amiibo usages
                        foreach (var amiiboUsage in node.SelectNodes(".//*[@class='features']/li"))
                        {
                            game.amiiboUsage.Add(new AmiiboUsage
                            {
                                Usage = amiiboUsage.GetDirectInnerText().Trim(),
                                write = amiiboUsage.SelectSingleNode("em").InnerText == "(Read+Write)"
                            });
                        }

                        if (DBamiibo.Value.Name == "Shadow Mewtwo")
                            game.gameName = "Pokkén Tournament";

                        // Add game to the correct console and get correct titleid
                        Regex rgx = new("[^a-zA-Z0-9 -]");
                        switch (node.SelectSingleNode(".//*[@class='name']/span").InnerText.Trim().ToLower())
                        {
                            case "switch":
                                try
                                {
                                    List<SwitchreleasesRelease> games = SwitchGames.FindAll(SwitchGame => rgx.Replace(WebUtility.HtmlDecode(SwitchGame.name).ToLower(), "").Contains(rgx.Replace(game.gameName.ToLower(), "")));
                                    if (games.Count == 0)
                                    {
                                        game.gameID = game.gameName switch
                                        {
                                            "Cyber Shadow" => new List<string> { "0100C1F0141AA000" },
                                            "Jikkyou Powerful Pro Baseball" => new List<string> { "0100E9C00BF28000" },
                                            "Super Kirby Clash" => new List<string> { "01003FB00C5A8000" },
                                            "Shovel Knight Showdown" => new List<string> { "0100B380022AE000" },
                                            _ => throw new Exception(),
                                        };
                                    }
                                    games.ForEach(SwitchGame =>
                                        game.gameID.Add(SwitchGame.titleid.Substring(0, 16)));

                                    game.gameID = game.gameID.Distinct().ToList();
                                    ExAmiibo.gamesSwitch.Add(game);
                                }
                                catch
                                {
                                    missingGames.Add(game.gameName + " (Switch)");
                                }
                                break;
                            case "wii u":
                                try
                                {
                                    string[] gameIDs = WiiUGames.Find(WiiUGame => WiiUGame.Name.Contains(game.gameName, StringComparer.OrdinalIgnoreCase)).Ids;
                                    if (gameIDs.Length == 0)
                                    {
                                        throw new Exception();
                                    }
                                    foreach (string ID in gameIDs)
                                    {
                                        game.gameID.Add(ID.Substring(0, 16));
                                    }
                                    game.gameID = game.gameID.Distinct().ToList();
                                    ExAmiibo.gamesWiiU.Add(game);
                                }
                                catch
                                {
                                    missingGames.Add(game.gameName + " (Wii U)");
                                }
                                break;
                            case "3ds":
                                try
                                {
                                    List<DSreleasesRelease> games = DSGames.FindAll(DSGame => rgx.Replace(WebUtility.HtmlDecode(DSGame.name).ToLower(), "").Contains(rgx.Replace(game.gameName.ToLower(), "")));
                                    if (games.Count == 0)
                                    {
                                        game.gameID = game.gameName switch
                                        {
                                            "Style Savvy: Styling Star" => new List<string> { "00040000001C2500" },
                                            "Metroid Prime: Blast Ball" => new List<string> { "0004000000175300" },
                                            "Mini Mario & Friends amiibo Challenge" => new List<string> { "000400000016C300", "000400000016C200" },
                                            "Team Kirby Clash Deluxe" => new List<string> { "00040000001AB900", "00040000001AB800" },
                                            "Kirby's Extra Epic Yarn" => new List<string> { "00040000001D1F00" },
                                            "Kirby's Blowout Blast" => new List<string> { "0004000000196F00" },
                                            "BYE-BYE BOXBOY!" => new List<string> { "00040000001B5400", "00040000001B5300" },
                                            "Azure Striker Gunvolt 2" => new List<string> { "00040000001A6E00" },
                                            "niconico app" => new List<string> { "0005000010116400" },
                                            _ => throw new Exception(),
                                        };
                                    }
                                    games.ForEach(DSGame =>
                                        game.gameID.Add(DSGame.titleid.Substring(0, 16)));

                                    game.gameID = game.gameID.Distinct().ToList();
                                    ExAmiibo.games3DS.Add(game);
                                }
                                catch
                                {
                                    missingGames.Add(game.gameName + " (3DS)");
                                }
                                break;
                            default:
                                break;
                        }
                    });
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(url);
                    Console.WriteLine(e);
                }

                // Sort all gamelists
                ExAmiibo.gamesSwitch.Sort();
                ExAmiibo.gamesWiiU.Sort();
                ExAmiibo.games3DS.Sort();

                // Add the amiibos to the export list
                export.amiibos.Add(DBamiibo.Key, ExAmiibo);

                // Show which amiibo just got added
                Console.ForegroundColor = ConsoleColor.White;
                AmiiboCounter++;
                Console.WriteLine($"{ AmiiboCounter.ToString("D3") }/{ BRootobject.rootobject.amiibos.Count } Done with { DBamiibo.Value.OriginalName } ({ DBamiibo.Value.amiiboSeries })");
            });

            // Sort everything
            Hex[] KeyArray = export.amiibos.Keys.ToArray();
            Array.Sort(KeyArray);
            Dictionary<Hex, Games> SortedAmiibos = new();
            foreach (var key in KeyArray)
            {
                SortedAmiibos.Add(key, export.amiibos[key]);
            }

            // Write the file
            File.WriteAllText("./games_info.json", JsonConvert.SerializeObject(SortedAmiibos, Formatting.Indented), Encoding.UTF8);

            // Inform we're done
            Console.WriteLine("\nDone generating the JSON!");

            // Show missing games
            if(missingGames.Count != 0)
            {
                Console.WriteLine("However, the following games could not be added to the database:");
                foreach (var Game in missingGames.Distinct())
                {
                    Console.WriteLine("\t" + Game);
                }
            }

        }
    }
}
