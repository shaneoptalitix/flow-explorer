using System.Text.Json.Serialization;

namespace AzureDevOpsReporter.Models;

// API Response from Bitbucket
public class BitbucketCommitsResponse
{
    [JsonPropertyName("values")]
    public List<BitbucketCommitDetail> Values { get; set; } = new();

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

public class BitbucketCommitDetail
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public BitbucketAuthor Author { get; set; } = new();
}

public class BitbucketAuthor
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public BitbucketUser? User { get; set; }
}

public class BitbucketUser
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

// Simplified output model for API consumers
public class BitbucketCommit
{
    public string CommitId { get; set; } = string.Empty;
    public string ShortCommitId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime CommitDate { get; set; }
    public string CommitUrl { get; set; } = string.Empty;
}