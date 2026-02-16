using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monapi.Worker.Jira;

/// <summary>
/// Builds Jira incident ticket payloads and comment strings for the monitoring worker.
/// Uses strongly-typed models instead of anonymous objects for readability.
/// </summary>
public static class JiraIncidentTicket
{
    /// <summary>
    /// Creates an incident ticket payload for application status degradation.
    /// </summary>
    public static object Create(string projectKey, string appName, string status, string priority = "Medium")
    {
        var statusIcon = StatusIcon(status);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        return new JiraIssuePayload
        {
            Fields = new JiraFields
            {
                Project = new JiraKeyRef { Key = projectKey },
                Summary = $"{statusIcon} {appName} - Status: {status}",
                Description = JiraDoc.Build(
                    JiraDoc.Heading("Incident Details"),
                    JiraDoc.LabeledField("Application", appName),
                    JiraDoc.LabeledField("Status", $"{statusIcon} {status}"),
                    JiraDoc.LabeledField("Detected", $"{timestamp} UTC"),
                    JiraDoc.Heading("Description"),
                    JiraDoc.Paragraph($"This incident was automatically created by Monarch monitoring system. The application '{appName}' has entered a {status.ToLower()} state and requires investigation."),
                    JiraDoc.Paragraph("Please check the application logs, infrastructure metrics, and dependencies to identify the root cause.", italic: true)
                ),
                IssueType = new JiraNameRef { Name = "Incident" },
                Priority = new JiraNameRef { Name = priority },
                Labels = new[] { "monarch-incident", "automated", $"status-{status.ToLower()}" }
            }
        };
    }

    /// <summary>
    /// Creates a status update comment string for an existing incident ticket.
    /// </summary>
    public static string CreateUpdateComment(string appName, string newStatus, string oldStatus)
    {
        var icon = StatusIcon(newStatus);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        return $"{icon} *Status Update*\n\n" +
               $"Application: {appName}\n" +
               $"Previous Status: {oldStatus}\n" +
               $"Current Status: {newStatus}\n" +
               $"Updated: {timestamp} UTC\n\n" +
               $"_This update was automatically posted by Monarch monitoring system._";
    }

    /// <summary>
    /// Creates a recovery comment string for resolving an incident.
    /// </summary>
    public static string CreateRecoveryComment(string appName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        return $"🟢 *Recovery Confirmed*\n\n" +
               $"Application: {appName}\n" +
               $"Status: Operational\n" +
               $"Recovered: {timestamp} UTC\n\n" +
               $"The application has returned to normal operation. This incident can be closed.\n\n" +
               $"_This update was automatically posted by Monarch monitoring system._";
    }

    private static string StatusIcon(string status) => status switch
    {
        "Down" => "🔴",
        "Degraded" => "🟡",
        "Operational" => "🟢",
        _ => "⚪"
    };
}

// ===== Shared Jira Document Format Models =====
// These models map directly to the Jira REST API v3 ADF (Atlassian Document Format).

public class JiraIssuePayload
{
    [JsonPropertyName("fields")]
    public JiraFields Fields { get; set; } = new();
}

public class JiraFields
{
    [JsonPropertyName("project")]
    public JiraKeyRef Project { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public JiraDocument Description { get; set; } = new();

    [JsonPropertyName("issuetype")]
    public JiraNameRef IssueType { get; set; } = new();

    [JsonPropertyName("priority")]
    public JiraNameRef Priority { get; set; } = new();

    [JsonPropertyName("labels")]
    public string[] Labels { get; set; } = Array.Empty<string>();
}

public class JiraKeyRef
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

public class JiraNameRef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraDocument
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "doc";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("content")]
    public List<JiraContentBlock> Content { get; set; } = new();
}

public class JiraContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "paragraph";

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JiraTextNode>? Content { get; set; }

    [JsonPropertyName("attrs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JiraBlockAttrs? Attrs { get; set; }
}

public class JiraBlockAttrs
{
    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public class JiraTextNode
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("marks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JiraMark>? Marks { get; set; }
}

public class JiraMark
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Helper class to build Jira ADF (Atlassian Document Format) content blocks.
/// </summary>
public static class JiraDoc
{
    public static JiraDocument Build(params JiraContentBlock[] blocks)
    {
        return new JiraDocument { Content = blocks.ToList() };
    }

    public static JiraContentBlock Heading(string text, int level = 2)
    {
        return new JiraContentBlock
        {
            Type = "heading",
            Attrs = new JiraBlockAttrs { Level = level },
            Content = new List<JiraTextNode>
            {
                new() { Text = text }
            }
        };
    }

    public static JiraContentBlock Paragraph(string text, bool bold = false, bool italic = false)
    {
        var node = new JiraTextNode { Text = text };

        if (bold || italic)
        {
            node.Marks = new List<JiraMark>();
            if (bold) node.Marks.Add(new JiraMark { Type = "strong" });
            if (italic) node.Marks.Add(new JiraMark { Type = "em" });
        }

        return new JiraContentBlock
        {
            Type = "paragraph",
            Content = new List<JiraTextNode> { node }
        };
    }

    public static JiraContentBlock LabeledField(string label, string value)
    {
        return new JiraContentBlock
        {
            Type = "paragraph",
            Content = new List<JiraTextNode>
            {
                new() { Text = $"{label}: ", Marks = new List<JiraMark> { new() { Type = "strong" } } },
                new() { Text = value }
            }
        };
    }
}
