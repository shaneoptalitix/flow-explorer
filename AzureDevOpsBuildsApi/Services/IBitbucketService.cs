using AzureDevOpsReporter.Models;

namespace AzureDevOpsReporter.Services;

public interface IBitbucketService
{
    Task<PagedBitbucketCommitsResponse> GetCommitsAsync(
        string branchName,
        int pageLength = 30,
        int maxPages = 10);

    Task<CommitComparisonResponse> GetCommitDifferenceAsync(
        string fromCommit,
        string toCommit);

    bool ClearCache();
}