using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using System.Net.Http.Json;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrderController : ControllerBase
    {
        private readonly OrderDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Inventory service URL
        private const string InventoryServiceUrl = "http://127.0.0.1:8007";

        public OrderController(OrderDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetOrders()
        {
            return Ok(await _context.Orders.ToListAsync());
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound("Order not found");
            return Ok(order);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] Order order)
        {
            /* In a real scenario, this service would validate stock by communicating synchronously
               with the Product Service or asynchronously via an event bus (e.g. RabbitMQ).
               For this basic architecture demo, we'll just save the order assuming product exists. */

            // Ensure required fields have values (in case they weren't sent in request)
            if (string.IsNullOrEmpty(order.Status))
                order.Status = "Pending";
            if (order.CreatedAt == default)
                order.CreatedAt = DateTime.UtcNow;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound("Order not found");

            // Return stock to inventory before deleting order
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var inventoryRequest = new
                {
                    ProductId = order.ProductId,
                    Quantity = order.Quantity
                };
                
                var inventoryResponse = await httpClient.PostAsJsonAsync(
                    $"{InventoryServiceUrl}/api/inventory/increment",
                    inventoryRequest);
                
                if (inventoryResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Stock returned for product {order.ProductId}: +{order.Quantity}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not return stock to inventory: {ex.Message}");
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
