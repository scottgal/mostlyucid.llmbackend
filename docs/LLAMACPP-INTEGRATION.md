# LlamaCpp Backend Integration Guide

## Overview

The LlamaCpp backend integration provides native support for running local GGUF format models using the llama.cpp server. This integration includes automatic model downloading, eliminating the need for external dependencies beyond the llama.cpp server itself.

## Key Features

- **Automatic Model Download**: Specify a model URL and the backend will download it automatically on first use
- **GGUF Format Support**: Works with all GGUF quantized models
- **OpenAI-Compatible API**: Uses standard OpenAI-compatible endpoints for easy integration
- **GPU Acceleration**: Configurable GPU layer offloading for faster inference
- **Progress Logging**: Detailed download progress and status logging
- **No External Dependencies**: Models are downloaded and managed locally

## Prerequisites

### 1. Install llama.cpp Server

Download and build llama.cpp from the official repository:

```bash
# Clone the repository
git clone https://github.com/ggerganov/llama.cpp.git
cd llama.cpp

# Build with default options (CPU only)
make

# Or build with GPU support (CUDA)
make LLAMA_CUDA=1

# Or build with GPU support (Metal on macOS)
make LLAMA_METAL=1
```

### 2. Start the llama.cpp Server

The server must be running before the LlamaCpp backend can connect to it:

```bash
# Basic server start (the backend will handle model loading)
./llama-server --port 8080 -c 4096

# Or with GPU acceleration
./llama-server --port 8080 -c 4096 -ngl 32

# Or with specific model already loaded
./llama-server -m ./models/model.gguf --port 8080 -c 4096
```

## Configuration

### Basic Configuration

Add a LlamaCpp backend to your `appsettings.json`:

```json
{
  "LlmSettings": {
    "Backends": [
      {
        "Name": "LlamaCpp-Local",
        "Type": "LlamaCpp",
        "BaseUrl": "http://localhost:8080",
        "ModelName": "llama-3-8b-instruct",

        "ModelPath": "./models/llama-3-8b-instruct-q4_k_m.gguf",
        "ModelUrl": "https://huggingface.co/QuantFactory/Meta-Llama-3-8B-Instruct-GGUF/resolve/main/Meta-Llama-3-8B-Instruct.Q4_K_M.gguf",
        "AutoDownloadModel": true,

        "ContextSize": 4096,
        "GpuLayers": 0,
        "Enabled": true,
        "Priority": 1
      }
    ]
  }
}
```

### Configuration Properties

#### Model Configuration

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `ModelPath` | string | Local path where the GGUF model file should be stored | null |
| `ModelUrl` | string | URL to download the model from if it doesn't exist | null |
| `AutoDownloadModel` | bool | Automatically download model if it doesn't exist | true |

#### LlamaCpp-Specific Settings

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `ContextSize` | int | Maximum context window size in tokens | 2048 |
| `GpuLayers` | int | Number of layers to offload to GPU (0 = CPU only) | 0 |
| `Threads` | int | Number of CPU threads (null = auto-detect) | null |
| `UseMemoryLock` | bool | Lock model in RAM to prevent swapping | false |
| `Seed` | int | Random seed for reproducible outputs (-1 = random) | -1 |

#### Standard LLM Settings

All standard LLM settings are supported:

- `Temperature`: Sampling temperature (0.0 - 2.0)
- `MaxInputTokens`: Maximum input context size
- `MaxOutputTokens`: Maximum completion length
- `TopP`: Top-p sampling parameter
- `FrequencyPenalty`: Frequency penalty (-2.0 to 2.0)
- `PresencePenalty`: Presence penalty (-2.0 to 2.0)
- `StopSequences`: Custom stop sequences

## Usage Examples

### Example 1: Auto-Download Llama 3 8B

```json
{
  "Name": "LlamaCpp-Llama3",
  "Type": "LlamaCpp",
  "BaseUrl": "http://localhost:8080",
  "ModelName": "llama-3-8b-instruct",

  "ModelPath": "./models/llama-3-8b-instruct-q4_k_m.gguf",
  "ModelUrl": "https://huggingface.co/QuantFactory/Meta-Llama-3-8B-Instruct-GGUF/resolve/main/Meta-Llama-3-8B-Instruct.Q4_K_M.gguf",
  "AutoDownloadModel": true,

  "ContextSize": 4096,
  "Temperature": 0.7,
  "MaxOutputTokens": 2000,
  "Enabled": true
}
```

### Example 2: GPU-Accelerated Mistral 7B

```json
{
  "Name": "LlamaCpp-Mistral-GPU",
  "Type": "LlamaCpp",
  "BaseUrl": "http://localhost:8081",
  "ModelName": "mistral-7b-instruct",

  "ModelPath": "./models/mistral-7b-instruct-v0.2-q5_k_m.gguf",
  "ModelUrl": "https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/mistral-7b-instruct-v0.2.Q5_K_M.gguf",
  "AutoDownloadModel": true,

  "ContextSize": 8192,
  "GpuLayers": 32,
  "Threads": 8,
  "UseMemoryLock": true,
  "Temperature": 0.8,
  "Enabled": true
}
```

