using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;

namespace NoMercyBot.Services.Twitch.Scripting;

public class CommandScriptLoader
{
    private readonly TwitchCommandService _commandService;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly TtsService _ttsService;
    private readonly ILogger<CommandScriptLoader> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _appDbContext;

    public CommandScriptLoader(
        TwitchCommandService commandService,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        TtsService ttsService,
        AppDbContext appDbContext,
        ILogger<CommandScriptLoader> logger,
        IServiceScopeFactory scopeFactory)
    {
        _commandService = commandService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _ttsService = ttsService;
        _appDbContext = appDbContext;
        _logger = logger;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
    }

    public async Task LoadAllAsync()
    {
        ConcurrentDictionary<string, byte> loadedCommands = new(StringComparer.OrdinalIgnoreCase);

        // First, load from project path (development scripts in source control)
        string? projectPath = AppFiles.ProjectCommandsPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            _logger.LogInformation("Loading command scripts from project path: {Path}", projectPath);
            await Parallel.ForEachAsync(Directory.GetFiles(projectPath, "*.cs"),
                async (file, _) =>
                {
                    string commandName = Path.GetFileNameWithoutExtension(file);
                    loadedCommands.TryAdd(commandName, 0);
                    await LoadScriptAsync(file);
                });
        }

        // Then, load from AppData path (user customizations), skipping already loaded commands
        if (Directory.Exists(AppFiles.CommandsPath))
        {
            _logger.LogInformation("Loading command scripts from AppData path: {Path}", AppFiles.CommandsPath);
            await Parallel.ForEachAsync(Directory.GetFiles(AppFiles.CommandsPath, "*.cs"),
                async (file, _) =>
                {
                    string commandName = Path.GetFileNameWithoutExtension(file);
                    if (!loadedCommands.ContainsKey(commandName))
                    {
                        await LoadScriptAsync(file);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping AppData command {CommandName}, already loaded from project", commandName);
                    }
                });
        }
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string commandName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            ScriptOptions options = ScriptOptions.Default;

            IEnumerable<string> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location);

            options = options.AddReferences(assemblies);

            IBotCommand botCommand = await CSharpScript.EvaluateAsync<IBotCommand>(scriptCode, options);

            ChatCommand chatCommand = new()
            {
                Name = botCommand.Name,
                Permission = botCommand.Permission,
                Callback = async ctx =>
                {
                    CommandScriptContext scriptCtx = new()
                    {
                        Channel = ctx.Message.Broadcaster.Username,
                        BroadcasterId = ctx.BroadcasterId,
                        CommandName = ctx.CommandName,
                        Arguments = ctx.Arguments,
                        Message = ctx.Message,
                        ReplyAsync = ctx.ReplyAsync,
                        CancellationToken = ctx.CancellationToken,
                        DatabaseContext = ctx.DatabaseContext,
                        ServiceProvider = ctx.ServiceProvider,
                        TwitchChatService = ctx.TwitchChatService,
                        TwitchApiService = ctx.TwitchApiService,
                        TtsService = ctx.TtsService
                    };

                    await botCommand.Callback(scriptCtx);
                }
            };

            CommandScriptContext scriptCtx = new()
            {
                DatabaseContext = new(),
                ServiceProvider = _serviceProvider,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService,
                TtsService = _ttsService
            };

            await botCommand.Init(scriptCtx);

            _commandService.RegisterCommand(chatCommand);

            _logger.LogInformation($"Loaded command script: {commandName}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load command script: {filePath} - {ex.Message}");
        }
    }
}