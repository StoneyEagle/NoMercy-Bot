using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Other;

public class PermissionService : IService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(IServiceScopeFactory serviceScopeFactory, ILogger<PermissionService> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
    }

    public static bool Can(IService service, string permission)
    {
        return true;
    }

    public bool HasMinLevel(string userType, string level)
    {
        return level switch
        {
            "broadcaster" => userType is "Broadcaster",
            "lead_moderator" => userType is "LeadModerator" or "Moderator" or "Broadcaster",
            "moderator" => userType is "Moderator" or "Broadcaster",
            "vip" => userType is "Vip" or "Moderator" or "Broadcaster",
            "subscriber" => userType is "subscriber" or "Vip" or "Moderator" or "Broadcaster",
            "everyone" => true,
            _ => false
        };
    }
}