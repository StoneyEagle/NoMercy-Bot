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
        IServiceScopeFactory scopeFactory
    )
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

    private readonly ConcurrentBag<string> _loadedCommandNames = [];

    public async Task LoadAllAsync()
    {
        string? projectPath = AppFiles.ProjectCommandsPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            await Parallel.ForEachAsync(
                Directory.GetFiles(projectPath, "*.cs"),
                async (file, _) =>
                {
                    await LoadScriptAsync(file);
                }
            );
        }

        List<string> sorted = _loadedCommandNames.OrderBy(n => n).ToList();
        _logger.LogInformation(
            "Loaded {Count} command scripts: {Names}",
            sorted.Count,
            string.Join(", ", sorted)
        );
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string commandName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            ScriptOptions options = ScriptOptions.Default;

            IEnumerable<string> assemblies = AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location);

            options = options
                .AddReferences(assemblies)
                .AddImports("System")
                .AddImports("System.Linq")
                .AddImports("System.Threading.Tasks")
                .AddImports("System.Collections.Generic")
                .AddImports("Microsoft.EntityFrameworkCore")
                .AddImports("Microsoft.Extensions.DependencyInjection")
                .AddImports("NoMercyBot.Database.Models")
                .AddImports("NoMercyBot.Services.Interfaces")
                .AddImports("NoMercyBot.Services.Twitch")
                .AddImports("NoMercyBot.Services.Twitch.Scripting")
                .AddImports("NoMercyBot.Services.Other")
                .AddImports("NoMercyBot.Services.Widgets")
                .AddImports("NoMercyBot.Globals.SystemCalls")
                .AddImports("NoMercyBot.Globals.NewtonSoftConverters");

            IBotCommand botCommand = await CSharpScript.EvaluateAsync<IBotCommand>(
                scriptCode,
                options
            );

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
                        TtsService = ctx.TtsService,
                    };

                    await botCommand.Callback(scriptCtx);
                },
            };

            CommandScriptContext scriptCtx = new()
            {
                DatabaseContext = new(),
                ServiceProvider = _serviceProvider,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService,
                TtsService = _ttsService,
            };

            await botCommand.Init(scriptCtx);

            _commandService.RegisterCommand(chatCommand);
            _loadedCommandNames.Add(commandName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load command script: {filePath} - {ex.Message}");
        }
    }
}
