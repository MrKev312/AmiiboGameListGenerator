using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AmiiboGameList;

/// <summary>Class to be JSONified and exported.</summary>
public class AmiiboKeyValue
{
    public Dictionary<Hex, Games> amiibos = new();
}

public class DBRootobjectInstance
{
    public DBRootobject rootobject;
}

/// <summary>Class to map all the database data to.</summary>
public class DBRootobject
{
    public Dictionary<string, string> amiibo_series = new();
    public Dictionary<Hex, DBAmiibo> amiibos = new();
    public Dictionary<string, string> characters = new();
    public Dictionary<string, string> game_series = new();
    public Dictionary<string, string> types = new();
}

/// <summary>Amiibo class for amiibo from the database.</summary>
public class DBAmiibo
{
    public string OriginalName;
    public Hex ID;
    private readonly Lazy<string> name;
    private readonly Lazy<string> url;

    public DBAmiibo()
    {
        name = new Lazy<string>(() =>
        {
            string ReturnName = OriginalName switch
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
                _ => OriginalName
            };

            ReturnName = ReturnName.Replace("Slider", "");
            ReturnName = ReturnName.Replace("R.O.B.", "R O B");

            ReturnName = ReturnName.Replace(".", "");
            ReturnName = ReturnName.Replace("'", " ");
            ReturnName = ReturnName.Replace("\"", "");

            ReturnName = ReturnName.Replace(" & ", " ");
            ReturnName = ReturnName.Replace(" - ", " ");

            return ReturnName.Trim();
        });
        url = new Lazy<string>(() =>
        {
            string url = default;
            // If the amiibo is an animal crossing card, look name up on site and get the first link
            if (type == "Card" && amiiboSeries == "Animal Crossing")
            {
                // Look amiibo up
                HtmlDocument AmiiboLookup = new();
                AmiiboLookup.LoadHtml(
                    WebUtility.HtmlDecode(
                        new HttpClient().GetStringAsync("https://amiibo.life/search?q=" + characterName).Result
                        )
                    );

                // Filter for card amiibo only and get url
                foreach (HtmlNode item in AmiiboLookup.DocumentNode.SelectNodes("//ul[@class='figures-cards small-block-grid-2 medium-block-grid-4 large-block-grid-4']/li"))
                {
                    if (item.ChildNodes[1].GetAttributeValue("href", string.Empty).Contains("cards"))
                    {
                        url = "https://amiibo.life" + item.ChildNodes[1].GetAttributeValue("href", string.Empty);
                        break;
                    }
                }

                return url;
            }
            else
            {
                // Handle amiibo where gameseries is set to others
                switch (Name.ToLower())
                {
                    case "super mario cereal":
                        return "https://amiibo.life/amiibo/super-mario-cereal/super-mario-cereal";

                    case "solaire of astora":
                        return "https://amiibo.life/amiibo/dark-souls/solaire-of-astora";

                    default:
                        string GameSeriesURL = amiiboSeries.ToLower();

                        // Regex to cleanup url
                        GameSeriesURL = Regex.Replace(GameSeriesURL, @"[!.]", "");
                        GameSeriesURL = Regex.Replace(GameSeriesURL, @"[' ]", "-");

                        url = $"https://amiibo.life/amiibo/{GameSeriesURL}/{Name.Replace(" ", "-").ToLower()}";

                        // Handle cat in getter for name
                        if (url.EndsWith("cat"))
                        {
                            url = url.Insert(url.LastIndexOf('/') + 1, "cat-")[..url.Length];
                        }

                        return url;
                }
            }
        });
    }

    public string URL => url.Value;

    /// <summary>Gets or sets the name.</summary>
    /// <value>The name.</value>
    public string Name
    {
        get => name.Value;
        set => OriginalName = value;
    }

    /// <summary>Gets the name of the character.</summary>
    /// <value>The name of the character.</value>
    public string characterName
    {
        get
        {
            string CharacterName = Program.BRootobject.rootobject.characters[$"0x{ID.ToString().ToLower().Substring(2, 4)}"];
            switch (CharacterName)
            {
                case "Spork/Crackle":
                    CharacterName = "Spork";
                    break;
                case "OHare":
                    CharacterName = "O'Hare";
                    break;
                default:
                    break;
            }

            return CharacterName;
        }
    }

    /// <summary>Gets the amiibo series.</summary>
    /// <value>The amiibo series.</value>
    public string amiiboSeries
    {
        get
        {
            string ID = $"0x{this.ID.ToString().Substring(14, 2)}";
            string AmiiboSeries = Program.BRootobject.rootobject.amiibo_series[ID.ToLower()];

            return AmiiboSeries switch
            {
                "Super Mario Bros." => "Super Mario",
                "Monster Hunter" => "Monster Hunter Stories",
                "Legend Of Zelda" => "The Legend Of Zelda",
                "Skylanders" => "Skylanders Superchargers",
                "8-bit Mario" => "Super Mario Bros 30th Anniversary",
                "Monster Sunter Stories Rise" => "Monster Hunter Rise",
                "Yu-Gi-Oh!" => "Yu-Gi-Oh! Rush Duel Saikyo Battle Royale",
                _ => AmiiboSeries,
            };
        }
    }
    /// <summary>Gets the type.</summary>
    /// <value>The type.</value>
    public string type
    {
        get
        {
            string Type = Program.BRootobject.rootobject.types[$"0x{ID.ToString().Substring(8, 2)}"];
            return Type;
        }
    }
}
