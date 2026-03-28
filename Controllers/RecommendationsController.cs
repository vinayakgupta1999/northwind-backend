using Microsoft.AspNetCore.Mvc;
using Northwind.Recommendations.API.Models;
using Northwind.Recommendations.API.Services;

namespace Northwind.Recommendations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _svc;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(IRecommendationService svc, ILogger<RecommendationsController> logger)
    {
        _svc    = svc;
        _logger = logger;
    }

    /// <summary>
    /// Hybrid recommendations — paged.
    /// Response: { page, pageSize, totalCount, totalPages, hasNext, hasPrev, data: [...] }
    /// </summary>
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetHybrid(
        string customerId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _svc.GetHybridRecommendationsAsync(customerId, page, pageSize);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid error for {CustomerId}", customerId);
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Collaborative filtering — paged.
    /// Response: { page, pageSize, totalCount, totalPages, hasNext, hasPrev, data: [...] }
    /// </summary>
    [HttpGet("customer/{customerId}/collaborative")]
    public async Task<IActionResult> GetCollaborative(
        string customerId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _svc.GetCollaborativeRecommendationsAsync(customerId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collaborative error for {CustomerId}", customerId);
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Content-based filtering — paged.
    /// Response: { page, pageSize, totalCount, totalPages, hasNext, hasPrev, data: [...] }
    /// </summary>
    [HttpGet("customer/{customerId}/content-based")]
    public async Task<IActionResult> GetContentBased(
        string customerId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _svc.GetContentBasedRecommendationsAsync(customerId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContentBased error for {CustomerId}", customerId);
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Trending products — paged.
    /// Response: { page, pageSize, totalCount, totalPages, hasNext, hasPrev, data: [...] }
    /// </summary>
    [HttpGet("trending")]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int days     = 90,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _svc.GetTrendingProductsAsync(days, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trending error");
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Frequently bought together — paged.
    /// Response: { page, pageSize, totalCount, totalPages, hasNext, hasPrev, data: [...] }
    /// </summary>
    [HttpGet("product/{productId}/frequently-bought-together")]
    public async Task<IActionResult> GetFbt(
        int productId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 6)
    {
        try
        {
            var result = await _svc.GetFrequentlyBoughtTogetherAsync(productId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FBT error for {ProductId}", productId);
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly IRecommendationService _svc;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(IRecommendationService svc, ILogger<CustomersController> logger)
    {
        _svc    = svc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try { return Ok(await _svc.GetCustomersAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAll customers error");
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetById(string customerId)
    {
        try
        {
            var result = await _svc.GetCustomerSummaryAsync(customerId);
            if (result == null) return NotFound(new { message = $"Customer '{customerId}' not found." });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetById error for {CustomerId}", customerId);
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}
