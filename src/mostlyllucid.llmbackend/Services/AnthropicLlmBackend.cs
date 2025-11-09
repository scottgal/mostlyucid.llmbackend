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
/// Anthropic (Claude) LLM backend implementation
/// </summary>
public class AnthropicLlmBackend : BaseLlmBackend
{
    private const string DefaultAnthropicVersion = "2023-06-01";
    private const string DefaultModel = "claude-3-5-sonnet-20241022";

    public AnthropicLlmBackend(
        LlmBackendConfig config,
        ILogger<AnthropicLlmBackend> logger,
        HttpClient httpClient)
        : base(config, logger, httpClient)
    {
        // Base constructor already calls ConfigureHttpClient via virtual dispatch.
        // Avoid calling it twice to prevent duplicate headers.
    }

    /// <summary>
    /// Configure HTTP client with Anthropic-specific headers
    /// </summary>
    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        // Add Anthropic API key header idempotently
        if (!string.IsNullOrEmpty(Config.ApiKey))
        {
            if (HttpClient.DefaultRequestHeaders.Contains("x-api-key"))
            {
                HttpClient.DefaultRequestHeaders.Remove("x-api-key");
            }
            HttpClient.DefaultRequestHeaders.Add("x-api-key", Config.ApiKey);
        }

        // Add Anthropic version header idempotently
        var version = Config.AnthropicVersion ?? DefaultAnthropicVersion;
        if (HttpClient.DefaultRequestHeaders.Contains("anthropic-version"))
        {
            HttpClient.DefaultRequestHeaders.Remove("anthropic-version");
        }
        HttpClient.DefaultRequestHeaders.Add("anthropic-version", version);
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Anthropic doesn't have a dedicated health endpoint, so we'll try a minimal request
            var testRequest = new
            {
                model = Config.ModelName ?? DefaultModel,
                messages = new[]
                {
                    new { role = "user", content = "Hi" }
                },
                max_tokens = 1
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync("/v1/messages", content, cancellationToken);

            // Consider both success and certain error codes as "available"
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Anthropic backend {BackendName} is not available", Name);
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
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
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
            // Separate system messages from user/assistant messages
            var systemMessage = request.Messages
                .Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Content)
                .FirstOrDefault();

            var messages = request.Messages
                .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .Select(m => new
                {
                    role = m.Role.ToLowerInvariant(),
                    content = m.Content
                })
                .ToList();

            var anthropicRequest = new
            {
                model = Config.ModelName ?? DefaultModel,
                messages = messages,
                system = systemMessage, // System message sent separately in Anthropic API
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens ?? 2000,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                top_p = request.TopP ?? Config.TopP,
                stop_sequences = request.StopSequences ?? Config.StopSequences,
                stream = request.Stream && Config.EnableStreaming
            };

            var json = JsonSerializer.Serialize(anthropicRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("/v1/messages", content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError(
                    "Anthropic request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                return CreateErrorResponse($"Anthropic request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseBody);

            if (anthropicResponse == null)
            {
                return CreateErrorResponse("Failed to deserialize Anthropic response");
            }

            var text = anthropicResponse.Content?.FirstOrDefault()?.Text ?? string.Empty;
            var promptTokens = anthropicResponse.Usage?.InputTokens ?? 0;
            var completionTokens = anthropicResponse.Usage?.OutputTokens ?? 0;
            var totalTokens = promptTokens + completionTokens;
            var finishReason = anthropicResponse.StopReason;

            return CreateSuccessResponse(
                text,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName ?? DefaultModel,
                totalTokens,
                promptTokens,
                completionTokens,
                finishReason);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error calling Anthropic backend {BackendName}", Name);
            return CreateErrorResponse($"Error calling Anthropic: {ex.Message}", ex);
        }
    }

    #region Anthropic Response Models

    private class AnthropicResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    #endregion
}
