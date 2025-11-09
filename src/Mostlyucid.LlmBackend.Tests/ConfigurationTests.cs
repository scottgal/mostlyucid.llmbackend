using Mostlyucid.LlmBackend.Configuration;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class ConfigurationTests
{
    [Fact]
    public void LlmBackendConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new LlmBackendConfig();

        // Assert
        Assert.Empty(config.Name);
        Assert.True(config.AutoDownloadModel);
        Assert.Equal(100, config.Priority);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void LlmBackendConfig_LlamaCppType_CanBeSet()
    {
        // Arrange & Act
        var config = new LlmBackendConfig
        {
            Type = LlmBackendType.LlamaCpp,
            ModelPath = "./models/test.gguf",
            ModelUrl = "https://example.com/model.gguf",
            ContextSize = 4096,
            GpuLayers = 20
        };

        // Assert
        Assert.Equal(LlmBackendType.LlamaCpp, config.Type);
        Assert.Equal("./models/test.gguf", config.ModelPath);
        Assert.Equal("https://example.com/model.gguf", config.ModelUrl);
        Assert.Equal(4096, config.ContextSize);
        Assert.Equal(20, config.GpuLayers);
    }

    [Fact]
    public void LlmSettings_DefaultSelectionStrategy_IsFailover()
    {
        // Arrange & Act
        var settings = new LlmSettings();

        // Assert
        Assert.Equal(BackendSelectionStrategy.Failover, settings.SelectionStrategy);
    }

    [Fact]
    public void LlmSettings_DefaultTimeout_Is120Seconds()
    {
        // Arrange & Act
        var settings = new LlmSettings();

        // Assert
        Assert.Equal(120, settings.TimeoutSeconds);
    }

    [Fact]
    public void LlmSettings_DefaultMaxRetries_Is3()
    {
        // Arrange & Act
        var settings = new LlmSettings();

        // Assert
        Assert.Equal(3, settings.MaxRetries);
    }

    [Fact]
    public void LlmSettings_CircuitBreakerDefaults_AreCorrect()
    {
        // Arrange & Act
        var settings = new LlmSettings();

        // Assert
        Assert.True(settings.CircuitBreaker.Enabled);
        Assert.Equal(5, settings.CircuitBreaker.FailureThreshold);
        Assert.Equal(30, settings.CircuitBreaker.DurationOfBreakSeconds);
    }

    [Fact]
    public void LlmSettings_PluginDefaults_AreCorrect()
    {
        // Arrange & Act
        var settings = new LlmSettings();

        // Assert
        Assert.True(settings.Plugins.Enabled);
        Assert.Equal("plugins", settings.Plugins.PluginDirectory);
        Assert.True(settings.Plugins.LoadOnStartup);
    }

    [Fact]
    public void BackendSelectionStrategy_AllValues_CanBeSet()
    {
        // Act & Assert
        Assert.Equal(BackendSelectionStrategy.Failover, BackendSelectionStrategy.Failover);
        Assert.Equal(BackendSelectionStrategy.RoundRobin, BackendSelectionStrategy.RoundRobin);
        Assert.Equal(BackendSelectionStrategy.Specific, BackendSelectionStrategy.Specific);
        Assert.Equal(BackendSelectionStrategy.LowestLatency, BackendSelectionStrategy.LowestLatency);
        Assert.Equal(BackendSelectionStrategy.Random, BackendSelectionStrategy.Random);
    }

    [Fact]
    public void LlmBackendType_IncludesLlamaCpp()
    {
        // Arrange
        var allTypes = Enum.GetValues<LlmBackendType>();

        // Assert
        Assert.Contains(LlmBackendType.LlamaCpp, allTypes);
    }

    [Fact]
    public void LlmBackendConfig_WithCompleteConfiguration_StoresAllValues()
    {
        // Arrange & Act
        var config = new LlmBackendConfig
        {
            Name = "Test-Backend",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ApiKey = "test-key",
            ModelName = "test-model",
            Temperature = 0.8,
            MaxInputTokens = 4096,
            MaxOutputTokens = 2000,
            TopP = 0.95,
            FrequencyPenalty = 0.5,
            PresencePenalty = 0.5,
            StopSequences = new List<string> { "</s>" },
            Priority = 1,
            Enabled = true,
            TimeoutSeconds = 60,
            MaxRetries = 5,
            AdditionalHeaders = new Dictionary<string, string> { { "Custom", "Header" } },
            EnableStreaming = true,
            EnableFunctionCalling = false,
            CostPerMillionInputTokens = 0.5m,
            CostPerMillionOutputTokens = 1.5m,
            // LlamaCpp-specific
            ModelPath = "./models/test.gguf",
            ModelUrl = "https://example.com/model.gguf",
            ContextSize = 8192,
            GpuLayers = 32,
            Threads = 8,
            AutoDownloadModel = true,
            UseMemoryLock = true,
            Seed = 42
        };

        // Assert
        Assert.Equal("Test-Backend", config.Name);
        Assert.Equal(LlmBackendType.LlamaCpp, config.Type);
        Assert.Equal("http://localhost:8080", config.BaseUrl);
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("test-model", config.ModelName);
        Assert.Equal(0.8, config.Temperature);
        Assert.Equal(4096, config.MaxInputTokens);
        Assert.Equal(2000, config.MaxOutputTokens);
        Assert.Equal(0.95, config.TopP);
        Assert.Equal(0.5, config.FrequencyPenalty);
        Assert.Equal(0.5, config.PresencePenalty);
        Assert.Single(config.StopSequences);
        Assert.Equal(1, config.Priority);
        Assert.True(config.Enabled);
        Assert.Equal(60, config.TimeoutSeconds);
        Assert.Equal(5, config.MaxRetries);
        Assert.Single(config.AdditionalHeaders);
        Assert.True(config.EnableStreaming);
        Assert.False(config.EnableFunctionCalling);
        Assert.Equal(0.5m, config.CostPerMillionInputTokens);
        Assert.Equal(1.5m, config.CostPerMillionOutputTokens);
        Assert.Equal("./models/test.gguf", config.ModelPath);
        Assert.Equal("https://example.com/model.gguf", config.ModelUrl);
        Assert.Equal(8192, config.ContextSize);
        Assert.Equal(32, config.GpuLayers);
        Assert.Equal(8, config.Threads);
        Assert.True(config.AutoDownloadModel);
        Assert.True(config.UseMemoryLock);
        Assert.Equal(42, config.Seed);
    }
}
