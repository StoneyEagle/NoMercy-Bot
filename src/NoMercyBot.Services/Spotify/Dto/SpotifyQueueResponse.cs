using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyQueueResponse
{
    [JsonProperty("currently_playing")]
    public Item CurrentlyPlaying { get; set; }

    [JsonProperty("queue")]
    public List<Item> Queue { get; set; }
}
