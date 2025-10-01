using System.Text;
using System.Text.Json;
using AzureDevOpsReporter.Configuration;
using AzureDevOpsReporter.Models;
using Microsoft.Extensions.Options;

namespace AzureDevOpsReporter.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsConfig _config;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<AzureDevOpsService> logger)
    {
        _httpClient = httpClient;
        _config = configuration.GetSection("AzureDevOps").Get<AzureDevOpsConfig>() 
                 ?? throw new InvalidOperationException("AzureDevOps configuration is missing");
        _logger = logger;
        
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
            // Validate paging parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, Math.Min(100, pageSize)); // Limit max page size to 100

            _logger.LogInformation(
                "Starting to fetch environment reports - Page: {PageNumber}, PageSize: {PageSize}, " +
                "IncludeVariableGroups: {IncludeVariableGroups}, SortBy: {SortBy}, SortOrder: {SortOrder}", 
                pageNumber, pageSize, includeVariableGroups, sortBy, sortOrder);
            
            // Get environments and optionally variable groups in parallel
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

            // Apply environment name filter
            if (!string.IsNullOrEmpty(environmentNameFilter))
            {
                environments = environments.Where(e => 
                    e.Name.Contains(environmentNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                _logger.LogInformation($"Filtered to {environments.Count} environments by name");
            }

            foreach (var environment in environments)
            {
                _logger.LogInformation($"Processing environment: {environment.Name} (ID: {environment.Id})");
                
                // Find matching variable group by name (only if includeVariableGroups is true)
                VariableGroup? matchingVariableGroup = null;
                if (includeVariableGroups)
                {
                    matchingVariableGroup = variableGroups.FirstOrDefault(vg => 
                        vg.Name.Equals(environment.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingVariableGroup != null)
                    {
                        _logger.LogInformation($"Found matching variable group: {matchingVariableGroup.Name} (ID: {matchingVariableGroup.Id}) for environment {environment.Name}");
                    }
                    else
                    {
                        _logger.LogWarning($"No matching variable group found for environment: {environment.Name}");
                    }
                }
                
                // Get deployment records for this environment
                var deploymentRecords = await GetEnvironmentDeploymentRecordsAsync(environment.Id);
                _logger.LogInformation($"Found {deploymentRecords.Count} deployment records for environment {environment.Name}");

                // Apply stage name filter
                if (!string.IsNullOrEmpty(stageNameFilter))
                {
                    deploymentRecords = deploymentRecords.Where(dr =>
                        dr.StageName.Contains(stageNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered stage name {deploymentRecords.Count} deployment records for environment {environment.Name}");
                }

                // Apply result filter
                if (!string.IsNullOrEmpty(resultFilter))
                {
                    deploymentRecords = deploymentRecords.Where(dr =>
                        dr.Result.Equals(resultFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation($"Filtered result {deploymentRecords.Count} deployment records for environment {environment.Name}");                        
                }

                foreach (var deploymentRecord in deploymentRecords.OrderByDescending(dr => dr.FinishTime).Take(1))
                {
                    try
                    {
                        // Get build details using owner.id as buildId
                        var build = await GetBuildAsync(deploymentRecord.Owner.Id);
                        
                        // Prepare variable dictionary
                        var variableDict = includeVariableGroups && matchingVariableGroup?.Variables != null 
                            ? matchingVariableGroup.Variables.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value.IsSecret ? "[HIDDEN]" : kvp.Value.Value
                            ) 
                            : new Dictionary<string, string>();
                        
                        // Calculate PortalUrl
                        var portalUrl = CalculatePortalUrl(environment.Name, matchingVariableGroup);
                        
                        var report = new EnvironmentReport
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
                            PortalUrl = portalUrl
                        };

                        reports.Add(report);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to get build details for deployment record {deploymentRecord.Id}: {ex.Message}");
                        
                        // Prepare variable dictionary
                        var variableDict = includeVariableGroups && matchingVariableGroup?.Variables != null 
                            ? matchingVariableGroup.Variables.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value.IsSecret ? "[HIDDEN]" : kvp.Value.Value
                            ) 
                            : new Dictionary<string, string>();
                        
                        // Calculate PortalUrl
                        var portalUrl = CalculatePortalUrl(environment.Name, matchingVariableGroup);
                        
                        // Still add the report without build details
                        var report = new EnvironmentReport
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
                            PortalUrl = portalUrl
                        };

                        reports.Add(report);
                    }
                }
            }

            // Apply sorting based on parameters
            reports = ApplySorting(reports, sortBy, sortOrder);
            
            var totalCount = reports.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            
            // Apply paging
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

    private string? CalculatePortalUrl(string environmentName, VariableGroup? variableGroup)
    {
        if (variableGroup?.Variables == null)
        {
            return null;
        }

        var variables = variableGroup.Variables;
        
        // Helper to get non-secret variable value
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

        // Check if Elsa.Enabled exists and is true
        var isElsaEnabled = !string.IsNullOrEmpty(elsaEnabledStr) && 
                           elsaEnabledStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (!isElsaEnabled)
        {
            // Condition 1: Elsa.Enabled doesn't exist or = false
            if (!string.IsNullOrEmpty(loadBalancerDomain))
            {
                return $"https://{loadBalancerDomain}";
            }
        }
        else
        {
            // Condition 2: Elsa.Enabled = true
            if (!string.IsNullOrEmpty(kubernetesHostname))
            {
                // Has Kubernetes.HttpRoute.Hostname
                return $"https://{kubernetesHostname}";
            }
            else if (!string.IsNullOrEmpty(tenantName))
            {
                // No Kubernetes.HttpRoute.Hostname, build URL from Tenant.Name
                // Determine {env} from environment name
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
                    // Default fallback
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
        var url = $"_apis/distributedtask/environments?api-version={_config.ApiVersion}";
        var response = await _httpClient.GetStringAsync(url);
        var environmentsResponse = JsonSerializer.Deserialize<EnvironmentsResponse>(response);
        return environmentsResponse?.Value ?? new List<Models.Environment>();
    }

    private async Task<List<EnvironmentDeploymentRecord>> GetEnvironmentDeploymentRecordsAsync(int environmentId)
    {
        var url = $"_apis/distributedtask/environments/{environmentId}/environmentdeploymentrecords?api-version={_config.ApiVersion}";
        var response = await _httpClient.GetStringAsync(url);
        var deploymentRecordsResponse = JsonSerializer.Deserialize<EnvironmentDeploymentRecordsResponse>(response);
        return deploymentRecordsResponse?.Value ?? new List<EnvironmentDeploymentRecord>();
    }

    private async Task<List<VariableGroup>> GetVariableGroupsAsync()
    {
        var url = $"_apis/distributedtask/variablegroups?api-version={_config.ApiVersion}";
        var response = await _httpClient.GetStringAsync(url);
        var variableGroupsResponse = JsonSerializer.Deserialize<VariableGroupsResponse>(response);
        return variableGroupsResponse?.Value ?? new List<VariableGroup>();
    }

    private async Task<Build?> GetBuildAsync(int buildId)
    {
        try
        {
            var url = $"_apis/build/builds/{buildId}?api-version={_config.ApiVersion}";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<Build>(response);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning($"Build with ID {buildId} not found");
            return null;
        }
    }
}