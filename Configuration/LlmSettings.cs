namespace Mostlyucid.LlmBackend.Configuration;

/// <summary>
/// Root configuration for LLM backends
/// </summary>
public class LlmSettings
{
    public const string SectionName = "LlmSettings";

    /// <summary>
    /// List of configured backends
    /// </summary>
    public List<LlmBackendConfig> Backends { get; set; } = new();

    /// <summary>
    /// Selection strategy for choosing which backend to use
    /// </summary>
    public BackendSelectionStrategy SelectionStrategy { get; set; } = BackendSelectionStrategy.Failover;

    /// <summary>
    /// Timeout for LLM requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum retries per backend before failing over
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Base delay between retries in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Default temperature for LLM requests (0.0 - 2.0)
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    /// Default maximum tokens for completion
    /// </summary>
    public int? DefaultMaxTokens { get; set; } = 2000;

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitConfig RateLimit { get; set; } = new();

    /// <summary>
    /// Response caching configuration
    /// </summary>
    public CachingConfig Caching { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckConfig HealthCheck { get; set; } = new();

    /// <summary>
    /// Secrets provider configuration
    /// </summary>
    public SecretsConfig Secrets { get; set; } = new();

    /// <summary>
    /// Telemetry and metrics configuration
    /// </summary>
    public TelemetryConfig Telemetry { get; set; } = new();

    /// <summary>
    /// Context memory configuration
    /// </summary>
    public MemoryConfig Memory { get; set; } = new();

    /// <summary>
    /// Plugin configuration
    /// </summary>
    public PluginConfig Plugins { get; set; } = new();

    /// <summary>
    /// Prompt builder configuration for different task types
    /// </summary>
    /// <remarks>
    /// Configure which prompt builder to use for different scenarios.
    /// Task types: "Default", "Translation", "Chat", "Summarization", "CodeGeneration", etc.
    /// You can also specify custom task types and map them to specific prompt builders.
    ///
    /// Example configuration:
    /// <code>
    /// "PromptBuilders": {
    ///   "Default": "DefaultPromptBuilder",
    ///   "Translation": "TranslationPromptBuilder",
    ///   "Chat": "CustomChatPromptBuilder"
    /// }
    /// </code>
    /// </remarks>
    public Dictionary<string, string> PromptBuilders { get; set; } = new()
    {
        ["Default"] = "DefaultPromptBuilder",
        ["Translation"] = "TranslationPromptBuilder"
    };
}

/// <summary>
/// Circuit breaker configuration for preventing cascading failures
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Enable circuit breaker pattern
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds to keep circuit open before attempting to close
    /// </summary>
    public int DurationOfBreakSeconds { get; set; } = 30;

    /// <summary>
    /// Sampling duration in seconds for tracking failures
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum throughput before circuit breaker activates
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;
}

/// <summary>
/// Rate limiting configuration to prevent API quota exhaustion
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum requests per time window
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Time window in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum concurrent requests
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Queue size for pending requests
    /// </summary>
    public int QueueLimit { get; set; } = 100;
}

/// <summary>
/// Response caching configuration to reduce API calls and costs
/// </summary>
public class CachingConfig
{
    /// <summary>
    /// Enable response caching
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Cache provider type
    /// </summary>
    public CacheProvider Provider { get; set; } = CacheProvider.Memory;

    /// <summary>
    /// Cache expiration in minutes
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum cache size in MB
    /// </summary>
    public int MaxCacheSizeMb { get; set; } = 100;

    /// <summary>
    /// Connection string for distributed cache (Redis, SQL, etc.)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Cache key prefix
    /// </summary>
    public string KeyPrefix { get; set; } = "llm:";
}

/// <summary>
/// Health check configuration for monitoring backend availability
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Enable automatic health checks
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Number of consecutive failures before marking backend as unhealthy
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Number of consecutive successes before marking backend as healthy
    /// </summary>
    public int HealthyThreshold { get; set; } = 2;
}

