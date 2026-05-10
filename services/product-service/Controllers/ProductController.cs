using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Models;

namespace ProductService.Controllers
{
    [ApiController]
    [Route("products")]
    public class ProductController : ControllerBase
    {
        private readonly ProductDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductController(ProductDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts()
        {
            return Ok(await _context.Products.ToListAsync());
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");
            return Ok(product);
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product productUpdated)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");

            var stockChanged = product.Stock != productUpdated.Stock;

            product.Name = productUpdated.Name;
            product.Price = productUpdated.Price;
            product.Stock = productUpdated.Stock;

            await _context.SaveChangesAsync();

            // Si cambió el stock, sincronizar con Inventory Service
            if (stockChanged)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // Actualizar el inventario directamente con el nuevo stock total
                    await client.PutAsJsonAsync($"http://127.0.0.1:8007/api/inventory/{id}/sync", 
                        new { stock = productUpdated.Stock });
                }
                catch
                {
                    // Silencioso - si falla, el inventario se sincroniza en checkout
                }
            }

            return Ok(product);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            
            // Avisar al CartService que elimine el producto de todos los carritos
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                await client.DeleteAsync($"http://127.0.0.1:8004/carts/products/{id}");
            }
            catch (Exception)
            {
                // Silencioso. Si el CartService no responde, igual borramos el producto.
            }

            return NoContent();
        }

        /// <summary>
        /// Update stock by delta (positive = add, negative = subtract)
        /// PUT /products/{id}/stock
        /// </summary>
        [HttpPut("{id}/stock")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] StockUpdateRequest request)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");

            // If delta is provided, add it to current stock
            if (request.Delta.HasValue)
            {
                product.Stock += request.Delta.Value;
            }
            // If newStock is provided, set it directly
            else if (request.NewStock.HasValue)
            {
                product.Stock = request.NewStock.Value;
            }
            // Otherwise just return current stock
            else
            {
                return Ok(product);
            }

            if (product.Stock < 0)
                product.Stock = 0;

            await _context.SaveChangesAsync();
            return Ok(product);
        }
        [HttpPost("seed")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SeedProducts()
        {
            if (await _context.Products.AnyAsync())
                return BadRequest("Database already has products.");

            var products = new List<Product>
            {
                new Product { Name = "Mechanical Keyboard RGB", Price = 89.99m, Stock = 50, Description = "Professional mechanical keyboard with blue switches." },
                new Product { Name = "Gaming Mouse 12000 DPI", Price = 45.50m, Stock = 100, Description = "Ergonomic gaming mouse." },
                new Product { Name = "UltraWide Monitor 34\"", Price = 450.00m, Stock = 15, Description = "Curved monitor for productivity." },
                new Product { Name = "Wireless Headphones", Price = 120.00m, Stock = 30, Description = "Noise-cancelling headphones." },
                new Product { Name = "USB-C Hub 7-in-1", Price = 35.00m, Stock = 200, Description = "Aluminum hub with HDMI." },
                new Product { Name = "Developer Hoodie", Price = 55.00m, Stock = 40, Description = "Premium cotton hoodie." },
                new Product { Name = "Standing Desk", Price = 320.00m, Stock = 10, Description = "Electric height-adjustable desk." }
            };

            _context.Products.AddRange(products);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Database seeded successfully", Count = products.Count });
        }
    }

    public class StockUpdateRequest
    {
        public int? Delta { get; set; }      // Add/subtract from current stock
        public int? NewStock { get; set; }   // Set directly
    }
}
