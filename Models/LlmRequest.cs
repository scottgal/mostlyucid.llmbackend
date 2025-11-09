namespace Mostlyucid.LlmBackend.Models;

/// <summary>
/// Request to an LLM backend
/// </summary>
public class LlmRequest
{
    /// <summary>
    /// The prompt to complete
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// System message to guide the model's behavior
    /// </summary>
    public string? SystemMessage { get; set; }

    /// <summary>
    /// Temperature for randomness (0.0 - 2.0)
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Top-p sampling
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Frequency penalty
    /// </summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Presence penalty
    /// </summary>
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Stop sequences
    /// </summary>
    public List<string>? StopSequences { get; set; }

    /// <summary>
    /// Whether to stream the response
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// Preferred backend name (optional)
    /// </summary>
    public string? PreferredBackend { get; set; }
}

/// <summary>
/// Response from an LLM backend
/// </summary>
public class LlmResponse
{
    /// <summary>
    /// The generated text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if unsuccessful
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Backend that processed the request
    /// </summary>
    public string? Backend { get; set; }

    /// <summary>
    /// Model used
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Total tokens used
    /// </summary>
    public int? TotalTokens { get; set; }

    /// <summary>
    /// Prompt tokens
    /// </summary>
    public int? PromptTokens { get; set; }

    /// <summary>
    /// Completion tokens
    /// </summary>
    public int? CompletionTokens { get; set; }

    /// <summary>
    /// Time taken in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Finish reason
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// Exception if any
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Chat message
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Role (system, user, assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional name of the sender
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Chat completion request
/// </summary>
public class ChatRequest : LlmRequest
{
    /// <summary>
    /// Conversation history
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();
}
