namespace AmiiboGameList.Utility;

public enum LogLevel
{
    Verbose,
    Info,
    Warn,
    Error
}

public interface ILogger
{
    void Log(string message, LogLevel severity = LogLevel.Info);
    LogLevel CurrentLogLevel { get; set; }
}

public class ConsoleLogger : ILogger
{
    public LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;
    private readonly object _lock = new();

    /// <summary>
    /// Logs a message to the console with the specified severity level.
    /// </summary>
    /// <remarks>The message is logged only if the specified severity level is greater than or equal to the
    /// current log level. The console text color is adjusted based on the severity level to visually distinguish log
    /// messages.</remarks>
    /// <param name="message">The message to log. Cannot be null or empty.</param>
    /// <param name="severity">The severity level of the log message. Defaults to <see cref="LogLevel.Info"/> if not specified.</param>
    public void Log(string message, LogLevel severity = LogLevel.Info)
    {
        if (severity >= CurrentLogLevel)
        {
            lock (_lock)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                switch (severity)
                {
                    case LogLevel.Verbose:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case LogLevel.Info:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case LogLevel.Warn:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                }

                Console.WriteLine(message);
                Console.ForegroundColor = originalColor;
            }
        }
    }
}