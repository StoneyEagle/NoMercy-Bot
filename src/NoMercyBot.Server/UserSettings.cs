using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Extensions;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.SystemCalls;

namespace NoMercyBot.Server;

public static class UserSettings
{
    public static bool TryGetUserSettings(out Dictionary<string, (string, string)> settings)
    {
        settings = new();

        try
        {
            using AppDbContext context = new();
            List<Configuration> configuration = context
                .Configurations.Where(configuration =>
                    !string.IsNullOrWhiteSpace(configuration.SecureValue)
                    || !string.IsNullOrEmpty(configuration.Value)
                )
                .ToList();

            foreach (Configuration? config in configuration)
                settings[config.Key] = (config.Value, config.SecureValue);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void ApplySettings(
        Dictionary<string, (string value, string secureValue)> settings
    )
    {
        foreach (KeyValuePair<string, (string value, string secureValue)> setting in settings)
        {
            Logger.App($"Configuration: {setting.Key} = {setting.Value.value}");
            switch (setting.Key)
            {
                case "internalPort":
                    Config.InternalServerPort = int.Parse(setting.Value.value);
                    break;
                case "queueRunners":
                    Config.QueueWorkers = new(Config.QueueWorkers.Key, setting.Value.value.ToInt());
                    // await QueueRunner.SetWorkerCount(Config.QueueWorkers.Key, setting.Value.value.ToInt());
                    break;
                case "cronRunners":
                    Config.CronWorkers = new(Config.CronWorkers.Key, setting.Value.value.ToInt());
                    // await QueueRunner.SetWorkerCount(Config.CronWorkers.Key, setting.Value.value.ToInt());
                    break;
                case "swagger":
                    Config.Swagger = setting.Value.value.ToBoolean();
                    break;
                case "DnsServer":
                    // Config.DnsServer is readonly, cannot set
                    break;
                case "InternalClientPort":
                    Config.InternalClientPort = int.Parse(setting.Value.value);
                    break;
                case "QueueWorkers":
                    Config.QueueWorkers = new(Config.QueueWorkers.Key, setting.Value.value.ToInt());
                    break;
                case "CronWorkers":
                    Config.CronWorkers = new(Config.CronWorkers.Key, setting.Value.value.ToInt());
                    break;
                case "UseTts":
                    Config.UseTts = setting.Value.value.ToBoolean();
                    break;
                case "SaveTtsToDisk":
                    Config.SaveTtsToDisk = setting.Value.value.ToBoolean();
                    break;
                case "PlayTtsLocally":
                    Config.PlayTtsLocally = setting.Value.value.ToBoolean();
                    break;
                case "UseFrankerfacezEmotes":
                    Config.UseFrankerfacezEmotes = setting.Value.value.ToBoolean();
                    break;
                case "UseBttvEmotes":
                    Config.UseBttvEmotes = setting.Value.value.ToBoolean();
                    break;
                case "UseSevenTvEmotes":
                    Config.UseSevenTvEmotes = setting.Value.value.ToBoolean();
                    break;
                case "UseChatCodeSnippets":
                    Config.UseChatCodeSnippets = setting.Value.value.ToBoolean();
                    break;
                case "UseChatHtmlParser":
                    Config.UseChatHtmlParser = setting.Value.value.ToBoolean();
                    break;
                case "UseChatOgParser":
                    Config.UseChatOgParser = setting.Value.value.ToBoolean();
                    break;
            }
        }
    }
}
