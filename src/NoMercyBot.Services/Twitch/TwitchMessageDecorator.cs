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
        CancellationToken cancellationToken = default)
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
            MaxDegreeOfParallelism = (int)Math.Floor(Environment.ProcessorCount / 2.0)
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
        if (DecorateCommand()) return;
        if (!HasDecoratorServices) return;

        List<ChatMessageFragment> newFragments = [];

        foreach (ChatMessageFragment fragment in _fragments)
        {
            int index = _fragments.IndexOf(fragment);

            if (fragment.Type != "text")
            {
                newFragments.Add(fragment);

                if (_fragments.ElementAtOrDefault(index - 0)?.Text != " ")
                    newFragments.Add(new()
                    {
                        Type = "text",
                        Text = " "
                    });

                continue;
            }

            string[] words = fragment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (newFragments.Count > 0 || _fragments.ElementAtOrDefault(index - 0)?.Text != " ")
                    newFragments.Add(new()
                    {
                        Type = "text",
                        Text = " "
                    });

                newFragments.Add(new()
                {
                    Type = "text",
                    Text = string.IsNullOrEmpty(word)
                        // replace all versions of whitespace with a single space
                        ? $"{word}"
                        : word
                });

                if (newFragments.LastOrDefault()?.Text != " ")
                    newFragments.Add(new()
                    {
                        Type = "text",
                        Text = " "
                    });
            }
        }

        if (newFragments.FirstOrDefault()?.Text == " ") newFragments.RemoveAt(0);

        if (newFragments.LastOrDefault()?.Text == " ") newFragments.RemoveAt(newFragments.Count - 1);

        _fragments = newFragments;
    }

    private void ImplodeTextFragments()
    {
        if (!HasDecoratorServices) return;

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
                    newFragments.Add(new()
                    {
                        Type = "text",
                        Text = currentText.ToString(),
                    });
                    currentText.Clear();
                }

                newFragments.Add(fragment);
            }

        if (currentText.Length > 0)
            newFragments.Add(new()
            {
                Type = "text",
                Text = currentText.ToString(),
            });

        _fragments = newFragments
            .Where(fragment => fragment.Text != " ")
            .ToList();
    }

    private bool DecorateCommand()
    {
        ChatMessageFragment? firstFragment = _fragments.FirstOrDefault();

        if (firstFragment is null || firstFragment.Type != "text" || !firstFragment.Text.StartsWith("!"))
            return false;

        ChatMessage.IsCommand = true;

        string text = string.Join("", _fragments.Select(x => x.Text));

        string[] parts = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
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
                Args = args
            }
        ];

        return true;
    }

    private void DecorateTwitchEmotes()
    {
        Parallel.ForEach(_fragments.ToList(), _parallelOptions, (fragment) =>
        {
            if (fragment.Type != "emote" || fragment.Emote is null) return;

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
                        { "1", new($"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/1.0") },
                        { "2", new($"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/2.0") },
                        { "3", new($"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.Emote.Id}/default/dark/3.0") }
                    }
                }
            };
        });
    }

    private void DecorateBadges()
    {
        if (ChatMessage.Badges.Count == 0) ChatMessage.Badges = [];

        List<ChatBadge> badges = [];
        foreach (ChatBadge badge in ChatMessage.Badges)
        {
            ChatBadge? badgeResult = _twitchBadgeService.TwitchBadges
                .LastOrDefault(b => b.SetId == badge.SetId && b.Id == badge.Id);

            if (badgeResult != null) badges.Add(badgeResult);
        }

        ChatMessage.Badges = badges;
    }

    private void DecorateFrankerFaceEmotes()
    {
        if (!Config.UseFrankerfacezEmotes) return;

        Parallel.ForEach(_fragments.ToList(), _parallelOptions, fragment =>
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text)) return;

            Emoticon? emote = _frankerFacezService.FrankerFacezEmotes
                .FirstOrDefault(e => e.Name.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase));
            if (emote == null) return;

            int index = _fragments.IndexOf(fragment);
            _fragments[index] = new()
            {
                Type = "emote",
                Text = fragment.Text,
                Emote = new()
                {
                    Id = emote.Id.ToString(),
                    Provider = "frankerfacez",
                    Urls = emote.Urls
                }
            };
        });
    }

    private void DecorateBttvEmotes()
    {
        if (!Config.UseBttvEmotes) return;

        Parallel.ForEach(_fragments.ToList(), _parallelOptions, fragment =>
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text)) return;

            BttvEmote? emote = _bttvService.BttvEmotes
                .FirstOrDefault(e => e.Code.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase));
            if (emote == null) return;

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
                        { "3", new($"https://cdn.betterttv.net/emote/{emote.Id}/3x") }
                    }
                }
            };
        });
    }

    private void DecorateSevenTvEmotes()
    {
        if (!Config.UseSevenTvEmotes) return;

        Parallel.ForEach(_fragments.ToList(), _parallelOptions, fragment =>
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text)) return;

            SevenTvEmote? emote = _sevenTvService.SevenTvEmotes
                .FirstOrDefault(e => e.Name.Equals(fragment.Text, StringComparison.OrdinalIgnoreCase));
            if (emote == null) return;

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
                        { "4", new($"https://cdn.7tv.app/emote/{emote.Id}/4x") }
                    }
                }
            };
        });
    }

    private void DecorateCodeSnippet()
    {
        if (!Config.UseChatCodeSnippets) return;
        // throw new NotImplementedException();
    }

    private void DecrateHtml()
    {
        if (!Config.UseChatHtmlParser) return;
        if (!_permissionService.HasMinLevel(ChatMessage.UserType, "subscriber")) return;

        // turn all text fragments containing valid html into html fragments
        Parallel.ForEach(_fragments.ToList(), _parallelOptions, fragment =>
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text)) return;

            if (!fragment.Text.Contains("<") || !fragment.Text.Contains(">")) return;

            if (!_htmlMetadataService.ValidateHtml(fragment.Text, out HtmlDocument document)) return;

            int index = _fragments.IndexOf(fragment);
            _fragments[index] = new()
            {
                Type = "html",
                Text = document.ParsedText
            };
        });
    }

    private async Task DecorateUrlFragments()
    {
        foreach (ChatMessageFragment fragment in _fragments.ToList())
        {
            if (fragment.Type != "text" || string.IsNullOrWhiteSpace(fragment.Text)) continue;

            // Check if the text is a URL
            if (!Uri.TryCreate(fragment.Text, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) continue;

            int index = _fragments.IndexOf(fragment);
            _fragments[index] = new()
            {
                Type = "url",
                Text = fragment.Text,
                HtmlContent = Config.UseChatOgParser && _permissionService.HasMinLevel(ChatMessage.UserType, "subscriber")
                    ? await _htmlMetadataService.MakeComponent(uri,
                        ChatMessage.UserType is "Subscriber" or "Vip" or "Moderator" or "Broadcaster")
                    : null
            };
        }
    }
}