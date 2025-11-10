namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Interface for LLM backend plugins that can be dynamically loaded
/// </summary>
public interface ILlmBackendPlugin
{
    /// <summary>
    /// Unique identifier for this plugin
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Display name for this plugin
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Plugin author
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Backend types supported by this plugin
    /// </summary>
    IEnumerable<string> SupportedBackendTypes { get; }

    /// <summary>
    /// Create a backend instance for the specified type
    /// </summary>
    /// <param name="backendType">The backend type identifier</param>
    /// <param name="config">Backend configuration</param>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    /// <param name="httpClientFactory">HTTP client factory</param>
    /// <returns>An ILlmBackend instance</returns>
    ILlmBackend CreateBackend(
        string backendType,
        Configuration.LlmBackendConfig config,
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory,
        System.Net.Http.IHttpClientFactory httpClientFactory);

    /// <summary>
    /// Validate that the plugin can be loaded and initialized
    /// </summary>
    /// <returns>True if plugin is valid and can be used</returns>
    bool Validate();
}
