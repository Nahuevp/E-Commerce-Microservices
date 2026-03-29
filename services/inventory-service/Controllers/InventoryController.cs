using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Models;
using InventoryService.Models.DTOs;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private const int ReservationTtlMinutes = 15;
        
        public InventoryController(InventoryDbContext context, ILogger<InventoryController> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        /// <summary>
        /// Check product availability
        /// GET /api/inventory/{productId}/availability
        /// </summary>
        [HttpGet("{productId}/availability")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckAvailability(int productId)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == productId);
                
            // Auto-initialize inventory if not exists
            if (inventory == null)
            {
                _logger.LogInformation("Auto-initializing inventory for product {ProductId}", productId);
                inventory = new Inventory
                {
                    ProductId = productId,
                    AvailableStock = 100,
                    ReservedStock = 0,
                    TotalStock = 100
                };
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
            }
            
            return Ok(new AvailabilityResponse
            {
                ProductId = inventory.ProductId,
                AvailableStock = inventory.AvailableStock,
                ReservedStock = inventory.ReservedStock,
                TotalStock = inventory.TotalStock
            });
        }
        
        /// <summary>
        /// Reserve stock for a product
        /// POST /api/inventory/reserve
        /// </summary>
        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> ReserveStock([FromBody] ReserveStockRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            // Find inventory for product
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
                
            // Auto-initialize inventory if not exists (for convenience)
            if (inventory == null)
            {
                _logger.LogInformation("Auto-initializing inventory for product {ProductId}", request.ProductId);
                inventory = new Inventory
                {
                    ProductId = request.ProductId,
                    AvailableStock = 100, // Default stock
                    ReservedStock = 0,
                    TotalStock = 100
                };
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
            }
            
            // Check if sufficient stock is available
            if (inventory.AvailableStock < request.Quantity)
            {
                _logger.LogWarning("Not enough stock for product {ProductId}. Available: {Available}, Needed: {Needed}", 
                    request.ProductId, inventory.AvailableStock, request.Quantity);
                
                return BadRequest(new { 
                    error = "Not enough stock",
                    available = inventory.AvailableStock,
                    requested = request.Quantity
                });
            }
            
            // Use transaction to ensure atomicity
            await using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Decrement available stock
                inventory.AvailableStock -= request.Quantity;
                inventory.ReservedStock += request.Quantity;
                
                // Create reservation
                var reservation = new Reservation
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    ReservationCode = GenerateReservationCode(),
                    Status = ReservationStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(ReservationTtlMinutes)
                };
                
                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "Stock reserved: ReservationId={ReservationId}, ProductId={ProductId}, Quantity={Quantity}",
                    reservation.Id, request.ProductId, request.Quantity);
                
                return Ok(new ReservationResponse
                {
                    ReservationId = reservation.Id,
                    ReservationCode = reservation.ReservationCode,
                    ProductId = reservation.ProductId,
                    Quantity = reservation.Quantity,
                    Status = reservation.Status,
                    CreatedAt = reservation.CreatedAt,
                    ExpiresAt = reservation.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to reserve stock for ProductId={ProductId}", request.ProductId);
                return StatusCode(500, new { error = "Failed to reserve stock" });
            }
        }
        
        /// <summary>
        /// Release a reservation (rollback)
        /// DELETE /api/inventory/reserve/{id}
        /// </summary>
        [HttpDelete("reserve/{id}")]
        [Authorize]
        public async Task<IActionResult> ReleaseReservation(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Inventory)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (reservation == null)
            {
                return NotFound(new { error = "Reservation not found", reservationId = id });
            }
            
            // Can only release Active reservations
            if (reservation.Status != ReservationStatus.Active)
            {
                return BadRequest(new 
                { 
                    error = "Reservation cannot be released",
                    currentStatus = reservation.Status
                });
            }
            
            await using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Return stock to available
                var inventory = reservation.Inventory;
                if (inventory != null)
                {
                    inventory.AvailableStock += reservation.Quantity;
                    inventory.ReservedStock -= reservation.Quantity;
                }
                
                // Mark reservation as cancelled
                reservation.Status = ReservationStatus.Cancelled;
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "Reservation released: ReservationId={ReservationId}, ProductId={ProductId}, Quantity={Quantity}",
                    reservation.Id, reservation.ProductId, reservation.Quantity);
                
                return Ok(new MessageResponse 
                { 
                    Message = $"Reservation {reservation.ReservationCode} has been cancelled. Stock returned to available." 
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to release reservation {ReservationId}", id);
                return StatusCode(500, new { error = "Failed to release reservation" });
            }
        }
        
        /// <summary>
        /// Confirm a reservation (checkout complete)
        /// POST /api/inventory/confirm/{id}
        /// </summary>
        [HttpPost("confirm/{id}")]
        [Authorize]
        public async Task<IActionResult> ConfirmReservation(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Inventory)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (reservation == null)
            {
                return NotFound(new { error = "Reservation not found", reservationId = id });
            }
            
            // Check if reservation is still active
            if (reservation.Status != ReservationStatus.Active)
            {
                return BadRequest(new 
                { 
                    error = "Reservation cannot be confirmed",
                    currentStatus = reservation.Status
                });
            }
            
            // Check if reservation has expired
            if (DateTime.UtcNow > reservation.ExpiresAt)
            {
                // Auto-expire the reservation
                await ExpireReservation(reservation);
                return BadRequest(new 
                { 
                    error = "Reservation has expired",
                    reservationId = id
                });
            }
            
            await using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var inventory = reservation.Inventory;
                if (inventory != null)
                {
                    // Move from reserved to "sold" - permanently decrement reserved stock
                    // AvailableStock stays the same (it was already decremented at reservation time)
                    inventory.ReservedStock -= reservation.Quantity;
                    // TotalStock also decreases by the sold quantity
                    inventory.TotalStock -= reservation.Quantity;
                }
                
                // Mark reservation as confirmed
                reservation.Status = ReservationStatus.Confirmed;
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "Reservation confirmed: ReservationId={ReservationId}, ProductId={ProductId}, Quantity={Quantity}",
                    reservation.Id, reservation.ProductId, reservation.Quantity);
                
                return Ok(new MessageResponse 
                { 
                    Message = $"Reservation {reservation.ReservationCode} confirmed. Stock permanently decremented." 
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to confirm reservation {ReservationId}", id);
                return StatusCode(500, new { error = "Failed to confirm reservation" });
            }
        }
        
        /// <summary>
        /// Get reservation by ID
        /// GET /api/inventory/reserve/{id}
        /// </summary>
        [HttpGet("reserve/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReservation(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Inventory)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (reservation == null)
            {
                return NotFound(new { error = "Reservation not found", reservationId = id });
            }
            
            return Ok(new ReservationResponse
            {
                ReservationId = reservation.Id,
                ReservationCode = reservation.ReservationCode,
                ProductId = reservation.ProductId,
                Quantity = reservation.Quantity,
                Status = reservation.Status,
                CreatedAt = reservation.CreatedAt,
                ExpiresAt = reservation.ExpiresAt
            });
        }
        
        // ========== Legacy endpoints for backward compatibility ==========
        
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetInventory()
        {
            return Ok(await _context.Inventories.ToListAsync());
        }

        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInventoryByProduct(int productId)
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
            if (inventory == null) return NotFound("Inventory not found");
            return Ok(inventory);
        }

        [HttpPost("initialize")]
        [Authorize]
        public async Task<IActionResult> CreateInventory([FromBody] Inventory inventory)
        {
            var existing = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == inventory.ProductId);
            if (existing != null)
            {
                return Conflict(new { error = "Inventory already exists for this product", productId = inventory.ProductId });
            }
            
            inventory.AvailableStock = inventory.AvailableStock;
            inventory.ReservedStock = 0;
            inventory.TotalStock = inventory.AvailableStock; // Initial total equals available
            
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();
            return Ok(inventory);
        }

        [HttpPut("{productId}")]
        [Authorize]
        public async Task<IActionResult> UpdateInventory(int productId, [FromBody] UpdateInventoryRequest request)
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
            if (inventory == null) return NotFound("Inventory not found");

            // Update stocks while maintaining invariant
            var newAvailable = request.AvailableStock ?? inventory.AvailableStock;
            var newReserved = request.ReservedStock ?? inventory.ReservedStock;
            var newTotal = request.TotalStock ?? inventory.TotalStock;
            
            // Validate new values
            if (newAvailable < 0 || newReserved < 0 || newTotal < 0)
            {
                return BadRequest("Stock values cannot be negative");
            }
            
            if (newAvailable + newReserved != newTotal)
            {
                return BadRequest($"Invalid stock state: AvailableStock ({newAvailable}) + ReservedStock ({newReserved}) must equal TotalStock ({newTotal})");
            }
            
            inventory.AvailableStock = newAvailable;
            inventory.ReservedStock = newReserved;
            inventory.TotalStock = newTotal;

            await _context.SaveChangesAsync();
            return Ok(inventory);
        }
        
        // ========== Private helper methods ==========
        
        private static string GenerateReservationCode()
        {
            // Generate a unique reservation code like "RES-ABC123"
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var code = new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return $"RES-{code}";
        }
        
        private async Task ExpireReservation(Reservation reservation)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Return stock to available
                var inventory = reservation.Inventory;
                if (inventory != null)
                {
                    inventory.AvailableStock += reservation.Quantity;
                    inventory.ReservedStock -= reservation.Quantity;
                }
                
                reservation.Status = ReservationStatus.Expired;
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation(
                    "Reservation expired: ReservationId={ReservationId}, ProductId={ProductId}, Quantity={Quantity}",
                    reservation.Id, reservation.ProductId, reservation.Quantity);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to expire reservation {ReservationId}", reservation.Id);
            }
        }

        /// <summary>
        /// Decrement stock after purchase - no auth needed for internal calls
        /// POST /api/inventory/decrement
        /// </summary>
        [HttpPost("decrement")]
        [AllowAnonymous]
        public async Task<IActionResult> DecrementStock([FromBody] DecrementStockRequest request)
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
            
            if (inventory == null)
            {
                // Auto-initialize inventory if doesn't exist (allow negative for simplicity)
                _logger.LogWarning("Product {ProductId} not in inventory, creating entry with negative stock", request.ProductId);
                inventory = new Inventory
                {
                    ProductId = request.ProductId,
                    TotalStock = -request.Quantity,
                    AvailableStock = -request.Quantity,
                    ReservedStock = 0
                };
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
                
                return Ok(new { ProductId = request.ProductId, AvailableStock = inventory.AvailableStock, TotalStock = inventory.TotalStock, Created = true });
            }

            // Decrement available stock
            inventory.AvailableStock -= request.Quantity;
            inventory.TotalStock -= request.Quantity;

            if (inventory.AvailableStock < 0 || inventory.TotalStock < 0)
            {
                _logger.LogWarning("Product {ProductId} stock going negative: Available={Available}, Decrement={Quantity}", 
                    request.ProductId, inventory.AvailableStock, request.Quantity);
                // Allow negative for simplicity - don't fail checkout
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Decremented stock for product {ProductId} by {Quantity}. New available: {Available}", 
                request.ProductId, request.Quantity, inventory.AvailableStock);

            return Ok(new { ProductId = request.ProductId, AvailableStock = inventory.AvailableStock, TotalStock = inventory.TotalStock });
        }

        /// <summary>
        /// Increment stock when order is cancelled - returns stock to available
        /// POST /api/inventory/increment
        /// </summary>
        [HttpPost("increment")]
        [AllowAnonymous]
        public async Task<IActionResult> IncrementStock([FromBody] DecrementStockRequest request)
        {
            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
            
            if (inventory == null)
            {
                // Auto-initialize if doesn't exist
                inventory = new Inventory
                {
                    ProductId = request.ProductId,
                    TotalStock = request.Quantity,
                    AvailableStock = request.Quantity,
                    ReservedStock = 0
                };
                _context.Inventories.Add(inventory);
            }
            else
            {
                // Increment stock back
                inventory.AvailableStock += request.Quantity;
                inventory.TotalStock += request.Quantity;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Incremented stock for product {ProductId} by {Quantity}. New available: {Available}", 
                request.ProductId, request.Quantity, inventory.AvailableStock);

            return Ok(new { ProductId = request.ProductId, AvailableStock = inventory.AvailableStock, TotalStock = inventory.TotalStock });
        }
    }

    /// <summary>
    /// Request to decrement/increment stock
    /// </summary>
    public class DecrementStockRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
    
    public class UpdateInventoryRequest
    {
        public int? AvailableStock { get; set; }
        public int? ReservedStock { get; set; }
        public int? TotalStock { get; set; }
    }
}
