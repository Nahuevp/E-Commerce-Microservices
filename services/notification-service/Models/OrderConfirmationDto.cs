namespace NotificationService.Models
{
    public class OrderConfirmationRequest
    {
        public int UserId { get; set; }
        public int OrderId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public class OrderConfirmationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int NotificationId { get; set; }
        public DateTime SentAt { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
