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
        
        // Service URLs
        private const string InventoryServiceUrl = "http://127.0.0.1:8007";
        private const string ProductServiceUrl = "http://127.0.0.1:8002";

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
            try
            {
                Console.WriteLine($"Creating order: UserId={order.UserId}, ProductId={order.ProductId}, Quantity={order.Quantity}, TotalPrice={order.TotalPrice}");
                
                // Ensure required fields have values (in case they weren't sent in request)
                if (string.IsNullOrEmpty(order.Status))
                    order.Status = "Pending";
                if (order.CreatedAt == default)
                    order.CreatedAt = DateTime.UtcNow;

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Order created successfully: Id={order.Id}");
                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR creating order: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound("Order not found");

            if (order.Status?.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) == true)
                return BadRequest("Order is already cancelled");

            // Return stock to inventory and sync product service before cancelling order
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                
                // 1. Return to Inventory Service
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
                    Console.WriteLine($"Stock returned to inventory for product {order.ProductId}: +{order.Quantity}");
                }

                // 2. Sync stock back to Product Service so frontend updates correctly
                try
                {
                    var stockUpdateResponse = await httpClient.PutAsJsonAsync(
                        $"{ProductServiceUrl}/products/{order.ProductId}/stock",
                        new { delta = order.Quantity }); // Positive delta to add back stock
                    
                    if (stockUpdateResponse.IsSuccessStatusCode)
                        Console.WriteLine($"Synced product stock for {order.ProductId}: +{order.Quantity}");
                    else
                        Console.WriteLine($"Failed to sync product stock for {order.ProductId}: {stockUpdateResponse.StatusCode}");
                }
                catch (Exception stockEx)
                {
                    Console.WriteLine($"Warning: Could not sync product stock for {order.ProductId}: {stockEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not return stock: {ex.Message}");
            }

            // Update status instead of hard deleting.
            // DO NOT call _context.Orders.Update(order) to avoid Npgsql DateTime tracking exceptions.
            // EF Core will automatically detect only the change to the Status field.
            order.Status = "Cancelled";
            
            try
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"Order {order.Id} successfully marked as cancelled in DB.");
            }
            catch (Exception dbEx)
            {
                Console.WriteLine($"DB Error updating order {order.Id}: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                return StatusCode(500, new { error = "Database error", message = dbEx.Message });
            }

            return NoContent();
        }
    }
}
