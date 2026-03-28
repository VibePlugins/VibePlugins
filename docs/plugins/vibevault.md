---
uid: plugins-vibevault
---

# VibeVault Plugin

VibeVault is an Unturned RocketMod plugin that adds virtual storage vaults for players, similar to Minecraft's ender chests. Vaults persist across deaths and server restarts.

## Quick Start

1. Place `VibeVault.dll` in your `Rocket/Plugins/` folder.
2. Restart the server to generate the default configuration.
3. Edit `Rocket/Plugins/VibeVault/VibeVault.configuration.xml`.
4. Assign permissions to your groups in the Rocket permissions config.

## Commands

| Command | Aliases | Arguments | Description |
|---------|---------|-----------|-------------|
| `/vault` | `/v` | `[name] [player]` | Open a personal vault. No args opens best tier; one arg opens a named vault; two args opens another player's vault. |
| `/vaults` | `/vlist` | `[player]` | List all vaults you have access to. |
| `/vaultview` | `/vview`, `/vv` | `<player> [name]` | Read-only view of another player's vault. |
| `/vaultshare` | `/vshare`, `/vs` | `<player> [vault] [readonly]` | Share a vault. Use `remove` subcommand to revoke. |
| `/vaultupgrade` | `/vupgrade`, `/vu` | `[from] [to]` | Upgrade vault tier (costs XP). |
| `/trash` | `/t` | -- | Trash vault; items destroyed on close. |

## Permissions

| Permission | Description |
|------------|-------------|
| `vibevault.vault` | Base vault access |
| `vibevault.vault.default` | Access default tier |
| `vibevault.vault.vip` | Access VIP tier |
| `vibevault.vault.elite` | Access elite tier |
| `vibevault.vault.other` | Access other players' vaults |
| `vibevault.group` | Group vault access |
| `vibevault.view` | Read-only vault viewing |
| `vibevault.share` | Share vaults |
| `vibevault.upgrade` | Upgrade vault tier |
| `vibevault.trash` | Use trash command |

## Configuration

### Storage Backend

Set `StorageType` to `file` (default) for JSON file storage, or `mysql` to use a MySQL database. When using MySQL, set `MySqlConnectionString` to your connection string.

### Vault Tiers

Define tiers under `<VaultTiers>`. Each tier has a `Name`, `Permission`, `Width`, `Height`, and `Priority`. The highest-priority tier a player has permission for is opened by default.

```xml
<VaultTiers>
  <VaultTierConfig Name="default" Permission="vibevault.vault.default" Width="8" Height="4" Priority="0" />
  <VaultTierConfig Name="vip" Permission="vibevault.vault.vip" Width="10" Height="6" Priority="1" />
  <VaultTierConfig Name="elite" Permission="vibevault.vault.elite" Width="12" Height="8" Priority="2" />
</VaultTiers>
```

### Group Vaults

Group vaults are shared among players in the same in-game group. Only one player can access the group vault at a time.

```xml
<GroupVault Width="8" Height="6" Permission="vibevault.group" />
```

### Upgrades

Define upgrade paths with XP costs:

```xml
<Upgrades>
  <VaultUpgradeConfig FromTier="default" ToTier="vip" ExperienceCost="1000" Permission="vibevault.upgrade" />
  <VaultUpgradeConfig FromTier="vip" ToTier="elite" ExperienceCost="5000" Permission="vibevault.upgrade" />
</Upgrades>
```

## Vault Sharing

Share vaults with other players using `/vaultshare`:

```
/vaultshare PlayerName vip           # Full access
/vaultshare PlayerName vip readonly  # Read-only
/vaultshare remove PlayerName vip    # Revoke
```

## Developer API

Access the API via `VibeVaultPlugin.Instance.API` (implements `IVibeVaultAPI`):

```csharp
var api = VibeVaultPlugin.Instance.API;

// Read vault data
var vault = api.GetVault(playerId, "default");

// Check status
bool inUse = api.IsVaultOpen(playerId, "vip");

// Share programmatically
api.ShareVault(ownerId, "default", targetId, canModify: true);

// Listen for events
api.OnVaultOpened += (ownerId, vaultName) =>
{
    Rocket.Core.Logging.Logger.Log($"{ownerId} opened {vaultName}");
};
```

### API Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetVault(ownerId, vaultName)` | `VaultData?` | Load vault data |
| `SaveVault(data)` | `bool` | Save vault data |
| `GetPlayerVaults(ownerId)` | `List<VaultData>` | All player vaults |
| `IsVaultOpen(ownerId, vaultName)` | `bool` | Check if vault is in use |
| `ShareVault(ownerId, vaultName, targetId, canModify)` | `bool` | Share a vault |
| `UnshareVault(ownerId, vaultName, targetId)` | `bool` | Revoke sharing |
| `GetSharedVaults(playerId)` | `List<SharedVaultAccess>` | Vaults shared with player |

### Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `OnVaultOpened` | `string ownerId, string vaultName` | Fired when a vault opens |
| `OnVaultClosed` | `string ownerId, string vaultName` | Fired when a vault closes |
