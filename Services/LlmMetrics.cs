using Prometheus;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Prometheus metrics for LLM backend monitoring
/// </summary>
/// <remarks>
/// <para>
/// This class provides Prometheus metrics for monitoring LLM backend usage, performance,
/// and costs. Metrics can be exposed via the standard /metrics endpoint and scraped by
/// Prometheus for visualization in Grafana.
/// </para>
///
/// <para><strong>Exposed Metrics:</strong></para>
/// <list type="bullet">
/// <item><strong>llm_requests_total</strong> - Counter of total requests per backend, model, and status</item>
/// <item><strong>llm_request_duration_seconds</strong> - Histogram of request durations</item>
/// <item><strong>llm_tokens_total</strong> - Counter of tokens used (prompt, completion, total)</item>
/// <item><strong>llm_estimated_cost_usd</strong> - Counter of estimated costs in USD</item>
/// <item><strong>llm_errors_total</strong> - Counter of errors by backend and error type</item>
/// <item><strong>llm_backend_health</strong> - Gauge of backend health status (1=healthy, 0=unhealthy)</item>
/// </list>
///
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// // In Program.cs or Startup.cs
/// app.UseMetricServer(); // Exposes /metrics endpoint
///
/// // Metrics are automatically recorded by backends if telemetry is enabled
/// </code>
///
/// <para><strong>Grafana Dashboard:</strong></para>
/// Example queries for Grafana:
/// <code>
/// # Request rate per backend
/// rate(llm_requests_total[5m])
///
/// # Average request duration
/// rate(llm_request_duration_seconds_sum[5m]) / rate(llm_request_duration_seconds_count[5m])
///
/// # Total cost over time
/// increase(llm_estimated_cost_usd[1h])
///
/// # Error rate
/// rate(llm_errors_total[5m])
///
/// # Token usage by type
/// rate(llm_tokens_total[5m])
/// </code>
/// </remarks>
public static class LlmMetrics
{
    /// <summary>
    /// Total number of LLM requests
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name (e.g., "openai-primary", "anthropic-secondary")
    /// - model: Model name (e.g., "gpt-4", "claude-3-5-sonnet-20241022")
    /// - status: Request status ("success", "failure", "timeout", "cancelled")
    /// </remarks>
    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "llm_requests_total",
        "Total number of LLM requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "backend", "model", "status" }
        });

    /// <summary>
    /// Duration of LLM requests in seconds
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    /// - model: Model name
    ///
    /// Buckets are optimized for LLM response times (100ms to 60s)
    /// </remarks>
    public static readonly Histogram RequestDuration = Metrics.CreateHistogram(
        "llm_request_duration_seconds",
        "Duration of LLM requests in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "backend", "model" },
            Buckets = new[] { 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 20, 30, 60 }
        });

    /// <summary>
    /// Total number of tokens used
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    /// - model: Model name
    /// - token_type: Type of tokens ("prompt", "completion", "total")
    /// </remarks>
    public static readonly Counter TokensTotal = Metrics.CreateCounter(
        "llm_tokens_total",
        "Total number of tokens used",
        new CounterConfiguration
        {
            LabelNames = new[] { "backend", "model", "token_type" }
        });

    /// <summary>
    /// Estimated cost in USD
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    /// - model: Model name
    ///
    /// Note: This is an estimate based on configured pricing.
    /// Actual costs may vary. Always verify with your provider's billing.
    /// </remarks>
    public static readonly Counter EstimatedCostUsd = Metrics.CreateCounter(
        "llm_estimated_cost_usd",
        "Estimated cost in USD (based on token usage and configured pricing)",
        new CounterConfiguration
        {
            LabelNames = new[] { "backend", "model" }
        });

    /// <summary>
    /// Total number of errors
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    /// - error_type: Type of error ("api_error", "timeout", "network", "rate_limit", "auth", "unknown")
    /// </remarks>
    public static readonly Counter ErrorsTotal = Metrics.CreateCounter(
        "llm_errors_total",
        "Total number of errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "backend", "error_type" }
        });

    /// <summary>
    /// Backend health status (1=healthy, 0=unhealthy)
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    ///
    /// Updated periodically by health checks.
    /// Use this to monitor backend availability over time.
    /// </remarks>
    public static readonly Gauge BackendHealth = Metrics.CreateGauge(
        "llm_backend_health",
        "Backend health status (1=healthy, 0=unhealthy)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "backend" }
        });

    /// <summary>
    /// Active requests currently in flight
    /// </summary>
    /// <remarks>
    /// Labels:
    /// - backend: Backend name
    ///
    /// Tracks concurrent requests to help identify load patterns
    /// and potential bottlenecks.
    /// </remarks>
    public static readonly Gauge ActiveRequests = Metrics.CreateGauge(
        "llm_active_requests",
        "Number of requests currently in flight",
        new GaugeConfiguration
        {
            LabelNames = new[] { "backend" }
        });

    /// <summary>
    /// Record a successful request
    /// </summary>
    /// <param name="backendName">Backend that processed the request</param>
    /// <param name="modelName">Model used</param>
    /// <param name="durationMs">Request duration in milliseconds</param>
    /// <param name="promptTokens">Number of prompt tokens</param>
    /// <param name="completionTokens">Number of completion tokens</param>
    /// <param name="estimatedCost">Estimated cost in USD (optional)</param>
    public static void RecordSuccess(
        string backendName,
        string modelName,
        long durationMs,
        int promptTokens,
        int completionTokens,
        decimal? estimatedCost = null)
    {
        RequestsTotal.WithLabels(backendName, modelName, "success").Inc();
        RequestDuration.WithLabels(backendName, modelName).Observe(durationMs / 1000.0);

        TokensTotal.WithLabels(backendName, modelName, "prompt").Inc(promptTokens);
        TokensTotal.WithLabels(backendName, modelName, "completion").Inc(completionTokens);
        TokensTotal.WithLabels(backendName, modelName, "total").Inc(promptTokens + completionTokens);

        if (estimatedCost.HasValue && estimatedCost.Value > 0)
        {
            EstimatedCostUsd.WithLabels(backendName, modelName).Inc((double)estimatedCost.Value);
        }
    }

    /// <summary>
    /// Record a failed request
    /// </summary>
    /// <param name="backendName">Backend that attempted the request</param>
    /// <param name="modelName">Model that was requested (use "unknown" if not determined)</param>
    /// <param name="durationMs">Request duration in milliseconds</param>
    /// <param name="errorType">Type of error (api_error, timeout, network, rate_limit, auth, unknown)</param>
    public static void RecordFailure(
        string backendName,
        string modelName,
        long durationMs,
        string errorType = "unknown")
    {
        RequestsTotal.WithLabels(backendName, modelName, "failure").Inc();
        RequestDuration.WithLabels(backendName, modelName).Observe(durationMs / 1000.0);
        ErrorsTotal.WithLabels(backendName, errorType).Inc();
    }

    /// <summary>
    /// Update backend health status
    /// </summary>
    /// <param name="backendName">Backend name</param>
    /// <param name="isHealthy">Whether the backend is healthy</param>
    public static void UpdateBackendHealth(string backendName, bool isHealthy)
    {
        BackendHealth.WithLabels(backendName).Set(isHealthy ? 1 : 0);
    }

    /// <summary>
    /// Increment active requests counter (call when request starts)
    /// </summary>
    /// <param name="backendName">Backend name</param>
    public static void IncrementActiveRequests(string backendName)
    {
        ActiveRequests.WithLabels(backendName).Inc();
    }

    /// <summary>
    /// Decrement active requests counter (call when request ends)
    /// </summary>
    /// <param name="backendName">Backend name</param>
    public static void DecrementActiveRequests(string backendName)
    {
        ActiveRequests.WithLabels(backendName).Dec();
    }

    /// <summary>
    /// Categorize an error message into a standard error type
    /// </summary>
    /// <param name="errorMessage">The error message</param>
    /// <returns>Error type category</returns>
    public static string CategorizeError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "unknown";

        var lower = errorMessage.ToLowerInvariant();

        if (lower.Contains("timeout") || lower.Contains("timed out"))
            return "timeout";

        if (lower.Contains("rate limit") || lower.Contains("429") || lower.Contains("quota"))
            return "rate_limit";

        if (lower.Contains("auth") || lower.Contains("401") || lower.Contains("403") || lower.Contains("unauthorized"))
            return "auth";

        if (lower.Contains("network") || lower.Contains("connection") || lower.Contains("dns"))
            return "network";

        if (lower.Contains("400") || lower.Contains("404") || lower.Contains("500") || lower.Contains("502") || lower.Contains("503"))
            return "api_error";

        return "unknown";
    }
}
