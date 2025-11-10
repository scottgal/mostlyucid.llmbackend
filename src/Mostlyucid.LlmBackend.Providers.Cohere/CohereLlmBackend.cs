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
/// Cohere LLM backend implementation
/// </summary>
public class CohereLlmBackend : BaseLlmBackend
{
    private const string DefaultModel = "command-r-plus";

    public CohereLlmBackend(
        LlmBackendConfig config,
        ILogger<CohereLlmBackend> logger,
        HttpClient httpClient,
        TelemetryConfig? telemetry = null)
        : base(config, logger, httpClient, telemetry)
    {
        ConfigureHttpClient();
    }

    /// <summary>
    /// Configure HTTP client with Cohere-specific headers
    /// </summary>
    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        // Add Cohere API key header
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to check models endpoint
            var response = await _httpClient.GetAsync("/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cohere backend {BackendName} is not available", Name);
            return false;
        }
    }

    public override async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cohereRequest = new
            {
                model = _config.ModelName ?? DefaultModel,
                prompt = request.Prompt,
                max_tokens = request.MaxTokens ?? _config.MaxOutputTokens ?? 2000,
                temperature = request.Temperature ?? _config.Temperature ?? 0.7,
                p = request.TopP ?? _config.TopP,
                frequency_penalty = request.FrequencyPenalty ?? _config.FrequencyPenalty,
                presence_penalty = request.PresencePenalty ?? _config.PresencePenalty,
                stop_sequences = request.Stop ?? _config.StopSequences,
                stream = request.Stream && _config.EnableStreaming
            };

            var json = JsonSerializer.Serialize(cohereRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/v1/generate", content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Cohere request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                RecordFailure(errorContent);
                return CreateErrorResponse(response.StatusCode.ToString(), errorContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var cohereResponse = JsonSerializer.Deserialize<CohereGenerateResponse>(responseBody);

            if (cohereResponse == null)
            {
                RecordFailure("Failed to deserialize response");
                return CreateErrorResponse("DeserializationError", "Failed to parse Cohere response");
            }

            RecordSuccess(stopwatch.ElapsedMilliseconds);

            return new LlmResponse
            {
                Content = cohereResponse.Generations?.FirstOrDefault()?.Text ?? string.Empty,
                BackendUsed = Name,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ModelUsed = _config.ModelName ?? DefaultModel,
                PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                CompletionTokens = cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0,
                TotalTokens = (cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0) +
                              (cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0),
                FinishReason = cohereResponse.Generations?.FirstOrDefault()?.FinishReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error calling Cohere backend {BackendName}", Name);
            RecordFailure(ex.Message);
            return CreateErrorResponse("Exception", ex.Message);
        }
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Convert messages to Cohere chat format
            var chatHistory = request.Messages
                .Take(request.Messages.Count - 1) // All except last
                .Select(m => new
                {
                    role = ConvertRole(m.Role),
                    message = m.Content
                })
                .ToList();

            var lastMessage = request.Messages.LastOrDefault();
            if (lastMessage == null)
            {
                return CreateErrorResponse("InvalidRequest", "No messages provided");
            }

            var cohereRequest = new
            {
                model = _config.ModelName ?? DefaultModel,
                message = lastMessage.Content,
                chat_history = chatHistory.Any() ? chatHistory : null,
                max_tokens = request.MaxTokens ?? _config.MaxOutputTokens ?? 2000,
                temperature = request.Temperature ?? _config.Temperature ?? 0.7,
                p = request.TopP ?? _config.TopP,
                frequency_penalty = request.FrequencyPenalty ?? _config.FrequencyPenalty,
                presence_penalty = request.PresencePenalty ?? _config.PresencePenalty,
                stop_sequences = request.Stop ?? _config.StopSequences,
                stream = request.Stream && _config.EnableStreaming
            };

            var json = JsonSerializer.Serialize(cohereRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/v1/chat", content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Cohere chat request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                RecordFailure(errorContent);
                return CreateErrorResponse(response.StatusCode.ToString(), errorContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var cohereResponse = JsonSerializer.Deserialize<CohereChatResponse>(responseBody);

            if (cohereResponse == null)
            {
                RecordFailure("Failed to deserialize response");
                return CreateErrorResponse("DeserializationError", "Failed to parse Cohere response");
            }

            RecordSuccess(stopwatch.ElapsedMilliseconds);

            return new LlmResponse
            {
                Content = cohereResponse.Text ?? string.Empty,
                BackendUsed = Name,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ModelUsed = _config.ModelName ?? DefaultModel,
                PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                CompletionTokens = cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0,
                TotalTokens = (cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0) +
                              (cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0),
                FinishReason = cohereResponse.FinishReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error calling Cohere chat backend {BackendName}", Name);
            RecordFailure(ex.Message);
            return CreateErrorResponse("Exception", ex.Message);
        }
    }

    /// <summary>
    /// Convert role to Cohere format
    /// </summary>
    private string ConvertRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "assistant" => "CHATBOT",
            "user" => "USER",
            "system" => "SYSTEM",
            _ => "USER"
        };
    }

    #region Cohere Response Models

    private class CohereGenerateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("generations")]
        public List<Generation>? Generations { get; set; }

        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("meta")]
        public Meta? Meta { get; set; }
    }

    private class CohereChatResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("generation_id")]
        public string? GenerationId { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("meta")]
        public Meta? Meta { get; set; }
    }

    private class Generation
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class Meta
    {
        [JsonPropertyName("api_version")]
        public ApiVersion? ApiVersion { get; set; }

        [JsonPropertyName("billed_units")]
        public BilledUnits? BilledUnits { get; set; }

        [JsonPropertyName("tokens")]
        public Tokens? Tokens { get; set; }
    }

    private class ApiVersion
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    private class BilledUnits
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    private class Tokens
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    #endregion
}
