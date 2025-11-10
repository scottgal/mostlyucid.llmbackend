using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Base class for LLM backend implementations
/// </summary>
public abstract class BaseLlmBackend : ILlmBackend
{
    protected readonly LlmBackendConfig Config;
    protected readonly ILogger Logger;
    protected readonly HttpClient HttpClient;
    protected readonly TelemetryConfig? Telemetry;

    private readonly ConcurrentBag<long> _latencies = new();
    private int _successfulRequests;
    private int _failedRequests;
    private string? _lastError;
    private DateTime? _lastSuccessfulRequest;

    // Budget tracking
    private decimal _currentSpend;
    private DateTime _spendPeriodStart;
    private bool _budgetExceeded;
    private readonly object _budgetLock = new();

    protected BaseLlmBackend(
        LlmBackendConfig config,
        ILogger logger,
        HttpClient httpClient,
        TelemetryConfig? telemetry = null)
    {
        Config = config;
        Logger = logger;
        HttpClient = httpClient;
        Telemetry = telemetry;
        _spendPeriodStart = DateTime.UtcNow;
        ConfigureHttpClient();
    }

    /// <summary>
    /// Check if a specific telemetry feature is enabled
    /// </summary>
    protected bool IsTelemetryEnabled(Func<TelemetryConfig, bool> predicate)
    {
        return Telemetry != null && predicate(Telemetry);
    }

    public string Name => Config.Name;

    protected virtual void ConfigureHttpClient()
    {
        if (!string.IsNullOrEmpty(Config.BaseUrl))
        {
            HttpClient.BaseAddress = new Uri(Config.BaseUrl);
        }

        HttpClient.Timeout = TimeSpan.FromSeconds(Config.MaxInputTokens ?? 120);
    }

    public abstract Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    public abstract Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
    public abstract Task<LlmResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    public virtual Task<BackendHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = new BackendHealth
        {
            IsHealthy = _successfulRequests > 0 || _failedRequests == 0,
            AverageLatencyMs = _latencies.Any() ? _latencies.Average() : 0,
            SuccessfulRequests = _successfulRequests,
            FailedRequests = _failedRequests,
            LastError = _lastError,
            LastSuccessfulRequest = _lastSuccessfulRequest
        };

