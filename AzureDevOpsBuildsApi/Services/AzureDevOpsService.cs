using System.Text;
using System.Text.Json;
using AzureDevOpsReporter.Configuration;
using AzureDevOpsReporter.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AzureDevOpsReporter.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsConfig _config;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly IMemoryCache _cache;
    
    // Cache keys
    private const string ENVIRONMENTS_CACHE_KEY = "environments";
    private const string VARIABLE_GROUPS_CACHE_KEY = "variablegroups";
    private const string DEPLOYMENT_RECORDS_CACHE_KEY_PREFIX = "deployments_env_";
    private const string BUILD_CACHE_KEY_PREFIX = "build_";
    
    // Cache durations
    private static readonly TimeSpan EnvironmentsCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VariableGroupsCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DeploymentRecordsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BuildCacheDuration = TimeSpan.FromMinutes(30);

    public AzureDevOpsService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<AzureDevOpsService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _config = configuration.GetSection("AzureDevOps").Get<AzureDevOpsConfig>() 
                 ?? throw new InvalidOperationException("AzureDevOps configuration is missing");
        _logger = logger;
        _cache = cache;
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_config.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
        _httpClient.BaseAddress = new Uri($"https://dev.azure.com/{_config.Organization}/{_config.Project}/");
    }

    public async Task<PagedEnvironmentReportResponse> GetEnvironmentReportsAsync(
        string? environmentNameFilter = null,
        string? stageNameFilter = null,
        string? resultFilter = null,
        int pageNumber = 1,
        int pageSize = 40,
        bool includeVariableGroups = true,
        string sortBy = "deploymentFinishTime",
        string sortOrder = "desc")
    {
        var reports = new List<EnvironmentReport>();

        try
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, Math.Min(100, pageSize));

            _logger.LogInformation(
                "Starting to fetch environment reports - Page: {PageNumber}, PageSize: {PageSize}, " +
                "IncludeVariableGroups: {IncludeVariableGroups}, SortBy: {SortBy}, SortOrder: {SortOrder}", 
                pageNumber, pageSize, includeVariableGroups, sortBy, sortOrder);
            
            var environmentsTask = GetEnvironmentsAsync();
            Task<List<VariableGroup>>? variableGroupsTask = null;
            
            if (includeVariableGroups)
            {
                variableGroupsTask = GetVariableGroupsAsync();
                await Task.WhenAll(environmentsTask, variableGroupsTask);
            }
            else
            {
                await environmentsTask;
            }
            
            var environments = await environmentsTask;
            var variableGroups = includeVariableGroups && variableGroupsTask != null 
                ? await variableGroupsTask 
                : new List<VariableGroup>();
            
            _logger.LogInformation($"Found {environments.Count} environments" + 
                (includeVariableGroups ? $" and {variableGroups.Count} variable groups" : ""));

            if (!string.IsNullOrEmpty(environmentNameFilter))
            {
                environments = environments.Where(e => 
                    e.Name.Contains(environmentNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                _logger.LogInformation($"Filtered to {environments.Count} environments by name");
            }

            foreach (var environment in environments)
            {
                _logger.LogInformation($"Processing environment: {environment.Name} (ID: {environment.Id})");
                
                VariableGroup? matchingVariableGroup = null;
                if (includeVariableGroups)
                {
                    matchingVariableGroup = variableGroups.FirstOrDefault(vg => 
                        vg.Name.Equals(environment.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingVariableGroup != null)
                    {
                        _logger.LogInformation($"Found matching variable group: {matchingVariableGroup.Name} (ID: {matchingVariableGroup.Id}) for environment {environment.Name}");
                    }
                }
                
                var deploymentRecords = await GetEnvironmentDeploymentRecordsAsync(environment.Id);
                _logger.LogInformation($"Found {deploymentRecords.Count} deployment records for environment {environment.Name}");

                if (!string.IsNullOrEmpty(stageNameFilter))
                {
                    deploymentRecords = deploymentRecords.Where(dr =>
                        dr.StageName.Contains(stageNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(resultFilter))
                {
                    deploymentRecords = deploymentRecords.Where(dr =>
                        dr.Result.Equals(resultFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var latestDeployment = deploymentRecords.OrderByDescending(dr => dr.FinishTime).FirstOrDefault();
                
                if (latestDeployment != null)
                {
                    var historicalDeployments = deploymentRecords
                        .Where(dr => dr.Result.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
                        .Where(dr => dr.FinishTime < latestDeployment.FinishTime)
                        .OrderByDescending(dr => dr.FinishTime)
                        .Take(3)
                        .ToList();
                    
                    _logger.LogInformation($"Found {historicalDeployments.Count} historical deployments for environment {environment.Name}");

                    var report = await CreateEnvironmentReport(
                        environment, 
                        latestDeployment, 
                        matchingVariableGroup, 
                        includeVariableGroups);
                    
                    foreach (var histDep in historicalDeployments)
                    {
                        var histReport = await CreateEnvironmentReport(
                            environment, 
                            histDep, 
                            matchingVariableGroup, 
                            includeVariableGroups);
                        report.HistoricalDeployments.Add(histReport);
                    }
                    
                    reports.Add(report);
                }
            }

            reports = ApplySorting(reports, sortBy, sortOrder);
            
            var totalCount = reports.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            
            var pagedReports = reports
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            _logger.LogInformation($"Completed processing. Total reports: {totalCount}, Page {pageNumber}/{totalPages}, Returning: {pagedReports.Count} items");
            
            return new PagedEnvironmentReportResponse
            {
                Data = pagedReports,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching environment reports");
            throw;
        }
    }

    private async Task<EnvironmentReport> CreateEnvironmentReport(
        Models.Environment environment,
        EnvironmentDeploymentRecord deploymentRecord,
        VariableGroup? matchingVariableGroup,
        bool includeVariableGroups)
    {
        try
        {
            var build = await GetBuildAsync(deploymentRecord.Owner.Id);
            
            var variableDict = includeVariableGroups && matchingVariableGroup?.Variables != null 
                ? matchingVariableGroup.Variables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.IsSecret ? "[HIDDEN]" : kvp.Value.Value
                ) 
                : new Dictionary<string, string>();
            
            var portalUrl = CalculatePortalUrl(environment.Name, matchingVariableGroup);
            
            return new EnvironmentReport
            {
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name,
                EnvironmentLastModifiedBy = environment.LastModifiedBy.UniqueName,
                EnvironmentLastModifiedOn = environment.LastModifiedOn,
                DeploymentRecordId = deploymentRecord.Id,
                DeploymentRecordEnvironmentId = deploymentRecord.EnvironmentId,
                DeploymentRecordStageName = deploymentRecord.StageName,
                DeploymentRecordDefinitionId = deploymentRecord.Definition.Id,
                DeploymentRecordDefinitionName = deploymentRecord.Definition.Name,
                DeploymentRecordOwnerId = deploymentRecord.Owner.Id,
                DeploymentRecordOwnerName = deploymentRecord.Owner.Name,
                DeploymentRecordResult = deploymentRecord.Result,
                DeploymentRecordFinishTime = deploymentRecord.FinishTime,
                BuildId = build?.Id ?? 0,
                BuildNumber = build?.BuildNumber ?? string.Empty,
                BuildStatus = build?.Status ?? string.Empty,
                BuildStartTime = build?.StartTime,
                BuildSourceBranch = build?.SourceBranch ?? string.Empty,
                BuildSourceVersion = build?.SourceVersion ?? string.Empty,
                BuildTriggerMessage = build?.TriggerInfo?.Message ?? string.Empty,
                BuildTriggerRepository = build?.TriggerInfo?.TriggerRepository ?? string.Empty,
                VariableGroupId = matchingVariableGroup?.Id ?? 0,
                VariableGroupName = matchingVariableGroup?.Name ?? string.Empty,
                VariableGroupVariables = variableDict,
                PortalUrl = portalUrl,
                HistoricalDeployments = new List<EnvironmentReport>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to get build details for deployment record {deploymentRecord.Id}: {ex.Message}");
            
            var variableDict = includeVariableGroups && matchingVariableGroup?.Variables != null 
                ? matchingVariableGroup.Variables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.IsSecret ? "[HIDDEN]" : kvp.Value.Value
                ) 
                : new Dictionary<string, string>();
            
            var portalUrl = CalculatePortalUrl(environment.Name, matchingVariableGroup);
            
            return new EnvironmentReport
            {
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name,
                EnvironmentLastModifiedBy = environment.LastModifiedBy.UniqueName,
                EnvironmentLastModifiedOn = environment.LastModifiedOn,
                DeploymentRecordId = deploymentRecord.Id,
                DeploymentRecordEnvironmentId = deploymentRecord.EnvironmentId,
                DeploymentRecordStageName = deploymentRecord.StageName,
                DeploymentRecordDefinitionId = deploymentRecord.Definition.Id,
                DeploymentRecordDefinitionName = deploymentRecord.Definition.Name,
                DeploymentRecordOwnerId = deploymentRecord.Owner.Id,
                DeploymentRecordOwnerName = deploymentRecord.Owner.Name,
                DeploymentRecordResult = deploymentRecord.Result,
                DeploymentRecordFinishTime = deploymentRecord.FinishTime,
                VariableGroupId = matchingVariableGroup?.Id ?? 0,
                VariableGroupName = matchingVariableGroup?.Name ?? string.Empty,
                VariableGroupVariables = variableDict,
                PortalUrl = portalUrl,
                HistoricalDeployments = new List<EnvironmentReport>()
            };
        }
    }

    private string? CalculatePortalUrl(string environmentName, VariableGroup? variableGroup)
    {
        if (variableGroup?.Variables == null)
        {
            return null;
        }

        var variables = variableGroup.Variables;
        
        string? GetVariableValue(string key)
        {
            if (variables.TryGetValue(key, out var variable) && !variable.IsSecret)
            {
                return variable.Value;
            }
            return null;
        }

        var loadBalancerDomain = GetVariableValue("LoadBalancer.Domain");
        var elsaEnabledStr = GetVariableValue("Elsa.Enabled");
        var kubernetesHostname = GetVariableValue("Kubernetes.HttpRoute.Hostname");
        var tenantName = GetVariableValue("Tenant.Name")?.ToLowerInvariant();

        var isElsaEnabled = !string.IsNullOrEmpty(elsaEnabledStr) && 
                           elsaEnabledStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (!isElsaEnabled)
        {
            if (!string.IsNullOrEmpty(loadBalancerDomain))
            {
                return $"https://{loadBalancerDomain}";
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(kubernetesHostname))
            {
                return $"https://{kubernetesHostname}";
            }
            else if (!string.IsNullOrEmpty(tenantName))
            {
                var envNameLower = environmentName.ToLowerInvariant();
                string env;
                
                if (envNameLower.Contains("uat"))
                {
                    env = "uat";
                }
                else if (envNameLower.Contains("qa"))
                {
                    env = "dev";
                }
                else
                {
                    env = "dev";
                }
                
                return $"https://{tenantName}-{env}.flow.optalitix.net/{tenantName}/login";
            }
        }

        return null;
    }

    private List<EnvironmentReport> ApplySorting(List<EnvironmentReport> reports, string sortBy, string sortOrder)
    {
        var isAscending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
        
        return sortBy.ToLowerInvariant() switch
        {
            "deploymentfinishtime" => isAscending 
                ? reports.OrderBy(r => r.DeploymentRecordFinishTime ?? DateTime.MinValue).ToList()
                : reports.OrderByDescending(r => r.DeploymentRecordFinishTime ?? DateTime.MinValue).ToList(),
            
            "buildstarttime" => isAscending
                ? reports.OrderBy(r => r.BuildStartTime ?? DateTime.MinValue).ToList()
                : reports.OrderByDescending(r => r.BuildStartTime ?? DateTime.MinValue).ToList(),
            
            _ => reports.OrderByDescending(r => r.DeploymentRecordFinishTime ?? DateTime.MinValue).ToList()
        };
    }

    private async Task<List<Models.Environment>> GetEnvironmentsAsync()
    {
        return await _cache.GetOrCreateAsync(ENVIRONMENTS_CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EnvironmentsCacheDuration;
            entry.Size = 10; // Relative size for cache management
            _logger.LogInformation("Cache miss - fetching environments from Azure DevOps");
            
            var url = $"_apis/distributedtask/environments?api-version={_config.ApiVersion}";
            var response = await _httpClient.GetStringAsync(url);
            var environmentsResponse = JsonSerializer.Deserialize<EnvironmentsResponse>(response);
            return environmentsResponse?.Value ?? new List<Models.Environment>();
        }) ?? new List<Models.Environment>();
    }

    private async Task<List<EnvironmentDeploymentRecord>> GetEnvironmentDeploymentRecordsAsync(int environmentId)
    {
        var cacheKey = $"{DEPLOYMENT_RECORDS_CACHE_KEY_PREFIX}{environmentId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DeploymentRecordsCacheDuration;
            entry.Size = 5; // Smaller relative size
            _logger.LogInformation("Cache miss - fetching deployment records for environment {EnvironmentId}", environmentId);
            
            var url = $"_apis/distributedtask/environments/{environmentId}/environmentdeploymentrecords?api-version={_config.ApiVersion}";
            var response = await _httpClient.GetStringAsync(url);
            var deploymentRecordsResponse = JsonSerializer.Deserialize<EnvironmentDeploymentRecordsResponse>(response);
            return deploymentRecordsResponse?.Value ?? new List<EnvironmentDeploymentRecord>();
        }) ?? new List<EnvironmentDeploymentRecord>();
    }

    private async Task<List<VariableGroup>> GetVariableGroupsAsync()
    {
        return await _cache.GetOrCreateAsync(VARIABLE_GROUPS_CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = VariableGroupsCacheDuration;
            entry.Size = 15; // Larger relative size for variable groups
            _logger.LogInformation("Cache miss - fetching variable groups from Azure DevOps");
            
            var url = $"_apis/distributedtask/variablegroups?api-version={_config.ApiVersion}";
            var response = await _httpClient.GetStringAsync(url);
            var variableGroupsResponse = JsonSerializer.Deserialize<VariableGroupsResponse>(response);
            return variableGroupsResponse?.Value ?? new List<VariableGroup>();
        }) ?? new List<VariableGroup>();
    }

    private async Task<Build?> GetBuildAsync(int buildId)
    {
        var cacheKey = $"{BUILD_CACHE_KEY_PREFIX}{buildId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = BuildCacheDuration;
            entry.Size = 1; // Small size for individual builds
            
            try
            {
                _logger.LogInformation("Cache miss - fetching build {BuildId} from Azure DevOps", buildId);
                var url = $"_apis/build/builds/{buildId}?api-version={_config.ApiVersion}";
                var response = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<Build>(response);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _logger.LogWarning($"Build with ID {buildId} not found");
                return null;
            }
        });
    }
}