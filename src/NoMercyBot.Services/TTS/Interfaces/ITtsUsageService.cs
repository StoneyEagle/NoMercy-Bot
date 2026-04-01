using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.TTS.Interfaces;

public interface ITtsUsageService
{
    /// <summary>
    /// Checks if character usage would be allowed without exceeding limits
    /// </summary>
    Task<bool> CanUseCharactersAsync(string providerId, int characterCount);

    /// <summary>
    /// Records TTS usage after successful synthesis
    /// </summary>
    Task<TtsUsageRecord> RecordUsageAsync(string providerId, int characterCount, decimal cost);

    /// <summary>
    /// Gets current character usage for a provider in the current billing period
    /// </summary>
    Task<int> GetCurrentUsageAsync(string providerId);

    /// <summary>
    /// Gets remaining characters for the current billing period
    /// </summary>
    Task<int> GetRemainingCharactersAsync(string providerId);

    /// <summary>
    /// Checks if temporary override is active
    /// </summary>
    Task<bool> HasTemporaryOverrideAsync();

    /// <summary>
    /// Sets temporary override status
    /// </summary>
    Task SetTemporaryOverrideAsync(bool enabled);

    /// <summary>
    /// Checks if the provider has exceeded its monthly character limit
    /// </summary>
    Task<bool> IsMonthlyLimitExceededAsync(string providerId, int additionalCharacters = 0);

    /// <summary>
    /// Gets current month's usage statistics for a provider
    /// </summary>
    Task<(
        int charactersUsed,
        decimal totalCost,
        int remainingCharacters
    )> GetCurrentMonthUsageAsync(string providerId);
}
