using System.Text.Json.Serialization;

namespace AzureDevOpsReporter.Models;

// Response models for Azure DevOps API
public class EnvironmentsResponse
{
    [JsonPropertyName("value")]
    public List<Environment> Value { get; set; } = new();
}

public class Environment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("lastModifiedBy")]
    public LastModifiedBy LastModifiedBy { get; set; } = new();
    
    [JsonPropertyName("lastModifiedOn")]
    public DateTime LastModifiedOn { get; set; }
}

public class LastModifiedBy
{
    [JsonPropertyName("uniqueName")]
    public string UniqueName { get; set; } = string.Empty;
}

public class EnvironmentDeploymentRecordsResponse
{
    [JsonPropertyName("value")]
    public List<EnvironmentDeploymentRecord> Value { get; set; } = new();
}

public class EnvironmentDeploymentRecord
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("environmentId")]
    public int EnvironmentId { get; set; }
    
    [JsonPropertyName("stageName")]
    public string StageName { get; set; } = string.Empty;
    
    [JsonPropertyName("definition")]
    public Definition Definition { get; set; } = new();
    
    [JsonPropertyName("owner")]
    public Owner Owner { get; set; } = new();
    
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
    
    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }
}

public class Definition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Owner
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class BuildsResponse
{
    [JsonPropertyName("value")]
    public List<Build> Value { get; set; } = new();
}

public class Build
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }
    
    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }
    
    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = string.Empty;
    
    [JsonPropertyName("sourceVersion")]
    public string SourceVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("triggerInfo")]
    public TriggerInfo? TriggerInfo { get; set; }
}

public class TriggerInfo
{
    [JsonPropertyName("ci.message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("ci.triggerRepository")]
    public string TriggerRepository { get; set; } = string.Empty;
}

public class VariableGroupsResponse
{
    [JsonPropertyName("value")]
    public List<VariableGroup> Value { get; set; } = new();
}

public class VariableGroup
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("variables")]
    public Dictionary<string, VariableValue> Variables { get; set; } = new();
}

public class VariableValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }
}

// Pipeline Branch Info
public class PipelineBranchInfo
{
    public string BranchName { get; set; } = string.Empty;
    public int LatestBuildId { get; set; }
    public string LatestBuildNumber { get; set; } = string.Empty;
    public string LatestBuildStatus { get; set; } = string.Empty;
    public string LatestBuildResult { get; set; } = string.Empty;
    public DateTime? LatestBuildStartTime { get; set; }
    public DateTime? LatestBuildFinishTime { get; set; }
    public string LatestBuildSourceVersion { get; set; } = string.Empty;
    public int TotalBuilds { get; set; }
}

// Output model
public class EnvironmentReport
{
    public int EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string EnvironmentLastModifiedBy { get; set; } = string.Empty;
    public DateTime EnvironmentLastModifiedOn { get; set; }
    public int DeploymentRecordId { get; set; }
    public int DeploymentRecordEnvironmentId { get; set; }
    public string DeploymentRecordStageName { get; set; } = string.Empty;
    public int DeploymentRecordDefinitionId { get; set; }
    public string DeploymentRecordDefinitionName { get; set; } = string.Empty;
    public int DeploymentRecordOwnerId { get; set; }
    public string DeploymentRecordOwnerName { get; set; } = string.Empty;
    public string DeploymentRecordResult { get; set; } = string.Empty;
    public DateTime? DeploymentRecordFinishTime { get; set; }
    public int BuildId { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string BuildStatus { get; set; } = string.Empty;
    public DateTime? BuildStartTime { get; set; }
    public string BuildSourceBranch { get; set; } = string.Empty;
    public string BuildSourceVersion { get; set; } = string.Empty;
    public string BuildTriggerMessage { get; set; } = string.Empty;
    public string BuildTriggerRepository { get; set; } = string.Empty;
    public int VariableGroupId { get; set; }
    public string VariableGroupName { get; set; } = string.Empty;
    public Dictionary<string, string> VariableGroupVariables { get; set; } = new();
    public string? PortalUrl { get; set; } = null;
    public List<EnvironmentReport> HistoricalDeployments { get; set; } = new();
}

// Paged response model
public class PagedEnvironmentReportResponse
{
    public List<EnvironmentReport> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}