using System.Collections.Concurrent;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

/*
 * Sci-Fi Story Writer's Tool
 *
 * Demonstrates:
 * - Structured creative writing prompts
 * - Long-form content generation with context windows
 * - Round-robin load balancing across models for variety
 * - Character and plot consistency management
 * - Temperature and creativity controls
 *
 * Features:
 * - Generate story outlines, characters, scenes
 * - Track story elements for consistency
 * - Use multiple models for creative variety
 * - Budget-aware creative writing
 */

var builder = WebApplication.CreateBuilder(args);

// Configure backends optimized for creative writing
builder.Services.AddLlmBackend(settings =>
{
    // Round-robin for variety in creative output
    settings.SelectionStrategy = BackendSelectionStrategy.RoundRobin;

    settings.Backends = new List<LlmBackendConfig>
    {
        // Claude 3.5 Sonnet - Best for creative writing, excellent prose
        new()
        {
            Name = "claude-writer",
            Type = LlmBackendType.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? "your-api-key-here",
            ModelName = "claude-3-5-sonnet-20241022",
            Temperature = 0.9, // High creativity
            MaxInputTokens = 200000, // Large context window
            MaxOutputTokens = 4096,

            CostPerMillionInputTokens = 3.00m,
            CostPerMillionOutputTokens = 15.00m,
            MaxSpendUsd = 5.00m, // $5 daily budget for creative work
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        },

        // GPT-4o - Great at sci-fi concepts and world-building
        new()
        {
            Name = "gpt4o-creator",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? "your-api-key-here",
            ModelName = "gpt-4o",
            Temperature = 0.9,
            MaxInputTokens = 128000,
            MaxOutputTokens = 4096,

            CostPerMillionInputTokens = 5.00m,
            CostPerMillionOutputTokens = 15.00m,
            MaxSpendUsd = 5.00m,
            SpendResetPeriod = SpendResetPeriod.Daily,
            Enabled = true
        },

        // Local Llama 3 - Free, good for brainstorming
        new()
        {
            Name = "llama3-brainstorm",
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
        LogTokenCounts = true
    };
});

// Story storage
var stories = new ConcurrentDictionary<string, Story>();

var app = builder.Build();

// Generate story outline
app.MapPost("/outline", async (OutlineRequest request, ILlmService llm) =>
{
    var prompt = $"""
        You are a creative sci-fi story writer. Generate a detailed story outline based on these parameters:

        Genre: {request.Genre ?? "Hard Science Fiction"}
        Themes: {string.Join(", ", request.Themes ?? new[] { "AI", "Space exploration" })}
        Setting: {request.Setting ?? "Near-future solar system"}
        Tone: {request.Tone ?? "Thoughtful and optimistic"}

        Create a story outline with:
        1. Title
        2. Logline (1-2 sentences)
        3. Three-act structure with major plot points
        4. Character archetypes needed
        5. Key sci-fi concepts to explore
        6. Potential twists or complications

        Be creative and original. Focus on hard sci-fi concepts with plausible science.
        """;

    var response = await llm.CompleteAsync(new LlmRequest
    {
        Prompt = prompt,
        Temperature = 0.9,
        MaxTokens = 2000
    });

    if (!response.Success)
    {
        return Results.Problem(response.ErrorMessage);
    }

    var storyId = Guid.NewGuid().ToString();
    stories[storyId] = new Story
    {
        Id = storyId,
        Outline = response.Content ?? "",
        CreatedWith = response.BackendUsed ?? "unknown",
        Metadata = request
    };

    return Results.Ok(new
    {
        storyId,
        outline = response.Content,
        model = response.BackendUsed,
        tokens = response.TotalTokens
    });
});

// Generate character profile
app.MapPost("/character", async (CharacterRequest request, ILlmService llm) =>
{
    var prompt = $"""
        Create a detailed character profile for a sci-fi story with these parameters:

        Role: {request.Role}
        Background: {request.Background}
        Motivation: {request.Motivation}

        Include:
        1. Name and physical description
        2. Personality traits and quirks
        3. Background and expertise
        4. Goals and motivations
        5. Internal conflicts
        6. Relationships with other characters
        7. Character arc potential

        Make them three-dimensional, flawed, and interesting. Avoid clichés.
        """;

    var response = await llm.CompleteAsync(new LlmRequest
    {
        Prompt = prompt,
        Temperature = 0.85,
        MaxTokens = 1500
    });

    if (!response.Success)
    {
        return Results.Problem(response.ErrorMessage);
    }

    return Results.Ok(new
    {
        character = response.Content,
        model = response.BackendUsed,
        tokens = response.TotalTokens
    });
});

// Write a scene
app.MapPost("/scene", async (SceneRequest request, ILlmService llm) =>
{
    var contextInfo = "";
    if (!string.IsNullOrEmpty(request.StoryId) && stories.TryGetValue(request.StoryId, out var story))
    {
        contextInfo = $"""

            Story Context:
            {story.Outline}

            Previous Scenes:
            {string.Join("\n\n---\n\n", story.Scenes.TakeLast(2))}
            """;
    }

    var prompt = $"""
        Write a compelling sci-fi scene with the following parameters:

        Scene Description: {request.Description}
        Point of View: {request.PointOfView ?? "Third person limited"}
        Mood: {request.Mood ?? "Tense"}
        Length: {request.WordCount ?? 500} words
        {contextInfo}

        Requirements:
        - Show, don't tell
        - Use vivid sensory details
        - Include believable dialogue if appropriate
        - Maintain internal consistency with the story world
        - Build tension and conflict
        - End with a hook or transition

        Write the scene now:
        """;

    var response = await llm.CompleteAsync(new LlmRequest
    {
        Prompt = prompt,
        Temperature = 0.9,
        MaxTokens = (request.WordCount ?? 500) * 2 // Rough token estimate
    });

    if (!response.Success)
    {
        return Results.Problem(response.ErrorMessage);
    }

    // Add scene to story if ID provided
    if (!string.IsNullOrEmpty(request.StoryId) && stories.TryGetValue(request.StoryId, out var storyToUpdate))
    {
        storyToUpdate.Scenes.Add(response.Content ?? "");
    }

    // Calculate cost
    decimal? cost = null;
    if (response.PromptTokens.HasValue && response.CompletionTokens.HasValue)
    {
        var backend = response.BackendUsed ?? "";
        if (backend.Contains("claude"))
        {
            cost = (response.PromptTokens.Value * 3.00m / 1_000_000m) +
                  (response.CompletionTokens.Value * 15.00m / 1_000_000m);
        }
        else if (backend.Contains("gpt4o"))
        {
            cost = (response.PromptTokens.Value * 5.00m / 1_000_000m) +
                  (response.CompletionTokens.Value * 15.00m / 1_000_000m);
        }
    }

    return Results.Ok(new
    {
        scene = response.Content,
        model = response.BackendUsed,
        tokens = response.TotalTokens,
        cost
    });
});

// Get story
app.MapGet("/stories/{id}", (string id) =>
{
    if (stories.TryGetValue(id, out var story))
    {
        return Results.Ok(story);
    }
    return Results.NotFound();
});

// Creative prompts library
app.MapGet("/prompts", () =>
{
    var prompts = new
    {
        themes = new[] {
            "First Contact",
            "Post-Scarcity Society",
            "Artificial Intelligence Rights",
            "Climate Engineering",
            "Quantum Computing",
            "Generation Ships",
            "Memory Upload",
            "Time Dilation",
            "Dyson Sphere",
            "Fermi Paradox"
        },
        settings = new[] {
            "Mars Colony (2150)",
            "Europa Research Station",
            "Ringworld Megastructure",
            "Asteroid Mining Belt",
            "Post-Singularity Earth",
            "Interstellar Ark Ship",
            "Virtual Reality Collective",
            "Lunar Manufacturing Hub"
        },
        conflicts = new[] {
            "Human vs. AI Ethics",
            "Resource Scarcity in Space",
            "Cultural Clash (Earth vs. Colonists)",
            "Corporate Exploitation",
            "Scientific Discovery with Consequences",
            "Rebellion Against Control",
            "First Contact Misunderstanding"
        }
    };
    return Results.Ok(prompts);
});

// Welcome page
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Sci-Fi Story Writer</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { font-family: 'Courier New', monospace; background: #0a0a0a; color: #00ff00; }
            .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
            header { border: 2px solid #00ff00; padding: 20px; margin-bottom: 20px; }
            h1 { font-size: 32px; text-shadow: 0 0 10px #00ff00; }
            .section { border: 1px solid #00ff00; padding: 20px; margin: 20px 0; background: #0f0f0f; }
            button { background: #00ff00; color: #0a0a0a; border: none; padding: 10px 20px; font-family: 'Courier New', monospace; cursor: pointer; font-weight: bold; }
            button:hover { background: #00cc00; box-shadow: 0 0 10px #00ff00; }
            textarea { width: 100%; background: #0a0a0a; color: #00ff00; border: 1px solid #00ff00; padding: 10px; font-family: 'Courier New', monospace; resize: vertical; }
            input, select { background: #0a0a0a; color: #00ff00; border: 1px solid #00ff00; padding: 8px; font-family: 'Courier New', monospace; }
            .output { white-space: pre-wrap; line-height: 1.6; }
            .label { color: #00ffff; margin-top: 10px; margin-bottom: 5px; }
            .cost { color: #ffff00; }
        </style>
    </head>
    <body>
        <div class="container">
            <header>
                <h1>⚡ SCI-FI STORY WRITER ⚡</h1>
                <p>AI-Powered Creative Writing Tool</p>
                <p style="margin-top: 10px;">Models: Claude 3.5 Sonnet | GPT-4o | Llama 3 (Local)</p>
            </header>

            <div class="section">
                <h2>📖 Generate Story Outline</h2>
                <div class="label">Genre:</div>
                <input type="text" id="genre" placeholder="Hard Science Fiction" style="width: 100%;">
                <div class="label">Themes (comma-separated):</div>
                <input type="text" id="themes" placeholder="AI consciousness, space exploration, ethics" style="width: 100%;">
                <div class="label">Setting:</div>
                <input type="text" id="setting" placeholder="Near-future Mars colony" style="width: 100%;">
                <div class="label">Tone:</div>
                <input type="text" id="tone" placeholder="Thoughtful and optimistic" style="width: 100%;">
                <br><br>
                <button onclick="generateOutline()">Generate Outline</button>
                <div class="label" style="margin-top: 20px;">Output:</div>
                <div id="outline-output" class="output"></div>
            </div>

            <div class="section">
                <h2>👤 Create Character</h2>
                <div class="label">Role:</div>
                <input type="text" id="char-role" placeholder="Brilliant but isolated xenobiologist" style="width: 100%;">
                <div class="label">Background:</div>
                <input type="text" id="char-background" placeholder="Grew up on Europa station" style="width: 100%;">
                <div class="label">Motivation:</div>
                <input type="text" id="char-motivation" placeholder="Prove life exists beyond Earth" style="width: 100%;">
                <br><br>
                <button onclick="generateCharacter()">Create Character</button>
                <div class="label" style="margin-top: 20px;">Output:</div>
                <div id="character-output" class="output"></div>
            </div>

            <div class="section">
                <h2>✍️ Write Scene</h2>
                <div class="label">Scene Description:</div>
                <textarea id="scene-desc" rows="3" placeholder="The crew discovers an alien artifact that defies physics..."></textarea>
                <div class="label">Point of View:</div>
                <input type="text" id="scene-pov" placeholder="Third person limited" style="width: 100%;">
                <div class="label">Mood:</div>
                <input type="text" id="scene-mood" placeholder="Tense, mysterious" style="width: 100%;">
                <div class="label">Word Count:</div>
                <input type="number" id="scene-words" value="500" style="width: 200px;">
                <br><br>
                <button onclick="generateScene()">Write Scene</button>
                <div class="label" style="margin-top: 20px;">Output:</div>
                <div id="scene-output" class="output"></div>
                <div id="scene-cost" class="cost"></div>
            </div>

            <div class="section">
                <h2>📚 Quick Prompts</h2>
                <button onclick="loadPrompts()">Load Creative Prompts</button>
                <div id="prompts-output" class="output" style="margin-top: 10px;"></div>
            </div>
        </div>

        <script>
            async function generateOutline() {
                const output = document.getElementById('outline-output');
                output.textContent = 'Generating...';

                try {
                    const response = await fetch('/outline', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            genre: document.getElementById('genre').value || "Hard Science Fiction",
                            themes: (document.getElementById('themes').value || "AI, space").split(',').map(t => t.trim()),
                            setting: document.getElementById('setting').value || "Near-future",
                            tone: document.getElementById('tone').value || "Thoughtful"
                        })
                    });

                    const data = await response.json();
                    output.textContent = `[Generated by ${data.model}]\n\n${data.outline}`;
                } catch (error) {
                    output.textContent = `Error: ${error.message}`;
                }
            }

            async function generateCharacter() {
                const output = document.getElementById('character-output');
                output.textContent = 'Generating...';

                try {
                    const response = await fetch('/character', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            role: document.getElementById('char-role').value || "Scientist",
                            background: document.getElementById('char-background').value || "Unknown",
                            motivation: document.getElementById('char-motivation').value || "Discovery"
                        })
                    });

                    const data = await response.json();
                    output.textContent = `[Generated by ${data.model}]\n\n${data.character}`;
                } catch (error) {
                    output.textContent = `Error: ${error.message}`;
                }
            }

            async function generateScene() {
                const output = document.getElementById('scene-output');
                const costDiv = document.getElementById('scene-cost');
                output.textContent = 'Writing...';
                costDiv.textContent = '';

                try {
                    const response = await fetch('/scene', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            description: document.getElementById('scene-desc').value || "A discovery",
                            pointOfView: document.getElementById('scene-pov').value,
                            mood: document.getElementById('scene-mood').value,
                            wordCount: parseInt(document.getElementById('scene-words').value) || 500
                        })
                    });

                    const data = await response.json();
                    output.textContent = `[Generated by ${data.model}]\n\n${data.scene}`;

                    if (data.cost) {
                        costDiv.textContent = `Cost: $${data.cost.toFixed(6)} | Tokens: ${data.tokens}`;
                    } else {
                        costDiv.textContent = `FREE | Tokens: ${data.tokens || 'N/A'}`;
                    }
                } catch (error) {
                    output.textContent = `Error: ${error.message}`;
                }
            }

            async function loadPrompts() {
                const output = document.getElementById('prompts-output');

                try {
                    const response = await fetch('/prompts');
                    const data = await response.json();

                    output.innerHTML = `
                        <strong>THEMES:</strong><br>${data.themes.join(', ')}<br><br>
                        <strong>SETTINGS:</strong><br>${data.settings.join(', ')}<br><br>
                        <strong>CONFLICTS:</strong><br>${data.conflicts.join(', ')}
                    `;
                } catch (error) {
                    output.textContent = `Error: ${error.message}`;
                }
            }
        </script>
    </body>
    </html>
    """, "text/html"));

app.Run();

// Models
record OutlineRequest
{
    public string? Genre { get; init; }
    public string[]? Themes { get; init; }
    public string? Setting { get; init; }
    public string? Tone { get; init; }
}
record CharacterRequest
{
    public required string Role { get; init; }
    public required string Background { get; init; }
    public required string Motivation { get; init; }
}
record SceneRequest
{
    public required string Description { get; init; }
    public string? PointOfView { get; init; }
    public string? Mood { get; init; }
    public int? WordCount { get; init; }
    public string? StoryId { get; init; }
}
class Story
{
    public required string Id { get; init; }
    public required string Outline { get; init; }
    public List<string> Scenes { get; init; } = new();
    public required string CreatedWith { get; init; }
    public required OutlineRequest Metadata { get; init; }
}
