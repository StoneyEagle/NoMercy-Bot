using System.Net;
using System.Text;
using HtmlAgilityPack;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Emotes;
using NoMercyBot.Services.Emotes.Dto;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using TwitchLib.PubSub.Models.Responses;

namespace NoMercyBot.Services.Twitch;

public class TwitchMessageDecorator : IService
{
    private readonly FrankerFacezService _frankerFacezService;
    private readonly BttvService _bttvService;
    private readonly SevenTvService _sevenTvService;
    private readonly HtmlMetadataService _htmlMetadataService;
    private readonly ParallelOptions _parallelOptions;
    private readonly TwitchBadgeService _twitchBadgeService;
    private readonly PermissionService _permissionService;

    private ChatMessage ChatMessage { get; set; }
    private List<ChatMessageFragment> _fragments = [];
    private string MessageType => ChatMessage.MessageType.ToLowerInvariant();

    private bool HasDecoratorServices =>
        Config.UseBttvEmotes
        || Config.UseFrankerfacezEmotes
        || Config.UseSevenTvEmotes
        || Config.UseChatHtmlParser
        || Config.UseChatOgParser;

    public TwitchMessageDecorator(
        FrankerFacezService frankerFacezService,
        BttvService bttvService,
        SevenTvService sevenTvService,
        TwitchBadgeService twitchBadgeService,
        HtmlMetadataService htmlMetadataService,
        PermissionService permissionService,
        CancellationToken cancellationToken = default
    )
    {
        _frankerFacezService = frankerFacezService;
        _bttvService = bttvService;
        _sevenTvService = sevenTvService;
        _twitchBadgeService = twitchBadgeService;
        _htmlMetadataService = htmlMetadataService;
        _permissionService = permissionService;

        ChatMessage = new();

        _parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = (int)Math.Floor(Environment.ProcessorCount / 2.0),
        };
    }

    public async Task DecorateMessage(ChatMessage chatMessage)
    {
        ChatMessage = chatMessage;
        // Copy the fragments to a new list to avoid modifying the original message
        _fragments = chatMessage.Fragments.ToList();

        DecorateBadges();
        DecorateTwitchEmotes();

        DecrateHtml();

        // Split up all text fragments into individual word fragments so that we can decorate them with emotes
        ExplodeTextFragments();
        DecorateBttvEmotes();
        DecorateFrankerFaceEmotes();
        DecorateSevenTvEmotes();

        await DecorateUrlFragments();

        // Merge all consecutive text fragments back together to avoid having too many text fragments
        ImplodeTextFragments();

        DecorateCodeSnippet();

        // Overwrite the original fragments with the modified ones
        ChatMessage.Fragments = _fragments;
    }

    private void ExplodeTextFragments()
    {
        if (DecorateCommand())
            return;
        if (!HasDecoratorServices)
            return;

        List<ChatMessageFragment> newFragments = [];

        foreach (ChatMessageFragment fragment in _fragments)
        {
            int index = _fragments.IndexOf(fragment);

            if (fragment.Type != "text")
            {
                newFragments.Add(fragment);

                if (_fragments.ElementAtOrDefault(index - 0)?.Text != " ")
                    newFragments.Add(new() { Type = "text", Text = " " });

                continue;
            }

            string[] words = fragment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (newFragments.Count > 0 || _fragments.ElementAtOrDefault(index - 0)?.Text != " ")
                    newFragments.Add(new() { Type = "text", Text = " " });

                newFragments.Add(
                    new()
                    {
                        Type = "text",
                        Text = string.IsNullOrEmpty(word)
                            // replace all versions of whitespace with a single space
                            ? $"{word}"
                            : word,
                    }
                );

                if (newFragments.LastOrDefault()?.Text != " ")
                    newFragments.Add(new() { Type = "text", Text = " " });
            }
        }

        if (newFragments.FirstOrDefault()?.Text == " ")
            newFragments.RemoveAt(0);

        if (newFragments.LastOrDefault()?.Text == " ")
            newFragments.RemoveAt(newFragments.Count - 1);

        _fragments = newFragments;
    }

    private void ImplodeTextFragments()
    {
        if (!HasDecoratorServices)
            return;

        List<ChatMessageFragment> newFragments = [];
        StringBuilder currentText = new();

        foreach (ChatMessageFragment fragment in _fragments)
            if (fragment.Type == "text")
            {
                currentText.Append(fragment.Text);
            }
            else
            {
                if (currentText.Length > 0)
                {
                    newFragments.Add(new() { Type = "text", Text = currentText.ToString() });
                    currentText.Clear();
                }

                newFragments.Add(fragment);
            }

        if (currentText.Length > 0)
            newFragments.Add(new() { Type = "text", Text = currentText.ToString() });

        _fragments = newFragments.Where(fragment => fragment.Text != " ").ToList();
    }

    private bool DecorateCommand()
    {
        ChatMessageFragment? firstFragment = _fragments.FirstOrDefault();

        if (
            firstFragment is null
            || firstFragment.Type != "text"
            || !firstFragment.Text.StartsWith("!")
        )
            return false;

        ChatMessage.IsCommand = true;

        string text = string.Join("", _fragments.Select(x => x.Text));

        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (parts.Length == 0)
            return false;

        string commandName = parts[0].TrimStart('!').ToLowerInvariant();
        string[] args = parts.Skip(1).ToArray();

        _fragments =
        [
            new()
            {
                Type = "command",
                Text = text,
                Command = commandName,
                Args = args,
            },
        ];

        return true;
    }

    private void DecorateTwitchEmotes()
    {
        Parallel.ForEach(
            _fragments.ToList(),
            _parallelOptions,
            (fragment) =>
            {
                if (fragment.Type != "emote" || fragment.Emote is null)
                    return;

                int index = _fragments.IndexOf(fragment);
                _fragments[index] = new()
                {
                    Type = fragment.Type,
                    Text = fragment.Text,
                    Cheermote = fragment.Cheermote,
                    Mention = fragment.Mention,
                    Emote = new()
                    {
                        Id = fragment.Emote.Id,
                        Format = fragment.Emote.Format,
                        OwnerId = fragment.Emote.OwnerId,
                        EmoteSetId = fragment.Emote.EmoteSetId,
                        Provider = "twitch",
                        IsGigantified = ChatMessage.IsGigantified,
                        Urls = new()
                        {
                            {
                                "1",
                                new(
                                    $"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/1.0"
                                )
                            },
                            {
                                "2",
                                new(
                                    $"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/2.0"
                                )
                            },
                            {
                                "3",
                                new(
                                    $"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/3.0"
                                )
                            },
                        },
                    },
                };
            }
        );
    }

    private void DecorateBadges()
    {
        if (ChatMessage.Badges.Count == 0)
            ChatMessage.Badges = [];

        List<ChatBadge> badges = [];
        foreach (ChatBadge badge in ChatMessage.Badges)
        {
            ChatBadge? badgeResult = _twitchBadgeService.TwitchBadges.LastOrDefault(b =>
                b.SetId == badge.SetId && b.Id == badge.Id
            );

            if (badgeResult != null)
                badges.Add(badgeResult);
        }

        ChatMessage.Badges = badges;
    }

    private void DecorateFrankerFaceEmotes()
    {
        if (!Config.UseFrankerfacezEmotes)
            return;

        Parallel.ForEach(
            _fragments.ToList(),
            _parallelOptions,
            fragment =>
            {
                if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text))
                    return;

                Emoticon? emote = _frankerFacezService.FrankerFacezEmotes.FirstOrDefault(e =>
                    e.Name.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase)
                );
                if (emote == null)
                    return;

                int index = _fragments.IndexOf(fragment);
                _fragments[index] = new()
                {
                    Type = "emote",
                    Text = fragment.Text,
                    Emote = new()
                    {
                        Id = emote.Id.ToString(),
                        Provider = "frankerfacez",
                        Urls = emote.Urls,
                    },
                };
            }
        );
    }

    private void DecorateBttvEmotes()
    {
        if (!Config.UseBttvEmotes)
            return;

        Parallel.ForEach(
            _fragments.ToList(),
            _parallelOptions,
            fragment =>
            {
                if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text))
                    return;

                BttvEmote? emote = _bttvService.BttvEmotes.FirstOrDefault(e =>
                    e.Code.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase)
                );
                if (emote == null)
                    return;

                int index = _fragments.IndexOf(fragment);
                _fragments[index] = new()
                {
                    Type = "emote",
                    Text = fragment.Text,
                    Emote = new()
                    {
                        Id = emote.Id,
                        Provider = "bttv",
                        Urls = new()
                        {
                            { "1", new($"https://cdn.betterttv.net/emote/{emote.Id}/1x") },
                            { "2", new($"https://cdn.betterttv.net/emote/{emote.Id}/2x") },
                            { "3", new($"https://cdn.betterttv.net/emote/{emote.Id}/3x") },
                        },
                    },
                };
            }
        );
    }

    private void DecorateSevenTvEmotes()
    {
        if (!Config.UseSevenTvEmotes)
            return;

        Parallel.ForEach(
            _fragments.ToList(),
            _parallelOptions,
            fragment =>
            {
                if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text))
                    return;

                SevenTvEmote? emote = _sevenTvService.SevenTvEmotes.FirstOrDefault(e =>
                    e.Name.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase)
                );
                if (emote == null)
                    return;

                int index = _fragments.IndexOf(fragment);
                _fragments[index] = new()
                {
                    Type = "emote",
                    Text = fragment.Text,
                    Emote = new()
                    {
                        Id = emote.Id,
                        Provider = "7tv",
                        Urls = new()
                        {
                            { "1", new($"https://cdn.7tv.app/emote/{emote.Id}/1x") },
                            { "2", new($"https://cdn.7tv.app/emote/{emote.Id}/2x") },
                            { "3", new($"https://cdn.7tv.app/emote/{emote.Id}/3x") },
                            { "4", new($"https://cdn.7tv.app/emote/{emote.Id}/4x") },
                        },
                    },
                };
            }
        );
    }

    private void DecorateCodeSnippet()
    {
        if (!Config.UseChatCodeSnippets)
            return;
        // throw new NotImplementedException();
    }

    private void DecrateHtml()
    {
        if (!Config.UseChatHtmlParser)
            return;
        if (
            !_permissionService.UserHasMinLevel(
                ChatMessage.UserId,
                ChatMessage.UserType,
                "subscriber"
            )
        )
            return;

        // Check if any text fragment contains an opening HTML tag that might span across
        // multiple fragments (e.g. "<marquee> emote emote" where emotes are separate fragments).
        // In that case, we need to combine the fragments into a single HTML fragment with
        // emotes embedded as <img> tags.
        if (TryBuildMultiFragmentHtml())
            return;

        // turn all text fragments containing valid html into html fragments
        Parallel.ForEach(
            _fragments.ToList(),
            _parallelOptions,
            fragment =>
            {
                if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text))
                    return;

                if (!fragment.Text.Contains("<") || !fragment.Text.Contains(">"))
                    return;

                if (!_htmlMetadataService.ValidateHtml(fragment.Text, out HtmlDocument document))
                    return;

                int index = _fragments.IndexOf(fragment);
                _fragments[index] = new() { Type = "html", Text = document.DocumentNode.OuterHtml };
            }
        );
    }

    /// <summary>
    /// Detects HTML tags that span across multiple fragments (mixing text and emotes)
    /// and combines them into single HTML fragments with emotes embedded as img tags.
    /// For example: [text:"<marquee> "] [emote:"stoney90Waving"] [emote:"stoney90Waving"]
    /// becomes: [html:"<marquee><img src='...' alt='stoney90Waving'><img src='...' alt='stoney90Waving'></marquee>"]
    /// </summary>
    private bool TryBuildMultiFragmentHtml()
    {
        bool anyHtmlSpan = false;
        List<ChatMessageFragment> newFragments = [];
        int i = 0;

        while (i < _fragments.Count)
        {
            ChatMessageFragment fragment = _fragments[i];

            if (fragment.Type != "text" || !HasOpeningHtmlTag(fragment.Text))
            {
                newFragments.Add(fragment);
                i++;
                continue;
            }

            (List<ChatMessageFragment> spanFragments, int nextIndex) = CollectHtmlSpan(i);

            if (!spanFragments.Any(f => f.Type == "emote" && f.Emote != null))
            {
                newFragments.Add(fragment);
                i++;
                continue;
            }

            string combinedHtml = BuildCombinedHtml(spanFragments);

            if (!_htmlMetadataService.ValidateHtml(combinedHtml, out HtmlDocument document))
            {
                newFragments.Add(fragment);
                i++;
                continue;
            }

            newFragments.Add(new() { Type = "html", Text = document.DocumentNode.OuterHtml });
            anyHtmlSpan = true;
            i = nextIndex;
        }

        if (anyHtmlSpan)
            _fragments = newFragments;

        return anyHtmlSpan;
    }

    private (List<ChatMessageFragment> spanFragments, int nextIndex) CollectHtmlSpan(int startIndex)
    {
        List<ChatMessageFragment> spanFragments = [_fragments[startIndex]];
        bool foundClose = HasClosingHtmlTag(_fragments[startIndex].Text, out _);
        int j = startIndex + 1;

        while (j < _fragments.Count && !foundClose)
        {
            spanFragments.Add(_fragments[j]);

            if (_fragments[j].Type == "text" && HasClosingHtmlTag(_fragments[j].Text, out _))
                foundClose = true;

            j++;
        }

        return (spanFragments, j);
    }

    private static string BuildCombinedHtml(List<ChatMessageFragment> spanFragments)
    {
        StringBuilder htmlBuilder = new();

        foreach (ChatMessageFragment fragment in spanFragments)
        {
            if (fragment.Type != "emote" || fragment.Emote == null)
            {
                htmlBuilder.Append(fragment.Text);
                continue;
            }

            string? imgUrl = ResolveEmoteUrl(fragment.Emote);

            if (imgUrl != null)
                htmlBuilder.Append(
                    $"<img src=\"{imgUrl}\" alt=\"{WebUtility.HtmlEncode(fragment.Text)}\" />"
                );
            else
                htmlBuilder.Append(WebUtility.HtmlEncode(fragment.Text));
        }

        return htmlBuilder.ToString();
    }

    private static string? ResolveEmoteUrl(ChatEmote emote)
    {
        if (emote.Urls != null)
        {
            if (emote.Urls.TryGetValue("3", out Uri? url3))
                return url3.ToString();
            if (emote.Urls.TryGetValue("2", out Uri? url2))
                return url2.ToString();
            if (emote.Urls.TryGetValue("1", out Uri? url1))
                return url1.ToString();
        }

        if (!string.IsNullOrEmpty(emote.Id))
            return $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/3.0";

        return null;
    }

    private static bool HasOpeningHtmlTag(string text)
    {
        // Check for an opening HTML tag like <tagname or <tagname>
        // but not self-closing comments or just < in text
        int idx = text.IndexOf('<');
        if (idx < 0)
            return false;

        // Must have at least one letter after <
        for (int i = idx + 1; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsLetter(c))
                return true;
            if (c == '/' || c == '!')
                continue; // allow </tag or <!-- but keep scanning
            break;
        }
        return false;
    }

    private static bool HasClosingHtmlTag(string text, out string tagName)
    {
        tagName = "";
        // Check for a closing HTML tag like </tagname>
        int idx = text.IndexOf("</", StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int closeIdx = text.IndexOf('>', idx);
        if (closeIdx < 0)
            return false;

        tagName = text[(idx + 2)..closeIdx].Trim();
        return tagName.Length > 0;
    }

    private async Task DecorateUrlFragments()
    {
        foreach (ChatMessageFragment fragment in _fragments.ToList())
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text))
                continue;

            // Check if the text is a URL
            if (
                !Uri.TryCreate(fragment.Text, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
                continue;

            int index = _fragments.IndexOf(fragment);

            // Spotify track/episode URLs → safe metadata card for everyone
            if (IsSpotifyContentUrl(uri))
            {
                HtmlPreviewCustomContent? ogData = null;
                try
                {
                    ogData = await _htmlMetadataService.MakeComponent(uri, true);
                }
                catch
                {
                    // Fall back to minimal card if OG fetch fails
                }

                _fragments[index] = new()
                {
                    Type = "spotify",
                    Text = fragment.Text,
                    HtmlContent = ogData,
                };
                continue;
            }

            _fragments[index] = new()
            {
                Type = "url",
                Text = fragment.Text,
                HtmlContent =
                    Config.UseChatOgParser
                    && _permissionService.UserHasMinLevel(
                        ChatMessage.UserId,
                        ChatMessage.UserType,
                        "subscriber"
                    )
                        ? await _htmlMetadataService.MakeComponent(
                            uri,
                            _permissionService.UserHasMinLevel(
                                ChatMessage.UserId,
                                ChatMessage.UserType,
                                "subscriber"
                            )
                        )
                        : null,
            };
        }
    }

    private static bool IsSpotifyContentUrl(Uri uri)
    {
        return uri.Host.EndsWith("spotify.com", StringComparison.OrdinalIgnoreCase)
            && (
                uri.AbsolutePath.Contains("/track/", StringComparison.OrdinalIgnoreCase)
                || uri.AbsolutePath.Contains("/episode/", StringComparison.OrdinalIgnoreCase)
            );
    }
}
