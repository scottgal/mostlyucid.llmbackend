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

    public LlmBackendFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public ILlmBackend CreateBackend(LlmBackendConfig config)
    {
        var httpClient = _httpClientFactory.CreateClient();

        return config.Type switch
        {
            LlmBackendType.OpenAI or LlmBackendType.GenericOpenAI =>
                new OpenAILlmBackend(
                    config,
                    _loggerFactory.CreateLogger<OpenAILlmBackend>(),
                    httpClient),

            LlmBackendType.AzureOpenAI =>
                new AzureOpenAILlmBackend(
                    config,
                    _loggerFactory.CreateLogger<AzureOpenAILlmBackend>(),
                    httpClient),

            LlmBackendType.Ollama or LlmBackendType.LMStudio =>
                new OllamaLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<OllamaLlmBackend>(),
                    httpClient),

            LlmBackendType.EasyNMT =>
                new EasyNMTBackend(
                    config,
                    _loggerFactory.CreateLogger<EasyNMTBackend>(),
                    httpClient),

            LlmBackendType.Anthropic =>
                new AnthropicLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<AnthropicLlmBackend>(),
                    httpClient),

            LlmBackendType.Gemini =>
                new GeminiLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<GeminiLlmBackend>(),
                    httpClient),

            LlmBackendType.Cohere =>
                new CohereLlmBackend(
                    config,
                    _loggerFactory.CreateLogger<CohereLlmBackend>(),
                    httpClient),

            _ => throw new NotSupportedException($"Backend type {config.Type} is not supported")
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
