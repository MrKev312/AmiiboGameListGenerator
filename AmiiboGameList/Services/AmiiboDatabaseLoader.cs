using AmiiboGameList.Models;
using AmiiboGameList.Utility;

using System.Text.Json;

namespace AmiiboGameList.Services;

public static class AmiiboDatabaseLoader
{
    /// <summary>
    /// Loads the Amiibo database from a specified local file or a remote URL.
    /// </summary>
    /// <remarks>If <paramref name="inputPath"/> is provided, the method attempts to read the Amiibo database
    /// from the specified file. Otherwise, it downloads the database from the default URL. The method validates and
    /// processes the JSON data to ensure it is correctly formatted and assigns unique IDs to each Amiibo
    /// entry.</remarks>
    /// <param name="inputPath">The path to the local file containing the Amiibo database in JSON format. If null or empty, the database will be
    /// downloaded from the default remote URL.</param>
    /// <param name="httpService">An instance of <see cref="HttpService"/> used to download the Amiibo database when <paramref name="inputPath"/>
    /// is not provided.</param>
    /// <param name="logger">Logger for status and error messages.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="AmiiboJsonModel"/> 
    /// object representing the loaded Amiibo database.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the database cannot be downloaded from the remote URL, the local file cannot be read, or the JSON data
    /// is malformed or empty.</exception>
    public static async Task<AmiiboJsonModel> LoadAmiiboDatabaseAsync(string inputPath, HttpService httpService, ConsoleLogger logger)
    {
        logger.Log("Loading Amiibo database...");
        string amiiboJson;
        if (string.IsNullOrEmpty(inputPath))
        {
            logger.Log("Downloading Amiibo database from N3evin/AmiiboAPI...", LogLevel.Verbose);
            try
            {
                amiiboJson = await httpService.GetStringAsync("https://raw.githubusercontent.com/N3evin/AmiiboAPI/master/database/amiibo.json");
            }
            catch (Exception ex)
            {
                logger.Log($"Error downloading amiibo.json: {ex.Message}", LogLevel.Error);
                throw new InvalidOperationException("Failed to download amiibo.json.", ex);
            }
        }
        else
        {
            logger.Log($"Reading Amiibo database from local file: {inputPath}", LogLevel.Verbose);
            try
            {
                amiiboJson = await File.ReadAllTextAsync(inputPath);
            }
            catch (Exception ex)
            {
                logger.Log($"Error reading local amiibo.json from {inputPath}: {ex.Message}", LogLevel.Error);
                throw new InvalidOperationException($"Failed to read local amiibo.json from {inputPath}.", ex);
            }
        }

        logger.Log("Processing Amiibo database...", LogLevel.Verbose);
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
            logger.Log($"Error deserializing amiibo.json: {ex.Message}", LogLevel.Error);
            throw new InvalidOperationException("Failed to parse amiibo.json.", ex);
        }
    }
}