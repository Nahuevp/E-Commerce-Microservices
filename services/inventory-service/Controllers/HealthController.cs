using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers
{
    /// <summary>
    /// Health check endpoint for monitoring and dashboard
    /// </summary>
    [ApiController]
    [Route("inventory")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new 
            {
                service = "inventory-service",
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
