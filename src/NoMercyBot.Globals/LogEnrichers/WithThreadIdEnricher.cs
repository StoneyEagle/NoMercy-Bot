using Serilog.Core;
using Serilog.Events;

namespace NoMercyBot.Globals.LogEnrichers;

internal class WithThreadIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId)
        );
    }
}
