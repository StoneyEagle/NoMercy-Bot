using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ConfigController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        var config = new
        {
            Config.DnsServer,
            Config.ProxyServer,
            Config.InternalServerPort,
            Config.InternalClientPort,
            Config.InternalTtsPort,
            QueueWorkers = Config.QueueWorkers.Value,
            CronWorkers = Config.CronWorkers.Value,
            Config.UseTts,
            Config.SaveTtsToDisk,
            Config.PlayTtsLocally,
            Config.UseFrankerfacezEmotes,
            Config.UseBttvEmotes,
            Config.UseSevenTvEmotes,
            Config.UseChatCodeSnippets,
            Config.UseChatHtmlParser,
            Config.UseChatOgParser,
        };

        return Ok(config);
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] ConfigUpdateRequest request)
    {
        await UpdateConfigValue(
            request.QueueWorkers,
            "QueueWorkers",
            v => Config.QueueWorkers = new(Config.QueueWorkers.Key, v)
        );
        await UpdateConfigValue(
            request.CronWorkers,
            "CronWorkers",
            v => Config.CronWorkers = new(Config.CronWorkers.Key, v)
        );
        await UpdateConfigValue(request.UseTts, "UseTts", v => Config.UseTts = v);
        await UpdateConfigValue(
            request.SaveTtsToDisk,
            "SaveTtsToDisk",
            v => Config.SaveTtsToDisk = v
        );
        await UpdateConfigValue(
            request.PlayTtsLocally,
            "PlayTtsLocally",
            v => Config.PlayTtsLocally = v
        );
        await UpdateConfigValue(
            request.UseFrankerfacezEmotes,
            "UseFrankerfacezEmotes",
            v => Config.UseFrankerfacezEmotes = v
        );
        await UpdateConfigValue(
            request.UseBttvEmotes,
            "UseBttvEmotes",
            v => Config.UseBttvEmotes = v
        );
        await UpdateConfigValue(
            request.UseSevenTvEmotes,
            "UseSevenTvEmotes",
            v => Config.UseSevenTvEmotes = v
        );
        await UpdateConfigValue(
            request.UseChatCodeSnippets,
            "UseChatCodeSnippets",
            v => Config.UseChatCodeSnippets = v
        );
        await UpdateConfigValue(
            request.UseChatHtmlParser,
            "UseChatHtmlParser",
            v => Config.UseChatHtmlParser = v
        );
        await UpdateConfigValue(
            request.UseChatOgParser,
            "UseChatOgParser",
            v => Config.UseChatOgParser = v
        );
        await UpdateConfigValue(
            request.InternalServerPort,
            "InternalServerPort",
            v => Config.InternalServerPort = v
        );
        await UpdateConfigValue(
            request.InternalClientPort,
            "InternalClientPort",
            v => Config.InternalClientPort = v
        );
        await UpdateConfigValue(
            request.InternalTtsPort,
            "InternalTtsPort",
            v => Config.InternalTtsPort = v
        );
        await UpdateConfigValue(request.Swagger, "Swagger", v => Config.Swagger = v);

        return NoContent();
    }

    [HttpPut("secure-config")]
    public async Task<IActionResult> UpdateSecureConfig(
        [FromBody] SecureConfigUpdateRequest request
    )
    {
        // await UpdateSecureConfigValue(request.AzureTTSKey, "_AzureTtsApiKey", v => Config.AzureTtsApiKey = v);
        // await UpdateSecureConfigValue(request.AzureTTSEndpoint, "_AzureTtsEndpoint", v => Config.AzureTtsEndpoint = v);

        return NoContent();
    }

    private async Task UpdateConfigValue<T>(T? value, string key, Action<T> updateConfig)
        where T : struct
    {
        if (value == null)
            return;

        updateConfig(value.Value);

        string? stringValue = value is bool b ? b.ToString().ToLower() : value.ToString();
        if (stringValue == null)
            return;
        await _dbContext
            .Configurations.Upsert(new() { Key = key, Value = stringValue })
            .On(c => c.Key)
            .WhenMatched((oldConfig, newConfig) => new() { Value = newConfig.Value })
            .RunAsync();
    }

    private async Task UpdateSecureConfigValue(
        string? value,
        string key,
        Action<string> updateConfig
    )
    {
        if (string.IsNullOrEmpty(value))
            return;

        updateConfig(value);

        await _dbContext
            .Configurations.Upsert(new() { Key = key, SecureValue = value })
            .On(c => c.Key)
            .WhenMatched((oldConfig, newConfig) => new() { SecureValue = newConfig.SecureValue })
            .RunAsync();
    }

    public class ConfigUpdateRequest
    {
        public int? InternalServerPort { get; set; }
        public int? InternalClientPort { get; set; }
        public int? InternalTtsPort { get; set; }
        public bool? Swagger { get; set; }
        public int? QueueWorkers { get; set; }
        public int? CronWorkers { get; set; }
        public bool? UseTts { get; set; }
        public bool? SaveTtsToDisk { get; set; }
        public bool? PlayTtsLocally { get; set; }
        public bool? UseFrankerfacezEmotes { get; set; }
        public bool? UseBttvEmotes { get; set; }
        public bool? UseSevenTvEmotes { get; set; }
        public bool? UseChatCodeSnippets { get; set; }
        public bool? UseChatHtmlParser { get; set; }
        public bool? UseChatOgParser { get; set; }
    }

    public class SecureConfigUpdateRequest
    {
        public string? AzureTTSKey { get; set; }
        public string? AzureTTSEndpoint { get; set; }
    }
}
