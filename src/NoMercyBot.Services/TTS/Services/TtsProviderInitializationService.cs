using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NoMercyBot.Database;
using NoMercyBot.Services.TTS.Interfaces;

namespace NoMercyBot.Services.TTS.Services;

public class TtsProviderInitializationService : IHostedService
{
    private readonly ITtsProviderService _providerService;
    private readonly AppDbContext _dbContext;

    public TtsProviderInitializationService(
        ITtsProviderService providerService,
        AppDbContext dbContext
    )
    {
        _providerService = providerService;
        _dbContext = dbContext;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Refresh providers to sync database with code
            await _providerService.RefreshProvidersAsync();

            // Populate voices from all providers
            await PopulateProviderVoicesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't fail startup
            Console.WriteLine($"Failed to initialize TTS providers: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task PopulateProviderVoicesAsync(CancellationToken cancellationToken)
    {
        List<ITtsProvider> allProviders = await _providerService.GetAllProvidersAsync();

        foreach (ITtsProvider provider in allProviders)
            try
            {
                // Get voices from the provider
                List<Models.TtsVoice> providerVoices = await provider.GetAvailableVoicesAsync();

                if (providerVoices.Count == 0)
                    continue;

                // Mark all existing voices for this provider as inactive initially
                await _dbContext
                    .TtsVoices.Where(v => v.Provider == provider.Name)
                    .ExecuteUpdateAsync(
                        v => v.SetProperty(p => p.IsActive, false),
                        cancellationToken
                    );

                foreach (Models.TtsVoice providerVoice in providerVoices)
                {
                    // Create unique ID combining provider and voice ID
                    string uniqueId = $"{provider.Name}:{providerVoice.Id}";

                    await _dbContext
                        .TtsVoices.Upsert(
                            new()
                            {
                                Id = uniqueId,
                                SpeakerId = providerVoice.Id, // Store original voice ID
                                Name = providerVoice.Name,
                                DisplayName = providerVoice.DisplayName,
                                Locale = providerVoice.Locale,
                                Gender = providerVoice.Gender,
                                Region = providerVoice.Locale, // Map locale to region for backward compatibility
                                Provider = provider.Name,
                                IsDefault = providerVoice.IsDefault,
                                IsActive = true,
                                UpdatedAt = DateTime.UtcNow,
                                Age = 0, // Provider voices don't typically have age info
                                Accent = "", // Provider voices don't typically have accent info
                            }
                        )
                        .On(v => v.Id)
                        .WhenMatched(
                            (existing, incoming) =>
                                new()
                                {
                                    Id = existing.Id,
                                    SpeakerId = incoming.SpeakerId,
                                    Name = incoming.Name,
                                    DisplayName = incoming.DisplayName,
                                    Locale = incoming.Locale,
                                    Gender = incoming.Gender,
                                    Region = incoming.Region,
                                    Provider = existing.Provider,
                                    IsDefault = incoming.IsDefault,
                                    IsActive = true, // Mark as active since it's still available
                                    UpdatedAt = DateTime.UtcNow,
                                    // Keep existing values for legacy fields
                                    Age = existing.Age,
                                    Accent = existing.Accent,
                                }
                        )
                        .RunAsync(cancellationToken);
                }

                Console.WriteLine(
                    $"Updated {providerVoices.Count} voices for provider {provider.Name}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to populate voices for provider {provider.Name}: {ex.Message}"
                );
                continue;
            }

        // Clean up old inactive voices (optional - only remove after a certain period)
        DateTime cutoffDate = DateTime.UtcNow.AddDays(-30);
        int removedCount = await _dbContext
            .TtsVoices.Where(v =>
                !v.IsActive && v.UpdatedAt < cutoffDate && !string.IsNullOrEmpty(v.Provider)
            )
            .ExecuteDeleteAsync(cancellationToken);

        if (removedCount > 0)
            Console.WriteLine($"Removed {removedCount} old inactive provider voices");
    }
}
