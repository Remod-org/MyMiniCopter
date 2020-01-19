# MyMiniCopter
A simple plugin to spawn a mini copter for yourself. Limits to one mini copter per player with optional cooldown (using permission).

## Permissions

- `myminicopter.spawn` -- Allows player to spawn a mini copter
- `myminicopter.fetch`    -- Allows player to retrieve their mini copter
- `myminicopter.admin`  -- Allows an admin to run console commands (may change)
- `myminicopter.cooldown` -- Adds a cooldown to player
- `myminicopter.unlimited` -- Player can fly without fuel usage (will need to add at least 1 LGF unless "Allow unlimited to use fuel tank" is set to false)

## Chat Commands

- `/mymini` -- Spawn a mini copter
- `/nomini` -- Despawn mini copter
- `/wmini`   -- Find mini copter
- `/gmini`    -- Get/fetch mini copter

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

Set "Value in meters" to 0 to disable the requirement (default).

## Future Plans

* bomb drop
* health workaround
* spawner
* check console commands input/NRE
