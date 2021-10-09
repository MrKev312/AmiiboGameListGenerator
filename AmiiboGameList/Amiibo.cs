using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AmiiboGameList
{

    public class AmiiboKeyValue
    {
        public Dictionary<Hex, Games> amiibos = new();
    }

    public class DBRootobjectInstance
    {
        public DBRootobject rootobject;
    }

    public class DBRootobject
    {
        public Dictionary<string, string> amiibo_series = new();
        public Dictionary<Hex, DBAmiibo> amiibos = new();
        public Dictionary<string, string> characters = new();
        public Dictionary<string, string> game_series = new();
        public Dictionary<string, string> types = new();
    }

    public class DBAmiibo
    {
        public string OriginalName;
        public Release release;
        public Hex ID;

        public string Name {
            get
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
            }
            set => OriginalName = value;
        }

        public string characterName
        {
            get
            {
                string CharacterName = Program.BRootobject.rootobject.characters["0x" + ID.ToString().ToLower().Substring(2, 4)];
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

        public string amiiboSeries
        {
            get
            {
                string ID = "0x" + this.ID.ToString().Substring(14, 2);
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
        public string type
        {
            get
            {
                string Type = Program.BRootobject.rootobject.types["0x" + ID.ToString().Substring(8, 2)];
                return Type;
            }
        }
    }

    public class Games
    {
        public Games()
        {
            games3DS = new();
            gamesWiiU = new();
            gamesSwitch = new();
        }

        public List<Game> games3DS { get; set; }
        public List<Game> gamesWiiU { get; set; }
        public List<Game> gamesSwitch { get; set; }
    }

    public class Game : IComparable<Game>
    {
        public string gameName { get; set; }
        public List<string> gameID { get; set; }
        public List<AmiiboUsage> amiiboUsage { get; set; }

        public int CompareTo(Game other)
        {
            return gameName.CompareTo(other.gameName);
        }
    }

    public class AmiiboUsage
    {
        public string Usage;
        public bool write;
    }

    public class Release
    {
        public string au { get; set; }
        public string eu { get; set; }
        public string jp { get; set; }
        public string na { get; set; }
    }

}
