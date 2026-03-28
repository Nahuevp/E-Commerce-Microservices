using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryService.Models
{
    public class Reservation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ReservationCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = ReservationStatus.Active;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        
        [ForeignKey(nameof(ProductId))]
        public Inventory? Inventory { get; set; }
    }
    
    /// <summary>
    /// Reservation status constants
    /// </summary>
    public static class ReservationStatus
    {
        public const string Active = "Active";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
