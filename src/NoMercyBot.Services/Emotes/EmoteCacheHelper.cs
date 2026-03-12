using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Services.Emotes;

/// <summary>
/// Provides file-based caching for emote/badge data so services can fall back
/// to a cached copy when the upstream API is unavailable.
/// </summary>
public static class EmoteCacheHelper
{
    private static string GetCacheFilePath(string key) =>
        Path.Combine(AppFiles.CachePath, $"{key}.json");

    /// <summary>
    /// Saves data to a JSON cache file.
    /// </summary>
    public static void Save<T>(string key, T data, ILogger logger)
    {
        try
        {
            string path = GetCacheFilePath(key);
            string json = JsonConvert.SerializeObject(data);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write emote cache for {Key}", key);
        }
    }

    /// <summary>
    /// Loads data from a JSON cache file, or returns default if not available.
    /// </summary>
    public static T? Load<T>(string key, ILogger logger)
    {
        try
        {
            string path = GetCacheFilePath(key);
            if (!File.Exists(path)) return default;

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read emote cache for {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Executes a fetch action with retries. On total failure, falls back to the file cache.
    /// Returns the fetched list on success, or the cached list on failure (empty list if no cache exists).
    /// </summary>
    public static async Task<List<T>> FetchWithRetryAndCache<T>(
        string cacheKey,
        Func<Task<List<T>>> fetchAction,
        ILogger logger,
        int maxRetries = 3,
        int baseDelayMs = 2000)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                List<T> result = await fetchAction();
                if (result.Count > 0)
                {
                    Save(cacheKey, result, logger);
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Attempt {Attempt}/{Max} failed for {CacheKey}: {Message}",
                    attempt, maxRetries, cacheKey, ex.Message);
            }

            if (attempt < maxRetries)
            {
                int delay = baseDelayMs * (1 << (attempt - 1)); // exponential backoff
                logger.LogInformation("Retrying {CacheKey} in {Delay}ms...", cacheKey, delay);
                await Task.Delay(delay);
            }
        }

        // All retries failed — try cache
        logger.LogWarning("All {Max} fetch attempts failed for {CacheKey}, falling back to file cache", maxRetries, cacheKey);
        List<T>? cached = Load<List<T>>(cacheKey, logger);
        if (cached is { Count: > 0 })
        {
            logger.LogInformation("Loaded {Count} cached items for {CacheKey}", cached.Count, cacheKey);
            return cached;
        }

        logger.LogWarning("No cache available for {CacheKey}, returning empty list", cacheKey);
        return [];
    }
}
