using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Google Gemini LLM backend implementation
/// </summary>
public class GeminiLlmBackend : BaseLlmBackend
{
    private const string DefaultModel = "gemini-1.5-pro";
    private const string DefaultLocation = "us-central1";

    public GeminiLlmBackend(
        LlmBackendConfig config,
        ILogger<GeminiLlmBackend> logger,
        HttpClient httpClient)
        : base(config, logger, httpClient)
    {
        ConfigureHttpClient();
    }

    /// <summary>
    /// Configure HTTP client with Gemini-specific headers
    /// </summary>
    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        // Google API uses query parameter for API key, not headers
        // But we can set default headers if needed
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to generate content with a minimal request
            var endpoint = BuildEndpoint("generateContent");
            var testRequest = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = "Hi" } }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 1
                }
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);

            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Gemini backend {BackendName} is not available", Name);
            return false;
        }
    }

    public override async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Convert completion request to chat format
        var chatRequest = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = request.Prompt }
            },
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            StopSequences = request.StopSequences,
            Stream = request.Stream,
            PreferredBackend = request.PreferredBackend
        };

        return await ChatAsync(chatRequest, cancellationToken);
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = BuildEndpoint("generateContent");

            // Convert messages to Gemini format
            var contents = request.Messages.Select(m => new
            {
                role = ConvertRole(m.Role),
                parts = new[] { new { text = m.Content } }
            }).ToList();

            var geminiRequest = new
            {
                contents = contents,
                generationConfig = new
                {
                    temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                    topP = request.TopP ?? Config.TopP,
                    maxOutputTokens = request.MaxTokens ?? Config.MaxOutputTokens ?? 2000,
                    stopSequences = request.StopSequences ?? Config.StopSequences
                }
            };

            var json = JsonSerializer.Serialize(geminiRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError(
                    "Gemini request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                return CreateErrorResponse($"Gemini request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody);

            if (geminiResponse == null)
            {
                return CreateErrorResponse("Failed to deserialize Gemini response");
            }

            var textContent = geminiResponse.Candidates?
                .FirstOrDefault()?
                .Content?
                .Parts?
                .FirstOrDefault()?
                .Text ?? string.Empty;

            RecordSuccess(stopwatch.ElapsedMilliseconds);

            return new LlmResponse
            {
                Text = textContent,
                Backend = Name,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Model = Config.ModelName ?? DefaultModel,
                PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0,
                FinishReason = geminiResponse.Candidates?.FirstOrDefault()?.FinishReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error calling Gemini backend {BackendName}", Name);
            return CreateErrorResponse($"Error calling Gemini: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Build the endpoint URL with model and API key
    /// </summary>
    private string BuildEndpoint(string action)
    {
        var model = Config.ModelName ?? DefaultModel;
        var apiKey = Config.ApiKey;

        // For Vertex AI (GCP), use different URL structure
        if (!string.IsNullOrEmpty(Config.ProjectId))
        {
            var location = Config.Location ?? DefaultLocation;
            var projectId = Config.ProjectId;
            return $"/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:{action}";
        }

        // For Google AI Studio (public API)
        return $"/v1beta/models/{model}:{action}?key={apiKey}";
    }

    /// <summary>
    /// Convert role to Gemini format (user/model instead of user/assistant)
    /// </summary>
    private string ConvertRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "assistant" => "model",
            "system" => "user", // Gemini doesn't have system role, convert to user
            _ => role.ToLowerInvariant()
        };
    }

    #region Gemini Response Models

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; set; }

        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating>? SafetyRatings { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class UsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }

    private class SafetyRating
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("probability")]
        public string? Probability { get; set; }
    }

    #endregion
}