/// <summary>
/// Secrets provider configuration for secure API key management
/// </summary>
public class SecretsConfig
{
    /// <summary>
    /// Secrets provider type
    /// </summary>
    public SecretsProvider Provider { get; set; } = SecretsProvider.Configuration;

    /// <summary>
    /// Azure Key Vault URL (for Azure Key Vault provider)
    /// </summary>
    public string? KeyVaultUrl { get; set; }

    /// <summary>
    /// AWS Secrets Manager region (for AWS provider)
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Environment variable prefix for API keys
    /// </summary>
    public string EnvironmentVariablePrefix { get; set; } = "LLM_";

    /// <summary>
    /// Use managed identity for cloud secret providers
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;
}

/// <summary>
/// Telemetry and metrics configuration
/// </summary>
/// <remarks>
/// Controls logging verbosity at different levels:
///
/// <strong>Debug Level:</strong> Copious logging including:
/// - Full prompts (may contain sensitive user data)
/// - Complete raw API responses
/// - Request/response headers
/// - Detailed timing information
/// - Plugin loading details
/// - Configuration resolution steps
///
/// <strong>Information Level:</strong> Standard operational logging:
/// - Request summaries (backend used, model, duration)
/// - Success/failure indicators
/// - Token counts and costs
/// - Backend health status changes
/// - Plugin registration
///
/// <strong>Warning Level:</strong> Potential issues:
/// - Backend unavailability
/// - Retry attempts
/// - Circuit breaker state changes
/// - Configuration warnings
/// - Plugin validation failures
///
/// <strong>Error Level:</strong> Failures only:
/// - Request failures
/// - Backend errors
/// - Plugin loading errors
/// - Critical configuration issues
///
/// Set EnableDetailedLogging=true and use Debug log level for troubleshooting.
/// In production, use Information or Warning level with EnableDetailedLogging=false.
/// </remarks>
public class TelemetryConfig
{
    /// <summary>
    /// Enable OpenTelemetry metrics collection
    /// </summary>
    /// <remarks>
    /// Collects metrics such as request counts, latencies, error rates.
    /// Metrics can be exported to Prometheus, Application Insights, etc.
    /// </remarks>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable OpenTelemetry distributed tracing
    /// </summary>
    /// <remarks>
    /// Creates spans for LLM requests, allowing you to trace requests
    /// through your entire system including LLM backend calls.
    /// </remarks>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Enable detailed debug logging (includes prompts and responses)
    /// </summary>
    /// <remarks>
    /// <strong>WARNING:</strong> Enables very verbose logging at Debug level.
    /// Logs will include:
    /// - Complete prompts (may contain PII)
    /// - Full API responses
    /// - Request/response headers
    /// - Detailed configuration
    ///
    /// <strong>Use only for debugging</strong>. Do not enable in production
    /// as it may log sensitive user data and significantly increase log volume.
    ///
    /// Must set log level to Debug in logging configuration for this to take effect.
    /// </remarks>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Log full request prompts at Debug level
    /// </summary>
    /// <remarks>
    /// When true and log level is Debug, full prompts are logged.
    /// <strong>WARNING:</strong> Prompts may contain sensitive user data.
    /// Only enable for debugging specific issues.
    /// </remarks>
    public bool LogPrompts { get; set; } = false;

    /// <summary>
    /// Log full API responses at Debug level
    /// </summary>
    /// <remarks>
    /// When true and log level is Debug, complete raw API responses are logged.
    /// Useful for debugging API response parsing issues.
    /// May contain large amounts of data.
    /// </remarks>
    public bool LogResponses { get; set; } = false;

    /// <summary>
    /// Log HTTP headers at Debug level
    /// </summary>
    /// <remarks>
    /// Logs request and response headers for debugging authentication
    /// and API communication issues.
    /// </remarks>
    public bool LogHeaders { get; set; } = false;

    /// <summary>
    /// Log timing information at Debug level
    /// </summary>
    /// <remarks>
    /// Logs detailed timing for each stage of request processing:
    /// - Prompt building time
    /// - HTTP request time
    /// - Response parsing time
    /// - Total end-to-end time
    /// </remarks>
    public bool LogTiming { get; set; } = true;

