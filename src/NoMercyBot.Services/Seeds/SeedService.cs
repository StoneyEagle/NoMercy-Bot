using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.TTS.Interfaces;
using Serilog.Events;

namespace NoMercyBot.Services.Seeds;

public class SeedService : IHostedService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SeedService> _logger;

    public SeedService(IServiceScopeFactory serviceScopeFactory, ILogger<SeedService> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database seeding process");

        try
        {
            await Migrate(_dbContext);
            await EnsureDatabaseCreated(_dbContext);

            // Seed services first (they're required by other seeds)
            await ServiceSeed.Init(_dbContext);

            // Seed event subscriptions
            await EventSubscriptionSeed.Init(_dbContext);

            // Seed default rewards
            await RewardSeed.Init(_dbContext, _scope);

            // Get the PronounService to load pronouns from the API
            PronounService pronounService =
                _scope.ServiceProvider.GetRequiredService<PronounService>();
            await pronounService.LoadPronouns();

            // Get TTS providers for voice seeding
            IEnumerable<ITtsProvider> ttsProviders =
                _scope.ServiceProvider.GetServices<ITtsProvider>();
            await TtsVoiceSeed.Init(_dbContext, ttsProviders);

            // await DevSeed.Init(_dbContext, _scope);

            _logger.LogInformation("Successfully completed database seeding");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }

    private static Task Migrate(DbContext context)
    {
        try
        {
            // Configure SQLite for better performance and UTF-8 support
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
            context.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");

            // First check if the database exists - if not, create it
            bool dbExists = context.Database.CanConnect();

            if (!dbExists)
            {
                Logger.Setup(
                    "Database doesn't exist. Creating database and applying migrations...",
                    LogEventLevel.Verbose
                );
                context.Database.Migrate();
            }
            else
            {
                // Check if migration history table exists and has the correct records
                bool migrationTableExists;
                try
                {
                    migrationTableExists =
                        context.Database.ExecuteSqlRaw("SELECT COUNT(*) FROM __EFMigrationsHistory")
                        >= 0;
                }
                catch
                {
                    migrationTableExists = false;
                }

                // Get list of applied migrations in the database
                List<string> appliedMigrations = [];
                if (migrationTableExists)
                    appliedMigrations = context.Database.GetAppliedMigrations().ToList();

                // Get list of available migrations in code
                List<string> availableMigrations = context.Database.GetMigrations().ToList();

                if (migrationTableExists && appliedMigrations.Count == availableMigrations.Count)
                {
                    Logger.Setup(
                        "Database is up to date. No migrations needed.",
                        LogEventLevel.Verbose
                    );
                }
                else
                {
                    Logger.Setup("Checking for pending migrations...", LogEventLevel.Verbose);
                    bool hasPendingMigrations = context.Database.GetPendingMigrations().Any();

                    if (hasPendingMigrations)
                        try
                        {
                            context.Database.Migrate();
                            Logger.Setup("Migrations applied successfully.", LogEventLevel.Verbose);
                        }
                        catch (Exception ex) when (ex.Message.Contains("already exists"))
                        {
                            Logger.Setup(
                                "Tables already exist. Ensuring migration history is up to date...",
                                LogEventLevel.Verbose
                            );

                            try
                            {
                                if (migrationTableExists)
                                {
                                    // Don't delete - just ensure all migrations are recorded
                                    List<string> pendingMigrations = context
                                        .Database.GetPendingMigrations()
                                        .ToList();
                                    string version =
                                        context.GetType().Assembly.GetName().Version?.ToString()
                                        ?? "1.0.0";

                                    int added = 0;
                                    int failed = 0;
                                    foreach (string migration in pendingMigrations)
                                        try
                                        {
                                            context.Database.ExecuteSqlRaw(
                                                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                                                migration,
                                                version
                                            );
                                            added++;
                                        }
                                        catch
                                        {
                                            failed++;
                                        }

                                    if (added > 0 || failed > 0)
                                        Logger.Setup(
                                            $"Migration history: {added} added, {failed} failed out of {pendingMigrations.Count} pending",
                                            failed > 0
                                                ? LogEventLevel.Warning
                                                : LogEventLevel.Verbose
                                        );
                                }
                                else
                                {
                                    // Create the migrations history table
                                    context.Database.ExecuteSqlRaw(
                                        @"
                                        CREATE TABLE __EFMigrationsHistory (
                                            MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                                            ProductVersion TEXT NOT NULL
                                        );"
                                    );

                                    // Add all migrations to history
                                    string version =
                                        context.GetType().Assembly.GetName().Version?.ToString()
                                        ?? "1.0.0";
                                    foreach (string migration in availableMigrations)
                                        context.Database.ExecuteSqlRaw(
                                            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                                            migration,
                                            version
                                        );
                                    Logger.Setup(
                                        $"Migration history table created and populated with {availableMigrations.Count} migrations.",
                                        LogEventLevel.Verbose
                                    );
                                }
                            }
                            catch (Exception innerEx)
                            {
                                Logger.Setup(
                                    $"Failed to update migration history: {innerEx.Message}",
                                    LogEventLevel.Fatal
                                );
                            }
                        }
                    else
                        Logger.Setup("No pending migrations found.", LogEventLevel.Verbose);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying migrations: {ex.Message}", LogEventLevel.Fatal);
        }

        return Task.CompletedTask;
    }
}
