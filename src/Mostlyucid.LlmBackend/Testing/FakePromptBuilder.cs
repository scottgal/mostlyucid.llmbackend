using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Testing;

/// <summary>
/// Fake prompt builder for testing purposes
/// </summary>
/// <remarks>
/// <para>
/// This test double implements IPromptBuilder and allows you to control prompt generation
/// and verify that your code correctly uses prompt builders.
/// </para>
///
/// <para><strong>Example Usage:</strong></para>
/// <code>
/// // Create fake prompt builder
/// var fake = new FakePromptBuilder
/// {
///     PromptTemplate = "Task: {userMessage}\nContext: {context}"
/// };
///
/// // Build a prompt
/// var prompt = fake.BuildPrompt("Translate this", new PromptContext
/// {
///     ContextVariables = new Dictionary&lt;string, string&gt;
///     {
///         ["context"] = "French to English"
///     }
/// });
///
/// // Verify
/// Assert.Contains("Translate this", prompt);
/// Assert.Contains("French to English", prompt);
/// Assert.Equal(1, fake.BuildPromptCallCount);
/// </code>
/// </remarks>
public class FakePromptBuilder : IPromptBuilder
{
    private int _buildPromptCallCount;
    private int _buildChatRequestCallCount;
    private int _addToMemoryCallCount;
    private int _clearMemoryCallCount;

    private readonly List<ChatMessage> _memory = new();

    /// <summary>
    /// Create a new fake prompt builder
    /// </summary>
    public FakePromptBuilder()
    {
        PromptTemplate = "User: {userMessage}";
    }

    #region Configuration Properties

    /// <summary>
    /// Template for generated prompts
    /// </summary>
    /// <remarks>
    /// Supports these template variables:
    /// - {userMessage}: The user's message
    /// - {systemMessage}: System instructions
    /// - {context}: Context from ContextVariables
    /// - Any keys from PromptContext.ContextVariables
    /// - Any keys from PromptContext.TemplateVariables
    /// </remarks>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Whether to include conversation history in prompts
    /// </summary>
    public bool IncludeHistory { get; set; } = true;

    /// <summary>
    /// Maximum number of historical messages to include
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 10;

    #endregion

    #region Call History

    /// <summary>
    /// Number of times BuildPrompt was called
    /// </summary>
    public int BuildPromptCallCount => _buildPromptCallCount;

    /// <summary>
    /// Number of times BuildChatRequest was called
    /// </summary>
    public int BuildChatRequestCallCount => _buildChatRequestCallCount;

    /// <summary>
    /// Number of times AddToMemory was called
    /// </summary>
    public int AddToMemoryCallCount => _addToMemoryCallCount;

    /// <summary>
    /// Number of times ClearMemory was called
    /// </summary>
    public int ClearMemoryCallCount => _clearMemoryCallCount;

    /// <summary>
    /// Last user message passed to BuildPrompt
    /// </summary>
    public string? LastUserMessage { get; private set; }

    /// <summary>
    /// Last context passed to BuildPrompt
    /// </summary>
    public PromptContext? LastContext { get; private set; }

    /// <summary>
    /// Reset all call counters and history
    /// </summary>
    public void Reset()
    {
        _buildPromptCallCount = 0;
        _buildChatRequestCallCount = 0;
        _addToMemoryCallCount = 0;
        _clearMemoryCallCount = 0;
        LastUserMessage = null;
        LastContext = null;
        _memory.Clear();
    }

    #endregion

    #region IPromptBuilder Implementation

    public string BuildPrompt(string userMessage, PromptContext? context = null)
    {
        Interlocked.Increment(ref _buildPromptCallCount);
        LastUserMessage = userMessage;
        LastContext = context;

        var prompt = PromptTemplate
            .Replace("{userMessage}", userMessage)
            .Replace("{systemMessage}", context?.SystemMessage ?? "");

        // Replace context variables
        if (context?.ContextVariables != null)
        {
            foreach (var (key, value) in context.ContextVariables)
            {
                prompt = prompt.Replace($"{{{key}}}", value);
            }
        }

        // Replace template variables
        if (context?.TemplateVariables != null)
        {
            foreach (var (key, value) in context.TemplateVariables)
            {
                prompt = prompt.Replace($"{{{key}}}", value);
            }
        }

        // Include history if enabled
        if (IncludeHistory && _memory.Any())
        {
            var historyCount = Math.Min(_memory.Count, MaxHistoryMessages);
            var history = string.Join("\n", _memory.TakeLast(historyCount)
                .Select(m => $"{m.Role}: {m.Content}"));
            prompt = $"{history}\n{prompt}";
        }

        return prompt;
    }

    public ChatRequest BuildChatRequest(string userMessage, PromptContext? context = null)
    {
        Interlocked.Increment(ref _buildChatRequestCallCount);
        LastUserMessage = userMessage;
        LastContext = context;

        var messages = new List<ChatMessage>();

        // Add system message if provided
        if (!string.IsNullOrEmpty(context?.SystemMessage))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = context.SystemMessage
            });
        }

        // Add conversation history if enabled
        if (IncludeHistory && _memory.Any())
        {
            var historyCount = Math.Min(_memory.Count,
                context?.MaxMemoryMessages ?? MaxHistoryMessages);
            messages.AddRange(_memory.TakeLast(historyCount));
        }

        // Add user message
        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        return new ChatRequest
        {
            Messages = messages,
            Temperature = context?.Temperature,
            MaxTokens = context?.MaxTokens
        };
    }

    public void AddToMemory(string role, string content)
    {
        Interlocked.Increment(ref _addToMemoryCallCount);
        _memory.Add(new ChatMessage
        {
            Role = role,
            Content = content
        });
    }

    public void ClearMemory()
    {
        Interlocked.Increment(ref _clearMemoryCallCount);
        _memory.Clear();
    }

    public List<ChatMessage> GetMemory()
    {
        return new List<ChatMessage>(_memory);
    }

    #endregion
}
