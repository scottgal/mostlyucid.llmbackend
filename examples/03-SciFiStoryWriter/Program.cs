using System.Collections.Concurrent;
using System.Text;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

/*
 * SciFi Story Writer with Simultaneous Multi-LLM Collaboration
 *
 * Demonstrates:
 * - Simultaneous backend strategy (NEW!) - get multiple creative variations
 * - Page-by-page story building with user selection
 * - Cumulative context - each page builds on previous choices
 * - Final markdown story generation
 * - Comparing outputs from different LLMs side-by-side
 *
 * How it works:
 * 1. Generate story beats - get 3 different versions from 3 LLMs
 * 2. User selects their favorite version
 * 3. Generate first page - using selected beats, get 3 versions
 * 4. User selects best page
 * 5. Repeat - each new page sees previous selections
 * 6. Export final story as beautiful markdown
 *
 * This showcases how different models excel at different creative tasks!
 */

var builder = WebApplication.CreateBuilder(args);

// Configure Simultaneous strategy with diverse creative models
builder.Services.AddLlmBackend(settings =>
{
    // IMPORTANT: Simultaneous strategy calls ALL backends in parallel!
    settings.SelectionStrategy = BackendSelectionStrategy.Simultaneous;

    settings.Backends = new List<LlmBackendConfig>
    {
        // Claude 3.5 Sonnet - Best for nuanced, literary fiction
        new()
        {
            Name = "claude-writer",
            Type = LlmBackendType.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "your-key-here",
            ModelName = "claude-3-5-sonnet-20241022",
            Temperature = 0.9, // High creativity
            MaxInputTokens = 200000,
            MaxOutputTokens = 4096,
            CostPerMillionInputTokens = 3.00m,
            CostPerMillionOutputTokens = 15.00m,
            MaxSpendUsd = 5.00m,
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        },

        // GPT-4o - Great for plot structure and pacing
        new()
        {
            Name = "gpt4o-plotter",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "your-key-here",
            ModelName = "gpt-4o",
            Temperature = 0.9,
            MaxInputTokens = 128000,
            CostPerMillionInputTokens = 2.50m,
            CostPerMillionOutputTokens = 10.00m,
            MaxSpendUsd = 5.00m,
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        },

        // Llama 3 - Free local model for comparison
        new()
        {
            Name = "llama3-creative",
            Type = LlmBackendType.Ollama,
            BaseUrl = "http://localhost:11434",
            ModelName = "llama3",
            Temperature = 0.9,
            Enabled = true
        }
    };

    settings.Telemetry = new TelemetryConfig
    {
        EnableMetrics = true,
        LogTokenCounts = true,
        EnableCostTracking = true
    };
});

var app = builder.Build();

// In-memory story storage
var stories = new ConcurrentDictionary<string, Story>();

