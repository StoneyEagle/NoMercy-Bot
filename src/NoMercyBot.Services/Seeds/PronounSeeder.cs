using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Other;

namespace NoMercyBot.Services.Seeds;

public class PronounSeeder : IHostedService
{
    private readonly IServiceScope _scope;
    private readonly ILogger<PronounSeeder> _logger;

    public PronounSeeder(IServiceScopeFactory serviceScopeFactory, ILogger<PronounSeeder> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting PronounSeeder");

        try
        {
            // Get the PronounService to load pronouns from the API
            PronounService pronounService =
                _scope.ServiceProvider.GetRequiredService<PronounService>();
            await pronounService.LoadPronouns();

            _logger.LogInformation("Successfully seeded pronouns");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding pronouns");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
