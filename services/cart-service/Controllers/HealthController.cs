using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Controllers
{
    /// <summary>
    /// Health check endpoint for monitoring and dashboard
    /// </summary>
    [ApiController]
    [Route("carts")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new 
            {
                service = "cart-service",
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
