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
            await ctx.ReplyAsync("Voice commands: !voice languages | !voice get <language> | !voice set <name> | !voice current | !voice roulette");
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
            case "roulette":
                await HandleRouletteCommand(ctx);
                break;
            default:
                await ctx.ReplyAsync("Unknown voice command. Use: !voice languages | !voice get <language> | !voice set <name> | !voice current | !voice roulette");
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

        // Deduplicate by SpeakerId (same voice may exist in multiple providers)
        List<TtsVoice> uniqueVoices = matchingVoices
            .GroupBy(v => v.SpeakerId)
            .Select(g => g.First())
            .OrderBy(v => v.SpeakerId)
            .ToList();

        // Show just the name (strip locale prefix and "Neural" suffix)
        string voiceList = string.Join(", ", uniqueVoices.Select(v =>
        {
            string name = v.SpeakerId;
            int lastDash = name.LastIndexOf('-');
            if (lastDash >= 0) name = name[(lastDash + 1)..];
            if (name.EndsWith("Neural")) name = name[..^6];
            return name;
        }));
        await ctx.ReplyAsync($"{searchLanguage.ToUpperInvariant()} voices: {voiceList}");
    }

    private async Task HandleSetCommand(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length < 2)
        {
            await ctx.ReplyAsync("Usage: !voice set <name> (e.g. !voice set Ana, !voice set en-US-AnaNeural)");
            return;
        }

        // Merge all arguments after "set" to handle voice names with spaces
        string voiceIdentifier = string.Join(" ", ctx.Arguments.Skip(1)).Trim();
        string userId = ctx.Message.UserId;
        string search = voiceIdentifier.ToLowerInvariant();

        // Try exact match: Id, SpeakerId, Name, DisplayName
        TtsVoice? targetVoice = await ctx.DatabaseContext.TtsVoices
            .Where(v => v.IsActive && (
                v.Id == voiceIdentifier ||
                v.SpeakerId.ToLower() == search ||
                v.Name.ToLower() == search ||
                v.DisplayName.ToLower() == search))
            .FirstOrDefaultAsync(ctx.CancellationToken);

        // Fallback: match voice name after locale prefix (e.g. "AnaNeural" or "Ana" matches "en-US-AnaNeural")
        if (targetVoice == null)
        {
            string dashSearch = "-" + search;
            List<TtsVoice> matches = await ctx.DatabaseContext.TtsVoices
                .Where(v => v.IsActive && v.SpeakerId.ToLower().Contains(dashSearch))
                .OrderBy(v => v.SpeakerId)
                .ToListAsync(ctx.CancellationToken);

            List<TtsVoice> uniqueMatches = matches
                .GroupBy(v => v.SpeakerId)
                .Select(g => g.First())
                .Take(10)
                .ToList();

            if (uniqueMatches.Count == 1)
            {
                targetVoice = uniqueMatches[0];
            }
            else if (uniqueMatches.Count > 1)
            {
                string options = string.Join(", ", uniqueMatches.Select(v => v.SpeakerId));
                await ctx.ReplyAsync($"Multiple matches: {options}");
                return;
            }
        }

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

        string voiceName = targetVoice.SpeakerId;
        int lastDash = voiceName.LastIndexOf('-');
        if (lastDash >= 0) voiceName = voiceName[(lastDash + 1)..];
        if (voiceName.EndsWith("Neural")) voiceName = voiceName[..^6];

        await ctx.ReplyAsync($"✅ Voice set to {voiceName}!");
    }

    private static readonly string[] _rouletteReactions =
    {
        "The wheel has spoken! Your next TTS message will be in... {voice}. Good luck.",
        "Voice roulette says: {voice}. No takebacks.",
        "Spinning the wheel... {voice}! May the odds be ever in your favor.",
        "The RNG gods have chosen: {voice}. We are not responsible for what happens next.",
        "And the random voice is... {voice}! Chat, place your bets on how this sounds.",
        "Voice roulette has landed on {voice}. This should be interesting.",
    };

    private async Task HandleRouletteCommand(CommandScriptContext ctx)
    {
        string userId = ctx.Message.UserId;

        // Get a random voice from all active voices
        List<TtsVoice> allVoices = await ctx.DatabaseContext.TtsVoices
            .Where(v => v.IsActive)
            .ToListAsync(ctx.CancellationToken);

        if (allVoices.Count == 0)
        {
            await ctx.ReplyAsync("No voices available for roulette!");
            return;
        }

        TtsVoice randomVoice = allVoices[Random.Shared.Next(allVoices.Count)];

        // Save it as their voice preference
        UserTtsVoice? existingUserVoice = await ctx.DatabaseContext.UserTtsVoices
            .Where(uv => uv.UserId == userId)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (existingUserVoice != null)
        {
            existingUserVoice.TtsVoiceId = randomVoice.Id;
            existingUserVoice.SetAt = DateTime.UtcNow;
            ctx.DatabaseContext.UserTtsVoices.Update(existingUserVoice);
        }
        else
        {
            UserTtsVoice newUserVoice = new()
            {
                UserId = userId,
                TtsVoiceId = randomVoice.Id,
                SetAt = DateTime.UtcNow
            };
            await ctx.DatabaseContext.UserTtsVoices.AddAsync(newUserVoice, ctx.CancellationToken);
        }

        await ctx.DatabaseContext.SaveChangesAsync(ctx.CancellationToken);

        string voiceDisplay = $"{randomVoice.DisplayName} ({randomVoice.Locale})";
        string reaction = _rouletteReactions[Random.Shared.Next(_rouletteReactions.Length)]
            .Replace("{voice}", voiceDisplay);

        await ctx.ReplyAsync(reaction);
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
        await ctx.ReplyAsync($"Current voice: {voice.DisplayName} ({voice.SpeakerId})");
    }
}

return new VoiceCommand();
