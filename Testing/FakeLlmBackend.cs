using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Testing;

/// <summary>
/// Fake LLM backend for testing purposes
/// </summary>
/// <remarks>
/// <para>
/// This is a test double that implements ILlmBackend for use in unit tests.
/// It allows you to control responses, simulate failures, and verify that your
/// code correctly integrates with the LLM backend abstraction.
/// </para>
///
/// <para><strong>Common Use Cases:</strong></para>
/// <list type="bullet">
/// <item>Unit testing code that depends on ILlmService</item>
/// <item>Integration testing without calling real APIs</item>
/// <item>Simulating error conditions and retries</item>
/// <item>Testing failover behavior</item>
/// <item>Performance testing without API costs</item>
/// </list>
///
/// <para><strong>Example Usage:</strong></para>
/// <code>
/// // Simple response
/// var fake = new FakeLlmBackend("test-backend")
/// {
///     ResponseText = "This is a test response"
/// };
///
/// var result = await fake.CompleteAsync(new LlmRequest { Prompt = "Test" });
/// Assert.Equal("This is a test response", result.Content);
///
/// // Simulate failure
/// fake.SimulateFailure = true;
/// fake.FailureMessage = "API is down";
/// var result = await fake.CompleteAsync(request);
/// Assert.False(result.Success);
/// Assert.Equal("API is down", result.ErrorMessage);
///
/// // Verify requests
/// Assert.Equal(2, fake.RequestCount);
/// Assert.Contains("Test", fake.LastRequest.Prompt);
/// </code>
/// </remarks>
public class FakeLlmBackend : ILlmBackend
{
    private readonly ILogger _logger;
    private int _requestCount;
    private LlmRequest? _lastRequest;
    private ChatRequest? _lastChatRequest;

    /// <summary>
    /// Create a new fake LLM backend with default settings
    /// </summary>
    /// <param name="name">Backend name for identification</param>
    /// <param name="logger">Optional logger (defaults to NullLogger)</param>
    public FakeLlmBackend(string name = "FakeBackend", ILogger? logger = null)
    {
        Name = name;
        _logger = logger ?? NullLogger.Instance;
        ResponseText = "Fake response from {Name}";
        ModelUsed = "fake-model-1";
        IsAvailable = true;
        LatencyMs = 100;
    }

    #region Configuration Properties

    /// <summary>
    /// Backend name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Whether the backend should report as available
    /// </summary>
    /// <remarks>
    /// Set to false to simulate backend unavailability and test failover logic.
    /// </remarks>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Text to return in responses
    /// </summary>
    /// <remarks>
    /// Supports template variables:
    /// - {Name}: Backend name
    /// - {Prompt}: Original prompt text
    /// - {RequestCount}: Number of requests made
    /// </remarks>
    public string ResponseText { get; set; }

    /// <summary>
    /// Model name to report in responses
    /// </summary>
    public string ModelUsed { get; set; }

    /// <summary>
    /// Simulated latency in milliseconds
    /// </summary>
    /// <remarks>
    /// The fake backend will delay for this many milliseconds to simulate network latency.
    /// Useful for testing timeout and performance scenarios.
    /// </remarks>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Whether to simulate a failure
    /// </summary>
    /// <remarks>
    /// When true, all requests return error responses with FailureMessage.
    /// Useful for testing error handling and retry logic.
    /// </remarks>
    public bool SimulateFailure { get; set; }

    /// <summary>
    /// Error message to return when SimulateFailure is true
    /// </summary>
    public string FailureMessage { get; set; } = "Simulated failure";

    /// <summary>
    /// Simulated token counts for responses
    /// </summary>
    public int PromptTokens { get; set; } = 50;

    /// <summary>
    /// Simulated token counts for responses
    /// </summary>
    public int CompletionTokens { get; set; } = 100;

    #endregion

    #region Request History

    /// <summary>
    /// Number of requests made to this backend
    /// </summary>
    public int RequestCount => _requestCount;

    /// <summary>
    /// The last LlmRequest received
    /// </summary>
    public LlmRequest? LastRequest => _lastRequest;

    /// <summary>
    /// The last ChatRequest received
    /// </summary>
    public ChatRequest? LastChatRequest => _lastChatRequest;

    /// <summary>
    /// Reset request history counters
    /// </summary>
    public void Reset()
    {
        _requestCount = 0;
        _lastRequest = null;
        _lastChatRequest = null;
    }

    #endregion

    #region ILlmBackend Implementation

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{Backend}] Availability check: {IsAvailable}", Name, IsAvailable);
        return Task.FromResult(IsAvailable);
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        _lastRequest = request;
        Interlocked.Increment(ref _requestCount);

        _logger.LogInformation(
            "[{Backend}] Complete request #{Count}: {PromptLength} chars",
            Name,
            _requestCount,
            request.Prompt?.Length ?? 0);

        // Simulate latency
        if (LatencyMs > 0)
        {
            await Task.Delay((int)LatencyMs, cancellationToken);
        }

        if (SimulateFailure)
        {
            _logger.LogError("[{Backend}] Simulated failure: {Message}", Name, FailureMessage);
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = FailureMessage,
                BackendUsed = Name
            };
        }

        var responseText = ResponseText
            .Replace("{Name}", Name)
            .Replace("{Prompt}", request.Prompt ?? "")
            .Replace("{RequestCount}", _requestCount.ToString());

        return new LlmResponse
        {
            Success = true,
            Content = responseText,
            BackendUsed = Name,
            ModelUsed = ModelUsed,
            DurationMs = LatencyMs,
            PromptTokens = PromptTokens,
            CompletionTokens = CompletionTokens,
            TotalTokens = PromptTokens + CompletionTokens,
            FinishReason = "stop"
        };
    }

    public async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        _lastChatRequest = request;
        Interlocked.Increment(ref _requestCount);

        _logger.LogInformation(
            "[{Backend}] Chat request #{Count}: {MessageCount} messages",
            Name,
            _requestCount,
            request.Messages?.Count ?? 0);

        // Simulate latency
        if (LatencyMs > 0)
        {
            await Task.Delay((int)LatencyMs, cancellationToken);
        }

        if (SimulateFailure)
        {
            _logger.LogError("[{Backend}] Simulated failure: {Message}", Name, FailureMessage);
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = FailureMessage,
                BackendUsed = Name
            };
        }

        var lastMessage = request.Messages?.LastOrDefault()?.Content ?? "";
        var responseText = ResponseText
            .Replace("{Name}", Name)
            .Replace("{Prompt}", lastMessage)
            .Replace("{RequestCount}", _requestCount.ToString());

        return new LlmResponse
        {
            Success = true,
            Content = responseText,
            BackendUsed = Name,
            ModelUsed = ModelUsed,
            DurationMs = LatencyMs,
            PromptTokens = PromptTokens,
            CompletionTokens = CompletionTokens,
            TotalTokens = PromptTokens + CompletionTokens,
            FinishReason = "stop"
        };
    }

    public Task<BackendHealth> GetHealthAsync()
    {
        return Task.FromResult(new BackendHealth
        {
            IsHealthy = IsAvailable,
            AverageLatencyMs = LatencyMs,
            SuccessfulRequests = SimulateFailure ? 0 : _requestCount,
            FailedRequests = SimulateFailure ? _requestCount : 0,
            LastError = SimulateFailure ? FailureMessage : null,
            LastSuccessfulRequest = IsAvailable && !SimulateFailure ? DateTime.UtcNow : null
        });
    }

    #endregion
}
