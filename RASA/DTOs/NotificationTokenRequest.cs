namespace RasaApi.DTOs
{
    public class NotificationTokenRequest
    {
        public string FcmToken { get; set; } = string.Empty;

        public string? DeviceName { get; set; }
    }
}