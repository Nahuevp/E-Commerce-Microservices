using System.ComponentModel.DataAnnotations;

namespace PaymentService.Models
{
    public class PaymentRequest
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [CreditCard(ErrorMessage = "Invalid card number format")]
        public string CardNumber { get; set; } = string.Empty;
    }
}
