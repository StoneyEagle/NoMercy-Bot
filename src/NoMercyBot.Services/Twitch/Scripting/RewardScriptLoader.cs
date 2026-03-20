using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Twitch.Scripting;

public class RewardScriptLoader
{
    private readonly TwitchRewardService _rewardService;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger<RewardScriptLoader> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppDbContext _appDbContext;

    public RewardScriptLoader(
        TwitchRewardService rewardService,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        AppDbContext appDbContext,
        ILogger<RewardScriptLoader> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _rewardService = rewardService;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _appDbContext = appDbContext;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private readonly List<string> _loadedRewardNames = [];
    private readonly List<(TwitchReward twitchReward, IReward scriptReward, string filePath)>
        _pendingRegistrations = [];

    public async Task LoadAllAsync()
    {
        string? projectPath = AppFiles.ProjectRewardsPath;
        if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
        {
            foreach (string file in Directory.GetFiles(projectPath, "*.cs"))
            {
                await LoadScriptAsync(file);
            }
        }

        // Auto-create any missing Twitch channel points rewards (deferred until API is ready)
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for the web server to be listening
                await Task.Delay(5000);
                await EnsureTwitchRewardsExistAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Background reward auto-registration failed: {Error}",
                    ex.Message
                );
            }
        });

        _logger.LogInformation(
            "Loaded {Count} reward scripts: {Names}",
            _loadedRewardNames.Count,
            string.Join(", ", _loadedRewardNames)
        );
    }

    private async Task LoadScriptAsync(string filePath)
    {
        string scriptCode = await File.ReadAllTextAsync(filePath);
        string rewardName = Path.GetFileNameWithoutExtension(filePath);
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

            IReward reward = await CSharpScript.EvaluateAsync<IReward>(scriptCode, options);

            TwitchReward twitchReward = new()
            {
                RewardId = reward.RewardId,
                RewardTitle = reward.RewardTitle,
                Permission = reward.Permission,
                Callback = async ctx =>
                {
                    // Create a new scope for each callback execution to avoid DbContext threading issues
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    AppDbContext dbContext =
                        scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    RewardScriptContext scriptCtx = new()
                    {
                        Channel = ctx.Channel,
                        BroadcasterLogin = ctx.BroadcasterLogin,
                        BroadcasterId = ctx.BroadcasterId,
                        RewardId = ctx.RewardId,
                        RewardTitle = ctx.RewardTitle,
                        RedemptionId = ctx.RedemptionId,
                        User = ctx.User,
                        UserId = ctx.UserId,
                        UserLogin = ctx.UserLogin,
                        UserDisplayName = ctx.UserDisplayName,
                        UserInput = ctx.UserInput,
                        Cost = ctx.Cost,
                        Status = ctx.Status,
                        RedeemedAt = ctx.RedeemedAt,
                        ReplyAsync = ctx.ReplyAsync,
                        RefundAsync = ctx.RefundAsync,
                        FulfillAsync = ctx.FulfillAsync,
                        CancellationToken = ctx.CancellationToken,
                        DatabaseContext = dbContext,
                        ServiceProvider = scope.ServiceProvider,
                        TwitchChatService = _twitchChatService,
                        TwitchApiService = _twitchApiService,
                    };

                    await reward.Callback(scriptCtx);
                },
            };

            // Use the injected AppDbContext for Init (runs during startup, single-threaded)
            RewardScriptContext scriptCtx = new()
            {
                DatabaseContext = _appDbContext,
                ServiceProvider = _scopeFactory.CreateScope().ServiceProvider,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService,
            };

            await reward.Init(scriptCtx);

            _rewardService.RegisterReward(twitchReward);
            _loadedRewardNames.Add(rewardName);
            _pendingRegistrations.Add((twitchReward, reward, filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Failed to load reward script: {FilePath} - {ErrorMessage}",
                filePath,
                ex.Message
            );
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
            }
        }
    }

    private async Task EnsureTwitchRewardsExistAsync()
    {
        string baseUrl = $"http://localhost:{Config.InternalServerPort}";
        using HttpClient client = new();

        foreach (
            (
                TwitchReward twitchReward,
                IReward scriptReward,
                string filePath
            ) in _pendingRegistrations
        )
        {
            // Check if this reward already exists on Twitch by its ID
            try
            {
                HttpResponseMessage checkResponse = await client.GetAsync(
                    $"{baseUrl}/api/rewards?rewardId={scriptReward.RewardId}"
                );
                if (checkResponse.IsSuccessStatusCode)
                    continue; // Already exists, skip
            }
            catch
            {
                // Ignore check errors, try creating anyway
            }

            try
            {
                HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                    $"{baseUrl}/api/rewards",
                    new
                    {
                        title = scriptReward.RewardTitle,
                        cost = scriptReward.Cost,
                        prompt = scriptReward.Prompt,
                        isUserInputRequired = scriptReward.IsUserInputRequired,
                        backgroundColor = scriptReward.BackgroundColor,
                    }
                );

                if (createResponse.IsSuccessStatusCode)
                {
                    string createJson = await createResponse.Content.ReadAsStringAsync();
                    ChannelPointsCustomRewardsResponseData? created =
                        JsonConvert.DeserializeObject<ChannelPointsCustomRewardsResponseData>(
                            createJson
                        );

                    if (created != null)
                    {
                        _logger.LogInformation(
                            "Created Twitch reward '{Title}' with ID {Id}",
                            created.Title,
                            created.Id
                        );

                        twitchReward.RewardId = created.Id;
                        _rewardService.RegisterReward(twitchReward);
                        UpdateScriptRewardId(filePath, created.Id);
                    }
                }
                else
                {
                    // Already exists or other error — title-based matching handles it
                    string errorBody = await createResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug(
                        "Reward '{Title}' not created (may already exist): {Error}",
                        scriptReward.RewardTitle,
                        errorBody
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to create Twitch reward '{Title}': {Error}",
                    scriptReward.RewardTitle,
                    ex.Message
                );
            }
        }
    }

    private void UpdateScriptRewardId(string filePath, Guid newId)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            string updated = Regex.Replace(
                content,
                @"Guid\.Parse\(""[0-9a-fA-F\-]+""\)",
                $"Guid.Parse(\"{newId}\")"
            );

            if (updated != content)
            {
                File.WriteAllText(filePath, updated);
                _logger.LogInformation(
                    "Updated reward script {File} with Twitch GUID {Id}",
                    Path.GetFileName(filePath),
                    newId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to update script file {File}: {Error}",
                Path.GetFileName(filePath),
                ex.Message
            );
        }
    }
}
