# MyMiniCopter
A simple plugin to spawn a mini copter for yourself. Limits to one mini copter per player with optional cooldown (using permission).

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
  "Chat Settings": {
      "Prefix": "[My MiniCopter] :"
  },
      "Cooldown (on permission)": {
          "Use Cooldown": true,
          "Value in minutes": "60"
      },
      "Global": {
          "Allow spawn when building blocked": false,
          "Allow unlimited to use fuel tank": false,
          "Destroy copter on player sleep": false,
          "Standard fuel consumption per second": 0.25
      },
      "Minimum Distance for /gmini": {
          "Value in meters": "0"
      },
      "Minimum Distance for /nomini": {
          "Value in meters": "0"
      }
}
```
Global:

- "Allow spawn when building blocked": set to true to allow player to use /mymini while building blocked
- "Allow unlimited to use fuel tank": If the player has the unlimited permissions, they will not be able to use the fuel tank if this is set.  1LGF is added so the copter will start.
- "Destroy copter on player sleep": If this is true, the player's copter will be destroyed when they disconnect.  It should check that no one is mounted to the copter.
- "Standard fuel consumption per second": How much fuel does the copter use (assuming player does NOT have the unlimited permission).  0.25/S is the default.

Set "Value in meters" for gmini or nomini to 0 to disable the requirement (default).

## Future Plans

* health workaround
* check console commands input/NRE
