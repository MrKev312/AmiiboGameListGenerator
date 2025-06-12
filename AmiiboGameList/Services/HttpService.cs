using AmiiboGameList.Utility;

namespace AmiiboGameList.Services;

/// <summary>
/// Defines methods for performing HTTP GET requests to retrieve data as strings or byte arrays.
/// </summary>
/// <remarks>This interface provides asynchronous methods for making HTTP GET requests with retry logic. 
/// Implementations should handle retries up to the specified maximum number of attempts in case of transient
/// failures.</remarks>
public interface IHttpService
{
    /// <summary>
    /// Asynchronously retrieves the content of the specified URL as a string.
    /// </summary>
    /// <remarks>This method performs an HTTP GET request to the specified URL and returns the response
    /// content  as a string. If the request fails due to transient network issues, it will retry up to  <paramref
    /// name="maxAttempts"/> times before throwing an exception.</remarks>
    /// <param name="url">The URL of the resource to retrieve. This parameter cannot be null or empty.</param>
    /// <param name="maxAttempts">The maximum number of retry attempts in case of transient failures. Must be greater than 0.  The default value
    /// is 5.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the content of the  resource as a
    /// string.</returns>
    Task<string> GetStringAsync(string url, int maxAttempts = 5);

    /// <summary>
    /// Asynchronously retrieves the content of the specified URL as a byte array.
    /// </summary>
    /// <remarks>This method performs retries for transient network errors, up to the specified <paramref
    /// name="maxAttempts"/>. If the operation fails after all attempts, an exception is thrown.</remarks>
    /// <param name="url">The URL of the resource to retrieve. Must be a valid, non-null, and non-empty string.</param>
    /// <param name="maxAttempts">The maximum number of retry attempts in case of transient failures. Must be greater than or equal to 1. Defaults
    /// to 5.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the content of the URL as a byte
    /// array.</returns>
    Task<byte[]> GetByteArrayAsync(string url, int maxAttempts = 5);
}

/// Provides methods for making HTTP requests with built-in retry logic for transient errors.
/// </summary>
/// <remarks>This service is designed to handle HTTP requests with automatic retries for transient errors, such as
/// server errors (HTTP 5xx), timeouts, or rate-limiting responses (HTTP 429). The retry logic includes exponential
/// backoff between attempts and logs detailed information about each attempt.</remarks>
/// <param name="logger">An instance of <see cref="ILogger"/> for logging operations and errors.</param>
public class HttpService(ILogger logger) : IHttpService
{
    private static readonly HttpClient HttpClient = new();

	/// <inheritdoc cref="IHttpService.GetStringAsync(string, int)"/>
	public async Task<string> GetStringAsync(string url, int maxAttempts = 5)
    {
        return await ExecuteHttpCall(async () => await HttpClient.GetStringAsync(url), url, maxAttempts);
    }

	/// <inheritdoc cref="IHttpService.GetByteArrayAsync(string, int)"/>
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