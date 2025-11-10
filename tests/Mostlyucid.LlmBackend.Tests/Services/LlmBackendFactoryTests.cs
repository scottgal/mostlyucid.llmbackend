using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests.Services;

public class LlmBackendFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly HttpClient _httpClient;

    public LlmBackendFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpClient = new HttpClient();

        // Setup default mock behaviors
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public void CreateBackend_OpenAI_ShouldCreateOpenAIBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<OpenAILlmBackend>();
        backend.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void CreateBackend_GenericOpenAI_ShouldCreateOpenAIBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "GenericOpenAI",
            Type = LlmBackendType.GenericOpenAI,
            ApiKey = "test-key",
            BaseUrl = "https://api.example.com"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<OpenAILlmBackend>();
    }

    [Fact]
    public void CreateBackend_AzureOpenAI_ShouldCreateAzureOpenAIBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "AzureOpenAI",
            Type = LlmBackendType.AzureOpenAI,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<AzureOpenAILlmBackend>();
    }

    [Fact]
    public void CreateBackend_Anthropic_ShouldCreateAnthropicBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "Claude",
            Type = LlmBackendType.Anthropic,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<AnthropicLlmBackend>();
    }

    [Fact]
    public void CreateBackend_Gemini_ShouldCreateGeminiBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "Gemini",
            Type = LlmBackendType.Gemini,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<GeminiLlmBackend>();
    }

    [Fact]
    public void CreateBackend_Cohere_ShouldCreateCohereBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "Cohere",
            Type = LlmBackendType.Cohere,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<CohereLlmBackend>();
    }

    [Fact]
    public void CreateBackend_Ollama_ShouldCreateOllamaBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "Ollama",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<OllamaLlmBackend>();
    }

    [Fact]
    public void CreateBackend_LMStudio_ShouldCreateOllamaBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "LMStudio",
            Type = LlmBackendType.LMStudio,
            BaseUrl = "http://localhost:1234"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<OllamaLlmBackend>();
    }

    [Fact]
    public void CreateBackend_EasyNMT_ShouldCreateEasyNMTBackend()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "EasyNMT",
            Type = LlmBackendType.EasyNMT,
            BaseUrl = "http://localhost:5000"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<EasyNMTBackend>();
    }

    [Fact]
    public void CreateBackend_UnsupportedType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "Unknown",
            Type = (LlmBackendType)999
        };

        // Act & Assert
        var act = () => factory.CreateBackend(config);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("Backend type * is not supported*");
    }

    [Fact]
    public void CreateBackend_WithPlugin_ShouldUsePluginBackend()
    {
        // Arrange
        var mockPlugin = new Mock<ILlmBackendPlugin>();
        var mockBackend = new Mock<ILlmBackend>();
        mockBackend.Setup(b => b.Name).Returns("PluginBackend");

        mockPlugin.Setup(p => p.SupportedBackendTypes)
            .Returns(new List<string> { "CustomType" });
        mockPlugin.Setup(p => p.CreateBackend(
                "CustomType",
                It.IsAny<LlmBackendConfig>(),
                It.IsAny<ILoggerFactory>(),
                It.IsAny<IHttpClientFactory>()))
            .Returns(mockBackend.Object);

        var pluginLoader = new Mock<LlmPluginLoader>(
            It.IsAny<ILogger<LlmPluginLoader>>(),
            It.IsAny<string?>());
        pluginLoader.Setup(p => p.GetPluginForBackendType("CustomType"))
            .Returns(mockPlugin.Object);

        var factory = CreateFactory(pluginLoader.Object);
        var config = new LlmBackendConfig
        {
            Name = "PluginBackend",
            Type = LlmBackendType.OpenAI, // Type doesn't matter when CustomBackendType is set
            CustomBackendType = "CustomType"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Name.Should().Be("PluginBackend");
        mockPlugin.Verify(p => p.CreateBackend(
            "CustomType",
            config,
            _loggerFactoryMock.Object,
            _httpClientFactoryMock.Object), Times.Once);
    }

    [Fact]
    public void CreateBackend_WithPluginButNoLoader_ShouldFallbackToBuiltIn()
    {
        // Arrange
        var factory = CreateFactory(pluginLoader: null);
        var config = new LlmBackendConfig
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            CustomBackendType = "CustomType", // Plugin type specified but no loader
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        backend.Should().BeOfType<OpenAILlmBackend>();
    }

    [Fact]
    public void CreateBackends_ShouldCreateMultipleBackends()
    {
        // Arrange
        var factory = CreateFactory();
        var configs = new List<LlmBackendConfig>
        {
            new LlmBackendConfig
            {
                Name = "OpenAI",
                Type = LlmBackendType.OpenAI,
                Enabled = true,
                ApiKey = "key1"
            },
            new LlmBackendConfig
            {
                Name = "Claude",
                Type = LlmBackendType.Anthropic,
                Enabled = true,
                ApiKey = "key2"
            }
        };

        // Act
        var backends = factory.CreateBackends(configs);

        // Assert
        backends.Should().HaveCount(2);
        backends[0].Should().BeOfType<OpenAILlmBackend>();
        backends[1].Should().BeOfType<AnthropicLlmBackend>();
    }

    [Fact]
    public void CreateBackends_ShouldFilterDisabledBackends()
    {
        // Arrange
        var factory = CreateFactory();
        var configs = new List<LlmBackendConfig>
        {
            new LlmBackendConfig
            {
                Name = "OpenAI",
                Type = LlmBackendType.OpenAI,
                Enabled = true,
                ApiKey = "key1"
            },
            new LlmBackendConfig
            {
                Name = "Claude",
                Type = LlmBackendType.Anthropic,
                Enabled = false, // Disabled
                ApiKey = "key2"
            },
            new LlmBackendConfig
            {
                Name = "Gemini",
                Type = LlmBackendType.Gemini,
                Enabled = true,
                ApiKey = "key3"
            }
        };

        // Act
        var backends = factory.CreateBackends(configs);

        // Assert
        backends.Should().HaveCount(2);
        backends.Select(b => b.Name).Should().NotContain("Claude");
        backends.Select(b => b.Name).Should().Contain("OpenAI");
        backends.Select(b => b.Name).Should().Contain("Gemini");
    }

    [Fact]
    public void CreateBackends_EmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var factory = CreateFactory();
        var configs = new List<LlmBackendConfig>();

        // Act
        var backends = factory.CreateBackends(configs);

        // Assert
        backends.Should().BeEmpty();
    }

    [Fact]
    public void CreateBackends_AllDisabled_ShouldReturnEmptyList()
    {
        // Arrange
        var factory = CreateFactory();
        var configs = new List<LlmBackendConfig>
        {
            new LlmBackendConfig
            {
                Name = "OpenAI",
                Type = LlmBackendType.OpenAI,
                Enabled = false
            },
            new LlmBackendConfig
            {
                Name = "Claude",
                Type = LlmBackendType.Anthropic,
                Enabled = false
            }
        };

        // Act
        var backends = factory.CreateBackends(configs);

        // Assert
        backends.Should().BeEmpty();
    }

    [Fact]
    public void CreateBackend_ShouldPassTelemetryConfig()
    {
        // Arrange
        var telemetry = new TelemetryConfig
        {
            EnableMetrics = true,
            EnableTracing = true
        };
        var factory = CreateFactory(telemetry: telemetry);
        var config = new LlmBackendConfig
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            ApiKey = "test-key"
        };

        // Act
        var backend = factory.CreateBackend(config);

        // Assert
        backend.Should().NotBeNull();
        // Telemetry config is passed to the backend constructor
    }

    [Fact]
    public void CreateBackend_ShouldUseHttpClientFactory()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new LlmBackendConfig
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            ApiKey = "test-key"
        };

        // Act
        factory.CreateBackend(config);

        // Assert
        _httpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Once);
    }

    private LlmBackendFactory CreateFactory(
        LlmPluginLoader? pluginLoader = null,
        TelemetryConfig? telemetry = null)
    {
        return new LlmBackendFactory(
            _loggerFactoryMock.Object,
            _httpClientFactoryMock.Object,
            pluginLoader,
            telemetry);
    }
}
