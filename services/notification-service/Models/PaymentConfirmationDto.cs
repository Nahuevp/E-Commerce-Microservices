namespace NotificationService.Models
{
    public class PaymentConfirmationRequest
    {
        public int UserId { get; set; }
        public int PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class PaymentConfirmationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int NotificationId { get; set; }
        public DateTime SentAt { get; set; }
    }
}
