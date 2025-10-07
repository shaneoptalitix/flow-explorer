using AzureDevOpsReporter.Models;
using AzureDevOpsReporter.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsReporter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PipelineController : ControllerBase
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IAzureDevOpsService azureDevOpsService,
        ILogger<PipelineController> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets pipeline branches with build information for a specific pipeline definition
    /// </summary>
    /// <param name="definitionId">The pipeline definition ID</param>
    /// <param name="resultFilter">Filter by build result (default: succeeded). Use empty string to show all results.</param>
    /// <param name="sortBy">Sort field: latestBuildFinishTime, latestBuildStartTime, branchName, or totalBuilds (default: latestBuildFinishTime)</param>
    /// <param name="sortOrder">Sort order: asc or desc (default: desc)</param>
    /// <returns>List of branches with their latest build information</returns>
    /// <response code="200">Returns the list of pipeline branches</response>
    /// <response code="400">Bad request - invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{definitionId}/branches")]
    [ProducesResponseType(typeof(List<PipelineBranchInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<PipelineBranchInfo>>> GetPipelineBranches(
        int definitionId,        
        [FromQuery] int top = 300,
        [FromQuery] string sortBy = "latestBuildFinishTime",
        [FromQuery] string sortOrder = "desc")
    {
        try
        {
            if (definitionId <= 0)
            {
                return BadRequest("Definition ID must be greater than 0.");
            }

            // Validate sort parameters
            var validSortFields = new[] { "latestbuildfinishtime", "latestbuildstarttime", "branchname", "totalbuilds" };
            if (!validSortFields.Contains(sortBy.ToLowerInvariant()))
            {
                return BadRequest($"Invalid sortBy value. Valid values are: latestBuildFinishTime, latestBuildStartTime, branchName, totalBuilds");
            }

            var validSortOrders = new[] { "asc", "desc" };
            if (!validSortOrders.Contains(sortOrder.ToLowerInvariant()))
            {
                return BadRequest("Invalid sortOrder value. Valid values are: asc, desc");
            }

            _logger.LogInformation(
                "Getting branches for pipeline definition {DefinitionId} with filters - SortBy: {SortBy}, SortOrder: {SortOrder}", 
                definitionId, sortBy, sortOrder);
            
            var branches = await _azureDevOpsService.GetPipelineBranchesAsync(definitionId, top, sortBy, sortOrder);
            
            _logger.LogInformation(
                "Successfully retrieved {Count} branches for pipeline definition {DefinitionId}", 
                branches.Count, definitionId);
            
            return Ok(branches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting pipeline branches for definition {DefinitionId}", definitionId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred while processing your request");
        }
    }
}