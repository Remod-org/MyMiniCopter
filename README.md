# MyMiniCopter
A simple plugin to spawn a mini copter for yourself. Limits to one mini copter per player with optional cooldown (using permission).

![](https://www.remod.org/sites/default/files/inline-images/mincopter.jpg)

## Permissions

- `myminicopter.spawn` -- Allows player to spawn a mini copter (/mymini)
- `myminicopter.fetch`    -- Allows player to use /gmini retrieve their mini copter
- `myminicopter.where`    -- Allows player to use /wmini to locate their mini copter (NEW!)
- `myminicopter.admin`  -- Allows an admin to run console commands (may change)
- `myminicopter.cooldown` -- Adds a cooldown to player
- `myminicopter.unlimited` -- Player can fly without fuel usage (will need to add at least 1 LGF unless "Allow unlimited to use fuel tank" is set to false)

## Chat Commands

- `/mymini` -- Spawn a mini copter
- `/nomini` -- Despawn mini copter
- `/wmini   -- Find mini copter
- `/gmini   -- Get/fetch mini copter

## Console Commands

- `spawnminicopter <player ID>`
- `killminicopter <player ID>`

## For Developers

```csharp
(void) SpawnMyMinicopter (BasePlayer player)
(void) KillMyMinicopterPlease (BasePlayer player)
no return value;
```

## Configuration

```json
{
  "Global": {
    "allowWhenBlocked": false,
    "useCooldown": true,
    "copterDecay": false,
    "killOnSleep": false,
    "allowFuelIfUnlimited": false,
    "allowDriverDismountWhileFlying": true,
    "allowPassengerDismountWhileFlying": true,
    "stdFuelConsumption": 0.25,
    "cooldownmin": 60.0,
    "mindistance": 0.0,
    "gminidistance": 0.0,
    "minDismountHeight": 7.0,
    "startingFuel": 0.0,
    "Prefix": "[My MiniCopter] :"
  },
  "Version": {
    "Major": 0,
    "Minor": 0,
    "Patch": 0
  }
}
```
Global:

- `allowWhenBlocked` -- Set to true to allow player to use /mymini while building blocked
- `useCooldown` -- Enforce a cooldown for minutes between use of /mymini.
- `copterDecay` -- Enable decay
- `killOnSleep` -- Kill the copter when the user leaves the server
- `allowFuelIfUnlimited` -- Allow unlimited permission users to add fuel anyway.
- `allowDriverDismountWhileFlying` -- Allow the driver to dismount while flying above minDismountHeight.
- `allowPassengerDismountWhileFlying` --  Allow passenger to dismount while flying above minDismountHeight.
- `stdFuelConsumption` -- Adjust fuel consumption per second from standard amount (0.25f)
- `cooldownmin` -- Minutes to wait between usage of /mymini
- `mindistance` -- Miniumum distance to copter for using /nomini
- `gminidistance` -- Miniumum distance to copter for using /gmini
- `minDismountHeight` -- Miniumum height for dismount (for allow rules above)
- `startingFuel` -- How much fuel to start with for non-unlimited permission players (default 0)
- `Prefix` -- Prefix for chat messages (default [MyMiniCopter: ])

Set "Value in meters" for gmini or nomini to 0 to disable the requirement (default).

## Future Plans

* health workaround
* check console commands input/NRE
