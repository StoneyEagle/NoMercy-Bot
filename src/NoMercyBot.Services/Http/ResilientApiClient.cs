using System.Net;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace NoMercyBot.Services.Http;

public class ResilientApiClient
{
    private readonly RestClient _client;
    private readonly ILogger _logger;
    private readonly string _baseUrl;

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    private static readonly HashSet<HttpStatusCode> TransientStatusCodes =
    [
        HttpStatusCode.RequestTimeout, // 408
        HttpStatusCode.TooManyRequests, // 429
        HttpStatusCode.InternalServerError, // 500
        HttpStatusCode.BadGateway, // 502
        HttpStatusCode.ServiceUnavailable, // 503
        HttpStatusCode.GatewayTimeout, // 504
    ];

    internal ResilientApiClient(RestClient client, ILogger logger, string baseUrl)
    {
        _client = client;
        _logger = logger;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// The underlying RestClient, for cases where the caller needs raw control (no retry).
    /// </summary>
    public RestClient Client => _client;

    /// <summary>
    /// Executes a request with automatic retry for transient failures.
    /// </summary>
    public async Task<RestResponse> ExecuteAsync(
        RestRequest request,
        CancellationToken ct = default
    )
    {
        RestResponse? lastResponse = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                lastResponse = await _client.ExecuteAsync(request, ct);

                if (!IsTransientStatusCode(lastResponse.StatusCode))
                    return lastResponse;

                if (attempt < MaxRetries)
                {
                    TimeSpan delay = GetDelay(attempt, lastResponse);
                    _logger.LogWarning(
                        "Transient HTTP {StatusCode} from {BaseUrl}{Resource}, retry {Attempt}/{Max} after {Delay}ms",
                        (int)lastResponse.StatusCode,
                        _baseUrl,
                        request.Resource,
                        attempt + 1,
                        MaxRetries,
                        (int)delay.TotalMilliseconds
                    );
                    await Task.Delay(delay, ct);
                }
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < MaxRetries)
            {
                TimeSpan delay = GetDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Transient error from {BaseUrl}{Resource}, retry {Attempt}/{Max} after {Delay}ms",
                    _baseUrl,
                    request.Resource,
                    attempt + 1,
                    MaxRetries,
                    (int)delay.TotalMilliseconds
                );
                await Task.Delay(delay, ct);
            }
        }

        // All retries exhausted — return the last response (caller handles the failure as before)
        _logger.LogError(
            "All {Max} retries exhausted for {BaseUrl}{Resource}, returning last response (HTTP {StatusCode})",
            MaxRetries,
            _baseUrl,
            request.Resource,
            (int)(lastResponse?.StatusCode ?? 0)
        );

        return lastResponse!;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        TransientStatusCodes.Contains(statusCode);

    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException
        || ex is System.IO.IOException
        || (ex.InnerException is HttpRequestException or System.IO.IOException);

    private static TimeSpan GetDelay(int attempt, RestResponse? response = null)
    {
        // Respect Retry-After header for 429
        if (response?.StatusCode == HttpStatusCode.TooManyRequests)
        {
            string? retryAfter = response
                .Headers?.FirstOrDefault(h =>
                    string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase)
                )
                ?.Value?.ToString();

            if (retryAfter is not null && int.TryParse(retryAfter, out int seconds) && seconds > 0)
                return TimeSpan.FromSeconds(Math.Min(seconds, 60)); // cap at 60s
        }

        // Exponential backoff with jitter
        TimeSpan baseDelay =
            attempt < BackoffDelays.Length ? BackoffDelays[attempt] : BackoffDelays[^1];
        double jitter = Random.Shared.NextDouble() * 0.5 + 0.75; // 0.75–1.25x
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitter);
    }
}
