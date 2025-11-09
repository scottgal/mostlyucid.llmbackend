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
        HttpClient httpClient) : base(config, logger, httpClient)
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var messages = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList();

            var requestBody = new
            {
                model = Config.ModelName ?? "gpt-4",
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
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("v1/chat/completions", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"OpenAI returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
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

            return CreateSuccessResponse(
                messageContent,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName ?? "gpt-4",
                totalTokens,
                promptTokens,
                completionTokens,
                finishReason);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"OpenAI request failed: {ex.Message}", ex);
        }
    }
}
