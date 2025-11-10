using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;
using Polly;
using Polly.Retry;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Main service for LLM operations with failover and load balancing
/// </summary>
public class LlmService : ILlmService
{
    private readonly ILogger<LlmService> _logger;
    private readonly LlmSettings _settings;
    private readonly List<ILlmBackend> _backends;
    private readonly ConcurrentDictionary<string, BackendStatistics> _statistics;
    private readonly AsyncRetryPolicy _retryPolicy;
    private int _roundRobinIndex;

    public LlmService(
        ILogger<LlmService> logger,
        IOptions<LlmSettings> settings,
        IEnumerable<ILlmBackend> backends)
    {
        _logger = logger;
        _settings = settings.Value;
        _backends = backends.OrderBy(b => b.Name).ToList();
        _statistics = new ConcurrentDictionary<string, BackendStatistics>();

        // Initialize statistics
        foreach (var backend in _backends)
        {
            _statistics[backend.Name] = new BackendStatistics
            {
                Name = backend.Name,
                IsAvailable = true
            };
        }

        // Configure retry policy
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _settings.MaxRetries,
                retryAttempt => _settings.UseExponentialBackoff
                    ? TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    : TimeSpan.FromSeconds(1),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}s due to: {Message}",
                        retryCount,
                        timeSpan.TotalSeconds,
                        exception.Message);
                });
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var backends = SelectBackends(request.PreferredBackend);

        // Handle Simultaneous strategy - call all backends in parallel
        if (_settings.SelectionStrategy == BackendSelectionStrategy.Simultaneous && backends.Count > 1)
        {
            return await CompleteSimultaneousAsync(request, backends, cancellationToken);
        }

        foreach (var backend in backends)
        {
            var stats = _statistics[backend.Name];
            stats.TotalRequests++;
            stats.LastUsed = DateTime.UtcNow;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await backend.CompleteAsync(request, cancellationToken));

                if (response.Success)
                {
                    stats.SuccessfulRequests++;
                    UpdateAverageResponseTime(stats, response.DurationMs);
                    return response;
                }

                stats.FailedRequests++;
                _logger.LogWarning(
                    "[{Backend}] Request failed: {Error}",
                    backend.Name,
                    response.ErrorMessage);

                // If not using failover strategy, return immediately
                if (_settings.SelectionStrategy != BackendSelectionStrategy.Failover)
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                _logger.LogError(
                    ex,
                    "[{Backend}] Exception during request",
                    backend.Name);

                // If not using failover strategy, throw
                if (_settings.SelectionStrategy != BackendSelectionStrategy.Failover)
                {
                    throw;
                }
            }
        }

        return new LlmResponse
        {
            Success = false,
            ErrorMessage = "All backends failed"
        };
    }

    public async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var backends = SelectBackends(request.PreferredBackend);

        // Handle Simultaneous strategy - call all backends in parallel
        if (_settings.SelectionStrategy == BackendSelectionStrategy.Simultaneous && backends.Count > 1)
        {
            return await ChatSimultaneousAsync(request, backends, cancellationToken);
        }

        foreach (var backend in backends)
        {
            var stats = _statistics[backend.Name];
            stats.TotalRequests++;
            stats.LastUsed = DateTime.UtcNow;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await backend.ChatAsync(request, cancellationToken));

                if (response.Success)
                {
                    stats.SuccessfulRequests++;
                    UpdateAverageResponseTime(stats, response.DurationMs);
                    return response;
                }

                stats.FailedRequests++;
                _logger.LogWarning(
                    "[{Backend}] Request failed: {Error}",
                    backend.Name,
                    response.ErrorMessage);

                // If not using failover strategy, return immediately
                if (_settings.SelectionStrategy != BackendSelectionStrategy.Failover)
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                _logger.LogError(
                    ex,
                    "[{Backend}] Exception during request",
                    backend.Name);

                // If not using failover strategy, throw
                if (_settings.SelectionStrategy != BackendSelectionStrategy.Failover)
                {
                    throw;
                }
            }
        }

        return new LlmResponse
        {
            Success = false,
            ErrorMessage = "All backends failed"
        };
    }

    public List<string> GetAvailableBackends()
    {
        return _backends.Select(b => b.Name).ToList();
    }

    public async Task<Dictionary<string, BackendHealth>> TestBackendsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, BackendHealth>();

        foreach (var backend in _backends)
        {
            try
            {
                var health = await backend.GetHealthAsync(cancellationToken);
                results[backend.Name] = health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Backend}] Health check failed", backend.Name);
                results[backend.Name] = new BackendHealth
                {
                    IsHealthy = false,
                    LastError = ex.Message
                };
            }
        }

        return results;
    }

    public ILlmBackend? GetBackend(string name)
    {
        return _backends.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public Dictionary<string, BackendStatistics> GetStatistics()
    {
        return new Dictionary<string, BackendStatistics>(_statistics);
    }

    private List<ILlmBackend> SelectBackends(string? preferredBackend)
    {
        // If a specific backend is preferred, use it
        if (!string.IsNullOrEmpty(preferredBackend))
        {
            var backend = _backends.FirstOrDefault(b =>
                b.Name.Equals(preferredBackend, StringComparison.OrdinalIgnoreCase));
            if (backend != null)
            {
                return new List<ILlmBackend> { backend };
            }
        }

        return _settings.SelectionStrategy switch
        {
            BackendSelectionStrategy.Failover => _backends
                .OrderBy(b => _statistics[b.Name].FailedRequests)
                .ToList(),

            BackendSelectionStrategy.RoundRobin => new List<ILlmBackend>
            {
                _backends[Interlocked.Increment(ref _roundRobinIndex) % _backends.Count]
            },

            BackendSelectionStrategy.LowestLatency => _backends
                .OrderBy(b => _statistics[b.Name].AverageResponseTimeMs)
                .ToList(),

            BackendSelectionStrategy.Random => new List<ILlmBackend>
            {
                _backends[Random.Shared.Next(_backends.Count)]
            },

            BackendSelectionStrategy.Simultaneous => _backends.ToList(),

            _ => _backends
        };
    }

    /// <summary>
    /// Call multiple backends simultaneously and return all responses
    /// </summary>
    private async Task<LlmResponse> CompleteSimultaneousAsync(
        LlmRequest request,
        List<ILlmBackend> backends,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing simultaneous completion across {Count} backends",
            backends.Count);

        // Call all backends in parallel
        var tasks = backends.Select(async backend =>
        {
            var stats = _statistics[backend.Name];
            stats.TotalRequests++;
            stats.LastUsed = DateTime.UtcNow;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await backend.CompleteAsync(request, cancellationToken));

                if (response.Success)
                {
                    stats.SuccessfulRequests++;
                    UpdateAverageResponseTime(stats, response.DurationMs);
                }
                else
                {
                    stats.FailedRequests++;
                }

                return response;
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                _logger.LogError(ex, "[{Backend}] Exception during simultaneous request", backend.Name);

                return new LlmResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Backend = backend.Name,
                    Exception = ex
                };
            }
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // Find the first successful response to use as primary
        var primaryResponse = responses.FirstOrDefault(r => r.Success);
        if (primaryResponse == null)
        {
            // All failed - return first response with all others as alternatives
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = "All backends failed",
                AlternativeResponses = responses.Skip(1).ToList()
            };
        }

        // Add all other responses (successful or not) as alternatives
        primaryResponse.AlternativeResponses = responses
            .Where(r => r != primaryResponse)
            .ToList();

        _logger.LogInformation(
            "Simultaneous completion completed: {SuccessCount}/{TotalCount} successful",
            responses.Count(r => r.Success),
            responses.Length);

        return primaryResponse;
    }

    /// <summary>
    /// Call multiple backends simultaneously for chat and return all responses
    /// </summary>
    private async Task<LlmResponse> ChatSimultaneousAsync(
        ChatRequest request,
        List<ILlmBackend> backends,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing simultaneous chat across {Count} backends",
            backends.Count);

        // Call all backends in parallel
        var tasks = backends.Select(async backend =>
        {
            var stats = _statistics[backend.Name];
            stats.TotalRequests++;
            stats.LastUsed = DateTime.UtcNow;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await backend.ChatAsync(request, cancellationToken));

                if (response.Success)
                {
                    stats.SuccessfulRequests++;
                    UpdateAverageResponseTime(stats, response.DurationMs);
                }
                else
                {
                    stats.FailedRequests++;
                }

                return response;
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                _logger.LogError(ex, "[{Backend}] Exception during simultaneous chat", backend.Name);

                return new LlmResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Backend = backend.Name,
                    Exception = ex
                };
            }
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // Find the first successful response to use as primary
        var primaryResponse = responses.FirstOrDefault(r => r.Success);
        if (primaryResponse == null)
        {
            // All failed - return first response with all others as alternatives
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = "All backends failed",
                AlternativeResponses = responses.Skip(1).ToList()
            };
        }

        // Add all other responses (successful or not) as alternatives
        primaryResponse.AlternativeResponses = responses
            .Where(r => r != primaryResponse)
            .ToList();

        _logger.LogInformation(
            "Simultaneous chat completed: {SuccessCount}/{TotalCount} successful",
            responses.Count(r => r.Success),
            responses.Length);

        return primaryResponse;
    }

    private void UpdateAverageResponseTime(BackendStatistics stats, long durationMs)
    {
        // Simple moving average
        var totalRequests = stats.SuccessfulRequests + stats.FailedRequests;
        stats.AverageResponseTimeMs = (stats.AverageResponseTimeMs * (totalRequests - 1) + durationMs) / totalRequests;
    }
}