    /// <summary>
    /// Service name for telemetry
    /// </summary>
    /// <remarks>
    /// Used in OpenTelemetry traces and metrics to identify this service.
    /// Helps distinguish LLM backend calls from other services in distributed traces.
    /// </remarks>
    public string ServiceName { get; set; } = "LlmBackend";

    /// <summary>
    /// Enable cost tracking and logging
    /// </summary>
    /// <remarks>
    /// Tracks and logs costs based on token usage and configured pricing.
    /// Logs cost information at Information level after each request.
    /// Requires CostPerMillionInputTokens and CostPerMillionOutputTokens
    /// to be configured on backends.
    /// </remarks>
    public bool EnableCostTracking { get; set; } = false;

    /// <summary>
    /// Log token counts with each request
    /// </summary>
    /// <remarks>
    /// Logs input/output/total token counts at Information level.
    /// Useful for monitoring API usage and optimizing prompts.
    /// </remarks>
    public bool LogTokenCounts { get; set; } = true;

    /// <summary>
    /// Include correlation IDs in all log messages
    /// </summary>
    /// <remarks>
    /// Adds a unique correlation ID to each request that appears in all
    /// related log messages. Helps track a single request through logs.
    /// </remarks>
    public bool IncludeCorrelationId { get; set; } = true;
}

/// <summary>
/// Context memory configuration
/// </summary>
public class MemoryConfig
{
    /// <summary>
    /// Memory provider type
    /// </summary>
    public MemoryProvider Provider { get; set; } = MemoryProvider.InMemory;

    /// <summary>
    /// Connection string for persistent memory (Redis, SQL, etc.)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Default token limit for context memory
    /// </summary>
    public int DefaultTokenLimit { get; set; } = 4096;

    /// <summary>
    /// Enable automatic context compression
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// TTL for memory entries in minutes
    /// </summary>
    public int TtlMinutes { get; set; } = 60;
}

/// <summary>
/// Configuration for a single LLM backend
/// </summary>
public class LlmBackendConfig
{
    /// <summary>
    /// Unique name for this backend
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of backend
    /// </summary>
    public LlmBackendType Type { get; set; }

    /// <summary>
    /// Custom backend type identifier (used for plugin backends)
    /// Only used when Type is not recognized or for plugin-provided backends
    /// </summary>
    /// <remarks>
    /// When using plugin-provided backends, set this to match one of the types
    /// in the plugin's SupportedBackendTypes list.
    /// Example: "Mistral", "Perplexity", "TogetherAI"
    /// </remarks>
    public string? CustomBackendType { get; set; }

    /// <summary>
    /// Prompt builder type to use for this backend
    /// </summary>
    /// <remarks>
    /// Specifies which prompt builder this backend should use.
    /// If not set, uses the default prompt builder from global PromptBuilders configuration.
    ///
    /// Common values:
    /// - "DefaultPromptBuilder" - Basic prompt building
    /// - "TranslationPromptBuilder" - Optimized for translation with placeholder preservation
    /// - "ChatPromptBuilder" - Conversational with history management
    /// - Custom plugin-provided types
    ///
    /// You can override the global prompt builder selection on a per-backend basis.
    /// This is useful when different backends need different prompting strategies.
    /// </remarks>
    /// <example>"TranslationPromptBuilder"</example>
    public string? PromptBuilderType { get; set; }

    /// <summary>
    /// Base URL for the backend API
    /// </summary>
    /// <remarks>
    /// The base URL for the LLM provider's API endpoint.
    /// Examples:
    /// - OpenAI: "https://api.openai.com"
    /// - Azure OpenAI: "https://YOUR_RESOURCE.openai.azure.com"
    /// - Anthropic: "https://api.anthropic.com"
    /// - Local Ollama: "http://localhost:11434"
    /// </remarks>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key (if required) - can be overridden by secrets provider
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model name to use
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Temperature override for this backend (0.0 - 2.0)
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Maximum input tokens (context window)
    /// </summary>
    public int? MaxInputTokens { get; set; }

