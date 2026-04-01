using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercyBot.Services.Twitch;

/// <summary>
/// Named pipe IPC server that allows the headless Claude CLI process to send
/// progress updates to Twitch chat while working on a task.
/// Pipe name: "nomercy-bot-claude-ipc"
/// Protocol: one UTF-8 line per message, newline-delimited.
/// </summary>
public class ClaudeIpcService : BackgroundService
{
    public const string PipeName = "nomercy-bot-claude-ipc";

    private readonly TwitchChatService _chatService;
    private readonly ILogger<ClaudeIpcService> _logger;

    public ClaudeIpcService(TwitchChatService chatService, ILogger<ClaudeIpcService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Claude IPC pipe server starting on: {PipeName}", PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream pipe = new(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );

                await pipe.WaitForConnectionAsync(stoppingToken);

                using StreamReader reader = new(pipe);
                while (pipe.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(stoppingToken);
                    if (line == null)
                        break;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string? threadId = ClaudeSessionBridge.ActiveThreadMessageId;
                    string channel = ClaudeSessionBridge.BroadcasterId;

                    if (!string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(channel))
                    {
                        string sanitized =
                            line.Length > 450 ? line.Substring(0, 447) + "..." : line;
                        await _chatService.SendReplyAsBot(channel, sanitized, threadId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude IPC pipe error");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
