using System.Text.Json;
using Monarch.Models;

// Slack Admin Service for Monarch
// Manages Slack webhook configuration, channel mappings, and message templates
// Channels are loaded from the Docker secret (slack_webhooks.json)
// App mappings and templates are persisted to a local JSON config file

namespace Monarch.Services
{
    /// <summary>
    /// Service for managing Slack admin configuration from the Monarch UI.
    /// Channels are derived from the Docker secret containing webhook URLs.
    /// </summary>
    public class SlackAdminService
    {
        private readonly string _configPath;
        private readonly string _secretPath;
        private SlackAdminConfig _config;

        public SlackAdminService(string configPath = "slack-admin-config.json", string secretPath = "/run/secrets/monarch_slack_webhooks")
        {
            _configPath = configPath;
            _secretPath = secretPath;
            _config = LoadConfig();
            LoadChannelsFromSecret();
        }

        private SlackAdminConfig LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<SlackAdminConfig>(json) ?? new SlackAdminConfig();
            }
            return new SlackAdminConfig();
        }

        private void SaveConfig()
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }

        /// <summary>
        /// Loads Slack channels from the Docker secret (slack_webhooks.json).
        /// Each key in the JSON becomes a channel, named after the key.
        /// </summary>
        private void LoadChannelsFromSecret()
        {
            try
            {
                if (!File.Exists(_secretPath))
                {
                    Console.WriteLine($"Slack webhooks secret not found at {_secretPath}. No channels will be available.");
                    return;
                }

                var json = File.ReadAllText(_secretPath).Trim();
                var webhooks = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (webhooks == null || !webhooks.Any())
                {
                    Console.WriteLine("Slack webhooks secret is empty or invalid.");
                    return;
                }

                _config.Channels.Clear();
                bool isFirst = true;

                foreach (var kvp in webhooks)
                {
                    _config.Channels.Add(new SlackChannel
                    {
                        Key = kvp.Key,
                        ChannelName = kvp.Key,
                        WebhookUrl = kvp.Value,
                        IsDefault = isFirst
                    });
                    isFirst = false;
                }

                Console.WriteLine($"Loaded {webhooks.Count} Slack channel(s) from secret: {string.Join(", ", webhooks.Keys)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load Slack webhooks from secret: {ex.Message}");
            }
        }

        // ===== Channel Management =====

        public List<SlackChannel> GetChannels() => _config.Channels;

        public void AddChannel(SlackChannel channel)
        {
            _config.Channels.Add(channel);
            SaveConfig();
        }

        public void UpdateChannel(string key, SlackChannel updated)
        {
            var index = _config.Channels.FindIndex(c => c.Key == key);
            if (index >= 0)
            {
                _config.Channels[index] = updated;
                SaveConfig();
            }
        }

        public void RemoveChannel(string key)
        {
            _config.Channels.RemoveAll(c => c.Key == key);
            SaveConfig();
        }

        // ===== App-to-Channel Mappings =====

        public List<AppChannelMapping> GetAppMappings() => _config.AppMappings;

        public void AddAppMapping(AppChannelMapping mapping)
        {
            _config.AppMappings.Add(mapping);
            SaveConfig();
        }

        public void UpdateAppMapping(string appName, AppChannelMapping updated)
        {
            var index = _config.AppMappings.FindIndex(m => m.AppName == appName);
            if (index >= 0)
            {
                _config.AppMappings[index] = updated;
                SaveConfig();
            }
        }

        public void RemoveAppMapping(string appName)
        {
            _config.AppMappings.RemoveAll(m => m.AppName == appName);
            SaveConfig();
        }

        // ===== Message Templates =====

        public List<MessageTemplate> GetTemplates() => _config.Templates;

        public void AddTemplate(MessageTemplate template)
        {
            template.Id = Guid.NewGuid().ToString();
            template.CreatedAt = DateTime.UtcNow;
            _config.Templates.Add(template);
            SaveConfig();
        }

        public void UpdateTemplate(string id, MessageTemplate updated)
        {
            var index = _config.Templates.FindIndex(t => t.Id == id);
            if (index >= 0)
            {
                updated.UpdatedAt = DateTime.UtcNow;
                _config.Templates[index] = updated;
                SaveConfig();
            }
        }

        public void RemoveTemplate(string id)
        {
            _config.Templates.RemoveAll(t => t.Id == id);
            SaveConfig();
        }

        public MessageTemplate? GetTemplateById(string id)
        {
            return _config.Templates.FirstOrDefault(t => t.Id == id);
        }

        // ===== Webhook Notifications =====

        /// <summary>
        /// Sends a notification message to a Slack channel via its webhook URL.
        /// Used to notify channels when they are added to an app configuration.
        /// </summary>
        public async Task SendChannelNotificationAsync(string channelKey, string message)
        {
            try
            {
                var channel = _config.Channels.FirstOrDefault(c => c.Key == channelKey);
                if (channel == null || string.IsNullOrEmpty(channel.WebhookUrl))
                {
                    Console.WriteLine($"Cannot send notification: Channel '{channelKey}' not found or has no webhook URL.");
                    return;
                }

                using var httpClient = new HttpClient();
                var payload = new { text = message };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(channel.WebhookUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Slack notification sent to '{channelKey}': {message}");
                }
                else
                {
                    Console.WriteLine($"Failed to send Slack notification to '{channelKey}': {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Slack notification to '{channelKey}': {ex.Message}");
            }
        }
    }
}
