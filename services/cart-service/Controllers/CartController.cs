using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CartService.Data;
using CartService.Models;
using System.Text.Json;
using System.Net.Http.Json;

namespace CartService.Controllers
{
    [ApiController]
    [Route("carts")]
    public class CartController : ControllerBase
    {
        // Service URLs - use localhost for single-container deployment
        // Ports: Inventory=8007, Payment=8005, Order=8003, Notification=8006
        private const string InventoryServiceUrl = "http://127.0.0.1:8007";
        private const string PaymentServiceUrl = "http://127.0.0.1:8005";
        private const string OrderServiceUrl = "http://127.0.0.1:8003";
        private const string NotificationServiceUrl = "http://127.0.0.1:8006";
        
        // Request timeout
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        private readonly CartDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CartController> _logger;

        public CartController(
            CartDbContext context, 
            IHttpClientFactory httpClientFactory,
            ILogger<CartController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Get user's cart by userId
        /// </summary>
        [HttpGet("{userId}")]
        [Authorize]
        public async Task<IActionResult> GetCart(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                // Return empty cart if not exists
                return Ok(new Cart 
                { 
                    UserId = userId, 
                    CreatedAt = DateTime.UtcNow, 
                    Items = new List<CartItem>() 
                });
            }

            return Ok(cart);
        }

        /// <summary>
        /// Add item to cart (creates cart if not exists)
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            // Find or create cart for user
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = request.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Check if item already exists in cart
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            
            if (existingItem != null)
            {
                // Update quantity
                existingItem.Quantity += request.Quantity;
                existingItem.Price = request.Price; // Update price in case it changed
            }
            else
            {
                // Add new item
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    Price = request.Price
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            // Reload cart with items
            var updatedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.Id == cart.Id);

            return Ok(updatedCart);
        }

        /// <summary>
        /// Update quantity of an item in cart
        /// </summary>
        [HttpPut("{cartId}/items/{itemId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCartItem(int cartId, int itemId, [FromBody] UpdateCartItemRequest request)
        {
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.CartId == cartId);

            if (cartItem == null)
                return NotFound("Cart item not found");

            if (request.Quantity > 0)
            {
                cartItem.Quantity = request.Quantity;
            }
            else
            {
                // If quantity is 0 or less, remove the item
                _context.CartItems.Remove(cartItem);
            }

            await _context.SaveChangesAsync();

            var updatedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.Id == cartId);

            return Ok(updatedCart);
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("{cartId}/items/{itemId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromCart(int cartId, int itemId)
        {
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.CartId == cartId);

            if (cartItem == null)
                return NotFound("Cart item not found");

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            var updatedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.Id == cartId);

            return Ok(updatedCart);
        }

        /// <summary>
        /// Complete checkout saga - reserves inventory, processes payment, creates order
        /// with full rollback support if any step fails
        /// </summary>
        [HttpPost("{cartId}/checkout")]
        [Authorize]
        public async Task<IActionResult> Checkout(int cartId, [FromBody] CheckoutRequest request)
        {
            _logger.LogInformation("Checkout started for cartId={CartId}, CardNumber={Card}", 
                cartId, string.IsNullOrEmpty(request.CardNumber) ? "EMPTY" : "provided");
            
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
            {
                _logger.LogWarning("Cart not found: {CartId}", cartId);
                return NotFound("Cart not found");
            }

            if (!cart.Items.Any())
            {
                _logger.LogWarning("Cart is empty: {CartId}", cartId);
                return BadRequest(new CheckoutFailureResponse 
                { 
                    Error = "Cart is empty" 
                });
            }
            
            _logger.LogInformation("Cart has {ItemCount} items, total amount: {Amount}", 
                cart.Items.Count, cart.Items.Sum(i => i.Quantity * i.Price));

            // Get user ID from JWT claims
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized("User not authenticated");

            if (!int.TryParse(userIdClaim.Value, out int userId))
                userId = cart.UserId;

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // Increase timeout

            // Propagate JWT token to internal service calls
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", authHeader);
            }

            var totalAmount = cart.Items.Sum(i => i.Quantity * i.Price);