        return Task.FromResult(health);
    }

    protected void RecordSuccess(long latencyMs)
    {
        Interlocked.Increment(ref _successfulRequests);
        _lastSuccessfulRequest = DateTime.UtcNow;
        _latencies.Add(latencyMs);

        // Keep only last 100 latencies
        while (_latencies.Count > 100)
        {
            _latencies.TryTake(out _);
        }
    }

    protected void RecordFailure(string error)
    {
        Interlocked.Increment(ref _failedRequests);
        _lastError = error;
    }

    /// <summary>
    /// Check if spend period needs to be reset based on configuration
    /// </summary>
    private void CheckAndResetSpendPeriod()
    {
        if (!Config.MaxSpendUsd.HasValue || Config.SpendResetPeriod == SpendResetPeriod.Never)
            return;

        lock (_budgetLock)
        {
            var now = DateTime.UtcNow;
            bool shouldReset = false;

            switch (Config.SpendResetPeriod)
            {
                case SpendResetPeriod.Daily:
                    // Reset if we've crossed midnight UTC
                    shouldReset = now.Date > _spendPeriodStart.Date;
                    break;

                case SpendResetPeriod.Weekly:
                    // Reset if we've crossed the configured day of week
                    var daysSinceStart = (now - _spendPeriodStart).Days;
                    var currentDayOfWeek = now.DayOfWeek;
                    if (daysSinceStart >= 7 || (daysSinceStart > 0 && currentDayOfWeek == Config.SpendResetDayOfWeek))
                    {
                        shouldReset = true;
                    }
                    break;

                case SpendResetPeriod.Monthly:
                    // Reset if we've crossed the configured day of month
                    if (now.Month != _spendPeriodStart.Month || now.Year != _spendPeriodStart.Year)
                    {
                        shouldReset = true;
                    }
                    else if (now.Day >= Config.SpendResetDayOfMonth && _spendPeriodStart.Day < Config.SpendResetDayOfMonth)
                    {
                        shouldReset = true;
                    }
                    break;
            }

            if (shouldReset)
            {
                var previousSpend = _currentSpend;
                _currentSpend = 0;
                _budgetExceeded = false;
                _spendPeriodStart = now;

                Logger.LogInformation(
                    "[{Backend}] Budget period reset. Previous spend: ${PreviousSpend:F6}, " +
                    "Reset period: {Period}, New period start: {Start:yyyy-MM-dd HH:mm:ss} UTC",
                    Name,
                    previousSpend,
                    Config.SpendResetPeriod,
                    _spendPeriodStart);

                // Update Prometheus gauge
                if (Telemetry?.EnableMetrics == true)
                {
                    LlmMetrics.UpdateBackendBudget(Name, 0, Config.MaxSpendUsd ?? 0);
                }
            }
        }
    }

    /// <summary>
    /// Check if backend is within budget
    /// </summary>
    protected bool IsWithinBudget()
    {
        if (!Config.MaxSpendUsd.HasValue)
            return true;

        CheckAndResetSpendPeriod();

        lock (_budgetLock)
        {
            if (_budgetExceeded)
            {
                if (Config.LogBudgetExceeded)
                {
                    Logger.LogWarning(
                        "[{Backend}] Backend disabled due to budget limit. " +
                        "Spend: ${CurrentSpend:F6} / ${MaxSpend:F6}. " +
                        "Period: {Period}, Started: {Start:yyyy-MM-dd HH:mm:ss} UTC",
                        Name,
                        _currentSpend,
                        Config.MaxSpendUsd.Value,
                        Config.SpendResetPeriod,
                        _spendPeriodStart);
                }
                return false;
            }

            return _currentSpend < Config.MaxSpendUsd.Value;
        }
    }

    /// <summary>
    /// Record spend and check budget limits
    /// </summary>
    protected void RecordSpend(decimal amount)
    {
        if (!Config.MaxSpendUsd.HasValue || amount <= 0)
            return;

        lock (_budgetLock)
        {
            _currentSpend += amount;

            // Check if budget exceeded
            if (!_budgetExceeded && _currentSpend >= Config.MaxSpendUsd.Value)
            {
                _budgetExceeded = true;

                Logger.LogWarning(
                    "[{Backend}] ⚠️ BUDGET LIMIT EXCEEDED! Backend will be disabled until next reset. " +
                    "Spend: ${CurrentSpend:F6} >= ${MaxSpend:F6}. " +
                    "Reset: {Period} on {ResetInfo}",
                    Name,
                    _currentSpend,
                    Config.MaxSpendUsd.Value,
                    Config.SpendResetPeriod,
                    GetNextResetInfo());
            }

            // Update Prometheus gauge
            if (Telemetry?.EnableMetrics == true)
            {
                LlmMetrics.UpdateBackendBudget(Name, _currentSpend, Config.MaxSpendUsd.Value);
            }
        }
    }

    /// <summary>
    /// Get human-readable info about next budget reset
    /// </summary>
    private string GetNextResetInfo()
    {
        return Config.SpendResetPeriod switch
        {
            SpendResetPeriod.Daily => "Daily at midnight UTC",
            SpendResetPeriod.Weekly => $"Weekly on {Config.SpendResetDayOfWeek} at midnight UTC",
            SpendResetPeriod.Monthly => $"Monthly on day {Config.SpendResetDayOfMonth} at midnight UTC",
            SpendResetPeriod.Never => "Manual reset required",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Get current budget status for monitoring
    /// </summary>
    public (decimal CurrentSpend, decimal? MaxSpend, bool Exceeded, DateTime PeriodStart) GetBudgetStatus()
    {
        lock (_budgetLock)
        {
            return (_currentSpend, Config.MaxSpendUsd, _budgetExceeded, _spendPeriodStart);
        }
    }

    protected LlmResponse CreateSuccessResponse(
        string text,
        long durationMs,
        string? model = null,
        int? totalTokens = null,
        int? promptTokens = null,
        int? completionTokens = null,
        string? finishReason = null)
    {
        RecordSuccess(durationMs);

        // Calculate estimated cost if pricing is configured
        decimal? estimatedCost = null;
        if (promptTokens.HasValue && completionTokens.HasValue &&
            Config.CostPerMillionInputTokens.HasValue && Config.CostPerMillionOutputTokens.HasValue)
        {
            estimatedCost = (promptTokens.Value * Config.CostPerMillionInputTokens.Value / 1_000_000m) +
                          (completionTokens.Value * Config.CostPerMillionOutputTokens.Value / 1_000_000m);

            // Record spend for budget tracking
            RecordSpend(estimatedCost.Value);
        }

        // Record Prometheus metrics if enabled
        if (Telemetry?.EnableMetrics == true && promptTokens.HasValue && completionTokens.HasValue)
        {
            var modelName = model ?? Config.ModelName ?? "unknown";

            LlmMetrics.RecordSuccess(
                Name,
                modelName,
                durationMs,
                promptTokens.Value,
                completionTokens.Value,
                estimatedCost);

            LlmMetrics.DecrementActiveRequests(Name);
        }

        return new LlmResponse
        {
            Success = true,
            Text = text,
            Backend = Name,
            Model = model ?? Config.ModelName,
            DurationMs = durationMs,
            TotalTokens = totalTokens,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = finishReason
        };
    }

    protected LlmResponse CreateErrorResponse(string error, Exception? exception = null)
    {
        RecordFailure(error);

        // Record Prometheus metrics if enabled
        if (Telemetry?.EnableMetrics == true)
        {
            var modelName = Config.ModelName ?? "unknown";
            var errorType = LlmMetrics.CategorizeError(error);

            LlmMetrics.RecordFailure(Name, modelName, 0, errorType);
            LlmMetrics.DecrementActiveRequests(Name);
        }

        return new LlmResponse
        {
            Success = false,
            ErrorMessage = error,
            Backend = Name,
            Exception = exception
        };
    }

    protected async Task<T> ExecuteWithTimingAsync<T>(Func<Task<T>> action)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
