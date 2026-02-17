using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RestSharp;

namespace Monapi.Worker.Jira;

// Handles direct communication with Jira REST API
// Manages authentication, request formatting, and response parsing

public class JiraConnector
{
    private readonly string _baseUrl;
    private readonly string _projectKey;
    private readonly string _issueType;
    private readonly string _authHeader;

    public JiraConnector(string baseUrl, string projectKey, string issueType, string emailAndToken)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _projectKey = projectKey;
        _issueType = issueType;
        
        // Create Basic Auth header from email:token format
        var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(emailAndToken));
        _authHeader = $"Basic {base64Credentials}";
    }

    // Creates a new Jira incident issue for an application based on template

    public async Task<JiraTicket?> CreateIncidentIssue(string appName, string status, string priority = "Medium")
    {
        try
        {
            var url = $"{_baseUrl}/rest/api/3/issue";
            var options = new RestClientOptions(url);
            var client = new RestClient(options);
            var request = new RestRequest();

            request.AddHeader("Authorization", _authHeader);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");

            // Build the Jira issue payload using template
            var payload = JiraIncidentTicket.Create(_projectKey, appName, status, priority);

            request.AddJsonBody(payload);
            var response = await client.PostAsync(request);

            if (!response.IsSuccessful)
            {
                Console.WriteLine($"Failed to create Jira issue: {response.StatusCode} - {response.Content}");
                return null;
            }

            var jsonResponse = JsonNode.Parse(response.Content!);
            var issueKey = jsonResponse?["key"]?.ToString();
            var issueId = jsonResponse?["id"]?.ToString();

            if (string.IsNullOrEmpty(issueKey))
            {
                Console.WriteLine("Failed to parse issue key from Jira response");
                return null;
            }

            Console.WriteLine($"Successfully created Jira issue: {issueKey}");

            var statusIcon = status switch
            {
                "Down" => "🔴",
                "Degraded" => "🟡",
                _ => "⚪"
            };

            return new JiraTicket
            {
                IssueKey = issueKey,
                Summary = $"{statusIcon} {appName} - Status: {status}",
                Description = $"Application {appName} status is {status}",
                Status = "Open",
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception creating Jira issue: {ex.Message}");
            return null;
        }
    }

    // Adds a comment to an existing Jira issue

    public async Task<bool> AddComment(string issueKey, string comment)
    {
        try
        {
            var url = $"{_baseUrl}/rest/api/3/issue/{issueKey}/comment";
            var options = new RestClientOptions(url);
            var client = new RestClient(options);
            var request = new RestRequest();

            request.AddHeader("Authorization", _authHeader);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");

            var payload = new
            {
                body = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = comment
                                }
                            }
                        }
                    }
                }
            };

            request.AddJsonBody(payload);
            var response = await client.PostAsync(request);

            if (response.IsSuccessful)
            {
                Console.WriteLine($"Successfully added comment to {issueKey}");
                return true;
            }

            Console.WriteLine($"Failed to add comment to {issueKey}: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception adding comment to Jira: {ex.Message}");
            return false;
        }
    }

    // Transitions a Jira issue to a different status (e.g., "Done", "In Progress")

    public async Task<bool> TransitionIssue(string issueKey, string transitionName)
    {
        try
        {
            // First, get available transitions
            var transitionId = await GetTransitionId(issueKey, transitionName);
            if (transitionId == null)
            {
                Console.WriteLine($"Transition '{transitionName}' not found for {issueKey}");
                return false;
            }

            var url = $"{_baseUrl}/rest/api/3/issue/{issueKey}/transitions";
            var options = new RestClientOptions(url);
            var client = new RestClient(options);
            var request = new RestRequest();

            request.AddHeader("Authorization", _authHeader);
            request.AddHeader("Content-Type", "application/json");

            var payload = new
            {
                transition = new { id = transitionId }
            };

            request.AddJsonBody(payload);
            var response = await client.PostAsync(request);

            if (response.IsSuccessful)
            {
                Console.WriteLine($"Successfully transitioned {issueKey} to {transitionName}");
                return true;
            }

            Console.WriteLine($"Failed to transition {issueKey}: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception transitioning Jira issue: {ex.Message}");
            return false;
        }
    }

    // Gets the transition ID for a specific transition name

    private async Task<string?> GetTransitionId(string issueKey, string transitionName)
    {
        try
        {
            var url = $"{_baseUrl}/rest/api/3/issue/{issueKey}/transitions";
            var options = new RestClientOptions(url);
            var client = new RestClient(options);
            var request = new RestRequest();

            request.AddHeader("Authorization", _authHeader);
            request.AddHeader("Accept", "application/json");

            var response = await client.GetAsync(request);

            if (!response.IsSuccessful)
            {
                return null;
            }

            var jsonResponse = JsonNode.Parse(response.Content!);
            var transitions = jsonResponse?["transitions"]?.AsArray();

            if (transitions == null)
            {
                return null;
            }

            foreach (var transition in transitions)
            {
                var name = transition?["name"]?.ToString();
                if (name?.Equals(transitionName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return transition?["id"]?.ToString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // Gets information about a specific Jira issue
    public async Task<JiraTicket?> GetIssue(string issueKey)
    {
        try
        {
            var url = $"{_baseUrl}/rest/api/3/issue/{issueKey}";
            var options = new RestClientOptions(url);
            var client = new RestClient(options);
            var request = new RestRequest();

            request.AddHeader("Authorization", _authHeader);
            request.AddHeader("Accept", "application/json");

            var response = await client.GetAsync(request);

            if (!response.IsSuccessful)
            {
                Console.WriteLine($"Failed to get Jira issue {issueKey}: {response.StatusCode}");
                return null;
            }

            var jsonResponse = JsonNode.Parse(response.Content!);
            var fields = jsonResponse?["fields"];

            return new JiraTicket
            {
                IssueKey = issueKey,
                Summary = fields?["summary"]?.ToString() ?? "",
                Status = fields?["status"]?["name"]?.ToString() ?? "",
                Priority = fields?["priority"]?["name"]?.ToString() ?? "Medium",
                CreatedAt = DateTime.Parse(fields?["created"]?.ToString() ?? DateTime.UtcNow.ToString()),
                UpdatedAt = DateTime.Parse(fields?["updated"]?.ToString() ?? DateTime.UtcNow.ToString())
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception getting Jira issue: {ex.Message}");
            return null;
        }
    }
}
