using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Twitch.Scripting;

public class RewardChangeHandler
{
    public Guid RewardId { get; set; }
    public string? RewardTitle { get; set; }
    public IRewardChangeHandler Handler { get; set; } = null!;
}

public class RewardChangeScriptLoader
{
    private readonly TwitchRewardChangeService _rewardChangeService;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger<RewardChangeScriptLoader> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _appDbContext;
    private static readonly ConcurrentDictionary<string, RewardChangeHandler> HandlersById = new();
    private static readonly ConcurrentDictionary<string, RewardChangeHandler> HandlersByTitle =
        new();

    public RewardChangeScriptLoader(
        TwitchRewardChangeService rewardChangeService,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        AppDbContext appDbContext,
        ILogger<RewardChangeScriptLoader> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _rewardChangeService = rewardChangeService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _appDbContext = appDbContext;
        _logger = logger;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
    }

    private readonly List<string> _loadedHandlerNames = [];

    public async Task LoadAllAsync()
    {
        string? projectPath = AppFiles.ProjectChangesPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            foreach (string file in Directory.GetFiles(projectPath, "*.cs"))
            {
                await LoadScriptAsync(file);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} reward change handlers: {Names}",
            _loadedHandlerNames.Count,
            string.Join(", ", _loadedHandlerNames)
        );
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string handlerName = Path.GetFileNameWithoutExtension(filePath);
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

            IRewardChangeHandler handler = await CSharpScript.EvaluateAsync<IRewardChangeHandler>(
                scriptCode,
                options
            );

            RewardChangeContext scriptCtx = new()
            {
                DatabaseContext = _appDbContext,
                ServiceProvider = _serviceProvider,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService,
            };

            await handler.Init(scriptCtx);

            RegisterHandler(handler);
            _loadedHandlerNames.Add(handlerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Failed to load reward change handler: {FilePath} - {ErrorMessage}",
                filePath,
                ex.Message
            );
        }
    }

    public void RegisterHandler(IRewardChangeHandler handler)
    {
        RewardChangeHandler changeHandler = new()
        {
            RewardId = handler.RewardId,
            RewardTitle = handler.RewardTitle,
            Handler = handler,
        };

        if (handler.RewardId != Guid.Empty)
            HandlersById.TryAdd(handler.RewardId.ToString(), changeHandler);

        if (!string.IsNullOrEmpty(handler.RewardTitle))
            HandlersByTitle.TryAdd(handler.RewardTitle.ToLowerInvariant(), changeHandler);

        _logger.LogDebug(
            "Registered/Updated reward change handler: {RewardTitle} (ID: {RewardId})",
            handler.RewardTitle ?? "Unknown",
            handler.RewardId
        );
    }

    public IRewardChangeHandler? GetHandler(Guid rewardId)
    {
        if (HandlersById.TryGetValue(rewardId.ToString(), out RewardChangeHandler? handler))
            return handler.Handler;
        return null;
    }

    public IRewardChangeHandler? GetHandler(string rewardTitle)
    {
        if (
            HandlersByTitle.TryGetValue(
                rewardTitle.ToLowerInvariant(),
                out RewardChangeHandler? handler
            )
        )
            return handler.Handler;
        return null;
    }

    public IEnumerable<IRewardChangeHandler> ListHandlers()
    {
        return HandlersById
            .Values.Concat(HandlersByTitle.Values)
            .Distinct(new RewardChangeHandlerComparer())
            .Select(h => h.Handler);
    }

    private class RewardChangeHandlerComparer : IEqualityComparer<RewardChangeHandler>
    {
        public bool Equals(RewardChangeHandler? x, RewardChangeHandler? y)
        {
            if (x == null || y == null)
                return x == y;
            return x.RewardId == y.RewardId;
        }

        public int GetHashCode(RewardChangeHandler obj)
        {
            return obj.RewardId.GetHashCode();
        }
    }
}
