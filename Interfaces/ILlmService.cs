using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Main service interface for LLM operations
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Complete a prompt using the configured backend strategy
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chat completion using the configured backend strategy
    /// </summary>
    Task<LlmResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of available backends
    /// </summary>
    List<string> GetAvailableBackends();

    /// <summary>
    /// Test all backends and return their status
    /// </summary>
    Task<Dictionary<string, BackendHealth>> TestBackendsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific backend by name
    /// </summary>
    ILlmBackend? GetBackend(string name);

    /// <summary>
    /// Get statistics for all backends
    /// </summary>
    Dictionary<string, BackendStatistics> GetStatistics();
}

/// <summary>
/// Statistics for a backend
/// </summary>
public class BackendStatistics
{
    public string Name { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsAvailable { get; set; }
}
