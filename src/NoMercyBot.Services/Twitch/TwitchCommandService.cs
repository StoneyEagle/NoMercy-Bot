using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Other;

namespace NoMercyBot.Services.Twitch;

public class CommandUsageRecord
{
    [JsonProperty("command")]
    public string Command { get; set; } = null!;

    [JsonProperty("arguments")]
    public string[] Arguments { get; set; } = [];
}

public enum CommandPermission
{
    Broadcaster,
    Moderator,
    Vip,
    Subscriber,
    Everyone,
}

public static class PermissionDeniedReplies
{
    private static readonly string[] _modReplies =
    {
        "Do you see a sword next to your name? No? Then this command isn't for you, bestie.",
        "Last I checked, you don't have a sword. Come back when Twitch trusts you with one.",
        "That command requires a mod sword. Yours appears to be... imaginary.",
        "Nice try, but you'd need a sword icon for that. Yours is still in the mail, apparently.",
        "I see no sword, no axe, nothing. Just audacity. Impressive, but insufficient.",
        "This command is above your pay grade. And by pay grade, I mean badge grade.",
        "You tried to use a mod command without being a mod. Bold strategy. Didn't work though.",
        "Access denied. Your badge collection is missing a very important piece: a sword.",
        "That's a mod-only command. You're giving 'I asked the teacher for the answer key' energy.",
        "Imagine having a sword next to your name. Now stop imagining, because you don't.",
        "Sorry, that command requires a little sword icon. You know, the one you don't have.",
        "I'd let you use that command but my programming says no. And honestly? I agree with it.",
        "You need mod powers for that. Currently your power level is 'guy who types in chat.'",
        "Command reserved for people with shiny badges. Your badge collection? Still a work in progress.",
        "Swordless AND trying mod commands? The audacity is almost admirable. Almost.",
    };

    private static readonly string[] _subReplies =
    {
        "That command is for subs, but don't sweat it - you're still cool for hanging out here.",
        "Sub-only command, sorry! But hey, your presence in chat is already legendary.",
        "That one's locked to subs. Nothing personal - just how it's set up!",
        "Oops, that's a sub perk! But honestly, just being here is what matters.",
        "Sub-only command. But you're vibing in chat and that's worth more than any badge.",
        "That's reserved for subs, but you're still VIP in our hearts.",
        "Can't let you use that one, it's sub-only. Stick around though, you're great company!",
        "That command needs a sub badge, but your chat energy? Priceless. No badge needed.",
        "Sub-only, sorry! But the best things in this stream are free - like the chaos.",
        "That's behind the sub wall. But you being here? That's the real content.",
    };

    private static readonly string[] _vipReplies =
    {
        "That's a VIP command, but you're still a star in this chat!",
        "VIP-only, sorry! Doesn't make you any less awesome though.",
        "That one needs the gem badge. But gems are overrated - your chat game is fire.",
        "VIP command, can't let you through on this one. You're still great company though!",
        "That's locked to VIPs. But real talk, your presence here is what makes the stream good.",
        "Can't do that one without VIP. But you're out here being a legend in chat regardless.",
        "VIP-only command! But between us, badges don't define how cool you are in here.",
        "That needs VIP access. Hang tight though, you're already one of the best chatters here.",
        "VIP command, sorry! But honestly, the best vibes in chat come from everyone.",
        "That's a VIP perk, but don't let that stop you from being the real MVP of chat.",
    };

    private static readonly string[] _broadcasterReplies =
    {
        "Nice try, but that's a broadcaster-only command. You'd need to own this channel for that.",
        "That command is reserved for the one who pays the bills around here.",
        "Broadcaster only. Unless you've secretly been running this stream the whole time?",
        "You need to be the streamer for that one. Last I checked, you're on the wrong side of the camera.",
        "That's above everyone's pay grade except one person. And that person isn't you.",
    };

    public static string GetRandomReply(CommandPermission requiredLevel)
    {
        string[] replies = requiredLevel switch
        {
            CommandPermission.Broadcaster => _broadcasterReplies,
            CommandPermission.Moderator => _modReplies,
            CommandPermission.Vip => _vipReplies,
            CommandPermission.Subscriber => _subReplies,
            _ => _modReplies,
        };
        return replies[Random.Shared.Next(replies.Length)];
    }
}

