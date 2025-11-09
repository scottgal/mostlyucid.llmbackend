using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class CohereLlmBackendTests
{
    private readonly Mock<ILogger<CohereLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public CohereLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<CohereLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-Cohere",
            Type = LlmBackendType.Cohere,
            BaseUrl = "https://api.cohere.ai",
            ApiKey = "test-api-key",
            ModelName = "command-r-plus",
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
        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-Cohere", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/models")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""models"": []}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

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
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/generate")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""id"": ""gen_123"",
                    ""generations"": [{
                        ""id"": ""gen_123_1"",
                        ""text"": ""This is a test response from Cohere"",
                        ""finish_reason"": ""COMPLETE""
                    }],
                    ""prompt"": ""Test prompt"",
                    ""meta"": {
                        ""billed_units"": {
                            ""input_tokens"": 15,
                            ""output_tokens"": 8
                        }
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

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
        Assert.Equal("This is a test response from Cohere", response.Text);
        Assert.Equal(23, response.TotalTokens);
        Assert.Equal(15, response.PromptTokens);
        Assert.Equal(8, response.CompletionTokens);
        Assert.Equal("COMPLETE", response.FinishReason);
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
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/chat")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""text"": ""Chat response from Cohere"",
                    ""generation_id"": ""chat_123"",
                    ""finish_reason"": ""COMPLETE"",
                    ""meta"": {
                        ""billed_units"": {
                            ""input_tokens"": 20,
                            ""output_tokens"": 10
                        }
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello Cohere" }
            },
            Temperature = 0.7
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response from Cohere", response.Text);
        Assert.Equal(30, response.TotalTokens);
        Assert.Equal(20, response.PromptTokens);
        Assert.Equal(10, response.CompletionTokens);
        Assert.Equal("COMPLETE", response.FinishReason);
    }

    [Fact]
    public async Task ChatAsync_WithMultipleMessages_FormatsHistoryCorrectly()
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
                    ""text"": ""Response"",
                    ""finish_reason"": ""COMPLETE"",
                    ""meta"": {""billed_units"": {""input_tokens"": 10, ""output_tokens"": 5}}
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "First message" },
                new() { Role = "assistant", Content = "First response" },
                new() { Role = "user", Content = "Second message" }
            }
        };

        // Act
        await backend.ChatAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"message\":\"Second message\"", content); // Last message is separate
        Assert.Contains("\"chat_history\"", content); // History should be present
        Assert.Contains("CHATBOT", content); // Assistant role converted to CHATBOT
    }

    [Fact]
    public async Task ChatAsync_WithNoMessages_ReturnsError()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>()
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("No messages", response.ErrorMessage);
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
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

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
        Assert.Contains("Network error", response.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAsync_WithHttpException_ReturnsErrorResponse()
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
            BaseAddress = new Uri("https://api.cohere.ai")
        };

        var backend = new CohereLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("Request timeout", response.ErrorMessage);
    }
}
