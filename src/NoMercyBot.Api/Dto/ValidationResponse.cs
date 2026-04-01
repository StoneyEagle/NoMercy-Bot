using Newtonsoft.Json;

namespace NoMercyBot.Api.Dto;

public class ValidationResponse
{
    [JsonProperty("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonProperty("login")]
    public string Login { get; set; } = string.Empty;

    [JsonProperty("scopes")]
    public List<string> Scopes { get; set; } = [];

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}
