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

        // Record Prometheus metrics if enabled
        if (Telemetry?.EnableMetrics == true && promptTokens.HasValue && completionTokens.HasValue)
        {
            var modelName = model ?? Config.ModelName ?? "unknown";

            // Calculate estimated cost if pricing is configured
            decimal? estimatedCost = null;
            if (Config.CostPerMillionInputTokens.HasValue && Config.CostPerMillionOutputTokens.HasValue)
            {
                estimatedCost = (promptTokens.Value * Config.CostPerMillionInputTokens.Value / 1_000_000m) +
                              (completionTokens.Value * Config.CostPerMillionOutputTokens.Value / 1_000_000m);
            }

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