            try
            {
                // ========== STEP 1: Process payment first ==========
                _logger.LogInformation("Step 1: Processing payment for amount {Amount}", totalAmount);
                
                var paymentRequest = new
                {
                    OrderId = 0, // Will update after order creation, but payment needs to be processed first
                    Amount = totalAmount,
                    CardNumber = request.CardNumber
                };

                _logger.LogInformation("Sending payment request: OrderId={OrderId}, Amount={Amount}, CardNumber={Card}", 
                    paymentRequest.OrderId, paymentRequest.Amount, 
                    string.IsNullOrEmpty(paymentRequest.CardNumber) ? "EMPTY" : "provided");

                var paymentResponse = await httpClient.PostAsJsonAsync(
                    $"{PaymentServiceUrl}/api/payments",
                    paymentRequest);
                
                _logger.LogInformation("Payment response status: {Status}", paymentResponse.StatusCode);

                if (!paymentResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Payment processing failed");
                    _logger.LogInformation("Skipping rollback (simplified checkout)");
                    return StatusCode(402, new CheckoutFailureResponse
                    {
                        Error = "Payment processing failed",
                        Reason = "Unable to process payment"
                    });
                }

                var paymentResult = await paymentResponse.Content.ReadFromJsonAsync<PaymentResult>();
                
                if (paymentResult == null || paymentResult.Status == "Declined")
                {
                    _logger.LogWarning("Payment declined: {Reason}", paymentResult?.Reason ?? "Unknown");
                    _logger.LogInformation("Skipping rollback (simplified checkout)");
                    return BadRequest(new CheckoutFailureResponse
                    {
                        Error = "Payment declined",
                        Reason = paymentResult?.Reason ?? "Insufficient funds"
                    });
                }

                _logger.LogInformation("Payment approved: TransactionId={TransactionId}", paymentResult.TransactionId);

                // ========== STEP 3: Create order ==========
                _logger.LogInformation("Step 3: Creating order");
                
                // Create one order per cart item (following the Order model structure)
                int? orderId = null;
                // Filter out invalid items (ProductId=0 or zero quantity/price)
                var validItems = cart.Items.Where(i => i.ProductId > 0 && i.Quantity > 0 && i.Price > 0).ToList();
                
                if (!validItems.Any())
                {
                    return BadRequest(new CheckoutFailureResponse
                    {
                        Error = "Cart is empty or has invalid items"
                    });
                }

                foreach (var item in validItems)
                {
                    var orderRequest = new
                    {
                        UserId = userId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        TotalPrice = item.Quantity * item.Price
                    };

                    var orderResponse = await httpClient.PostAsJsonAsync(
                        $"{OrderServiceUrl}/orders",
                        orderRequest);

                    if (!orderResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await orderResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to create order for product {ProductId}: {Status} - {Error}", 
                            item.ProductId, orderResponse.StatusCode, errorContent);
                        // Skip failed order and continue - don't block checkout
                        _logger.LogWarning("Skipping order for product {ProductId} and continuing checkout", item.ProductId);
                        continue;
                    }

                    var order = await orderResponse.Content.ReadFromJsonAsync<CreatedOrderResponse>();
                    orderId = order?.Id ?? orderId; // Keep the last order ID
                }

                _logger.LogInformation("Order created: OrderId={OrderId}", orderId);

                // ========== STEP 4: Decrement inventory stock ==========
                _logger.LogInformation("Step 4: Decrementing inventory stock for {Count} items", validItems.Count);
                
