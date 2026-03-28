namespace PaymentService.Models
{
    public class PaymentResponse
    {
        public string? TransactionId { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
        public decimal Amount { get; set; }
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
