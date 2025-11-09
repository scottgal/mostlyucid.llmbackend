# Publishing Guide

This guide explains how to publish new versions of Mostlyucid.LlmBackend to NuGet.

## Prerequisites

### 1. Convert Icon to PNG

Before publishing, convert the SVG icon to PNG:

```bash
# Using ImageMagick (recommended)
convert -background none -size 128x128 icon.svg icon.png

# Or using Inkscape
inkscape icon.svg --export-type=png --export-filename=icon.png --export-width=128 --export-height=128
```

See [ICON-README.md](ICON-README.md) for more options.

### 2. NuGet API Key

1. Go to https://www.nuget.org/
2. Sign in with your account
3. Go to Account Settings → API Keys
4. Click "Create"
5. Configure:
   - **Key Name**: `Mostlyucid.LlmBackend Publishing`
   - **Scopes**: Push, Push new packages and package versions
   - **Glob Pattern**: `Mostlyucid.LlmBackend*`
   - **Expiration**: 1 year (recommended)
6. Copy the generated key

### 3. GitHub Secrets

Add the NuGet API key to GitHub:

1. Go to your repository on GitHub
2. Settings → Secrets and variables → Actions
3. Click "New repository secret"
4. Name: `NUGET_API_KEY`
5. Value: Paste your NuGet API key
6. Click "Add secret"

## Publishing Process

### Option 1: Tag-Based Release (Recommended)

This automatically publishes when you create a version tag:

```bash
# Ensure you're on the main branch
git checkout main
git pull origin main

# Ensure icon.png exists
ls -la icon.png

# Create and push a version tag
git tag -a v2.1.0 -m "Release v2.1.0: LlamaCpp integration"
git push origin v2.1.0
```

The GitHub Action will:
1. Build the project
2. Run tests
3. Create NuGet package
4. Publish to NuGet.org
5. Create GitHub Release with artifacts

### Option 2: Manual Trigger

You can manually trigger the workflow from GitHub:

1. Go to Actions → Publish to NuGet
2. Click "Run workflow"
3. Enter the version (e.g., `2.1.0`)
4. Click "Run workflow"

### Option 3: Local Publishing

To publish manually from your machine:

```bash
# Ensure icon.png exists
ls -la icon.png

# Clean previous builds
dotnet clean
rm -rf bin obj artifacts

# Build and pack
dotnet restore
dotnet build --configuration Release
dotnet pack --configuration Release --output ./artifacts

# Test the package locally (optional)
dotnet nuget push ./artifacts/Mostlyucid.LlmBackend.2.1.0.nupkg --source ~/local-nuget-feed

# Publish to NuGet
dotnet nuget push ./artifacts/*.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## Version Numbering

We follow [Semantic Versioning](https://semver.org/):

- **Major** (X.0.0): Breaking changes
- **Minor** (x.X.0): New features, backward compatible
- **Patch** (x.x.X): Bug fixes, backward compatible

### Version 2.1.0 (Current)

- **Type**: Minor release
- **Reason**: New LlamaCpp backend (new feature, no breaking changes)

### Next Versions

- **2.1.1**: Bug fixes to LlamaCpp or other components
- **2.2.0**: Next new feature (streaming support, new provider, etc.)
- **3.0.0**: Breaking changes (API changes, configuration changes, etc.)

## Pre-Release Checklist

Before publishing, ensure:

- [ ] Version number updated in `.csproj`
- [ ] `RELEASE-NOTES.md` updated with changes
- [ ] `README.md` updated with new features
- [ ] `icon.png` exists (converted from `icon.svg`)
- [ ] All tests pass: `dotnet test`
- [ ] Build succeeds: `dotnet build --configuration Release`
- [ ] Pack succeeds: `dotnet pack --configuration Release`
- [ ] Documentation is up to date
- [ ] Examples are tested and working
- [ ] No sensitive data in code or config

## Post-Release Tasks

After publishing:

1. **Verify on NuGet.org**
   - Check https://www.nuget.org/packages/Mostlyucid.LlmBackend/
   - Ensure version is visible
   - Verify package contents
   - Check icon displays correctly
   - Review README on NuGet page

2. **Test Installation**
   ```bash
   # Create test project
   mkdir test-install
   cd test-install
   dotnet new console
   dotnet add package Mostlyucid.LlmBackend --version 2.1.0
   dotnet build
   ```

3. **Update Documentation**
   - Update main repository README if needed
   - Add release notes to GitHub Releases
   - Update any external documentation

4. **Announce Release**
   - GitHub Discussions
   - Social media if applicable
   - Email to contributors

## Troubleshooting

### Package Push Fails

**Error**: "Package already exists"
```bash
# Use --skip-duplicate flag
dotnet nuget push ./artifacts/*.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### Icon Not Displaying

1. Ensure `icon.png` exists in root directory
2. Check `.csproj` has: `<PackageIcon>icon.png</PackageIcon>`
3. Check `.csproj` has: `<None Include="icon.png" Pack="true" PackagePath="\" />`
4. Verify PNG is valid: `file icon.png`

### Release Notes Not Loading

1. Ensure `RELEASE-NOTES.md` exists
2. Check path in `.csproj` is correct
3. Try using inline release notes if file reading fails

### Symbol Package Fails

If symbol package (`.snupkg`) fails:

```xml
<!-- In .csproj, disable symbols temporarily -->
<IncludeSymbols>false</IncludeSymbols>
```

## Package Contents

The NuGet package includes:

- **DLLs**: Compiled library binaries
- **XML**: Documentation comments
- **README.md**: Package documentation
- **UNLICENSE**: License file
- **icon.png**: Package icon
- **Examples**: Configuration examples in `content/examples/`
- **Symbols** (`.snupkg`): Debugging symbols

## Testing Before Release

### Local NuGet Feed

Create a local feed to test:

```bash
# Create local feed directory
mkdir ~/local-nuget-feed

# Pack and push to local feed
dotnet pack --configuration Release --output ./artifacts
cp ./artifacts/*.nupkg ~/local-nuget-feed/

# Test in another project
dotnet new console -n TestProject
cd TestProject
dotnet nuget add source ~/local-nuget-feed --name LocalTest
dotnet add package Mostlyucid.LlmBackend --version 2.1.0
dotnet restore
```

### Validate Package

```bash
# Extract and inspect package
unzip -l ./artifacts/Mostlyucid.LlmBackend.2.1.0.nupkg

# Check package metadata
dotnet nuget verify ./artifacts/Mostlyucid.LlmBackend.2.1.0.nupkg
```

## GitHub Actions Status

Monitor the build:

1. Go to repository → Actions tab
2. Find the running workflow
3. Click to see detailed logs
4. Check for errors in each step

## Support

If you encounter issues:

1. Check [GitHub Actions logs](https://github.com/scottgal/mostlyucid.llmbackend/actions)
2. Review [NuGet package status](https://www.nuget.org/packages/Mostlyucid.LlmBackend/)
3. Open an issue with details

## References

- [NuGet Package Documentation](https://docs.microsoft.com/en-us/nuget/)
- [GitHub Actions](https://docs.github.com/en/actions)
- [Semantic Versioning](https://semver.org/)
- [.NET CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/)