// Generate story beats/outline with multiple LLM perspectives
app.MapPost("/beats", async (BeatsRequest request, ILlmService llm) =>
{
    var storyId = Guid.NewGuid().ToString();

    var prompt = $"""
        You are a creative sci-fi story writer. Generate 3-5 story beats (key plot points)
        for a short story based on these parameters:

        Genre: {request.Genre}
        Themes: {string.Join(", ", request.Themes)}
        Setting: {request.Setting}
        Tone: {request.Tone}

        Return ONLY the beats as a numbered list. Be creative and engaging!
        Each beat should be 1-2 sentences.

        Example format:
        1. [First major event/setup]
        2. [Rising action/complication]
        3. [Climax/turning point]
        4. [Resolution/consequence]
        """;

    var llmRequest = new LlmRequest
    {
        Prompt = prompt,
        Temperature = 0.9,
        MaxTokens = 1000
    };

    var response = await llm.CompleteAsync(llmRequest);

    if (!response.Success)
    {
        return Results.Problem("Failed to generate story beats");
    }

    // With Simultaneous strategy, we get multiple responses!
    var allBeats = new List<BeatVariation>
    {
        new()
        {
            Backend = response.Backend ?? "unknown",
            Model = response.Model ?? "unknown",
            Beats = response.Content ?? "",
            TokensUsed = response.TotalTokens ?? 0,
            DurationMs = response.DurationMs
        }
    };

    // Add alternative responses
    if (response.AlternativeResponses != null)
    {
        foreach (var alt in response.AlternativeResponses.Where(r => r.Success))
        {
            allBeats.Add(new BeatVariation
            {
                Backend = alt.Backend ?? "unknown",
                Model = alt.Model ?? "unknown",
                Beats = alt.Content ?? "",
                TokensUsed = alt.TotalTokens ?? 0,
                DurationMs = alt.DurationMs
            });
        }
    }

    // Create new story
    var story = new Story
    {
        Id = storyId,
        Genre = request.Genre,
        Themes = request.Themes,
        Setting = request.Setting,
        Tone = request.Tone,
        BeatVariations = allBeats,
        CreatedAt = DateTime.UtcNow
    };

    stories[storyId] = story;

    return Results.Ok(new
    {
        storyId,
        variations = allBeats,
        message = $"Generated {allBeats.Count} different story beat variations! Select your favorite to continue."
    });
});

// Select beats and generate first page
app.MapPost("/page/{storyId}", async (string storyId, PageRequest request, ILlmService llm) =>
{
    if (!stories.TryGetValue(storyId, out var story))
    {
        return Results.NotFound("Story not found");
    }

    // Build context from previous selections
    var context = new StringBuilder();
    if (!string.IsNullOrEmpty(story.SelectedBeats))
    {
        context.AppendLine("Story Beats:");
        context.AppendLine(story.SelectedBeats);
        context.AppendLine();
    }

    if (story.Pages.Any())
    {
        context.AppendLine("Story so far:");
        foreach (var page in story.Pages)
        {
            context.AppendLine($"\n--- Page {page.PageNumber} ---");
            context.AppendLine(page.SelectedText);
        }
        context.AppendLine();
    }

    var pageNumber = story.Pages.Count + 1;
    var prompt = $"""
        You are writing a sci-fi story. Here's the context:

        {context}

        Now write page {pageNumber} of the story.

        Requirements:
        - Continue naturally from previous pages
        - Write 300-400 words
        - End with a hook for the next page
        - Match the tone: {story.Tone}
        - Setting: {story.Setting}

        Write ONLY the story text for this page. No meta-commentary.
        """;

    var llmRequest = new LlmRequest
    {
        Prompt = prompt,
        Temperature = 0.9,
        MaxTokens = 600
    };

    var response = await llm.CompleteAsync(llmRequest);

    if (!response.Success)
    {
        return Results.Problem("Failed to generate page");
    }

    // Collect all variations
    var variations = new List<PageVariation>
    {
        new()
        {
            Backend = response.Backend ?? "unknown",
            Model = response.Model ?? "unknown",
            Text = response.Content ?? "",
            TokensUsed = response.TotalTokens ?? 0,
            DurationMs = response.DurationMs
        }
    };

    if (response.AlternativeResponses != null)
    {
        foreach (var alt in response.AlternativeResponses.Where(r => r.Success))
        {
            variations.Add(new PageVariation
            {
                Backend = alt.Backend ?? "unknown",
                Model = alt.Model ?? "unknown",
                Text = alt.Content ?? "",
                TokensUsed = alt.TotalTokens ?? 0,
                DurationMs = alt.DurationMs
            });
        }
    }

    return Results.Ok(new
    {
        pageNumber,
        variations,
        totalPages = story.Pages.Count,
        message = $"Page {pageNumber} generated with {variations.Count} variations! Select your favorite."
    });
});

