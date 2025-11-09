using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// EasyNMT backend implementation
/// </summary>
public class EasyNMTBackend : BaseLlmBackend
{
    public EasyNMTBackend(
        LlmBackendConfig config,
        ILogger<EasyNMTBackend> logger,
        HttpClient httpClient) : base(config, logger, httpClient)
    {
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await HttpClient.GetAsync("models", cancellationToken);
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
        // For translation, we expect source and target languages in the system message or use defaults
        var sourceLang = "auto";
        var targetLang = "en";

        // Try to parse from system message if provided
        if (!string.IsNullOrEmpty(request.SystemMessage))
        {
            // Expected format: "translate from {source} to {target}"
            // This is a simple implementation - could be enhanced
        }

        return await TranslateAsync(request.Prompt, sourceLang, targetLang, cancellationToken);
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        // Extract the last user message for translation
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage == null)
        {
            return CreateErrorResponse("No user message found in chat request");
        }

        return await CompleteAsync(new LlmRequest { Prompt = lastUserMessage.Content }, cancellationToken);
    }

    private async Task<LlmResponse> TranslateAsync(
        string text,
        string sourceLang,
        string targetLang,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Try POST first (supports longer text)
            var requestBody = new
            {
                text,
                source_lang = sourceLang,
                target_lang = targetLang,
                beam_size = 5,
                perform_sentence_splitting = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("translate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback to GET if POST fails
                var encodedText = Uri.EscapeDataString(text);
                var url = $"translate?text={encodedText}&source_lang={sourceLang}&target_lang={targetLang}";
                response = await HttpClient.GetAsync(url, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateErrorResponse($"EasyNMT returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // EasyNMT can return different response formats (object with "translation" or a plain JSON string)
            string translatedText;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("translation", out var translationProp))
                {
                    translatedText = translationProp.GetString() ?? string.Empty;
                }
                else
                {
                    translatedText = string.Empty;
                }
            }
            else if (root.ValueKind == JsonValueKind.String)
            {
                translatedText = root.GetString() ?? string.Empty;
            }
            else
            {
                translatedText = string.Empty;
            }

            stopwatch.Stop();

            return CreateSuccessResponse(
                translatedText,
                stopwatch.ElapsedMilliseconds,
                "EasyNMT");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateErrorResponse($"EasyNMT request failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get available language pairs
    /// </summary>
    public async Task<List<(string Source, string Target)>> GetLanguagePairsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await HttpClient.GetAsync("language_pairs", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<(string, string)>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var pairs = new List<(string, string)>();
            foreach (var pair in root.EnumerateArray())
            {
                if (pair.GetArrayLength() >= 2)
                {
                    var source = pair[0].GetString() ?? string.Empty;
                    var target = pair[1].GetString() ?? string.Empty;
                    pairs.Add((source, target));
                }
            }

            return pairs;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{BackendName}] Failed to get language pairs", Name);
            return new List<(string, string)>();
        }
    }
}
