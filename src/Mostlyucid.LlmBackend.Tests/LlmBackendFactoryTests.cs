using Microsoft.Extensions.Logging;
using Moq;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class LlmBackendFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public LlmBackendFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Setup mocks to return mock logger and HttpClient
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
    }

    [Fact]
    public void CreateBackend_WithLlamaCppType_ReturnsLlamaCppBackend()
    {
        // Arrange
        var factory = new LlmBackendFactory(_loggerFactoryMock.Object, _httpClientFactoryMock.Object);
        var config = new LlmBackendConfig
        {
            Name = "Test",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ModelPath = "./models/test.gguf",
            AutoDownloadModel = false
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        Assert.NotNull(backend);
        Assert.IsType<LlamaCppLlmBackend>(backend);
        Assert.Equal("Test", backend.Name);
    }

    [Fact]
    public void CreateBackend_WithOpenAIType_ReturnsOpenAIBackend()
    {
        // Arrange
        var factory = new LlmBackendFactory(_loggerFactoryMock.Object, _httpClientFactoryMock.Object);
        var config = new LlmBackendConfig
        {
            Name = "Test",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        Assert.NotNull(backend);
        Assert.IsType<OpenAILlmBackend>(backend);
    }

    [Fact]
    public void CreateBackend_WithOllamaType_ReturnsOllamaBackend()
    {
        // Arrange
        var factory = new LlmBackendFactory(_loggerFactoryMock.Object, _httpClientFactoryMock.Object);
        var config = new LlmBackendConfig
        {
            Name = "Test",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        Assert.NotNull(backend);
        Assert.IsType<OllamaLlmBackend>(backend);
    }

    [Fact]
    public void CreateBackends_WithMultipleConfigs_ReturnsAllBackends()
    {
        // Arrange
        var factory = new LlmBackendFactory(_loggerFactoryMock.Object, _httpClientFactoryMock.Object);
        var configs = new List<LlmBackendConfig>
        {
            new() { Name = "LlamaCpp", Type = LlmBackendType.LlamaCpp, BaseUrl = "http://localhost:8080", Enabled = true, AutoDownloadModel = false },
            new() { Name = "OpenAI", Type = LlmBackendType.OpenAI, BaseUrl = "https://api.openai.com", Enabled = true },
            new() { Name = "Disabled", Type = LlmBackendType.Ollama, BaseUrl = "http://localhost:11434", Enabled = false }
        };

        // Act
        var backends = factory.CreateBackends(configs);

        // Assert
        Assert.Equal(2, backends.Count); // Only enabled backends
        Assert.Contains(backends, b => b.Name == "LlamaCpp");
        Assert.Contains(backends, b => b.Name == "OpenAI");
        Assert.DoesNotContain(backends, b => b.Name == "Disabled");
    }

    [Fact]
    public void CreateBackend_WithAllLlamaCppOptions_ConfiguresCorrectly()
    {
        // Arrange
        var factory = new LlmBackendFactory(_loggerFactoryMock.Object, _httpClientFactoryMock.Object);
        var config = new LlmBackendConfig
        {
            Name = "Test-Full-Config",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ModelName = "llama-3-8b",
            ModelPath = "./models/llama-3-8b-q4.gguf",
            ModelUrl = "https://example.com/model.gguf",
            AutoDownloadModel = true,
            ContextSize = 8192,
            GpuLayers = 32,
            Threads = 8,
            UseMemoryLock = true,
            Seed = 42,
            Temperature = 0.8,
            MaxOutputTokens = 4000,
            TopP = 0.95,
            FrequencyPenalty = 0.5,
            PresencePenalty = 0.5,
            StopSequences = new List<string> { "</s>", "[INST]" }
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        Assert.NotNull(backend);
        Assert.IsType<LlamaCppLlmBackend>(backend);
        Assert.Equal("Test-Full-Config", backend.Name);
    }
}
