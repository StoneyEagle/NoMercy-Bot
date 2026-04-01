using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Services.TTS.Services;

public class TtsCacheService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public TtsCacheService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Generates a SHA256 hash from text content and voice ID for cache key
    /// </summary>
    public string GenerateContentHash(string textContent, string voiceId)
    {
        string combined = $"{textContent}|{voiceId}";
        byte[] bytes = Encoding.UTF8.GetBytes(combined);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Checks if a cached TTS file exists for the given text and voice combination
    /// </summary>
    public async Task<TtsCacheEntry?> GetCachedEntryAsync(
        string textContent,
        string voiceId,
        CancellationToken cancellationToken = default
    )
    {
        string contentHash = GenerateContentHash(textContent, voiceId);

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        TtsCacheEntry? cacheEntry = await db.TtsCacheEntries.FirstOrDefaultAsync(
            x => x.ContentHash == contentHash,
            cancellationToken
        );

        if (cacheEntry != null)
        {
            // Check if the file still exists on disk
            if (File.Exists(cacheEntry.FilePath))
            {
                // Update access tracking
                cacheEntry.LastAccessedAt = DateTime.UtcNow;
                cacheEntry.AccessCount++;
                await db.SaveChangesAsync(cancellationToken);

                return cacheEntry;
            }
            else
            {
                // File no longer exists, remove from cache
                db.TtsCacheEntries.Remove(cacheEntry);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new cache entry for synthesized TTS audio
    /// </summary>
    public async Task<TtsCacheEntry> CreateCacheEntryAsync(
        string textContent,
        string voiceId,
        string provider,
        byte[] audioBytes,
        decimal cost,
        CancellationToken cancellationToken = default
    )
    {
        string contentHash = GenerateContentHash(textContent, voiceId);
        string fileName = $"tts_cache_{contentHash}.wav";
        string filePath = Path.Combine(AppFiles.CachePath, "tts", "cached", fileName);

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

        // Write audio file to disk
        await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);

        // Create cache entry in database
        TtsCacheEntry cacheEntry = new()
        {
            ContentHash = contentHash,
            TextContent = textContent,
            VoiceId = voiceId,
            Provider = provider,
            FilePath = filePath,
            FileSize = audioBytes.Length,
            CharacterCount = textContent.Length,
            Cost = cost,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 1,
        };

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        db.TtsCacheEntries.Add(cacheEntry);
        await db.SaveChangesAsync(cancellationToken);

        return cacheEntry;
    }

    /// <summary>
    /// Gets the file path for a cached TTS entry
    /// </summary>
    public string GetCacheFilePath(string contentHash)
    {
        string fileName = $"tts_cache_{contentHash}.wav";
        return Path.Combine(AppFiles.CachePath, "tts", "cached", fileName);
    }

    /// <summary>
    /// Cleans up old cache entries based on age and access frequency
    /// </summary>
    public async Task CleanupOldCacheEntriesAsync(
        TimeSpan maxAge,
        int minAccessCount = 1,
        CancellationToken cancellationToken = default
    )
    {
        DateTime cutoffDate = DateTime.UtcNow - maxAge;

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync(
            cancellationToken
        );
        List<TtsCacheEntry> oldEntries = await db
            .TtsCacheEntries.Where(x =>
                x.LastAccessedAt < cutoffDate && x.AccessCount < minAccessCount
            )
            .ToListAsync(cancellationToken);

        foreach (TtsCacheEntry entry in oldEntries)
        {
            // Delete file from disk if it exists
            if (File.Exists(entry.FilePath))
                try
                {
                    File.Delete(entry.FilePath);
                }
                catch
                {
                    // Ignore file deletion errors
                }

            // Remove from database
            db.TtsCacheEntries.Remove(entry);
        }

        if (oldEntries.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }
}
