# VibeVault

An Unturned RocketMod plugin that adds virtual storage vaults for players, similar to Minecraft's ender chests. Vaults persist across deaths and server restarts, support multiple tiers, sharing, group access, and more.

## Features

- Multiple vault tiers with configurable sizes (default, VIP, elite)
- Permission-based access control
- File or MySQL storage backends
- Group vaults shared within game groups (one user at a time)
- Vault sharing with read/write or read-only access
- Read-only vault viewing for admins
- Vault tier upgrades costing XP
- Trash command for quick item disposal
- Public API for developer integration
- Auto-saves on disconnect and death

## Installation

1. Download `VibeVault.dll`.
2. Place it in your `Rocket/Plugins/` folder.
3. Restart the server to generate the default configuration.
4. Edit the config at `Rocket/Plugins/VibeVault/VibeVault.configuration.xml`.
5. Set up permissions in your Rocket permissions config.

## Configuration

The configuration file is auto-generated on first run. Below is an overview of the available settings.

| Setting | Description | Default |
|---------|-------------|---------|
| `StorageType` | Storage backend (`file` or `mysql`) | `file` |
| `MySqlConnectionString` | MySQL connection string (only used when StorageType is `mysql`) | — |
| `StorageBarricadeId` | Asset ID for the storage barricade | `328` (Metal Locker) |

### Vault Tiers

Vault tiers are defined under `<VaultTiers>`. Each tier has a name, required permission, grid dimensions, and a priority (higher priority tiers are opened by default when no name is specified).

```xml
<VaultTiers>
  <VaultTier>
    <Name>default</Name>
    <Permission>vibevault.vault.default</Permission>
    <Width>8</Width>
    <Height>4</Height>
    <Priority>0</Priority>
  </VaultTier>
  <VaultTier>
    <Name>vip</Name>
    <Permission>vibevault.vault.vip</Permission>
    <Width>10</Width>
    <Height>6</Height>
    <Priority>1</Priority>
  </VaultTier>
  <VaultTier>
    <Name>elite</Name>
    <Permission>vibevault.vault.elite</Permission>
    <Width>12</Width>
    <Height>8</Height>
    <Priority>2</Priority>
  </VaultTier>
</VaultTiers>
```

### Group Vault

```xml
<GroupVault>
  <Width>10</Width>
  <Height>6</Height>
  <Permission>vibevault.group</Permission>
</GroupVault>
```

### Trash

```xml
<Trash>
  <Width>8</Width>
  <Height>4</Height>
  <Permission>vibevault.trash</Permission>
</Trash>
```

### Upgrades

Upgrade paths define which tiers can be upgraded to which, along with the XP cost.

```xml
<Upgrades>
  <Upgrade>
    <FromTier>default</FromTier>
    <ToTier>vip</ToTier>
    <ExperienceCost>500</ExperienceCost>
    <Permission>vibevault.upgrade</Permission>
  </Upgrade>
  <Upgrade>
    <FromTier>vip</FromTier>
    <ToTier>elite</ToTier>
    <ExperienceCost>1500</ExperienceCost>
    <Permission>vibevault.upgrade</Permission>
  </Upgrade>
</Upgrades>
```

## Commands

| Command | Aliases | Arguments | Description |
|---------|---------|-----------|-------------|
| `/vault` | `/v` | `[name] [player]` | Open a personal vault. No arguments opens your best tier. One argument opens a named vault. Two arguments opens another player's vault (requires `vibevault.vault.other`). |
| `/vaults` | `/vlist` | `[player]` | List all vaults you have access to. Optionally specify a player to view theirs. |
| `/vaultview` | `/vview`, `/vv` | `<player> [name]` | Open a read-only view of another player's vault. |
| `/vaultshare` | `/vshare`, `/vs` | `<player> [vault] [readonly]` | Share a vault with another player. Use `/vaultshare remove <player> [vault]` to revoke access. |
| `/vaultupgrade` | `/vupgrade`, `/vu` | `[from] [to]` | Upgrade a vault tier. Costs XP as defined in the config. |
| `/trash` | `/t` | — | Open a trash vault. All items inside are destroyed when the vault is closed. |

## Permissions

