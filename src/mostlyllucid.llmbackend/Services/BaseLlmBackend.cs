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

    private readonly ConcurrentBag<long> _latencies = new();
    private int _successfulRequests;
    private int _failedRequests;
    private string? _lastError;
    private DateTime? _lastSuccessfulRequest;

    protected BaseLlmBackend(
        LlmBackendConfig config,
        ILogger logger,
        HttpClient httpClient)
    {
        Config = config;
        Logger = logger;
        HttpClient = httpClient;
        ConfigureHttpClient();
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
        // Ensure duration is at least 1ms to avoid zero values in extremely fast mocked calls
        var safeDuration = Math.Max(1, (int)durationMs);
        RecordSuccess(safeDuration);

        return new LlmResponse
        {
            Success = true,
            Text = text,
            Backend = Name,
            Model = model ?? Config.ModelName,
            DurationMs = safeDuration,
            TotalTokens = totalTokens,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = finishReason
        };
    }

    protected LlmResponse CreateErrorResponse(string error, Exception? exception = null)
    {
        RecordFailure(error);

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
