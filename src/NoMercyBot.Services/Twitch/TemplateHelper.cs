using System.Text.RegularExpressions;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Twitch.Scripting;

namespace NoMercyBot.Services.Twitch;

public static class TemplateHelper
{
    // Matches all known template placeholders in a single pass (case-insensitive).
    // Groups: 1=tag, 2=verb singular, 3=verb plural (only for verb: syntax)
    private static readonly Regex PlaceholderPattern = new(
        @"\{(" +
        @"name|subject|object|possessive|" +
        @"presenttense|pasttense|tense|" +
        @"verb:([^|]+)\|([^}]+)|" +
        @"genderedterm|" +
        @"game|title|link|username|displayname|id|status" +
        @")\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// Replace template placeholders with actual values.
    /// </summary>
    /// <param name="template">The template string with placeholders</param>
    /// <param name="ctx">The command context</param>
    /// <param name="isLive">Whether the user is live</param>
    /// <param name="gameName">The game name</param>
    /// <param name="title">The stream title</param>
    /// <param name="usernamePronunciation">Optional pronunciation override for TTS (replaces {name}, {username}, {displayname})</param>
    /// <param name="pronounNameOverride">Optional name override used when pronoun is "any" (for readable name in {subject}/{object}). Falls back to usernamePronunciation, then DisplayName.</param>
    public static string ReplaceTemplatePlaceholders(
        string template,
        CommandScriptContext ctx,
        bool? isLive = null,
        string? gameName = null,
        string? title = null,
        string? usernamePronunciation = null,
        string? pronounNameOverride = null
    )
    {
        ChatMessage message = ctx.Message;

        // Use pronunciation if provided, otherwise fall back to display name
        string nameForTemplate = usernamePronunciation ?? message.DisplayName;

        // For "any" pronoun substitution, prefer the explicit override, then pronunciation, then display name
        string nameForPronoun = pronounNameOverride ?? usernamePronunciation ?? message.DisplayName;

        // Get pronouns - when pronoun is "any", "other", or similar non-standard values,
        // use smart alternation. When pronouns aren't set, also use smart alternation.
        string[] nonGrammaticalPronouns = ["any", "other"];
        bool hasPronouns = message.User?.Pronoun != null
            && !string.IsNullOrEmpty(message.User.Pronoun.Subject);
        bool isNonGrammatical = hasPronouns
            && !string.IsNullOrEmpty(message.User.Pronoun.Name)
            && nonGrammaticalPronouns.Contains(message.User.Pronoun.Name.ToLower());

        // Smart alternation: first {Subject} uses the name, subsequent use they/them.
        // This produces natural text like "StoneyEagle is awesome! They play great games."
        if (!hasPronouns || isNonGrammatical)
        {
            return ReplaceWithSmartAlternation(
                template, message, nameForTemplate, nameForPronoun,
                isLive, gameName, title, usernamePronunciation);
        }

        // Explicit pronouns path: user has he/him, she/her, they/them, etc.
        return ReplaceWithExplicitPronouns(
            template, message, nameForTemplate, nameForPronoun,
            isLive, gameName, title, usernamePronunciation);
    }

    /// <summary>
    /// Single-pass left-to-right replacement for users without explicit pronouns.
    /// First {Subject} becomes the user's name (singular verb agreement),
    /// subsequent {Subject} becomes "they" (plural verb agreement).
    /// </summary>
    private static string ReplaceWithSmartAlternation(
        string template,
        ChatMessage message,
        string nameForTemplate,
        string nameForPronoun,
        bool? isLive,
        string? gameName,
        string? title,
        string? usernamePronunciation)
    {
        bool nameIntroduced = false;
        bool lastSubjectSingular = true;

        return PlaceholderPattern.Replace(template, m =>
        {
            string tag = m.Groups[1].Value;
            bool cap = char.IsUpper(tag[0]);
            string key = tag.ToLower();

            if (key.StartsWith("verb:"))
                return lastSubjectSingular ? m.Groups[2].Value : m.Groups[3].Value;

            switch (key)
            {
                case "name":
                    nameIntroduced = true;
                    lastSubjectSingular = true;
                    return nameForTemplate;

                case "subject":
                    if (!nameIntroduced)
                    {
                        nameIntroduced = true;
                        lastSubjectSingular = true;
                        return nameForPronoun;
                    }
                    lastSubjectSingular = false;
                    return cap ? "They" : "they";

                case "object":
                    return cap ? "Them" : "them";

                case "possessive":
                    if (!nameIntroduced)
                    {
                        nameIntroduced = true;
                        lastSubjectSingular = true;
                        return nameForPronoun + "'s";
                    }
                    return cap ? "Their" : "their";

                case "presenttense":
                    return ApplyCase(lastSubjectSingular ? "is" : "are", cap);

                case "pasttense":
                    return ApplyCase(lastSubjectSingular ? "was" : "were", cap);

                case "tense":
                    if (!isLive.HasValue) return m.Value;
                    string be = lastSubjectSingular ? "is" : "are";
                    string was = lastSubjectSingular ? "was" : "were";
                    return ApplyCase(isLive.Value ? be : was, cap);

                case "genderedterm":
                    return ApplyCase("friend", cap);

                case "game": return gameName ?? "";
                case "title": return title ?? "";
                case "link": return $"https://www.twitch.tv/{message.User.Username}";
                case "username": return usernamePronunciation ?? message.User.Username;
                case "displayname": return nameForTemplate;
                case "id": return message.User.Id;
                case "status":
                    if (!isLive.HasValue) return m.Value;
                    return ApplyCase(isLive.Value ? "live" : "offline", cap);

                default: return m.Value;
            }
        });
    }

    /// <summary>
    /// Multi-pass replacement for users with explicit pronouns (he/him, she/her, they/them).
    /// All occurrences use the same pronoun consistently.
    /// </summary>
    private static string ReplaceWithExplicitPronouns(
        string template,
        ChatMessage message,
        string nameForTemplate,
        string nameForPronoun,
        bool? isLive,
        string? gameName,
        string? title,
        string? usernamePronunciation)
    {
        string subjectPronoun = message.User.Pronoun!.Subject;
        string objectPronoun = !string.IsNullOrEmpty(message.User.Pronoun.Object)
            ? message.User.Pronoun.Object
            : "them";

        bool isSingular = message.User.Pronoun.Singular
            || subjectPronoun.ToLower() is "he" or "she";

        string beVerb = isSingular ? "is" : "are";
        string wasVerb = isSingular ? "was" : "were";

        string possessive = subjectPronoun.ToLower() switch
        {
            "he" => "his",
            "she" => "her",
            "they" => "their",
            _ => "their",
        };

        string genderedTerm = subjectPronoun.ToLower() switch
        {
            "he" => "dude",
            "she" => "dudette",
            _ => "friend",
        };

        return PlaceholderPattern.Replace(template, m =>
        {
            string tag = m.Groups[1].Value;
            bool cap = char.IsUpper(tag[0]);
            string key = tag.ToLower();

            if (key.StartsWith("verb:"))
                return isSingular ? m.Groups[2].Value : m.Groups[3].Value;

            switch (key)
            {
                case "name": return nameForTemplate;
                case "subject": return cap ? subjectPronoun : subjectPronoun.ToLower();
                case "object": return ApplyCase(objectPronoun, cap);
                case "possessive": return ApplyCase(possessive, cap);
                case "presenttense": return ApplyCase(beVerb, cap);
                case "pasttense": return ApplyCase(wasVerb, cap);
                case "tense":
                    if (!isLive.HasValue) return m.Value;
                    return ApplyCase(isLive.Value ? beVerb : wasVerb, cap);
                case "genderedterm": return ApplyCase(genderedTerm, cap);
                case "game": return gameName ?? "";
                case "title": return title ?? "";
                case "link": return $"https://www.twitch.tv/{message.User.Username}";
                case "username": return usernamePronunciation ?? message.User.Username;
                case "displayname": return nameForTemplate;
                case "id": return message.User.Id;
                case "status":
                    if (!isLive.HasValue) return m.Value;
                    return ApplyCase(isLive.Value ? "live" : "offline", cap);
                default: return m.Value;
            }
        });
    }

    private static string ApplyCase(string value, bool capitalize)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return capitalize
            ? char.ToUpper(value[0]) + value[1..]
            : value.ToLower();
    }

    public static string ReplaceTemplatePlaceholders(
        string template,
        RewardScriptContext ctx,
        bool? isLive = null,
        string? gameName = null,
        string? title = null,
        string? usernamePronunciation = null,
        string? pronounNameOverride = null
    )
    {
        CommandScriptContext commandCtx = new()
        {
            Message = new()
            {
                User = ctx.User,
                DisplayName = ctx.UserDisplayName,
                Id = ctx.UserId,
            },
            DatabaseContext = ctx.DatabaseContext,
            TwitchApiService = ctx.TwitchApiService,
            TwitchChatService = ctx.TwitchChatService,
            ServiceProvider = ctx.ServiceProvider,
        };

        return ReplaceTemplatePlaceholders(
            template,
            commandCtx,
            isLive,
            gameName,
            title,
            usernamePronunciation,
            pronounNameOverride
        );
    }
}
