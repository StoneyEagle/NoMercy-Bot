using Newtonsoft.Json;

namespace NoMercyBot.Database;

public class Emote
{
    [JsonProperty("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("StartIndex")]
    public int StartIndex { get; set; }

    [JsonProperty("EndIndex")]
    public int EndIndex { get; set; }

    [JsonProperty("ImageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    public Emote(string id, string name, int startIndex, int endIndex, string imageUrl)
    {
        Id = id;
        Name = name;
        StartIndex = startIndex;
        EndIndex = endIndex;
        ImageUrl = imageUrl;
    }

    public Emote() { }
}
