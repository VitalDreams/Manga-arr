using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;

namespace NzbDrone.Core.Manga.Monitoring
{
    public interface INotificationService
    {
        Task SendAsync(Notification notification);
        Task SendTestAsync(string provider);
        void Configure(NotificationSettings settings);
        NotificationSettings GetSettings();
    }

    public class NotificationService : INotificationService
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;
        private NotificationSettings _settings;

        public NotificationService(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = new NotificationSettings();
        }

        public async Task SendAsync(Notification notification)
        {
            _logger.Debug($"Sending notification: {notification.Title}");

            var tasks = new List<Task>();

            // Discord webhook
            if (!string.IsNullOrEmpty(_settings.DiscordWebhookUrl))
            {
                tasks.Add(SendDiscordAsync(notification));
            }

            // Gotify
            if (!string.IsNullOrEmpty(_settings.GotifyUrl))
            {
                tasks.Add(SendGotifyAsync(notification));
            }

            // Ntfy
            if (!string.IsNullOrEmpty(_settings.NtfyTopic))
            {
                tasks.Add(SendNtfyAsync(notification));
            }

            // Webhook (generic)
            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                tasks.Add(SendWebhookAsync(notification));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                _logger.Info($"Notification sent: {notification.Title}");
            }
            else
            {
                _logger.Debug("No notification providers configured");
            }
        }

        public async Task SendTestAsync(string provider)
        {
            var testNotification = new Notification
            {
                Title = "MangaArr Test Notification",
                Message = "This is a test notification from MangaArr",
                Type = NotificationType.Info
            };

            switch (provider.ToLower())
            {
                case "discord":
                    await SendDiscordAsync(testNotification);
                    break;
                case "gotify":
                    await SendGotifyAsync(testNotification);
                    break;
                case "ntfy":
                    await SendNtfyAsync(testNotification);
                    break;
                case "webhook":
                    await SendWebhookAsync(testNotification);
                    break;
                default:
                    throw new ArgumentException($"Unknown notification provider: {provider}");
            }
        }

        public void Configure(NotificationSettings settings)
        {
            _settings = settings;
            _logger.Info("Notification settings updated");
        }

        public NotificationSettings GetSettings() => _settings;

        private async Task SendDiscordAsync(Notification notification)
        {
            try
            {
                var embed = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = notification.Title,
                            description = notification.Message,
                            color = GetColor(notification.Type),
                            timestamp = DateTime.UtcNow.ToString("o"),
                            fields = notification.Data != null
                                ? new[] { new { name = "Details", value = JsonSerializer.Serialize(notification.Data), inline = false } }
                                : null
                        }
                    }
                };

                var json = JsonSerializer.Serialize(embed);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(new HttpRequestBuilder(_settings.DiscordWebhookUrl).Build());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send Discord notification");
            }
        }

        private async Task SendGotifyAsync(Notification notification)
        {
            try
            {
                var url = $"{_settings.GotifyUrl}/message?token={_settings.GotifyToken}";
                var payload = new
                {
                    title = notification.Title,
                    message = notification.Message,
                    priority = GetPriority(notification.Type)
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(new HttpRequestBuilder(url).Build());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send Gotify notification");
            }
        }

        private async Task SendNtfyAsync(Notification notification)
        {
            try
            {
                var url = $"https://ntfy.sh/{_settings.NtfyTopic}";
                var payload = new
                {
                    topic = _settings.NtfyTopic,
                    title = notification.Title,
                    message = notification.Message,
                    tags = GetTags(notification.Type)
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(new HttpRequestBuilder(url).Build());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send Ntfy notification");
            }
        }

        private async Task SendWebhookAsync(Notification notification)
        {
            try
            {
                var payload = new
                {
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type.ToString(),
                    data = notification.Data,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(new HttpRequestBuilder(_settings.WebhookUrl).Build());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send webhook notification");
            }
        }

        private int GetColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Download => 0x2ecc71, // Green
                NotificationType.Error => 0xe74c3c,     // Red
                NotificationType.Warning => 0xf39c12,   // Yellow
                NotificationType.Info => 0x3498db,      // Blue
                _ => 0x95a5a6                           // Gray
            };
        }

        private int GetPriority(NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => 8,
                NotificationType.Warning => 5,
                NotificationType.Download => 3,
                NotificationType.Info => 1,
                _ => 1
            };
        }

        private string[] GetTags(NotificationType type)
        {
            return type switch
            {
                NotificationType.Download => new[] { "white_check_mark", "books" },
                NotificationType.Error => new[] { "x", "warning" },
                NotificationType.Warning => new[] { "warning" },
                NotificationType.Info => new[] { "information_source" },
                _ => Array.Empty<string>()
            };
        }
    }

    public class Notification
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }
        public object Data { get; set; }
    }

    public enum NotificationType
    {
        Info,
        Download,
        Warning,
        Error
    }

    public class NotificationSettings
    {
        public string DiscordWebhookUrl { get; set; }
        public string GotifyUrl { get; set; }
        public string GotifyToken { get; set; }
        public string NtfyTopic { get; set; }
        public string WebhookUrl { get; set; }
    }
}
