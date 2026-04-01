using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.TTS.Interfaces;
using Serilog.Events;

namespace NoMercyBot.Services.TTS.Services;

public class TtsProviderService : ITtsProviderService
{
    private readonly ITtsUsageService _usageService;
    private readonly IServiceProvider _serviceProvider;

    public TtsProviderService(ITtsUsageService usageService, IServiceProvider serviceProvider)
    {
        _usageService = usageService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the best available TTS provider that can handle the character count WITHOUT exceeding monthly limits
    /// </summary>
    public async Task<ITtsProvider?> GetBestAvailableProviderAsync(int characterCount)
    {
        // Get all registered TTS provider implementations directly
        List<ITtsProvider> providers = _serviceProvider
            .GetServices<ITtsProvider>()
            .Where(p => p.IsEnabled)
            .ToList();

        if (!providers.Any())
        {
            Logger.Twitch(
                "No TTS provider implementations registered in DI container",
                LogEventLevel.Warning
            );
            return null;
        }

        foreach (ITtsProvider provider in providers)
        {
            // Check character limits using your existing service
            bool canUseCharacters = await _usageService.CanUseCharactersAsync(
                provider.Name,
                characterCount
            );
            if (!canUseCharacters)
            {
                int remaining = await _usageService.GetRemainingCharactersAsync(provider.Name);
                Logger.Twitch(
                    $"Provider {provider.Name} skipped: Character limit exceeded. Requested: {characterCount}, Remaining: {remaining}",
                    LogEventLevel.Warning
                );
                continue; // Skip this provider
            }

            return provider;
        }

        // No provider available that can handle the request without exceeding limits
        Logger.Twitch("All providers exceed character limits", LogEventLevel.Warning);
        return null;
    }

    /// <summary>
    /// Gets the best available TTS provider WITHOUT checking character limits - used for cached content and voice selection
    /// </summary>
    public async Task<ITtsProvider?> GetBestAvailableProviderIgnoringLimitsAsync()
    {
        // Get all registered TTS provider implementations directly
        List<ITtsProvider> providers = _serviceProvider
            .GetServices<ITtsProvider>()
            .Where(p => p.IsEnabled)
            .ToList();

        if (!providers.Any())
        {
            Logger.Twitch(
                "No TTS provider implementations registered in DI container",
                LogEventLevel.Warning
            );
            return null;
        }

        // Sort by priority and return the first available one
        foreach (ITtsProvider provider in providers.OrderBy(p => p.Priority))
            try
            {
                bool isAvailable = await provider.IsAvailableAsync();
                if (isAvailable)
                    return provider;
            }
            catch (Exception ex)
            {
                Logger.Twitch(
                    $"Error checking provider {provider.Name} availability: {ex.Message}",
                    LogEventLevel.Warning
                );
            }

        Logger.Twitch("No TTS providers are available", LogEventLevel.Warning);
        return null;
    }

    public async Task<List<ITtsProvider>> GetAllProvidersAsync()
    {
        // Get all registered TTS provider implementations directly
        List<ITtsProvider> providers = _serviceProvider.GetServices<ITtsProvider>().ToList();

        // Filter to only enabled and available providers
        List<ITtsProvider> availableProviders = new();

        foreach (ITtsProvider provider in providers.Where(p => p.IsEnabled))
            try
            {
                bool isAvailable = await provider.IsAvailableAsync();
                if (isAvailable)
                    availableProviders.Add(provider);
            }
            catch (Exception ex)
            {
                Logger.Twitch(
                    $"Error checking provider {provider.Name} availability: {ex.Message}",
                    LogEventLevel.Warning
                );
            }

        return availableProviders.OrderBy(p => p.Priority).ToList();
    }

    /// <summary>
    /// Gets all available TTS providers with their current usage status
    /// </summary>
    public async Task<List<TtsProviderStatus>> GetProviderStatusAsync()
    {
        // Get all registered TTS provider implementations
        List<ITtsProvider> providers = _serviceProvider.GetServices<ITtsProvider>().ToList();
        List<TtsProviderStatus> statuses = new();

        foreach (ITtsProvider provider in providers)
        {
            (int charactersUsed, decimal totalCost, int remainingCharacters) =
                await _usageService.GetCurrentMonthUsageAsync(provider.Name);

            statuses.Add(
                new()
                {
                    Id = provider.Name,
                    Name = provider.Name,
                    IsEnabled = provider.IsEnabled,
                    Priority = provider.Priority,
                    MonthlyCharacterLimit = 50000, // Default limit, you can make this configurable
                    CharactersUsedThisMonth = charactersUsed,
                    RemainingCharacters = remainingCharacters,
                    EstimatedMonthlyCost = totalCost,
                    MaxCharactersPerRequest = 3000, // Default max per request
                    IsLimitExceeded = remainingCharacters <= 0,
                }
            );
        }

        return statuses;
    }

    public Task RefreshProvidersAsync()
    {
        // This method can be used to refresh or reload providers if needed
        // For now, we will just return a completed task
        // In a real implementation, you might want to reload configurations or clear caches
        return Task.CompletedTask;
    }

    private ITtsProvider? GetProviderImplementation(string providerId)
    {
        // Get all registered TTS provider implementations
        IEnumerable<ITtsProvider> providers = _serviceProvider.GetServices<ITtsProvider>();

        return providers.FirstOrDefault(p =>
            string.Equals(p.Name, providerId, StringComparison.OrdinalIgnoreCase)
        );
    }
}

public class TtsProviderStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public int MonthlyCharacterLimit { get; set; }
    public int CharactersUsedThisMonth { get; set; }
    public int RemainingCharacters { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public int MaxCharactersPerRequest { get; set; }
    public bool IsLimitExceeded { get; set; }
}
