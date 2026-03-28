using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers
{
    /// <summary>
    /// Health check endpoint for monitoring and dashboard
    /// </summary>
    [ApiController]
    [Route("notifications")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new 
            {
                service = "notification-service",
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
