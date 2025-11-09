using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Ollama backend implementation (also works with LM Studio)
/// </summary>
public class OllamaLlmBackend : BaseLlmBackend
{
    public OllamaLlmBackend(
        LlmBackendConfig config,
        ILogger<OllamaLlmBackend> logger,
        HttpClient httpClient) : base(config, logger, httpClient)
    {
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await HttpClient.GetAsync("api/tags", cancellationToken);
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var requestBody = new
            {
                model = Config.ModelName ?? "llama3",
                prompt = request.Prompt,
                system = request.SystemMessage,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("api/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"Ollama returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var responseText = root.GetProperty("response").GetString() ?? string.Empty;

            stopwatch.Stop();

            return CreateSuccessResponse(
                responseText,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName ?? "llama3");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"Ollama request failed: {ex.Message}", ex);
        }
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
                model = Config.ModelName ?? "llama3",
                messages,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("api/chat", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"Ollama returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var messageContent = root
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            stopwatch.Stop();

            return CreateSuccessResponse(
                messageContent,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName ?? "llama3");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"Ollama request failed: {ex.Message}", ex);
        }
    }
}
