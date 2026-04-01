using System.Security.Cryptography;
using NoMercyBot.Globals.SystemCalls;
using Serilog.Events;

namespace NoMercyBot.Globals.Information;

public static class EventSubSecretStore
{
    private const int SecretLength = 32; // 256 bits
    private static readonly string SecretFilePath = Path.Combine(
        AppFiles.ConfigPath,
        "eventsub_secret.key"
    );
    private static string? _cachedSecret;

    public static string Secret => _cachedSecret ??= GetOrCreateSecret();

    /// <summary>
    /// Gets the existing secret or creates a new one if it doesn't exist
    /// </summary>
    /// <returns>The EventSub secret</returns>
    private static string GetOrCreateSecret()
    {
        try
        {
            // Check if the secret file exists
            if (File.Exists(SecretFilePath))
            {
                string secret = File.ReadAllText(SecretFilePath).Trim();

                // Validate the secret
                if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= SecretLength)
                    return secret;

                // If secret is invalid, delete the file and create a new one
                File.Delete(SecretFilePath);
                Logger.Setup(
                    "Invalid EventSub secret found. Creating a new one.",
                    LogEventLevel.Warning
                );
            }

            // Create a new secret
            string newSecret = GenerateSecret();

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(SecretFilePath)!);

            // Save the secret to file
            File.WriteAllText(SecretFilePath, newSecret);

            Logger.Setup("EventSub secret created successfully.");
            return newSecret;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Failed to create or load EventSub secret: {ex.Message}",
                LogEventLevel.Error
            );

            // Fallback: Generate an in-memory secret
            // This will be regenerated on application restart
            return GenerateSecret();
        }
    }

    /// <summary>
    /// Generates a cryptographically secure random string to use as the secret
    /// </summary>
    /// <returns>A secure random string</returns>
    private static string GenerateSecret()
    {
        byte[] secretBytes = RandomNumberGenerator.GetBytes(SecretLength);
        return Convert
            .ToBase64String(secretBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }
}
