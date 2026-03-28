using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models.DTOs
{
    /// <summary>
    /// Request to reserve stock for a product
    /// </summary>
    public class ReserveStockRequest
    {
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
    }
    
    /// <summary>
    /// Successful reservation response
    /// </summary>
    public class ReservationResponse
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
    /// Insufficient stock error response
    /// </summary>
    public class InsufficientStockResponse
    {
        public string Error { get; set; } = "Insufficient stock";
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
    }
    
    /// <summary>
    /// Product availability response
    /// </summary>
    public class AvailabilityResponse
    {
        public int ProductId { get; set; }
        public int AvailableStock { get; set; }
        public int ReservedStock { get; set; }
        public int TotalStock { get; set; }
    }
    
    /// <summary>
    /// Generic message response
    /// </summary>
    public class MessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
