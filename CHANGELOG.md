# Changelog

All notable changes to the Mostlyucid.LlmBackend project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2024-11-09

### Added

#### New LLM Backends
- **Anthropic Claude** - Full support for Claude 3 (Opus, Sonnet, Haiku) and Claude 3.5
- **Google Gemini** - Support for both AI Studio and Vertex AI deployments
- **Cohere** - Support for Command, Command R, and Command R+ models

#### Enterprise Features
- **Circuit Breaker Pattern** - Prevent cascading failures with configurable thresholds
- **Rate Limiting** - Protect against API quota exhaustion with request throttling
- **Response Caching** - Support for Memory, Redis, and SQL Server caching providers
- **Secrets Management** - Integration with Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, and environment variables
- **Health Checks** - Automatic backend health monitoring with configurable intervals
- **Cost Tracking** - Per-backend cost tracking with configurable token pricing
- **Enhanced Telemetry** - OpenTelemetry metrics and tracing support

#### Configuration Enhancements
- Comprehensive configuration options for all aspects of the library
- Per-backend timeout and retry overrides
- Custom HTTP headers support
- Additional sampling parameters (TopP, FrequencyPenalty, PresencePenalty)
- Stop sequences support
- Provider-specific configuration (Azure deployment names, Anthropic versions, etc.)

#### Context Memory
- Multiple memory provider support (InMemory, Redis, SQL Server, CosmosDB, File)
- Configurable TTL for memory entries
- Token limit management
- Automatic context compression option

### Changed
- **BREAKING**: Enhanced `LlmSettings` configuration model with new nested configuration sections
- **BREAKING**: `LlmBackendConfig` now includes many new optional properties
- Updated Polly to version 8.4.2
- Updated System.Text.Json to version 8.0.5
- Improved error handling and logging across all backends

### Improved
- More detailed XML documentation throughout
- Enhanced README with comprehensive examples
- Better factory pattern implementation
- Thread-safe operations across all services

### Fixed
- Improved token counting accuracy
- Better handling of null/optional parameters
- More robust error responses

## [1.0.0] - 2024-XX-XX

### Added
- Initial release
- OpenAI backend support
- Azure OpenAI backend support
- Ollama backend support
- LM Studio backend support
- EasyNMT backend support
- Basic failover strategy
- Round-robin strategy
- Lowest latency strategy
- Retry logic with exponential backoff
- Basic health checks
- Prompt builder interface
- Context memory (in-memory only)
- Dependency injection extensions

[2.0.0]: https://github.com/scottgal/mostlyucid.llmbackend/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/scottgal/mostlyucid.llmbackend/releases/tag/v1.0.0
