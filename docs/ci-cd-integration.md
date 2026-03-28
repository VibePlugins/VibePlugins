---
uid: ci-cd-integration
---

# CI/CD Integration

Run integration tests in CI pipelines with conditional container support.

## Environment Variable

Control container availability with `VIBEPLUGINS_ENABLE_CONTAINERS`:

| Value | Behavior |
|---|---|
| `true` or `1` | Force containers on |
| `false` or `0` | Force containers off -- container-dependent tests are skipped |
| Not set | Auto-detect via `docker info` |

## Test Attributes

### RequiresContainer

Mark tests that need a running Docker container:

```csharp
[RequiresContainer]
[Fact]
public async Task NeedsServer()
{
    // This test is skipped when containers are unavailable
}
```

### SkipContainer

Mark tests that can run without a container:

```csharp
[SkipContainer]
[Fact]
public void UnitTest()
{
    // This test always runs, even without Docker
}
```

Both attributes can be applied at the class or method level.

## Running Without Containers

When Docker is not available, use a filter to run only non-container tests:

```bash
dotnet test --filter "Category!=RequiresContainer"
```

## GitHub Actions Workflow

Here is a workflow that builds, tests (with or without containers), and publishes NuGet packages on version tags. Adapted from this project's CI pipeline:

```yaml
name: CI

on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:
    branches: [main]

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  SOLUTION: MyPlugin.sln

jobs:
  build-and-test:
    runs-on: windows-latest  # Required for net48 projects
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Build.props') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION }} -c Release --no-restore -warnaserror

      - name: Check container support
        id: container-check
        shell: bash
        run: |
          if docker info > /dev/null 2>&1; then
            echo "available=true" >> "$GITHUB_OUTPUT"
          else
            echo "available=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Build container image
        if: steps.container-check.outputs.available == 'true'
        run: dotnet run --project path/to/Tools -- build-image --tag vibeplugins:rocketmod

      - name: Test (with containers)
        if: steps.container-check.outputs.available == 'true'
        run: dotnet test ${{ env.SOLUTION }} -c Release --no-build --logger "trx;LogFileName=test-results.trx"
        env:
          VIBEPLUGINS_ENABLE_CONTAINERS: 'true'

      - name: Test (without containers)
        if: steps.container-check.outputs.available == 'false'
        run: dotnet test ${{ env.SOLUTION }} -c Release --no-build --filter "Category!=RequiresContainer" --logger "trx;LogFileName=test-results.trx"
        env:
          VIBEPLUGINS_ENABLE_CONTAINERS: 'false'

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/test-results.trx'
          if-no-files-found: warn
```

## Key Considerations

- **Windows runners** are required for `net48` projects. Use `runs-on: windows-latest`.
- **Docker availability** varies by CI provider and runner type. The container-check step handles this gracefully.
- **NuGet caching** speeds up subsequent builds. Cache on `*.csproj` and `Directory.Build.props` hashes.
- **Test results** in TRX format can be uploaded as artifacts for post-run analysis.
- **Tag-based publishing**: use `if: startsWith(github.ref, 'refs/tags/v')` to trigger NuGet pack/push only on version tags.