// Select a page variation and add to story
app.MapPost("/select/{storyId}/page", (string storyId, SelectionRequest request) =>
{
    if (!stories.TryGetValue(storyId, out var story))
    {
        return Results.NotFound("Story not found");
    }

    var page = new StoryPage
    {
        PageNumber = story.Pages.Count + 1,
        SelectedText = request.SelectedText,
        SelectedBackend = request.Backend,
        SelectedModel = request.Model
    };

    story.Pages.Add(page);
    story.UpdatedAt = DateTime.UtcNow;

    return Results.Ok(new
    {
        success = true,
        pageNumber = page.PageNumber,
        totalPages = story.Pages.Count,
        message = $"Page {page.PageNumber} added! Story now has {story.Pages.Count} page(s)."
    });
});

// Select story beats
app.MapPost("/select/{storyId}/beats", (string storyId, SelectionRequest request) =>
{
    if (!stories.TryGetValue(storyId, out var story))
    {
        return Results.NotFound("Story not found");
    }

    story.SelectedBeats = request.SelectedText;
    story.UpdatedAt = DateTime.UtcNow;

    return Results.Ok(new
    {
        success = true,
        message = "Story beats selected! Now start writing pages."
    });
});

// Export final story as markdown
app.MapGet("/export/{storyId}", (string storyId) =>
{
    if (!stories.TryGetValue(storyId, out var story))
    {
        return Results.NotFound("Story not found");
    }

    var markdown = new StringBuilder();

    // Title and metadata
    markdown.AppendLine("# Sci-Fi Story");
    markdown.AppendLine();
    markdown.AppendLine($"**Genre:** {story.Genre}  ");
    markdown.AppendLine($"**Themes:** {string.Join(", ", story.Themes)}  ");
    markdown.AppendLine($"**Setting:** {story.Setting}  ");
    markdown.AppendLine($"**Tone:** {story.Tone}  ");
    markdown.AppendLine();
    markdown.AppendLine("---");
    markdown.AppendLine();

    // Story beats
    if (!string.IsNullOrEmpty(story.SelectedBeats))
    {
        markdown.AppendLine("## Story Beats");
        markdown.AppendLine();
        markdown.AppendLine(story.SelectedBeats);
        markdown.AppendLine();
        markdown.AppendLine("---");
        markdown.AppendLine();
    }

    // Story content
    markdown.AppendLine("## The Story");
    markdown.AppendLine();

    foreach (var page in story.Pages.OrderBy(p => p.PageNumber))
    {
        markdown.AppendLine(page.SelectedText);
        markdown.AppendLine();
        markdown.AppendLine("---");
        markdown.AppendLine();
    }

    // Metadata footer
    markdown.AppendLine("## Story Metadata");
    markdown.AppendLine();
    markdown.AppendLine($"- **Total Pages:** {story.Pages.Count}");
    markdown.AppendLine($"- **Created:** {story.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
    markdown.AppendLine($"- **Last Updated:** {story.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
    markdown.AppendLine();
    markdown.AppendLine("### Models Used");
    var modelsUsed = story.Pages
        .GroupBy(p => $"{p.SelectedBackend} ({p.SelectedModel})")
        .Select(g => $"- {g.Key}: {g.Count()} page(s)")
        .ToList();
    foreach (var model in modelsUsed)
    {
        markdown.AppendLine(model);
    }

    return Results.Text(markdown.ToString(), "text/markdown");
});

// Get story status
app.MapGet("/story/{storyId}", (string storyId) =>
{
    if (!stories.TryGetValue(storyId, out var story))
    {
        return Results.NotFound("Story not found");
    }

    return Results.Ok(new
    {
        story.Id,
        story.Genre,
        story.Themes,
        story.Setting,
        story.Tone,
        hasBeats = !string.IsNullOrEmpty(story.SelectedBeats),
        pageCount = story.Pages.Count,
        pages = story.Pages.Select(p => new
        {
            p.PageNumber,
            preview = p.SelectedText.Length > 100
                ? p.SelectedText.Substring(0, 100) + "..."
                : p.SelectedText,
            p.SelectedBackend,
            p.SelectedModel
        }),
        story.CreatedAt,
        story.UpdatedAt
    });
});

// List all stories
app.MapGet("/stories", () =>
{
    var allStories = stories.Values
        .OrderByDescending(s => s.UpdatedAt)
        .Select(s => new
        {
            s.Id,
            s.Genre,
            s.Setting,
            pageCount = s.Pages.Count,
            s.CreatedAt,
            s.UpdatedAt
        });

    return Results.Ok(allStories);
});

// Welcome page
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Collaborative Sci-Fi Story Writer</title>
        <style>
            body {
                font-family: 'Courier New', monospace;
                background: #0a0e27;
                color: #00ff00;
                max-width: 1200px;
                margin: 20px auto;
                padding: 20px;
            }
            .terminal {
                background: #000;
                border: 2px solid #00ff00;
                border-radius: 5px;
                padding: 20px;
                margin: 20px 0;
            }
            h1, h2 {
                color: #00ffff;
                text-shadow: 0 0 10px #00ffff;
            }
            .variation {
                background: #1a1a2e;
                border: 1px solid #00ff00;
                padding: 15px;
                margin: 10px 0;
                border-radius: 5px;
            }
            .step {
                background: #16213e;
                padding: 15px;
                margin: 15px 0;
                border-left: 4px solid #00ffff;
            }
            code {
                background: #2a2a3e;
                padding: 2px 6px;
                border-radius: 3px;
                color: #00ffff;
            }
            .highlight {
                color: #ffff00;
                font-weight: bold;
            }
            pre {
                background: #1a1a1a;
                padding: 15px;
                border-radius: 5px;
                overflow-x: auto;
                border: 1px solid #00ff00;
            }
        </style>
    </head>
    <body>
        <div class="terminal">
            <h1>🚀 Collaborative Sci-Fi Story Writer</h1>
            <p class="highlight">Powered by Simultaneous Multi-LLM Strategy (NEW!)</p>
        </div>

        <h2>✨ What Makes This Special?</h2>
        <div class="terminal">
            <p><strong class="highlight">Simultaneous Strategy:</strong> Get 3 different creative versions from 3 different LLMs in parallel!</p>
            <ul>
                <li>Claude 3.5 Sonnet - Best for nuanced, literary fiction</li>
                <li>GPT-4o - Great for plot structure and pacing</li>
                <li>Llama 3 - Free local alternative</li>
            </ul>
            <p>Each model brings its own creative style. You pick the best parts from each!</p>
        </div>

        <h2>📖 How It Works</h2>

        <div class="step">
            <strong>Step 1: Generate Story Beats</strong>
            <p>Get 3 different outline variations. Select your favorite.</p>
            <pre>POST /beats
{
  "genre": "Cyberpunk",
  "themes": ["AI consciousness", "corporate dystopia"],
  "setting": "Neo-Tokyo 2157",
  "tone": "Dark and gritty"
}</pre>
        </div>

        <div class="step">
            <strong>Step 2: Write Pages (Iteratively)</strong>
            <p>For each page, get 3 versions. Pick your favorite. The next page builds on your choice!</p>
            <pre>POST /page/{storyId}

# Select the version you like best
POST /select/{storyId}/page
{
  "selectedText": "... the text you chose ...",
  "backend": "claude-writer",
  "model": "claude-3-5-sonnet-20241022"
}</pre>
        </div>

        <div class="step">
            <strong>Step 3: Repeat & Build</strong>
            <p>Each new page sees all previous selections. The story grows organically!</p>
        </div>

        <div class="step">
            <strong>Step 4: Export as Markdown</strong>
            <p>Get your complete story as beautiful markdown.</p>
            <pre>GET /export/{storyId}</pre>
        </div>

        <h2>🎯 Example Workflow</h2>
        <div class="terminal">
            <pre>
# 1. Generate beats
curl -X POST http://localhost:5000/beats \
  -H "Content-Type: application/json" \
  -d '{
    "genre": "Hard Sci-Fi",
    "themes": ["First contact", "quantum physics"],
    "setting": "Deep space research station",
    "tone": "Mysterious and scientific"
  }'

# Returns 3 beat variations - pick one
# Response: { "storyId": "abc-123", "variations": [...] }

# 2. Select your favorite beats
curl -X POST http://localhost:5000/select/abc-123/beats \
  -H "Content-Type: application/json" \
  -d '{
    "selectedText": "1. Scientists detect strange signal...",
    "backend": "gpt4o-plotter",
    "model": "gpt-4o"
  }'

