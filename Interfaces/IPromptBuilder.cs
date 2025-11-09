using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Interface for building prompts with context
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Build a prompt from a user message with optional context
    /// </summary>
    string BuildPrompt(string userMessage, PromptContext? context = null);

    /// <summary>
    /// Build a chat request with context and memory
    /// </summary>
    ChatRequest BuildChatRequest(string userMessage, PromptContext? context = null);

    /// <summary>
    /// Add a message to the builder's memory
    /// </summary>
    void AddToMemory(string role, string content);

    /// <summary>
    /// Clear the builder's memory
    /// </summary>
    void ClearMemory();

    /// <summary>
    /// Get current memory
    /// </summary>
    List<ChatMessage> GetMemory();
}

/// <summary>
/// Context for prompt building
/// </summary>
public class PromptContext
{
    /// <summary>
    /// System message to set behavior
    /// </summary>
    public string? SystemMessage { get; set; }

    /// <summary>
    /// Additional context variables
    /// </summary>
    public Dictionary<string, string> ContextVariables { get; set; } = new();

    /// <summary>
    /// Few-shot examples
    /// </summary>
    public List<(string Input, string Output)> Examples { get; set; } = new();

    /// <summary>
    /// Maximum number of previous messages to include
    /// </summary>
    public int MaxMemoryMessages { get; set; } = 10;

    /// <summary>
    /// Whether to include conversation history
    /// </summary>
    public bool IncludeHistory { get; set; } = true;

    /// <summary>
    /// Template variables for dynamic replacement
    /// </summary>
    public Dictionary<string, string> TemplateVariables { get; set; } = new();
}
