using AmiiboGameList.Utility;

namespace AmiiboGameList.Services;

public interface IHttpService
{
    Task<string> GetStringAsync(string url, int maxAttempts = 5);
    Task<byte[]> GetByteArrayAsync(string url, int maxAttempts = 5);
}

public class HttpService(ILogger logger) : IHttpService
{
    private static readonly HttpClient HttpClient = new();

    public async Task<string> GetStringAsync(string url, int maxAttempts = 5)
    {
        return await ExecuteHttpCall(async () => await HttpClient.GetStringAsync(url), url, maxAttempts);
    }

    public async Task<byte[]> GetByteArrayAsync(string url, int maxAttempts = 5)
    {
        return await ExecuteHttpCall(async () => await HttpClient.GetByteArrayAsync(url), url, maxAttempts);
    }

    private async Task<T> ExecuteHttpCall<T>(Func<Task<T>> action, string url, int maxAttempts)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (IsTransientHttpError(ex))
            {
                if (await HandleErrorAsync(i, maxAttempts, url, ex.Message, ex.StatusCode?.ToString() ?? "N/A"))
                    throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                if (await HandleErrorAsync(i, maxAttempts, url, "Timeout occurred.", "Timeout"))
                    throw;
            }
            catch (Exception ex)
            {
                logger.Log($"({i + 1}/{maxAttempts}) Non-retriable error for {url}: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        logger.Log($"All {maxAttempts} attempts failed for {url}.", LogLevel.Error);
        throw new HttpRequestException($"All {maxAttempts} attempts failed for {url}. Last error potentially masked.");
    }

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
        return ex.StatusCode.HasValue &&
               (((int)ex.StatusCode.Value >= 500 && (int)ex.StatusCode.Value < 600) ||
                ex.StatusCode.Value == System.Net.HttpStatusCode.RequestTimeout ||
                ex.StatusCode.Value == System.Net.HttpStatusCode.TooManyRequests);
    }

    private async Task<bool> HandleErrorAsync(int attempt, int maxAttempts, string url, string errorMessage, string errorCode)
    {
        logger.Log($"({attempt + 1}/{maxAttempts}) Error (Code: {errorCode}) while loading {url}: {errorMessage}", LogLevel.Error);

        if (attempt >= (maxAttempts - 1))
        {
            logger.Log($"Maximum retry attempts ({maxAttempts}) reached for {url}.", LogLevel.Error);
            return true;
        }

        TimeSpan delay = TimeSpan.FromSeconds((attempt + 1) * 5);
        logger.Log($"Retrying in {delay.TotalSeconds} seconds...", LogLevel.Verbose);
        await Task.Delay(delay);

        return false;
    }
}