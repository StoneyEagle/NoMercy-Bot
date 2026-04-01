using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using NoMercyBot.Services.Interfaces;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercyBot.Server.Swagger;

public class ServiceProviderDocumentFilter : IDocumentFilter
{
    private readonly IEnumerable<IAuthService> _authServices;

    public ServiceProviderDocumentFilter(IEnumerable<IAuthService> authServices)
    {
        _authServices = authServices;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Get all available service providers (Twitch, Spotify, etc.)
        List<string> serviceProviders = _authServices
            .Select(s => s.GetType().Name.Replace("AuthService", "").ToLower())
            .ToList();

        // Define TTS providers
        List<string> ttsProviders = ["Azure", "Legacy"];

        // For each operation that has a {provider} parameter
        foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
        foreach (KeyValuePair<OperationType, OpenApiOperation> operation in path.Value.Operations)
        {
            OpenApiParameter? providerParameter = operation.Value.Parameters.FirstOrDefault(p =>
                p.Name == "provider"
            );

            if (providerParameter != null)
            {
                // Determine which type of providers to use based on the endpoint path
                if (path.Key.StartsWith("/api/tts"))
                {
                    // TTS endpoints - use TTS providers
                    providerParameter.Schema.Example = new OpenApiString("azure");
                    providerParameter.Description =
                        "TTS Provider to filter voices by. Available TTS providers: "
                        + string.Join(", ", ttsProviders);

                    // Add enum values for TTS providers
                    providerParameter.Schema.Enum = ttsProviders
                        .Select(p => new OpenApiString(p))
                        .ToList<IOpenApiAny>();
                }
                else
                {
                    // Service endpoints - use service providers
                    providerParameter.Schema.Example = new OpenApiString("twitch");
                    providerParameter.Description =
                        "Service Provider. Available service providers: "
                        + string.Join(", ", serviceProviders);

                    // Add enum values for service providers
                    providerParameter.Schema.Enum = serviceProviders
                        .Select(p => new OpenApiString(p))
                        .ToList<IOpenApiAny>();
                }
            }
        }
    }
}
