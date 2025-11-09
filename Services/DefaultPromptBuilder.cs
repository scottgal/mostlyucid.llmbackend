using System.Text;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Default implementation of prompt builder
/// </summary>
public class DefaultPromptBuilder : IPromptBuilder
{
    private readonly IContextMemory _memory;

    public DefaultPromptBuilder(IContextMemory? memory = null)
    {
        _memory = memory ?? new InMemoryContextMemory();
    }

    public string BuildPrompt(string userMessage, PromptContext? context = null)
    {
        var sb = new StringBuilder();

        // Add system message if provided
        if (!string.IsNullOrEmpty(context?.SystemMessage))
        {
            sb.AppendLine($"System: {context.SystemMessage}");
            sb.AppendLine();
        }

        // Add few-shot examples
        if (context?.Examples?.Count > 0)
        {
            sb.AppendLine("Examples:");
            foreach (var example in context.Examples)
            {
                sb.AppendLine($"Input: {example.Input}");
                sb.AppendLine($"Output: {example.Output}");
                sb.AppendLine();
            }
        }

        // Add context variables
        if (context?.ContextVariables?.Count > 0)
        {
            sb.AppendLine("Context:");
            foreach (var kvp in context.ContextVariables)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        // Add conversation history
        if (context?.IncludeHistory == true)
        {
            var messages = _memory.GetMessages();
            var maxMessages = context.MaxMemoryMessages;

            if (messages.Count > 0)
            {
                var relevantMessages = messages.TakeLast(maxMessages);
                foreach (var msg in relevantMessages)
                {
                    sb.AppendLine($"{msg.Role}: {msg.Content}");
                }
                sb.AppendLine();
            }
        }

        // Add user message
        sb.AppendLine($"User: {userMessage}");

        var prompt = sb.ToString();

        // Replace template variables
        if (context?.TemplateVariables?.Count > 0)
        {
            foreach (var kvp in context.TemplateVariables)
            {
                prompt = prompt.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            }
        }

        return prompt;
    }

    public ChatRequest BuildChatRequest(string userMessage, PromptContext? context = null)
    {
        var request = new ChatRequest
        {
            Prompt = userMessage,
            Messages = new List<ChatMessage>()
        };

        // Add system message
        if (!string.IsNullOrEmpty(context?.SystemMessage))
        {
            request.Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = context.SystemMessage
            });
        }

        // Add examples as alternating user/assistant messages
        if (context?.Examples?.Count > 0)
        {
            foreach (var example in context.Examples)
            {
                request.Messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = example.Input
                });
                request.Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = example.Output
                });
            }
        }

        // Add conversation history
        if (context?.IncludeHistory == true)
        {
            var messages = _memory.GetMessages();
            var maxMessages = context.MaxMemoryMessages;
            var relevantMessages = messages.TakeLast(maxMessages);
            request.Messages.AddRange(relevantMessages);
        }

        // Add current user message
        request.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        return request;
    }

    public void AddToMemory(string role, string content)
    {
        _memory.AddMessage(role, content);
    }

    public void ClearMemory()
    {
        _memory.Clear();
    }

    public List<ChatMessage> GetMemory()
    {
        return _memory.GetMessages();
    }
}

/// <summary>
/// Specialized prompt builder for translation tasks
/// </summary>
public class TranslationPromptBuilder : IPromptBuilder
{
    private readonly IContextMemory _memory;

    public TranslationPromptBuilder(IContextMemory? memory = null)
    {
        _memory = memory ?? new InMemoryContextMemory();
    }

    public string BuildPrompt(string userMessage, PromptContext? context = null)
    {
        var sb = new StringBuilder();

        var sourceLang = context?.ContextVariables.GetValueOrDefault("SourceLanguage", "auto");
        var targetLang = context?.ContextVariables.GetValueOrDefault("TargetLanguage", "English");

        sb.AppendLine($"Translate the following text from {sourceLang} to {targetLang}.");
        sb.AppendLine("Preserve all formatting, placeholders (like {0}, {{variable}}, %s, etc.), and special characters.");
        sb.AppendLine();

        if (context?.ContextVariables?.ContainsKey("Context") == true)
        {
            sb.AppendLine($"Context: {context.ContextVariables["Context"]}");
            sb.AppendLine();
        }

        sb.AppendLine("Text to translate:");
        sb.AppendLine(userMessage);
        sb.AppendLine();
        sb.AppendLine("Provide only the translation without any explanations.");

        return sb.ToString();
    }

    public ChatRequest BuildChatRequest(string userMessage, PromptContext? context = null)
    {
        var prompt = BuildPrompt(userMessage, context);

        return new ChatRequest
        {
            Prompt = userMessage,
            Messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            }
        };
    }

    public void AddToMemory(string role, string content)
    {
        _memory.AddMessage(role, content);
    }

    public void ClearMemory()
    {
        _memory.Clear();
    }

    public List<ChatMessage> GetMemory()
    {
        return _memory.GetMessages();
    }
}
