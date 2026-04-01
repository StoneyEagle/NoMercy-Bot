using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace NoMercyBot.Services.Http;

public class ResilientApiClientFactory
{
    private readonly ConcurrentDictionary<string, ResilientApiClient> _clients = new();
    private readonly ILoggerFactory _loggerFactory;

    public ResilientApiClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Returns a cached <see cref="ResilientApiClient"/> for the given base URL.
    /// One RestClient (and its connection pool) is shared per unique base URL.
    /// </summary>
    public ResilientApiClient GetClient(string baseUrl, Action<RestClientOptions>? configure = null)
    {
        return _clients.GetOrAdd(
            baseUrl,
            url =>
            {
                RestClientOptions options = new(url) { ThrowOnAnyError = false };
                configure?.Invoke(options);

                RestClient restClient = new(options);
                ILogger logger = _loggerFactory.CreateLogger<ResilientApiClient>();
                return new ResilientApiClient(restClient, logger, url);
            }
        );
    }
}
