using System.Collections.Concurrent;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

/*
 * Chat Interface Example
 *
 * Demonstrates:
 * - Conversation history management with context memory
 * - Multi-model support (let user choose between Ollama and OpenAI)
 * - Rate limiting to prevent API abuse
 * - Budget tracking across conversations
 * - Simple web UI with chat history
 *
 * Features a minimal chat UI where users can:
 * - Select between local (free) and cloud (paid) models
 * - View conversation history
 * - Track costs in real-time
 */

var builder = WebApplication.CreateBuilder(args);

// Configure multiple backends for chat
builder.Services.AddLlmBackend(settings =>
{
    // Allow user to choose backend
    settings.SelectionStrategy = BackendSelectionStrategy.Specific;

    // Enable rate limiting to prevent abuse
    settings.RateLimit = new RateLimitConfig
    {
        Enabled = true,
        MaxRequests = 60,      // 60 requests
        WindowSeconds = 60,    // per minute
        MaxConcurrentRequests = 5
    };

    settings.Backends = new List<LlmBackendConfig>
    {
        // Local Ollama - Free, good for development/testing
        new()
        {
            Name = "ollama-llama3",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434",
            ModelName = "llama3",
            Temperature = 0.7,
            Enabled = true
        },

        // OpenAI GPT-4o-mini - Paid, better quality
        new()
        {
            Name = "openai-gpt4o-mini",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? "your-api-key-here",
            ModelName = "gpt-4o-mini",
            Temperature = 0.7,

            // Budget tracking
            CostPerMillionInputTokens = 0.15m,
            CostPerMillionOutputTokens = 0.60m,
            MaxSpendUsd = 1.00m, // $1 daily limit
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        },

        // Anthropic Claude - Paid alternative
        new()
        {
            Name = "anthropic-claude",
            Type = LlmBackendType.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? "your-api-key-here",
            ModelName = "claude-3-5-sonnet-20241022",
            Temperature = 0.7,

            // Budget tracking
            CostPerMillionInputTokens = 3.00m,
            CostPerMillionOutputTokens = 15.00m,
            MaxSpendUsd = 2.00m, // $2 daily limit
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        }
    };

    settings.Telemetry = new TelemetryConfig
    {
        EnableMetrics = true,
        LogTokenCounts = true
    };
});

// In-memory conversation storage (use database in production)
var conversations = new ConcurrentDictionary<string, List<ChatMessage>>();

var app = builder.Build();

// Chat endpoint
app.MapPost("/chat", async (ChatRequest request, ILlmService llm) =>
{
    var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

    // Get or create conversation history
    var history = conversations.GetOrAdd(conversationId, _ => new List<ChatMessage>
    {
        new() { Role = "system", Content = request.SystemPrompt ?? "You are a helpful, friendly assistant." }
    });

    // Add user message to history
    history.Add(new() { Role = "user", Content = request.Message });

    // Build chat request with history
    var chatRequest = new Mostlyucid.LlmBackend.Models.ChatRequest
    {
        Messages = history,
        Temperature = 0.7,
        MaxTokens = 2000,
        BackendName = request.Model // User-selected backend
    };

    var response = await llm.ChatAsync(chatRequest);

    if (!response.Success)
    {
        return Results.Problem(
            detail: response.ErrorMessage,
            statusCode: 500,
            title: "Chat failed");
    }

    // Add assistant response to history
    history.Add(new() { Role = "assistant", Content = response.Content ?? "" });

    // Calculate cost for paid models
    decimal? cost = null;
    if (response.PromptTokens.HasValue && response.CompletionTokens.HasValue)
    {
        var backend = response.BackendUsed ?? "";
        if (backend.Contains("openai"))
        {
            cost = (response.PromptTokens.Value * 0.15m / 1_000_000m) +
                  (response.CompletionTokens.Value * 0.60m / 1_000_000m);
        }
        else if (backend.Contains("anthropic"))
        {
            cost = (response.PromptTokens.Value * 3.00m / 1_000_000m) +
                  (response.CompletionTokens.Value * 15.00m / 1_000_000m);
        }
    }

    return Results.Ok(new ChatResponse
    {
        ConversationId = conversationId,
        Message = response.Content ?? "",
        Model = response.BackendUsed ?? "unknown",
        TokensUsed = response.TotalTokens,
        EstimatedCost = cost,
        MessageCount = history.Count - 1 // Exclude system message
    });
});

// Get conversation history
app.MapGet("/conversations/{id}", (string id) =>
{
    if (conversations.TryGetValue(id, out var history))
    {
        return Results.Ok(history.Where(m => m.Role != "system"));
    }
    return Results.NotFound();
});

// Clear conversation
app.MapDelete("/conversations/{id}", (string id) =>
{
    conversations.TryRemove(id, out _);
    return Results.Ok();
});

// List available models
app.MapGet("/models", () =>
{
    var models = new[]
    {
        new { id = "ollama-llama3", name = "Llama 3 (Free - Local)", cost = "FREE" },
        new { id = "openai-gpt4o-mini", name = "GPT-4o-mini (Paid)", cost = "$0.15/$0.60 per 1M tokens" },
        new { id = "anthropic-claude", name = "Claude 3.5 Sonnet (Paid)", cost = "$3/$15 per 1M tokens" }
    };
    return Results.Ok(models);
});

