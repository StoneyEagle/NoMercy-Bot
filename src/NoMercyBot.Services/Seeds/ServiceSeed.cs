using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Seeds;

public static class ServiceSeed
{
    public static async Task Init(this AppDbContext dbContext)
    {
        if (!await dbContext.Services.AnyAsync())
        {
            List<Service> services =
            [
                new()
                {
                    Name = "Twitch",
                    Enabled = false,
                    ClientId = null,
                    ClientSecret = null,
                    Scopes = [],
                },
                new()
                {
                    Name = "Spotify",
                    Enabled = false,
                    ClientId = null,
                    ClientSecret = null,
                    Scopes = [],
                },
                new()
                {
                    Name = "Discord",
                    Enabled = false,
                    ClientId = null,
                    ClientSecret = null,
                    Scopes = [],
                },
                new()
                {
                    Name = "OBS",
                    Enabled = false,
                    ClientId = null,
                    ClientSecret = null,
                    Scopes = [],
                },
            ];

            await dbContext.Services.AddRangeAsync(services);
            await dbContext.SaveChangesAsync();
        }
    }
}
