using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Controllers
{
    /// <summary>
    /// Health check endpoint for monitoring and dashboard
    /// </summary>
    [ApiController]
    [Route("payments")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new 
            {
                service = "payment-service",
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