### Example 3: Existing Model (No Download)

```json
{
  "Name": "LlamaCpp-Custom",
  "Type": "LlamaCpp",
  "BaseUrl": "http://localhost:8082",
  "ModelName": "custom-model",

  "ModelPath": "/path/to/existing/model.gguf",
  "AutoDownloadModel": false,

  "ContextSize": 2048,
  "Enabled": true
}
```

## C# Usage

### Basic Completion

```csharp
using Mostlyucid.LlmBackend;
using Mostlyucid.LlmBackend.Models;

// Configure services
services.AddLlmBackend(configuration);

// Use the service
var llmService = serviceProvider.GetRequiredService<ILlmService>();

var request = new LlmRequest
{
    Prompt = "Explain quantum computing in simple terms.",
    Temperature = 0.7,
    MaxTokens = 500
};

var response = await llmService.CompleteAsync(request);
Console.WriteLine(response.Text);
```

### Chat Completion

```csharp
var chatRequest = new ChatRequest
{
    Messages = new List<ChatMessage>
    {
        new() { Role = "system", Content = "You are a helpful assistant." },
        new() { Role = "user", Content = "What is the capital of France?" }
    },
    Temperature = 0.7,
    MaxTokens = 100
};

var response = await llmService.ChatAsync(chatRequest);
Console.WriteLine(response.Text);
```

### Direct Backend Usage

```csharp
// Get specific backend
var factory = serviceProvider.GetRequiredService<LlmBackendFactory>();
var config = new LlmBackendConfig
{
    Name = "Test",
    Type = LlmBackendType.LlamaCpp,
    BaseUrl = "http://localhost:8080",
    ModelPath = "./models/model.gguf",
    ModelUrl = "https://example.com/model.gguf",
    AutoDownloadModel = true,
    ContextSize = 4096
};

var backend = factory.CreateBackend(config);

// Check availability (will download model if needed)
var isAvailable = await backend.IsAvailableAsync();

// Make request
var response = await backend.CompleteAsync(new LlmRequest
{
    Prompt = "Hello, world!",
    MaxTokens = 50
});
```

## Model Sources

### HuggingFace Repositories

**TheBloke** - Huge collection of quantized models:
- https://huggingface.co/TheBloke
- Various quantization levels (Q4, Q5, Q8)

**QuantFactory** - Latest models in GGUF format:
- https://huggingface.co/QuantFactory

### Recommended Models

**Small/Fast (1-3B parameters)**
- TinyLlama 1.1B: Fast, good for simple tasks
- Phi-2 2.7B: Microsoft's efficient small model

**Medium (7-8B parameters)**
- Llama 3 8B: Meta's latest, very capable
- Mistral 7B: Excellent performance/size ratio
- Zephyr 7B: Fine-tuned for chat

**Large (13B+ parameters)**
- Llama 2 13B: Larger version with better quality
- Mixtral 8x7B: Mixture of experts, very capable

### Quantization Levels

Models are available in different quantization levels, balancing size vs. quality:

| Quantization | Size | Quality | Use Case |
|--------------|------|---------|----------|
| Q2_K | Smallest | Lower | Testing, resource-constrained |
| Q4_K_M | Small | Good | **Recommended for most uses** |
| Q5_K_M | Medium | Better | Good balance of size/quality |
| Q6_K | Large | High | When quality is important |
| Q8_0 | Largest | Highest | Maximum quality needed |

**Recommendation**: Start with Q4_K_M quantization for the best balance of size and quality.

## Download Behavior

### Automatic Download Process

1. **First Request**: When the backend receives its first request
2. **Check Existence**: Checks if model file exists at `ModelPath`
3. **Download**: If not found and `AutoDownloadModel` is true:
   - Creates directory if needed
   - Downloads from `ModelUrl` to temporary file
   - Shows progress every 5 seconds
   - Moves to final location when complete
4. **Skip**: If model exists or `AutoDownloadModel` is false, skips download

### Logging Examples

```
[LlamaCpp-Local] Downloading model from https://huggingface.co/...
[LlamaCpp-Local] Download progress: 25.3% (512 MB / 2048 MB bytes)
[LlamaCpp-Local] Download progress: 50.1% (1024 MB / 2048 MB bytes)
[LlamaCpp-Local] Download progress: 75.8% (1536 MB / 2048 MB bytes)
[LlamaCpp-Local] Model downloaded successfully to ./models/model.gguf (2048 MB bytes)
```

### Error Handling

If download fails:
- Error is logged with details
- Temporary file is cleaned up
- Backend remains unavailable until issue is resolved

### Manual Download

Alternatively, manually download models:

```bash
# Create models directory
mkdir -p ./models

# Download from HuggingFace
wget https://huggingface.co/TheBloke/Llama-2-7B-GGUF/resolve/main/llama-2-7b.Q4_K_M.gguf \
  -O ./models/llama-2-7b-q4_k_m.gguf

# Configure without auto-download
{
  "ModelPath": "./models/llama-2-7b-q4_k_m.gguf",
  "AutoDownloadModel": false
}
```

