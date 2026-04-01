using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.TTS.Interfaces;
using Serilog.Events;
using DatabaseTtsVoice = NoMercyBot.Database.Models.TtsVoice;
using ServicesTtsVoice = NoMercyBot.Services.TTS.Models.TtsVoice;

namespace NoMercyBot.Services.Seeds;

public static class TtsVoiceSeed
{
    public static async Task Init(AppDbContext dbContext, IEnumerable<ITtsProvider> ttsProviders)
    {
        try
        {
            List<DatabaseTtsVoice> allVoices = [];
            List<string> skippedProviders = [];

            // Get voices from all available providers
            foreach (ITtsProvider provider in ttsProviders)
                try
                {
                    await provider.InitializeAsync();

                    if (!await provider.IsAvailableAsync())
                    {
                        skippedProviders.Add(provider.Name);
                        continue;
                    }

                    List<ServicesTtsVoice> providerVoices =
                        await provider.GetAvailableVoicesAsync();
                    List<DatabaseTtsVoice> convertedVoices = ConvertProviderVoicesToDatabaseVoices(
                        providerVoices,
                        provider.Name
                    );

                    allVoices.AddRange(convertedVoices);
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        $"Error retrieving voices from provider '{provider.Name}': {ex.Message}",
                        LogEventLevel.Warning
                    );
                }

            if (allVoices.Count > 0)
            {
                await dbContext
                    .TtsVoices.UpsertRange(allVoices)
                    .On(v => v.Id)
                    .WhenMatched(
                        (existing, incoming) =>
                            new()
                            {
                                SpeakerId = incoming.SpeakerId,
                                Name = incoming.Name,
                                DisplayName = incoming.DisplayName,
                                Locale = incoming.Locale,
                                Gender = incoming.Gender,
                                Age = incoming.Age,
                                Accent = incoming.Accent,
                                Region = incoming.Region,
                                Provider = incoming.Provider,
                            }
                    )
                    .RunAsync();
            }

            string summary = $"TTS seed: {allVoices.Count} voices seeded";
            if (skippedProviders.Count > 0)
                summary += $", skipped providers: {string.Join(", ", skippedProviders)}";

            Logger.Setup(summary);

            // Seed TtsProvider records for each registered provider
            await SeedProviderRecords(dbContext, ttsProviders);

            // Migrate user voice preferences from Azure to Edge
            await MigrateAzureVoicesToEdge(dbContext);

            // Randomize users stuck on the default JennyNeural voice
            await RandomizeDefaultVoices(dbContext);
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error seeding TTS voices: {ex.Message}", LogEventLevel.Error);
        }
    }

    private static async Task SeedProviderRecords(
        AppDbContext dbContext,
        IEnumerable<ITtsProvider> ttsProviders
    )
    {
        foreach (ITtsProvider provider in ttsProviders)
        {
            bool isFree =
                string.Equals(provider.Name, "Edge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider.Name, "Legacy", StringComparison.OrdinalIgnoreCase);

            await dbContext
                .TtsProviders.Upsert(
                    new TtsProvider
                    {
                        Id = provider.Name.ToLowerInvariant(),
                        Name = provider.Name,
                        Type = provider.Type,
                        IsEnabled = provider.IsEnabled,
                        Priority = provider.Priority,
                        MonthlyCharacterLimit = isFree ? 0 : 500000,
                        CostPerCharacter = isFree ? 0m : 0.000016m,
                        MaxCharactersPerRequest = 3000,
                    }
                )
                .On(p => p.Id)
                .WhenMatched(
                    (existing, incoming) =>
                        new()
                        {
                            Name = incoming.Name,
                            Type = incoming.Type,
                            UpdatedAt = DateTime.UtcNow,
                        }
                )
                .RunAsync();
        }

        Logger.Setup("Seeded TTS provider records");
    }

    /// <summary>
    /// Migrates user voice preferences from Azure to Edge.
    /// For voices that exist in both providers (same SpeakerId), swaps the prefix.
    /// For voices without an Edge equivalent, assigns a random Edge voice with the same gender.
    /// </summary>
    private static async Task MigrateAzureVoicesToEdge(AppDbContext dbContext)
    {
        // Get all user preferences currently pointing to Azure
        List<UserTtsVoice> azureUserVoices = await dbContext
            .UserTtsVoices.Where(u => u.TtsVoiceId.StartsWith("Azure:"))
            .ToListAsync();

        if (azureUserVoices.Count == 0)
            return;

        // Get all active Edge voices from the database
        List<DatabaseTtsVoice> edgeVoices = await dbContext
            .TtsVoices.AsNoTracking()
            .Where(v => v.Provider == "Edge" && v.IsActive)
            .ToListAsync();

        if (edgeVoices.Count == 0)
        {
            Logger.Setup("No Edge voices available for migration, skipping", LogEventLevel.Warning);
            return;
        }

        // Build a set of Edge speaker IDs for fast lookup
        HashSet<string> edgeSpeakerIds = edgeVoices.Select(v => v.SpeakerId).ToHashSet();

        // Group Edge voices by gender for random assignment
        List<DatabaseTtsVoice> edgeMaleVoices = edgeVoices
            .Where(v =>
                v.Gender.Contains("Male", StringComparison.OrdinalIgnoreCase)
                && !v.Gender.Contains("Female", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        List<DatabaseTtsVoice> edgeFemaleVoices = edgeVoices
            .Where(v => v.Gender.Contains("Female", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Get Azure voice metadata for gender lookups on non-matching voices
        List<DatabaseTtsVoice> azureVoices = await dbContext
            .TtsVoices.AsNoTracking()
            .Where(v => v.Provider == "Azure")
            .ToListAsync();
        Dictionary<string, DatabaseTtsVoice> azureVoiceMap = azureVoices.ToDictionary(
            v => v.Id,
            v => v
        );

        Random random = new();
        int migrated = 0;

        foreach (UserTtsVoice userVoice in azureUserVoices)
        {
            string azureSpeakerId = userVoice.TtsVoiceId["Azure:".Length..];

            if (edgeSpeakerIds.Contains(azureSpeakerId))
            {
                // Direct match: same voice exists in Edge
                userVoice.TtsVoiceId = $"Edge:{azureSpeakerId}";
            }
            else
            {
                // No direct match: pick a random Edge voice with the same gender
                string gender = "Female"; // default
                if (
                    azureVoiceMap.TryGetValue(
                        userVoice.TtsVoiceId,
                        out DatabaseTtsVoice? azureVoice
                    )
                )
                    gender = azureVoice.Gender;

                List<DatabaseTtsVoice> candidates = gender.Contains(
                    "Female",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? edgeFemaleVoices
                    : edgeMaleVoices;

                if (candidates.Count == 0)
                    candidates = edgeVoices; // fallback to any voice

                DatabaseTtsVoice randomVoice = candidates[random.Next(candidates.Count)];
                userVoice.TtsVoiceId = randomVoice.Id;
            }

            userVoice.SetAt = DateTime.UtcNow;
            migrated++;
        }

        await dbContext.SaveChangesAsync();
        Logger.Setup($"Migrated {migrated} user voice preferences from Azure to Edge");
    }

    /// <summary>
    /// Assigns a random English Edge voice to users currently on Edge:en-US-JennyNeural (the old Azure default).
    /// Idempotent: only runs while users with that exact voice exist.
    /// </summary>
    private static async Task RandomizeDefaultVoices(AppDbContext dbContext)
    {
        List<UserTtsVoice> jennyUsers = await dbContext
            .UserTtsVoices.Where(u => u.TtsVoiceId == "Edge:en-US-JennyNeural")
            .ToListAsync();

        if (jennyUsers.Count == 0)
            return;

        // Get all active English Edge voices to pick from
        List<DatabaseTtsVoice> englishEdgeVoices = await dbContext
            .TtsVoices.AsNoTracking()
            .Where(v => v.Provider == "Edge" && v.IsActive && v.Locale.StartsWith("en-"))
            .ToListAsync();

        if (englishEdgeVoices.Count == 0)
            return;

        Random random = new();

        foreach (UserTtsVoice userVoice in jennyUsers)
        {
            DatabaseTtsVoice randomVoice = englishEdgeVoices[random.Next(englishEdgeVoices.Count)];
            userVoice.TtsVoiceId = randomVoice.Id;
            userVoice.SetAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        Logger.Setup(
            $"Randomized {jennyUsers.Count} users from Edge:en-US-JennyNeural to random English voices"
        );
    }

    private static List<DatabaseTtsVoice> ConvertProviderVoicesToDatabaseVoices(
        List<ServicesTtsVoice> providerVoices,
        string providerName
    )
    {
        return providerVoices
            .Select(voice => new DatabaseTtsVoice
            {
                Id = $"{providerName}:{voice.Id}",
                SpeakerId = voice.Id,
                Name = voice.Name,
                DisplayName = !string.IsNullOrWhiteSpace(voice.DisplayName)
                    ? voice.DisplayName
                    : voice.Name,
                Locale = voice.Locale,
                Gender = voice.Gender,
                Age = 0,
                Accent = string.Empty,
                Region = voice.Locale.Contains('-')
                    ? voice.Locale.Split('-')[1].ToUpperInvariant()
                    : string.Empty,
                Provider = providerName,
                IsDefault = voice.IsDefault,
                IsActive = true,
            })
            .ToList();
    }
}
