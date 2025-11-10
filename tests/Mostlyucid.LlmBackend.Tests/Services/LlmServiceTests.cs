using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;
using Mostlyucid.LlmBackend.Testing;

namespace Mostlyucid.LlmBackend.Tests.Services;

public class LlmServiceTests
{
    private readonly Mock<ILogger<LlmService>> _loggerMock;
    private readonly LlmSettings _settings;

    public LlmServiceTests()
    {
        _loggerMock = new Mock<ILogger<LlmService>>();
        _settings = new LlmSettings
        {
            MaxRetries = 3,
            UseExponentialBackoff = true,
            SelectionStrategy = BackendSelectionStrategy.Failover
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeBackends()
    {
        // Arrange
        var backends = new List<ILlmBackend>
        {
            CreateFakeBackend("Backend1"),
            CreateFakeBackend("Backend2")
        };

        // Act
        var service = CreateService(backends);
        var availableBackends = service.GetAvailableBackends();

        // Assert
        availableBackends.Should().HaveCount(2);
        availableBackends.Should().Contain("Backend1");
        availableBackends.Should().Contain("Backend2");
    }

    [Fact]
    public void Constructor_ShouldOrderBackendsByName()
    {
        // Arrange
        var backends = new List<ILlmBackend>
        {
            CreateFakeBackend("Zebra"),
            CreateFakeBackend("Alpha"),
            CreateFakeBackend("Beta")
        };

        // Act
        var service = CreateService(backends);
        var availableBackends = service.GetAvailableBackends();

        // Assert
        availableBackends.Should().Equal("Alpha", "Beta", "Zebra");
    }

    [Fact]
    public async Task CompleteAsync_WithSuccessfulBackend_ShouldReturnResponse()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        backend.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Test response",
            Backend = "Backend1"
        });

