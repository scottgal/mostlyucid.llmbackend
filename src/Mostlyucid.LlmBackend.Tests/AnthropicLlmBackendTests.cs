using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class AnthropicLlmBackendTests
{
    private readonly Mock<ILogger<AnthropicLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public AnthropicLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<AnthropicLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-Anthropic",
            Type = LlmBackendType.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = "test-api-key",
            ModelName = "claude-3-5-sonnet-20241022",
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
        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-Anthropic", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""id"": ""msg_test"",
                    ""content"": [{""text"": ""Hello""}],
                    ""stop_reason"": ""end_turn"",
                    ""usage"": {""input_tokens"": 10, ""output_tokens"": 1}
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiBadRequest_ReturnsTrue()
    {
        // Arrange - BadRequest is considered "available" (just wrong input)
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

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
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""id"": ""msg_123"",
                    ""type"": ""message"",
                    ""role"": ""assistant"",
                    ""content"": [{
                        ""type"": ""text"",
                        ""text"": ""This is a test response from Claude""
                    }],
                    ""model"": ""claude-3-5-sonnet-20241022"",
                    ""stop_reason"": ""end_turn"",
                    ""usage"": {
                        ""input_tokens"": 15,
                        ""output_tokens"": 8
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

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
        Assert.Equal("This is a test response from Claude", response.Text);
        Assert.Equal(23, response.TotalTokens);
        Assert.Equal(15, response.PromptTokens);
        Assert.Equal(8, response.CompletionTokens);
        Assert.Equal("end_turn", response.FinishReason);
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
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""id"": ""msg_chat123"",
                    ""content"": [{
                        ""type"": ""text"",
                        ""text"": ""Chat response from Claude""
                    }],
                    ""stop_reason"": ""stop"",
                    ""usage"": {
                        ""input_tokens"": 20,
                        ""output_tokens"": 10
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello Claude" }
            },
            Temperature = 0.7
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response from Claude", response.Text);
        Assert.Equal(30, response.TotalTokens);
        Assert.Equal(20, response.PromptTokens);
        Assert.Equal(10, response.CompletionTokens);
        Assert.Equal("stop", response.FinishReason);
    }

    [Fact]
    public async Task ChatAsync_WithSystemMessage_SeparatesSystemFromMessages()
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
                    ""content"": [{""text"": ""Response""}],
                    ""usage"": {""input_tokens"": 10, ""output_tokens"": 5}
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant" },
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Act
        await backend.ChatAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"system\":\"You are a helpful assistant\"", content);
        Assert.DoesNotContain("\"role\":\"system\"", content); // System should be separate
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
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var backend = new AnthropicLlmBackend(_config, _loggerMock.Object, httpClient);

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
}
