// Slack integration data models for Monarch admin panel
// Used to configure webhooks, channel mappings, and message templates

namespace Monarch.Models
{
    /// <summary>
    /// Represents a Slack channel webhook configuration
    /// </summary>
    public class SlackChannel
    {
        // Unique identifier for this channel config (e.g., "alerts", "incidents")
        public string Key { get; set; } = string.Empty;

        // Display name of the Slack channel
        public string ChannelName { get; set; } = string.Empty;

        // Slack incoming webhook URL
        public string WebhookUrl { get; set; } = string.Empty;

        // Whether this is the default channel for messages
        public bool IsDefault { get; set; } = false;
    }

    /// <summary>
    /// Maps an application/service to a Slack channel
    /// </summary>
    public class AppChannelMapping
    {
        // Name of the monitored application (e.g., "NewRelic", "Nagios", "Jira")
        public string AppName { get; set; } = string.Empty;

        // Key of the target Slack channel
        public string ChannelKey { get; set; } = string.Empty;

        // Whether this mapping is active
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Represents a reusable message template for Slack notifications
    /// </summary>
    public class MessageTemplate
    {
        // Unique identifier for the template
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // User-friendly template name
        public string Name { get; set; } = string.Empty;

        // Template content with optional placeholders
        public string Content { get; set; } = string.Empty;

        // Timestamp when the template was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Timestamp when the template was last updated
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Full Slack admin configuration (for persistence)
    /// </summary>
    public class SlackAdminConfig
    {
        public List<SlackChannel> Channels { get; set; } = new();
        public List<AppChannelMapping> AppMappings { get; set; } = new();
        public List<MessageTemplate> Templates { get; set; } = new();
    }
}
