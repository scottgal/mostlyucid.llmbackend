# Release Notes

## v2.1.0 - LlamaCpp Integration (2025-11-09)

### New Features

#### 🚀 LlamaCpp Backend Support
- **Automatic Model Downloading**: Download GGUF models automatically from HuggingFace or custom URLs
- **Local Model Execution**: Run LLMs locally without external API dependencies
- **GPU Acceleration**: Configurable GPU layer offloading for faster inference
- **GGUF Format Support**: Full support for all GGUF quantized models
- **OpenAI-Compatible API**: Seamless integration using standard endpoints

#### Configuration Properties
- `ModelPath`: Local path to GGUF model file
- `ModelUrl`: URL to download model from if not present
- `AutoDownloadModel`: Enable/disable automatic model downloading
- `ContextSize`: Configurable context window size (default: 2048)
- `GpuLayers`: Number of layers to offload to GPU (default: 0)
- `Threads`: CPU thread count (default: auto-detect)
- `UseMemoryLock`: Lock model in RAM to prevent swapping
- `Seed`: Random seed for reproducible outputs

#### Download Features
- Progress logging every 5 seconds
- Automatic directory creation
- Thread-safe download mechanism
- Temporary file handling with cleanup
- Resume-safe downloads
- Comprehensive error handling

### Documentation

#### New Documentation
- **LLAMACPP-INTEGRATION.md**: Complete integration guide with examples
- **LLAMACPP-TESTING.md**: Comprehensive testing guide and benchmarks
- **llamacpp-config.example.json**: Detailed configuration examples

#### Updated Documentation
- README updated with LlamaCpp in supported providers
- Example configuration includes LlamaCpp backend

### Technical Details

- **Backend Class**: `LlamaCppLlmBackend` in `Services/`
- **Configuration**: Added LlamaCpp-specific properties to `LlmBackendConfig`
- **Factory Support**: Integrated into `LlmBackendFactory`
- **API Compatibility**: Supports both native and OpenAI-compatible endpoints

### Dependencies

- Requires llama.cpp server (external)
- No additional NuGet packages required
- Models downloaded on-demand

### Breaking Changes

None - This is a purely additive release

### Example Configuration

```json
{
  "Name": "LlamaCpp-Local",
  "Type": "LlamaCpp",
  "BaseUrl": "http://localhost:8080",
  "ModelPath": "./models/llama-3-8b-instruct-q4_k_m.gguf",
  "ModelUrl": "https://huggingface.co/QuantFactory/Meta-Llama-3-8B-Instruct-GGUF/resolve/main/Meta-Llama-3-8B-Instruct.Q4_K_M.gguf",
  "AutoDownloadModel": true,
  "ContextSize": 4096,
  "GpuLayers": 0,
  "Temperature": 0.7,
  "Enabled": true
}
```

### License Change

- **Changed from MIT to Unlicense**
- Released into the public domain
- No restrictions on use, modification, or distribution

---

## v2.0.0 - Major Release

### New Features

#### Provider Support
- Added Anthropic Claude support (Claude 3, Claude 3.5)
- Added Google Gemini support (AI Studio and Vertex AI)
- Added Cohere support (Command R, Command R+)
- Enhanced OpenAI support with latest models

#### Enterprise Features
- Circuit breaker pattern for fault tolerance
- Rate limiting with configurable windows
- Response caching (Memory, Redis, SQL Server)
- Comprehensive health checks and monitoring
- OpenTelemetry metrics and tracing
- Cost tracking per provider

#### Security
- Secrets management (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Managed identity support for cloud providers
- Environment variable configuration
- Secure API key handling

#### Configuration Enhancements
- Enhanced configuration model with validation
- Per-backend timeout and retry settings
- Backend-specific parameters (Azure deployment, Anthropic version, etc.)
- Stop sequences and advanced sampling parameters
- Token limits and cost tracking

#### Plugin Architecture
- Dynamic plugin loading from DLLs
- Custom LLM provider support
- Plugin validation and error handling
- NuGet-distributable plugins

### Breaking Changes

- Configuration model enhanced (backward compatible with v1.x)
- Some property defaults changed for better defaults
- Health check interface extended

### Documentation

- Complete integration guides for all providers
- Plugin development guide
- Migration guide from v1.x
- Comprehensive examples

---

## v1.x - Initial Release

### Features

- Multi-provider abstraction (OpenAI, Azure OpenAI, Ollama, LM Studio)
- Failover and round-robin strategies
- Retry logic with exponential backoff
- Basic health checks
- Configuration-based setup
- Logging and error handling

---

## Future Roadmap

### Planned Features

- Streaming response support for all backends
- Function calling/tools for more providers
- Embeddings generation across providers
- Fine-tuning integration
- Model performance analytics
- Advanced caching strategies
- Distributed tracing enhancements

### Community Requests

Have a feature request? Open an issue at:
https://github.com/scottgal/mostlyucid.llmbackend/issues

---

## Support

- **Documentation**: See `/docs` directory
- **Examples**: See `/examples` directory
- **Issues**: https://github.com/scottgal/mostlyucid.llmbackend/issues
- **Discussions**: https://github.com/scottgal/mostlyucid.llmbackend/discussions

---

## Contributors

Thank you to all contributors who made this release possible!

Special thanks to the llama.cpp team for their excellent local LLM server.
