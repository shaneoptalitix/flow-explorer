using AzureDevOpsReporter.Models;
using AzureDevOpsReporter.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsReporter.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EnvironmentReportController : ControllerBase
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<EnvironmentReportController> _logger;

    public EnvironmentReportController(
        IAzureDevOpsService azureDevOpsService,
        ILogger<EnvironmentReportController> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets environment reports with optional filtering, sorting and paging
    /// </summary>
    /// <param name="environmentName">Filter by environment name (partial match, case-insensitive)</param>
    /// <param name="result">Filter by deployment result (exact match, case-insensitive)</param>
    /// <param name="pageNumber">Page number (default: 1, minimum: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 40, minimum: 1, maximum: 100)</param>
    /// <param name="includeVariableGroups">Include variable groups in the response (default: true)</param>
    /// <param name="sortBy">Sort field: deploymentFinishTime or buildStartTime (default: deploymentFinishTime)</param>
    /// <param name="sortOrder">Sort order: asc or desc (default: desc)</param>
    /// <returns>Paged list of environment reports with specified sorting</returns>
    /// <response code="200">Returns the paged list of environment reports</response>
    /// <response code="400">Bad request - invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedEnvironmentReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedEnvironmentReportResponse>> GetEnvironmentReports(
        [FromQuery] string? environmentName = null,
        [FromQuery] string? result = "succeeded",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 40,
        [FromQuery] bool includeVariableGroups = true,
        [FromQuery] string sortBy = "deploymentFinishTime",
        [FromQuery] string sortOrder = "desc")
    {
        try
        {
            // Validate paging parameters
            if (pageNumber < 1)
            {
                return BadRequest("Page number must be 1 or greater.");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Page size must be between 1 and 100.");
            }

            // Validate sort parameters
            var validSortFields = new[] { "deploymentfinishtime", "buildstarttime" };
            if (!validSortFields.Contains(sortBy.ToLowerInvariant()))
            {
                return BadRequest($"Invalid sortBy value. Valid values are: deploymentFinishTime, buildStartTime");
            }

            var validSortOrders = new[] { "asc", "desc" };
            if (!validSortOrders.Contains(sortOrder.ToLowerInvariant()))
            {
                return BadRequest("Invalid sortOrder value. Valid values are: asc, desc");
            }

            _logger.LogInformation(
                "Getting environment reports with filters - Environment: {EnvironmentName}, Result: {Result}, " +
                "Page: {PageNumber}, PageSize: {PageSize}, IncludeVariableGroups: {IncludeVariableGroups}, " +
                "SortBy: {SortBy}, SortOrder: {SortOrder}", 
                environmentName, result, pageNumber, pageSize, includeVariableGroups, sortBy, sortOrder);

            var response = await _azureDevOpsService.GetEnvironmentReportsAsync(
                environmentName, null, result, pageNumber, pageSize, includeVariableGroups, sortBy, sortOrder);

            _logger.LogInformation(
                "Successfully retrieved {Count} environment reports (Page {PageNumber}/{TotalPages})", 
                response.Data.Count, response.PageNumber, response.TotalPages);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting environment reports");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred while processing your request");
        }
    }
}