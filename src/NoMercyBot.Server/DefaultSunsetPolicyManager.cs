using System.Diagnostics.CodeAnalysis;
using Asp.Versioning;

namespace NoMercyBot.Server;

internal class DefaultSunsetPolicyManager : ISunsetPolicyManager
{
    public bool TryGetPolicy(
        string? name,
        ApiVersion? apiVersion,
        [MaybeNullWhen(false)] out SunsetPolicy sunsetPolicy
    )
    {
        sunsetPolicy = new();
        return true;
    }
}
