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