    /// <summary>
    /// Maximum output tokens (completion length)
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Top-p sampling parameter (0.0 - 1.0)
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Frequency penalty (-2.0 to 2.0)
    /// </summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Presence penalty (-2.0 to 2.0)
    /// </summary>
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Stop sequences
    /// </summary>
    public List<string>? StopSequences { get; set; }

    /// <summary>
    /// Priority for failover (lower is higher priority)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this backend is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom timeout for this backend in seconds (overrides global)
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Custom max retries for this backend (overrides global)
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Additional HTTP headers to send with requests
    /// </summary>
    public Dictionary<string, string>? AdditionalHeaders { get; set; }

    /// <summary>
    /// Azure-specific: Deployment name
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Azure-specific: API version
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// OpenAI-specific: Organization ID
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Anthropic-specific: Anthropic version header
    /// </summary>
    public string? AnthropicVersion { get; set; }

    /// <summary>
    /// Google-specific: Project ID
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Google-specific: Location/Region
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Enable streaming for this backend
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Enable function calling/tools for this backend
    /// </summary>
    public bool EnableFunctionCalling { get; set; } = false;

    /// <summary>
    /// Cost per 1M input tokens (for cost tracking)
    /// </summary>
    public decimal? CostPerMillionInputTokens { get; set; }

    /// <summary>
    /// Cost per 1M output tokens (for cost tracking)
    /// </summary>
    public decimal? CostPerMillionOutputTokens { get; set; }

    /// <summary>
    /// Maximum spend limit in USD for this backend
    /// </summary>
    /// <remarks>
    /// When the accumulated spend for the current period exceeds this limit,
    /// the backend will be automatically disabled until the next reset period.
    /// Set to null for unlimited spend.
    /// Requires CostPerMillionInputTokens and CostPerMillionOutputTokens to be configured.
    /// </remarks>
    /// <example>10.00m</example>
    public decimal? MaxSpendUsd { get; set; }

    /// <summary>
    /// How often to reset the spend counter
    /// </summary>
    /// <remarks>
    /// - Daily: Resets at midnight UTC
    /// - Weekly: Resets on configured SpendResetDayOfWeek at midnight UTC
    /// - Monthly: Resets on configured SpendResetDayOfMonth at midnight UTC
    /// - Never: Manual reset only (via API or restart)
    /// </remarks>
    public SpendResetPeriod SpendResetPeriod { get; set; } = SpendResetPeriod.Monthly;

    /// <summary>
    /// Day of week for weekly spend reset (0 = Sunday, 6 = Saturday)
    /// </summary>
    /// <remarks>
    /// Only used when SpendResetPeriod = Weekly.
    /// Defaults to Monday (DayOfWeek.Monday = 1).
    /// </remarks>
    public DayOfWeek SpendResetDayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>
    /// Day of month for monthly spend reset (1-31)
    /// </summary>
    /// <remarks>
    /// Only used when SpendResetPeriod = Monthly.
    /// If day doesn't exist in a month (e.g., 31st in February), resets on last day of that month.
    /// Defaults to 1 (first day of month).
    /// </remarks>
    public int SpendResetDayOfMonth { get; set; } = 1;

    /// <summary>
    /// Whether to log when backend is disabled due to budget limits
    /// </summary>
    /// <remarks>
    /// When true, logs a warning when a backend is disabled due to exceeding MaxSpendUsd.
    /// Defaults to true for visibility into cost controls.
    /// </remarks>
    public bool LogBudgetExceeded { get; set; } = true;
}

/// <summary>
/// Spend reset period for budget limits
/// </summary>
public enum SpendResetPeriod
{
    /// <summary>
    /// Reset spend counter daily at midnight UTC
    /// </summary>
    Daily,

    /// <summary>
    /// Reset spend counter weekly on configured day at midnight UTC
    /// </summary>
    Weekly,

    /// <summary>
    /// Reset spend counter monthly on configured day at midnight UTC
    /// </summary>
    Monthly,

