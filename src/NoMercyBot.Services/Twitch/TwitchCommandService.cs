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
                return;

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
            !_permissionService.HasMinLevel(
                message.UserId,
                message.UserType,
                command.Permission.ToString().ToLowerInvariant()
            )
        )
            return;

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
