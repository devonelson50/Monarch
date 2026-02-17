using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monarch.Jira;

/// <summary>
/// Builds a Jira test ticket payload for validating workspace configuration.
/// Uses strongly-typed models instead of anonymous objects for readability.
/// </summary>
public static class JiraTestTicket
{
    /// <summary>
    /// Creates a test ticket payload using the Jira REST API v3 document format.
    /// The issue type ID is resolved dynamically before calling this method.
    /// </summary>
    public static object Create(string workspaceKey, string workspaceName, string issueTypeId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        return new JiraIssuePayload
        {
            Fields = new JiraFields
            {
                Project = new JiraKeyRef { Key = workspaceKey },
                Summary = "[TEST] Monarch Admin Panel Configuration Test",
                Description = JiraDoc.Build(
                    JiraDoc.Paragraph($"This is a test ticket created from the Monarch Admin Panel."),
                    JiraDoc.Paragraph($"Workspace: {workspaceName} ({workspaceKey})"),
                    JiraDoc.Paragraph($"Created: {timestamp} UTC"),
                    JiraDoc.Paragraph("If you see this ticket, your Jira integration is configured correctly. This ticket can be safely closed or deleted.", italic: true)
                ),
                IssueType = new JiraIdRef { Id = issueTypeId },
                Priority = new JiraNameRef { Name = "Low" },
                Labels = new[] { "monarch-test", "automation" }
            }
        };
    }
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
    public JiraIdRef IssueType { get; set; } = new();

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

public class JiraIdRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
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
/// Reduces boilerplate when constructing ticket descriptions.
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
