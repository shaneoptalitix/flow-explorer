namespace AzureDevOpsReporter.Configuration;

public class AzureDevOpsConfig
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
}