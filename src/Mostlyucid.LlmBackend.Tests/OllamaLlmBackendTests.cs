using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class OllamaLlmBackendTests
{
    private readonly Mock<ILogger<OllamaLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public OllamaLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<OllamaLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-Ollama",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434",
            ModelName = "llama3",
            Temperature = 0.7,
            MaxOutputTokens = 2000
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-Ollama", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/tags")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""models"": []}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenConnectionFails_ReturnsFalse()
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
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/generate")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""model"": ""llama3"",
                    ""response"": ""This is a test response from Ollama"",
                    ""done"": true
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

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
        Assert.Equal("This is a test response from Ollama", response.Text);
        Assert.True(response.DurationMs > 0);
    }

    [Fact]
    public async Task CompleteAsync_WithSystemMessage_IncludesInRequest()
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
                Content = new StringContent(@"{""response"": ""Response"", ""done"": true}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest
        {
            Prompt = "Test",
            SystemMessage = "You are a helpful assistant"
        };

        // Act
        await backend.CompleteAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("You are a helpful assistant", content);
    }

    [Fact]
    public async Task CompleteAsync_WithServerError_ReturnsErrorResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/chat")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""model"": ""llama3"",
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""Chat response from Ollama""
                    },
                    ""done"": true
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello Ollama" }
            }
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response from Ollama", response.Text);
        Assert.True(response.DurationMs > 0);
    }

    [Fact]
    public async Task ChatAsync_WithMultipleMessages_FormatsCorrectly()
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
                Content = new StringContent(@"{
                    ""message"": {""content"": ""Response""},
                    ""done"": true
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "You are helpful" },
                new() { Role = "user", Content = "First question" },
                new() { Role = "assistant", Content = "First answer" },
                new() { Role = "user", Content = "Second question" }
            }
        };

        // Act
        await backend.ChatAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"messages\"", content);
        Assert.Contains("system", content);
        Assert.Contains("user", content);
        Assert.Contains("assistant", content);
    }

    [Fact]
    public async Task ChatAsync_WithHttpException_ReturnsErrorResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var backend = new OllamaLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Test" }
            }
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("Request timeout", response.ErrorMessage);
    }
}