public enum CommandType
{
    Command,
    Event,
    Message,
}

public class CommandContext
{
    public string Channel { get; set; } = null!;
    public string BroadcasterId { get; set; } = null!;
    public string CommandName { get; set; } = null!;
    public string[] Arguments { get; set; } = [];
    public ChatMessage Message { get; set; } = null!;
    public Func<string, Task> ReplyAsync { get; set; } = null!;
    public required AppDbContext DatabaseContext { get; init; } = null!;
    public required TwitchCommandService CommandService { get; set; } = null!;
    public required TwitchApiService TwitchApiService { get; set; } = null!;
    public required IServiceProvider ServiceProvider { get; set; } = null!;
    public required TwitchChatService TwitchChatService { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
    public TtsService TtsService { get; set; } = null!;
}

public class ChatCommand
{
    public string Name { get; set; } = null!;
    public CommandPermission Permission { get; set; } = CommandPermission.Everyone;
    public CommandType Type { get; set; } = CommandType.Command;
    public object? Storage { get; set; }
    public Func<CommandContext, Task> Callback { get; set; } = null!;
}

public class TwitchCommandService
{
    private static readonly ConcurrentDictionary<string, ChatCommand> Commands = new();
    private readonly ILogger<TwitchCommandService> _logger;
    private readonly AppDbContext _appDbContext;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly TtsService _ttsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly PermissionService _permissionService;

    public TwitchCommandService(
        AppDbContext appDbContext,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        TtsService ttsService,
        PermissionService permissionService,
        IServiceScopeFactory scopeFactory,
        ILogger<TwitchCommandService> logger
    )
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _ttsService = ttsService;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
        _permissionService = permissionService;
        LoadCommandsFromDatabase();
    }

    private void LoadCommandsFromDatabase()
    {
        lock (_appDbContext)
        {
            List<Command> dbCommands = _appDbContext.Commands.Where(c => c.IsEnabled).ToList();

            Parallel.ForEach(
                dbCommands,
                (dbCommand, _) =>
                {
                    RegisterCommand(
                        new()
                        {
                            Name = dbCommand.Name,
                            Permission = Enum.TryParse<CommandPermission>(
                                dbCommand.Permission,
                                true,
                                out CommandPermission perm
                            )
                                ? perm
                                : CommandPermission.Everyone,
                            Type = Enum.TryParse<CommandType>(
                                dbCommand.Type,
                                true,
                                out CommandType type
                            )
                                ? type
                                : CommandType.Command,
                            Callback = async ctx => await ctx.ReplyAsync(dbCommand.Response),
                        }
                    );
                }
            );
        }
    }

    public bool RegisterCommand(ChatCommand command)
    {
        Commands.TryAdd(command.Name.ToLowerInvariant(), command);

        _logger.LogDebug("Registered/Updated command: {CommandName}", command.Name);
        return true;
    }

    public bool RemoveCommand(string commandName)
    {
        return Commands.TryRemove(commandName.ToLowerInvariant(), out _);
    }

    public bool UpdateCommand(ChatCommand command)
    {
        Commands[command.Name.ToLowerInvariant()] = command;
        return true;
    }

    public IEnumerable<ChatCommand> ListCommands()
    {
        return Commands.Values;
    }

    public async Task ExecuteCommand(ChatMessage message)
    {
        if (!message.IsCommand || string.IsNullOrWhiteSpace(message.Message))
            return;

        ChatMessageFragment commandFragment = message.Fragments.First();

        if (Commands.TryGetValue(commandFragment.Command!, out ChatCommand? command))
        {
            if (
                !_permissionService.UserHasMinLevel(
                    message.UserId,
                    message.UserType,
                    command.Permission.ToString().ToLowerInvariant()
                )
            )
            {
                if (command.Permission != CommandPermission.Everyone)
                {
                    string reply = $"@{message.DisplayName} {PermissionDeniedReplies.GetRandomReply(command.Permission)}";
                    await _twitchChatService.SendReplyAsBot(message.Broadcaster.Username, reply, message.Id);
                }
                return;
            }

            CommandContext context = new()
            {
                Channel = message.BroadcasterId,
                BroadcasterId = message.BroadcasterId,
                CommandName = commandFragment.Command!,
                Arguments = commandFragment.Args!,
                Message = message,
                CommandService = this,
                ServiceProvider = _serviceProvider,
                TwitchApiService = _twitchApiService,
                TwitchChatService = _twitchChatService,
                TtsService = _ttsService,
                DatabaseContext = _appDbContext,
                ReplyAsync = async (reply) =>
                {
                    _logger.LogInformation($"Reply to {message.Username}: {reply}");
                    await _twitchChatService.SendMessageAsBot(message.Broadcaster.Username, reply);
                    await Task.CompletedTask;
                },
            };
            await command.Callback(context);

            // Track command usage for leaderboard
            try
            {
                _appDbContext.Records.Add(
                    new Record
                    {
                        UserId = message.UserId,
                        RecordType = "CommandUsage",
                        Data = new CommandUsageRecord
                        {
                            Command = commandFragment.Command!,
                            Arguments = commandFragment.Args ?? [],
                        }.ToJson(),
                    }
                );
                await _appDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to track command usage: {Error}", ex.Message);
            }
        }
        else
        {
            _logger.LogDebug($"Unknown command: {commandFragment.Command!}");
        }
    }

