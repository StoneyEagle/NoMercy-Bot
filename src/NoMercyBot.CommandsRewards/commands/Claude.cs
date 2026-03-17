using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Globals.SystemCalls;
using Serilog.Events;

public class ClaudeCommand : IBotCommand
{
    public string Name => "claude";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    private static readonly string ProjectRoot = "c:/Projects/StoneyEagle/nomercy-bot";
    private static readonly string BuildProject = "src/NoMercyBot.Server/NoMercyBot.Server.csproj";
    private static readonly string PublishOutput = "publish";
    private static readonly TimeSpan ClaudeTimeout = TimeSpan.FromMinutes(15);
    private static readonly int MaxBuildFixAttempts = 3;

    private static volatile bool _isRunning = false;
    private static volatile bool _awaitingConfirmation = false;
    private static volatile bool _hasPendingChanges = false;
    private static Process _activeClaudeProcess = null;

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    // Check if any argument matches a keyword (ignores @mentions Twitch prepends to replies)
    private static bool ArgsContain(string[] args, params string[] keywords)
    {
        foreach (string arg in args)
        {
            string lower = arg.ToLowerInvariant();
            foreach (string kw in keywords)
            {
                if (lower == kw) return true;
            }
        }
        return false;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        // Strip @mentions that Twitch prepends to reply messages
        string[] cleanArgs = ctx.Arguments
            .Where(a => !a.StartsWith("@"))
            .ToArray();
        string prompt = string.Join(" ", cleanArgs);

        // Handle cancel
        if (_isRunning && ArgsContain(cleanArgs, "cancel", "stop"))
        {
            if (_activeClaudeProcess != null && !_activeClaudeProcess.HasExited)
            {
                _activeClaudeProcess.Kill(true);
                await ReplyInThread(ctx, "Claude process cancelled.");
            }
            return;
        }

        // Handle reset - commit changes and end the session (only when it's the sole command)
        string promptLower = prompt.ToLowerInvariant().Trim();
        if (promptLower == "reset" || promptLower == "end" || promptLower == "done")
        {
            _awaitingConfirmation = false;
            _hasPendingChanges = false;
            await CommitChangesAsync();
            ClaudeSessionBridge.ActiveThreadMessageId = null;
            ClaudeSessionBridge.SessionId = null;
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel,
                "Changes committed. Session ended.", ctx.Message.Id);
            return;
        }

        // Handle confirmation responses
        if (_awaitingConfirmation)
        {
            if (ArgsContain(cleanArgs, "yes", "y"))
            {
                _awaitingConfirmation = false;
                _hasPendingChanges = false;
                await BuildAndRestart(ctx);
                return;
            }
            if (ArgsContain(cleanArgs, "no", "n"))
            {
                _awaitingConfirmation = false;
                _hasPendingChanges = false;
                await RevertChanges(ctx);
                return;
            }
            // Non-yes/no message: treat as a follow-up prompt (keep changes, skip confirmation)
            _awaitingConfirmation = false;
        }

