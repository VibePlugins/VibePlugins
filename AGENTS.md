# AGENTS.md — Skill Reference

This document describes the agent skills available under `.agents/skills/`. Each skill provides domain-specific knowledge and guidelines that agents can reference when working on this project.

> **Maintenance rule:** Update this file whenever a skill is added, removed, or significantly modified under `.agents/skills/`. When adding new plugins or packages, also update the "Available Plugins" table in `README.md`.

---

## Skills

### Harmony

**Path:** [`.agents/skills/harmony/SKILL.md`](.agents/skills/harmony/SKILL.md)

A comprehensive guide to [Harmony](https://harmony.pardeike.net/), the .NET runtime patching library used to intercept and modify game methods at the IL level.

**What it covers:**

- **Patch types** — Prefix, Postfix, Transpiler, Finalizer, and Reverse Patch
- **Targeting methods** — annotation-based and dynamic method targeting, conditional patching
- **Priority & ordering** — controlling execution order when multiple patches target the same method
- **IL manipulation** — common OpCodes, `CodeInstruction` helpers, practical transpiler examples
- **Manual patching API** — for runtime or dynamic patch decisions
- **Utility classes** — `AccessTools`, `Traverse`, `FileLog`
- **Common patterns** — guard patches, return-value modification, try/catch wrapping, timing/profiling
- **Edge cases & pitfalls** — generics, structs, inlined methods, static constructors, thread safety
- **Debugging** — patch state queries and runtime inspection

**When to use:** Reference this skill when writing, reviewing, or debugging Harmony patches in any plugin.

---

### RocketMod

**Path:** [`.agents/skills/rocket-mod/SKILL.md`](.agents/skills/rocket-mod/SKILL.md)

A comprehensive guide to developing [RocketMod](https://github.com/RocketMod) plugins for the [Unturned](https://store.steampowered.com/app/304930/Unturned/) dedicated server.

**What it covers:**

- **Architecture & project setup** — plugin layering, SDK-style `.csproj` configuration
- **Plugin structure** — main plugin class, configuration, nested configuration
- **Commands** — `IRocketCommand` implementation, console-compatible commands
- **Translations** — message localization system
- **Event handling** — RocketMod events (`U.Events`), native Unturned SDK events
- **Common Unturned APIs** — player operations, chat/messaging, items/assets, barricades/structures, terrain/world
- **Data persistence** — JSON file storage, MySQL database integration
- **Cross-plugin integration** — soft and hard dependencies
- **Harmony patching** — using Harmony within RocketMod plugins
- **Unity MonoBehaviour** — lifecycle methods, threading considerations
- **Project organization** — recommended directory structure, design guidelines
- **Common patterns** — cooldowns, VIP/tiered permissions, chat formatting

**Testing framework (`VibePlugins.RocketMod.TestBase`):**

- Writing tests with the base class, test lifecycle, and container configuration
- Command testing via a fluent builder API with result assertions
- Event testing with monitoring patterns and negative assertions
- Mock entities (players, zombies, animals)
- Remote code execution (`RunOnServerAsync`), tick/frame waiting
- Distributed testing with parallel server workers
- CI/CD integration (GitHub Actions), troubleshooting guide

**When to use:** Reference this skill when creating, modifying, testing, or reviewing any RocketMod plugin in this workspace.

---

### Documentation

**Path:** [`.agents/skills/documentation/SKILL.md`](.agents/skills/documentation/SKILL.md)

Guidelines for creating and maintaining project documentation, including the README and detailed feature guides in the `docs/` directory.

**What it covers:**

- **Writing style** -- concise, professional, code-first, no emojis or promotional language
- **File organization** -- README.md structure, docs/ page naming, self-contained pages
- **Document structure template** -- standard outline for each docs/ page
- **Code snippets** -- fenced blocks, runnable examples, length guidelines
- **Keeping docs up-to-date** -- when and how to update docs alongside code changes
- **API reference tables** -- method tables, builder method tables, event type tables
- **Quality checklist** -- verification steps before committing documentation changes

**When to use:** Reference this skill when writing, updating, or reviewing any documentation file in this repository.

---

## Adding a New Skill

1. Create a new directory under `.agents/skills/<SkillName>/`.
2. Add a `SKILL.md` file with the skill's reference content.
3. Update this `AGENTS.md` file with a new section following the format above.
4. If the new skill adds a plugin or package, update the "Available Plugins" table in `README.md`.
