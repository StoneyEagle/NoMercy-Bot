using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercyBot.Database;

public static class TokenStore
{
    private static IDataProtector? Protector { get; set; }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        if (Protector == null)
        {
            IDataProtectionProvider dataProtectionProvider =
                serviceProvider.GetRequiredService<IDataProtectionProvider>();
            Protector = dataProtectionProvider.CreateProtector("NoMercyBot.TokenProtection");
        }
    }

    public static string EncryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        if (Protector == null)
            throw new InvalidOperationException(
                "TokenStore not initialized. Call Initialize() first."
            );

        return Protector.Protect(token);
    }

    public static string DecryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        if (Protector == null)
            throw new InvalidOperationException(
                "TokenStore not initialized. Call Initialize() first."
            );

        try
        {
            return Protector.Unprotect(token);
        }
        catch (Exception)
        {
            // Return the original token if it wasn't protected
            return token;
        }
    }
}
