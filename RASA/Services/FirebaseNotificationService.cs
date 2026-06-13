using FirebaseAdmin.Messaging;

namespace RasaApi.Services
{
    public class FirebaseNotificationService
    {
        public async Task SendFallAlertNotificationAsync(
            List<string> fcmTokens,
            string elderlyName
        )
        {
            if (fcmTokens == null || fcmTokens.Count == 0)
            {
                return;
            }

            var message = new MulticastMessage
            {
                Tokens = fcmTokens,
                Notification = new Notification
                {
                    Title = "Peringatan Darurat RASA",
                    Body = $"{elderlyName} terindikasi jatuh. Segera periksa kondisi lansia."
                },
                Data = new Dictionary<string, string>
                {
                    { "type", "fall_detected" },
                    { "riskLevel", "darurat" },
                    { "elderlyName", elderlyName }
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "rasa_alert_channel",
                        Priority = NotificationPriority.HIGH,
                        Sound = "default"
                    }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        }
    }
}