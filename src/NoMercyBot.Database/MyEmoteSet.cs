using System.Globalization;
using Newtonsoft.Json;
using TwitchLib.Client.Models;

namespace NoMercyBot.Database;

public class MyEmoteSet : EmoteSet
{
    [JsonProperty("Emotes")]
    public List<Emote> Emotes { get; set; } = [];

    [JsonProperty("RawEmoteSetString")]
    public string RawEmoteSetString { get; set; } = string.Empty;

    public MyEmoteSet(string rawEmoteSetString, string message)
        : base(rawEmoteSetString, message)
    {
        RawEmoteSetString = rawEmoteSetString;
    }

    public MyEmoteSet(IEnumerable<Emote> emotes, string emoteSetData)
        : base(emoteSetData, "") // Use the string constructor instead
    {
        Emotes = emotes.ToList();
        RawEmoteSetString = emoteSetData;
    }

    public MyEmoteSet()
        : base("", "") { }

    // Add conversion from TwitchLib EmoteSet
    public static MyEmoteSet FromTwitchEmoteSet(EmoteSet emoteSet, string message)
    {
        StringInfo stringInfo = new(message);
        List<Emote> newEmotes = [];

        IOrderedEnumerable<TwitchLib.Client.Models.Emote> emotes = emoteSet.Emotes.OrderBy(x =>
            x.StartIndex
        );

        foreach (TwitchLib.Client.Models.Emote? emote in emotes)
            try
            {
                // Calculate actual string positions based on UTF-16 code units
                int startPos = stringInfo.SubstringByTextElements(0, emote.StartIndex).Length;
                int endPos = stringInfo.SubstringByTextElements(0, emote.EndIndex).Length + 1;

                // Extract the emote name using the UTF-16 positions
                string name = message[startPos..endPos];

                newEmotes.Add(
                    new()
                    {
                        Id = emote.Id,
                        Name = name,
                        StartIndex = emote.StartIndex,
                        EndIndex = emote.EndIndex,
                        ImageUrl =
                            $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/2.0",
                    }
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        return new(newEmotes, emoteSet.RawEmoteSetString);
    }
}
