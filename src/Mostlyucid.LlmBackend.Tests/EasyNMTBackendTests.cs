using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Xunit;

namespace Mostlyucid.LlmBackend.Tests;

public class EasyNMTBackendTests
{
    private readonly Mock<ILogger<EasyNMTBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public EasyNMTBackendTests()
    {
        _loggerMock = new Mock<ILogger<EasyNMTBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-EasyNMT",
            Type = LlmBackendType.EasyNMT,
            BaseUrl = "http://localhost:24080"
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-EasyNMT", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("models")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"[""opus-mt"", ""m2m_100""]")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

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
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

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
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("translate")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""translation"": ""Ceci est un test""}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest
        {
            Prompt = "This is a test"
        };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Ceci est un test", response.Text);
        Assert.True(response.DurationMs > 0);
    }

    [Fact]
    public async Task CompleteAsync_WithAlternateResponseFormat_ReturnsSuccess()
    {
        // Arrange - Some EasyNMT versions return just a string
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"""Translated text""")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Translated text", response.Text);
    }

    [Fact]
    public async Task CompleteAsync_WhenPostFails_FallsBackToGet()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""translation"": ""Fallback translation""}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Fallback translation", response.Text);
    }

    [Fact]
    public async Task CompleteAsync_WhenBothRequestsFail_ReturnsError()
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
                Content = new StringContent("Translation service error")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("InternalServerError", response.ErrorMessage);
    }

    [Fact]
    public async Task ChatAsync_WithValidMessages_ExtractsLastUserMessage()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("translate")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""translation"": ""Bonjour""}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "First message" },
                new() { Role = "assistant", Content = "Response" },
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Bonjour", response.Text);
    }

    [Fact]
    public async Task ChatAsync_WithNoUserMessages_ReturnsError()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "System message only" }
            }
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("No user message", response.ErrorMessage);
    }

    [Fact]
    public async Task GetLanguagePairsAsync_WhenSuccessful_ReturnsPairs()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("language_pairs")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"[[""en"", ""fr""], [""en"", ""de""], [""fr"", ""en""]]")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var pairs = await backend.GetLanguagePairsAsync();

        // Assert
        Assert.NotEmpty(pairs);
        Assert.Equal(3, pairs.Count);
        Assert.Contains(("en", "fr"), pairs);
        Assert.Contains(("en", "de"), pairs);
        Assert.Contains(("fr", "en"), pairs);
    }

    [Fact]
    public async Task GetLanguagePairsAsync_WhenFails_ReturnsEmptyList()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection error"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var pairs = await backend.GetLanguagePairsAsync();

        // Assert
        Assert.Empty(pairs);
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
            BaseAddress = new Uri("http://localhost:24080")
        };

        var backend = new EasyNMTBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest { Prompt = "Test" };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("Request timeout", response.ErrorMessage);
    }
}
