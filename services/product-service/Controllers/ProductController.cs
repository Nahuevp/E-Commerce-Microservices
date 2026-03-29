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

        public ProductController(ProductDbContext context)
        {
            _context = context;
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
        [Authorize]
        public async Task<IActionResult> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product productUpdated)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");

            product.Name = productUpdated.Name;
            product.Price = productUpdated.Price;
            product.Stock = productUpdated.Stock;

            await _context.SaveChangesAsync();
            return Ok(product);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound("Product not found");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
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
    }

    public class StockUpdateRequest
    {
        public int? Delta { get; set; }      // Add/subtract from current stock
        public int? NewStock { get; set; }   // Set directly
    }
}
