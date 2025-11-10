using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// LlamaCpp backend implementation with automatic model downloading
/// Supports GGUF format models and OpenAI-compatible API
/// </summary>
public class LlamaCppLlmBackend : BaseLlmBackend
{
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    private bool _modelDownloaded = false;

    public LlamaCppLlmBackend(
        LlmBackendConfig config,
        ILogger<LlamaCppLlmBackend> logger,
        HttpClient httpClient,
        TelemetryConfig? telemetry = null) : base(config, logger, httpClient, telemetry)
    {
    }

    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        // LlamaCpp typically doesn't require authentication for local instances
        // But we keep the option for remote instances
        if (!string.IsNullOrEmpty(Config.ApiKey))
        {
            HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.ApiKey}");
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure model is downloaded if needed
            if (!_modelDownloaded && Config.AutoDownloadModel)
            {
                await EnsureModelDownloadedAsync(cancellationToken);
            }

            // Check if LlamaCpp server is running via health endpoint
            var response = await HttpClient.GetAsync("health", cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            // Fallback: try to get models list (OpenAI-compatible endpoint)
            response = await HttpClient.GetAsync("v1/models", cancellationToken);
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
            // Ensure model is downloaded if needed
            if (!_modelDownloaded && Config.AutoDownloadModel)
            {
                await EnsureModelDownloadedAsync(cancellationToken);
            }

            var requestBody = new
            {
                prompt = request.Prompt,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens ?? 512,
                top_p = Config.TopP ?? 0.95,
                frequency_penalty = Config.FrequencyPenalty ?? 0.0,
                presence_penalty = Config.PresencePenalty ?? 0.0,
                stop = Config.StopSequences,
                n_predict = request.MaxTokens ?? Config.MaxOutputTokens ?? 512,
                // LlamaCpp-specific parameters
                n_ctx = Config.ContextSize ?? 2048,
                seed = Config.Seed ?? -1,
                n_threads = Config.Threads
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("completion", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"LlamaCpp returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var responseText = root.GetProperty("content").GetString() ?? string.Empty;

            // Try to extract token counts if available
            int? totalTokens = null;
            int? promptTokens = null;
            int? completionTokens = null;

            if (root.TryGetProperty("tokens_predicted", out var predictedElem))
            {
                completionTokens = predictedElem.GetInt32();
            }

            if (root.TryGetProperty("tokens_evaluated", out var evaluatedElem))
            {
                promptTokens = evaluatedElem.GetInt32();
            }

            if (promptTokens.HasValue && completionTokens.HasValue)
            {
                totalTokens = promptTokens.Value + completionTokens.Value;
            }

            stopwatch.Stop();

            return CreateSuccessResponse(
                responseText,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName,
                totalTokens,
                promptTokens,
                completionTokens);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"LlamaCpp request failed: {ex.Message}", ex);
        }
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Ensure model is downloaded if needed
            if (!_modelDownloaded && Config.AutoDownloadModel)
            {
                await EnsureModelDownloadedAsync(cancellationToken);
            }

            var messages = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList();

            var requestBody = new
            {
                model = Config.ModelName ?? "local-model",
                messages,
                temperature = request.Temperature ?? Config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? Config.MaxOutputTokens ?? 512,
                top_p = Config.TopP ?? 0.95,
                frequency_penalty = Config.FrequencyPenalty ?? 0.0,
                presence_penalty = Config.PresencePenalty ?? 0.0,
                stop = Config.StopSequences,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Use OpenAI-compatible endpoint
            var response = await HttpClient.PostAsync("v1/chat/completions", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"LlamaCpp returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Parse OpenAI-compatible response
            var messageContent = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            // Extract token usage if available
            int? totalTokens = null;
            int? promptTokens = null;
            int? completionTokens = null;
            string? finishReason = null;

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("total_tokens", out var totalElem))
                    totalTokens = totalElem.GetInt32();
                if (usage.TryGetProperty("prompt_tokens", out var promptElem))
                    promptTokens = promptElem.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completionElem))
                    completionTokens = completionElem.GetInt32();
            }

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                if (choices[0].TryGetProperty("finish_reason", out var finishElem))
                    finishReason = finishElem.GetString();
            }

            stopwatch.Stop();

            return CreateSuccessResponse(
                messageContent,
                stopwatch.ElapsedMilliseconds,
                Config.ModelName,
                totalTokens,
                promptTokens,
                completionTokens,
                finishReason);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"LlamaCpp request failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ensures the model is downloaded if AutoDownloadModel is enabled
    /// </summary>
    private async Task EnsureModelDownloadedAsync(CancellationToken cancellationToken)
    {
        if (_modelDownloaded || !Config.AutoDownloadModel)
            return;

        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_modelDownloaded)
                return;

            // Check if model path is specified
            if (string.IsNullOrEmpty(Config.ModelPath))
            {
                Logger.LogWarning("[{BackendName}] ModelPath not specified, skipping download", Name);
                _modelDownloaded = true; // Prevent further checks
                return;
            }

            // Check if model already exists
            if (File.Exists(Config.ModelPath))
            {
                Logger.LogInformation("[{BackendName}] Model already exists at {ModelPath}", Name, Config.ModelPath);
                _modelDownloaded = true;
                return;
            }

            // Check if download URL is specified
            if (string.IsNullOrEmpty(Config.ModelUrl))
            {
                Logger.LogWarning("[{BackendName}] ModelUrl not specified, cannot download model", Name);
                _modelDownloaded = true; // Prevent further checks
                return;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(Config.ModelPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.LogInformation("[{BackendName}] Created directory {Directory}", Name, directory);
            }

            // Download the model
            Logger.LogInformation("[{BackendName}] Downloading model from {ModelUrl} to {ModelPath}",
                Name, Config.ModelUrl, Config.ModelPath);

            using var downloadClient = new HttpClient();
            downloadClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large models

            var tempPath = Config.ModelPath + ".tmp";

            try
            {
                using var response = await downloadClient.GetAsync(Config.ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                var lastLogTime = DateTime.UtcNow;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;

                    // Log progress every 5 seconds
                    if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 5)
                    {
                        if (totalBytes.HasValue)
                        {
                            var progress = (double)totalBytesRead / totalBytes.Value * 100;
                            Logger.LogInformation("[{BackendName}] Download progress: {Progress:F1}% ({BytesRead} / {TotalBytes} bytes)",
                                Name, progress, totalBytesRead, totalBytes.Value);
                        }
                        else
                        {
                            Logger.LogInformation("[{BackendName}] Download progress: {BytesRead} bytes downloaded",
                                Name, totalBytesRead);
                        }
                        lastLogTime = DateTime.UtcNow;
                    }
                }

                await fileStream.FlushAsync(cancellationToken);

                // Move temp file to final location
                File.Move(tempPath, Config.ModelPath, overwrite: true);

                Logger.LogInformation("[{BackendName}] Model downloaded successfully to {ModelPath} ({TotalBytes} bytes)",
                    Name, Config.ModelPath, totalBytesRead);

                _modelDownloaded = true;
            }
            catch (Exception ex)
            {
                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                }

                Logger.LogError(ex, "[{BackendName}] Failed to download model from {ModelUrl}", Name, Config.ModelUrl);
                throw;
            }
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
