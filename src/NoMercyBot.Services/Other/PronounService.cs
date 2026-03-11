using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other.Dto;
using RestSharp;

namespace NoMercyBot.Services.Other;

public class PronounService : IService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PronounService> _logger;
    private static readonly Dictionary<string, Pronoun> Pronouns = new();

    public PronounService(IServiceScopeFactory serviceScopeFactory, ILogger<PronounService> logger,
        ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _client = apiClientFactory.GetClient("https://api.pronouns.alejo.io/v1/");
    }

    public async Task LoadPronouns()
    {
        try
        {
            if (Pronouns.Any()) return;

            RestRequest request = new("pronouns");
            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                throw new("Failed to fetch pronouns");

            PronounResponse? pronounsResponse = JsonConvert.DeserializeObject<PronounResponse>(response.Content);
            if (pronounsResponse == null) return;

            foreach ((string key, Pronoun pronoun) in pronounsResponse)
            {
                Pronouns[key] = pronoun;

                await _dbContext.Pronouns.Upsert(pronoun)
                    .On(p => p.Name)
                    .WhenMatched((_, newPronoun) => new()
                    {
                        Name = newPronoun.Name,
                        Subject = newPronoun.Subject,
                        Object = newPronoun.Object,
                        Singular = newPronoun.Singular
                    })
                    .RunAsync();
            }

            _logger.LogInformation($"Loaded {Pronouns.Count} pronouns");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading pronouns: {ex.Message}");
        }
    }

    public async Task<Pronoun?> GetUserPronoun(string username)
    {
        try
        {
            RestRequest request = new($"users/{username}");
            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                return null;

            UserPronounResponse? userPronoun = JsonConvert.DeserializeObject<UserPronounResponse>(response.Content);
            if (userPronoun == null) return null;

            return await _dbContext.Pronouns.FirstOrDefaultAsync(p => p.Name == userPronoun.PronounId);
        }
        catch
        {
            return null;
        }
    }
}
