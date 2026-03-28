namespace PaymentService.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string? CardNumber { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
