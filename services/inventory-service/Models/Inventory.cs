using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryService.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [Range(0, int.MaxValue)]
        public int AvailableStock { get; set; }
        
        [Required]
        [Range(0, int.MaxValue)]
        public int ReservedStock { get; set; }
        
        [Required]
        [Range(0, int.MaxValue)]
        public int TotalStock { get; set; }
        
        // Invariant: AvailableStock + ReservedStock = TotalStock
    }
}
