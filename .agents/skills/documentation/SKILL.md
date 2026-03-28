# Documentation Skill

Guidelines for creating and maintaining documentation in the VibeVault project.

---

## Writing Style

- No emojis.
- Concise and professional. State facts, skip filler.
- Lead with code, explain after. Readers want to see how something works before reading about it.
- Use tables for API reference (methods, options, parameters). Use short paragraphs for concepts.
- Do not use promotional language ("powerful", "blazing fast", "game-changing"). Describe what things do.

## File Organization

| Location | Purpose |
|---|---|
| `README.md` | Project overview, one quick example, feature list, links to docs |
| `docs/<topic>.md` | One file per feature/topic, kebab-case naming |
| `docs/troubleshooting.md` | Common errors and diagnostics |

Each docs page is self-contained. A reader can open any page and understand the topic without reading other pages first. Cross-links are provided but not required.

## Document Structure

Every `docs/` page should follow this structure:

```
# Title

One to three sentences explaining what this feature does.

## Basic Usage

(Code snippet showing the simplest usage)

## API Reference

(Tables listing methods, parameters, defaults, return types)

## Examples

(Complete, runnable test methods -- prefer examples from examples/ExamplePlugin.Tests/)

## Tips (optional)

(Short bullet points for common patterns or gotchas)
```

Keep sections in this order. Omit "Tips" if there is nothing non-obvious to say.

## Code Snippets

- Always use fenced code blocks with a language identifier: `csharp`, `xml`, `yaml`, `bash`.
- Prefer complete, runnable examples over fragments. A reader should be able to copy a test method into their project and run it.
- Pull examples from `examples/ExamplePlugin.Tests/` when possible. This keeps docs verifiable against real code.
- Keep inline snippets under 30 lines. For longer examples, show the key part inline and reference the full source file.

## README Structure

The README is a gateway, not a comprehensive reference. It should contain:

1. One-line project description
2. A single complete code example (a test method)
3. Available plugins table
4. Feature bullet list (one line per feature, no explanations)
5. Getting started steps (numbered, minimal)
6. Links to docs/ pages
7. Project structure tree (abbreviated)
8. License

Target length: 120-160 lines.

## Keeping Documentation Up-to-Date

| Trigger | Action |
|---|---|
| New API method added | Update the relevant docs/ page reference table |
| New feature added | Create a new docs/ page if it warrants one, or add a section to an existing page |
| Behavior change | Update affected examples and descriptions |
| New NuGet package | Add to the package table in README |
| Feature removed | Remove from docs and README feature list |
| New plugin or package added | Add to the "Available Plugins" table in README.md |

When modifying code, check whether the change affects any docs page. If it does, update the docs in the same commit.

## API Reference Tables

Use this format for method reference tables:

```markdown
| Method | Parameters | Default | Description |
|---|---|---|---|
| `WithName(string)` | name | `"TestPlayer"` | Sets the player display name |
```

For event types, list all properties:

```markdown
| Property | Type | Description |
|---|---|---|
| `Text` | `string` | The chat message content |
```

## Quality Checklist

Before committing documentation changes:

- [ ] Follows the document structure template
- [ ] Has at least one complete code example
- [ ] API reference tables are complete and match current source
- [ ] Links from README.md resolve correctly
- [ ] No emojis
- [ ] No promotional language
- [ ] Proofread for conciseness -- remove sentences that do not add information

## Examples

### Good: code-first, concise

```markdown
## Basic Usage

\```csharp
var result = await ExecuteCommand("heal")
    .AsPlayer(caller.HandleId)
    .WithArgs("Wounded", "50")
    .RunAsync();

result.ShouldSucceed()
      .ShouldContainMessage("Healed");
\```

`ExecuteCommand` returns a `CommandTestBuilder`. Configure the caller and arguments,
then call `RunAsync()` to send the command to the server. The returned
`CommandTestResult` supports fluent assertions.
```

### Bad: verbose, no code

```markdown
## Basic Usage

The command testing system provides a comprehensive and flexible way to test
your RocketMod commands. It uses a fluent builder pattern that allows you to
configure various aspects of the command execution, including who is executing
the command, what arguments are being passed, and how long to wait for the
response. The system is designed to be intuitive and easy to use, making it
simple to write thorough tests for all of your plugin's commands.
```

The first version shows the reader how to use the API in 8 lines, then explains
what happened in 3 sentences. The second version uses 6 sentences to say nothing
actionable.
