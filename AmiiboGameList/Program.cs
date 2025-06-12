using AmiiboGameList.Models;
using AmiiboGameList.Services;
using AmiiboGameList.Utility;

using System.Text.Json;

namespace AmiiboGameList;

public enum ExitCode
{
    Success = 0,
    SuccessWithErrors = 1,
    UnknownError = -1,
    NetworkError = -2,
    DatabaseLoadingError = -3,
    ArgumentError = -4
}

public class Program
{
    private static ConsoleLogger _logger;

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> Main(string[] args)
    {
        ConsoleLogger consoleLogger = new();
        _logger = consoleLogger;

        AppConfig config;
        try
        {
            config = CommandLineParser.ParseArguments(args, consoleLogger);
            consoleLogger.CurrentLogLevel = config.LoggingLevel;
        }
        catch (ArgumentException ex)
        {
            _logger.Log(ex.Message, LogLevel.Error);
            CommandLineParser.PrintUsage(_logger);
            return (int)ExitCode.ArgumentError;
        }

        _logger.Log("Application starting...");

        HttpService httpService = new(_logger);
        AmiiboJsonModel amiiboDb;

        try
        {
            amiiboDb = await AmiiboDatabaseLoader.LoadAmiiboDatabaseAsync(config.InputAmiiboDbPath, httpService, _logger);
        }
        catch (Exception ex)
        {
            _logger.Log($"Critical error loading Amiibo database: {ex.Message}", LogLevel.Error);
            return (int)ExitCode.DatabaseLoadingError;
        }

        GameDataService gameDataService = new(_logger, httpService);
        try
        {
            await gameDataService.LoadAllGameDataAsync();
        }
        catch (Exception ex)
        {
            _logger.Log($"Critical error loading game databases: {ex.Message}", LogLevel.Error);
            return (int)ExitCode.DatabaseLoadingError;
        }

        _logger.Log("All databases loaded successfully.");

        AmiiboDataService amiiboDataService = new(amiiboDb, httpService, _logger);
        AmiiboInfoCollectorService amiiboInfoCollector = new(httpService, _logger, gameDataService, amiiboDataService);

        Dictionary<string, GameCompatibility> processedAmiibos = [];
        List<string> missingGameTracker = [];

        int amiiboCounter = 0;
        int totalAmiiboCount = amiiboDb.Amiibos.Count;

        _logger.Log($"Processing {totalAmiiboCount} Amiibo entries...");

        List<Task> tasks = [];
        SemaphoreSlim semaphore = new(config.MaxParallelism);

        foreach (KeyValuePair<string, AmiiboEntry> amiiboEntryPair in amiiboDb.Amiibos)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    AmiiboDetails amiiboDetails = amiiboDataService.GetAmiiboDetails(amiiboEntryPair.Value);
                    GameCompatibility gameCompat = await amiiboInfoCollector.CollectGameCompatibilityAsync(amiiboDetails, missingGameTracker);

                    lock (processedAmiibos)
                    {
                        processedAmiibos.Add(amiiboEntryPair.Key, gameCompat);
                    }

                    Interlocked.Increment(ref amiiboCounter);
                    _logger.Log($"{amiiboCounter:D3}/{totalAmiiboCount} Processed: {amiiboDetails.OriginalName} ({amiiboDetails.AmiiboSeries})");
                }
                catch (HttpRequestException netEx)
                {
                    _logger.Log($"Network error processing Amiibo {amiiboEntryPair.Value.Name}: {netEx.Message}. This Amiibo might be skipped.", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Unexpected error processing Amiibo {amiiboEntryPair.Value.Name}: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        _logger.Log("Amiibo processing complete.");

        AmiiboGameSet outputData = new()
        {
            Amiibos = processedAmiibos.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        try
        {
            string jsonOutput = JsonSerializer.Serialize(outputData, jsonOptions);
            File.WriteAllText(config.OutputFilePath, jsonOutput.Replace("  ", "\t"));
            _logger.Log($"Output successfully written to {config.OutputFilePath}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error writing output JSON: {ex.Message}", LogLevel.Error);
            return (int)ExitCode.UnknownError;
        }

        if (missingGameTracker.Count != 0)
        {
            _logger.Log("The following games could not find their Title IDs and were not fully processed:", LogLevel.Warn);
            foreach (string game in missingGameTracker.Distinct().OrderBy(g => g))
            {
                _logger.Log($"\t- {game}", LogLevel.Warn);
            }

            return (int)ExitCode.SuccessWithErrors;
        }

        _logger.Log("Application finished successfully.");
        return (int)ExitCode.Success;
    }
}