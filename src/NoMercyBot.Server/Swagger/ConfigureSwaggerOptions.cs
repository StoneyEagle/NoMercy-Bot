using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercyBot.Server.Swagger;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (ApiVersionDescription description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));

            options.AddSecurityDefinition(
                "twitch_auth",
                new()
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new()
                    {
                        Implicit = new()
                        {
                            AuthorizationUrl = new("https://id.twitch.tv/oauth2/authorize"),
                            TokenUrl = new("https://id.twitch.tv/oauth2/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                { "user:read:email", "Read your email address" },
                                { "user:read:follows", "Read your follows" },
                            },
                        },
                    },
                }
            );

            OpenApiSecurityScheme twitchSecurityScheme = new()
            {
                Reference = new() { Id = "twitch_auth", Type = ReferenceType.SecurityScheme },
                In = ParameterLocation.Header,
                Name = "Authorization",
                Type = SecuritySchemeType.OAuth2,
                Description = "Twitch OAuth2 Bearer Token",
                Scheme = "Bearer",
            };

            options.AddSecurityRequirement(
                new()
                {
                    { twitchSecurityScheme, [] },
                    {
                        new()
                        {
                            Reference = new()
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        []
                    },
                }
            );
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        OpenApiInfo info = new()
        {
            Title = "NoMercyBot API",
            Version = description.ApiVersion.ToString(),
            Description = "NoMercyBot API",
            Contact = new()
            {
                Name = "NoMercy",
                Email = "info@nomercy.tv",
                Url = new("https://bot.nomercy.tv"),
            },
            TermsOfService = new("https://nomercy.tv/terms-of-service"),
        };

        if (description.IsDeprecated)
            info.Description += " This API version has been deprecated.";

        return info;
    }
}
