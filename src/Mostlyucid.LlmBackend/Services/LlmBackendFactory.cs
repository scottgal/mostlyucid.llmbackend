using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Factory for creating LLM backend instances
/// </summary>
public class LlmBackendFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmPluginLoader? _pluginLoader;
    private readonly TelemetryConfig? _telemetry;

    public LlmBackendFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        LlmPluginLoader? pluginLoader = null,
        TelemetryConfig? telemetry = null)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _pluginLoader = pluginLoader;
        _telemetry = telemetry;
    }

    public ILlmBackend CreateBackend(LlmBackendConfig config)
    {
        var httpClient = _httpClientFactory.CreateClient();

        // First, check if a plugin handles this backend type
        if (!string.IsNullOrEmpty(config.CustomBackendType) && _pluginLoader != null)
        {
            var plugin = _pluginLoader.GetPluginForBackendType(config.CustomBackendType);
            if (plugin != null)
            {
                return plugin.CreateBackend(
                    config.CustomBackendType,
                    config,
                    _loggerFactory,
                    _httpClientFactory);
            }
        }

        // Fall back to built-in backend types
        return config.Type switch
        {
            LlmBackendType.OpenAI or LlmBackendType.GenericOpenAI =>
                new OpenAILlmBackend(
                    config,
                    _loggerFactory.CreateLogger<OpenAILlmBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.AzureOpenAI =>
                new AzureOpenAILlmBackend(
                    config,
                    _loggerFactory.CreateLogger<AzureOpenAILlmBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.Ollama or LlmBackendType.LMStudio =>
                new OllamaLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<OllamaLlmBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.EasyNMT =>
                new EasyNMTBackend(
                    config,
                    _loggerFactory.CreateLogger<EasyNMTBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.Anthropic =>
                new AnthropicLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<AnthropicLlmBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.Gemini =>
                new GeminiLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<GeminiLlmBackend>(),
                    httpClient,
                    _telemetry),

            LlmBackendType.Cohere =>
                new CohereLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<CohereLlmBackend>(),
                    httpClient,
                    _telemetry),

            _ => throw new NotSupportedException($"Backend type {config.Type} is not supported. " +
                $"If this is a plugin backend, ensure CustomBackendType is set and the plugin is loaded.")
        };
    }

    public List<ILlmBackend> CreateBackends(List<LlmBackendConfig> configs)
    {
        return configs
            .Where(c => c.Enabled)
            .Select(CreateBackend)
            .ToList();
    }
}
