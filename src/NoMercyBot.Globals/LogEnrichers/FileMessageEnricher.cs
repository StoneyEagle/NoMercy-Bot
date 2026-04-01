using Serilog.Core;
using Serilog.Events;

namespace NoMercyBot.Globals.LogEnrichers;

internal class FileMessageEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.RemovePropertyIfPresent("@mt");
    }
}