    /// <summary>
    /// Never reset automatically (manual reset only)
    /// </summary>
    Never
}

/// <summary>
/// Strategy for selecting which backend to use
/// </summary>
public enum BackendSelectionStrategy
{
    /// <summary>
    /// Try backends in priority order until one succeeds
    /// </summary>
    Failover,

    /// <summary>
    /// Rotate through backends in order
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Use a specific named backend
    /// </summary>
    Specific,

    /// <summary>
    /// Use the backend with the lowest average latency
    /// </summary>
    LowestLatency,

    /// <summary>
    /// Randomly select a backend
    /// </summary>
    Random,

    /// <summary>
    /// Send request to multiple backends simultaneously and return all responses
    /// </summary>
    /// <remarks>
    /// Useful for creative tasks where you want to compare outputs from different models.
    /// All enabled backends will be called in parallel, and all successful responses
    /// will be returned. This allows users to choose the best version.
    /// Note: This will consume API quota/budget from all backends used.
    /// </remarks>
    Simultaneous
}

/// <summary>
/// Supported LLM backend types
/// </summary>
public enum LlmBackendType
{
    OpenAI,
    AzureOpenAI,
    Ollama,
    LMStudio,
    EasyNMT,
    Anthropic,
    Gemini,
    Cohere,
    GenericOpenAI
}

/// <summary>
/// Cache provider types
/// </summary>
public enum CacheProvider
{
    /// <summary>
    /// In-memory caching (not distributed)
    /// </summary>
    Memory,

    /// <summary>
    /// Redis distributed cache
    /// </summary>
    Redis,

    /// <summary>
    /// SQL Server distributed cache
    /// </summary>
    SqlServer,

    /// <summary>
    /// NCache distributed cache
    /// </summary>
    NCache,

    /// <summary>
    /// Custom cache provider
    /// </summary>
    Custom
}

/// <summary>
/// Secrets provider types for secure API key management
/// </summary>
public enum SecretsProvider
{
    /// <summary>
    /// Use configuration files (appsettings.json, etc.) - not secure for production
    /// </summary>
    Configuration,

    /// <summary>
    /// Use environment variables
    /// </summary>
    EnvironmentVariables,

    /// <summary>
    /// Use Azure Key Vault
    /// </summary>
    AzureKeyVault,

    /// <summary>
    /// Use AWS Secrets Manager
    /// </summary>
    AwsSecretsManager,

    /// <summary>
    /// Use HashiCorp Vault
    /// </summary>
    HashiCorpVault,

    /// <summary>
    /// Use Google Cloud Secret Manager
    /// </summary>
    GoogleSecretManager,

    /// <summary>
    /// Custom secrets provider
    /// </summary>
    Custom
}

/// <summary>
/// Memory provider types for context storage
/// </summary>
public enum MemoryProvider
{
    /// <summary>
    /// In-memory storage (not persistent)
    /// </summary>
    InMemory,

    /// <summary>
    /// Redis-based persistent storage
    /// </summary>
    Redis,

    /// <summary>
    /// SQL Server-based persistent storage
    /// </summary>
    SqlServer,

    /// <summary>
    /// Azure Cosmos DB storage
    /// </summary>
    CosmosDb,

    /// <summary>
    /// File-based storage
    /// </summary>
    File,

    /// <summary>
    /// Custom memory provider
    /// </summary>
    Custom
}

/// <summary>
/// Plugin configuration for loading external LLM backend providers
/// </summary>
public class PluginConfig
{
    /// <summary>
    /// Enable plugin loading
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory to search for plugin DLLs
    /// </summary>
    public string PluginDirectory { get; set; } = "plugins";

    /// <summary>
    /// Search subdirectories for plugins
    /// </summary>
    public bool SearchSubdirectories { get; set; } = true;

    /// <summary>
    /// Load plugins on startup
    /// </summary>
    public bool LoadOnStartup { get; set; } = true;

    /// <summary>
    /// List of specific plugin assemblies to load (optional)
    /// </summary>
    public List<string>? SpecificPlugins { get; set; }
}
