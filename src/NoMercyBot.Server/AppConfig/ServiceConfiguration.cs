using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using I18N.DotNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercyBot.Api.Dto;
using NoMercyBot.Database;
using NoMercyBot.Globals.Constraints;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Server.Swagger;
using NoMercyBot.Services;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Twitch;
using RestSharp;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercyBot.Server.AppConfig;

public static class ServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        ConfigureKestrel(services);
        ConfigureCoreServices(services);
        ConfigureLogging(services);
        ConfigureApi(services);
        ConfigureCors(services);
        ConfigureRateLimiting(services);
    }

    private static void ConfigureKestrel(IServiceCollection services)
    {
        services
            .AddDataProtection()
            .SetApplicationName("NoMercyBot")
            .PersistKeysToFileSystem(new(AppFiles.ConfigPath));
    }

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        // Add Memory Cache
        services.AddMemoryCache();

        services.AddDbContext<AppDbContext>(optionsAction =>
        {
            optionsAction.UseSqlite(
                $"Data Source={AppFiles.DatabaseFile}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            );
        });

        services.AddDbContextFactory<AppDbContext>(optionsAction =>
        {
            optionsAction.UseSqlite(
                $"Data Source={AppFiles.DatabaseFile}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            );
        });

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ILocalizer, Localizer>();

        services.AddBotServices();
        services.AddEventSubServices();

        services.AddSingleton<ServiceResolver>();
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
            logging.AddFilter("Network", LogLevel.Warning);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Trace);

            logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Warning);

            logging.AddFilter(
                "Microsoft.AspNetCore.HostFiltering.HostFilteringMiddleware",
                LogLevel.Warning
            );
            logging.AddFilter(
                "Microsoft.AspNetCore.Cors.Infrastructure.CorsMiddleware",
                LogLevel.Warning
            );
            logging.AddFilter("Microsoft.AspNetCore.Middleware", LogLevel.Warning);
        });
    }

    private static void ConfigureApi(IServiceCollection services)
    {
        // Add Controllers and JSON Options
        services
            .AddControllers(options =>
            {
                options.EnableEndpointRouting = true;
            })
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint));
        });

        services.AddMvc(option => option.EnableEndpointRouting = false);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "BearerToken";
                options.DefaultChallengeScheme = "BearerToken";
                options.DefaultSignInScheme = "BearerToken";
            })
            .AddBearerToken(options =>
            {
                // ReSharper disable once RedundantDelegateCreation
                options.Events.OnMessageReceived = new(async message =>
                {
                    StringValues accessToken = message.Request.Query["access_token"];
                    string[] result = accessToken.ToString().Split('&');

                    if (result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                        message.Request.Headers["Authorization"] = $"Bearer {result[0]}";

                    if (
                        !message.Request.Headers.TryGetValue(
                            "Authorization",
                            out StringValues authHeader
                        )
                    )
                    {
                        message.Fail("No authorization header");
                        await Task.CompletedTask;
                    }

                    accessToken = authHeader.ToString().Split("Bearer ").LastOrDefault();
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        message.Fail("No token provided");
                        await Task.CompletedTask;
                    }

                    ResilientApiClientFactory factory =
                        message.HttpContext.RequestServices.GetRequiredService<ResilientApiClientFactory>();
                    ResilientApiClient client = factory.GetClient(
                        $"{TwitchConfig.AuthUrl}/validate"
                    );
                    RestRequest request = new();
                    request.AddHeader("Authorization", $"OAuth {accessToken}");

                    RestResponse response = await client.ExecuteAsync(request);
                    if (!response.IsSuccessful)
                    {
                        message.Fail("Failed to validate token");
                        await Task.CompletedTask;
                    }

                    ValidationResponse? user = response.Content?.FromJson<ValidationResponse>();
                    if (user == null)
                    {
                        message.Fail("Invalid token");
                        await Task.CompletedTask;
                    }

                    message.HttpContext.User = new(
                        new ClaimsIdentity(
                            [
                                new(ClaimTypes.NameIdentifier, user!.UserId),
                                new(ClaimTypes.Name, user.Login),
                            ],
                            "BearerToken"
                        )
                    );

                    await Task.CompletedTask;
                });
            });

        if (
            TwitchConfig.Service().ClientId is not null
            && TwitchConfig.Service().ClientSecret is not null
        )
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "BearerToken";
                    options.DefaultChallengeScheme = "BearerToken";
                    options.DefaultSignInScheme = "BearerToken";
                })
                .AddTwitch(options =>
                {
                    options.ClientId = TwitchConfig.Service().ClientId!;
                    options.ClientSecret = TwitchConfig.Service().ClientSecret!;
                });

        services.AddAuthorization();

        services.AddControllers();

        services.AddEndpointsApiExplorer();

        services.AddHttpContextAccessor();
        services
            .AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = 1024 * 1000 * 100;

                o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            })
            .AddNewtonsoftJsonProtocol(options =>
            {
                options.PayloadSerializerSettings = JsonHelper.Settings;
            });

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        ConfigureApiVersioning(services);
        ConfigureSwagger(services);
    }

    private static void ConfigureSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerDefaultValues>();

            options.DocumentFilter<ServiceProviderDocumentFilter>();
        });

        services.AddSwaggerGenNewtonsoftSupport();

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddTransient<ServiceProviderDocumentFilter>();
    }

    private static void ConfigureApiVersioning(IServiceCollection services)
    {
        // Add API versioning
        services
            .AddApiVersioning(config =>
            {
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new(1, 0);
                config.UnsupportedApiVersionStatusCode = 418;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "VV";
                options.SubstituteApiVersionInUrl = true;
                options.DefaultApiVersion = new(1, 0);
            });
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.Identity?.IsAuthenticated == true
                        ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous"
                        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });
    }

    private static void ConfigureCors(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                "VueAppPolicy",
                builder =>
                {
                    builder
                        .WithOrigins("http://localhost:6037")
                        .WithOrigins("http://localhost:6038")
                        .WithOrigins("https://qirldtzxyrkjv4k8x9zppsiqvg6foanr.nomercy.tv")
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .WithHeaders("Access-Control-Allow-Private-Network", "true")
                        .WithHeaders("Access-Control-Allow-Headers", "*")
                        .AllowAnyHeader();
                }
            );
        });
    }
}
