namespace PaymentService.Models
{
    public class PaymentRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string? CardNumber { get; set; }
    }
}
