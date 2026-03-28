using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly PaymentDbContext _context;
        private const string DECLINE_CARD_PREFIX = "4000";

        public PaymentController(PaymentDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Process a payment for an order
        /// </summary>
        /// <param name="request">Payment request with orderId, amount, and cardNumber</param>
        /// <returns>Payment response with transactionId, status, and details</returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            // Validate amount > 0
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Amount must be greater than 0" });
            }

            // Generate unique transaction ID
            var transactionId = $"TXN-{Guid.NewGuid():N}";

            // Determine payment status based on card number
            // Cards starting with "4000" are declined (simulated fraud/insufficient funds)
            string status;
            string? reason;

            if (!string.IsNullOrEmpty(request.CardNumber) && 
                request.CardNumber.StartsWith(DECLINE_CARD_PREFIX))
            {
                status = "Declined";
                reason = "Insufficient funds";
            }
            else
            {
                status = "Approved";
                reason = null;
            }

            // Create payment record
            var payment = new Payment
            {
                OrderId = request.OrderId,
                TransactionId = transactionId,
                Amount = request.Amount,
                CardNumber = MaskCardNumber(request.CardNumber),
                Status = status,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Build response
            var response = new PaymentResponse
            {
                TransactionId = transactionId,
                Status = status,
                Reason = reason,
                Amount = payment.Amount,
                OrderId = payment.OrderId,
                CreatedAt = payment.CreatedAt
            };

            // Return 402 Payment Required if declined
            if (status == "Declined")
            {
                return StatusCode(402, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get payment status by transaction ID or payment ID
        /// </summary>
        /// <param name="id">Transaction ID (TXN-...) or numeric payment ID</param>
        /// <returns>Payment details including status, amount, and timestamp</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetPayment(string id)
        {
            Payment? payment = null;

            // Try to find by TransactionId first (TXN-xxxxx format)
            if (id.StartsWith("TXN-", StringComparison.OrdinalIgnoreCase))
            {
                payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.TransactionId == id);
            }

            // If not found, try by numeric ID
            if (payment == null && int.TryParse(id, out var numericId))
            {
                payment = await _context.Payments.FindAsync(numericId);
            }

            if (payment == null)
            {
                return NotFound(new { error = "Payment not found" });
            }

            var response = new PaymentResponse
            {
                TransactionId = payment.TransactionId,
                Status = payment.Status,
                Reason = payment.Reason,
                Amount = payment.Amount,
                OrderId = payment.OrderId,
                CreatedAt = payment.CreatedAt
            };

            return Ok(response);
        }

        /// <summary>
        /// Get all payments (admin endpoint)
        /// </summary>
        /// <returns>List of all payments</returns>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetPayments()
        {
            var payments = await _context.Payments
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var responses = payments.Select(p => new PaymentResponse
            {
                TransactionId = p.TransactionId,
                Status = p.Status,
                Reason = p.Reason,
                Amount = p.Amount,
                OrderId = p.OrderId,
                CreatedAt = p.CreatedAt
            });

            return Ok(responses);
        }

        /// <summary>
        /// Mask card number for security (show only last 4 digits)
        /// </summary>
        private static string MaskCardNumber(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            {
                return "****";
            }

            var lastFour = cardNumber[^4..];
            return $"****{lastFour}";
        }
    }
}
