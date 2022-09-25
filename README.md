# MyMiniCopter
A simple plugin to spawn a mini copter for yourself. Limits to one mini copter per player with optional cooldown (using permission).

![](https://www.remod.org/sites/default/files/inline-images/mincopter.jpg)

Uses: NoEscape (optional)

## Permissions

- `myminicopter.spawn` -- Allows player to spawn a mini copter (/mymini)
- `myminicopter.fetch`    -- Allows player to use /gmini retrieve their mini copter
- `myminicopter.where`    -- Allows player to use /wmini to locate their mini copter (NEW!)
- `myminicopter.admin`  -- Allows an admin to run console commands (may change)
- `myminicopter.cooldown` -- Adds a cooldown to player
- `myminicopter.unlimited` -- Player can fly without fuel usage (will need to add at least 1 LGF unless "Allow unlimited to use fuel tank" is set to false)
- `myminicopter.hover` -- Player can enable or disable hover mode

## Chat Commands

- `/mymini` -- Spawn a mini copter
- `/nomini` -- Despawn mini copter
- `/wmini`  -- Find mini copter
- `/gmini`  -- Get/fetch mini copter
- `/hmini`  -- Enable/disable hovering

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
    "allowDamage": true,
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
    "Prefix": "[My MiniCopter] :",
    "TimedHover": false,
    "DisableHoverOnDismount": true,
    "EnableRotationOnHover": true,
    "PassengerCanToggleHover": false,
    "HoverWithoutEngine": false,
    "UseFuelOnHover": true,
    "HoverDuration": 60.0,
    "UseKeystrokeForHover": false,
    "HoverKey": 134217728
  },
  "Version": {
    "Major": 0,
    "Minor": 4,
    "Patch": 3
  }
}
```
Global:

- `allowWhenBlocked` -- Set to true to allow player to use /mymini while building blocked
- `useCooldown` -- Enforce a cooldown for minutes between use of /mymini.
- `useNoEscape` -- Use the NoEscape plugin to check and prevent command use while "raid blocked" per that plugin
- `copterDecay` -- Enable decay
- `allowDamage` -- Enable/allow damage (old default)
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
- `TimedHover` -- Use a timer to limit how long a copter can remain in hover mode
- `DisableHoverOnDismount` -- Disable hover if no players are seated
- `EnableRotationOnHover` -- Allow the driver to rotate the copter while hovering
- `PassengerCanToggleHover` -- Allow the passenger to toggle hovering
- `HoverWithoutEngine` -- Hover with engine stopped
- `UseFuelOnHover` -- Use fuel while hovering
- `HoverDuration` -- How long the copter will hover if TimedHover is true
- `UseKeystrokeForHover` -- Allow use of middle mouse button (by default) to toggle hovering
- `HoverKey` -- Set the key used for hover toggling (default is middle mouse button)

Set "Value in meters" for gmini or nomini to 0 to disable the requirement (default).

### Notes on hovering

1. The player/owner must have the myminicopter.hover permission.

2. Some code was borrowed from HelicopterHover but modified for our purposes.  Essentially, the parts that maintain height and control fuel usage are from that plugin.  See the plugin code for more details.

3. Fuel usage is still disabled if the player has the unlimited permission.

4. While hovering, if UseKeystrokeForHover is enabled, the driver can click the middle mouse button to toggle hovering on and off.

5. Also, if UseKeystrokeForHover is enabled, the BACK button (S) will become the stabilzation button while hovering.  This allows the player to automatically right the minicopter.

6. If UseKeystrokeForHover is NOT enabled (false), then neither of these functions work.  To toggle hovering in that case, use the /hmini chat command.

7. If you want to use another key instead of MMB, you must use one that the game will actually send.  This is generally limited to WASD, Shift, Ctrl, and perhaps a few others.  Be careful not to interfere with other player motion commands, map, etc.

## Future Plans

* health workaround
* check console commands input/NRE
