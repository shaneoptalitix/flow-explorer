using System.Text;
using System.Text.Json;
using AzureDevOpsReporter.Configuration;
using AzureDevOpsReporter.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AzureDevOpsReporter.Services;

public class BitbucketService : IBitbucketService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BitbucketService> _logger;
    private readonly IMemoryCache _cache;
    private readonly BitbucketConfig _config;

    private const string COMMITS_CACHE_KEY_PREFIX = "bitbucket_commits_";
    private static readonly TimeSpan CommitsCacheDuration = TimeSpan.FromMinutes(10);

    public BitbucketService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BitbucketService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _config = configuration.GetSection("Bitbucket").Get<BitbucketConfig>()
                 ?? throw new InvalidOperationException("Bitbucket configuration is missing");

        ConfigureHttpClient();
    }

    public bool ClearCache()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Remove 100% of cache entries
            return true;
        }
        return false;
    }

    private void ConfigureHttpClient()
    {
        // Configure Basic Auth
        var authToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_config.Username}:{_config.AppPassword}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
        
        _httpClient.BaseAddress = new Uri("https://api.bitbucket.org/2.0/");
        
        _logger.LogInformation(
            "Bitbucket service configured for workspace: {Workspace}, repo: {Repo}",
            _config.Workspace, _config.Repository);
    }

    public async Task<List<BitbucketCommit>> GetCommitsAsync(
        string branchName,
        int pageLength = 30)
    {
        var cacheKey = $"{COMMITS_CACHE_KEY_PREFIX}{_config.Workspace}_{_config.Repository}_{branchName}_{pageLength}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CommitsCacheDuration;
            entry.Size = 5;

            _logger.LogInformation(
                "Fetching commits from Bitbucket for {Workspace}/{Repo}/branch/{Branch}",
                _config.Workspace, _config.Repository, branchName);

            try
            {
                var url = $"repositories/{_config.Workspace}/{_config.Repository}/commits/{branchName}?pagelen={pageLength}";

                var response = await _httpClient.GetStringAsync(url);
                var commitsResponse = JsonSerializer.Deserialize<BitbucketCommitsResponse>(response);

                if (commitsResponse?.Values == null)
                {
                    _logger.LogWarning("No commits found in Bitbucket response");
                    return new List<BitbucketCommit>();
                }

                // Map to simplified output model
                var commits = commitsResponse.Values.Select(c => new BitbucketCommit
                {
                    CommitId = c.Hash,
                    ShortCommitId = c.Hash.Length > 7 ? c.Hash.Substring(0, 7) : c.Hash,
                    Message = c.Message?.Trim() ?? string.Empty,
                    Author = c.Author?.User?.DisplayName ?? c.Author?.Raw ?? "Unknown",
                    AuthorUsername = c.Author?.User?.Username ?? string.Empty,
                    CommitDate = c.Date,
                    CommitUrl = $"https://bitbucket.org/{_config.Workspace}/{_config.Repository}/commits/{c.Hash}"
                }).ToList();

                _logger.LogInformation(
                    "Successfully retrieved {Count} commits for {Workspace}/{Repo}/branch/{Branch}",
                    commits.Count, _config.Workspace, _config.Repository, branchName);

                return commits;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, 
                    "HTTP error fetching commits for {Workspace}/{Repo}/branch/{Branch}: {StatusCode}",
                    _config.Workspace, _config.Repository, branchName, ex.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error fetching commits for {Workspace}/{Repo}/branch/{Branch}",
                    _config.Workspace, _config.Repository, branchName);
                throw;
            }
        }) ?? new List<BitbucketCommit>();
    }
}