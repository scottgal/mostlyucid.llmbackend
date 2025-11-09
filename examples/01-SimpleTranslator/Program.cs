using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

/*
 * Simple Translator Example with Cost Optimization
 *
 * Demonstrates:
 * - Multi-backend failover strategy for cost optimization
 * - Free local translation (EasyNMT + Ollama) with paid fallback
 * - Priority-based backend selection
 * - Budget tracking across multiple backends
 *
 * Backend Strategy:
 * 1. EasyNMT (Priority 1) - Free, specialized translation model
 * 2. Ollama/Qwen 2.5 1.5B (Priority 2) - Free, lightweight local model
 * 3. OpenAI GPT-4o-mini (Priority 3) - Paid fallback for complex translations
 */

var builder = WebApplication.CreateBuilder(args);

// Configure multi-backend failover for translation
builder.Services.AddLlmBackend(settings =>
{
    // Use failover strategy - tries backends in priority order
    settings.SelectionStrategy = BackendSelectionStrategy.Failover;

    settings.Backends = new List<LlmBackendConfig>
    {
        // PRIMARY: EasyNMT - Free, specialized translation
        new()
        {
            Name = "EasyNMT-Local",
            Type = LlmBackendType.EasyNMT,
            BaseUrl = "http://localhost:24080", // Default EasyNMT port
            ModelName = "opus-mt", // OPUS-MT models for translation
            Priority = 1, // Try this first
            Enabled = true
        },

        // SECONDARY: Ollama with lightweight model - Free, general purpose
        new()
        {
            Name = "Ollama-Qwen-1.5B",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434",
            ModelName = "qwen2.5:1.5b", // Lightweight but capable model
            Temperature = 0.3,
            Priority = 2, // Try if EasyNMT unavailable
            Enabled = true
        },

        // FALLBACK: OpenAI - Paid, high quality for complex text
        new()
        {
            Name = "OpenAI-Fallback",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? "your-api-key-here",
            ModelName = "gpt-4o-mini",
            Temperature = 0.3,
            Priority = 3, // Use only if local models fail

            // Cost tracking and budget protection
            CostPerMillionInputTokens = 0.15m,
            CostPerMillionOutputTokens = 0.60m,
            MaxSpendUsd = 2.00m, // $2 daily limit for fallback
            SpendResetPeriod = SpendResetPeriod.Daily,
            LogBudgetExceeded = true,
            Enabled = true
        }
    };

    // Enable telemetry to see which backend is used
    settings.Telemetry = new TelemetryConfig
    {
        EnableMetrics = true,
        LogTokenCounts = true
    };
});

var app = builder.Build();

