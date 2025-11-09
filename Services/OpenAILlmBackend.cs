using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// OpenAI backend implementation
/// </summary>
public class OpenAILlmBackend : BaseLlmBackend
{
    public OpenAILlmBackend(
        LlmBackendConfig config,
        ILogger<OpenAILlmBackend> logger,
        HttpClient httpClient,
        TelemetryConfig? telemetry = null) : base(config, logger, httpClient, telemetry)
    {
    }

    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        if (!string.IsNullOrEmpty(Config.ApiKey))
        {
            HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.ApiKey}");
        }

        if (!string.IsNullOrEmpty(Config.OrganizationId))
        {
            HttpClient.DefaultRequestHeaders.Add("OpenAI-Organization", Config.OrganizationId);
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check budget first (no point checking API if we're over budget)
        if (!IsWithinBudget())
        {
            return false;
        }

        try
        {
            var response = await HttpClient.GetAsync("v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{BackendName}] Availability check failed", Name);
            return false;
        }
    }

    public override async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // OpenAI's completion API is deprecated, use chat instead
        var chatRequest = new ChatRequest
        {
            Prompt = request.Prompt,
            SystemMessage = request.SystemMessage,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            StopSequences = request.StopSequences,
            Messages = new List<ChatMessage>()
        };

        if (!string.IsNullOrEmpty(request.SystemMessage))
        {
            chatRequest.Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = request.SystemMessage
            });
        }

        chatRequest.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Prompt
        });

        return await ChatAsync(chatRequest, cancellationToken);
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        // Increment active requests metric
        if (Telemetry?.EnableMetrics == true)
        {
            LlmMetrics.IncrementActiveRequests(Name);
        }

        var stopwatch = Stopwatch.StartNew();
        var modelName = Config.ModelName ?? "gpt-4";

        // Information level: Starting request
        Logger.LogInformation(
            "[{Backend}] Starting chat request with model {Model}, {MessageCount} messages",
            Name,
            modelName,
            request.Messages?.Count ?? 0);

        try
        {
            var messages = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList();

            var requestBody = new
            {
                model = modelName,
                messages,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens,
                top_p = request.TopP,
                frequency_penalty = request.FrequencyPenalty,
                presence_penalty = request.PresencePenalty,
                stop = request.StopSequences
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = IsTelemetryEnabled(t => t.LogPrompts) // Pretty print for debug logging
            });

            // Debug level: Log the full request payload
            if (IsTelemetryEnabled(t => t.LogPrompts))
            {
                Logger.LogDebug(
                    "[{Backend}] OpenAI request payload:\n{RequestJson}",
                    Name,
                    json);
            }

            // Debug level: Log HTTP headers being sent
            if (IsTelemetryEnabled(t => t.LogHeaders))
            {
                var headers = string.Join(", ",
                    HttpClient.DefaultRequestHeaders.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
                Logger.LogDebug(
                    "[{Backend}] Request headers: {Headers}",
                    Name,
                    headers);
            }

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("v1/chat/completions", content, cancellationToken);

            // Debug level: Log response status and headers
            if (IsTelemetryEnabled(t => t.LogHeaders))
            {
                var responseHeaders = string.Join(", ",
                    response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
                Logger.LogDebug(
                    "[{Backend}] Response status: {StatusCode}, Headers: {Headers}",
                    Name,
                    response.StatusCode,
                    responseHeaders);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Error level: API error response
                Logger.LogError(
                    "[{Backend}] OpenAI API error {StatusCode}: {Error}",
                    Name,
                    response.StatusCode,
                    errorContent);

                return CreateErrorResponse($"OpenAI returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            // Debug level: Log the full raw response
            if (IsTelemetryEnabled(t => t.LogResponses))
            {
                Logger.LogDebug(
                    "[{Backend}] OpenAI raw response:\n{ResponseJson}",
                    Name,
                    responseJson);
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var messageContent = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var usage = root.GetProperty("usage");
            var totalTokens = usage.GetProperty("total_tokens").GetInt32();
            var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

            var finishReason = root
                .GetProperty("choices")[0]
                .GetProperty("finish_reason")
                .GetString();

            stopwatch.Stop();

            // Debug level: Detailed timing breakdown
            if (IsTelemetryEnabled(t => t.LogTiming))
            {
                Logger.LogDebug(
                    "[{Backend}] Request completed in {Duration}ms",
                    Name,
                    stopwatch.ElapsedMilliseconds);
            }

            // Information level: Success summary with token counts
            if (IsTelemetryEnabled(t => t.LogTokenCounts))
            {
                // Calculate estimated cost if configured
                var estimatedCost = 0m;
                if (Config.CostPerMillionInputTokens.HasValue && Config.CostPerMillionOutputTokens.HasValue)
                {
                    estimatedCost = (promptTokens * Config.CostPerMillionInputTokens.Value / 1_000_000m) +
                                  (completionTokens * Config.CostPerMillionOutputTokens.Value / 1_000_000m);

                    Logger.LogInformation(
                        "[{Backend}] Completed in {Duration}ms. Tokens: {PromptTokens}+{CompletionTokens}={TotalTokens}. " +
                        "Est. cost: ${Cost:F6}. Finish: {FinishReason}",
                        Name,
                        stopwatch.ElapsedMilliseconds,
                        promptTokens,
                        completionTokens,
                        totalTokens,
                        estimatedCost,
                        finishReason);
                }
                else
                {
                    Logger.LogInformation(
                        "[{Backend}] Completed in {Duration}ms. Tokens: {PromptTokens}+{CompletionTokens}={TotalTokens}. Finish: {FinishReason}",
                        Name,
                        stopwatch.ElapsedMilliseconds,
                        promptTokens,
                        completionTokens,
                        totalTokens,
                        finishReason);
                }
            }
            else
            {
                Logger.LogInformation(
                    "[{Backend}] Completed in {Duration}ms",
                    Name,
                    stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResponse(
                messageContent,
                stopwatch.ElapsedMilliseconds,
                modelName,
                totalTokens,
                promptTokens,
                completionTokens,
                finishReason);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Error level: Exception details
            Logger.LogError(
                ex,
                "[{Backend}] OpenAI request failed after {Duration}ms: {Message}",
                Name,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            return CreateErrorResponse($"OpenAI request failed: {ex.Message}", ex);
        }
    }
}
