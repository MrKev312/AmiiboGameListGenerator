using AmiiboGameList.Utility;

namespace AmiiboGameList;

/// <summary>
/// Represents the configuration settings for the application.
/// </summary>
/// <remarks>This class provides properties to configure input and output file paths,  the level of parallelism
/// for processing, and the logging level used by the application.</remarks>
public class AppConfig
{
    public string InputAmiiboDbPath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = "games_info.json";
    public int MaxParallelism { get; set; } = 4;
    public LogLevel LoggingLevel { get; set; } = LogLevel.Info;
}