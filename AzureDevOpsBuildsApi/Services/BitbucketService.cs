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
    private const string COMPARISON_CACHE_KEY_PREFIX = "bitbucket_comparison_";
    private static readonly TimeSpan CommitsCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ComparisonCacheDuration = TimeSpan.FromMinutes(15);

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

    public async Task<PagedBitbucketCommitsResponse> GetCommitsAsync(
        string branchName,
        int pageLength = 30,
        int maxPages = 10)
    {
        var cacheKey = $"{COMMITS_CACHE_KEY_PREFIX}{_config.Workspace}_{_config.Repository}_{branchName}_{pageLength}_{maxPages}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CommitsCacheDuration;
            entry.Size = 10;

            _logger.LogInformation(
                "Fetching commits from Bitbucket for {Workspace}/{Repo}/branch/{Branch} (maxPages: {MaxPages}, pageLength: {PageLength})",
                _config.Workspace, _config.Repository, branchName, maxPages, pageLength);

            // ====== ENHANCED DIAGNOSTIC LOGGING ======
            _logger.LogInformation(
                "[DIAGNOSTIC] Raw branch name received: '{BranchName}' (Length: {Length}, Has refs/heads/: {HasRefsHeads})",
                branchName,
                branchName?.Length ?? 0,
                branchName?.StartsWith("refs/heads/") ?? false);

            var allCommits = new List<BitbucketCommit>();
            var pagesFetched = 0;

            // Clean the branch name properly for Bitbucket API
            var cleanBranchName = branchName.Replace("refs/heads/", "");
            _logger.LogInformation(
                "[DIAGNOSTIC] Cleaned branch name: '{CleanBranch}' (Original: '{OriginalBranch}')",
                cleanBranchName, branchName);

            // URL encode the branch name - Use Uri.EscapeDataString for proper encoding
            var encodedBranch = Uri.EscapeDataString(cleanBranchName);
            _logger.LogInformation(
                "[DIAGNOSTIC] URL-encoded branch name: '{EncodedBranch}' (Clean: '{CleanBranch}')",
                encodedBranch, cleanBranchName);

            string? nextUrl = $"repositories/{_config.Workspace}/{_config.Repository}/commits/{encodedBranch}?pagelen={pageLength}";

            _logger.LogInformation(
                "[DIAGNOSTIC] Full API endpoint: {BaseUrl}{RelativeUrl}",
                _httpClient.BaseAddress, nextUrl);

            try
            {
                while (!string.IsNullOrEmpty(nextUrl) && pagesFetched < maxPages)
                {
                    _logger.LogInformation(
                        "[DIAGNOSTIC] Attempting to fetch page {Page} for branch '{Branch}' using URL: {Url}",
                        pagesFetched + 1, cleanBranchName, nextUrl);

                    HttpResponseMessage httpResponse;
                    try
                    {
                        // Make the request and capture the full response
                        httpResponse = await _httpClient.GetAsync(nextUrl);

                        _logger.LogInformation(
                            "[DIAGNOSTIC] HTTP Response received - Status: {StatusCode} ({StatusCodeNumber}), Reason: {ReasonPhrase}",
                            httpResponse.StatusCode,
                            (int)httpResponse.StatusCode,
                            httpResponse.ReasonPhrase ?? "N/A");

                        // If not successful, log the error details
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await httpResponse.Content.ReadAsStringAsync();
                            _logger.LogError(
                                "[DIAGNOSTIC] HTTP Error Details:\n" +
                                "  Status Code: {StatusCode} ({StatusCodeNumber})\n" +
                                "  URL: {Url}\n" +
                                "  Branch (original): '{OriginalBranch}'\n" +
                                "  Branch (cleaned): '{CleanBranch}'\n" +
                                "  Branch (encoded): '{EncodedBranch}'\n" +
                                "  Workspace: {Workspace}\n" +
                                "  Repository: {Repository}\n" +
                                "  Response Body: {ErrorBody}",
                                httpResponse.StatusCode,
                                (int)httpResponse.StatusCode,
                                nextUrl,
                                branchName,
                                cleanBranchName,
                                encodedBranch,
                                _config.Workspace,
                                _config.Repository,
                                errorContent.Length > 500 ? errorContent.Substring(0, 500) + "..." : errorContent);
                        }

                        httpResponse.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex,
                            "[DIAGNOSTIC] HttpRequestException Details:\n" +
                            "  Exception Message: {Message}\n" +
                            "  Status Code: {StatusCode}\n" +
                            "  URL: {Url}\n" +
                            "  Branch (original): '{OriginalBranch}'\n" +
                            "  Branch (cleaned): '{CleanBranch}'\n" +
                            "  Branch (encoded): '{EncodedBranch}'\n" +
                            "  Workspace: {Workspace}\n" +
                            "  Repository: {Repository}",
                            ex.Message,
                            ex.StatusCode,
                            nextUrl,
                            branchName,
                            cleanBranchName,
                            encodedBranch,
                            _config.Workspace,
                            _config.Repository);
                        throw;
                    }

                    var response = await httpResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation(
                        "[DIAGNOSTIC] Response content length: {Length} bytes",
                        response.Length);

                    var commitsResponse = JsonSerializer.Deserialize<BitbucketCommitsResponse>(response);

                    if (commitsResponse?.Values == null || commitsResponse.Values.Count == 0)
                    {
                        _logger.LogInformation(
                            "[DIAGNOSTIC] No more commits found on page {Page} for branch {Branch}",
                            pagesFetched + 1, cleanBranchName);
                        break;
                    }

                    _logger.LogInformation(
                        "[DIAGNOSTIC] Successfully deserialized {Count} commits from response",
                        commitsResponse.Values.Count);

                    // Map commits from this page
                    var pageCommits = commitsResponse.Values.Select(c => new BitbucketCommit
                    {
                        CommitId = c.Hash,
                        ShortCommitId = c.Hash.Length > 7 ? c.Hash.Substring(0, 7) : c.Hash,
                        Message = c.Message?.Trim() ?? string.Empty,
                        Author = c.Author?.User?.DisplayName ?? c.Author?.Raw ?? "Unknown",
                        AuthorUsername = c.Author?.User?.Username ?? string.Empty,
                        CommitDate = c.Date,
                        CommitUrl = $"https://bitbucket.org/{_config.Workspace}/{_config.Repository}/commits/{c.Hash}"
                    }).ToList();

                    allCommits.AddRange(pageCommits);
                    pagesFetched++;

                    _logger.LogInformation(
                        "Page {Page} fetched: {Count} commits (Total so far: {Total})",
                        pagesFetched, pageCommits.Count, allCommits.Count);

                    // Get next page URL from response
                    nextUrl = commitsResponse.Next;

                    // If we have a next URL, convert it from absolute to relative
                    if (!string.IsNullOrEmpty(nextUrl))
                    {
                        var baseUrl = _httpClient.BaseAddress?.ToString();
                        if (!string.IsNullOrEmpty(baseUrl) && nextUrl.StartsWith(baseUrl))
                        {
                            nextUrl = nextUrl.Replace(baseUrl, string.Empty);
                        }

                        _logger.LogInformation(
                            "[DIAGNOSTIC] Next page URL: {NextUrl}",
                            nextUrl);
                    }
                }

                var hasMorePages = !string.IsNullOrEmpty(nextUrl) && pagesFetched >= maxPages;

                _logger.LogInformation(
                    "Successfully retrieved {Total} commits across {Pages} pages for {Workspace}/{Repo}/branch/{Branch}. HasMorePages: {HasMore}",
                    allCommits.Count, pagesFetched, _config.Workspace, _config.Repository, cleanBranchName, hasMorePages);

                return new PagedBitbucketCommitsResponse
                {
                    Commits = allCommits,
                    TotalCommits = allCommits.Count,
                    PagesFetched = pagesFetched,
                    CommitsPerPage = pageLength,
                    HasMorePages = hasMorePages,
                    NextPageUrl = hasMorePages ? nextUrl : null
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "[DIAGNOSTIC] Final HttpRequestException caught for {Workspace}/{Repo}/branch/{Branch}: {StatusCode}",
                    _config.Workspace, _config.Repository, cleanBranchName, ex.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[DIAGNOSTIC] Unexpected exception while fetching commits for {Workspace}/{Repo}/branch/{Branch}",
                    _config.Workspace, _config.Repository, cleanBranchName);
                throw;
            }
        }) ?? new PagedBitbucketCommitsResponse();
    }

    public async Task<CommitComparisonResponse> GetCommitDifferenceAsync(
        string fromCommit,
        string toCommit)
    {
        var cacheKey = $"{COMPARISON_CACHE_KEY_PREFIX}{_config.Workspace}_{_config.Repository}_{fromCommit}_{toCommit}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ComparisonCacheDuration;
            entry.Size = 10;

            _logger.LogInformation(
                "Comparing commits from {FromCommit} to {ToCommit} for {Workspace}/{Repo}",
                fromCommit, toCommit, _config.Workspace, _config.Repository);

            var response = new CommitComparisonResponse
            {
                FromCommit = fromCommit,
                ToCommit = toCommit,
                FromCommitShort = fromCommit.Length > 7 ? fromCommit.Substring(0, 7) : fromCommit,
                ToCommitShort = toCommit.Length > 7 ? toCommit.Substring(0, 7) : toCommit
            };

            try
            {
                // Use Bitbucket's diff API which is more accurate
                var diffUrl = $"repositories/{_config.Workspace}/{_config.Repository}/diff/{toCommit}..{fromCommit}";

                try
                {
                    // First, check if we can use the commits API for a direct comparison
                    var compareUrl = $"repositories/{_config.Workspace}/{_config.Repository}/commits/?include={toCommit}&exclude={fromCommit}";

                    _logger.LogInformation("Attempting direct comparison using include/exclude");

                    var allCommits = new List<BitbucketCommit>();
                    var pagesFetched = 0;
                    var maxPages = 50;
                    string? nextUrl = compareUrl + "&pagelen=100";

                    while (!string.IsNullOrEmpty(nextUrl) && pagesFetched < maxPages)
                    {
                        _logger.LogInformation("Fetching comparison page {Page}", pagesFetched + 1);

                        var apiResponse = await _httpClient.GetStringAsync(nextUrl);
                        var commitsResponse = JsonSerializer.Deserialize<BitbucketCommitsResponse>(apiResponse);

                        if (commitsResponse?.Values == null || commitsResponse.Values.Count == 0)
                        {
                            break;
                        }

                        var pageCommits = commitsResponse.Values.Select(commit => new BitbucketCommit
                        {
                            CommitId = commit.Hash,
                            ShortCommitId = commit.Hash.Length > 7 ? commit.Hash.Substring(0, 7) : commit.Hash,
                            Message = commit.Message?.Trim() ?? string.Empty,
                            Author = commit.Author?.User?.DisplayName ?? commit.Author?.Raw ?? "Unknown",
                            AuthorUsername = commit.Author?.User?.Username ?? string.Empty,
                            CommitDate = commit.Date,
                            CommitUrl = $"https://bitbucket.org/{_config.Workspace}/{_config.Repository}/commits/{commit.Hash}"
                        }).ToList();

                        allCommits.AddRange(pageCommits);
                        pagesFetched++;

                        _logger.LogInformation(
                            "Page {Page} fetched: {Count} commits (Total so far: {Total})",
                            pagesFetched, pageCommits.Count, allCommits.Count);

                        // Get next page URL
                        nextUrl = commitsResponse.Next;
                        if (!string.IsNullOrEmpty(nextUrl))
                        {
                            nextUrl = nextUrl.Replace(_httpClient.BaseAddress?.ToString()!, string.Empty);
                        }
                    }

                    response.Commits = allCommits;
                    response.TotalCommits = allCommits.Count;
                    response.FromCommitFound = true;
                    response.ToCommitFound = true;

                    if (pagesFetched >= maxPages && !string.IsNullOrEmpty(nextUrl))
                    {
                        response.ErrorMessage = $"More than {allCommits.Count} commits found between these releases. Showing first {allCommits.Count}.";
                        _logger.LogWarning(response.ErrorMessage);
                    }

                    _logger.LogInformation(
                        "Successfully compared commits using include/exclude: found {Count} commits between {FromCommit} and {ToCommit}",
                        allCommits.Count, fromCommit, toCommit);

                    return response;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Include/exclude comparison failed, commits may not be on same branch: {Message}", ex.Message);
                    response.ErrorMessage = "The two commits do not appear to be on the same branch or one of the commits was not found. Please ensure both commits are from the same branch lineage.";
                    response.FromCommitFound = false;
                    response.ToCommitFound = false;
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Include/exclude comparison failed, falling back to legacy method");

                    // Fall back to the old method
                    return await FallbackComparisonAsync(fromCommit, toCommit, response);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "HTTP error comparing commits for {Workspace}/{Repo}: {StatusCode}",
                    _config.Workspace, _config.Repository, ex.StatusCode);

                response.ErrorMessage = $"HTTP error: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error comparing commits for {Workspace}/{Repo}",
                    _config.Workspace, _config.Repository);

                response.ErrorMessage = $"Error: {ex.Message}";
                return response;
            }
        }) ?? new CommitComparisonResponse
        {
            FromCommit = fromCommit,
            ToCommit = toCommit,
            ErrorMessage = "Failed to retrieve comparison from cache"
        };
    }

    private async Task<CommitComparisonResponse> FallbackComparisonAsync(
        string fromCommit,
        string toCommit,
        CommitComparisonResponse response)
    {
        _logger.LogInformation("Using fallback comparison method");

        // Fetch commits starting from toCommit (newer) going backwards
        var allCommits = new List<BitbucketCommit>();
        var pagesFetched = 0;
        var maxPages = 50;
        var foundFromCommit = false;
        var foundToCommit = false;

        string? nextUrl = $"repositories/{_config.Workspace}/{_config.Repository}/commits/{toCommit}?pagelen=100";

        while (!string.IsNullOrEmpty(nextUrl) && pagesFetched < maxPages && !foundFromCommit)
        {
            _logger.LogInformation("Fetching fallback comparison page {Page}", pagesFetched + 1);

            var apiResponse = await _httpClient.GetStringAsync(nextUrl);
            var commitsResponse = JsonSerializer.Deserialize<BitbucketCommitsResponse>(apiResponse);

            if (commitsResponse?.Values == null || commitsResponse.Values.Count == 0)
            {
                break;
            }

            foreach (var commit in commitsResponse.Values)
            {
                // Check if this is the toCommit (should be first)
                if (commit.Hash.StartsWith(toCommit, StringComparison.OrdinalIgnoreCase) && !foundToCommit)
                {
                    foundToCommit = true;
                    _logger.LogInformation("Found toCommit: {Commit}", commit.Hash);
                    continue; // Don't include the toCommit itself
                }

                // Check if this is the fromCommit (we stop here)
                if (commit.Hash.StartsWith(fromCommit, StringComparison.OrdinalIgnoreCase))
                {
                    foundFromCommit = true;
                    _logger.LogInformation("Found fromCommit: {Commit}", commit.Hash);
                    break; // Stop - we've reached the starting commit
                }

                // Only add commits after we've found toCommit
                if (foundToCommit)
                {
                    allCommits.Add(new BitbucketCommit
                    {
                        CommitId = commit.Hash,
                        ShortCommitId = commit.Hash.Length > 7 ? commit.Hash.Substring(0, 7) : commit.Hash,
                        Message = commit.Message?.Trim() ?? string.Empty,
                        Author = commit.Author?.User?.DisplayName ?? commit.Author?.Raw ?? "Unknown",
                        AuthorUsername = commit.Author?.User?.Username ?? string.Empty,
                        CommitDate = commit.Date,
                        CommitUrl = $"https://bitbucket.org/{_config.Workspace}/{_config.Repository}/commits/{commit.Hash}"
                    });
                }
            }

            pagesFetched++;

            // Get next page URL
            nextUrl = commitsResponse.Next;
            if (!string.IsNullOrEmpty(nextUrl))
            {
                nextUrl = nextUrl.Replace(_httpClient.BaseAddress?.ToString()!, string.Empty);
            }
        }

        response.Commits = allCommits;
        response.TotalCommits = allCommits.Count;
        response.FromCommitFound = foundFromCommit;
        response.ToCommitFound = foundToCommit;

        if (!foundToCommit)
        {
            response.ErrorMessage = $"Could not find the newer commit ({toCommit}) in the repository history.";
            _logger.LogWarning(response.ErrorMessage);
        }
        else if (!foundFromCommit)
        {
            response.ErrorMessage = $"Could not find the older commit ({fromCommit}) in the repository history after checking {allCommits.Count} commits. These commits may not be on the same branch, or there are more than 5000 commits between them.";
            _logger.LogWarning(response.ErrorMessage);
        }

        _logger.LogInformation(
            "Fallback comparison completed: found {Count} commits between {FromCommit} and {ToCommit}",
            allCommits.Count, fromCommit, toCommit);

        return response;
    }
}