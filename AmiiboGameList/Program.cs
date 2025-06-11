using AmiiboGameList.Models;
using AmiiboGameList.Services;
using AmiiboGameList.Utility;

using System.Text;
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
			config = ParseArguments(args, consoleLogger);
			consoleLogger.CurrentLogLevel = config.LoggingLevel;
		}
		catch (ArgumentException ex)
		{
			_logger.Log(ex.Message, LogLevel.Error);
			PrintUsage();
			return (int)ExitCode.ArgumentError;
		}

		_logger.Log("Application starting...");

		HttpService httpService = new(_logger);
		AmiiboJsonModel amiiboDb;

		try
		{
			amiiboDb = await LoadAmiiboDatabaseAsync(config.InputAmiiboDbPath, httpService);
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

	private static async Task<AmiiboJsonModel> LoadAmiiboDatabaseAsync(string inputPath, HttpService httpService)
	{
		_logger.Log("Loading Amiibo database...");
		string amiiboJson;
		if (string.IsNullOrEmpty(inputPath))
		{
			_logger.Log("Downloading Amiibo database from N3evin/AmiiboAPI...", LogLevel.Verbose);
			try
			{
				amiiboJson = await httpService.GetStringAsync("https://raw.githubusercontent.com/N3evin/AmiiboAPI/master/database/amiibo.json");
			}
			catch (Exception ex)
			{
				_logger.Log($"Error downloading amiibo.json: {ex.Message}", LogLevel.Error);
				throw new InvalidOperationException("Failed to download amiibo.json.", ex);
			}
		}
		else
		{
			_logger.Log($"Reading Amiibo database from local file: {inputPath}", LogLevel.Verbose);
			try
			{
				amiiboJson = await File.ReadAllTextAsync(inputPath);
			}
			catch (Exception ex)
			{
				_logger.Log($"Error reading local amiibo.json from {inputPath}: {ex.Message}", LogLevel.Error);
				throw new InvalidOperationException($"Failed to read local amiibo.json from {inputPath}.", ex);
			}
		}

		_logger.Log("Processing Amiibo database...", LogLevel.Verbose);
		try
		{
			AmiiboJsonModel db = JsonSerializer.Deserialize<AmiiboJsonModel>(amiiboJson);
			if (db?.Amiibos == null)
				throw new JsonException("Amiibo data is malformed or empty. Please check the input file or URL.");

			foreach (KeyValuePair<string, AmiiboEntry> kvp in db.Amiibos)
			{
				kvp.Value.Id = kvp.Key;
			}

			return db;
		}
		catch (JsonException ex)
		{
			_logger.Log($"Error deserializing amiibo.json: {ex.Message}", LogLevel.Error);
			throw new InvalidOperationException("Failed to parse amiibo.json.", ex);
		}
	}

	private static AppConfig ParseArguments(string[] args, ConsoleLogger tempLoggerForEarlyLogging)
	{
		AppConfig config = new();
		if (args.Contains("-h") || args.Contains("--help") || args.Contains("/?"))
		{
			PrintUsage();
			Environment.Exit((int)ExitCode.Success);
		}

		tempLoggerForEarlyLogging.Log($"Running with arguments: {string.Join(' ', args)}", LogLevel.Verbose);

		for (int i = 0; i < args.Length; i++)
		{
			string currentArg = args[i].ToLowerInvariant();
			switch (currentArg)
			{
				case "-i":
				case "-input":
					if (i + 1 < args.Length)
					{
						config.InputAmiiboDbPath = args[++i];
						if (!File.Exists(config.InputAmiiboDbPath))
							throw new ArgumentException($"Input file not found: {config.InputAmiiboDbPath}");
					}
					else
						throw new ArgumentException("Missing value for input argument.");
					break;
				case "-o":
				case "-output":
					if (i + 1 < args.Length)
					{
						config.OutputFilePath = args[++i];
						string dir = Path.GetDirectoryName(config.OutputFilePath);
						if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
							Directory.CreateDirectory(dir);
					}
					else
						throw new ArgumentException("Missing value for output argument.");
					break;
				case "-p":
				case "-parallelism":
					config.MaxParallelism = i + 1 < args.Length && int.TryParse(args[++i], out int pValue) && pValue > 0
						? pValue
						: throw new ArgumentException("Invalid value for parallelism argument. Must be a positive integer.");
					break;
				case "-l":
				case "-log":
					config.LoggingLevel = i + 1 < args.Length && Enum.TryParse(args[++i], true, out LogLevel logLevel)
						? logLevel
						: throw new ArgumentException("Invalid value for log level argument. Valid values: Verbose, Info, Warn, Error.");
					break;
				default:
					throw new ArgumentException($"Unknown argument: {args[i]}");
			}
		}

		tempLoggerForEarlyLogging.Log("Arguments parsed.", LogLevel.Verbose);
		return config;
	}

	private static void PrintUsage()
	{
		StringBuilder sb = new();
		sb.AppendLine("Amiibo Game List Generator");
		sb.AppendLine("Usage: AmiiboGameList.exe [options]");
		sb.AppendLine("Options:");
		sb.AppendLine("  -i, --input {filepath}    Specify local amiibo.json database path. (Optional, downloads if not provided)");
		sb.AppendLine("  -o, --output {filepath}   Specify output JSON file path. (Default: games_info.json)");
		sb.AppendLine("  -p, --parallelism {value} Specify max degree of parallelism for processing. (Default: 4)");
		sb.AppendLine("  -l, --log {level}         Set logging level (Verbose, Info, Warn, Error). (Default: Info)");
		sb.AppendLine("  -h, --help                Show this help message.");
		_logger.Log(sb.ToString());
	}
}