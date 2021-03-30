using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AmiiboGameList
{

    public class AmiiboKeyValue
    {
        public Dictionary<string, Games> amiibos = new();
    }

    public class DBRootobjectInstance
    {
        public DBRootobject rootobject;
    }

    public class DBRootobject
    {
        public Dictionary<string, string> amiibo_series = new();
        public Dictionary<string, DBAmiibo> amiibos = new();
        public Dictionary<string, string> characters = new();
        public Dictionary<string, string> game_series = new();
        public Dictionary<string, string> types = new();
    }

    public class DBAmiibo
    {
        private string iD;
        public string name;
        public Release release;

        public string ID { get => iD; set => iD = value.Replace("0x", ""); }

        public string characterName
        {
            get
            {
                string CharacterName = Program.BRootobject.rootobject.characters["0x" + iD.Substring(0, 4)];
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
                string ID = "0x" + iD.Substring(12, 2);
                string AmiiboSeries = Program.BRootobject.rootobject.amiibo_series[ID];
                return AmiiboSeries;
            }
        }
        public string type
        {
            get
            {
                string Type = Program.BRootobject.rootobject.types["0x" + iD.Substring(6, 2)];
                return Type;
            }
        }
    }

    public class Games
    {
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
