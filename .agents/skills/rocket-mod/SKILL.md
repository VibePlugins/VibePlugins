# RocketMod Plugin Development Guide

## Overview

This guide covers developing production-grade **Unturned RocketMod plugins** targeting the **LDM (Legally Distinct Missile)** framework — the actively maintained fork of RocketMod 4 bundled with Unturned by Smartly Dressed Games.

**Unturned** is a free-to-play multiplayer zombie survival game built on Unity (C#). **RocketMod** is a server-side plugin framework that loads as an Unturned Module and provides plugin lifecycle management, commands, permissions, configuration, translations, and an event system on top of the game's native `SDG.Unturned` API.

---

## Architecture

```
[ Your Plugin ]                        <-- RocketPlugin<TConfig>, commands, event handlers
[ Rocket.Core + Rocket.Unturned ]      <-- Plugin framework (commands, config, permissions, events)
[ Unturned Module System ]             <-- IModuleNexus entry point
[ SDG.Unturned / Assembly-CSharp ]     <-- Game API (players, items, vehicles, structures, chat)
[ Unity Engine ]                       <-- MonoBehaviour, GameObject, Vector3, coroutines
```

Plugins are .NET Framework class libraries compiled to DLLs and placed in `Rocket/Plugins/`. RocketMod discovers and loads them automatically.

---

## Project Setup

### SDK-Style .csproj (Required)

Always use the modern dotnet SDK project format. Never use the legacy verbose .csproj format.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <RootNamespace>YourNamespace.PluginName</RootNamespace>
    <AssemblyName>PluginName</AssemblyName>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <!-- Provides Rocket.API, Rocket.Core, Rocket.Unturned, SDG.Unturned, UnityEngine, Steamworks -->
    <PackageReference Include="RestoreMonarchy.RocketRedist" Version="3.24.7.1" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

**Key points:**
- Target `net48` (.NET Framework 4.8) for compatibility with Unturned's runtime.
- Use `RestoreMonarchy.RocketRedist` NuGet package — it bundles all required RocketMod and Unturned assemblies.
- `ExcludeAssets="runtime"` prevents copying framework DLLs into build output (the server already has them).
- Use `LangVersion=latest` to access modern C# features that compile to .NET 4.8.

### Additional Dependencies

```xml
<!-- For Harmony patching (when you need to intercept internal game methods) -->
<PackageReference Include="Lib.Harmony" Version="2.2.2" />

<!-- For JSON persistence -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

<!-- Cross-plugin dependency (compile-time only) -->
<Reference Include="OtherPlugin">
  <HintPath>..\lib\OtherPlugin.dll</HintPath>
  <Private>False</Private>
</Reference>
```

---

## Plugin Structure

### Main Plugin Class

Every plugin has a single entry point class inheriting from `RocketPlugin<TConfiguration>`:

```csharp
using Rocket.Core.Plugins;
using Rocket.Core.Logging;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

namespace YourNamespace.PluginName
{
    public class MyPlugin : RocketPlugin<MyPluginConfiguration>
    {
        public static MyPlugin Instance { get; private set; }

        protected override void Load()
        {
            Instance = this;

            // Subscribe to events
            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;

            Logger.Log($"{Name} v{Assembly.GetName().Version} loaded.");
        }

        protected override void Unload()
        {
            // Always unsubscribe — failure to do so causes memory leaks and duplicate handlers on reload
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            Logger.Log($"{Name} unloaded.");
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            UnturnedChat.Say(player, Translate("welcome", player.DisplayName));
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            // Cleanup player-specific state
        }
    }
}
```

**Critical rules:**
- `Load()` is your initialization. Subscribe to events, initialize services, start coroutines.
- `Unload()` is your teardown. **Always** unsubscribe from every event, destroy components, cancel invocations, dispose timers. Plugins can be reloaded at runtime — failure to clean up causes duplicate handlers and memory leaks.
- The static `Instance` singleton is a necessary pattern because commands and other classes need access to the plugin (for configuration, translations, etc.). Use a property with private setter.
- `RocketPlugin` inherits from `MonoBehaviour`, so you have access to Unity lifecycle methods (`Update()`, `InvokeRepeating()`, `StartCoroutine()`, `gameObject.AddComponent<T>()`).

### Configuration

```csharp
using Rocket.API;

namespace YourNamespace.PluginName
{
    public class MyPluginConfiguration : IRocketPluginConfiguration
    {
        public string WelcomeMessage { get; set; }
        public int MaxItems { get; set; }
        public float CooldownSeconds { get; set; }
        public bool EnableFeatureX { get; set; }

        public void LoadDefaults()
        {
            WelcomeMessage = "Welcome, {0}!";
            MaxItems = 10;
            CooldownSeconds = 30f;
            EnableFeatureX = true;
        }
    }
}
```

**Key points:**
- Implements `IRocketPluginConfiguration`. The `LoadDefaults()` method provides initial values written to XML on first load.
- RocketMod automatically serializes/deserializes this class to `Rocket/Plugins/PluginName.configuration.xml`.
- Access via `Configuration.Instance` on the plugin class (e.g., `MyPlugin.Instance.Configuration.Instance.MaxItems`).
- Use properties (not fields) for clean XML serialization. Fields work too but properties are preferred.
- For complex nested configuration, use classes with `[XmlArrayItem]` and `[XmlAttribute]` attributes for clean XML output.
- Configuration is reloaded when an admin runs `/rocket reload`.

### Nested Configuration Example

```csharp
using System.Collections.Generic;
using System.Xml.Serialization;
using Rocket.API;

namespace YourNamespace.PluginName
{
    public class MyPluginConfiguration : IRocketPluginConfiguration
    {
        public float DefaultCooldown { get; set; }
        public List<KitDefinition> Kits { get; set; }
        public List<VipTier> VipTiers { get; set; }

        public void LoadDefaults()
        {
            DefaultCooldown = 60f;
            Kits = new List<KitDefinition>
            {
                new KitDefinition
                {
                    Name = "starter",
                    Cooldown = 300,
                    Items = new List<KitItem>
                    {
                        new KitItem { Id = 15, Amount = 1 },
                        new KitItem { Id = 13, Amount = 2 }
                    }
                }
            };
            VipTiers = new List<VipTier>
            {
                new VipTier { Permission = "myplugin.vip", Value = 5 },
                new VipTier { Permission = "myplugin.mvp", Value = 10 }
            };
        }
    }

    public class KitDefinition
    {
        public string Name { get; set; }
        public int Cooldown { get; set; }

        [XmlArrayItem("Item")]
        public List<KitItem> Items { get; set; }
    }

    public class KitItem
    {
        [XmlAttribute("id")]
        public ushort Id { get; set; }

        [XmlAttribute("amount")]
        public byte Amount { get; set; }
    }

    public class VipTier
    {
        [XmlAttribute]
        public string Permission { get; set; }

        [XmlAttribute]
        public int Value { get; set; }
    }
}
```

---

## Commands

### IRocketCommand Implementation

Each command is a separate class implementing `IRocketCommand`:

```csharp
using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;

namespace YourNamespace.PluginName.Commands
{
    public class MyCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "mycommand";
        public string Help => "Does something useful.";
        public string Syntax => "<target> [amount]";
        public List<string> Aliases => new List<string> { "mc" };
        public List<string> Permissions => new List<string> { "myplugin.mycommand" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length < 1)
            {
                UnturnedChat.Say(caller, MyPlugin.Instance.Translate("command_usage", Syntax));
                return;
            }

            var player = (UnturnedPlayer)caller;
            var targetName = command[0];
            var target = UnturnedPlayer.FromName(targetName);

            if (target == null)
            {
                UnturnedChat.Say(caller, MyPlugin.Instance.Translate("player_not_found", targetName));
                return;
            }

            int amount = 1;
            if (command.Length >= 2 && !int.TryParse(command[1], out amount))
            {
                UnturnedChat.Say(caller, MyPlugin.Instance.Translate("invalid_amount"));
                return;
            }

            // Execute command logic...
            UnturnedChat.Say(caller, MyPlugin.Instance.Translate("command_success", target.DisplayName, amount));
        }
    }
}
```

**Key points:**
- `AllowedCaller` — `Player`, `Console`, or `Both`. When `Both`, check `caller is ConsolePlayer` before casting to `UnturnedPlayer`.
- `Permissions` — RocketMod checks these automatically before `Execute` runs. Players need the permission or a wildcard parent.
- Commands are auto-discovered from the plugin assembly — no registration needed.
- Always validate argument count and types. Return early with usage message on bad input.
- Use `UnturnedPlayer.FromName()` or `UnturnedPlayer.FromCSteamID()` to resolve player targets.

### Console-Compatible Commands

```csharp
public void Execute(IRocketPlayer caller, string[] command)
{
    UnturnedPlayer targetPlayer;

    if (caller is ConsolePlayer)
    {
        if (command.Length < 1)
        {
            Logger.Log("Usage: /mycommand <player>");
            return;
        }
        targetPlayer = UnturnedPlayer.FromName(command[0]);
    }
    else
    {
        targetPlayer = command.Length >= 1
            ? UnturnedPlayer.FromName(command[0])
            : (UnturnedPlayer)caller;
    }

    if (targetPlayer == null)
    {
        UnturnedChat.Say(caller, MyPlugin.Instance.Translate("player_not_found"));
        return;
    }

    // Logic...
}
```

---

## Translations

```csharp
public override TranslationList DefaultTranslations => new TranslationList
{
    { "welcome", "Welcome to the server, {0}!" },
    { "command_usage", "Usage: /mycommand {0}" },
    { "player_not_found", "Player \"{0}\" not found." },
    { "invalid_amount", "Amount must be a number." },
    { "command_success", "Successfully applied to {0} (x{1})." },
    { "no_permission", "You don't have permission to do that." },
    { "cooldown_active", "Please wait {0} seconds before using this again." }
};
```

**Usage:** `Translate("key", arg0, arg1, ...)` from within the plugin class, or `MyPlugin.Instance.Translate(...)` from commands and other classes.

**Rich text:** Unturned chat supports Unity rich text tags. Use `<color=#hex>`, `<b>`, `<i>`. Some plugins use `[[b]]` / `[[/b]]` in translations and replace `[[` with `<` and `]]` with `>` to avoid XML parsing issues in the translation file.

RocketMod auto-creates `Rocket/Plugins/PluginName.en.translation.xml` from defaults. Server admins can edit this file to customize messages.

---

## Event Handling

### RocketMod Events (via U.Events)

```csharp
// In Load():
U.Events.OnPlayerConnected += OnPlayerConnected;
U.Events.OnPlayerDisconnected += OnPlayerDisconnected;

// In Unload():
U.Events.OnPlayerConnected -= OnPlayerConnected;
U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
```

### Unturned Native Events (SDG.Unturned)

These are the most commonly used game events for plugins:

```csharp
// Player damage (cancellable)
DamageTool.damagePlayerRequested += OnDamagePlayerRequested;

private void OnDamagePlayerRequested(ref DamagePlayerParameters parameters, ref bool shouldAllow)
{
    // Set shouldAllow = false to prevent damage
    // Access parameters.player, parameters.killer, parameters.damage, parameters.cause, etc.
}

// Player death
PlayerLife.onPlayerDied += OnPlayerDied;

private void OnPlayerDied(PlayerLife sender, EDeathCause cause, ELimb limb, CSteamID instigator)
{
    var player = UnturnedPlayer.FromPlayer(sender.player);
    // Handle death...
}

// Chat
ChatManager.onChatted += OnChatted;

private void OnChatted(SteamPlayer player, EChatMode mode, ref Color chatted,
    ref bool isRich, string text, ref bool isVisible)
{
    // Set isVisible = false to hide the message
}

// Barricade/structure damage
BarricadeManager.onDamageBarricadeRequested += OnDamageBarricade;
StructureManager.onDamageStructureRequested += OnDamageStructure;

// Barricade/structure placement
BarricadeManager.onDeployBarricadeRequested += OnDeployBarricade;

// Vehicle events
VehicleManager.onDamageVehicleRequested += OnDamageVehicle;
VehicleManager.onEnterVehicleRequested += OnEnterVehicle;

// Item pickup
ItemManager.onTakeItemRequested += OnTakeItem;

// Server save
SaveManager.onPostSave += OnServerSave;

// Level loaded
Level.onLevelLoaded += OnLevelLoaded;
```

**Critical:** Always unsubscribe from every native event in `Unload()`. Native events are static delegates — they persist across plugin reloads.

### Event Handler Patterns

Many Unturned events use `ref bool shouldAllow` to let handlers cancel actions:

```csharp
private void OnDamageBarricade(CSteamID instigatorSteamID, Transform barricadeTransform,
    ref ushort pendingTotalDamage, ref bool shouldAllow, EDamageOrigin damageOrigin)
{
    // Find the barricade
    if (!BarricadeManager.tryGetRegion(barricadeTransform, out byte x, out byte y, out ushort plant, out BarricadeRegion region))
        return;

    var drop = region.FindBarricadeByRootTransform(barricadeTransform);
    if (drop == null) return;

    var data = drop.GetServersideData();
    ulong ownerSteamId = data.owner;

    // Example: prevent damage to own structures
    if (ownerSteamId == instigatorSteamID.m_SteamID)
    {
        shouldAllow = false;
    }
}
```

---

## Common Unturned API Usage

### Player Operations

```csharp
UnturnedPlayer player = UnturnedPlayer.FromPlayer(nativePlayer);
UnturnedPlayer player = UnturnedPlayer.FromCSteamID(steamId);
UnturnedPlayer player = UnturnedPlayer.FromName("PlayerName");

// Properties
CSteamID steamId = player.CSteamID;
string name = player.DisplayName;
Vector3 position = player.Position;
bool isDead = player.Dead;
bool isAdmin = player.IsAdmin;
bool inVehicle = player.IsInVehicle;
float health = player.Health;
float hunger = player.Hunger;
float thirst = player.Thirst;
uint experience = player.Experience;

// Actions
player.Heal(100);
player.Hunger = 0;
player.Thirst = 0;
player.Experience += 500;
player.GiveItem(itemId, amount);
player.GiveVehicle(vehicleId);
player.Teleport(position, rotation);
player.Kick("reason");
player.Ban("reason", duration);
player.Admin(true);  // Give/remove admin

// Access native Player object for lower-level operations
Player nativePlayer = player.Player;
nativePlayer.life.askDamage(damage, direction, cause, limb, killer, out _);
nativePlayer.teleportToLocation(position, yaw);
nativePlayer.inventory.forceAddItem(item, autoEquip);
```

### Chat & Messaging

```csharp
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

// Simple messages
UnturnedChat.Say(player, "Hello!");
UnturnedChat.Say(player, "Warning!", Color.red);
UnturnedChat.Say("Broadcast to all players!", Color.green);

// Rich text with icon (advanced)
ChatManager.serverSendMessage(
    message,
    Color.white,
    toPlayer: player.SteamPlayer(),
    mode: EChatMode.SAY,
    iconURL: "https://example.com/icon.png",
    useRichTextFormatting: true
);
```

### Items & Assets

```csharp
using SDG.Unturned;

// Find item asset by ID
ItemAsset itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, itemId);
if (itemAsset == null) { /* invalid ID */ }

// Find by GUID (preferred for modern assets)
ItemAsset itemAsset = Assets.find<ItemAsset>(someGuid);

// Find vehicle asset
VehicleAsset vehicleAsset = (VehicleAsset)Assets.find(EAssetType.VEHICLE, vehicleId);

// Give item to player
Item item = new Item(itemId, amount, quality);
player.Player.inventory.forceAddItem(item, autoEquip: false);

// Spawn vehicle at position
VehicleManager.spawnLockedVehicleForPlayerV2(vehicleId, position, rotation, player.Player);
```

### Player Lookups

```csharp
using SDG.Unturned;
using Steamworks;

// All online players
foreach (SteamPlayer sp in Provider.clients)
{
    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
    // ...
}

// Find players in radius
List<Player> nearby = new List<Player>();
PlayerTool.getPlayersInRadius(center, sqrRadius, nearby);

// Get player from Steam ID
SteamPlayer steamPlayer = PlayerTool.getSteamPlayer(steamId);
Player nativePlayer = PlayerTool.getPlayer(new CSteamID(steam64Id));
```

### Barricades & Structures

```csharp
// Find barricade by transform
if (BarricadeManager.tryGetRegion(transform, out byte x, out byte y, out ushort plant, out BarricadeRegion region))
{
    BarricadeDrop drop = region.FindBarricadeByRootTransform(transform);
    BarricadeData data = drop.GetServersideData();
    ulong owner = data.owner;
    ulong group = data.group;
}

// Destroy barricade
BarricadeManager.destroyBarricade(drop, x, y, plant);

// Iterate all barricades
for (byte x = 0; x < Regions.WORLD_SIZE; x++)
{
    for (byte y = 0; y < Regions.WORLD_SIZE; y++)
    {
        BarricadeRegion region = BarricadeManager.regions[x, y];
        foreach (BarricadeDrop drop in region.drops)
        {
            // Process barricade...
        }
    }
}
```

### Terrain & World

```csharp
// Get ground height at position
float groundHeight = LevelGround.getHeight(position);

// Check if position is underground (cave detection)
bool isUnderground = position.y < groundHeight - 5f;

// Current map name
string mapName = Provider.map;

// Server save directory
string saveDir = ServerSavedata.directory;
```

---

## Data Persistence

### JSON File Storage

For most plugins, JSON files are the simplest and most appropriate persistence method:

```csharp
using Newtonsoft.Json;
using System.IO;

namespace YourNamespace.PluginName.Services
{
    public class DataStore<T> where T : class, new()
    {
        private readonly string _filePath;

        public DataStore(string filePath)
        {
            _filePath = filePath;
        }

        public T Load()
        {
            if (!File.Exists(_filePath))
                return new T();

            string json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }

        public void Save(T data)
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}
```

**Usage in plugin:**

```csharp
private DataStore<PluginData> _dataStore;

protected override void Load()
{
    Instance = this;

    string dataPath = Path.Combine(Directory, "data.json");
    _dataStore = new DataStore<PluginData>(dataPath);
    _data = _dataStore.Load();

    // Save on server auto-save
    SaveManager.onPostSave += SaveData;
}

protected override void Unload()
{
    SaveManager.onPostSave -= SaveData;
    SaveData();
}

private void SaveData() => _dataStore.Save(_data);
```

**Storage location:** Use `Directory` property on the plugin (resolves to `Rocket/Plugins/`) or construct a map-specific path using `ServerSavedata.directory` and `Provider.map` for data that should be per-map.

### Database (MySQL)

For plugins requiring shared state across multiple servers or high-volume data, use MySQL with `MySql.Data`:

```xml
<PackageReference Include="MySql.Data" Version="8.0.33" />
```

Use parameterized queries — never interpolate user input into SQL strings.

---

## Cross-Plugin Integration

### Soft Dependencies

Use `IsDependencyLoaded` and `ExecuteDependencyCode` to optionally integrate with other plugins without hard compile-time dependencies:

```csharp
protected override void Load()
{
    Instance = this;

    if (IsDependencyLoaded("Uconomy"))
    {
        Logger.Log("Uconomy detected, economy features enabled.");
    }
}

public void ChargePlayer(UnturnedPlayer player, decimal amount)
{
    ExecuteDependencyCode("Uconomy", (IRocketPlugin plugin) =>
    {
        var uconomy = (Uconomy.Uconomy)plugin;
        uconomy.Database.IncreaseBalance(player.CSteamID.ToString(), -amount);
    });
}
```

### Hard Dependencies

When you need compile-time access to another plugin's types, add a reference with `<Private>False</Private>`:

```xml
<Reference Include="Uconomy">
  <HintPath>..\lib\Uconomy.dll</HintPath>
  <Private>False</Private>
</Reference>
```

Wait for all plugins to finish loading before resolving references:

```csharp
R.Plugins.OnPluginsLoaded += () =>
{
    var otherPlugin = R.Plugins.GetPlugin("OtherPlugin");
};
```

---

## Harmony Patching

> **Full reference:** See `.agents/skills/Harmony/SKILL.md` for the complete Harmony guide covering all patch types, IL transpilers, CodeMatcher, parameter injection, priorities, and utilities.

For intercepting internal game methods not exposed via events, use Harmony. Add `Lib.Harmony` to your `.csproj`:

```xml
<PackageReference Include="Lib.Harmony" Version="2.2.2" />
```

### Plugin Setup Pattern

```csharp
using HarmonyLib;

public class MyPlugin : RocketPlugin<MyPluginConfiguration>
{
    public const string HarmonyId = "com.yourname.pluginname";
    private Harmony _harmony;

    protected override void Load()
    {
        Instance = this;
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll(Assembly); // Discovers all [HarmonyPatch] classes in your assembly
    }

    protected override void Unload()
    {
        _harmony.UnpatchAll(HarmonyId); // Always pass your ID — never call UnpatchAll() without it
        _harmony = null;
    }
}
```

### Prefix — Block or Modify Before Execution

Use a prefix to cancel an action or modify arguments before the original method runs. Return `false` to skip the original.

```csharp
// Prevent players from claiming beds under certain conditions
[HarmonyPatch(typeof(InteractableBed), nameof(InteractableBed.ReceiveClaimRequest))]
static class BedClaimPatch
{
    [HarmonyPrefix]
    static bool Prefix(InteractableBed __instance, in ServerInvocationContext context)
    {
        Player player = context.GetPlayer();
        if (player == null || player.life.isDead)
            return false; // Skip original — don't allow claim

        // Custom logic here...
        return true; // Let original run
    }
}
```

### Prefix — Intercept and Clean Up on Destroy

```csharp
// Track when a barricade is salvaged to clean up plugin state
[HarmonyPatch(typeof(BarricadeManager), nameof(BarricadeManager.salvageBarricade))]
static class SalvageBarricadePatch
{
    [HarmonyPrefix]
    static void Prefix(Transform transform)
    {
        InteractableBed bed = transform.GetComponent<InteractableBed>();
        if (bed != null)
        {
            // Clean up plugin data associated with this bed
        }
    }
}
```

### Postfix — Read or Modify Return Values

```csharp
[HarmonyPatch(typeof(ItemManager), "GetItemAmount")]
static class ItemAmountPatch
{
    [HarmonyPostfix]
    static void Postfix(ref int __result, ushort itemId)
    {
        // Double the drop amount for configured items
        if (MyPlugin.Instance.Configuration.Instance.DoubledItems.Contains(itemId))
            __result *= 2;
    }
}
```

### Manual Patching (Without Annotations)

For dynamic or private targets, use the manual API:

```csharp
protected override void Load()
{
    Instance = this;
    var harmony = new Harmony("com.yourname.plugin");

    // Patch a private method by reflection
    var original = typeof(InteractableSentry).GetMethod("Update",
        BindingFlags.Instance | BindingFlags.NonPublic);
    var prefix = typeof(SentryPatch).GetMethod("Prefix",
        BindingFlags.Static | BindingFlags.NonPublic);
    var postfix = typeof(SentryPatch).GetMethod("Postfix",
        BindingFlags.Static | BindingFlags.NonPublic);

    harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
}
```

### Transpiler — IL-Level Method Rewriting

For surgically modifying logic *inside* a method (not just before/after). Use when prefix/postfix can't achieve the goal.

```csharp
// Replace a config check with custom logic inside UseableGun.fire
[HarmonyPatch(typeof(UseableGun), "fire")]
static class GunFirePatch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            // Find where Has_Durability is loaded and redirect to our check
            if (codes[i].LoadsField(AccessTools.Field(typeof(ItemsConfigData),
                nameof(ItemsConfigData.Has_Durability))))
            {
                // Replace the config lookup with our custom method
                codes[i - 2] = new CodeInstruction(OpCodes.Ldarg_0);
                codes[i - 1] = CodeInstruction.Call(typeof(PlayerCaller), "get_player");
                codes[i] = CodeInstruction.Call(typeof(GunFirePatch), nameof(ShouldUseDurability));
            }
        }
        return codes;
    }

    static bool ShouldUseDurability(Player player)
    {
        // Custom durability logic per-item
        return Provider.modeConfigData.Items.Has_Durability;
    }
}
```

> **See the Harmony skill** for `CodeMatcher` usage, which is the preferred pattern-matching approach for complex transpilers.

### Accessing Private Fields

Use triple-underscore parameters to read/write private fields without reflection:

```csharp
[HarmonyPatch(typeof(InteractableSentry), "Update")]
static class SentryPatch
{
    [HarmonyPrefix]
    static void Prefix(InteractableSentry __instance, ref bool ___hasWeapon)
    {
        ___hasWeapon = true; // Force sentry to always think it has a weapon
    }
}
```

### Guidelines for Harmony in RocketMod Plugins

- **Prefer native events first.** Use `DamageTool.damagePlayerRequested`, `BarricadeManager.onDamageBarricadeRequested`, etc. Only use Harmony when no event/delegate exists.
- **Always unpatch in `Unload()`.** Pass your specific Harmony ID to `UnpatchAll()`.
- **Use `PatchAll(Assembly)`** for annotation-based discovery — this is the standard pattern.
- **One Harmony ID per plugin.** Use reverse-domain notation: `com.yourname.pluginname`.
- **Log patch failures.** Transpiler targets can shift between game updates. Add warnings when patterns aren't found.
- **Avoid patching hot paths** (like `Update()`) with heavy logic — it runs every frame.

### Real-World Examples

The following cloned plugins in `C:\Users\esozb\source\repos\external\` demonstrate Harmony usage:

| Plugin | Harmony Usage | Key Files |
|---|---|---|
| **MoreHomes** | Prefix patches on `BarricadeManager` and `InteractableBed` to intercept bed claims/destruction | `Patches/BarricadeManagerPatches.cs`, `Patches/InteractableBedPatches.cs` |
| **InfiniteSentry** | Manual patching of private `InteractableSentry.Update()` with prefix/postfix | `InfiniteSentry.cs` |
| **SItemModifier** | Transpiler patches on `UseableGun.fire` and `UseableMelee.fire` for durability override | `Patches.Durability.cs` |
| **BetterSpawns** | Replaces `PlayerLife.ServerRespawn` behavior via prefix | `RespawnHandler/ServerRespawnPatch.cs` |
| **UnturnedProfiler** | Dynamic/programmatic patching — wraps arbitrary methods with `Stopwatch` timing at runtime | `Patches/HarmonyProfiling.cs`, `ProfilerPlugin.cs` |
| **Dummy** | All major techniques: `[HarmonyPrefix]`, `[HarmonyTranspiler]` (IL rewrite), `[HarmonyReversePatch]`, and manual `CreateProcessor()` | `Patches/Patch_Provider.cs`, `Patches/Patch_InteractableVehicle.cs` |

---

## Unity MonoBehaviour Integration

Since `RocketPlugin` extends `MonoBehaviour`, you can use Unity lifecycle methods and features:

```csharp
// Periodic tasks
protected override void Load()
{
    Instance = this;
    InvokeRepeating(nameof(PeriodicTask), 60f, 60f);
}

protected override void Unload()
{
    CancelInvoke(nameof(PeriodicTask));
}

private void PeriodicTask()
{
    // Runs every 60 seconds
}

// Custom MonoBehaviour components
protected override void Load()
{
    Instance = this;
    var component = gameObject.AddComponent<MyCustomComponent>();
}

protected override void Unload()
{
    var component = gameObject.GetComponent<MyCustomComponent>();
    if (component != null)
        Destroy(component);
}
```

### Threading

Unturned's game logic runs on the main Unity thread. If you need to run something on the main thread from a callback:

```csharp
using SDG.Unturned;

TaskDispatcher.QueueOnMainThread(() =>
{
    // This runs on the main thread next frame
    player.Player.teleportToLocation(position, yaw);
});
```

---

## Project Organization

### Recommended Directory Structure

```
PluginName/
  PluginName/
    Commands/
      CommandFoo.cs
      CommandBar.cs
    Models/
      PlayerData.cs
    Services/
      DataStore.cs
      CooldownManager.cs
    MyPlugin.cs
    MyPluginConfiguration.cs
  PluginName.sln
```

### Design Guidelines

1. **One class per file.** Name the file after the class.
2. **Commands in a `Commands/` folder.** One command per file.
3. **Configuration as a top-level file** next to the plugin class.
4. **Models in `Models/`** for data classes (persistence, DTOs).
5. **Services in `Services/`** for reusable logic (data stores, managers, helpers).
6. **Keep the plugin class thin.** It should wire things together in `Load()`/`Unload()` and hold minimal logic. Delegate to services.
7. **No god classes.** If a service is doing too many things, split it.
8. **Use meaningful namespaces** that reflect the folder structure: `YourOrg.PluginName.Commands`, `YourOrg.PluginName.Services`, etc.

### What NOT to Do

- **Don't use static state everywhere.** The plugin `Instance` singleton is fine; avoid making everything else static. It makes testing impossible and creates hidden coupling.
- **Don't swallow exceptions silently.** Log errors with `Logger.LogError()` or `Logger.LogException()`.
- **Don't forget to unsubscribe.** Every `+=` in `Load()` needs a corresponding `-=` in `Unload()`.
- **Don't use `Thread.Sleep()` or blocking calls on the main thread.** Use `InvokeRepeating`, coroutines, or `TaskDispatcher`.
- **Don't hardcode item/vehicle IDs in logic.** Put them in configuration so server admins can customize.
- **Don't use reflection to access private game members unless absolutely necessary.** Prefer public API, events, and Harmony patches. Reflection breaks silently on game updates.
- **Don't over-engineer.** A simple plugin doesn't need dependency injection, abstract factories, or plugin-within-a-plugin module systems. Match complexity to the problem.

---

## Cooldown Pattern

Many plugins need per-player command cooldowns:

```csharp
using System;
using System.Collections.Generic;
using Steamworks;

namespace YourNamespace.PluginName.Services
{
    public class CooldownManager
    {
        private readonly Dictionary<CSteamID, DateTime> _cooldowns = new();

        public bool IsOnCooldown(CSteamID player, float cooldownSeconds, out float remaining)
        {
            remaining = 0f;
            if (!_cooldowns.TryGetValue(player, out DateTime lastUsed))
                return false;

            double elapsed = (DateTime.UtcNow - lastUsed).TotalSeconds;
            if (elapsed >= cooldownSeconds)
                return false;

            remaining = (float)(cooldownSeconds - elapsed);
            return true;
        }

        public void SetCooldown(CSteamID player)
        {
            _cooldowns[player] = DateTime.UtcNow;
        }

        public void RemoveCooldown(CSteamID player)
        {
            _cooldowns.Remove(player);
        }

        public void Clear() => _cooldowns.Clear();
    }
}
```

---

## VIP / Tiered Permission Pattern

For plugins that offer different limits based on permissions:

```csharp
public static int GetEffectiveLimit(IRocketPlayer player, int defaultValue, List<VipTier> tiers)
{
    int best = defaultValue;
    foreach (var tier in tiers)
    {
        if (player.HasPermission(tier.Permission) && tier.Value > best)
            best = tier.Value;
    }
    return best;
}
```

---

## Chat Message Formatting

For sending messages with custom colors and icons:

```csharp
using SDG.Unturned;
using UnityEngine;

public static class MessageHelper
{
    public static void Send(UnturnedPlayer player, string message, Color color, string iconUrl = null)
    {
        ChatManager.serverSendMessage(
            message,
            color,
            toPlayer: player.SteamPlayer(),
            mode: EChatMode.SAY,
            iconURL: iconUrl,
            useRichTextFormatting: true
        );
    }

    public static void Broadcast(string message, Color color, string iconUrl = null)
    {
        ChatManager.serverSendMessage(
            message,
            color,
            mode: EChatMode.GLOBAL,
            iconURL: iconUrl,
            useRichTextFormatting: true
        );
    }
}
```

---

## Testing & Deployment

1. **Build** the plugin: `dotnet build -c Release`
2. **Copy** the output DLL to `Unturned/Servers/<ServerID>/Rocket/Plugins/`
3. **Start the server** or use `/rocket reload` in the console to hot-reload.
4. **Check logs** in `Rocket/Logs/` for errors.
5. **Test commands** in-game and verify configuration XML is generated correctly.
6. Use `/p reload <PluginName>` for targeted reload during development.

---

## Key Reference Types

| Type | Namespace | Purpose |
|---|---|---|
| `RocketPlugin<T>` | `Rocket.Core.Plugins` | Plugin base class |
| `IRocketPluginConfiguration` | `Rocket.API` | Configuration interface |
| `IRocketCommand` | `Rocket.API` | Command interface |
| `IRocketPlayer` | `Rocket.API` | Abstract player (includes console) |
| `UnturnedPlayer` | `Rocket.Unturned.Player` | Unturned player wrapper |
| `UnturnedChat` | `Rocket.Unturned.Chat` | Chat helper |
| `U` | `Rocket.Unturned` | Static gateway to Rocket services |
| `Logger` | `Rocket.Core.Logging` | Logging (static) |
| `Player` | `SDG.Unturned` | Native Unturned player |
| `SteamPlayer` | `SDG.Unturned` | Player connection metadata |
| `Provider` | `SDG.Unturned` | Server state, player list |
| `ChatManager` | `SDG.Unturned` | Chat system |
| `ItemManager` | `SDG.Unturned` | World items |
| `VehicleManager` | `SDG.Unturned` | Vehicles |
| `BarricadeManager` | `SDG.Unturned` | Player-placed structures |
| `StructureManager` | `SDG.Unturned` | Permanent structures |
| `EffectManager` | `SDG.Unturned` | Effects and UI |
| `DamageTool` | `SDG.Unturned` | Damage system |
| `Assets` | `SDG.Unturned` | Asset lookup |
| `CSteamID` | `Steamworks` | Steam identity |
| `TaskDispatcher` | `SDG.Unturned` | Main thread dispatch |

---

## Testing RocketMod Plugins with VibePlugins.RocketMod.TestBase

### Overview

`VibePlugins.RocketMod.TestBase` is a containerized integration testing framework that runs xUnit tests against real Unturned + RocketMod server instances inside Docker containers. Instead of mocking the game engine, your plugin loads and executes in an actual server process, giving you high-confidence integration tests.

**Architecture:**

```
[ xUnit Test Host ]
       |
       | TCP bridge (JSON protocol)
       v
[ Docker Container ]
  ├── Unturned Dedicated Server
  ├── RocketMod (LDM)
  ├── Your Plugin (deployed automatically)
  └── TestBase Harness (Harmony-patched into the server)
      ├── Mock entity system (players, zombies, animals)
      ├── Command executor
      ├── Event capture system
      └── Remote code execution
```

The test harness is injected into the server via Harmony patches and exposes a TCP bridge that the test host communicates with. The harness can create mock entities, execute commands, capture events, and run arbitrary code on the server thread.

**Key packages:**

| Package | Purpose |
|---|---|
| `VibePlugins.RocketMod.TestBase` | Core framework: base class, assertions, container management |
| `VibePlugins.RocketMod.TestBase.Xunit` | xUnit integration: custom test framework, attributes, parallel execution |
| `VibePlugins.RocketMod.TestBase.Shared` | Shared protocol types between test host and server harness |
| `VibePlugins.RocketMod.TestBase.Tools` | CLI tooling for building container images |

---

### Setup

#### Project Setup

Create a standard xUnit test project targeting `net8.0` (the test host runs on modern .NET; only the plugin itself targets `net48`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="VibePlugins.RocketMod.TestBase" Version="*" />
    <PackageReference Include="VibePlugins.RocketMod.TestBase.Xunit" Version="*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference your plugin project so TPlugin can be resolved -->
    <ProjectReference Include="..\MyPlugin\MyPlugin.csproj" />
  </ItemGroup>
</Project>
```

#### Assembly Attributes

Add these to a file (e.g., `AssemblyInfo.cs` or at the top of any `.cs` file):

```csharp
using VibePlugins.RocketMod.TestBase.Xunit;

[assembly: RocketModTestFramework]
[assembly: ServerWorker(Count = 1)]
```

- `[assembly: RocketModTestFramework]` registers the custom xUnit test framework that manages container lifecycle.
- `[assembly: ServerWorker(Count = N)]` controls how many server containers run in parallel. Use `1` for local development, higher values for CI.

#### Docker Requirement

The framework requires Docker to run Unturned server containers. Enable container support through one of:

- **Environment variable:** Set `VIBEPLUGINS_ENABLE_CONTAINERS=true` (or `1`).
- **Automatic detection:** If the environment variable is not set, the framework probes for Docker by running `docker info`. If Docker is available, containers are enabled automatically.

#### Building the Container Image

Build the Unturned + RocketMod server image used by tests:

```bash
dotnet vibeplugins-rocketmod build-image
```

This produces the `vibeplugins:rocketmod` Docker image containing Unturned Dedicated Server, RocketMod, and the test harness.

---

### Writing Tests

#### Base Class

All test classes inherit from `RocketModPluginTestBase<TPlugin>`, where `TPlugin` is your plugin's main class:

```csharp
using VibePlugins.RocketMod.TestBase;
using Xunit;

public class MyPluginTests : RocketModPluginTestBase<MyPlugin>
{
    [Fact]
    public async Task Plugin_Loads_Successfully()
    {
        // If we get here, the plugin loaded without errors.
        // InitializeAsync already verified plugin load success.
        Assert.NotNull(Server);
    }
}
```

If you do not need a specific plugin type, use the non-generic `RocketModPluginTestBase` which defaults to an internal `TestRocketModPlugin`.

#### Test Lifecycle

Each test follows this lifecycle:

1. **InitializeAsync** (automatic, runs before each test):
   - Gets or creates a `TestSession` (calls `ConfigureContainer` for customization).
   - Cleans the `Plugins/` and `Libraries/` directories in the container.
   - Deploys the plugin assembly from `typeof(TPlugin).Assembly`.
   - Resets the MySQL database if a MySQL sidecar is configured.
   - Restarts the server container.
   - Waits for the test harness to become ready (default timeout: 120s).
   - Waits for the plugin to load (default timeout: 60s).
   - Throws `PluginLoadFailedException` if the plugin fails to load.

2. **Test body** runs.

3. **DisposeAsync** (automatic, runs after each test):
   - Executes any registered cleanup callbacks.
   - Sends `DestroyAllMocks` to remove all mock entities.
   - Sends `ClearEventCaptures` to reset event monitors.

#### Configuring the Container

Override `ConfigureContainer` to customize the Unturned server:

```csharp
public class MyPluginTests : RocketModPluginTestBase<MyPlugin>
{
    protected override void ConfigureContainer(UnturnedContainerBuilder builder)
    {
        builder
            .WithSkipAssets()
            .WithOfflineOnly()
            .WithNoWebRequests()
            .WithMap("PEI")
            .WithMaxPlayersLimit(8);
    }
}
```

#### Adding MySQL

For plugins that require a database:

```csharp
protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    builder
        .WithSkipAssets()
        .WithOfflineOnly()
        .WithMySql(mysql => mysql
            .WithDatabase("myplugin_test")
            .WithPassword("testpass"));
}

[Fact]
public async Task Database_Creates_Tables()
{
    Assert.True(HasMySql);
    // MySqlConnectionString is available for direct DB assertions
    string connStr = MySqlConnectionString;
    // Use a MySQL client library to query the database...
}
```

#### Custom Cleanup

Register cleanup callbacks that run during `DisposeAsync`:

```csharp
[Fact]
public async Task Test_With_Cleanup()
{
    OnCleanup(async () =>
    {
        // Custom cleanup logic here
        await Task.CompletedTask;
    });

    // Test logic...
}
```

#### Overriding Timeouts

Override the default timeouts if your plugin or environment needs more time:

```csharp
public class SlowPluginTests : RocketModPluginTestBase<MyHeavyPlugin>
{
    protected override TimeSpan HarnessReadyTimeout => TimeSpan.FromSeconds(180);
    protected override TimeSpan PluginLoadTimeout => TimeSpan.FromSeconds(90);
}
```

---

### Command Testing

The framework provides two APIs for executing commands: a fluent builder and a direct method.

#### Fluent Builder (Recommended)

Use `ExecuteCommand("name")` to get a `CommandTestBuilder` with fluent configuration:

```csharp
[Fact]
public async Task HealCommand_HealsTarget()
{
    var admin = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());
    var target = await CreatePlayerAsync(p => p.WithName("Target").WithHealth(50));

    var result = await ExecuteCommand("heal")
        .AsPlayer(admin.HandleId)
        .WithArgs("Target", "100")
        .RunAsync();

    result.ShouldSucceed()
          .ShouldContainMessage("healed");
}
```

#### Builder Methods

| Method | Description |
|---|---|
| `.AsPlayer(Guid handleId)` | Execute as a previously created mock player (resolves name, Steam ID, admin from the mock) |
| `.AsPlayer(string name, ulong steamId, bool isAdmin)` | Execute as an inline (ad-hoc) player identity |
| `.AsConsole()` | Execute as server console (default: Steam ID 0, admin) |
| `.WithArgs(params string[] args)` | Set the command arguments |
| `.WithTimeout(TimeSpan timeout)` | Override the default 30-second timeout |
| `.RunAsync()` | Send the command and return a `CommandTestResult` |

#### Direct Execution

For simpler cases, use `ExecuteCommandAsync` directly:

```csharp
var result = await ExecuteCommandAsync(
    commandName: "balance",
    args: new[] { "Alice" },
    callerSteamId: 76561198000000001,
    callerName: "Admin",
    isAdmin: true);

result.ShouldSucceed();
```

#### Asserting on Results

`CommandTestResult` provides fluent assertion methods that throw `XunitException` on failure:

| Method | Description |
|---|---|
| `.ShouldSucceed()` | Assert status is `CommandStatus.Success` |
| `.ShouldFail()` | Assert status is not `Success` |
| `.ShouldHaveStatus(CommandStatus expected)` | Assert a specific status |
| `.ShouldThrow<TException>()` | Assert command threw the specified exception type |
| `.ShouldThrow(string typeName)` | Assert exception type name contains the substring |
| `.ShouldContainMessage(string substring)` | Assert at least one chat message contains the text |
| `.ShouldContainMessage(Func<ChatMessageInfo, bool>)` | Assert at least one chat message matches the predicate |
| `.ShouldNotContainMessage(string substring)` | Assert no chat message contains the text |
| `.ShouldHaveMessageCount(int expected)` | Assert exact number of chat messages produced |

All assertion methods return `this` for fluent chaining:

```csharp
result.ShouldSucceed()
      .ShouldContainMessage("healed")
      .ShouldNotContainMessage("error")
      .ShouldHaveMessageCount(1);
```

#### Properties on CommandTestResult

- `Status` (`CommandStatus`) -- the execution status.
- `ChatMessages` (`IReadOnlyList<ChatMessageInfo>`) -- chat messages produced during execution.
- `Exception` (`SerializableExceptionInfo`) -- exception info if the command threw, or `null`.
- `Response` (`ExecuteCommandResponse`) -- the raw underlying response.

---

### Event Testing

The framework captures server-side events and makes them available for assertions in the test host.

#### Event Monitor Pattern

Start monitoring an event type before the action that triggers it, then wait for the event:

```csharp
[Fact]
public async Task WelcomeMessage_OnPlayerConnect()
{
    var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

    await CreatePlayerAsync(p => p.WithName("NewPlayer"));

    var msg = await chatMonitor.WaitForAsync(
        m => m.Text.Contains("Welcome"),
        timeout: TimeSpan.FromSeconds(5));

    Assert.NotNull(msg);
    Assert.Contains("NewPlayer", msg.Text);
}
```

#### EventMonitor Methods

| Method | Description |
|---|---|
| `WaitForAsync(predicate, timeout)` | Wait for a single event matching the predicate. Default timeout: 10s. Throws `TimeoutException` if no match. |
| `WaitForCountAsync(count, predicate, timeout)` | Wait for N matching events. Returns `IReadOnlyList<TEvent>`. |
| `ShouldNotOccurAsync(predicate, timeout)` | Assert that no matching event occurs within the timeout. Default timeout: 2s. Throws `XunitException` if one does. |

#### Negative Assertions

Verify that an event does NOT happen:

```csharp
[Fact]
public async Task Admin_DoesNot_ReceiveWelcome()
{
    var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

    await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());

    await chatMonitor.ShouldNotOccurAsync(
        m => m.Text.Contains("Welcome"),
        timeout: TimeSpan.FromSeconds(3));
}
```

#### Collecting Multiple Events

```csharp
[Fact]
public async Task BroadcastReachesAllPlayers()
{
    var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

    await CreatePlayerAsync(p => p.WithName("Alice"));
    await CreatePlayerAsync(p => p.WithName("Bob"));

    await ExecuteCommand("broadcast")
        .AsConsole()
        .WithArgs("Hello everyone!")
        .RunAsync();

    var messages = await chatMonitor.WaitForCountAsync(
        count: 2,
        predicate: m => m.Text.Contains("Hello everyone!"),
        timeout: TimeSpan.FromSeconds(5));

    Assert.Equal(2, messages.Count);
}
```

#### Scenario Builder

For complex multi-step tests, use the `ScenarioBuilder` with Given/When/Then syntax:

```csharp
[Fact]
public async Task HealScenario()
{
    await CreateScenario()
        .Given(async () => await CreatePlayerAsync(p => p.WithName("Alice").WithHealth(50)))
        .When(async () => await ExecuteCommand("heal")
            .AsConsole()
            .WithArgs("Alice", "100")
            .RunAsync())
        .ThenExpectMessage("healed")
        .ThenExpectNoEvent<PlayerDeathEvent>()
        .ExecuteAsync()
        .ContinueWith(t => t.Result.ShouldPass());
}
```

**ScenarioBuilder methods:**

| Method | Description |
|---|---|
| `.Given(Func<Task> setup)` | Add a setup step (runs first, in order) |
| `.When(Func<Task> action)` | Add an action under test (runs after all Given steps, in order) |
| `.ThenExpect<TEvent>(predicate, timeout)` | Expect an event to occur (default timeout: 10s) |
| `.ThenExpectMessage(string substring)` | Expect a chat message containing the substring |
| `.ThenExpectNoEvent<TEvent>(timeout)` | Expect no event of this type (default timeout: 2s) |
| `.ExecuteAsync()` | Run the scenario and return a `ScenarioResult` |

**ScenarioResult:**

- `.AllExpectationsMet` -- `true` if all expectations passed.
- `.Failures` -- list of failure description strings.
- `.ShouldPass()` -- throws `XunitException` listing all failures if any expectation was not met.

---

### Mock Entities

The framework creates mock entities on the server that behave like real game objects from the plugin's perspective.

#### Creating Players

```csharp
var player = await CreatePlayerAsync(p => p
    .WithName("Alice")
    .WithSteamId(76561198000000001)
    .WithHealth(100)
    .WithMaxHealth(100)
    .AsAdmin()
    .AtPosition(100f, 50f, 200f)
    .WithExperience(5000));
```

`CreatePlayerAsync` returns a `CreateMockResponse` containing:
- `HandleId` (`Guid`) -- used to reference this mock in subsequent calls (e.g., `AsPlayer(handleId)`).

**PlayerOptions fluent methods:**

| Method | Description |
|---|---|
| `.WithName(string)` | Display name (default: `"TestPlayer"`) |
| `.WithSteamId(ulong)` | Steam64 ID (default: auto-generated) |
| `.WithHealth(byte)` | Current health 0-255 (default: `100`) |
| `.WithMaxHealth(byte)` | Maximum health 0-255 (default: `100`) |
| `.AsAdmin()` | Grant admin privileges |
| `.AtPosition(float x, float y, float z)` | Spawn position (default: origin) |
| `.WithExperience(uint)` | Starting experience points (default: `0`) |

#### Creating Zombies

```csharp
var zombie = await CreateZombieAsync(z => z
    .AtPosition(100f, 0f, 100f)
    .WithHealth(300)
    .WithSpeciality(1));
```

**ZombieOptions fluent methods:**

| Method | Description |
|---|---|
| `.AtPosition(float x, float y, float z)` | Spawn position (default: origin) |
| `.WithHealth(ushort)` | Starting health (default: `100`) |
| `.WithSpeciality(byte)` | Zombie speciality/type identifier (default: `0`) |

#### Creating Animals

```csharp
var animal = await CreateAnimalAsync(a => a
    .AtPosition(50f, 0f, 50f)
    .WithHealth(200)
    .WithAnimalId(1));
```

**Note:** `CreateAnimalAsync` follows the same pattern but is defined on the base class. `AnimalOptions` supports:

| Method | Description |
|---|---|
| `.AtPosition(float x, float y, float z)` | Spawn position (default: origin) |
| `.WithHealth(ushort)` | Starting health (default: `100`) |
| `.WithAnimalId(ushort)` | Animal type ID from Unturned's animal table (default: `0`) |

---

### Remote Code Execution

For scenarios that require running custom logic directly on the server, use `RunOnServerAsync<T>`:

```csharp
[Fact]
public async Task CheckServerState()
{
    int playerCount = await RunOnServerAsync<int>(
        typeName: "SDG.Unturned.Provider, Assembly-CSharp",
        methodName: "get_clients",
        serializedArgs: Array.Empty<string>());

    Assert.True(playerCount >= 0);
}
```

**Parameters:**

| Parameter | Description |
|---|---|
| `typeName` | Assembly-qualified type name containing the static method |
| `methodName` | Name of the static method to invoke |
| `serializedArgs` | JSON-serialized arguments for the method |

**Returns:** The deserialized result of type `T`. If the remote method returns void or null, returns `default(T)`.

**Throws:** `RemoteExecutionException` if the server-side invocation fails, with the remote exception type and message.

---

### Tick/Frame Waiting

Wait for the server's game loop to advance a specific number of frames:

```csharp
// Wait for 5 Update frames (the main Unity Update loop)
await WaitTicksAsync(5);

// Wait for 1 FixedUpdate frame (physics update, runs at fixed intervals)
await WaitTicksAsync(1, TickType.FixedUpdate);

// Wait for 3 LateUpdate frames
await WaitTicksAsync(3, TickType.LateUpdate);
```

This is essential for testing logic that depends on game frame timing, such as:
- Coroutines that yield with `WaitForEndOfFrame` or `WaitForFixedUpdate`.
- Physics-based interactions that only resolve during `FixedUpdate`.
- UI updates or deferred operations that happen in `LateUpdate`.
- Anything using `InvokeRepeating` or frame-counting timers.

---

### Container Configuration

All methods on `UnturnedContainerBuilder` available for `ConfigureContainer`:

#### Server Flags

| Method | Description |
|---|---|
| `.WithSkipAssets(bool enabled = true)` | Skip loading asset bundles (`-SkipAssets`). Speeds up container startup significantly. |
| `.WithOfflineOnly()` | Run without Steam networking (`-OfflineOnly`). Required for most test environments. |
| `.WithNoWebRequests()` | Prevent outbound HTTP requests (`-NoWebRequests`). Good for isolated tests. |
| `.WithMap(string mapName)` | Set the map (default: `"PEI"`). Maps to `+InternetServer/<mapName>`. |
| `.WithMaxPlayersLimit(int limit)` | Cap maximum player count (`-MaxPlayersLimit`). |
| `.WithGameplayConfigFile(string path)` | Specify a gameplay config file path inside the container. |
| `.WithLogGameplayConfig()` | Log gameplay configuration on startup (`-LogGameplayConfig`). |
| `.WithConstNetEvents()` | Enable deterministic networking events (`-ConstNetEvents`). |
| `.WithCustomFlag(string flag)` | Add any arbitrary CLI flag to the server launch command. |
| `.WithImage(string image)` | Override the Docker image (default: `"vibeplugins:rocketmod"`). |
| `.WithBridgePort(int port)` | Override the TCP bridge port (default: `27099`). |
| `.WithServerName(string name)` | Override the Unturned server directory name (default: `"TestServer"`). |

#### Sidecar Containers

| Method | Description |
|---|---|
| `.WithMySql(Action<MySqlSidecarOptions> configure)` | Add a MySQL 8.0 sidecar container. |
| `.WithRedis()` | Add a Redis 7 (Alpine) sidecar container. |
| `.WithAdditionalContainer(string name, IContainer container)` | Add any custom sidecar container (Testcontainers `IContainer`). |

#### MySQL Configuration

`MySqlSidecarOptions` supports fluent configuration:

| Method | Default | Description |
|---|---|---|
| `.WithDatabase(string)` | `"test"` | Database name to create |
| `.WithUsername(string)` | `"root"` | MySQL username |
| `.WithPassword(string)` | `"test"` | MySQL root password |
| `.WithPort(int)` | `3306` | Port to expose |

#### Lifecycle

| Method | Description |
|---|---|
| `.WithCleanupCallback(Func<Task>)` | Register a callback invoked when the container is disposed. |

---

### Distributed Testing

#### Parallel Execution with Server Workers

Configure multiple server containers for parallel test execution:

```csharp
[assembly: ServerWorker(Count = 3)]
```

This creates 3 independent Unturned server containers. The custom xUnit test framework distributes test classes across available workers using load balancing. Each test class runs on a single worker, and different test classes can run in parallel on different workers.

**How load balancing works:**
- When a test class starts, the framework acquires an available server worker from the pool.
- The worker's container is configured, the plugin is deployed, and the server is restarted.
- When the test class finishes, the worker is released back to the pool.
- Multiple test classes execute concurrently, up to the number of available workers.

**Container reuse between tests:**
- Within a test class, the same container is reused across all `[Fact]` methods.
- Between tests, the plugin directories are cleaned, the plugin is redeployed, and the server is restarted.
- Mocks and event captures are cleared between tests via `DisposeAsync`.
- The container itself is not destroyed between tests -- only restarted -- for faster iteration.

---

### CI/CD Integration

#### Conditional Test Execution

Use attributes to control which tests run based on container availability:

```csharp
// This test will be skipped if containers are unavailable
[RequiresContainer]
[Fact]
public async Task Integration_RequiresServer()
{
    var result = await ExecuteCommand("status").AsConsole().RunAsync();
    result.ShouldSucceed();
}

// This test runs even without containers (for unit-level logic)
[SkipContainer]
[Fact]
public void Config_Validation_NoContainerNeeded()
{
    var config = new MyPluginConfiguration();
    config.LoadDefaults();
    Assert.Equal(10, config.MaxItems);
}
```

#### Environment Variable

Set `VIBEPLUGINS_ENABLE_CONTAINERS` in your CI pipeline:

- `true` or `1` -- enable container tests.
- `false` or `0` -- disable container tests (tests marked `[RequiresContainer]` are skipped).
- Not set -- the framework auto-detects Docker availability.

#### GitHub Actions Example

```yaml
name: Plugin Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      docker:
        image: docker:dind
        options: --privileged
    env:
      VIBEPLUGINS_ENABLE_CONTAINERS: "true"
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build container image
        run: dotnet vibeplugins-rocketmod build-image

      - name: Build plugin
        run: dotnet build src/MyPlugin/MyPlugin.csproj -c Release

      - name: Run tests
        run: dotnet test src/MyPlugin.Tests/MyPlugin.Tests.csproj -c Release --logger "trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: "**/*.trx"
```

---

### Running Tests

#### Running Locally

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run a specific test class
dotnet test --filter "FullyQualifiedName~MyPluginTests"

# Run with container support explicitly enabled
VIBEPLUGINS_ENABLE_CONTAINERS=true dotnet test
```

#### Interpreting Results

| Failure Type | What It Means | How to Fix |
|---|---|---|
| `PluginLoadFailedException` | Your plugin threw during `Load()` or a dependency is missing. | Check plugin code, ensure all DLL dependencies are included. |
| `ServerStartupFailedException` | The Unturned server or harness failed to start. | Check Docker is running, image is built, container logs. |
| `TimeoutException` (harness ready) | The server did not become ready within the timeout. | Increase `HarnessReadyTimeout`, check container resources. |
| `TimeoutException` (event wait) | An expected event did not fire within the timeout. | Verify plugin logic triggers the event, increase timeout. |
| `XunitException` (assertion) | A fluent assertion (e.g., `ShouldSucceed`) failed. | Inspect the failure message for details on actual vs. expected. |
| `RemoteExecutionException` | Server-side code threw during `RunOnServerAsync`. | Check the remote exception type and message in the error. |
| `OperationCanceledException` | A bridge request timed out. | Check network, container health, increase timeout. |

#### Troubleshooting

- **Container logs:** Use `docker logs <container-id>` to inspect Unturned server output.
- **Increasing timeouts:** Override `HarnessReadyTimeout` and `PluginLoadTimeout` on your test class.
- **Docker availability:** Run `docker info` to verify Docker is accessible.
- **Image not found:** Run `dotnet vibeplugins-rocketmod build-image` to build or rebuild the server image.
- **Port conflicts:** The bridge port is mapped dynamically. If tests fail intermittently, check for port exhaustion.
- **Plugin not loading:** Ensure your `TPlugin` type is public and in the correct assembly. Check that all plugin dependencies are resolvable.

---

### Known Limitations & Gotchas

These issues were discovered during integration testing and are important to be aware of:

**Containerfile & Server Startup:**
- SteamCMD requires `+force_install_dir /opt/unturned` — the default install path is unpredictable
- The Unturned server instance directory is named after the **map** (e.g., `Servers/PEI/`), not a custom server name. `WithMap("MyMap")` changes both the map and the server directory
- `-SkipAssets` is added by default for faster startup (~60-90 seconds vs ~3-5 minutes)
- First Docker image build downloads ~1.9 GB via SteamCMD; subsequent builds use the Docker cache

**Plugin Deployment:**
- `0Harmony.dll` must be explicitly copied to `Rocket/Libraries/` — RocketMod cannot resolve it otherwise
- The framework uses `AppDomain.CurrentDomain.BaseDirectory` to find transitive dependencies because `Assembly.Location` may point to shadow-copied paths
- The TestHarness plugin DLL is automatically deployed alongside your test plugin

**Server Lifecycle & Containers:**
- `docker restart` preserves the container but may reassign the mapped port — the framework queries `docker port` directly instead of using Testcontainers' cached value
- Testcontainers' `StopAsync()` destroys the container; the framework uses `docker restart` via CLI for reuse
- Do not use `WithWaitStrategy` for port 27099 — the harness plugin isn't deployed when the container first starts

**TCP Bridge / Harness Timing:**
- `Level.onPostLevelLoaded` fires before RocketMod plugin `Load()` in the loading sequence — the harness sends the ready message when a client connects, not on level load
- The bridge retry loop retries the entire connect+wait-for-ready cycle, not just connection attempts
- The server may accept TCP connections before the harness TCP listener is fully initialized

**xUnit Framework Integration:**
- `TestFrameworkAttribute` is sealed in xUnit 2.9.x — use `RocketModTestFrameworkConstants` with `[assembly: TestFramework(...)]`
- `XunitTestAssemblyRunner.RunAsync()` is not virtual — initialization is done in `RunTestCollectionsAsync()`
- `XunitTestMethodRunner.ConstructorArguments` is not a protected member — stored locally

**RocketMod LDM (RestoreMonarchy.RocketRedist) Specifics:**
- Uses synchronous `Load()` / `Unload()`, NOT the async `OnActivate()` / `OnDeactivate()` from the Rocket.Core reference submodule
- Harmony patches targeting plugin lifecycle must target `RocketPlugin.Load()`, not `Plugin.ActivateAsync()`
- `U.Events.OnPlayerConnected` requires the `Rocket.Unturned.U` class instance, not `UnturnedEvents` static access

---

### Testing Framework Reference

Quick-reference table of key types and their purpose.

| Type | Namespace | Purpose |
|---|---|---|
| `RocketModPluginTestBase<TPlugin>` | `VibePlugins.RocketMod.TestBase` | Base class for all plugin integration tests |
| `RocketModPluginTestBase` | `VibePlugins.RocketMod.TestBase` | Non-generic base class (uses default test plugin) |
| `CommandTestBuilder` | `VibePlugins.RocketMod.TestBase.Assertions` | Fluent builder for configuring and executing commands |
| `CommandTestResult` | `VibePlugins.RocketMod.TestBase.Assertions` | Wraps command response with fluent assertion methods |
| `EventMonitor<TEvent>` | `VibePlugins.RocketMod.TestBase.Assertions` | Waits for and asserts on server-side events |
| `ScenarioBuilder` | `VibePlugins.RocketMod.TestBase.Assertions` | Given/When/Then builder for multi-step test scenarios |
| `ScenarioResult` | `VibePlugins.RocketMod.TestBase.Assertions` | Result of a scenario execution with pass/fail info |
| `UnturnedContainerBuilder` | `VibePlugins.RocketMod.TestBase.Containers` | Fluent builder for configuring the server container |
| `MySqlSidecarOptions` | `VibePlugins.RocketMod.TestBase.Containers` | Configuration for the MySQL sidecar container |
| `PlayerOptions` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Configuration for mock player creation |
| `ZombieOptions` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Configuration for mock zombie creation |
| `AnimalOptions` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Configuration for mock animal creation |
| `ChatMessageEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured chat message event |
| `PlayerConnectedEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured player connection event |
| `PlayerDisconnectedEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured player disconnection event |
| `PlayerDeathEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured player death event |
| `PlayerDamageEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured player damage event |
| `VehicleEnterEvent` | `VibePlugins.RocketMod.TestBase.Shared.Protocol` | Captured vehicle enter event |
| `ContainerSupport` | `VibePlugins.RocketMod.TestBase.Xunit` | Static helper to check Docker availability |
| `RocketModTestFrameworkAttribute` | `VibePlugins.RocketMod.TestBase.Xunit` | Assembly attribute to register the custom xUnit framework |
| `ServerWorkerAttribute` | `VibePlugins.RocketMod.TestBase.Xunit` | Assembly attribute to configure parallel worker count |
| `RequiresContainerAttribute` | `VibePlugins.RocketMod.TestBase.Xunit` | Marks a test as requiring a running container |
| `SkipContainerAttribute` | `VibePlugins.RocketMod.TestBase.Xunit` | Marks a test as runnable without a container |