// Simple chat UI
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Chat Interface</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { font-family: Arial, sans-serif; height: 100vh; display: flex; flex-direction: column; }
            header { background: #007bff; color: white; padding: 15px; }
            .chat-container { flex: 1; display: flex; flex-direction: column; max-width: 900px; margin: 0 auto; width: 100%; }
            .messages { flex: 1; overflow-y: auto; padding: 20px; background: #f5f5f5; }
            .message { margin: 10px 0; padding: 10px 15px; border-radius: 8px; max-width: 70%; }
            .user { background: #007bff; color: white; margin-left: auto; }
            .assistant { background: white; }
            .input-area { padding: 20px; background: white; border-top: 1px solid #ddd; }
            .input-row { display: flex; gap: 10px; margin-bottom: 10px; }
            input, select { padding: 10px; font-size: 14px; border: 1px solid #ddd; border-radius: 4px; }
            input[type="text"] { flex: 1; }
            button { padding: 10px 20px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }
            button:hover { background: #0056b3; }
            .info { font-size: 12px; color: #666; margin-top: 10px; }
            .cost { color: #28a745; font-weight: bold; }
        </style>
    </head>
    <body>
        <header>
            <h1>💬 Chat Interface</h1>
            <p>Powered by mostlylucid.llmbackend</p>
        </header>

        <div class="chat-container">
            <div class="messages" id="messages">
                <div class="message assistant">
                    <strong>Assistant:</strong> Hello! I'm here to help. Select a model and start chatting!
                </div>
            </div>

            <div class="input-area">
                <div class="input-row">
                    <select id="model">
                        <option value="ollama-llama3">Llama 3 (Free - Local)</option>
                        <option value="openai-gpt4o-mini" selected>GPT-4o-mini (Paid - Best Quality)</option>
                        <option value="anthropic-claude">Claude 3.5 Sonnet (Paid - Long Context)</option>
                    </select>
                    <button onclick="clearChat()">Clear Chat</button>
                </div>
                <div class="input-row">
                    <input type="text" id="input" placeholder="Type your message..." onkeypress="if(event.key==='Enter') sendMessage()">
                    <button onclick="sendMessage()">Send</button>
                </div>
                <div class="info">
                    Messages: <span id="msgCount">0</span> |
                    Total cost: $<span id="totalCost" class="cost">0.000000</span>
                </div>
            </div>
        </div>

        <script>
            let conversationId = null;
            let totalCost = 0;
            let messageCount = 0;

            async function sendMessage() {
                const input = document.getElementById('input');
                const model = document.getElementById('model').value;
                const message = input.value.trim();

                if (!message) return;

                // Add user message to UI
                addMessage('user', message);
                input.value = '';

                // Send to backend
                try {
                    const response = await fetch('/chat', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            message: message,
                            model: model,
                            conversationId: conversationId
                        })
                    });

                    const data = await response.json();
                    conversationId = data.conversationId;

                    // Add assistant response to UI
                    addMessage('assistant', data.message, data.model, data.estimatedCost);

                    // Update stats
                    messageCount = data.messageCount;
                    if (data.estimatedCost) {
                        totalCost += data.estimatedCost;
                    }
                    document.getElementById('msgCount').textContent = messageCount;
                    document.getElementById('totalCost').textContent = totalCost.toFixed(6);

                } catch (error) {
                    addMessage('assistant', `Error: ${error.message}`);
                }
            }

            function addMessage(role, content, model, cost) {
                const messagesDiv = document.getElementById('messages');
                const messageDiv = document.createElement('div');
                messageDiv.className = `message ${role}`;

                let costInfo = '';
                if (cost !== undefined && cost > 0) {
                    costInfo = ` <small>(cost: $${cost.toFixed(6)})</small>`;
                } else if (cost !== undefined) {
                    costInfo = ' <small>(FREE)</small>';
                }

                messageDiv.innerHTML = `<strong>${role === 'user' ? 'You' : 'Assistant'}:</strong> ${content}${costInfo}`;
                messagesDiv.appendChild(messageDiv);
                messagesDiv.scrollTop = messagesDiv.scrollHeight;
            }

            function clearChat() {
                if (conversationId) {
                    fetch(`/conversations/${conversationId}`, { method: 'DELETE' });
                }
                document.getElementById('messages').innerHTML = `
                    <div class="message assistant">
                        <strong>Assistant:</strong> Chat cleared! How can I help you?
                    </div>`;
                conversationId = null;
                totalCost = 0;
                messageCount = 0;
                document.getElementById('msgCount').textContent = '0';
                document.getElementById('totalCost').textContent = '0.000000';
            }
        </script>
    </body>
    </html>
    """, "text/html"));

app.Run();

// Request/Response models
record ChatRequest
{
    public required string Message { get; init; }
    public string? Model { get; init; }
    public string? ConversationId { get; init; }
    public string? SystemPrompt { get; init; }
}
record ChatResponse
{
    public required string ConversationId { get; init; }
    public required string Message { get; init; }
    public required string Model { get; init; }
    public int? TokensUsed { get; init; }
    public decimal? EstimatedCost { get; init; }
    public int MessageCount { get; init; }
}