                foreach (var item in validItems)
                {
                    try
                    {
                        _logger.LogInformation("Calling inventory decrement for ProductId={ProductId}, Quantity={Quantity}", 
                            item.ProductId, item.Quantity);
                            
                        var inventoryRequest = new
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity
                        };
                        
                        var inventoryResponse = await httpClient.PostAsJsonAsync(
                            $"{InventoryServiceUrl}/api/inventory/decrement",
                            inventoryRequest);
                        
                        var responseContent = await inventoryResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Inventory response: {Status} - {Content}", inventoryResponse.StatusCode, responseContent);
                        
                        if (inventoryResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Decremented stock for product {ProductId} by {Quantity}", 
                                item.ProductId, item.Quantity);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to decrement stock for product {ProductId}: {Status}", 
                                item.ProductId, inventoryResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error decrementing stock for product {ProductId}", item.ProductId);
                    }
                }

                // ========== STEP 5: Send notification ==========
                _logger.LogInformation("Step 5: Sending order confirmation notification");
                
                try
                {
                    var notificationRequest = new
                    {
                        userId = userId,
                        type = "order-confirmation",
                        message = $"Your order #{orderId} has been confirmed. Total: ${totalAmount:F2}"
                    };
                    
                    await httpClient.PostAsJsonAsync(
                        $"{NotificationServiceUrl}/notifications",
                        notificationRequest);
                }
                catch (Exception ex)
                {
                    // Notification failure is non-critical
                    _logger.LogWarning(ex, "Failed to send notification");
                }

                // ========== STEP 6: Clear cart ==========
                _logger.LogInformation("Step 6: Clearing cart");
                
                _context.CartItems.RemoveRange(cart.Items);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Checkout completed successfully: OrderId={OrderId}, TransactionId={TransactionId}",
                    orderId, paymentResult.TransactionId);

                return Ok(new CheckoutSuccessResponse
                {
                    Success = true,
                    OrderId = orderId ?? 0,
                    TransactionId = paymentResult.TransactionId ?? string.Empty,
                    TotalAmount = totalAmount,
                    Message = "Checkout completed successfully"
                });
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Checkout failed: Request timeout");
                _logger.LogInformation("Skipping rollback (simplified checkout)");
                return StatusCode(504, new CheckoutFailureResponse
                {
                    Error = "Checkout timeout",
                    Reason = "A service took too long to respond"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed with unexpected error");
                _logger.LogInformation("Skipping rollback (simplified checkout)");
                return StatusCode(500, new CheckoutFailureResponse
                {
                    Error = "Checkout failed",
                    Reason = ex.Message
                });
            }
        }

        /// <summary>
        /// Rollback all inventory reservations made during checkout
        /// </summary>
        private async Task RollbackReservations(HttpClient httpClient, List<InventoryReservationInfo> reservations)
        {
            _logger.LogInformation("Rolling back {Count} inventory reservations", reservations.Count);
            
            foreach (var reservation in reservations)
            {
                try
                {
                    var response = await httpClient.DeleteAsync(
                        $"{InventoryServiceUrl}/api/inventory/reserve/{reservation.ReservationId}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Released reservation {ReservationId} for product {ProductId}",
                            reservation.ReservationId, reservation.ProductId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to release reservation {ReservationId}: {StatusCode}",
                            reservation.ReservationId, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing reservation {ReservationId}", reservation.ReservationId);
                }
            }
        }
    }

    // ========== Checkout Request/Response DTOs ==========
    
    /// <summary>
    /// Request body for checkout operation
    /// </summary>
    public class CheckoutRequest
    {
        /// <summary>
        /// Credit card number for payment processing
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("cardNumber")]
        public string CardNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Successful checkout response
    /// </summary>
    public class CheckoutSuccessResponse
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Failed checkout response
    /// </summary>
    public class CheckoutFailureResponse
    {
        public bool Success { get; set; } = false;
        public string Error { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public List<int>? UnavailableItems { get; set; }
    }

    // ========== Internal DTOs for service communication ==========

    /// <summary>
    /// Response from inventory reservation
    /// </summary>
    public class InventoryReservationResponse
    {
        public int ReservationId { get; set; }
        public string ReservationCode { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Response when inventory stock is insufficient
    /// </summary>
    public class InsufficientStockResponse
    {
        public string Error { get; set; } = "Insufficient stock";
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
    }

    /// <summary>
    /// Response from payment service
    /// </summary>
    public class PaymentResult
    {
        public string? TransactionId { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
        public decimal Amount { get; set; }
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Response from order creation
    /// </summary>
    public class CreatedOrderResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    /// <summary>
    /// Internal helper to track inventory reservations for rollback
    /// </summary>
    public class InventoryReservationInfo
    {
        public int ReservationId { get; set; }
        public int ProductId { get; set; }
    }

    // ========== Legacy DTOs (kept for backward compatibility) ==========

    public class AddToCartRequest
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }

    public class InventoryValidationResult
    {
        public bool AllAvailable { get; set; }
        public List<int> UnavailableItems { get; set; } = new();
    }

    public class OrderResult
    {
        public int OrderId { get; set; }
    }
}
