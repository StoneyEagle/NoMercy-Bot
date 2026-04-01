using System.Reflection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.NewtonSoftConverters;

namespace NoMercyBot.Services.Widgets;

public class WidgetScaffoldService : IWidgetScaffoldService
{
    private readonly ILogger<WidgetScaffoldService> _logger;

    private readonly Dictionary<string, string> _supportedFrameworks = new()
    {
        { "vanilla", "Vanilla HTML/CSS/JavaScript" },
        { "vue", "Vue 3 with Composition API" },
        { "react", "React with TypeScript" },
        { "svelte", "Svelte with TypeScript" },
        { "angular", "Angular with TypeScript" },
    };

    public WidgetScaffoldService(ILogger<WidgetScaffoldService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CreateWidgetScaffoldAsync(
        Ulid widgetId,
        string widgetName,
        string framework,
        Dictionary<string, object> settings
    )
    {
        try
        {
            string sourcePath = WidgetFiles.GetWidgetSourcePath(widgetId);
            string distPath = WidgetFiles.GetWidgetDistPath(widgetId);

            // Ensure directories exist
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(distPath);

            return framework.ToLowerInvariant() switch
            {
                "vanilla" => await CreateFromStubs(
                    sourcePath,
                    "vanilla",
                    widgetId,
                    widgetName,
                    settings
                ),
                "vue" => await CreateFromStubs(sourcePath, "vue", widgetId, widgetName, settings),
                "react" => await CreateFromStubs(
                    sourcePath,
                    "react",
                    widgetId,
                    widgetName,
                    settings
                ),
                "svelte" => await CreatePlaceholder(sourcePath, widgetName, "Svelte"),
                "angular" => await CreatePlaceholder(sourcePath, widgetName, "Angular"),
                _ => throw new ArgumentException($"Unsupported framework: {framework}"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create widget scaffold for {Framework}: {Message}",
                framework,
                ex.Message
            );
            return false;
        }
    }

    public Task<bool> ValidateFrameworkAsync(string framework)
    {
        return Task.FromResult(_supportedFrameworks.ContainsKey(framework.ToLowerInvariant()));
    }

    public List<string> GetSupportedFrameworks()
    {
        return _supportedFrameworks.Keys.ToList();
    }

    private async Task<bool> CreateFromStubs(
        string targetPath,
        string framework,
        Ulid widgetId,
        string widgetName,
        Dictionary<string, object> settings
    )
    {
        try
        {
            // Get the path to the stub files
            string stubsPath = GetStubsPath(framework);

            if (!Directory.Exists(stubsPath))
            {
                _logger.LogError(
                    "Stub directory not found for framework {Framework}: {Path}",
                    framework,
                    stubsPath
                );
                return false;
            }

            // Create template replacement values
            Dictionary<string, string> replacements = new()
            {
                { "{{WIDGET_ID}}", widgetId.ToString() },
                { "{{WIDGET_NAME}}", widgetName },
                { "{{WIDGET_NAME_KEBAB}}", widgetName.ToLowerInvariant().Replace(" ", "-") },
                { "{{WIDGET_CLASS_NAME}}", widgetName.Replace(" ", "") },
                { "{{WIDGET_DESCRIPTION}}", $"{widgetName} overlay widget" },
            };

            // Copy and process all files from stub directory
            await CopyStubFiles(stubsPath, targetPath, replacements);

            _logger.LogInformation(
                "Created {Framework} scaffold for widget {WidgetName}",
                framework,
                widgetName
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create {Framework} scaffold: {Message}",
                framework,
                ex.Message
            );
            return false;
        }
    }

    private async Task<bool> CreatePlaceholder(
        string targetPath,
        string widgetName,
        string frameworkDisplayName
    )
    {
        string readmeContent = $"# {widgetName}\n\n{frameworkDisplayName} scaffolding coming soon!";
        await File.WriteAllTextAsync(Path.Combine(targetPath, "README.md"), readmeContent);

        _logger.LogInformation(
            "Created placeholder {Framework} scaffold for widget {WidgetName}",
            frameworkDisplayName,
            widgetName
        );
        return true;
    }

    private string GetStubsPath(string framework)
    {
        // Get the directory where this assembly is located
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;

        // Navigate to the Stubs directory relative to the assembly
        return Path.Combine(assemblyDirectory, "Widgets", "Stubs", framework);
    }

    private async Task CopyStubFiles(
        string sourceDir,
        string targetDir,
        Dictionary<string, string> replacements
    )
    {
        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Get all files in the source directory
        foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            // Calculate relative path
            string relativePath = Path.GetRelativePath(sourceDir, filePath);
            string targetFilePath = Path.Combine(targetDir, relativePath);

            // Create target subdirectories if needed
            string? targetFileDir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(targetFileDir))
                Directory.CreateDirectory(targetFileDir);

            // Read source file content
            string content = await File.ReadAllTextAsync(filePath);

            // Apply template replacements
            foreach (KeyValuePair<string, string> replacement in replacements)
                content = content.Replace(replacement.Key, replacement.Value);

            // Write processed content to target file
            await File.WriteAllTextAsync(targetFilePath, content);
        }
    }

    public Task SaveConfigurationFileAsync(Widget widget)
    {
        // this method stores a widget.json file with its configuration
        string configPath = WidgetFiles.GetWidgetConfigFile(widget.Id);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            File.WriteAllText(configPath, widget.ToJson());
            _logger.LogInformation(
                "Saved configuration for widget {WidgetId} to {ConfigPath}",
                widget.Id,
                configPath
            );
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save configuration for widget {WidgetId}: {Message}",
                widget.Id,
                ex.Message
            );
            throw;
        }
    }
}
