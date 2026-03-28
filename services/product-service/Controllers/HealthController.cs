using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProductService.Controllers
{
    [ApiController]
    [Route("products")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new 
            {
                service = "product-service",
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}