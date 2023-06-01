#region License (GPL v2)
/*
    DESCRIPTION
    Copyright (c) 2023 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License v2.0

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v2)
#region License Notice
/*
 * Hovering class modified from code at https://umod.org/plugins/helicopter-hover,
   originally licensed under the following license:

   MIT License
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */
#endregion
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using System.Text;
using Oxide.Core.Libraries.Covalence;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("My Mini Copter", "RFC1920", "0.5.8")]
    // Thanks to BuzZ[PHOQUE], the original author of this plugin
    [Description("Spawn a Mini Helicopter")]
    internal class MyMiniCopter : RustPlugin
    {
        [PluginReference]
        private readonly Plugin NoEscape, Friends, Clans;
        public static MyMiniCopter Instance;

        private const string prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private ConfigData configData;

        private const string MinicopterSpawn = "myminicopter.spawn";
        private const string MinicopterFetch = "myminicopter.fetch";
        private const string MinicopterWhere = "myminicopter.where";
        private const string MinicopterAdmin = "myminicopter.admin";
        private const string MinicopterCooldown = "myminicopter.cooldown";
        private const string MinicopterUnlimited = "myminicopter.unlimited";
        private const string MinicopterCanHover = "myminicopter.canhover";

        private static LayerMask layerMask = LayerMask.GetMask("Terrain", "World", "Construction");

        private Dictionary<ulong, ulong> currentMounts = new Dictionary<ulong, ulong>();
        private Dictionary<int, Hovering> hovers = new Dictionary<int, Hovering>();
        private Dictionary<ulong, DateTime> hoverDelayTimers = new Dictionary<ulong, DateTime>();
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private bool do056upgrade;

        private class StoredData
        {
            public Dictionary<ulong, NetworkableId> playerminiID = new Dictionary<ulong, NetworkableId>();
            public Dictionary<ulong, double> playercounter = new Dictionary<ulong, double>();
        }
        private StoredData storedData;

        // For pre-0.5.6 upgrade
        private class OldStoredData
        {
            public Dictionary<ulong, uint> playerminiID = new Dictionary<ulong, uint>();
            public Dictionary<ulong, double> playercounter = new Dictionary<ulong, double>();
        }

        private bool HasPermission(ConsoleSystem.Arg arg, string permname)
        {
            return !(arg.Connection.player is BasePlayer) || permission.UserHasPermission((arg.Connection.player as BasePlayer)?.UserIDString, permname);
        }

        #region loadunload
        private void OnServerInitialized()
        {
            LoadConfigVariables();

            if (configData?.VIPSettings?.Count > 0)
            {
                foreach (string vipperm in configData.VIPSettings.Keys)
                {
                    string perm = vipperm.StartsWith($"{Name.ToLower()}.") ? vipperm : $"{Name.ToLower()}.{vipperm}";
                    DoLog($"Registering vip perm {perm}");
                    permission.RegisterPermission(perm, this);
                }
            }

            if (((configData.Global.cooldownmin * 60) <= 120) && configData.Global.useCooldown)
            {
                PrintError("Please set a longer cooldown time. Minimum is 2 min.");
                configData.Global.cooldownmin = 2;
                SaveConfig(configData);
            }

            LoadData();
            foreach (KeyValuePair<ulong, NetworkableId> playerMini in storedData.playerminiID)
            {
                MiniCopter miniCopter = BaseNetworkable.serverEntities.Find(playerMini.Value) as MiniCopter;
                if (miniCopter == null) continue;
                BasePlayer pl = FindPlayerById(playerMini.Key);
                if (pl == null) continue;

                VIPSettings vipsettings;
                GetVIPSettings(pl, out vipsettings);
                bool vip = vipsettings != null;

                if (permission.UserHasPermission(playerMini.Key.ToString(), MinicopterCanHover))
                {
                    hovers.Add(miniCopter.GetInstanceID(), miniCopter.gameObject.AddComponent<Hovering>());
                }

                StorageContainer fuelCan = miniCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                if (permission.UserHasPermission(playerMini.Key.ToString(), MinicopterUnlimited) || (vip && vipsettings.unlimited))
                {
                    miniCopter.fuelPerSec = 0f;
                    if (fuelCan?.IsValid() == true)
                    {
                        if (fuelCan?.inventory.IsEmpty() != true)
                        {
                            DoLog($"Setting fuel for MiniCopter {playerMini.Value} owned by {playerMini.Key}.");
                            ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelCan?.inventory);
                            fuelCan?.inventory.MarkDirty();
                        }

                        // Default for unlimited fuel
                        fuelCan?.SetFlag(BaseEntity.Flags.Locked, true);
                        if (configData.Global.allowFuelIfUnlimited || (vip && vipsettings.canloot))
                        {
                            fuelCan?.SetFlag(BaseEntity.Flags.Locked, false);
                        }
                    }
                    continue;
                }
                else
                {
                    // Done here in case player's unlimited permission was revoked.
                    fuelCan?.SetFlag(BaseEntity.Flags.Locked, false);
                }
                miniCopter.fuelPerSec = vip ? vipsettings.stdFuelConsumption : configData.Global.stdFuelConsumption;
            }
        }

        private void OnNewSave()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void Init()
        {
            Instance = this;

            AddCovalenceCommand("mymini", "SpawnMyMinicopterCommand");
            AddCovalenceCommand("nomini", "KillMyMinicopterCommand");
            AddCovalenceCommand("gmini",  "GetMyMiniMyCopterCommand");
            AddCovalenceCommand("wmini",  "WhereisMyMiniMyCopterCommand");
            AddCovalenceCommand("remini", "ReSpawnMyMinicopterCommand");
            AddCovalenceCommand("hmini",  "HoverMyMinicopterCommand");

            permission.RegisterPermission(MinicopterSpawn, this);
            permission.RegisterPermission(MinicopterFetch, this);
            permission.RegisterPermission(MinicopterWhere, this);
            permission.RegisterPermission(MinicopterAdmin, this);
            permission.RegisterPermission(MinicopterCooldown, this);
            permission.RegisterPermission(MinicopterUnlimited, this);
            permission.RegisterPermission(MinicopterCanHover, this);
        }

        private void Unload()
        {
            SaveData();
            foreach (KeyValuePair<int, Hovering> hover in hovers) UnityEngine.Object.Destroy(hover.Value);
        }
        #endregion

        #region Messages
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MyMiniHelp", "Spawn minicopter in front of you." },
                {"NoMiniHelp", "Destroy your minicopter if in range ({0} meters)." },
                {"WMiniHelp", "Find your minicopter." },
                {"GetMiniHelp", "Retrieve your minicopter." },
                {"AlreadyMsg", "You already have a mini helicopter.\nUse command '/nomini' to remove it."},
                {"SpawnedMsg", "Your mini copter has spawned !\nUse command '/nomini' to remove it."},
                {"KilledMsg", "Your mini copter has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"RaidBlockMsg", "You are not allowed to do this while raid blocked!"},
                {"SpawnUsage", "You need to supply a valid SteamId."},
                {"NoFoundMsg", "You do not have an active copter."},
                {"FoundMsg", "Your copter is located at {0}."},
                {"CooldownMsg", "You must wait {0} seconds before spawning a new mini copter."},
                {"DistanceMsg", "You must be within {0} meters of your mini copter."},
                {"FlyingMsg", "Your copter is currently flying and cannot be fetched."},
                {"RunningMsg2", "Your copter is currently running and cannot be fetched."},
                {"BlockedMsg", "You cannot spawn or fetch your copter while building blocked."},
                {"NotFlying", "The copter is not flying" },
                {"NoAccess", "You do not have permission to access this minicopter" },
                {"NoPermission", "You do not have permission to hover" },
                {"HoverEnabled", "MiniCopter hover: enabled" },
                {"HoverDisabled", "MiniCopter hover: disabled" },
                {"NotInHelicopter", "You are not in a minicopter" },
                {"NoPassengerToggle", "Passengers cannot toggle hover" }
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MyMiniHelp", "Créez un mini hélicoptère devant vous." },
                {"NoMiniHelp", "Détruisez votre mini hélicoptère si il est à portée. ({0} mètres)." },
                {"GetMiniHelp", "Récupérez votre mini hélicoptère." },
                {"AlreadyMsg", "Vous avez déjà un mini hélicoptère\nUtilisez la commande '/nomini' pour le supprimer."},
                {"SpawnedMsg", "Votre mini hélico est arrivé !\nUtilisez la commande '/nomini' pour le supprimer."},
                {"KilledMsg", "Votre mini hélico a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"RaidBlockMsg", "Vous n'êtes pas autorisé à faire cela pendant que le raid est bloqué!"},
                {"SpawnUsage", "Vous devez fournir un SteamId valide."},
                {"NoFoundMsg", "Vous n'avez pas de mini hélico actif"},
                {"FoundMsg", "Votre mini hélico est situé à {0}."},
                {"CooldownMsg", "Vous devez attendre {0} secondes avant de créer un nouveau mini hélico."},
                {"DistanceMsg", "Vous devez être à moins de {0} mètres de votre mini-hélico."},
                {"BlockedMsg", "Vous ne pouvez pas faire apparaître ou aller chercher votre hélico lorsque la construction est bloquée."},
                {"NotFlying", "L'hélicoptère ne vole pas"},
                {"NoPermission", "Vous n'êtes pas autorisé à survoler" },
                {"HoverEnabled", "Vol stationnaire mini hélicoptère: activé" },
                {"HoverDisabled", "Vol stationnaire mini hélicoptère: désactivé" },
                {"NotInHelicopter", "Vous n'êtes pas dans un mini hélicoptère" },
                {"NoPassengerToggle", "Les passagers ne peuvent pas basculer en vol stationnaire" }
            }, this, "fr");
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player?.userID.IsSteamId() != true || input == null) return;
            if (!configData.Global.UseKeystrokeForHover) return;
            if (!permission.UserHasPermission(player.UserIDString, MinicopterCanHover)) return;
            //if (input.current.buttons > 0) Puts($"OnPlayerInput: {input.current.buttons}");
            if (!player.isMounted) return;
            int hoverkey = configData.Global.HoverKey > 0 ? configData.Global.HoverKey : (int)BUTTON.FIRE_THIRD; // MMB
            bool dohover = input.current.buttons == hoverkey;
            bool stabilize = input.current.buttons == (int)BUTTON.BACKWARD;

            if (!(dohover || stabilize)) return;
            BaseHelicopterVehicle mini = player.GetMountedVehicle() as BaseHelicopterVehicle;
            if (mini == null) return;

            DoLog($"Stabilize: {stabilize}, hover toggle: {dohover}");
            DoLog($"HoverDelay: {hoverDelayTimers.ContainsKey(player.userID)}");
            // Process hoverDelayTimers for user regardless of hover or stabilize.
            //   If trying to hover and timer not expired, return.
            //   If timer expired, remove timer and continue.
            if (hoverDelayTimers.ContainsKey(player.userID))
            {
                if (DateTime.Now - hoverDelayTimers[player.userID] < TimeSpan.FromMilliseconds(1000) && dohover)
                {
                    DoLog("Hover delay not elapsed, returning");
                    return;
                }
                hoverDelayTimers.Remove(player.userID);
            }

            // Now, if trying to hover, setup a new delay timer for the next keystroke.
            if (dohover)
            {
                DoLog("Resetting hover delay timer");
                hoverDelayTimers.Remove(player.userID);
                hoverDelayTimers.Add(player.userID, DateTime.Now);
            }

            // Process hover or stablize
            if (storedData.playerminiID.ContainsKey(player.userID) && mini?.net.ID.Value == storedData.playerminiID[player.userID].Value)
            {
                if (dohover && player != mini?.GetDriver() && !configData.Global.PassengerCanToggleHover)
                {
                    Message(player.IPlayer, "NoPassengerToggle");
                    return;
                }

                if (mini.IsEngineOn() && mini.GetDriver())
                {
                    int iid = mini.GetInstanceID();
                    DoLog($"Hovers contains {iid}: {hovers.ContainsKey(iid)}");
                    if (stabilize && hovers.ContainsKey(iid) && hovers[iid].isHovering)
                    {
                        DoLog($"Stabilizing {mini.net.ID}");
                        hovers[iid]?.Stabilize();
                    }
                    else if (dohover && hovers.ContainsKey(iid))
                    {
                        DoLog($"Toggling hover for {mini.net.ID}");
                        hovers[iid]?.ToggleHover();
                    }
                }
            }
        }

        private string Lang(string key, string id = default(string), params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        // Chat message to online player with ulong
        private void ChatPlayerOnline(ulong userid, string message)
        {
            BasePlayer player = BasePlayer.FindByID(userid);
            if (player != null)
            {
                SendReply(player, Lang("KilledMsg"));
            }
        }
        #endregion

        #region Commands
        // Chat spawn
        [Command("mymini")]
        private void SpawnMyMinicopterCommand(IPlayer player, string command, string[] args)
        {
            double secondsSinceEpoch = DateTime.UtcNow.Subtract(epoch).TotalSeconds;
            BasePlayer bplayer = player.Object as BasePlayer;

            if (!player.HasPermission(MinicopterSpawn))
            {
                Message(player, "NoPermMsg");
                return;
            }
            if (IsRaidBlocked(bplayer))
            {
                Message(player, "RaidBlockMsg");
                return;
            }

            if (storedData.playerminiID.ContainsKey(bplayer.userID))
            {
                if (!configData.Global.allowRespawnWhenActive)
                {
                    Message(player, "AlreadyMsg");
                    return;
                }
                KillMyMinicopterPlease(bplayer, true);
            }

            if (bplayer.IsBuildingBlocked() && !configData.Global.allowWhenBlocked)
            {
                Message(player, "BlockedMsg");
                return;
            }

            bool hascooldown = player.HasPermission(MinicopterCooldown);
            if (!configData.Global.useCooldown) hascooldown = false;

            int secsleft;
            if (hascooldown)
            {
                VIPSettings vipsettings;
                GetVIPSettings(player.Object as BasePlayer, out vipsettings);
                float cooldownMin = vipsettings != null ? vipsettings.cooldownmin : configData.Global.cooldownmin;

                if (!storedData.playercounter.ContainsKey(bplayer.userID))
                {
                    storedData.playercounter.Add(bplayer.userID, secondsSinceEpoch);
                    SaveData();
                }
                else
                {
                    double count;
                    storedData.playercounter.TryGetValue(bplayer.userID, out count);

                    if ((secondsSinceEpoch - count) > (cooldownMin * 60))
                    {
                        DoLog("Player reached cooldown.  Clearing data.");
                        storedData.playercounter.Remove(bplayer.userID);
                        SaveData();
                    }
                    else
                    {
                        secsleft = Math.Abs((int)((cooldownMin * 60) - (secondsSinceEpoch - count)));

                        if (secsleft > 0)
                        {
                            DoLog($"Player DID NOT reach cooldown. Still {secsleft} secs left.");
                            Message(player, "CooldownMsg", secsleft.ToString());
                            return;
                        }
                    }
                }
            }
            else
            {
                if (storedData.playercounter.ContainsKey(bplayer.userID))
                {
                    storedData.playercounter.Remove(bplayer.userID);
                    SaveData();
                }
            }
            SpawnMyMinicopter(bplayer);
        }

        // Fetch copter
        [Command("gmini")]
        private void GetMyMiniMyCopterCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer bplayer = player.Object as BasePlayer;
            if (bplayer.IsBuildingBlocked() && !configData.Global.allowWhenBlocked)
            {
                Message(player, "BlockedMsg");
                return;
            }

            bool canspawn = player.HasPermission(MinicopterSpawn);
            bool canfetch = player.HasPermission(MinicopterFetch);
            if (!(canspawn && canfetch))
            {
                Message(player, "NoPermMsg");
                return;
            }
            if (IsRaidBlocked(bplayer))
            {
                Message(player, "RaidBlockMsg");
                return;
            }

            VIPSettings vipsettings;
            GetVIPSettings(player.Object as BasePlayer, out vipsettings);
            bool vip = vipsettings != null;

            if (storedData.playerminiID.ContainsKey(bplayer.userID))
            {
                NetworkableId findme;
                storedData.playerminiID.TryGetValue(bplayer.userID, out findme);
                BaseNetworkable foundent = BaseNetworkable.serverEntities.Find(findme);
                if (foundent != null)
                {
                    // Distance check - need a Y check as well... maybe.
                    float gminiDistance = vip ? vipsettings.gminidistance : configData.Global.gminidistance;
                    if (gminiDistance > 0f && Vector3.Distance(bplayer.transform.position, foundent.transform.position) > gminiDistance)
                    {
                        Message(player, "DistanceMsg", gminiDistance);
                        return;
                    }

                    MiniCopter copter = foundent as MiniCopter;
                    float terrainHeight = TerrainMeta.HeightMap.GetHeight(foundent.transform.position);
                    if (copter.engineController.IsOn)
                    {
                        if (!configData.Global.StopEngineOnGMini)
                        {
                            Message(player, "RunningMsg2");
                            return;
                        }
                        copter.engineController.StopEngine();
                    }

                    // Check for and dismount all players before moving the copter
                    foreach (BaseVehicle.MountPointInfo mountPointInfo in copter.mountPoints)
                    {
                        if (mountPointInfo.mountable != null)
                        {
                            BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                            if (mounted)
                            {
                                if (mounted.transform.position.y - terrainHeight > 10f)
                                {
                                    Message(player, "FlyingMsg");
                                    return;
                                }

                                Vector3 player_pos = mounted.transform.position + new Vector3(1, 0, 1);
                                mounted.DismountObject();
                                mounted.MovePosition(player_pos);
                                mounted.SendNetworkUpdateImmediate(false);
                                mounted.ClientRPCPlayer(null, bplayer, "ForcePositionTo", player_pos);
                                mountPointInfo.mountable._mounted = null;
                            }
                        }
                    }
                    Vector3 newLoc = new Vector3(bplayer.transform.position.x + 2f, bplayer.transform.position.y + 2f, bplayer.transform.position.z + 2f);
                    foundent.transform.position = newLoc;
                    Message(player, "FoundMsg", newLoc);
                }
            }
            else
            {
                Message(player, "NoFoundMsg");
            }
        }

        // Find copter
        [Command("wmini")]
        private void WhereisMyMiniMyCopterCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MinicopterWhere))
            {
                Message(player, "NoPermMsg");
                return;
            }
            BasePlayer bplayer = player.Object as BasePlayer;
            if (storedData.playerminiID.ContainsKey(bplayer.userID))
            {
                NetworkableId findme;
                storedData.playerminiID.TryGetValue(bplayer.userID, out findme);
                BaseNetworkable foundit = BaseNetworkable.serverEntities.Find(findme);
                if (foundit != null)
                {
                    string loc = foundit.transform.position.ToString();
                    Message(player, "FoundMsg", loc);
                }
            }
            else
            {
                Message(player, "NoFoundMsg");
            }
        }

        [Command("hmini")]
        private void HoverMyMinicopterCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MinicopterCanHover))
            {
                Message(player, "NoPermMsg");
                return;
            }
            ulong playerId = ulong.Parse(player.Id);
            if (hoverDelayTimers.ContainsKey(playerId))
            {
                if (DateTime.Now - hoverDelayTimers[playerId] < TimeSpan.FromMilliseconds(1000))
                {
                    return;
                }
                hoverDelayTimers.Remove(playerId);
            }
            hoverDelayTimers.Add(playerId, DateTime.Now);

            BaseHelicopterVehicle mini = (player.Object as BasePlayer)?.GetMountedVehicle() as BaseHelicopterVehicle;
            if (mini == null) return;
            if (storedData.playerminiID.ContainsKey(playerId) && mini.net.ID.Value == storedData.playerminiID[playerId].Value)
            {
                if ((player.Object as BasePlayer) != mini.GetDriver() && !configData.Global.PassengerCanToggleHover)
                {
                    Message(player, "NoPassengerToggle");
                    return;
                }

                if (mini.IsEngineOn() && mini.GetDriver())
                {
                    DoLog($"Finding hover object for {mini.net.ID}");
                    hovers[mini.GetInstanceID()]?.ToggleHover();
                }
            }
        }

        // Chat despawn
        [Command("remini")]
        private void ReSpawnMyMinicopterCommand(IPlayer player, string command, string[] args)
        {
            if (IsRaidBlocked(player.Object as BasePlayer))
            {
                Message(player, "RaidBlockMsg");
                return;
            }
            KillMyMinicopterCommand(player, "nomini", new string[0]);
            SpawnMyMinicopterCommand(player, "mymini", new string[0]);
        }

        [Command("nomini")]
        private void KillMyMinicopterCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MinicopterSpawn))
            {
                Message(player, "NoPermMsg");
                return;
            }
            if (IsRaidBlocked(player.Object as BasePlayer))
            {
                Message(player, "RaidBlockMsg");
                return;
            }
            KillMyMinicopterPlease(player.Object as BasePlayer);
        }
        #endregion

        #region consolecommands
        // Console spawn
        [ConsoleCommand("spawnminicopter")]
        private void SpawnMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                if (arg.Args == null)
                {
                    DoLog("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if (!HasPermission(arg, MinicopterAdmin))
            {
                SendReply(arg.Connection.player as BasePlayer, Lang("NoPermMsg"));
                return;
            }
            else if (arg.Args == null)
            {
                SendReply(arg.Connection.player as BasePlayer, Lang("SpawnUsage"));
                return;
            }

            if (arg.Args.Length == 1)
            {
                ulong steamid = Convert.ToUInt64(arg.Args[0]);
                if (steamid == 0) return;
                if (!steamid.IsSteamId()) return;
                BasePlayer player = BasePlayer.FindByID(steamid);
                if (player != null)
                {
                    SpawnMyMinicopter(player);
                }
            }
        }

        // Console despawn
        [ConsoleCommand("killminicopter")]
        private void KillMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                if (arg.Args == null)
                {
                    DoLog("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if (!HasPermission(arg, MinicopterAdmin))
            {
                SendReply(arg.Connection.player as BasePlayer, Lang("NoPermMsg"));
                return;
            }
            else if (arg.Args == null)
            {
                SendReply(arg.Connection.player as BasePlayer, Lang("SpawnUsage"));
                return;
            }

            if (arg.Args.Length == 1)
            {
                ulong steamid = Convert.ToUInt64(arg.Args[0]);
                if (steamid == 0) return;
                if (!steamid.IsSteamId()) return;
                BasePlayer player = BasePlayer.FindByID(steamid);
                if (player != null)
                {
                    KillMyMinicopterPlease(player);
                }
            }
        }
        #endregion

        #region ourhooks
        // Spawn hook
        private void SpawnMyMinicopter(BasePlayer player)
        {
            if (player.IsBuildingBlocked() && !configData.Global.allowWhenBlocked)
            {
                SendReply(player, Lang("BlockedMsg"));
                return;
            }

            VIPSettings vipsettings;
            GetVIPSettings(player, out vipsettings);
            bool vip = vipsettings != null;

            Quaternion rotation = player.GetNetworkRotation();
            Vector3 forward = rotation * Vector3.forward;
            // Make straight perpendicular to up axis so we don't spawn into ground or above player's head.
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 position = player.transform.position + (straight * 5f);
            position.y = player.transform.position.y + 2.5f;

            if (position == default(Vector3)) return;
            BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleMini == null) return;
            vehicleMini.OwnerID = player.userID;

            MiniCopter miniCopter = vehicleMini as MiniCopter;

            vehicleMini.Spawn();
            if (permission.UserHasPermission(player.UserIDString, MinicopterCanHover))
            {
                hovers.Add(miniCopter.GetInstanceID(), miniCopter.gameObject.AddComponent<Hovering>());
            }
            if (permission.UserHasPermission(player.UserIDString, MinicopterUnlimited) || (vip && vipsettings.unlimited))
            {
                // Set fuel requirements to 0
                DoLog("Setting fuel requirements to zero");
                miniCopter.fuelPerSec = 0f;
                if (!configData.Global.allowFuelIfUnlimited && !(vip && vipsettings.canloot))
                {
                    // If the player is not allowed to use the fuel container, add 1 fuel so the copter will start.
                    // Also lock fuel container since there is no point in adding/removing fuel
                    StorageContainer fuelCan = miniCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                    if (fuelCan?.IsValid() == true)
                    {
                        ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelCan.inventory);
                        fuelCan.inventory.MarkDirty();
                        fuelCan.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
            }
            else if (configData.Global.startingFuel > 0 || (vip && vipsettings.startingFuel > 0))
            {
                StorageContainer fuelCan = miniCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                if (fuelCan?.IsValid() == true)
                {
                    float sf = vip ? vipsettings.startingFuel : configData.Global.startingFuel;
                    ItemManager.CreateByItemID(-946369541, Convert.ToInt32(sf))?.MoveToContainer(fuelCan.inventory);
                    fuelCan.inventory.MarkDirty();
                }
            }
            else
            {
                miniCopter.fuelPerSec = vip ? vipsettings.stdFuelConsumption : configData.Global.stdFuelConsumption;
            }

            SendReply(player, Lang("SpawnedMsg"));
            NetworkableId minicopteruint = vehicleMini.net.ID;
            DoLog($"SPAWNED MINICOPTER {minicopteruint} for player {player?.displayName} OWNER {vehicleMini?.OwnerID}");
            storedData.playerminiID.Remove(player.userID);
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
            storedData.playerminiID.Add(player.userID, minicopteruint);
            SaveData();

            miniCopter = null;
        }

        // Kill minicopter hook
        private void KillMyMinicopterPlease(BasePlayer player, bool killalways=false)
        {
            bool foundcopter = false;
            VIPSettings vipsettings;
            GetVIPSettings(player, out vipsettings);
            float minDistance = vipsettings != null ? vipsettings.mindistance : configData.Global.mindistance;

            if (minDistance == 0f || killalways)
            {
                foundcopter = true;
            }
            else
            {
                List<BaseEntity> copterlist = new List<BaseEntity>();
                Vis.Entities(player.transform.position, minDistance, copterlist);

                foreach (BaseEntity p in copterlist)
                {
                    MiniCopter foundent = p.GetComponentInParent<MiniCopter>();
                    if (foundent != null)
                    {
                        foundcopter = true;
                    }
                }
            }

            if (storedData.playerminiID.ContainsKey(player.userID) && foundcopter)
            {
                NetworkableId findPlayerId;
                storedData.playerminiID.TryGetValue(player.userID, out findPlayerId);
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(findPlayerId);
                tokill?.Kill(BaseNetworkable.DestroyMode.Gib);
                storedData.playerminiID.Remove(player.userID);
                ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                currentMounts.Remove(myKey);

                if (storedData.playercounter.ContainsKey(player.userID) && !configData.Global.useCooldown)
                {
                    storedData.playercounter.Remove(player.userID);
                }
                SaveData();
                LoadData();
            }
            else if (!foundcopter)
            {
                DoLog("Player too far from copter to destroy.");
                SendReply(player, Lang("DistanceMsg", null, minDistance));
            }
        }

        private bool IsRaidBlocked(BasePlayer player)
        {
            if (configData.Global.useNoEscape && NoEscape)
            {
                return (bool) NoEscape?.CallHook("IsRaidBlocked", player);
            }
            return false;
        }
        #endregion

        #region hooks
        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (mountable == null) return null;
            MiniCopter mini = mountable?.GetComponentInParent<MiniCopter>();
            if (mini == null) return null;

            DoLog($"CanMountEntity: Player {player?.userID} wants to mount seat id {mountable?.net.ID}");
            NetworkableId currentseat = new NetworkableId(mini.net.ID.Value);
            currentseat.Value += 3; // Start with driver seat
            for (int i = 0; i < 2; i++)
            {
                // Find copter and seats in storedData
                DoLog($"  Is this our copter with ID {mini.net.ID.Value}?");
                if (storedData.playerminiID.ContainsValue(mini.net.ID))
                {
                    DoLog("    yes, it is...");
                    if (player?.userID.IsSteamId() != true) return true; // Block mounting by NPCs
                    BaseVehicle minimount = BaseNetworkable.serverEntities.Find(mini.net.ID) as BaseVehicle;
                    DoLog($"Does {player.userID} match {minimount?.OwnerID}, or are they a friend?");
                    if (!IsFriend(player.userID, minimount.OwnerID))
                    {
                        DoLog("Player does not own minicopter, and is not a friend of the owner.");
                        Message(player.IPlayer, "NoAccess");
                        return false;
                    }

                    if (currentMounts.ContainsValue(player.userID))
                    {
                        if (!player.GetMounted())
                        {
                            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                            currentMounts.Remove(myKey);
                        }
                        return false;
                    }
                    break;
                }
                currentseat.Value++;
            }
            return null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                DoLog($"OnEntityMounted: Player {player.userID} mounted seat id {mountable.net.ID}");
                // Check this seat's ID to see if the copter is one of ours
                NetworkableId currentseat = new NetworkableId(mini.net.ID.Value);
                currentseat.Value += 3; // Start with driver seat
                for (int i = 0; i < 2; i++)
                {
                    // Find copter in storedData
                    DoLog($"Is this our copter with ID {mini.net.ID.Value}?");
                    if (storedData.playerminiID.ContainsValue(mini.net.ID))
                    {
                        DoLog("    yes, it is...");
                        DoLog($"Removing {player.displayName}'s ID {player.userID} from currentMounts for seat {mountable.net.ID} on {currentseat.Value}");
                        currentMounts.Remove(mountable.net.ID.Value);
                        DoLog($"Adding {player.displayName}'s ID {player.userID} to currentMounts for seat {mountable.net.ID} on {currentseat.Value}");
                        currentMounts.Add(mountable.net.ID.Value, player.userID);
                        break;
                    }
                    currentseat.Value++;
                }
            }
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player?.userID.IsSteamId() != true) return null;
            MiniCopter mini = mountable?.GetComponentInParent<MiniCopter>();
            DoLog($"CanDismountEntity: Player {player.userID} wants to dismount seat id {mountable.net.ID}");

            // Only operates if mini is not null and if we are flying above minimum height
            if (mini != null && !Physics.Raycast(new Ray(mountable.transform.position, Vector3.down), configData.Global.minDismountHeight, layerMask))
            {
                DoLog($"Is this our copter with ID {mini.net.ID.Value}?");
                NetworkableId passenger = new NetworkableId(mini.net.ID.Value);
                passenger.Value += 4;
                NetworkableId driver = new NetworkableId(mini.net.ID.Value);
                driver.Value += 3;
                if (storedData.playerminiID.ContainsValue(mini.net.ID))
                {
                    DoLog("    yes, it is...");
                    if (!configData.Global.allowDriverDismountWhileFlying)
                    {
                        DoLog("DENY PILOT DISMOUNT");
                        return false;
                    }
                    ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                    currentMounts.Remove(myKey);
                }
                else if (storedData.playerminiID.ContainsValue(passenger))
                {
                    if (!configData.Global.allowPassengerDismountWhileFlying)
                    {
                        DoLog("DENY PASSENGER DISMOUNT");
                        return false;
                    }
                    ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                    currentMounts.Remove(myKey);
                }
            }
            return null;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                DoLog($"OnEntityDismounted: Player {player.userID} dismounted seat id {mountable.net.ID}");
                NetworkableId currentseat = new NetworkableId(mini.net.ID.Value);
                currentseat.Value += 3; // Start with driver seat
                for (int i = 0; i < 2; i++)
                {
                    // Find copter and seats in storedData
                    DoLog($"Is this our copter with ID {mini.net.ID.Value}?");
                    if (storedData.playerminiID.ContainsValue(mini.net.ID))
                    {
                        DoLog("    yes, it is...");
                        DoLog($"Removing {player.displayName}'s ID {player.userID} from currentMounts for seat {mountable.net.ID} on {currentseat.Value}");
                        currentMounts.Remove(mountable.net.ID.Value);
                        break;
                    }
                    currentseat.Value++;
                }
            }
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
        }

        // On kill - tell owner
        private void OnEntityKill(MiniCopter entity)
        {
            if (entity == null) return;
            if (entity.net.ID.Value == 0) return;

            if (storedData == null) return;
            if (storedData.playerminiID == null) return;
            ulong todelete = new ulong();

            if (!storedData.playerminiID.ContainsValue(entity.net.ID))
            {
                DoLog("KILLED non-plugin minicopter");
                return;
            }
            foreach (KeyValuePair<ulong, NetworkableId> item in storedData.playerminiID)
            {
                if (item.Value == entity.net.ID)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    BasePlayer player = BasePlayer.FindByID(item.Key);
                    todelete = item.Key;
                }
            }
            if (todelete != 0)
            {
                storedData.playerminiID.Remove(todelete);
                currentMounts.Remove(entity.net.ID.Value);
                currentMounts.Remove(entity.net.ID.Value + 1);
                currentMounts.Remove(entity.net.ID.Value + 2);
                hovers.Remove(entity.GetInstanceID());
                SaveData();
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity?.net?.ID == null) return null;
            if (hitInfo?.damageTypes == null) return null;

            if (storedData?.playerminiID?.ContainsValue(entity.net.ID) == true)
            {
                if (hitInfo?.damageTypes?.GetMajorityDamageType().ToString() == "Decay")
                {
                    if (configData.Global.copterDecay)
                    {
                        DoLog($"Enabling standard decay for spawned minicopter {entity.net.ID}.");
                    }
                    else
                    {
                        DoLog($"Disabling decay for spawned minicopter {entity.net.ID}.");
                        hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
                    }
                    return null;
                }
                else
                {
                    if (!configData.Global.allowDamage) return true;

                    foreach (KeyValuePair<string, VIPSettings> vip in configData.VIPSettings)
                    {
                        string perm = vip.Key.StartsWith($"{Name.ToLower()}.") ? vip.Key : $"{Name.ToLower()}.{vip.Key}";
                        if (permission.UserHasPermission(entity.OwnerID.ToString(), perm) && vip.Value is VIPSettings && !vip.Value.allowDamage)
                        {
                            return true;
                        }
                    }
                }

            }
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!configData.Global.killOnSleep) return;
            if (player?.userID.IsSteamId() != true) return;

            if (storedData.playerminiID.ContainsKey(player.userID))
            {
                NetworkableId findMiniId;
                storedData.playerminiID.TryGetValue(player.userID, out findMiniId);
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(findMiniId);
                if (tokill == null) return; // Didn't find it

                // Check for mounted players
                BaseVehicle copter = tokill as BaseVehicle;
                for (int i = 0; i < copter?.mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = copter.mountPoints[i];
                    if (mountPointInfo.mountable != null)
                    {
                        BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                        if (mounted)
                        {
                            DoLog("Copter owner sleeping but another one is mounted - cannot destroy copter");
                            return;
                        }
                    }
                }
                DoLog("Copter owner sleeping - destroying copter");
                tokill.Kill();
                storedData.playerminiID.Remove(player.userID);
                ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                currentMounts.Remove(myKey);

                if (storedData.playercounter.ContainsKey(player.userID) && !configData.Global.useCooldown)
                {
                    storedData.playercounter.Remove(player.userID);
                }
                SaveData();
            }
        }
        #endregion

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>").Append(Name).Append(' ').Append(Version).Append("</color> · Spawn a Mini Helicopter\n");
            sb.Append("  · ").Append("/mymini: ").AppendLine(Lang("MyMiniHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/nomini: ").AppendLine(Lang("NoMiniHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/wmini: ").AppendLine(Lang("WMiniHelp"));

            if (permission.UserHasPermission(player.UserIDString, MinicopterFetch))
            {
                sb.Append("  · ").Append("/gmini: ").AppendLine(Lang("GetMiniHelp"));
            }
            player.ChatMessage(sb.ToString());
        }

        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (!configData.Global.useFriends && !configData.Global.useClans && !configData.Global.useTeams) return true;
            if (playerid == ownerid) return true;

            if (configData.Global.useFriends && Friends != null)
            {
                object fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if (configData.Global.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    return true;
                }
            }
            if (configData.Global.useTeams)
            {
                RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerid);
                if (playerTeam?.members.Contains(ownerid) == true)
                {
                    return true;
                }
            }
            return false;
        }

        private void GetVIPSettings(BasePlayer player, out VIPSettings vipsettings)
        {
            if (player?.userID.IsSteamId() != true)
            {
                DoLog("User has no VIP settings");
                vipsettings = null;
                return;
            }
            foreach (KeyValuePair<string, VIPSettings> vip in configData.VIPSettings)
            {
                string perm = vip.Key.StartsWith($"{Name.ToLower()}.") ? vip.Key : $"{Name.ToLower()}.{vip.Key}";
                if (permission.UserHasPermission(player.UserIDString, perm) && vip.Value is VIPSettings)
                {
                    DoLog($"User has VIP setting {perm}");
                    vipsettings = vip.Value;
                    return; // No need to keep trying
                }
            }
            vipsettings = null;
        }

        private static BasePlayer FindPlayerById(ulong userid)
        {
            foreach (BasePlayer current in BasePlayer.allPlayerList)
            {
                if (current.userID == userid)
                {
                    return current;
                }
            }
            return null;
        }

        private void DoLog(string message)
        {
            if (configData.Global.debug) Puts(message);
        }
        #region config
        public class Global
        {
            public bool allowWhenBlocked;
            public bool allowRespawnWhenActive;
            public bool useCooldown;
            public bool useNoEscape;
            public bool useFriends;
            public bool useClans;
            public bool useTeams;
            public bool copterDecay;
            public bool allowDamage;
            public bool killOnSleep;
            public bool allowFuelIfUnlimited;
            public bool allowDriverDismountWhileFlying;
            public bool allowPassengerDismountWhileFlying;
            public bool debug;
            public bool StopEngineOnGMini;
            public float stdFuelConsumption;
            public float cooldownmin;
            public float mindistance;
            public float gminidistance;
            public float minDismountHeight;
            public float startingFuel;
            public string Prefix; // Chat prefix
            public bool TimedHover;
            public bool DisableHoverOnDismount;
            public bool EnableRotationOnHover;
            public bool PassengerCanToggleHover;
            public bool HoverWithoutEngine;
            public bool UseFuelOnHover;
            public float HoverDuration;
            public bool UseKeystrokeForHover;
            public int HoverKey;
        }

        public class VIPSettings
        {
            public bool unlimited;
            public bool canloot;
            public bool allowDamage;
            public float stdFuelConsumption;
            public float startingFuel;
            public float cooldownmin;
            public float mindistance;
            public float gminidistance;
        }

        public class ConfigData
        {
            public Global Global;
            public Dictionary<string, VIPSettings> VIPSettings { get; set; }
            public VersionNumber Version;
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(0, 3, 7))
            {
                configData.Global.allowRespawnWhenActive = false;
            }

            if (configData.Version < new VersionNumber(0, 4, 0))
            {
                configData.Global.allowDamage = true;
            }

            if (configData.Version < new VersionNumber(0, 4, 3))
            {
                configData.Global.useNoEscape = false;
                configData.Global.EnableRotationOnHover = true;
                configData.Global.DisableHoverOnDismount = true;
                configData.Global.TimedHover = false;
                configData.Global.HoverDuration = 60;
                configData.Global.UseFuelOnHover = true;
                configData.Global.PassengerCanToggleHover = false;
                configData.Global.HoverWithoutEngine = false;
                configData.Global.UseKeystrokeForHover = false;
                configData.Global.HoverKey = 134217728; // MMB / BUTTON.FIRE_THIRD
            }

            if (configData.Version < new VersionNumber(0, 4, 7))
            {
                configData.VIPSettings = new Dictionary<string, VIPSettings>
                {
                    {
                        "myminicopter.viplevel1", new VIPSettings()
                        {
                            startingFuel = 20,
                            canloot = true,
                            allowDamage = true,
                            stdFuelConsumption = 0.15f,
                            cooldownmin = 120f
                        }
                    }
                };
            }

            if (configData.VIPSettings == null)
            {
                configData.VIPSettings = new Dictionary<string, VIPSettings>();
            }

            if (configData.Version < new VersionNumber(0, 5, 3))
            {
                foreach (KeyValuePair<string, VIPSettings> vip in configData.VIPSettings)
                {
                    vip.Value.allowDamage = true;
                }
            }

            if (configData.Version < new VersionNumber(0, 5, 6))
            {
                do056upgrade = true;
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                Global = new Global()
                {
                    allowWhenBlocked = false,
                    allowRespawnWhenActive = false,
                    useCooldown = true,
                    copterDecay = false,
                    allowDamage = true,
                    killOnSleep = false,
                    allowFuelIfUnlimited = false,
                    allowDriverDismountWhileFlying = true,
                    allowPassengerDismountWhileFlying = true,
                    stdFuelConsumption = 0.25f,
                    cooldownmin = 60f,
                    mindistance = 0f,
                    gminidistance = 0f,
                    minDismountHeight = 7f,
                    startingFuel = 0f,
                    debug = false,
                    Prefix = "[My MiniCopter]: ",
                    useNoEscape = false,
                    EnableRotationOnHover = true,
                    DisableHoverOnDismount = true,
                    PassengerCanToggleHover = false,
                    HoverWithoutEngine = false,
                    UseFuelOnHover = true,
                    TimedHover = false,
                    HoverDuration = 60,
                    UseKeystrokeForHover = false,
                    HoverKey = 134217728
                },
                Version = Version
            };
            SaveConfig(config);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private void SaveData()
        {
            // Save the data file as we add/remove minicopters.
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void LoadData()
        {
            if (do056upgrade)
            {
                storedData = new StoredData();
                OldStoredData oldminiData = Interface.Oxide.DataFileSystem.ReadObject<OldStoredData>(Name);
                if (oldminiData != null)
                {
                    foreach (KeyValuePair<ulong, uint> playerMini in oldminiData.playerminiID)
                    {
                        NetworkableId ni = new NetworkableId()
                        {
                            Value = playerMini.Value
                        };
                        storedData.playerminiID.Add(playerMini.Key, ni);
                    }
                    storedData.playercounter = oldminiData.playercounter;
                    SaveData();

                    do056upgrade = false;
                }
            }

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (storedData == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }
        #endregion

        #region Hover
        private class Hovering : MonoBehaviour
        {
            // Portions borrowed from HelicopterHover plugin but modified
            private BaseHelicopterVehicle _helicopter;
            MiniCopter _minicopter;
            Rigidbody _rb;

            Timer _timedHoverTimer;
            Timer _fuelUseTimer;

            Coroutine _hoverCoroutine;
            VehicleEngineController<MiniCopter> _engineController;

            public bool isHovering => _rb.constraints == RigidbodyConstraints.FreezePositionY;

            public void Awake()
            {
                if (!TryGetComponent(out _helicopter))
                {
                    Instance.DoLog("Failed to get BHV component for MyMiniCopter");
                    Instance.hovers.Remove(_helicopter.GetInstanceID());
                    DestroyImmediate(this);
                    return;
                }
                if (!TryGetComponent(out _rb))
                {
                    Instance.DoLog("Failed to get RB component for MyMiniCopter");
                    Instance.hovers.Remove(_helicopter.GetInstanceID());
                    DestroyImmediate(this);
                    return;
                }
                _minicopter = GetComponent<MiniCopter>();
                _engineController = _minicopter?.engineController;
            }

            public void ToggleHover()
            {
                Instance.DoLog("ToggleHover");
                if (isHovering) StopHover();
                else StartHover();

                foreach (BaseVehicle.MountPointInfo info in _helicopter.mountPoints)
                {
                    BasePlayer player = info.mountable.GetMounted();
                    if (player != null) Instance.PrintToChat(player, Instance.lang.GetMessage(isHovering ? "HoverEnabled" : "HoverDisabled", Instance, player.UserIDString));
                }
            }

            public void StartHover()
            {
                Instance.DoLog("StartHover");
                _rb.constraints = RigidbodyConstraints.FreezePositionY;
                Instance.DoLog("Setting Freeze Rotation");
                if (!Instance.configData.Global.EnableRotationOnHover) _rb.freezeRotation = true;

                Instance.DoLog("Finishing Engine Start");
                _engineController?.FinishStartingEngine();

                Instance.DoLog("Starting Hover Coroutine");
                if (_helicopter != null) _hoverCoroutine = ServerMgr.Instance.StartCoroutine(HoveringCoroutine());
            }

            public void StopHover()
            {
                Instance.DoLog("StopHover");
                _rb.constraints = RigidbodyConstraints.None;
                Instance.DoLog("Disabling Freeze Rotation");
                _rb.freezeRotation = false;

                Instance.DoLog("Stopping Hover Coroutine");
                if (_hoverCoroutine != null) ServerMgr.Instance.StopCoroutine(_hoverCoroutine);
                if (_timedHoverTimer != null) _timedHoverTimer.Destroy();
                if (_fuelUseTimer != null) _fuelUseTimer.Destroy();
            }

            IEnumerator HoveringCoroutine() //Keep engine running and manage fuel
            {
                if (Instance.configData.Global.TimedHover) _timedHoverTimer = Instance.timer.Once(Instance.configData.Global.HoverDuration, () => StopHover());

                EntityFuelSystem fuelSystem = _minicopter?.GetFuelSystem();
                /* Using GetDriver, the engine will begin stalling and then die in a few seconds if the playerowner moves to the passenger seat.
                 * - The engine stops mid-air, which is not realistic.
                 * - The playerowner can move back and the engine should start again.
                 * Using GetMounted, the engine also stops mid-air.
                 * - The playerowner can move back and restart the engine.
                 * Can optionally just kill the hover if the engine stops for any reason - see FixedUpdate.
                 */
                BasePlayer player = _helicopter.GetDriver();

                if (fuelSystem != null && !Instance.permission.UserHasPermission(player.UserIDString, "minicopter.unlimited"))
                {
                    if (Instance.configData.Global.UseFuelOnHover) _fuelUseTimer = Instance.timer.Every(1f, () =>
                    {
                        if (fuelSystem.HasFuel() && _minicopter.GetDriver() == null) fuelSystem.TryUseFuel(1f, _minicopter.fuelPerSec);
                        else if (!fuelSystem.HasFuel()) _fuelUseTimer.Destroy();
                    });
                }

                //Keep engine on
                while (isHovering)
                {
                    if (!(_engineController?.IsOn ?? false) && (_helicopter.AnyMounted() || !Instance.configData.Global.DisableHoverOnDismount)) _engineController?.FinishStartingEngine();

                    if (fuelSystem != null)
                    {
                        if (!fuelSystem.HasFuel() && !Instance.permission.UserHasPermission(player.UserIDString, "minicopter.unlimited")) //If no fuel, stop hovering
                        {
                            StopHover();
                            _engineController?.StopEngine();

                            yield break;
                        }
                    }

                    yield return null;
                }
            }

            public void Stabilize()
            {
                if (!isHovering) return;
                Instance.DoLog("Fixing rotation to stabilize position");
                Quaternion q = Quaternion.FromToRotation(_minicopter.transform.up, Vector3.up) * _minicopter.transform.rotation;
                _minicopter.transform.rotation = Quaternion.Slerp(_minicopter.transform.rotation, q, Time.deltaTime * 3.5f);
            }

            private void FixedUpdate()
            {
                bool found = false;
                foreach (BaseVehicle.MountPointInfo info in _helicopter.mountPoints)
                {
                    if (info.mountable.GetMounted())
                    {
                        found = true;
                    }
                }

                if (!found && isHovering && Instance.configData.Global.DisableHoverOnDismount)
                {
                    StopHover();
                }
                else if (_engineController.IsOff && isHovering && !Instance.configData.Global.HoverWithoutEngine)
                {
                    StopHover();
                }
            }

            private void OnDestroy() //Stop any timers or coroutines persisting after destruction or plugin unload
            {
                if (_hoverCoroutine != null) ServerMgr.Instance.StopCoroutine(_hoverCoroutine);
                _timedHoverTimer?.Destroy();
                _fuelUseTimer?.Destroy();
            }
        }
        #endregion
    }
}
