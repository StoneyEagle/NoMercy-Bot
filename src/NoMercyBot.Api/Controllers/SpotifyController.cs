using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Spotify.Dto;
using SpotifyAPI.Web;
using CurrentlyPlaying = NoMercyBot.Services.Spotify.Dto.CurrentlyPlaying;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/spotify")]
public class SpotifyController : ControllerBase
{
    private readonly SpotifyApiService _spotifyApiService;
    private readonly ILogger _logger;

    public SpotifyController(SpotifyApiService spotifyApiService, ILogger<SpotifyController> logger)
    {
        _spotifyApiService = spotifyApiService;
        _logger = logger;
    }

    [HttpPost("set-volume")]
    public async Task<IActionResult> SetVolume([FromQuery] int volume)
    {
        try
        {
            bool result = await _spotifyApiService.SetVolume(new(volume));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set volume");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("resume")]
    public async Task<IActionResult> ResumePlayback()
    {
        try
        {
            bool result = await _spotifyApiService.ResumePlayback();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume playback");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("pause")]
    public async Task<IActionResult> Pause()
    {
        try
        {
            bool result = await _spotifyApiService.Pause();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause playback");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("previous")]
    public async Task<IActionResult> PreviousTrack()
    {
        try
        {
            bool result = await _spotifyApiService.PreviousTrack();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to skip to previous track");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("next")]
    public async Task<IActionResult> NextTrack()
    {
        try
        {
            bool result = await _spotifyApiService.NextTrack();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to skip to next track");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("add-to-playlist/{playlistId}")]
    public async Task<IActionResult> AddToPlaylist(
        string playlistId,
        [FromBody] PlaylistAddItemsRequest request
    )
    {
        try
        {
            SnapshotResponse response = await _spotifyApiService.AddToPlaylist(playlistId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add to playlist");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("currently-playing")]
    public async Task<IActionResult> GetCurrentlyPlaying()
    {
        try
        {
            CurrentlyPlaying? result = await _spotifyApiService.GetCurrentlyPlaying();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get currently playing track");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("player-state")]
    public async Task<IActionResult> GetPlayerState()
    {
        try
        {
            SpotifyState? result = await _spotifyApiService.GetPlayerState();
            return result is not null ? Ok(result) : NotFound("No player state found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player state");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("add-to-queue")]
    public async Task<IActionResult> AddToQueue([FromBody] PlayerAddToQueueRequest request)
    {
        try
        {
            bool result = await _spotifyApiService.AddToQueue(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add to queue");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue()
    {
        try
        {
            SpotifyQueueResponse? result = await _spotifyApiService.GetQueue();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        try
        {
            object devices = await _spotifyApiService.GetDevices();
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("transfer-playback")]
    public async Task<IActionResult> TransferPlayback([FromBody] string deviceId)
    {
        try
        {
            bool result = await _spotifyApiService.TransferPlayback(deviceId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer playback");
            return BadRequest(ex.Message);
        }
    }
}
