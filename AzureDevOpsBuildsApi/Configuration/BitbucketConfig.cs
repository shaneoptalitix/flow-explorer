namespace AzureDevOpsReporter.Configuration;

public class BitbucketConfig
{
    public string Workspace { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}