# Mostlyucid.LlmBackend.Providers.LlamaCpp

LlamaCpp provider for Mostlyucid.LlmBackend library. Supports locally-hosted GGUF models with automatic downloading capability, ideal for running tiny embedded models.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.LlamaCpp
```

## Prerequisites

Install and run [llama.cpp](https://github.com/ggerganov/llama.cpp) server locally.

```bash
# Build llama.cpp
git clone https://github.com/ggerganov/llama.cpp
cd llama.cpp
make

# Run the server (example with a model)
./server -m models/your-model.gguf --port 8080
```

## Usage

### Basic Configuration

```csharp
services.AddLlmBackend(config =>
{
    config.Backends.Add(new LlmBackendConfig
    {
        Type = LlmBackendType.LlamaCpp,
        Name = "LlamaCpp-Local",
        BaseUrl = "http://localhost:8080",
        ModelName = "tinyllama-1.1b",
        Enabled = true
    });
});
```

### With Automatic Model Downloading

```csharp
config.Backends.Add(new LlmBackendConfig
{
    Type = LlmBackendType.LlamaCpp,
    Name = "LlamaCpp-TinyModel",
    BaseUrl = "http://localhost:8080",
    ModelName = "tinyllama-1.1b",

    // Automatic download settings
    AutoDownloadModel = true,
    ModelPath = "./models/tinyllama-1.1b.gguf",
    ModelUrl = "https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",

    // LlamaCpp-specific settings
    ContextSize = 2048,
    Threads = 4,
    GpuLayers = 0, // 0 = CPU only, increase for GPU acceleration
    Seed = -1, // -1 = random

    Enabled = true
});
```

## Supported Models

Works with any GGUF format model:
- **Tiny Models** (ideal for embedded use):
  - TinyLlama 1.1B
  - Phi-2 (2.7B)
  - StableLM-Zephyr-3B
- **Small Models**:
  - Llama 2 7B
  - Mistral 7B
  - Mixtral 8x7B
- **Quantized versions** (Q4_K_M, Q5_K_M, etc.)

Find models at [Hugging Face](https://huggingface.co/models?library=gguf)

## Features

### Core Features
- ✅ **Automatic Model Downloading** - Download models from URLs on first use
- ✅ **GGUF Format Support** - Native support for llama.cpp model format
- ✅ **Tiny Model Support** - Run 1-3B parameter models efficiently
- ✅ **CPU & GPU Acceleration** - Offload layers to GPU for faster inference
- ✅ **OpenAI-Compatible API** - Chat completions endpoint compatible

### Benefits
- **No API Costs** - Run models locally without cloud fees
- **Privacy** - Data stays on your machine
- **Offline Capability** - Works without internet (after model download)
- **Low Resource Usage** - Tiny models work on modest hardware
- **Customizable** - Fine-tune context size, threads, and GPU layers

## Configuration Options

### LlamaCpp-Specific Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ModelPath` | string | null | Local path to GGUF model file |
| `ModelUrl` | string | null | URL to download model from if not found locally |
| `AutoDownloadModel` | bool | true | Automatically download model if it doesn't exist |
| `ContextSize` | int? | 2048 | Context window size (tokens) |
| `GpuLayers` | int? | 0 | Number of layers to offload to GPU (0 = CPU only) |
| `Threads` | int? | auto | Number of CPU threads to use |
| `Seed` | int? | -1 | Random seed (-1 = random) |
| `UseMemoryLock` | bool | false | Lock memory to prevent swapping |

### Example: Production Configuration

```json
{
  "LlmSettings": {
    "Backends": [
      {
        "Type": "LlamaCpp",
        "Name": "TinyAssistant",
        "BaseUrl": "http://localhost:8080",
        "ModelName": "tinyllama-1.1b-chat",

        "AutoDownloadModel": true,
        "ModelPath": "/app/models/tinyllama-1.1b.gguf",
        "ModelUrl": "https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",

        "ContextSize": 2048,
        "Threads": 4,
        "GpuLayers": 35,

        "Temperature": 0.7,
        "MaxOutputTokens": 512,

        "CostPerMillionInputTokens": 0,
        "CostPerMillionOutputTokens": 0,

        "Enabled": true
      }
    ]
  }
}
```

## Use Cases

### Ideal For:
- **Embedded AI** - Run AI on edge devices or in containers
- **Cost Optimization** - Zero API costs for high-volume scenarios
- **Privacy-Sensitive** - Keep data processing local
- **Offline Applications** - Work without internet connectivity
- **Development/Testing** - Test without consuming API quotas
- **Simple Tasks** - Classification, extraction, simple Q&A

### When to Use Tiny Models (1-3B params):
- Text classification
- Entity extraction
- Simple summarization
- Code completion (basic)
- Structured data extraction
- Sentiment analysis

### When to Upgrade to Larger Models:
- Complex reasoning
- Creative writing
- Technical documentation
- Multi-step tasks
- Domain-specific expertise

## Performance Tips

1. **Choose the Right Quantization**:
   - Q4_K_M: Best balance of quality and speed
   - Q5_K_M: Better quality, slightly slower
   - Q8_0: Near-original quality, slower

2. **GPU Acceleration**:
   ```csharp
   GpuLayers = 35  // Offload all layers to GPU
   ```

3. **Thread Optimization**:
   ```csharp
   Threads = Environment.ProcessorCount - 2  // Leave some CPU for system
   ```

4. **Context Size**:
   - Lower = faster, less memory
   - Higher = more context retention

## Troubleshooting

### Model Download Fails
- Check internet connectivity
- Verify ModelUrl is accessible
- Ensure sufficient disk space
- Check write permissions for ModelPath

### llama.cpp Server Not Responding
```bash
# Check if server is running
curl http://localhost:8080/health

# Start server with verbose logging
./server -m models/model.gguf --port 8080 --verbose
```

### Out of Memory
- Reduce `ContextSize`
- Use more aggressive quantization (Q4 instead of Q8)
- Reduce `GpuLayers` if using GPU
- Use a smaller model (TinyLlama instead of Llama 2)

### Slow Performance
- Increase `GpuLayers` if you have a GPU
- Optimize `Threads` for your CPU
- Use Q4 quantization instead of Q8
- Reduce `ContextSize`

## Examples

### Simple Chat

```csharp
var llmService = serviceProvider.GetRequiredService<ILlmService>();

var response = await llmService.ChatAsync(new ChatRequest
{
    Messages = new List<ChatMessage>
    {
        new() { Role = "user", Content = "What is 2+2?" }
    },
    MaxTokens = 100,
    Temperature = 0.7
});

Console.WriteLine(response.Text);
```

### Text Classification

```csharp
var request = new LlmRequest
{
    Prompt = "Classify the sentiment of: 'I love this product!'\nSentiment:",
    MaxTokens = 10,
    Temperature = 0.3,
    PreferredBackend = "LlamaCpp-TinyModel"
};

var response = await llmService.CompleteAsync(request);
// Output: "Positive"
```

## License

MIT

## Links

- [llama.cpp](https://github.com/ggerganov/llama.cpp) - The underlying engine
- [GGUF Models on Hugging Face](https://huggingface.co/models?library=gguf)
- [Mostlyucid.LlmBackend Documentation](https://github.com/scottgal/mostlyucid.llmbackend)
