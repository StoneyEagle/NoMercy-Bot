using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Widgets;

public class WidgetScriptLoader
{
    private readonly IWidgetEventService _widgetEventService;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger<WidgetScriptLoader> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _appDbContext;
    private static readonly ConcurrentBag<IWidgetScript> LoadedScripts = new();

    public WidgetScriptLoader(
        IWidgetEventService widgetEventService,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        AppDbContext appDbContext,
        ILogger<WidgetScriptLoader> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _widgetEventService = widgetEventService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _appDbContext = appDbContext;
        _logger = logger;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
    }

    private readonly List<string> _loadedScriptNames = [];

    public async Task LoadAllAsync()
    {
        string? projectPath = AppFiles.ProjectWidgetsPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            foreach (string file in Directory.GetFiles(projectPath, "*.cs"))
            {
                await LoadScriptAsync(file);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} widget scripts: {Names}",
            _loadedScriptNames.Count,
            string.Join(", ", _loadedScriptNames)
        );
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string scriptName = Path.GetFileNameWithoutExtension(filePath);
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

            IWidgetScript script = await CSharpScript.EvaluateAsync<IWidgetScript>(
                scriptCode,
                options
            );

            WidgetScriptContext scriptCtx = new()
            {
                DatabaseContext = _appDbContext,
                ServiceProvider = _serviceProvider,
                WidgetEventService = _widgetEventService,
                TwitchApiService = _twitchApiService,
                TwitchChatService = _twitchChatService,
            };

            await script.Init(scriptCtx);

            LoadedScripts.Add(script);
            _loadedScriptNames.Add(scriptName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Failed to load widget script: {FilePath} - {ErrorMessage}",
                filePath,
                ex.Message
            );
        }
    }

    public IEnumerable<IWidgetScript> GetAllScripts()
    {
        return LoadedScripts;
    }
}
