using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class GeminiLlmBackendTests
{
    private readonly Mock<ILogger<GeminiLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public GeminiLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<GeminiLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-Gemini",
            Type = LlmBackendType.Gemini,
            BaseUrl = "https://generativelanguage.googleapis.com",
            ApiKey = "test-api-key",
            ModelName = "gemini-1.5-pro",
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
        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-Gemini", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("generateContent")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""candidates"": [{
                        ""content"": {
                            ""parts"": [{""text"": ""Hello""}]
                        },
                        ""finishReason"": ""STOP""
                    }],
                    ""usageMetadata"": {
                        ""promptTokenCount"": 5,
                        ""candidatesTokenCount"": 1,
                        ""totalTokenCount"": 6
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("generateContent")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""candidates"": [{
                        ""content"": {
                            ""parts"": [{
                                ""text"": ""This is a test response from Gemini""
                            }],
                            ""role"": ""model""
                        },
                        ""finishReason"": ""STOP""
                    }],
                    ""usageMetadata"": {
                        ""promptTokenCount"": 12,
                        ""candidatesTokenCount"": 8,
                        ""totalTokenCount"": 20
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
        Assert.Equal("This is a test response from Gemini", response.Text);
        Assert.Equal(20, response.TotalTokens);
        Assert.Equal(12, response.PromptTokens);
        Assert.Equal(8, response.CompletionTokens);
        Assert.Equal("STOP", response.FinishReason);
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
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("generateContent")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""candidates"": [{
                        ""content"": {
                            ""parts"": [{""text"": ""Chat response from Gemini""}]
                        },
                        ""finishReason"": ""STOP""
                    }],
                    ""usageMetadata"": {
                        ""promptTokenCount"": 18,
                        ""candidatesTokenCount"": 6,
                        ""totalTokenCount"": 24
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello Gemini" }
            },
            Temperature = 0.7
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response from Gemini", response.Text);
        Assert.Equal(24, response.TotalTokens);
        Assert.Equal(18, response.PromptTokens);
        Assert.Equal(6, response.CompletionTokens);
        Assert.Equal("STOP", response.FinishReason);
    }

    [Fact]
    public async Task ChatAsync_WithAssistantRole_ConvertsToModelRole()
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
                    ""candidates"": [{
                        ""content"": {""parts"": [{""text"": ""Response""}]},
                        ""finishReason"": ""STOP""
                    }],
                    ""usageMetadata"": {""totalTokenCount"": 10}
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi there" },
                new() { Role = "user", Content = "How are you?" }
            }
        };

        // Act
        await backend.ChatAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"role\":\"model\"", content); // assistant should be converted to model
        Assert.DoesNotContain("\"role\":\"assistant\"", content);
    }

    [Fact]
    public async Task ChatAsync_WithVertexAIConfig_UsesCorrectEndpoint()
    {
        // Arrange
        var vertexConfig = new LlmBackendConfig
        {
            Name = "Test-Vertex",
            Type = LlmBackendType.Gemini,
            BaseUrl = "https://us-central1-aiplatform.googleapis.com",
            ProjectId = "test-project-123",
            Location = "us-central1",
            ModelName = "gemini-1.5-pro"
        };

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
                    ""candidates"": [{
                        ""content"": {""parts"": [{""text"": ""Response""}]},
                        ""finishReason"": ""STOP""
                    }],
                    ""usageMetadata"": {""totalTokenCount"": 10}
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://us-central1-aiplatform.googleapis.com")
        };

        var backend = new GeminiLlmBackend(vertexConfig, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        await backend.CompleteAsync(request);

        // Assert
        Assert.NotNull(capturedRequest);
        var uri = capturedRequest!.RequestUri!.ToString();
        Assert.Contains("/v1/projects/test-project-123", uri);
        Assert.Contains("/locations/us-central1", uri);
        Assert.Contains("/publishers/google/models/gemini-1.5-pro", uri);
        Assert.Contains(":generateContent", uri);
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
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };

        var backend = new GeminiLlmBackend(_config, _loggerMock.Object, httpClient);

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
