namespace NoMercyBot.Services.Twitch;

/// <summary>
/// Static bridge between the Claude command script (Roslyn) and the ChatEventHandler (compiled).
/// Holds the active thread message ID so the chat handler can route broadcaster replies
/// back to the !claude command without requiring the !claude prefix.
/// </summary>
public static class ClaudeSessionBridge
{
    /// <summary>
    /// The message ID of the original !claude command that started the current thread.
    /// Set by the Claude command script, checked by ChatEventHandler.
    /// Null means no active session.
    /// </summary>
    public static volatile string? ActiveThreadMessageId;

    /// <summary>
    /// The Claude CLI session ID from the first invocation.
    /// Used with --resume to pin follow-ups to the exact conversation,
    /// regardless of other Claude sessions running in the same project.
    /// </summary>
    public static volatile string? SessionId;
}
