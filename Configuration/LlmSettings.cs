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
public class TelemetryConfig
{
    /// <summary>
    /// Enable OpenTelemetry metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable OpenTelemetry tracing
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Log request and response content (may contain sensitive data)
    /// </summary>
    public bool LogContent { get; set; } = false;

    /// <summary>
    /// Service name for telemetry
    /// </summary>
    public string ServiceName { get; set; } = "LlmBackend";

    /// <summary>
    /// Enable cost tracking
    /// </summary>
    public bool EnableCostTracking { get; set; } = false;
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
    public string? CustomBackendType { get; set; }

    /// <summary>
    /// Base URL for the backend API
    /// </summary>
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
    /// LlamaCpp-specific: Local path to the model file (GGUF format)
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// LlamaCpp-specific: URL to download the model from if it doesn't exist locally
    /// </summary>
    public string? ModelUrl { get; set; }

    /// <summary>
    /// LlamaCpp-specific: Context window size (default: 2048)
    /// </summary>
    public int? ContextSize { get; set; }

    /// <summary>
    /// LlamaCpp-specific: Number of layers to offload to GPU (default: 0 = CPU only)
    /// </summary>
    public int? GpuLayers { get; set; }

    /// <summary>
    /// LlamaCpp-specific: Number of threads to use for computation (default: auto-detect)
    /// </summary>
    public int? Threads { get; set; }

    /// <summary>
    /// LlamaCpp-specific: Automatically download model if it doesn't exist (default: true)
    /// </summary>
    public bool AutoDownloadModel { get; set; } = true;

    /// <summary>
    /// LlamaCpp-specific: Use memory lock to prevent swapping (default: false)
    /// </summary>
    public bool? UseMemoryLock { get; set; }

    /// <summary>
    /// LlamaCpp-specific: Seed for random number generation (-1 = random)
    /// </summary>
    public int? Seed { get; set; }
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
    Random
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
    GenericOpenAI,
    LlamaCpp
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