    public async Task ExecuteCommandByName(string commandName, string[] args, ChatMessage message)
    {
        if (!Commands.TryGetValue(commandName.ToLowerInvariant(), out ChatCommand? command))
            return;

        if (
            !_permissionService.UserHasMinLevel(
                message.UserId,
                message.UserType,
                command.Permission.ToString().ToLowerInvariant()
            )
        )
        {
            if (command.Permission != CommandPermission.Everyone)
            {
                string reply = $"@{message.DisplayName} {PermissionDeniedReplies.GetRandomReply(command.Permission)}";
                await _twitchChatService.SendReplyAsBot(message.Broadcaster.Username, reply, message.Id);
            }
            return;
        }

        CommandContext context = new()
        {
            Channel = message.BroadcasterId,
            BroadcasterId = message.BroadcasterId,
            CommandName = commandName,
            Arguments = args,
            Message = message,
            CommandService = this,
            ServiceProvider = _serviceProvider,
            TwitchApiService = _twitchApiService,
            TwitchChatService = _twitchChatService,
            TtsService = _ttsService,
            DatabaseContext = _appDbContext,
            ReplyAsync = async (reply) =>
            {
                _logger.LogInformation($"Reply to {message.Username}: {reply}");
                await _twitchChatService.SendMessageAsBot(message.Broadcaster.Username, reply);
            },
        };
        await command.Callback(context);
    }

    public async Task AddOrUpdateUserCommandAsync(
        string name,
        string response,
        string permission = "everyone",
        string type = "command",
        bool isEnabled = true,
        string? description = null
    )
    {
        Command? dbCommand = await _appDbContext.Commands.FirstOrDefaultAsync(c => c.Name == name);
        if (dbCommand == null)
        {
            dbCommand = new()
            {
                Name = name,
                Response = response,
                Permission = permission,
                Type = type,
                IsEnabled = isEnabled,
                Description = description,
            };
            await _appDbContext.Commands.AddAsync(dbCommand);
        }
        else
        {
            dbCommand.Name = name;
            dbCommand.Response = response;
            dbCommand.Permission = permission;
            dbCommand.Type = type;
            dbCommand.IsEnabled = isEnabled;
            dbCommand.Description = description;
            _appDbContext.Commands.Update(dbCommand);
        }

        await _appDbContext.SaveChangesAsync();

        RegisterCommand(
            new()
            {
                Name = dbCommand.Name,
                Permission = Enum.TryParse<CommandPermission>(
                    dbCommand.Permission,
                    true,
                    out CommandPermission perm
                )
                    ? perm
                    : CommandPermission.Everyone,
                Type = Enum.TryParse<CommandType>(dbCommand.Type, true, out CommandType commandType)
                    ? commandType
                    : CommandType.Command,
                Callback = async ctx => await ctx.ReplyAsync(dbCommand.Response),
            }
        );
    }

    public async Task<bool> RemoveUserCommandAsync(string name)
    {
        Command? dbCommand = await _appDbContext.Commands.FirstOrDefaultAsync(c => c.Name == name);
        if (dbCommand == null)
            return false;
        _appDbContext.Commands.Remove(dbCommand);
        await _appDbContext.SaveChangesAsync();
        RemoveCommand(name);
        return true;
    }
}
