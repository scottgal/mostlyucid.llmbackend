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
        ConfigureHttpClient();
    }

    /// <summary>
    /// Configure HTTP client with Anthropic-specific headers
    /// </summary>
    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        // Add Anthropic API key header
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        }

        // Add Anthropic version header
        var version = _config.AnthropicVersion ?? DefaultAnthropicVersion;
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", version);
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Anthropic doesn't have a dedicated health endpoint, so we'll try a minimal request
            var testRequest = new
            {
                model = _config.ModelName ?? DefaultModel,
                messages = new[]
                {
                    new { role = "user", content = "Hi" }
                },
                max_tokens = 1
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

            // Consider both success and certain error codes as "available"
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anthropic backend {BackendName} is not available", Name);
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
            Stop = request.Stop,
            Stream = request.Stream,
            BackendName = request.BackendName,
            Context = request.Context
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
                model = _config.ModelName ?? DefaultModel,
                messages = messages,
                system = systemMessage, // System message sent separately in Anthropic API
                max_tokens = request.MaxTokens ?? _config.MaxOutputTokens ?? 2000,
                temperature = request.Temperature ?? _config.Temperature ?? 0.7,
                top_p = request.TopP ?? _config.TopP,
                stop_sequences = request.Stop ?? _config.StopSequences,
                stream = request.Stream && _config.EnableStreaming
            };

            var json = JsonSerializer.Serialize(anthropicRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Anthropic request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                RecordFailure(errorContent);
                return CreateErrorResponse(response.StatusCode.ToString(), errorContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseBody);

            if (anthropicResponse == null)
            {
                RecordFailure("Failed to deserialize response");
                return CreateErrorResponse("DeserializationError", "Failed to parse Anthropic response");
            }

            RecordSuccess(stopwatch.ElapsedMilliseconds);

            return new LlmResponse
            {
                Content = anthropicResponse.Content?.FirstOrDefault()?.Text ?? string.Empty,
                BackendUsed = Name,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ModelUsed = _config.ModelName ?? DefaultModel,
                PromptTokens = anthropicResponse.Usage?.InputTokens ?? 0,
                CompletionTokens = anthropicResponse.Usage?.OutputTokens ?? 0,
                TotalTokens = (anthropicResponse.Usage?.InputTokens ?? 0) + (anthropicResponse.Usage?.OutputTokens ?? 0),
                FinishReason = anthropicResponse.StopReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error calling Anthropic backend {BackendName}", Name);
            RecordFailure(ex.Message);
            return CreateErrorResponse("Exception", ex.Message);
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