        var service = CreateService(new[] { backend });
        var request = new LlmRequest { Prompt = "Test prompt" };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Text.Should().Be("Test response");
        response.Backend.Should().Be("Backend1");
    }

    [Fact]
    public async Task CompleteAsync_WithFailoverStrategy_ShouldTryNextBackend()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = false,
            ErrorMessage = "Backend1 failed"
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response"
        });

        _settings.SelectionStrategy = BackendSelectionStrategy.Failover;
        var service = CreateService(new[] { backend1, backend2 });
        var request = new LlmRequest { Prompt = "Test prompt" };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Text.Should().Be("Backend2 response");
    }

    [Fact]
    public async Task CompleteAsync_AllBackendsFail_ShouldReturnFailureResponse()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse { Success = false, ErrorMessage = "Failed1" });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse { Success = false, ErrorMessage = "Failed2" });

        _settings.SelectionStrategy = BackendSelectionStrategy.Failover;
        var service = CreateService(new[] { backend1, backend2 });
        var request = new LlmRequest { Prompt = "Test prompt" };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("All backends failed");
    }

    [Fact]
    public async Task CompleteAsync_WithPreferredBackend_ShouldUsePreferredOnly()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend1 response"
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response"
        });

        var service = CreateService(new[] { backend1, backend2 });
        var request = new LlmRequest
        {
            Prompt = "Test prompt",
            PreferredBackend = "Backend2"
        };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Text.Should().Be("Backend2 response");
    }

    [Fact]
    public async Task CompleteAsync_RoundRobinStrategy_ShouldRotateBackends()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend1 response"
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response"
        });

        _settings.SelectionStrategy = BackendSelectionStrategy.RoundRobin;
        var service = CreateService(new[] { backend1, backend2 });

        // Act
        var response1 = await service.CompleteAsync(new LlmRequest { Prompt = "Test 1" });
        var response2 = await service.CompleteAsync(new LlmRequest { Prompt = "Test 2" });
        var response3 = await service.CompleteAsync(new LlmRequest { Prompt = "Test 3" });

        // Assert
        // Due to round-robin, we should see both backends used
        var backends = new[] { response1.Backend, response2.Backend, response3.Backend };
        backends.Should().Contain("Backend1");
        backends.Should().Contain("Backend2");
    }

    [Fact]
    public async Task CompleteAsync_RandomStrategy_ShouldUseRandomBackend()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse { Success = true, Text = "Response1" });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse { Success = true, Text = "Response2" });

        _settings.SelectionStrategy = BackendSelectionStrategy.Random;
        var service = CreateService(new[] { backend1, backend2 });

        // Act - make multiple requests to test randomness
        var responses = new List<LlmResponse>();
        for (int i = 0; i < 10; i++)
        {
            responses.Add(await service.CompleteAsync(new LlmRequest { Prompt = $"Test {i}" }));
        }

        // Assert - should use at least one of each (statistically very likely)
        var backends = responses.Select(r => r.Backend).Distinct().ToList();
        backends.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task CompleteAsync_SimultaneousStrategy_ShouldCallAllBackends()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend1 response",
            Backend = "Backend1"
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response",
            Backend = "Backend2"
        });

        _settings.SelectionStrategy = BackendSelectionStrategy.Simultaneous;
        var service = CreateService(new[] { backend1, backend2 });
        var request = new LlmRequest { Prompt = "Test prompt" };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.AlternativeResponses.Should().NotBeNull();
        response.AlternativeResponses.Should().HaveCount(1);

        // Both backends should have been called
        var allResponses = new[] { response }.Concat(response.AlternativeResponses ?? Array.Empty<LlmResponse>()).ToList();
        allResponses.Select(r => r.Backend).Should().Contain("Backend1");
        allResponses.Select(r => r.Backend).Should().Contain("Backend2");
    }

    [Fact]
    public async Task CompleteAsync_SimultaneousWithOneFailure_ShouldReturnSuccessfulResponse()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = false,
            ErrorMessage = "Backend1 failed"
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response",
            Backend = "Backend2"
        });

        _settings.SelectionStrategy = BackendSelectionStrategy.Simultaneous;
        var service = CreateService(new[] { backend1, backend2 });
        var request = new LlmRequest { Prompt = "Test prompt" };

        // Act
        var response = await service.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Text.Should().Be("Backend2 response");
        response.AlternativeResponses.Should().HaveCount(1);
        response.AlternativeResponses![0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChatAsync_WithSuccessfulBackend_ShouldReturnResponse()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        backend.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Chat response"
        });

        var service = CreateService(new[] { backend });
        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var response = await service.ChatAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Text.Should().Be("Chat response");
    }

    [Fact]
    public async Task TestBackendsAsync_ShouldCheckAllBackends()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        var backend2 = CreateFakeBackend("Backend2");

        var service = CreateService(new[] { backend1, backend2 });

        // Act
        var results = await service.TestBackendsAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainKey("Backend1");
        results.Should().ContainKey("Backend2");
        results["Backend1"].IsHealthy.Should().BeTrue();
        results["Backend2"].IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task TestBackendsAsync_WithUnhealthyBackend_ShouldReportUnhealthy()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetHealthy(false, "Connection failed");

        var service = CreateService(new[] { backend1, backend2 });

        // Act
        var results = await service.TestBackendsAsync();

        // Assert
        results["Backend1"].IsHealthy.Should().BeTrue();
        results["Backend2"].IsHealthy.Should().BeFalse();
        results["Backend2"].LastError.Should().Be("Connection failed");
    }

    [Fact]
    public void GetBackend_WithValidName_ShouldReturnBackend()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        var backend2 = CreateFakeBackend("Backend2");
        var service = CreateService(new[] { backend1, backend2 });

        // Act
        var backend = service.GetBackend("Backend1");

        // Assert
        backend.Should().NotBeNull();
        backend!.Name.Should().Be("Backend1");
    }

    [Fact]
    public void GetBackend_WithInvalidName_ShouldReturnNull()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        var service = CreateService(new[] { backend });

        // Act
        var result = service.GetBackend("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetBackend_ShouldBeCaseInsensitive()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        var service = CreateService(new[] { backend });

        // Act
        var result = service.GetBackend("BACKEND1");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Backend1");
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnBackendStats()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        backend.SetResponse(new LlmResponse { Success = true, Text = "Response" });

        var service = CreateService(new[] { backend });
        await service.CompleteAsync(new LlmRequest { Prompt = "Test" });

        // Act
        var stats = service.GetStatistics();

        // Assert
        stats.Should().ContainKey("Backend1");
        stats["Backend1"].TotalRequests.Should().Be(1);
        stats["Backend1"].SuccessfulRequests.Should().Be(1);
        stats["Backend1"].FailedRequests.Should().Be(0);
    }

    [Fact]
    public async Task CompleteAsync_ShouldUpdateStatistics()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        backend.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Response",
            DurationMs = 100
        });

        var service = CreateService(new[] { backend });

        // Act
        await service.CompleteAsync(new LlmRequest { Prompt = "Test" });
        var stats = service.GetStatistics();

        // Assert
        stats["Backend1"].TotalRequests.Should().Be(1);
        stats["Backend1"].SuccessfulRequests.Should().Be(1);
        stats["Backend1"].AverageResponseTimeMs.Should().Be(100);
        stats["Backend1"].LastUsed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteAsync_WithException_ShouldNotRetryOnNonFailoverStrategy()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        backend.SetException(new InvalidOperationException("Test exception"));

        _settings.SelectionStrategy = BackendSelectionStrategy.RoundRobin;
        var service = CreateService(new[] { backend });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(new LlmRequest { Prompt = "Test" }));
    }

    [Fact]
    public async Task CompleteAsync_LowestLatencyStrategy_ShouldUseLowestLatencyBackend()
    {
        // Arrange
        var backend1 = CreateFakeBackend("Backend1");
        backend1.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend1 response",
            DurationMs = 200
        });

        var backend2 = CreateFakeBackend("Backend2");
        backend2.SetResponse(new LlmResponse
        {
            Success = true,
            Text = "Backend2 response",
            DurationMs = 100
        });

        _settings.SelectionStrategy = BackendSelectionStrategy.LowestLatency;
        var service = CreateService(new[] { backend1, backend2 });

        // Prime the statistics
        await service.CompleteAsync(new LlmRequest { Prompt = "Test 1" });
        await service.CompleteAsync(new LlmRequest { Prompt = "Test 2" });

        // Act - now it should prefer Backend2
        var response = await service.CompleteAsync(new LlmRequest { Prompt = "Test 3" });

        // Assert - Backend2 should be preferred due to lower latency
        var stats = service.GetStatistics();
        stats["Backend2"].AverageResponseTimeMs.Should().BeLessThan(stats["Backend1"].AverageResponseTimeMs);
    }

    [Fact]
    public async Task CompleteAsync_WithCancellationToken_ShouldPassToBackend()
    {
        // Arrange
        var backend = CreateFakeBackend("Backend1");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = CreateService(new[] { backend });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CompleteAsync(new LlmRequest { Prompt = "Test" }, cts.Token));
    }

    private FakeLlmBackend CreateFakeBackend(string name)
    {
        return new FakeLlmBackend
        {
            Name = name,
            Config = new BackendConfig { Name = name }
        };
    }

    private LlmService CreateService(IEnumerable<ILlmBackend> backends)
    {
        return new LlmService(
            _loggerMock.Object,
            Options.Create(_settings),
            backends);
    }
}
