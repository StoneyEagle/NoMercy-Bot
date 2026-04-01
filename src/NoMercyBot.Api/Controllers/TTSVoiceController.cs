using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Api.Helpers;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.TTS.Interfaces;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tts")]
[Tags("TTS")]
public class TtsVoiceController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly TtsService _ttsService;
    private readonly ITtsProviderService _providerService;

    public TtsVoiceController(
        AppDbContext dbContext,
        TtsService ttsService,
        ITtsProviderService providerService
    )
    {
        _dbContext = dbContext;
        _ttsService = ttsService;
        _providerService = providerService;
    }

    [HttpGet("voices")]
    [ProducesResponseType(typeof(List<Services.TTS.Models.TtsVoice>), 200)]
    public async Task<IActionResult> GetVoices(
        [FromQuery] string? provider = null,
        [FromQuery] string? gender = null,
        [FromQuery] string? locale = null
    )
    {
        // Build query for active voices
        IQueryable<TtsVoice> query = _dbContext.TtsVoices.AsNoTracking().Where(v => v.IsActive);

        // Apply provider filter if specified
        if (!string.IsNullOrEmpty(provider))
            query = query.Where(v => v.Provider.ToLower() == provider.ToLower());

        // Apply gender filter if specified
        if (!string.IsNullOrEmpty(gender))
            query = query.Where(v => v.Gender.ToLower() == gender.ToLower());

        // Apply locale filter if specified
        if (!string.IsNullOrEmpty(locale))
            query = query.Where(v => v.Locale.ToLower() == locale.ToLower());

        // Execute query and convert to service models
        List<TtsVoice> dbVoices = await query.ToListAsync();

        return Ok(dbVoices);
    }

    [HttpGet("providers")]
    [ProducesResponseType(typeof(List<TtsProviderDto>), 200)]
    public async Task<IActionResult> GetTtsProviders()
    {
        List<ITtsProvider> providers = await _providerService.GetAllProvidersAsync();
        List<TtsProviderDto> providerInfo = [];

        foreach (ITtsProvider provider in providers)
        {
            bool isAvailable = await provider.IsAvailableAsync();
            providerInfo.Add(
                new()
                {
                    Name = provider.Name,
                    Type = provider.Type,
                    IsEnabled = provider.IsEnabled,
                    Priority = provider.Priority,
                    IsAvailable = isAvailable,
                }
            );
        }

        return Ok(providerInfo);
    }

    [Authorize]
    [HttpGet("voice")]
    public async Task<IActionResult> GetUserVoice()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        UserTtsVoice? userVoice = await _dbContext
            .UserTtsVoices.Include(x => x.TtsVoice)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (userVoice == null)
            return NotFound();

        // Return with provider information
        return Ok(
            new
            {
                id = userVoice.TtsVoice.Id,
                name = userVoice.TtsVoice.Name,
                displayName = userVoice.TtsVoice.Name,
                locale = userVoice.TtsVoice.Region,
                gender = userVoice.TtsVoice.Gender,
                provider = "Legacy", // Legacy voices are stored in database
                isDefault = false,
            }
        );
    }

    [Authorize]
    [HttpPost("voice")]
    public async Task<IActionResult> SetUserVoice([FromBody] SetTtsVoiceDto dto)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        // Look for the voice in the database (supports both legacy and provider voices)
        TtsVoice? voice = await _dbContext.TtsVoices.FirstOrDefaultAsync(x =>
            x.SpeakerId == dto.VoiceId && x.IsActive
        );

        if (voice != null)
        {
            // Handle voice selection using the database ID
            UserTtsVoice? userVoice = await _dbContext.UserTtsVoices.FirstOrDefaultAsync(x =>
                x.UserId == userId
            );

            if (userVoice == null)
            {
                userVoice = new() { UserId = userId, TtsVoiceId = voice.Id };
                _dbContext.UserTtsVoices.Add(userVoice);
            }
            else
            {
                userVoice.TtsVoiceId = voice.Id;
            }

            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        return NotFound("Voice not found or is not active");
    }

    [HttpPost]
    [Route("speak")]
    public async Task<IActionResult> Speak([FromBody] string request)
    {
        string userId = User.UserId().ToString();

        if (string.IsNullOrWhiteSpace(request))
            return BadRequest("Request cannot be empty.");

        try
        {
            // Assuming the request is a simple text string to be spoken
            TtsUsageRecord? result = await _ttsService.SendCachedTts(
                [new() { Type = "text", Text = request }],
                userId,
                CancellationToken.None
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("refresh-voices")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> RefreshProviderVoices()
    {
        try
        {
            // Get all providers and refresh their voices
            List<ITtsProvider?> allProviders = await _providerService.GetAllProvidersAsync();
            int totalVoicesUpdated = 0;
            if (allProviders.Count == 0)
                return NotFound(new { message = "No TTS providers available" });

            foreach (ITtsProvider? provider in allProviders)
            {
                if (provider is not { IsEnabled: true })
                    continue;

                try
                {
                    List<Services.TTS.Models.TtsVoice> providerVoices =
                        await provider.GetAvailableVoicesAsync();

                    if (providerVoices.Count == 0)
                        continue;

                    // Mark all existing voices for this provider as inactive initially
                    await _dbContext
                        .TtsVoices.Where(v => v.Provider == provider.Name)
                        .ExecuteUpdateAsync(v => v.SetProperty(p => p.IsActive, false));

                    foreach (Services.TTS.Models.TtsVoice providerVoice in providerVoices)
                    {
                        string uniqueId = $"{provider.Name}:{providerVoice.Id}";

                        await _dbContext
                            .TtsVoices.Upsert(
                                new()
                                {
                                    Id = uniqueId,
                                    SpeakerId = providerVoice.Id,
                                    Name = providerVoice.Name,
                                    DisplayName = providerVoice.DisplayName,
                                    Locale = providerVoice.Locale,
                                    Gender = providerVoice.Gender,
                                    Region = providerVoice.Locale,
                                    Provider = provider.Name,
                                    IsDefault = providerVoice.IsDefault,
                                    IsActive = true,
                                    UpdatedAt = DateTime.UtcNow,
                                    Age = 0,
                                    Accent = "",
                                }
                            )
                            .On(v => v.Id)
                            .WhenMatched(
                                (existing, incoming) =>
                                    new()
                                    {
                                        Id = existing.Id,
                                        SpeakerId = incoming.SpeakerId,
                                        Name = incoming.Name,
                                        DisplayName = incoming.DisplayName,
                                        Locale = incoming.Locale,
                                        Gender = incoming.Gender,
                                        Region = incoming.Region,
                                        Provider = existing.Provider,
                                        IsDefault = incoming.IsDefault,
                                        IsActive = true,
                                        UpdatedAt = DateTime.UtcNow,
                                        Age = existing.Age,
                                        Accent = existing.Accent,
                                    }
                            )
                            .RunAsync();
                    }

                    totalVoicesUpdated += providerVoices.Count;
                }
                catch (Exception ex)
                {
                    return StatusCode(
                        500,
                        $"Failed to refresh voices for provider {provider.Name}: {ex.Message}"
                    );
                }
            }

            return Ok(
                new
                {
                    message = "Provider voices refreshed successfully",
                    providersProcessed = allProviders.Count,
                    totalVoicesUpdated = totalVoicesUpdated,
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to refresh provider voices: {ex.Message}");
        }
    }
}

public class SetTtsVoiceDto
{
    public string VoiceId { get; set; } = string.Empty;
}

public class TtsProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public bool IsAvailable { get; set; }
}
