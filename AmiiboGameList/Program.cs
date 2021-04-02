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
            if (!File.Exists("amiibo.json"))
            {
                Console.WriteLine("Download and place the amiibo.json in the same folder as this exe");
                Console.ReadKey();
                return;
            }
            Regex rgx = new("[^a-zA-Z0-9 -]");
            // Amiibo data
            WebClient client = new();
            client.Encoding = Encoding.UTF8;
            BRootobject.rootobject = JsonConvert.DeserializeObject<DBRootobject>(File.ReadAllText(".\\amiibo.json").Trim());
            AmiiboKeyValue export = new();

            foreach (var entry in BRootobject.rootobject.amiibos)
            {
                entry.Value.ID = entry.Key;
            }
            // Wii U
            List<GameInfo> WiiUGames = JsonConvert.DeserializeObject<List<GameInfo>>(Properties.Resources.WiiU);
            // 3DS
            XmlSerializer serializer = new(typeof(DSreleases));
            byte[] byteArray = Encoding.UTF8.GetBytes(Properties.Resources.DS);
            MemoryStream stream = new(byteArray);
            List<DSreleasesRelease> DSGames = ((DSreleases)serializer.Deserialize(stream)).release.ToList();
            stream.Close();
            // Switch
            serializer = new XmlSerializer(typeof(Switchreleases));
            byteArray = Encoding.UTF8.GetBytes(client.DownloadString(@"http://nswdb.com/xml.php"));
            stream = new MemoryStream(byteArray);
            List<SwitchreleasesRelease> SwitchGames = ((Switchreleases)serializer.Deserialize(stream)).release.ToList();
            stream.Close();

            Parallel.ForEach(BRootobject.rootobject.amiibos, DBamiibo =>
            {
                WebClient AmiiboClient = new();
                Games ExAmiibo = new();
                ExAmiibo.gamesSwitch = new List<Game>();
                ExAmiibo.games3DS = new List<Game>();
                ExAmiibo.gamesWiiU = new List<Game>();
                string url = "";
                url = "https://amiibo.life/amiibo/" + DBamiibo.Value.amiiboSeries.ToLower()
                                                                .Replace("super mario bros", "super mario")
                                                                .Replace("monster hunter", "monster hunter stories")
                                                                .Replace("legend of zelda", "the legend of zelda")
                                                                .Replace("skylanders", "skylanders superchargers")
                                                                .Replace("8-bit mario", "super-mario-bros-30th-anniversary")
                                                                .Replace("yoshi's woolly world", "yoshi-s-woolly-world")
                                                                .Replace("monster hunter stories rise", "monster-hunter-rise")
                                                                .Trim().Replace(" ", "-").Replace("!", "").Replace(".", "") + "/";
                url += Regex.Replace(DBamiibo.Value.name, @"[®™\n\u2122\-()!.]", "").Replace(" ", "-").Replace("--", "-").Replace("'", "-").ToLower()
                    .Replace("slider", "")
                    .Replace("8bit-link", "link-the-legend-of-zelda")
                    .Replace("8bit-mario-modern-color", "mario-modern-colors")
                    .Replace("midna-&-wolf-link", "wolf-link")
                    .Replace("mr-game-&-watch", "mr-game-watch")
                    .Replace("oneeyed", "one-eyed")
                    .Replace("pacman", "pac-man")
                    .Replace("rob-nes", "r-o-b-nes")
                    .Replace("rob-famicom", "r-o-b-famicom")
                    .Replace("-&-luma", "")
                    .Replace("k-k-", "k-k")
                    .Replace("timmy-&-tommy", "timmy-tommy")
                    .Replace("toon-zelda-the-wind-waker", "zelda-the-wind-waker").Trim().Replace(" ", "-")
                    .Replace("8bit-mario-classic-color", "mario-classic-colors")
                    .Replace("banjo-&-kazooie", "banjo-kazooie");
                if (url.EndsWith("cat"))
                    url = url.Insert(url.LastIndexOf('/') + 1, "cat-").Substring(0, url.Length);
                if (DBamiibo.Value.type == "Card" && DBamiibo.Value.amiiboSeries == "Animal Crossing")
                {
                    url = null;
                    HtmlDocument htmlDoc = new();
                    htmlDoc.LoadHtml(
                        WebUtility.HtmlDecode(
                            AmiiboClient.DownloadString("https://amiibo.life/search?q=" + DBamiibo.Value.characterName)
                            )
                        );
                    foreach (var item in htmlDoc.DocumentNode.SelectNodes("//ul[@class='figures-cards small-block-grid-2 medium-block-grid-4 large-block-grid-4']/li"))
                    {
                        if (item.ChildNodes[1].GetAttributeValue("href", string.Empty).Contains("cards"))
                        {
                            url = "https://amiibo.life" + item.ChildNodes[1].GetAttributeValue("href", string.Empty);
                            break;
                        }
                    }
                }
                switch (url)
                {
                    case "https://amiibo.life/amiibo/others/super-mario-cereal":
                        url = "https://amiibo.life/amiibo/super-mario-cereal/super-mario-cereal";
                        break;

                    case "https://amiibo.life/amiibo/others/solaire-of-astora":
                        url = "https://amiibo.life/amiibo/dark-souls/solaire-of-astora";
                        break;
                    case "https://amiibo.life/amiibo/animal-crossing/isabelle--winter":
                        url = "https://amiibo.life/amiibo/animal-crossing/isabelle-winter-outfit";
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
                    try
                    {
                        htmlDoc.DocumentNode.SelectNodes("//*[@class='games panel']/a").First();
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("No games found for " + DBamiibo.Value.name);
                        return;
                    }
                    List<Game> consoleGames = new();
                    Parallel.ForEach(htmlDoc.DocumentNode.SelectNodes("//*[@class='games panel']/a"), node =>
                    {
                        Game game = new()
                        {
                            gameName = node.SelectSingleNode(".//*[@class='name']/text()[normalize-space()]").InnerText.Trim().Replace("Poochy & ", "").Trim().Replace("Ace Combat Assault Horizon Legacy +", "Ace Combat Assault Horizon Legacy+").Replace("Power Pros", "Jikkyou Powerful Pro Baseball"),
                            gameID = new List<string>(),
                            amiiboUsage = new List<AmiiboUsage>()
                        };

                        foreach (var amiiboUsage in node.SelectNodes(".//*[@class='features']/li"))
                        {
                            game.amiiboUsage.Add(new AmiiboUsage
                            {
                                Usage = amiiboUsage.GetDirectInnerText().Trim(),
                                write = amiiboUsage.SelectSingleNode("em").InnerText == "(Read+Write)"
                            });
                        }

                        if (DBamiibo.Value.name == "Shadow Mewtwo")
                            game.gameName = "Pokkén Tournament";
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
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Game not found: " + game.gameName + " (Switch)");
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
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Game not found: " + game.gameName + " (Wii U)");
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
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Game not found: " + game.gameName + " (3DS)");
                                }
                                break;
                            default:
                                break;
                        }
                    });
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(url);
                }
                export.amiibos.Add(DBamiibo.Key, ExAmiibo);
            });
            File.WriteAllText("./games_info.json", JsonConvert.SerializeObject(export, Formatting.Indented), Encoding.UTF8);
        }
    }
}
