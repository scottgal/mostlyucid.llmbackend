using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class LlamaCppLlmBackendTests
{
    private readonly Mock<ILogger<LlamaCppLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public LlamaCppLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<LlamaCppLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-LlamaCpp",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ModelName = "test-model",
            ModelPath = "./test-models/test.gguf",
            AutoDownloadModel = false, // Disable for unit tests
            ContextSize = 2048,
            Temperature = 0.7
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-LlamaCpp", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenHealthEndpointReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"ok\"}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenModelsEndpointReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/models")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\":[]}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenServerNotRunning_ReturnsFalse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("completion")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""content"": ""This is a test response"",
                    ""tokens_predicted"": 5,
                    ""tokens_evaluated"": 10
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest
        {
            Prompt = "Test prompt",
            MaxTokens = 100,
            Temperature = 0.7
        };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("This is a test response", response.Text);
        Assert.Equal(15, response.TotalTokens);
        Assert.Equal(10, response.PromptTokens);
        Assert.Equal(5, response.CompletionTokens);
        Assert.True(response.DurationMs > 0);
    }

    [Fact]
    public async Task CompleteAsync_WithServerError_ReturnsErrorResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("completion")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest
        {
            Prompt = "Test prompt",
            MaxTokens = 100
        };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("InternalServerError", response.ErrorMessage);
    }

    [Fact]
    public async Task ChatAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/chat/completions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""choices"": [{
                        ""message"": {
                            ""content"": ""Chat response""
                        },
                        ""finish_reason"": ""stop""
                    }],
                    ""usage"": {
                        ""total_tokens"": 20,
                        ""prompt_tokens"": 12,
                        ""completion_tokens"": 8
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Temperature = 0.7
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response", response.Text);
        Assert.Equal(20, response.TotalTokens);
        Assert.Equal(12, response.PromptTokens);
        Assert.Equal(8, response.CompletionTokens);
        Assert.Equal("stop", response.FinishReason);
    }

    [Fact]
    public void Configuration_ShouldRespectLlamaCppSpecificSettings()
    {
        // Arrange
        var config = new LlmBackendConfig
        {
            Name = "Test",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ModelPath = "./models/test.gguf",
            ModelUrl = "https://example.com/model.gguf",
            AutoDownloadModel = true,
            ContextSize = 4096,
            GpuLayers = 32,
            Threads = 8,
            UseMemoryLock = true,
            Seed = 42
        };

        // Act
        var httpClient = new HttpClient();
        var backend = new LlamaCppLlmBackend(config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test", backend.Name);
    }

    [Fact]
    public async Task CompleteAsync_ShouldIncludeContextSizeInRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedRequest = req;
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""content"": ""test"", ""tokens_predicted"": 1, ""tokens_evaluated"": 1}")
            });

        var config = new LlmBackendConfig
        {
            Name = "Test",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ContextSize = 8192,
            AutoDownloadModel = false
        };

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(config, _loggerMock.Object, httpClient);

        // Act
        await backend.CompleteAsync(new LlmRequest { Prompt = "Test" });

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"n_ctx\":8192", content);
    }
}
