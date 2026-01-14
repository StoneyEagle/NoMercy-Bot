using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;

public class VoiceCommand : IBotCommand
{
    public string Name => "voice";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.ReplyAsync("Voice commands: !voice languages | !voice get <language> | !voice set <voice-id/name> | !voice current");
            return;
        }

        string subCommand = ctx.Arguments[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "languages":
                await HandleLanguagesCommand(ctx);
                break;
            case "get":
                await HandleGetCommand(ctx);
                break;
            case "set":
                await HandleSetCommand(ctx);
                break;
            case "current":
                await HandleCurrentCommand(ctx);
                break;
            default:
                await ctx.ReplyAsync("Unknown voice command. Use: !voice languages | !voice get <language> | !voice set <voice-id/name> | !voice current");
                break;
        }
    }

    private async Task HandleLanguagesCommand(CommandScriptContext ctx)
    {
        List<string> availableLanguages = await ctx.DatabaseContext.TtsVoices
            .Where(v => v.IsActive)
            .Select(v => v.Locale)
            .Distinct()
            .OrderBy(locale => locale)
            .ToListAsync(ctx.CancellationToken);

        if (availableLanguages.Count == 0)
        {
            await ctx.ReplyAsync("No TTS voices available.");
            return;
        }

        // Group languages by primary language code
        Dictionary<string, List<string>> languageGroups = [];
        foreach (string locale in availableLanguages)
        {
            try
            {
                CultureInfo culture = new(locale);
                string languageCode = culture.TwoLetterISOLanguageName;
                
                if (!languageGroups.ContainsKey(languageCode))
                {
                    languageGroups[languageCode] = [];
                }
                languageGroups[languageCode].Add(locale);
            }
            catch
            {
                // Fallback for invalid locale codes
                languageGroups.TryAdd("other", []);
                languageGroups["other"].Add(locale);
            }
        }

        List<string> formattedLanguages = [];
        foreach (KeyValuePair<string, List<string>> group in languageGroups.OrderBy(g => g.Key))
        {
            formattedLanguages.Add($"{group.Key.ToUpperInvariant()}: {string.Join(", ", group.Value)}");
        }

        string languageList = string.Join(" | ", formattedLanguages);
        await ctx.ReplyAsync($"Available languages: {languageList}");
    }

    private async Task HandleGetCommand(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length < 2)
        {
            await ctx.ReplyAsync("Usage: !voice get <language> (e.g. !voice get en or !voice get en-US)");
            return;
        }

        string searchLanguage = ctx.Arguments[1].ToLowerInvariant();

        // Support both full locale (en-US) and language code (en) searches
        List<TtsVoice> matchingVoices = await ctx.DatabaseContext.TtsVoices
            .Where(v => v.IsActive && (
                v.Locale.ToLower() == searchLanguage || 
                v.Locale.ToLower().StartsWith(searchLanguage + "-")))
            .OrderBy(v => v.Provider)
            .ThenBy(v => v.DisplayName)
            .ToListAsync(ctx.CancellationToken);

        if (matchingVoices.Count == 0)
        {
            await ctx.ReplyAsync($"No voices found for '{searchLanguage}'. Try !voice languages");
            return;
        }

        // Group voices by provider for better readability
        Dictionary<string, List<string>> providerGroups = [];
        foreach (TtsVoice voice in matchingVoices)
        {
            string providerName = voice.Provider;
            if (!providerGroups.ContainsKey(providerName))
            {
                providerGroups[providerName] = [];
            }

            providerGroups[providerName].Add(voice.DisplayName);
        }

        List<string> formattedProviders = [];
        foreach (KeyValuePair<string, List<string>> group in providerGroups.OrderBy(g => g.Key))
        {
            formattedProviders.Add($"{group.Key}: {string.Join(", ", group.Value)}");
        }

        string voiceList = string.Join(" | ", formattedProviders);
        await ctx.ReplyAsync($"{searchLanguage.ToUpperInvariant()}: {voiceList}");
    }

    private async Task HandleSetCommand(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length < 2)
        {
            await ctx.ReplyAsync("Usage: !voice set <voice-id> or !voice set <voice-name>");
            return;
        }

        // Merge all arguments after "set" to handle voice names with spaces
        string voiceIdentifier = string.Join(" ", ctx.Arguments.Skip(1));
        string userId = ctx.Message.UserId;

        // Try to find voice by ID first, then by name (case-insensitive)
        TtsVoice? targetVoice = await ctx.DatabaseContext.TtsVoices
            .Where(v => v.IsActive && (v.Id == voiceIdentifier || v.DisplayName.ToLower() == voiceIdentifier.ToLower()))
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetVoice == null)
        {
            await ctx.ReplyAsync($"Voice '{voiceIdentifier}' not found. Use !voice get <language> to see available voices.");
            return;
        }

        // Check if user already has a voice preference
        UserTtsVoice? existingUserVoice = await ctx.DatabaseContext.UserTtsVoices
            .Where(uv => uv.UserId == userId)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (existingUserVoice != null)
        {
            // Update existing preference
            existingUserVoice.TtsVoiceId = targetVoice.Id;
            existingUserVoice.SetAt = DateTime.UtcNow;
            ctx.DatabaseContext.UserTtsVoices.Update(existingUserVoice);
        }
        else
        {
            // Create new preference
            UserTtsVoice newUserVoice = new()
            {
                UserId = userId,
                TtsVoiceId = targetVoice.Id,
                SetAt = DateTime.UtcNow
            };
            await ctx.DatabaseContext.UserTtsVoices.AddAsync(newUserVoice, ctx.CancellationToken);
        }

        await ctx.DatabaseContext.SaveChangesAsync(ctx.CancellationToken);

        await ctx.ReplyAsync($"✅ Voice set to {targetVoice.DisplayName}!");
    }

    private async Task HandleCurrentCommand(CommandScriptContext ctx)
    {
        string userId = ctx.Message.UserId;

        UserTtsVoice? userVoice = await ctx.DatabaseContext.UserTtsVoices
            .Include(uv => uv.TtsVoice)
            .Where(uv => uv.UserId == userId)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (userVoice == null)
        {
            // Check if there's a default voice available
            TtsVoice? defaultVoice = await ctx.DatabaseContext.TtsVoices
                .Where(v => v.IsActive && v.IsDefault)
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (defaultVoice != null)
            {
                await ctx.ReplyAsync($"Using default: {defaultVoice.DisplayName}. Set custom voice with !voice set <name>");
            }
            else
            {
                await ctx.ReplyAsync("No voice set. Use !voice get <language> to find voices.");
            }
            return;
        }

        TtsVoice voice = userVoice.TtsVoice;
        await ctx.ReplyAsync($"Current voice: {voice.DisplayName}");
    }
}

return new VoiceCommand();
