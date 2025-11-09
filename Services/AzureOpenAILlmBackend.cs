using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Azure OpenAI backend implementation
/// </summary>
public class AzureOpenAILlmBackend : BaseLlmBackend
{
    public AzureOpenAILlmBackend(
        LlmBackendConfig config,
        ILogger<AzureOpenAILlmBackend> logger,
        HttpClient httpClient) : base(config, logger, httpClient)
    {
    }

    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        if (!string.IsNullOrEmpty(Config.ApiKey))
        {
            HttpClient.DefaultRequestHeaders.Add("api-key", Config.ApiKey);
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = BuildChatEndpoint();
            var testRequest = new
            {
                messages = new[] { new { role = "user", content = "test" } },
                max_tokens = 1
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);

            // Azure returns 400 for very small requests, but that still means it's available
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest;
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
                messages,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens ?? 2000,
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
            var endpoint = BuildChatEndpoint();
            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"Azure OpenAI returned {response.StatusCode}: {errorContent}");
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
                Config.DeploymentName ?? Config.ModelName,
                totalTokens,
                promptTokens,
                completionTokens,
                finishReason);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"Azure OpenAI request failed: {ex.Message}", ex);
        }
    }

    private string BuildChatEndpoint()
    {
        var apiVersion = Config.ApiVersion ?? "2024-02-15-preview";
        var deploymentName = Config.DeploymentName ?? Config.ModelName ?? "gpt-4";
        return $"openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";
    }
}
