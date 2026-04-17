using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NoteApi.Services
{
    public class FcmNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverKey;

        public FcmNotificationService(IConfiguration configuration, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _serverKey = configuration["Firebase:ServerKey"]
                ?? throw new ArgumentException("Firebase:ServerKey is missing in configuration.");
        }

        public async Task SendAsync(string fcmToken, string title, string body, object? data = null)
        {
            var payload = new
            {
                to = fcmToken,
                notification = new { title, body },
                data
            };

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://fcm.googleapis.com/fcm/send");

            request.Headers.TryAddWithoutValidation("Authorization", $"key={_serverKey}");

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            await _httpClient.SendAsync(request);
        }
    }
}
