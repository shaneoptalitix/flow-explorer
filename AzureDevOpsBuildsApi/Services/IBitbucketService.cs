using AzureDevOpsReporter.Models;

namespace AzureDevOpsReporter.Services;

public interface IBitbucketService
{
    Task<List<BitbucketCommit>> GetCommitsAsync(
        string branchName,
        int pageLength = 30);

    bool ClearCache();
}