// Simple translation endpoint
app.MapPost("/translate", async (TranslationRequest request, ILlmService llm) =>
{
    try
    {
        // For EasyNMT, we can use simple format
        // For LLM backends, we need more structured prompts
        var prompt = $"""
            Translate the following text from {request.SourceLanguage} to {request.TargetLanguage}.

            IMPORTANT INSTRUCTIONS:
            - Preserve the original formatting (line breaks, punctuation, etc.)
            - Do not add explanations or comments
            - Only output the translated text
            - Preserve any special characters or placeholders like {{variable}}, [tag], etc.

            Text to translate:
            {request.Text}

            Translation:
            """;

        var llmRequest = new LlmRequest
        {
            Prompt = prompt,
            Temperature = 0.3,
            MaxTokens = request.Text.Length * 3
        };

        var response = await llm.CompleteAsync(llmRequest);

        if (!response.Success)
        {
            return Results.Problem(
                detail: response.ErrorMessage,
                statusCode: 500,
                title: "Translation failed");
        }

        // Calculate cost (only for paid backends)
        decimal? cost = null;
        if (response.PromptTokens.HasValue && response.CompletionTokens.HasValue)
        {
            // Only OpenAI has cost in this setup
            if (response.BackendUsed?.Contains("OpenAI") == true)
            {
                cost = (response.PromptTokens.Value * 0.15m / 1_000_000m) +
                      (response.CompletionTokens.Value * 0.60m / 1_000_000m);
            }
        }

        Console.WriteLine($"Translation via {response.BackendUsed}: " +
                         $"{response.TotalTokens ?? 0} tokens, " +
                         $"${cost?.ToString("F6") ?? "FREE"}, " +
                         $"{response.DurationMs}ms");

        return Results.Ok(new TranslationResponse
        {
            TranslatedText = response.Content?.Trim() ?? "",
            SourceLanguage = request.SourceLanguage,
            TargetLanguage = request.TargetLanguage,
            BackendUsed = response.BackendUsed ?? "unknown",
            TokensUsed = response.TotalTokens,
            DurationMs = response.DurationMs,
            EstimatedCost = cost
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Translation error");
    }
});

// Batch translation endpoint
app.MapPost("/translate/batch", async (BatchTranslationRequest request, ILlmService llm) =>
{
    var results = new List<TranslationResult>();
    var totalTokens = 0;
    var totalCost = 0m;
    var backendUsage = new Dictionary<string, int>();

    foreach (var text in request.Texts)
    {
        var prompt = $"""
            Translate from {request.SourceLanguage} to {request.TargetLanguage}.
            Preserve formatting. Only output the translation.

            Text: {text}

            Translation:
            """;

        var response = await llm.CompleteAsync(new LlmRequest
        {
            Prompt = prompt,
            Temperature = 0.3,
            MaxTokens = text.Length * 3
        });

        if (response.Success && response.Content != null)
        {
            totalTokens += response.TotalTokens ?? 0;

            // Track backend usage
            var backend = response.BackendUsed ?? "unknown";
            backendUsage[backend] = backendUsage.GetValueOrDefault(backend) + 1;

            // Calculate cost for paid backends
            if (response.PromptTokens.HasValue && response.CompletionTokens.HasValue &&
                backend.Contains("OpenAI"))
            {
                totalCost += (response.PromptTokens.Value * 0.15m / 1_000_000m) +
                            (response.CompletionTokens.Value * 0.60m / 1_000_000m);
            }

            results.Add(new TranslationResult
            {
                Original = text,
                Translated = response.Content.Trim(),
                Backend = backend
            });
        }
        else
        {
            results.Add(new TranslationResult
            {
                Original = text,
                Translated = $"[Translation failed: {response.ErrorMessage}]",
                Backend = "error"
            });
        }
    }

    Console.WriteLine($"Batch translation: {results.Count} texts");
    foreach (var (backend, count) in backendUsage)
    {
        Console.WriteLine($"  - {backend}: {count} translations");
    }
    Console.WriteLine($"Total: {totalTokens} tokens, ${totalCost:F6} cost");

    return Results.Ok(new BatchTranslationResponse
    {
        Translations = results,
        TotalTokens = totalTokens,
        EstimatedCost = totalCost,
        BackendUsage = backendUsage
    });
});

// Health check endpoint
app.MapGet("/health", async (ILlmService llm) =>
{
    var backends = await llm.TestBackendsAsync();
    var status = backends.Select(b => new
    {
        backend = b.Key,
        healthy = b.Value.IsHealthy,
        avgLatency = b.Value.AverageLatencyMs,
        successCount = b.Value.SuccessfulRequests,
        failedCount = b.Value.FailedRequests
    });

    return Results.Ok(status);
});

// Welcome page
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Cost-Optimized Translator API</title>
        <style>
            body { font-family: Arial, sans-serif; max-width: 900px; margin: 50px auto; padding: 20px; }
            code { background: #f4f4f4; padding: 2px 6px; border-radius: 3px; font-size: 14px; }
            pre { background: #f4f4f4; padding: 15px; border-radius: 5px; overflow-x: auto; font-size: 13px; }
            .backend { padding: 10px; margin: 10px 0; border-left: 4px solid #ddd; }
            .primary { border-color: #28a745; }
            .secondary { border-color: #ffc107; }
            .fallback { border-color: #dc3545; }
        </style>
    </head>
    <body>
        <h1>🌍 Cost-Optimized Translator API</h1>
        <p>Powered by mostlylucid.llmbackend with intelligent failover</p>

        <h2>Backend Strategy</h2>
        <div class="backend primary">
            <strong>🥇 Primary: EasyNMT (Free)</strong><br>
            Specialized translation model running locally. No cost, optimized for translations.
        </div>
        <div class="backend secondary">
            <strong>🥈 Secondary: Ollama Qwen 2.5 1.5B (Free)</strong><br>
            Lightweight local LLM. Falls back here if EasyNMT is unavailable.
        </div>
        <div class="backend fallback">
            <strong>🥉 Fallback: OpenAI GPT-4o-mini (Paid)</strong><br>
            Cloud API for complex translations. Only used if local models fail. $2 daily budget.
        </div>

        <h2>Quick Start</h2>
        <h3>1. Start Local Services</h3>
        <pre>
    # Start EasyNMT (optional - will use Ollama if not running)
    docker run -p 24080:80 easynmt/api:2.0-cpu

    # Start Ollama with qwen2.5:1.5b
    ollama pull qwen2.5:1.5b
    ollama serve</pre>

        <h3>2. Translate Text</h3>
        <pre>
    curl -X POST http://localhost:5000/translate \
      -H "Content-Type: application/json" \
      -d '{
        "text": "Hello, world!",
        "sourceLanguage": "English",
        "targetLanguage": "Spanish"
      }'</pre>

        <h3>3. Batch Translation</h3>
        <pre>
    curl -X POST http://localhost:5000/translate/batch \
      -H "Content-Type: application/json" \
      -d '{
        "texts": ["Hello", "Goodbye", "Thank you"],
        "sourceLanguage": "English",
        "targetLanguage": "French"
      }'</pre>

        <h2>Endpoints</h2>
        <ul>
            <li><code>POST /translate</code> - Translate single text</li>
            <li><code>POST /translate/batch</code> - Translate multiple texts (shows backend usage)</li>
            <li><code>GET /health</code> - Check all backends health</li>
        </ul>

        <h2>Features</h2>
        <ul>
            <li>✅ Automatic cost optimization (tries free backends first)</li>
            <li>✅ Transparent backend selection (shows which was used)</li>
            <li>✅ Budget protection on paid backends</li>
            <li>✅ Batch translation support</li>
            <li>✅ Health monitoring for all backends</li>
        </ul>

        <p><small>Set <code>OPENAI_API_KEY</code> environment variable to enable paid fallback.</small></p>
    </body>
    </html>
    """, "text/html"));

app.Run();

// Request/Response models
record TranslationRequest(string Text, string SourceLanguage, string TargetLanguage);
record TranslationResponse
{
    public required string TranslatedText { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required string BackendUsed { get; init; }
    public int? TokensUsed { get; init; }
    public long? DurationMs { get; init; }
    public decimal? EstimatedCost { get; init; }
}
record BatchTranslationRequest(List<string> Texts, string SourceLanguage, string TargetLanguage);
record TranslationResult
{
    public required string Original { get; init; }
    public required string Translated { get; init; }
    public required string Backend { get; init; }
}
record BatchTranslationResponse
{
    public required List<TranslationResult> Translations { get; init; }
    public int TotalTokens { get; init; }
    public decimal EstimatedCost { get; init; }
    public required Dictionary<string, int> BackendUsage { get; init; }
}
