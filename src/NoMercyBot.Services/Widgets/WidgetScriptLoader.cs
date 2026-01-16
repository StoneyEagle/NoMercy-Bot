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
        IServiceScopeFactory scopeFactory)
    {
        _widgetEventService = widgetEventService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _appDbContext = appDbContext;
        _logger = logger;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
    }

    public async Task LoadAllAsync()
    {
        HashSet<string> loadedScripts = new(StringComparer.OrdinalIgnoreCase);

        // First, load from project path (development scripts in source control)
        string? projectPath = AppFiles.ProjectWidgetsPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            _logger.LogInformation("Loading widget scripts from project path: {Path}", projectPath);
            foreach (string file in Directory.GetFiles(projectPath, "*.cs"))
            {
                string scriptName = Path.GetFileNameWithoutExtension(file);
                loadedScripts.Add(scriptName);
                await LoadScriptAsync(file);
            }
        }

        // Then, load from AppData path (user customizations), skipping already loaded scripts
        if (Directory.Exists(AppFiles.WidgetsPath))
        {
            _logger.LogInformation("Loading widget scripts from AppData path: {Path}", AppFiles.WidgetsPath);
            foreach (string file in Directory.GetFiles(AppFiles.WidgetsPath, "*.cs"))
            {
                string scriptName = Path.GetFileNameWithoutExtension(file);
                if (!loadedScripts.Contains(scriptName))
                {
                    await LoadScriptAsync(file);
                }
                else
                {
                    _logger.LogDebug("Skipping AppData widget script {ScriptName}, already loaded from project", scriptName);
                }
            }
        }
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string scriptName = Path.GetFileNameWithoutExtension(filePath);
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
                .AddImports("NoMercyBot.Services.Widgets")
                .AddImports("NoMercyBot.Services.Twitch")
                .AddImports("Microsoft.Extensions.DependencyInjection")
                .AddImports("Microsoft.EntityFrameworkCore");

            IWidgetScript script = await CSharpScript.EvaluateAsync<IWidgetScript>(scriptCode, options);

            WidgetScriptContext scriptCtx = new()
            {
                DatabaseContext = _appDbContext,
                ServiceProvider = _serviceProvider,
                WidgetEventService = _widgetEventService,
                TwitchApiService = _twitchApiService,
                TwitchChatService = _twitchChatService
            };

            await script.Init(scriptCtx);

            LoadedScripts.Add(script);

            _logger.LogInformation("Loaded widget script: {ScriptName} with events: {EventTypes}",
                scriptName, string.Join(", ", script.EventTypes));
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load widget script: {FilePath} - {ErrorMessage}", filePath, ex.Message);
        }
    }

    public IEnumerable<IWidgetScript> GetAllScripts()
    {
        return LoadedScripts;
    }
}
