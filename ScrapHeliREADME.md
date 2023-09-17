# MyScrapHeli
A simple plugin to spawn a scrap heli for yourself. Limits to one scrap heli per player with optional cooldown (using permission).

## Permissions

- `myscrapheli.spawn` -- Allows player to spawn a scrap heli (/myscrap)
- `myscrapheli.fetch`    -- Allows player to use /gscrap retrieve their scrap heli
- `myscrapheli.where`    -- Allows player to use /wscrap to locate their scrap heli (NEW!)
- `myscrapheli.admin`  -- Allows an admin to run console commands (may change)
- `myscrapheli.cooldown` -- Adds a cooldown to player
- `myscrapheli.unlimited` -- Player can fly without fuel usage (will need to add at least 1 LGF unless "Allow unlimited to use fuel tank" is set to false)

## Chat Commands

- `/myscrap` -- Spawn a scrap heli
- `/noscrap` -- Despawn scrap heli
- `/wscrap`  -- Find scrap heli
- `/gscrap`  -- Get/fetch scrap heli

## Console Commands

- `spawnscrapheli <player ID>`
- `killscrapheli <player ID>`

## For Developers

```csharp
(void) SpawnMyScrapHeli (BasePlayer player)
(void) KillMyScrapHeliPlease (BasePlayer player)
```

## Configuration

```json
{
  "Global": {
    "allowWhenBlocked": false,
    "useCooldown": true,
    "copterDecay": false,
    "allowDamage": true,
    "killOnSleep": false,
    "allowFuelIfUnlimited": false,
    "allowDriverDismountWhileFlying": true,
    "allowPassengerDismountWhileFlying": true,
    "stdFuelConsumption": 0.25,
    "cooldownmin": 60.0,
    "mindistance": 0.0,
    "ghelidistance": 0.0,
    "minDismountHeight": 7.0,
    "startingFuel": 0.0,
    "Prefix": "[My ScrapHeli] :"
  },
  "VIPSettings": {
    "myscrapheli.viplevel1": {
      "unlimited": false,
      "canloot": true,
      "stdFuelConsumption": 0.15,
      "startingFuel": 20.0,
      "cooldownmin": 120.0,
      "mindistance": 0.0,
      "ghelidistance": 0.0
    }
  },
  "Version": {
    "Major": 0,
    "Minor": 4,
    "Patch": 7
  }
}
```

Global:

- `allowWhenBlocked` -- Set to true to allow player to use /myscrap while building blocked
- `useCooldown` -- Enforce a cooldown for minutes between use of /myscrap.
- `useNoEscape` -- Use the NoEscape plugin to check and prevent command use while "raid blocked" per that plugin
- `copterDecay` -- Enable decay
- `allowDamage` -- Enable/allow damage (old default)
- `killOnSleep` -- Kill the copter when the user leaves the server
- `allowFuelIfUnlimited` -- Allow unlimited permission users to add fuel anyway.
- `allowDriverDismountWhileFlying` -- Allow the driver to dismount while flying above minDismountHeight.
- `allowPassengerDismountWhileFlying` --  Allow passenger to dismount while flying above minDismountHeight.
- `stdFuelConsumption` -- Adjust fuel consumption per second from standard amount (0.25f)
- `cooldownmin` -- Minutes to wait between usage of /myscrap
- `mindistance` -- Minimum distance to copter for using /noscrap
- `gscrapdistance` -- Minimum distance to copter for using /gscrap
- `minDismountHeight` -- Minimum height for dismount (for allow rules above)
- `startingFuel` -- How much fuel to start with for non-unlimited permission players (default 0)
- `Prefix` -- Prefix for chat messages (default [MyScrapHeli: ])

Set "Value in meters" for gscrap or noscrap to 0 to disable the requirement (default).

#### VIPSettings

For each level of VIP access you want, edit or create a copy of the default entry in the config:

```json
  "VIPSettings": {
    "myscrapheli.viplevel1": {
      "unlimited": false,
      "canloot": true,
      "stdFuelConsumption": 0.15,
      "startingFuel": 20.0,
      "cooldownmin": 120.0,
      "mindistance": 0.0,
      "gscrapdistance": 0.0
    },
    "myscrapheli.viplevel2": {
      "unlimited": true,
      "canloot": false,
      "stdFuelConsumption": 0.0,
      "startingFuel": 20.0,
      "cooldownmin": 240.0,
      "mindistance": 0.0,
      "gscrapdistance": 0.0
    }
  }
```

The setting above called myscrapheli.viplevel1 is the name of the permission to assign to give a user or group this access.

Below this are the things you can change vs. the default settings.  Overall, they should work the same as the default settings.

You can set the key for each value simply to viplevel1, maxhelis, etc.  The code will automatically add myscrapheli. to the beginning as is required.

