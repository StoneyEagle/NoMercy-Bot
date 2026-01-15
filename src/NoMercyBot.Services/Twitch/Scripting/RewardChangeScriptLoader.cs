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
    private static readonly ConcurrentDictionary<string, RewardChangeHandler> HandlersByTitle = new();

    public RewardChangeScriptLoader(
        TwitchRewardChangeService rewardChangeService,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        AppDbContext appDbContext,
        ILogger<RewardChangeScriptLoader> logger,
        IServiceScopeFactory scopeFactory)
    {
        _rewardChangeService = rewardChangeService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _appDbContext = appDbContext;
        _logger = logger;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
    }

    public async Task LoadAllAsync()
    {
        HashSet<string> loadedHandlers = new(StringComparer.OrdinalIgnoreCase);

        // First, load from project path (development scripts in source control)
        string? projectPath = AppFiles.ProjectChangesPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            _logger.LogInformation("Loading reward change handlers from project path: {Path}", projectPath);
            foreach (string file in Directory.GetFiles(projectPath, "*.cs"))
            {
                string handlerName = Path.GetFileNameWithoutExtension(file);
                loadedHandlers.Add(handlerName);
                await LoadScriptAsync(file);
            }
        }

        // Then, load from AppData path (user customizations), skipping already loaded handlers
        if (Directory.Exists(AppFiles.ChangesPath))
        {
            _logger.LogInformation("Loading reward change handlers from AppData path: {Path}", AppFiles.ChangesPath);
            foreach (string file in Directory.GetFiles(AppFiles.ChangesPath, "*.cs"))
            {
                string handlerName = Path.GetFileNameWithoutExtension(file);
                if (!loadedHandlers.Contains(handlerName))
                {
                    await LoadScriptAsync(file);
                }
                else
                {
                    _logger.LogDebug("Skipping AppData handler {HandlerName}, already loaded from project", handlerName);
                }
            }
        }
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string handlerName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            ScriptOptions options = ScriptOptions.Default;

            IEnumerable<string> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location);

            options = options.AddReferences(assemblies)
                .AddImports("System")
                .AddImports("System.Linq")
                .AddImports("System.Threading.Tasks")
                .AddImports("System.Collections.Generic")
                .AddImports("NoMercyBot.Services.Interfaces")
                .AddImports("NoMercyBot.Services.Twitch")
                .AddImports("NoMercyBot.Services.Twitch.Scripting")
                .AddImports("Microsoft.Extensions.DependencyInjection");

            IRewardChangeHandler handler = await CSharpScript.EvaluateAsync<IRewardChangeHandler>(scriptCode, options);

            RewardChangeContext scriptCtx = new()
            {
                DatabaseContext = _appDbContext,
                ServiceProvider = _serviceProvider,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService
            };

            await handler.Init(scriptCtx);

            RegisterHandler(handler);

            _logger.LogInformation("Loaded reward change handler: {HandlerName}", handlerName);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load reward change handler: {FilePath} - {ErrorMessage}", filePath, ex.Message);
        }
    }

    public void RegisterHandler(IRewardChangeHandler handler)
    {
        RewardChangeHandler changeHandler = new()
        {
            RewardId = handler.RewardId,
            RewardTitle = handler.RewardTitle,
            Handler = handler
        };

        if (handler.RewardId != Guid.Empty)
            HandlersById.TryAdd(handler.RewardId.ToString(), changeHandler);

        if (!string.IsNullOrEmpty(handler.RewardTitle))
            HandlersByTitle.TryAdd(handler.RewardTitle.ToLowerInvariant(), changeHandler);

        _logger.LogInformation("Registered/Updated reward change handler: {RewardTitle} (ID: {RewardId})",
            handler.RewardTitle ?? "Unknown", handler.RewardId);
    }

    public IRewardChangeHandler? GetHandler(Guid rewardId)
    {
        if (HandlersById.TryGetValue(rewardId.ToString(), out RewardChangeHandler? handler))
            return handler.Handler;
        return null;
    }

    public IRewardChangeHandler? GetHandler(string rewardTitle)
    {
        if (HandlersByTitle.TryGetValue(rewardTitle.ToLowerInvariant(), out RewardChangeHandler? handler))
            return handler.Handler;
        return null;
    }

    public IEnumerable<IRewardChangeHandler> ListHandlers()
    {
        return HandlersById.Values.Concat(HandlersByTitle.Values)
            .Distinct(new RewardChangeHandlerComparer())
            .Select(h => h.Handler);
    }

    private class RewardChangeHandlerComparer : IEqualityComparer<RewardChangeHandler>
    {
        public bool Equals(RewardChangeHandler? x, RewardChangeHandler? y)
        {
            if (x == null || y == null) return x == y;
            return x.RewardId == y.RewardId;
        }

        public int GetHashCode(RewardChangeHandler obj)
        {
            return obj.RewardId.GetHashCode();
        }
    }
}

