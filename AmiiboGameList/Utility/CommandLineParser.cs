namespace AmiiboGameList.Utility;

public static class CommandLineParser
{
	/// <summary>
	/// Parses command-line arguments and returns an <see cref="AppConfig"/> object containing the configuration
	/// settings.
	/// </summary>
	/// <remarks>If the arguments include <c>-h</c>, <c>--help</c>, or <c>/?</c>, the method prints usage
	/// information to the console and terminates the application.</remarks>
	/// <param name="args">An array of command-line arguments to parse.</param>
	/// <param name="tempLoggerForEarlyLogging">A temporary logger used for logging messages during the argument parsing process.</param>
	/// <returns>An <see cref="AppConfig"/> object populated with the parsed configuration settings, such as input file path,
	/// output file path,  maximum parallelism, and logging level.</returns>
	/// <exception cref="ArgumentException">Thrown if an unknown argument is encountered or if a required value is missing for an argument.</exception>
	public static AppConfig ParseArguments(string[] args, ConsoleLogger tempLoggerForEarlyLogging)
    {
        AppConfig config = new();
        if (args.Contains("-h") || args.Contains("--help") || args.Contains("/?"))
        {
            PrintUsage(tempLoggerForEarlyLogging);
            Environment.Exit(0);
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

    /// <summary>
    /// Prints the usage instructions for the Amiibo Game List Generator application.
    /// </summary>
    /// <remarks>The usage instructions include details about the application's purpose, command-line options,
    /// and their default values. This method is typically called to inform users about how to use the  application,
    /// especially when incorrect or insufficient arguments are provided.</remarks>
    /// <param name="logger">The <see cref="ConsoleLogger"/> instance used to output the usage instructions.</param>
    public static void PrintUsage(ConsoleLogger logger)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Amiibo Game List Generator");
        sb.AppendLine("Usage: AmiiboGameList.exe [options]");
        sb.AppendLine("Options:");
        sb.AppendLine("  -i, --input {filepath}    Specify local amiibo.json database path. (Optional, downloads if not provided)");
        sb.AppendLine("  -o, --output {filepath}   Specify output JSON file path. (Default: games_info.json)");
        sb.AppendLine("  -p, --parallelism {value} Specify max degree of parallelism for processing. (Default: 4)");
        sb.AppendLine("  -l, --log {level}         Set logging level (Verbose, Info, Warn, Error). (Default: Info)");
        sb.AppendLine("  -h, --help                Show this help message.");
        logger.Log(sb.ToString());
    }
}