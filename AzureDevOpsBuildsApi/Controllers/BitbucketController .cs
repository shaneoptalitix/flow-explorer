using Microsoft.AspNetCore.Mvc;
using AzureDevOpsReporter.Services;
using AzureDevOpsReporter.Models;

namespace AzureDevOpsReporter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BitbucketController : ControllerBase
{
    private readonly IBitbucketService _bitbucketService;
    private readonly ILogger<BitbucketController> _logger;

    public BitbucketController(
        IBitbucketService bitbucketService,
        ILogger<BitbucketController> logger)
    {
        _bitbucketService = bitbucketService;
        _logger = logger;
    }

    /// <summary>
    /// Gets commits for a specific branch from Bitbucket
    /// </summary>
    /// <param name="branchName">The branch name (e.g., 'main', 'develop', 'refs/heads/feature/xyz')</param>
    /// <param name="pageLength">Number of commits to return (default: 30, max: 100)</param>
    /// <returns>List of commits for the specified branch</returns>
    /// <response code="200">Returns the list of commits</response>
    /// <response code="400">Bad request - invalid parameters</response>
    /// <response code="404">Repository or branch not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("commits/{branchName}")]
    [ProducesResponseType(typeof(List<BitbucketCommit>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BitbucketCommit>>> GetCommits(
        string branchName,
        [FromQuery] int pageLength = 30)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return BadRequest("Branch name cannot be empty");
            }

            if (pageLength < 1 || pageLength > 100)
            {
                return BadRequest("Page length must be between 1 and 100");
            }

            _logger.LogInformation(
                "Getting commits for branch {Branch}, pageLength: {PageLength}",
                branchName, pageLength);

            var commits = await _bitbucketService.GetCommitsAsync(branchName, pageLength);

            _logger.LogInformation(
                "Successfully retrieved {Count} commits for branch {Branch}",
                commits.Count, branchName);

            return Ok(commits);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Branch not found: {Branch}", branchName);
            return NotFound($"Branch not found: {branchName}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogError(ex, "Unauthorized access to Bitbucket API");
            return StatusCode(StatusCodes.Status401Unauthorized, 
                "Unauthorized: Check Bitbucket credentials in configuration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting commits for branch {Branch}", branchName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "An error occurred while processing your request");
        }
    }
}