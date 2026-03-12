using System.Linq;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Twitch;

/// <summary>
/// Static bridge between the Claude command script (Roslyn) and the ChatEventHandler (compiled).
/// Persists session state to the Storage table so it survives bot restarts.
/// </summary>
public static class ClaudeSessionBridge
{
    private const string ThreadMessageIdKey = "claude_thread_message_id";
    private const string SessionIdKey = "claude_session_id";

    private static string? _activeThreadMessageId;
    private static string? _sessionId;
    private static bool _loaded;

    public static string? ActiveThreadMessageId
    {
        get
        {
            EnsureLoaded();
            return _activeThreadMessageId;
        }
        set
        {
            _activeThreadMessageId = value;
            Save(ThreadMessageIdKey, value);
        }
    }

    public static string? SessionId
    {
        get
        {
            EnsureLoaded();
            return _sessionId;
        }
        set
        {
            _sessionId = value;
            Save(SessionIdKey, value);
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            using AppDbContext db = new();
            _activeThreadMessageId = db.Storages
                .Where(s => s.Key == ThreadMessageIdKey)
                .Select(s => s.Value)
                .FirstOrDefault();
            _sessionId = db.Storages
                .Where(s => s.Key == SessionIdKey)
                .Select(s => s.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(_activeThreadMessageId))
                _activeThreadMessageId = null;
            if (string.IsNullOrEmpty(_sessionId))
                _sessionId = null;
        }
        catch
        {
            // If DB isn't ready yet, just use null
        }
    }

    private static void Save(string key, string? value)
    {
        try
        {
            using AppDbContext db = new();
            Storage? entry = db.Storages.FirstOrDefault(s => s.Key == key);

            if (value == null)
            {
                if (entry != null)
                    db.Storages.Remove(entry);
            }
            else if (entry != null)
            {
                entry.Value = value;
            }
            else
            {
                db.Storages.Add(new Storage { Key = key, Value = value });
            }

            db.SaveChanges();
        }
        catch
        {
            // Best-effort persistence
        }
    }
}
