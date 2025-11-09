# Package Icon

## Converting SVG to PNG

The NuGet package requires a PNG icon. To convert the icon.svg to icon.png, use one of these methods:

### Method 1: Using ImageMagick (Linux/Mac/Windows)

```bash
convert -background none -size 128x128 icon.svg icon.png
```

### Method 2: Using Inkscape (Linux/Mac/Windows)

```bash
inkscape icon.svg --export-type=png --export-filename=icon.png --export-width=128 --export-height=128
```

### Method 3: Using rsvg-convert (Linux/Mac)

```bash
rsvg-convert -w 128 -h 128 icon.svg > icon.png
```

### Method 4: Using Node.js sharp

```bash
npm install sharp sharp-cli
npx sharp -i icon.svg -o icon.png resize 128 128
```

### Method 5: Online Converter

1. Go to https://cloudconvert.com/svg-to-png
2. Upload icon.svg
3. Set dimensions to 128x128
4. Download icon.png

## Icon Requirements

- **Format**: PNG
- **Size**: 128x128 pixels minimum (recommended)
- **Transparency**: Supported and recommended
- **File size**: < 1MB (preferably < 100KB)

## Current Icon Design

The icon represents:
- **Central blue node**: The LlmBackend core/orchestrator
- **Green outer nodes**: Primary backend connections (OpenAI, Anthropic, etc.)
- **Purple diagonal nodes**: Additional/plugin backends
- **Orange data points**: Data flow indicators
- **Connecting lines**: Backend communication paths

This visualizes the library's core purpose: connecting multiple LLM backends through a unified interface.