# 3. Generate first page (gets 3 versions)
curl -X POST http://localhost:5000/page/abc-123

# 4. Select best version for page 1
curl -X POST http://localhost:5000/select/abc-123/page \
  -H "Content-Type: application/json" \
  -d '{
    "selectedText": "Dr. Sarah Chen stared at the readout...",
    "backend": "claude-writer",
    "model": "claude-3-5-sonnet-20241022"
  }'

# 5. Repeat steps 3-4 for each page

# 6. Export final story
curl http://localhost:5000/export/abc-123 > my-story.md
            </pre>
        </div>

        <h2>🔥 Why This is Awesome</h2>
        <div class="terminal">
            <ul>
                <li><strong>Creative Diversity:</strong> Different models = different styles!</li>
                <li><strong>Cherry-pick Best Ideas:</strong> Take Claude's prose, GPT-4o's plot</li>
                <li><strong>Cumulative Context:</strong> Each page builds on your choices</li>
                <li><strong>Cost Conscious:</strong> $5 daily budgets prevent overspend</li>
                <li><strong>Local Fallback:</strong> Llama 3 is free (if you have Ollama)</li>
                <li><strong>Markdown Export:</strong> Beautiful final stories</li>
            </ul>
        </div>

        <h2>📋 API Endpoints</h2>
        <div class="terminal">
            <ul>
                <li><code>POST /beats</code> - Generate story beats (3 variations)</li>
                <li><code>POST /select/{id}/beats</code> - Select favorite beats</li>
                <li><code>POST /page/{id}</code> - Generate next page (3 variations)</li>
                <li><code>POST /select/{id}/page</code> - Select favorite page version</li>
                <li><code>GET /story/{id}</code> - Get story status and content</li>
                <li><code>GET /export/{id}</code> - Export complete story as markdown</li>
                <li><code>GET /stories</code> - List all stories</li>
            </ul>
        </div>

        <h2>🚀 Setup</h2>
        <div class="terminal">
            <pre>