| Permission | Description |
|------------|-------------|
| `vibevault.vault` | Base vault access |
| `vibevault.vault.default` | Access the default vault tier |
| `vibevault.vault.vip` | Access the VIP vault tier |
| `vibevault.vault.elite` | Access the elite vault tier |
| `vibevault.vault.other` | Open another player's vault |
| `vibevault.group` | Access group vault |
| `vibevault.view` | Read-only vault viewing |
| `vibevault.share` | Share vaults with other players |
| `vibevault.upgrade` | Upgrade vault tier |
| `vibevault.trash` | Use the trash command |

## Group Vaults

Group vaults are shared storage spaces accessible by all members of an in-game group. Only one player can have a group vault open at a time to prevent conflicts. Group vault dimensions and the required permission are configured in the `<GroupVault>` section.

Players need the `vibevault.group` permission to access their group's vault.

## Vault Sharing

Players can share their personal vaults with others using the `/vaultshare` command.

**Share a vault with read/write access:**
```
/vaultshare PlayerName vip
```

**Share a vault with read-only access:**
```
/vaultshare PlayerName vip readonly
```

**Remove a shared vault:**
```
/vaultshare remove PlayerName vip
```

If no vault name is specified, the player's best available tier is used. The sharing player needs the `vibevault.share` permission.

## Upgrades

Vault upgrades allow players to move from a lower tier to a higher tier by spending XP. Upgrade paths and costs are defined in the `<Upgrades>` config section.

```
/vaultupgrade default vip
```

This deducts the configured XP cost and migrates the vault contents to the new tier. Players need the `vibevault.upgrade` permission.

## Trash

The `/trash` command opens a temporary vault. Any items placed inside are permanently destroyed when the vault is closed. This is useful for quickly disposing of unwanted items. Players need the `vibevault.trash` permission.

## API for Developers

VibeVault exposes a public API through the `IVibeVaultAPI` interface, accessible via `VibeVaultPlugin.Instance.API`.

### Methods

| Method | Description |
|--------|-------------|
| `GetVault(ownerId, vaultName)` | Retrieve a player's vault data |
| `SaveVault(data)` | Save vault data |
| `GetPlayerVaults(ownerId)` | Get all vaults belonging to a player |
| `IsVaultOpen(ownerId, vaultName)` | Check if a vault is currently open |
| `ShareVault(ownerId, vaultName, targetId, canModify)` | Share a vault with another player |
| `UnshareVault(ownerId, vaultName, targetId)` | Remove vault sharing for a player |
| `GetSharedVaults(playerId)` | Get all vaults shared with a player |

### Events

| Event | Description |
|-------|-------------|
| `OnVaultOpened` | Fired when a player opens a vault (params: ownerId, vaultName) |
| `OnVaultClosed` | Fired when a player closes a vault (params: ownerId, vaultName) |

### Example Usage

```csharp
var api = VibeVaultPlugin.Instance.API;

// Get a player's vault
var vault = api.GetVault(playerId, "default");

// Check if a vault is in use
bool isOpen = api.IsVaultOpen(playerId, "vip");

// Listen for events
api.OnVaultOpened += (ownerId, vaultName) =>
{
    Rocket.Core.Logging.Logger.Log($"{ownerId} opened vault {vaultName}");
};
```

## Troubleshooting

**Vault does not open:**
- Verify the player has the correct permission (e.g., `vibevault.vault` and a tier permission like `vibevault.vault.default`).
- Check the server console for error messages.

**Items are lost after restart:**
- Ensure the storage backend is configured correctly. If using MySQL, verify the connection string is valid and the database is reachable.
- Check that the server is shutting down gracefully so auto-save triggers.

**Group vault says it is already in use:**
- Only one player can access a group vault at a time. Wait for the other player to close it, or have them disconnect (auto-save will release the lock).

**MySQL connection errors:**
- Verify `MySqlConnectionString` in the config is correct.
- Ensure the MySQL server is running and accessible from the game server.
- Check that the database user has CREATE, SELECT, INSERT, UPDATE, and DELETE permissions.

**Upgrade command fails:**
- Confirm the player has enough XP for the upgrade.
- Verify the upgrade path exists in the config (e.g., `default` to `vip`).
- Check that the player has the `vibevault.upgrade` permission.
