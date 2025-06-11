using AmiiboGameList.Utility;

namespace AmiiboGameList;

public class AppConfig
{
    public string InputAmiiboDbPath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = "games_info.json";
    public int MaxParallelism { get; set; } = 4;
    public LogLevel LoggingLevel { get; set; } = LogLevel.Info;
}