## Performance Tuning

### CPU-Only Configuration

For systems without GPU:

```json
{
  "ContextSize": 2048,
  "GpuLayers": 0,
  "Threads": null,
  "UseMemoryLock": false
}
```

### GPU-Accelerated Configuration

For systems with NVIDIA GPU:

```json
{
  "ContextSize": 4096,
  "GpuLayers": 32,
  "Threads": 8,
  "UseMemoryLock": true
}
```

**GPU Layers Guidelines**:
- 7B models: 20-40 layers
- 13B models: 30-50 layers
- Larger models: 40-60+ layers
- Set to 99 to offload all layers

### Context Size Recommendations

| Model Size | Min Context | Recommended | Max Context |
|------------|-------------|-------------|-------------|
| 1-3B | 512 | 2048 | 4096 |
| 7-8B | 1024 | 4096 | 8192 |
| 13B+ | 2048 | 4096 | 16384 |

Larger context = more memory usage but can handle longer conversations.

## Troubleshooting

### Server Not Running

**Error**: "Availability check failed"

**Solution**: Ensure llama.cpp server is running:
```bash
./llama-server --port 8080 -c 4096
```

### Download Fails

**Error**: "Failed to download model from..."

**Solutions**:
1. Check internet connectivity
2. Verify URL is correct and accessible
3. Ensure sufficient disk space
4. Check write permissions on ModelPath directory

### Out of Memory

**Error**: Server crashes or hangs

**Solutions**:
1. Reduce `ContextSize`
2. Reduce `GpuLayers` (use more CPU)
3. Use smaller quantized model (Q4 instead of Q8)
4. Use smaller model (7B instead of 13B)

### Slow Performance

**Solutions**:
1. Increase `GpuLayers` if GPU available
2. Adjust `Threads` for your CPU
3. Enable `UseMemoryLock` if enough RAM
4. Use smaller quantized model
5. Reduce `ContextSize`

### Model Not Loading

**Error**: "Model not found" or similar

**Solutions**:
1. Check `ModelPath` is correct
2. Verify model file downloaded completely
3. Check file is valid GGUF format
4. Review logs for download errors

## Advanced Features

### Multiple Backends

Run multiple LlamaCpp instances with different models:

```json
{
  "Backends": [
    {
      "Name": "Fast-Model",
      "Type": "LlamaCpp",
      "BaseUrl": "http://localhost:8080",
      "ModelPath": "./models/tiny-llama.gguf",
      "Priority": 1
    },
    {
      "Name": "Quality-Model",
      "Type": "LlamaCpp",
      "BaseUrl": "http://localhost:8081",
      "ModelPath": "./models/llama-3-70b.gguf",
      "Priority": 2
    }
  ],
  "SelectionStrategy": "Failover"
}
```

### Failover Configuration

Use LlamaCpp as fallback for cloud providers:

```json
{
  "SelectionStrategy": "Failover",
  "Backends": [
    {
      "Name": "OpenAI-Primary",
      "Type": "OpenAI",
      "Priority": 1
    },
    {
      "Name": "LlamaCpp-Fallback",
      "Type": "LlamaCpp",
      "BaseUrl": "http://localhost:8080",
      "ModelPath": "./models/model.gguf",
      "AutoDownloadModel": true,
      "Priority": 99
    }
  ]
}
```

## API Compatibility

The LlamaCpp backend supports both native llama.cpp endpoints and OpenAI-compatible endpoints:

### Native Endpoint (Completion)

```
POST http://localhost:8080/completion
```

Used by `CompleteAsync()` method.

### OpenAI-Compatible Endpoint (Chat)

```
POST http://localhost:8080/v1/chat/completions
```

Used by `ChatAsync()` method.

### Health Check Endpoints

```
GET http://localhost:8080/health
GET http://localhost:8080/v1/models
```

Used by `IsAvailableAsync()` method.

## Best Practices

1. **Start Small**: Begin with Q4_K_M quantized 7B models
2. **Test Locally**: Verify configuration with small models first
3. **Monitor Resources**: Watch RAM/VRAM usage during inference
4. **Use Auto-Download**: Let the backend handle model downloads
5. **Set Priorities**: Configure failover from cloud to local
6. **Enable Logging**: Use detailed logging during initial setup
7. **Optimize Context**: Only use context size you need
8. **GPU Layers**: Tune based on your GPU's VRAM capacity

## Security Considerations

1. **Local Only**: LlamaCpp is designed for local use
2. **No API Keys**: No external API keys needed
3. **Network**: Ensure llama.cpp server is not exposed publicly
4. **Model Source**: Download models from trusted sources only
5. **Disk Space**: Monitor disk usage for large models

## Support and Resources

- **llama.cpp GitHub**: https://github.com/ggerganov/llama.cpp
- **Model Repository**: https://huggingface.co/models?library=gguf
- **Documentation**: See `examples/llamacpp-config.example.json`
- **Issues**: Report bugs to the library repository

## Version History

- **v2.1.0**: Initial LlamaCpp integration with auto-download support