# Set API keys
export ANTHROPIC_API_KEY=sk-ant-...
export OPENAI_API_KEY=sk-...

# Start Ollama (optional, for free local model)
ollama pull llama3
ollama serve

# Run the app
cd examples/03-SciFiStoryWriter
dotnet run

# Start writing!
# Visit http://localhost:5000 for this guide
            </pre>
        </div>

        <p style="text-align: center; margin-top: 40px; color: #888;">
            <small>Powered by mostlylucid.llmbackend v2.0 - Simultaneous Strategy</small>
        </p>
    </body>
    </html>
    """, "text/html"));

app.Run();

// Models
record BeatsRequest(
    string Genre,
    List<string> Themes,
    string Setting,
    string Tone
);

record PageRequest(
    string Instruction
);

record SelectionRequest(
    string SelectedText,
    string Backend,
    string Model
);

class Story
{
    public string Id { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<string> Themes { get; set; } = new();
    public string Setting { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public List<BeatVariation> BeatVariations { get; set; } = new();
    public string? SelectedBeats { get; set; }
    public List<StoryPage> Pages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

class BeatVariation
{
    public string Backend { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Beats { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public long DurationMs { get; set; }
}

class PageVariation
{
    public string Backend { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public long DurationMs { get; set; }
}

class StoryPage
{
    public int PageNumber { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public string SelectedBackend { get; set; } = string.Empty;
    public string SelectedModel { get; set; } = string.Empty;
}
