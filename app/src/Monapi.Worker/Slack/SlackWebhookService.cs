using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Slack webhook service for sending messages to appropriate Slack channels via webhooks

namespace Monapi.Worker.Slack
{
    public class SlackWebhookService
    {
        private readonly Dictionary<string, string> _webhooks;
        private readonly HttpClient _httpClient;

        public SlackWebhookService(string configPath)
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Slack webhook config not found: {configPath}");

            var json = File.ReadAllText(configPath);
            _webhooks = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? throw new Exception("Invalid Slack webhook config format.");
            _httpClient = new HttpClient();
        }

        public async Task SendMessageAsync(string message, string webhookKey = "default")
        {
            if (!_webhooks.TryGetValue(webhookKey, out var webhookUrl))
                throw new ArgumentException($"Webhook key '{webhookKey}' not found in config.");

            var payload = new { text = message };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();
        }
    }
}
