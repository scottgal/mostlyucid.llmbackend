using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Interface for an LLM backend implementation
/// </summary>
public interface ILlmBackend
{
    /// <summary>
    /// Name of the backend
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Check if the backend is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete a prompt
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chat completion
    /// </summary>
    Task<LlmResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backend health and statistics
    /// </summary>
    Task<BackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status of a backend
/// </summary>
public class BackendHealth
{
    public bool IsHealthy { get; set; }
    public double AverageLatencyMs { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastSuccessfulRequest { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