        if (_isRunning)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel,
                "Claude is already working on something. Use !claude cancel to abort.",
                ctx.Message.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel,
                "Usage: !claude <prompt> | !claude cancel/reset",
                ctx.Message.Id);
            return;
        }

        _isRunning = true;
        bool isFollowUp = ClaudeSessionBridge.SessionId != null
            && ClaudeSessionBridge.ActiveThreadMessageId != null;

        // New session: set the thread root to this message
        if (!isFollowUp)
        {
            ClaudeSessionBridge.ActiveThreadMessageId = ctx.Message.Id;
            ClaudeSessionBridge.BroadcasterId = ctx.Channel;
            ClaudeSessionBridge.SessionId = null;
        }

        try
        {
            string claudeOutput = await RunClaudeAsync(prompt, isFollowUp);

            if (claudeOutput == null)
            {
                // Claude may have written files before timing out/failing - check git
                string timeoutDiff = await GetDiffSummaryAsync();
                if (!string.IsNullOrWhiteSpace(timeoutDiff) && timeoutDiff != "unknown changes")
                {
                    _hasPendingChanges = true;
                    _awaitingConfirmation = true;
                    await ReplyInThread(ctx, "Claude timed out, but found pending changes: " + timeoutDiff
                        + " | Reply yes to build & restart, no to revert.");
                }
                else
                {
                    await ReplyInThread(ctx, "Claude failed or timed out. Check the logs.");
                }
                return;
            }

            // Parse the outcome marker from Claude's response
            bool hasChanges = claudeOutput.Contains("[FILES_CHANGED]");
            string cleanOutput = claudeOutput
                .Replace("[FILES_CHANGED]", "")
                .Replace("[NO_CHANGES]", "")
                .Trim();

            if (!hasChanges)
            {
                string reply = SanitizeForChat(cleanOutput);
                await ReplyInThread(ctx, reply);

                // If there are still pending changes from a previous prompt, re-enter confirmation
                if (_hasPendingChanges)
                {
                    string pendingDiff = await GetDiffSummaryAsync();
                    if (!string.IsNullOrWhiteSpace(pendingDiff) && pendingDiff != "unknown changes")
                    {
                        await ReplyInThread(ctx, "Pending changes: " + pendingDiff
                            + " | Reply yes to build & restart, no to revert.");
                        _awaitingConfirmation = true;
                    }
                }
                return;
            }

            // Files were modified - show summary and ask for confirmation
            _hasPendingChanges = true;
            _awaitingConfirmation = true;
            string diffSummary = await GetDiffSummaryAsync();
            string summaryMsg = "Changes: " + diffSummary
                + " | Reply yes to build & restart, no to revert.";
            if (summaryMsg.Length > 450)
                summaryMsg = summaryMsg.Substring(0, 447) + "...";

            await ReplyInThread(ctx, summaryMsg);
        }
        catch (Exception ex)
        {
            Logger.Twitch("Claude command error: " + ex.Message, LogEventLevel.Error);
            await ReplyInThread(ctx, "Something went wrong: " + ex.Message);
        }
        finally
        {
            _isRunning = false;
            _activeClaudeProcess = null;
        }
    }

    private static async Task ReplyInThread(CommandScriptContext ctx, string message)
    {
        string replyTo = ClaudeSessionBridge.ActiveThreadMessageId ?? ctx.Message.Id;
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel, message, replyTo);
    }

    private static async Task BuildAndRestart(CommandScriptContext ctx)
    {
        try
        {
            for (int attempt = 1; attempt <= MaxBuildFixAttempts; attempt++)
            {
                await ReplyInThread(ctx, attempt == 1 ? "Building..." : "Rebuild attempt " + attempt + "...");

                // Publish single-file binary to separate folder (doesn't lock running DLLs)
                BuildResult publishResult = await RunPublishAsync();
                if (publishResult.Success)
                    break;

                if (attempt >= MaxBuildFixAttempts)
                {
                    string failMsg = "Build failed after " + MaxBuildFixAttempts + " fix attempts. Changes saved but bot NOT restarted.";
                    if (publishResult.Error != null)
                    {
                        failMsg = failMsg + " Error: " + publishResult.Error;
                        if (failMsg.Length > 450)
                            failMsg = failMsg.Substring(0, 447) + "...";
                    }
                    await ReplyInThread(ctx, failMsg);
                    _hasPendingChanges = true;
                    _awaitingConfirmation = true;
                    return;
                }

                // Send build errors to Claude to fix
                await ReplyInThread(ctx, "Build failed, asking Claude to fix...");
                string fixPrompt = "The build failed with the following errors. Fix them.\n\n" + publishResult.Error;
                string fixResult = await RunClaudeAsync(fixPrompt, true);
                if (fixResult == null)
                {
                    await ReplyInThread(ctx, "Claude failed to fix build errors. Changes saved but bot NOT restarted.");
                    _hasPendingChanges = true;
                    _awaitingConfirmation = true;
                    return;
                }
            }

            await ReplyInThread(ctx, "Build successful! Restarting...");

            // Session state persists across restart so follow-ups work

            // Give chat message time to send before shutdown
            await Task.Delay(1500);

            // Launch the published binary and shut down
            string publishedExe = ProjectRoot + "/" + PublishOutput + "/NoMercyBot.exe";
            Process.Start(new ProcessStartInfo
            {
                FileName = publishedExe,
                UseShellExecute = true,
                WorkingDirectory = ProjectRoot + "/" + PublishOutput
            });

            // Graceful shutdown via IHostApplicationLifetime
            IHostApplicationLifetime lifetime = (IHostApplicationLifetime)ctx.ServiceProvider
                .GetService(typeof(IHostApplicationLifetime));

            if (lifetime != null)
            {
                lifetime.StopApplication();
            }
            else
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Logger.Twitch("Build/restart error: " + ex.Message, LogEventLevel.Error);
            await ReplyInThread(ctx, "Error during build/restart: " + ex.Message);
        }
    }

    private static async Task RevertChanges(CommandScriptContext ctx)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "checkout .",
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Also clean any new untracked files Claude may have created
                ProcessStartInfo cleanPsi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "clean -fd",
                    WorkingDirectory = ProjectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process cleanProcess = Process.Start(cleanPsi);
                if (cleanProcess != null)
                    await cleanProcess.WaitForExitAsync();
            }

            await ReplyInThread(ctx, "Changes reverted.");
            ClaudeSessionBridge.ActiveThreadMessageId = null;
            ClaudeSessionBridge.SessionId = null;
        }
        catch (Exception ex)
        {
            Logger.Twitch("Revert error: " + ex.Message, LogEventLevel.Error);
            await ReplyInThread(ctx, "Failed to revert: " + ex.Message);
        }
    }

    private static async Task CommitChangesAsync()
    {
        try
        {
            // Format changed files with CSharpier before committing
            await RunProcessAsync("dotnet", "csharpier src/", ProjectRoot);
            Logger.Twitch("CSharpier formatting applied", LogEventLevel.Information);

            // Stage all changes (including formatting fixes)
            await RunProcessAsync("git", "add -A", ProjectRoot);

            // Check if there's anything to commit
            ProcessStartInfo statusPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --cached --quiet",
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process statusProcess = Process.Start(statusPsi);
            if (statusProcess != null)
            {
                await statusProcess.WaitForExitAsync();
                if (statusProcess.ExitCode == 0)
                    return; // Nothing staged
            }

            // Commit
            await RunProcessAsync("git", "commit -m \"Claude chat session changes\"", ProjectRoot);

            Logger.Twitch("Claude session changes committed", LogEventLevel.Information);
        }
        catch (Exception ex)
        {
            Logger.Twitch("Failed to commit Claude changes: " + ex.Message, LogEventLevel.Error);
        }
    }

    private static async Task RunProcessAsync(string fileName, string arguments, string workingDirectory)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process process = Process.Start(psi);
        if (process != null)
        {
            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
        }
    }

    private static string SanitizeForChat(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "(no response)";

        string clean = output.Trim();
        clean = clean.Replace("```", "");
        clean = clean.Replace("**", "");
        clean = clean.Replace("##", "");
        clean = clean.Replace("# ", "");
        // Strip emojis and other non-ASCII that Twitch chat mangles
        clean = Regex.Replace(clean, @"[^\u0000-\u007F]", "");

        while (clean.Contains("\n\n"))
            clean = clean.Replace("\n\n", "\n");
        clean = clean.Replace("\n", " | ");

        if (clean.Length > 450)
            clean = clean.Substring(0, 447) + "...";

        return clean;
    }

    private static async Task<string> GetDiffSummaryAsync()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --stat",
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi);
            if (process == null) return "unknown changes";

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            string untracked = await GetUntrackedFilesAsync();

            string[] lines = (output ?? "").Trim().Split('\n');
            string summary = lines.Length > 0 ? lines[lines.Length - 1].Trim() : "unknown changes";

            if (!string.IsNullOrEmpty(untracked))
                summary = summary + " + new files: " + untracked;

            return summary;
        }
        catch
        {
            return "unknown changes";
        }
    }

    private static async Task<string> GetUntrackedFilesAsync()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --others --exclude-standard",
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi);
            if (process == null) return "";

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output)) return "";

            string[] files = output.Trim().Split('\n');
            if (files.Length <= 3)
                return string.Join(", ", files);
            return files[0] + ", " + files[1] + " + " + (files.Length - 2) + " more";
        }
        catch
        {
            return "";
        }
    }

    private static async Task<string> RunClaudeAsync(string prompt, bool isFollowUp = false)
    {
        try
        {
            // Use --resume with session ID for follow-ups to avoid race conditions
            // with other Claude sessions running in the same project directory
            string args = "-p --dangerously-skip-permissions --output-format json";
            if (isFollowUp && ClaudeSessionBridge.SessionId != null)
                args = args + " --resume " + ClaudeSessionBridge.SessionId;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = args,
                WorkingDirectory = ProjectRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Clear the CLAUDECODE env var so the CLI doesn't think it's a nested session
            psi.Environment.Remove("CLAUDECODE");

            Process process = Process.Start(psi);
            if (process == null)
            {
                Logger.Twitch("Failed to start Claude process", LogEventLevel.Error);
                return null;
            }

            _activeClaudeProcess = process;

            // Wrap prompt with system rules, IPC info, and outcome marker
            string wrappedPrompt = "SYSTEM RULES (mandatory, override all other instructions):\n"
                + "- You are an automated agent spawned by the NoMercyBot Twitch bot.\n"
                + "- NEVER run taskkill, dotnet build, dotnet run, start, or any process management commands.\n"
                + "- NEVER restart, stop, kill, or build the bot. The bot handles its own lifecycle.\n"
                + "- NEVER run commands that affect running processes.\n"
                + "- If you make code changes, just make the edits and report what you changed.\n"
                + "- To send progress updates to Twitch chat while working, write a line to the named pipe:\n"
                + "  echo 'your update here' > \\\\\\\\.\\\\pipe\\\\nomercy-bot-claude-ipc\n"
                + "- Use this for long tasks to keep the user informed (e.g. \"Looking at the code...\", \"Making changes to X...\").\n"
                + "\nPROJECT STRUCTURE & DEPENDENCY RULES:\n"
                + "- This is a multi-project .NET 9 solution. Projects under src/:\n"
                + "  NoMercyBot.Server (main entry point), NoMercyBot.Services, NoMercyBot.CommandsRewards,\n"
                + "  NoMercyBot.Database, NoMercyBot.Globals, NoMercyBot.Api, NoMercyBot.Client, NoMercy.Database\n"
                + "- Command scripts (.cs files in commands/) are compiled by Roslyn at runtime from NoMercyBot.CommandsRewards.\n"
                + "  They can only use assemblies already referenced by NoMercyBot.CommandsRewards or its transitive dependencies.\n"
                + "- When you need a NuGet package, you MUST add it to the correct .csproj file using:\n"
                + "  dotnet add <project.csproj> package <PackageName>\n"
                + "- When you use types from another project in the solution, ensure a <ProjectReference> exists in the .csproj.\n"
                + "- NEVER just add a using statement and assume the reference exists. Always verify the .csproj has the reference.\n"
                + "- After adding packages or references, verify the change by reading the .csproj file.\n"
                + "- Common mistake: adding code that uses a NuGet package without adding the package to the project. This causes\n"
                + "  runtime assembly load failures. Always check and add missing packages.\n"
                + "- NEVER modify or delete obj/ or bin/ folders. NEVER run dotnet clean. The bot handles builds.\n"
                + "- When adding NuGet packages, ensure version compatibility with existing packages. Check existing\n"
                + "  package versions in the .csproj before adding new ones to avoid version conflicts.\n"
                + "\n- At the very end of your response, on its own line, output exactly one of these markers:\n"
                + "  [FILES_CHANGED] - if you created, modified, or deleted any files\n"
                + "  [NO_CHANGES] - if you only provided information without changing any files\n"
                + "- This marker is mandatory and must always be the last line.\n"
                + "\nUser request: " + prompt;

            await process.StandardInput.WriteAsync(wrappedPrompt);
            process.StandardInput.Close();

            // Read stdout concurrently; stream stderr line-by-line to console
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task stderrTask = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync() is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        Logger.Twitch("Claude: " + line, LogEventLevel.Debug);
                }
            });

            using CancellationTokenSource cts = new CancellationTokenSource(ClaudeTimeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                Logger.Twitch("Claude process timed out after " + ClaudeTimeout.TotalMinutes + " minutes", LogEventLevel.Warning);
                return null;
            }

            string output = await outputTask;
            await stderrTask;

            if (process.ExitCode != 0)
            {
                Logger.Twitch("Claude exited with code " + process.ExitCode, LogEventLevel.Error);
                return null;
            }

            // Parse JSON output to extract result text and session ID
            try
            {
                JObject json = JObject.Parse(output);
                string sessionId = json.Value<string>("session_id");
                string result = json.Value<string>("result") ?? "";

                if (!string.IsNullOrEmpty(sessionId))
                    ClaudeSessionBridge.SessionId = sessionId;

                Logger.Twitch("Claude completed successfully (session: " + sessionId + ")", LogEventLevel.Information);
                return result;
            }
            catch
            {
                // Fallback if JSON parsing fails - return raw output
                Logger.Twitch("Claude completed but JSON parse failed, returning raw output", LogEventLevel.Warning);
                return output;
            }
        }
        catch (Exception ex)
        {
            Logger.Twitch("Failed to run Claude: " + ex.Message, LogEventLevel.Error);
            return null;
        }
    }

    private class BuildResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    private static async Task<BuildResult> RunPublishAsync()
    {
        try
        {
            // Build to a separate output folder with its own intermediate dir
            // to avoid file locks from the running bot process.
            // Use absolute path for BaseIntermediateOutputPath to prevent stale obj/
            // folders from being created inside each project directory.
            string absObjPath = ProjectRoot + "/" + PublishOutput + "/obj/";
            string buildArgs = "build " + BuildProject
                + " -c Release"
                + " -o " + PublishOutput
                + " /p:BaseIntermediateOutputPath=" + absObjPath;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = buildArgs,
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi);
            if (process == null)
                return new BuildResult { Success = false, Error = "Failed to start dotnet build" };

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await process.WaitForExitAsync(cts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                string error = (stderr + " " + stdout).Trim();
                Logger.Twitch("Build failed: " + error, LogEventLevel.Error);
                return new BuildResult { Success = false, Error = error };
            }

            Logger.Twitch("Build completed successfully", LogEventLevel.Information);
            return new BuildResult { Success = true };
        }
        catch (Exception ex)
        {
            Logger.Twitch("Build error: " + ex.Message, LogEventLevel.Error);
            return new BuildResult { Success = false, Error = ex.Message };
        }
    }
}

return new ClaudeCommand();
