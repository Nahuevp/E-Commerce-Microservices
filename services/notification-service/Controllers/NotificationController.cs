using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;

namespace NotificationService.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(NotificationDbContext context, ILogger<NotificationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Send order confirmation email
        /// </summary>
        [HttpPost("order-confirmation")]
        public async Task<IActionResult> SendOrderConfirmation([FromBody] OrderConfirmationRequest request)
        {
            _logger.LogInformation(
                "[{Timestamp}] Order Confirmation Event - UserId: {UserId}, OrderId: {OrderId}, Email: {Email}, Details: {Details}",
                DateTime.UtcNow, request.UserId, request.OrderId, request.Email, request.Details);

            // Simulate email sending (log + return success)
            _logger.LogInformation("[{Timestamp}] Simulated email sent to {Email} for order #{OrderId}", 
                DateTime.UtcNow, request.Email, request.OrderId);

            // Create notification record
            var notification = new Notification
            {
                UserId = request.UserId,
                Type = "order_confirmation",
                Subject = $"Order Confirmation - Order #{request.OrderId}",
                Message = $"Your order #{request.OrderId} has been confirmed. Details: {request.Details}",
                SentAt = DateTime.UtcNow,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var response = new OrderConfirmationResponse
            {
                Success = true,
                Message = "Order confirmation email sent successfully",
                NotificationId = notification.Id,
                SentAt = notification.SentAt,
                Email = request.Email
            };

            return Ok(response);
        }

        /// <summary>
        /// Send payment confirmation email
        /// </summary>
        [HttpPost("payment-confirmation")]
        public async Task<IActionResult> SendPaymentConfirmation([FromBody] PaymentConfirmationRequest request)
        {
            _logger.LogInformation(
                "[{Timestamp}] Payment Confirmation Event - UserId: {UserId}, PaymentId: {PaymentId}, Status: {Status}",
                DateTime.UtcNow, request.UserId, request.PaymentId, request.Status);

            // Simulate email sending (log + return success)
            _logger.LogInformation("[{Timestamp}] Simulated payment confirmation processed for PaymentId: {PaymentId}", 
                DateTime.UtcNow, request.PaymentId);

            // Create notification record
            var notification = new Notification
            {
                UserId = request.UserId,
                Type = "payment_confirmation",
                Subject = $"Payment Confirmation - Payment #{request.PaymentId}",
                Message = $"Your payment #{request.PaymentId} has been processed. Status: {request.Status}",
                SentAt = DateTime.UtcNow,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var response = new PaymentConfirmationResponse
            {
                Success = true,
                Message = "Payment confirmation email sent successfully",
                NotificationId = notification.Id,
                SentAt = notification.SentAt
            };

            return Ok(response);
        }

        /// <summary>
        /// Get notification history for the authenticated user
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetNotificationHistory()
        {
            // Extract UserId from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) 
                ?? User.FindFirst("sub") 
                ?? User.FindFirst("userId");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { error = "Invalid user token" });
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Type = n.Type,
                    Subject = n.Subject,
                    Message = n.Message,
                    SentAt = n.SentAt,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            return Ok(notifications);
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return NotFound(new { error = "Notification not found" });

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new NotificationResponse
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Type = notification.Type,
                Subject = notification.Subject,
                Message = notification.Message,
                SentAt = notification.SentAt,
                IsRead = notification.IsRead
            });
        }
    }
}
