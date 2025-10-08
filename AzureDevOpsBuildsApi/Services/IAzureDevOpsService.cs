using AzureDevOpsReporter.Models;

namespace AzureDevOpsReporter.Services;

public interface IAzureDevOpsService
{
    Task<PagedEnvironmentReportResponse> GetEnvironmentReportsAsync(
        string? environmentNameFilter = null,
        string? stageNameFilter = null,
        string? resultFilter = null,
        int pageNumber = 1,
        int pageSize = 40,
        bool includeVariableGroups = true,
        string sortBy = "deploymentFinishTime",
        string sortOrder = "desc");

    Task<List<PipelineBranchInfo>> GetPipelineBranchesAsync(
        int definitionId,
        int top = 300,
        string sortBy = "latestBuildStartTime",
        string sortOrder = "desc");

    bool ClearCache();
}