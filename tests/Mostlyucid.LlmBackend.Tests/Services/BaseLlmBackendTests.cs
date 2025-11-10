using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests.Services;

public class BaseLlmBackendTests
{
    private readonly Mock<ILogger<TestLlmBackend>> _loggerMock;
    private readonly HttpClient _httpClient;

    public BaseLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<TestLlmBackend>>();
        _httpClient = new HttpClient();
    }

    [Fact]
    public void Constructor_ShouldSetNameFromConfig()
    {
        // Arrange & Act
        var backend = CreateBackend("TestBackend");

        // Assert
        backend.Name.Should().Be("TestBackend");
    }

    [Fact]
    public void Constructor_ShouldInitializeBudgetTracking()
    {
        // Arrange & Act
        var backend = CreateBackend("Test", maxSpendUsd: 100m);
        var (currentSpend, maxSpend, exceeded, periodStart) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(0);
        maxSpend.Should().Be(100m);
        exceeded.Should().BeFalse();
        periodStart.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetHealthAsync_InitialState_ShouldReturnHealthy()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        var health = await backend.GetHealthAsync();

        // Assert
        health.IsHealthy.Should().BeTrue();
        health.SuccessfulRequests.Should().Be(0);
        health.FailedRequests.Should().Be(0);
        health.AverageLatencyMs.Should().Be(0);
    }

    [Fact]
    public async Task RecordSuccess_ShouldUpdateHealthMetrics()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        backend.TestRecordSuccess(100);
        backend.TestRecordSuccess(200);
        var health = await backend.GetHealthAsync();

        // Assert
        health.IsHealthy.Should().BeTrue();
        health.SuccessfulRequests.Should().Be(2);
        health.FailedRequests.Should().Be(0);
        health.AverageLatencyMs.Should().Be(150); // (100 + 200) / 2
        health.LastSuccessfulRequest.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordFailure_ShouldUpdateHealthMetrics()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        backend.TestRecordFailure("Test error");
        var health = await backend.GetHealthAsync();

        // Assert
        health.IsHealthy.Should().BeFalse(); // Failed requests without any successful ones
        health.SuccessfulRequests.Should().Be(0);
        health.FailedRequests.Should().Be(1);
        health.LastError.Should().Be("Test error");
    }

    [Fact]
    public async Task RecordSuccess_ShouldKeepLast100Latencies()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act - Record more than 100 latencies
        for (int i = 0; i < 150; i++)
        {
            backend.TestRecordSuccess(100);
        }

        var health = await backend.GetHealthAsync();

        // Assert - Should still calculate average correctly
        health.AverageLatencyMs.Should().Be(100);
        health.SuccessfulRequests.Should().Be(150);
    }

    [Fact]
    public void IsWithinBudget_NoBudgetSet_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: null);

        // Act
        var isWithinBudget = backend.TestIsWithinBudget();

        // Assert
        isWithinBudget.Should().BeTrue();
    }

    [Fact]
    public void IsWithinBudget_UnderBudget_ShouldReturnTrue()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 10m);

        // Act
        backend.TestRecordSpend(5m);
        var isWithinBudget = backend.TestIsWithinBudget();

        // Assert
        isWithinBudget.Should().BeTrue();
    }

    [Fact]
    public void IsWithinBudget_BudgetExceeded_ShouldReturnFalse()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 10m);

        // Act
        backend.TestRecordSpend(10m);
        var isWithinBudget = backend.TestIsWithinBudget();

        // Assert
        isWithinBudget.Should().BeFalse();
    }

    [Fact]
    public void RecordSpend_ShouldUpdateCurrentSpend()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m);

        // Act
        backend.TestRecordSpend(25m);
        backend.TestRecordSpend(30m);
        var (currentSpend, _, _, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(55m);
    }

    [Fact]
    public void RecordSpend_WhenExceedsBudget_ShouldSetBudgetExceededFlag()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 50m);

        // Act
        backend.TestRecordSpend(30m);
        var (_, _, exceeded1, _) = backend.GetBudgetStatus();
        backend.TestRecordSpend(25m);
        var (_, _, exceeded2, _) = backend.GetBudgetStatus();

        // Assert
        exceeded1.Should().BeFalse();
        exceeded2.Should().BeTrue();
    }

    [Fact]
    public void RecordSpend_NegativeAmount_ShouldNotUpdateSpend()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m);

        // Act
        backend.TestRecordSpend(-10m);
        var (currentSpend, _, _, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(0);
    }

    [Fact]
    public void CreateSuccessResponse_ShouldCalculateCost()
    {
        // Arrange
        var config = new LlmBackendConfig
        {
            Name = "Test",
            CostPerMillionInputTokens = 1m, // $1 per million input tokens
            CostPerMillionOutputTokens = 2m // $2 per million output tokens
        };
        var backend = new TestLlmBackend(config, _loggerMock.Object, _httpClient);

        // Act
        var response = backend.TestCreateSuccessResponse(
            text: "Test response",
            durationMs: 100,
            model: "test-model",
            totalTokens: 2000,
            promptTokens: 1000,
            completionTokens: 1000);

        // Assert
        response.Success.Should().BeTrue();
        response.Text.Should().Be("Test response");
        response.Backend.Should().Be("Test");
        response.Model.Should().Be("test-model");
        response.DurationMs.Should().Be(100);
        response.TotalTokens.Should().Be(2000);
        response.PromptTokens.Should().Be(1000);
        response.CompletionTokens.Should().Be(1000);

        // Cost: (1000 * 1 / 1,000,000) + (1000 * 2 / 1,000,000) = 0.001 + 0.002 = 0.003
        var (currentSpend, _, _, _) = backend.GetBudgetStatus();
        currentSpend.Should().Be(0.003m);
    }

    [Fact]
    public void CreateSuccessResponse_WithoutPricing_ShouldNotCalculateCost()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        var response = backend.TestCreateSuccessResponse(
            text: "Test response",
            durationMs: 100,
            promptTokens: 1000,
            completionTokens: 1000);

        // Assert
        response.Success.Should().BeTrue();
        var (currentSpend, _, _, _) = backend.GetBudgetStatus();
        currentSpend.Should().Be(0);
    }

    [Fact]
    public void CreateErrorResponse_ShouldRecordFailure()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        var response = backend.TestCreateErrorResponse("Test error");

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Test error");
        response.Backend.Should().Be("Test");
    }

    [Fact]
    public async Task CreateErrorResponse_ShouldUpdateHealthMetrics()
    {
        // Arrange
        var backend = CreateBackend("Test");

        // Act
        backend.TestCreateErrorResponse("Test error");
        var health = await backend.GetHealthAsync();

        // Assert
        health.FailedRequests.Should().Be(1);
        health.LastError.Should().Be("Test error");
    }

    [Fact]
    public void BudgetReset_Daily_ShouldResetAfterMidnight()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m, spendResetPeriod: SpendResetPeriod.Daily);
        backend.TestRecordSpend(50m);

        // Act - Simulate day change by setting spend period start to yesterday
        backend.TestSetSpendPeriodStart(DateTime.UtcNow.AddDays(-1));
        backend.TestCheckBudgetReset();
        var (currentSpend, _, exceeded, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(0);
        exceeded.Should().BeFalse();
    }

    [Fact]
    public void BudgetReset_Weekly_ShouldResetAfter7Days()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m, spendResetPeriod: SpendResetPeriod.Weekly);
        backend.TestRecordSpend(50m);

        // Act - Simulate week change
        backend.TestSetSpendPeriodStart(DateTime.UtcNow.AddDays(-8));
        backend.TestCheckBudgetReset();
        var (currentSpend, _, exceeded, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(0);
        exceeded.Should().BeFalse();
    }

    [Fact]
    public void BudgetReset_Monthly_ShouldResetInNewMonth()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m, spendResetPeriod: SpendResetPeriod.Monthly);
        backend.TestRecordSpend(50m);

        // Act - Simulate month change
        backend.TestSetSpendPeriodStart(DateTime.UtcNow.AddMonths(-1));
        backend.TestCheckBudgetReset();
        var (currentSpend, _, exceeded, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(0);
        exceeded.Should().BeFalse();
    }

    [Fact]
    public void BudgetReset_Never_ShouldNotReset()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 100m, spendResetPeriod: SpendResetPeriod.Never);
        backend.TestRecordSpend(50m);

        // Act - Simulate time passing
        backend.TestSetSpendPeriodStart(DateTime.UtcNow.AddDays(-30));
        backend.TestCheckBudgetReset();
        var (currentSpend, _, _, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(50m); // Should not reset
    }

    [Fact]
    public void IsTelemetryEnabled_WithNullTelemetry_ShouldReturnFalse()
    {
        // Arrange
        var backend = CreateBackend("Test", telemetry: null);

        // Act
        var isEnabled = backend.TestIsTelemetryEnabled(t => t.EnableMetrics);

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsTelemetryEnabled_WithEnabledMetrics_ShouldReturnTrue()
    {
        // Arrange
        var telemetry = new TelemetryConfig { EnableMetrics = true };
        var backend = CreateBackend("Test", telemetry: telemetry);

        // Act
        var isEnabled = backend.TestIsTelemetryEnabled(t => t.EnableMetrics);

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsTelemetryEnabled_WithDisabledMetrics_ShouldReturnFalse()
    {
        // Arrange
        var telemetry = new TelemetryConfig { EnableMetrics = false };
        var backend = CreateBackend("Test", telemetry: telemetry);

        // Act
        var isEnabled = backend.TestIsTelemetryEnabled(t => t.EnableMetrics);

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigureHttpClient_WithBaseUrl_ShouldSetBaseAddress()
    {
        // Arrange
        var config = new LlmBackendConfig
        {
            Name = "Test",
            BaseUrl = "https://api.example.com"
        };
        var httpClient = new HttpClient();
        var backend = new TestLlmBackend(config, _loggerMock.Object, httpClient);

        // Assert
        httpClient.BaseAddress.Should().NotBeNull();
        httpClient.BaseAddress!.ToString().Should().Be("https://api.example.com/");
    }

    [Fact]
    public void ConfigureHttpClient_WithMaxInputTokens_ShouldSetTimeout()
    {
        // Arrange
        var config = new LlmBackendConfig
        {
            Name = "Test",
            MaxInputTokens = 60
        };
        var httpClient = new HttpClient();
        var backend = new TestLlmBackend(config, _loggerMock.Object, httpClient);

        // Assert
        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void BudgetTracking_ThreadSafe_ShouldHandleConcurrentSpends()
    {
        // Arrange
        var backend = CreateBackend("Test", maxSpendUsd: 1000m);

        // Act - Simulate concurrent spends
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => backend.TestRecordSpend(1m)))
            .ToArray();
        Task.WaitAll(tasks);

        var (currentSpend, _, _, _) = backend.GetBudgetStatus();

        // Assert
        currentSpend.Should().Be(100m);
    }

    private TestLlmBackend CreateBackend(
        string name,
        decimal? maxSpendUsd = null,
        SpendResetPeriod? spendResetPeriod = null,
        TelemetryConfig? telemetry = null)
    {
        var config = new LlmBackendConfig
        {
            Name = name,
            MaxSpendUsd = maxSpendUsd,
            SpendResetPeriod = spendResetPeriod ?? SpendResetPeriod.Never
        };

        return new TestLlmBackend(config, _loggerMock.Object, _httpClient, telemetry);
    }
}

// Test implementation of BaseLlmBackend for testing purposes
public class TestLlmBackend : BaseLlmBackend
{
    public TestLlmBackend(
        LlmBackendConfig config,
        ILogger logger,
        HttpClient httpClient,
        TelemetryConfig? telemetry = null)
        : base(config, logger, httpClient, telemetry)
    {
    }

    public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public override Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmResponse { Success = true, Text = "Test response" });
    }

    public override Task<LlmResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmResponse { Success = true, Text = "Test chat response" });
    }

    // Expose protected methods for testing
    public void TestRecordSuccess(long latencyMs) => RecordSuccess(latencyMs);
    public void TestRecordFailure(string error) => RecordFailure(error);
    public bool TestIsWithinBudget() => IsWithinBudget();
    public void TestRecordSpend(decimal amount) => RecordSpend(amount);
    public bool TestIsTelemetryEnabled(Func<TelemetryConfig, bool> predicate) => IsTelemetryEnabled(predicate);

    public LlmResponse TestCreateSuccessResponse(
        string text,
        long durationMs,
        string? model = null,
        int? totalTokens = null,
        int? promptTokens = null,
        int? completionTokens = null,
        string? finishReason = null)
    {
        return CreateSuccessResponse(text, durationMs, model, totalTokens, promptTokens, completionTokens, finishReason);
    }

    public LlmResponse TestCreateErrorResponse(string error, Exception? exception = null)
    {
        return CreateErrorResponse(error, exception);
    }

    public void TestSetSpendPeriodStart(DateTime date)
    {
        // Use reflection to set private field
        var field = typeof(BaseLlmBackend).GetField("_spendPeriodStart",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, date);
    }

    public void TestCheckBudgetReset()
    {
        // Trigger budget check by calling IsWithinBudget
        IsWithinBudget();
    }
}
