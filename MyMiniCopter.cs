#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using System.Text;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("My Mini Copter", "RFC1920", "0.4.2")]
    // Thanks to BuzZ[PHOQUE], the original author of this plugin
    [Description("Spawn a Mini Helicopter")]
    internal class MyMiniCopter : RustPlugin
    {
        private const string prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private ConfigData configData;

        private const string MinicopterSpawn = "myminicopter.spawn";
        private const string MinicopterFetch = "myminicopter.fetch";
        private const string MinicopterWhere = "myminicopter.where";
        private const string MinicopterAdmin = "myminicopter.admin";
        private const string MinicopterCooldown = "myminicopter.cooldown";
        private const string MinicopterUnlimited = "myminicopter.unlimited";

        private static LayerMask layerMask = LayerMask.GetMask("Terrain", "World", "Construction");

        private Dictionary<ulong, ulong> currentMounts = new Dictionary<ulong, ulong>();
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private class StoredData
        {
            public Dictionary<ulong, uint> playerminiID = new Dictionary<ulong, uint>();
            public Dictionary<ulong, double> playercounter = new Dictionary<ulong, double>();
        }

        private StoredData storedData;

        private bool HasPermission(ConsoleSystem.Arg arg, string permname)
        {
            return !(arg.Connection.player is BasePlayer) || permission.UserHasPermission((arg.Connection.player as BasePlayer)?.UserIDString, permname);
        }

        #region loadunload
        private void OnServerInitialized()
        {
            LoadConfigVariables();
            if (((configData.Global.cooldownmin * 60) <= 120) && configData.Global.useCooldown)
            {
                PrintError("Please set a longer cooldown time. Minimum is 2 min.");
                configData.Global.cooldownmin = 2;
            }
            SaveConfig(configData);

            LoadData();
            foreach (KeyValuePair<ulong, uint> playerMini in storedData.playerminiID)
            {
                MiniCopter miniCopter = BaseNetworkable.serverEntities.Find(playerMini.Value) as MiniCopter;
                if (miniCopter == null) continue;

                if (permission.UserHasPermission(playerMini.Key.ToString(), MinicopterUnlimited))
                {
                    miniCopter.fuelPerSec = 0f;
                    StorageContainer fuelCan = miniCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                    if (fuelCan?.IsValid() == true)
                    {
                        if (configData.Global.debug) Puts($"Setting fuel for MiniCopter {playerMini.Value.ToString()} owned by {playerMini.Key.ToString()}.");
                        ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelCan.inventory);
                        fuelCan.inventory.MarkDirty();
                    }
                    continue;
                }
                miniCopter.fuelPerSec = configData.Global.stdFuelConsumption;
            }
        }

        private void OnNewSave()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void Init()
        {
            AddCovalenceCommand("mymini", "SpawnMyMinicopterCommand");
            AddCovalenceCommand("nomini", "KillMyMinicopterCommand");
            AddCovalenceCommand("gmini",  "GetMyMiniMyCopterCommand");
            AddCovalenceCommand("wmini",  "WhereisMyMiniMyCopterCommand");
            AddCovalenceCommand("remini", "ReSpawnMyMinicopterCommand");

            permission.RegisterPermission(MinicopterSpawn, this);
            permission.RegisterPermission(MinicopterFetch, this);
            permission.RegisterPermission(MinicopterWhere, this);
            permission.RegisterPermission(MinicopterAdmin, this);
            permission.RegisterPermission(MinicopterCooldown, this);
            permission.RegisterPermission(MinicopterUnlimited, this);
        }

        private void Unload()
        {
            SaveData();
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
                {"SpawnUsage", "You need to supply a valid SteamId."},
                {"NoFoundMsg", "You do not have an active copter."},
                {"FoundMsg", "Your copter is located at {0}."},
                {"CooldownMsg", "You must wait {0} seconds before spawning a new mini copter."},
                {"DistanceMsg", "You must be within {0} meters of your mini copter."},
                {"RunningMsg", "Your copter is currently flying and cannot be fetched."},
                {"BlockedMsg", "You cannot spawn or fetch your copter while building blocked."}
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
                {"SpawnUsage", "Vous devez fournir un SteamId valide."},
                {"NoFoundMsg", "Vous n'avez pas de mini hélico actif"},
                {"FoundMsg", "Votre mini hélico est situé à {0}."},
                {"CooldownMsg", "Vous devez attendre {0} secondes avant de créer un nouveau mini hélico."},
                {"DistanceMsg", "Vous devez être à moins de {0} mètres de votre mini-hélico."},
                {"BlockedMsg", "Vous ne pouvez pas faire apparaître ou aller chercher votre hélico lorsque la construction est bloquée."}
            }, this, "fr");
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

            if (!player.HasPermission(MinicopterSpawn))
            {
                Message(player, "NoPermMsg");
                return;
            }
            BasePlayer bplayer = player.Object as BasePlayer;
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

            int secsleft = 0;
            if (hascooldown)
            {
                if (!storedData.playercounter.ContainsKey(bplayer.userID))
                {
                    storedData.playercounter.Add(bplayer.userID, secondsSinceEpoch);
                    SaveData();
                }
                else
                {
                    double count;
                    storedData.playercounter.TryGetValue(bplayer.userID, out count);

                    if ((secondsSinceEpoch - count) > (configData.Global.cooldownmin * 60))
                    {
                        if (configData.Global.debug) Puts("Player reached cooldown.  Clearing data.");
                        storedData.playercounter.Remove(bplayer.userID);
                        SaveData();
                    }
                    else
                    {
                        secsleft = Math.Abs((int)((configData.Global.cooldownmin * 60) - (secondsSinceEpoch - count)));

                        if (secsleft > 0)
                        {
                            if (configData.Global.debug) Puts($"Player DID NOT reach cooldown. Still {secsleft.ToString()} secs left.");
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
            if (storedData.playerminiID.ContainsKey(bplayer.userID))
            {
                uint findme;
                storedData.playerminiID.TryGetValue(bplayer.userID, out findme);
                BaseNetworkable foundit = BaseNetworkable.serverEntities.Find(findme);
                if (foundit != null)
                {
                    BaseNetworkable ent = BaseNetworkable.serverEntities.Find(findme);

                    // Distance check
                    if (configData.Global.gminidistance > 0f && Vector3.Distance(bplayer.transform.position, ent.transform.position) > configData.Global.gminidistance)
                    {
                        Message(player, "DistanceMsg", configData.Global.gminidistance);
                        return;
                    }

                    MiniCopter copter = ent as MiniCopter;
                    if (copter.engineController.IsOn)
                    {
                        Message(player, "RunningMsg");
                        return;
                    }

                    // Check for and dismount all players before moving the copter
                    BaseVehicle bv = ent as BaseVehicle;
                    for (int i = 0; i < bv.mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = bv.mountPoints[i];
                        if (mountPointInfo.mountable != null)
                        {
                            BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                            if (mounted)
                            {
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
                    foundit.transform.position = newLoc;
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
                uint findme;
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

        // Chat despawn
        [Command("remini")]
        private void ReSpawnMyMinicopterCommand(IPlayer player, string command, string[] args)
        {
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
                    Puts("You need to supply a valid SteamId.");
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
                    Puts("You need to supply a valid SteamId.");
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

            Quaternion rotation = player.GetNetworkRotation();
            Vector3 forward = rotation * Vector3.forward;
            // Make straight perpendicular to up axis so we don't spawn into ground or above player's head.
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 position = player.transform.position + (straight * 5f);
            position.y = player.transform.position.y + 2.5f;

            if (position == default(Vector3)) return;
            BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleMini == null) return;
            BaseEntity miniEntity = vehicleMini as BaseEntity;
            miniEntity.OwnerID = player.userID;

            MiniCopter miniCopter = vehicleMini as MiniCopter;

            vehicleMini.Spawn();
            if (permission.UserHasPermission(player.UserIDString, MinicopterUnlimited))
            {
                // Set fuel requirements to 0
                miniCopter.fuelPerSec = 0f;
                if (!configData.Global.allowFuelIfUnlimited)
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
            else if (configData.Global.startingFuel > 0)
            {
                StorageContainer fuelCan = miniCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                if (fuelCan?.IsValid() == true)
                {
                    ItemManager.CreateByItemID(-946369541, Convert.ToInt32(configData.Global.startingFuel))?.MoveToContainer(fuelCan.inventory);
                    fuelCan.inventory.MarkDirty();
                }
            }
            else
            {
                miniCopter.fuelPerSec = configData.Global.stdFuelConsumption;
            }

            SendReply(player, Lang("SpawnedMsg"));
            uint minicopteruint = vehicleMini.net.ID;
            if (configData.Global.debug) Puts($"SPAWNED MINICOPTER {minicopteruint.ToString()} for player {player.displayName} OWNER {miniEntity.OwnerID}");
            storedData.playerminiID.Remove(player.userID);
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
            storedData.playerminiID.Add(player.userID, minicopteruint);
            SaveData();

            miniEntity = null;
            miniCopter = null;
        }

        // Kill minicopter hook
        private void KillMyMinicopterPlease(BasePlayer player, bool killalways=false)
        {
            bool foundcopter = false;
            if (configData.Global.mindistance == 0f || killalways)
            {
                foundcopter = true;
            }
            else
            {
                List<BaseEntity> copterlist = new List<BaseEntity>();
                Vis.Entities(player.transform.position, configData.Global.mindistance, copterlist);

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
                uint findPlayerId;
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
                if (configData.Global.debug) Puts("Player too far from copter to destroy.");
                SendReply(player, Lang("DistanceMsg", null, configData.Global.mindistance));
            }
        }
        #endregion

        #region hooks
        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null) return null;
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} wants to mount seat id {mountable.net.ID.ToString()}");
                uint id = mountable.net.ID - 2;
                for (int i = 0; i < 3; i++)
                {
                    // Find copter and seats in storedData
                    if (configData.Global.debug) Puts($"  Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerminiID.ContainsValue(id))
                    {
                        if (configData.Global.debug) Puts("    yes, it is...");
                        if (currentMounts.ContainsValue(player.userID))
                        {
                            if (!player.GetMounted())
                            {
                                ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                                currentMounts.Remove(myKey);
                            }
                            return false;
                        }
                    }
                    id++;
                }
            }
            return null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} mounted seat id {mountable.net.ID.ToString()}");
                // Check this seat's ID to see if the copter is one of ours
                uint id = mountable.net.ID - 2; // max seat == copter.net.ID + 2, e.g. passenger seat id - 2 == copter id
                for (int i = 0; i < 3; i++)
                {
                    // Find copter in storedData
                    if (configData.Global.debug) Puts($"Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerminiID.ContainsValue(id))
                    {
                        if (configData.Global.debug) Puts($"Removing {player.displayName}'s ID {player.userID} from currentMounts for seat {mountable.net.ID.ToString()} on {id}");
                        currentMounts.Remove(mountable.net.ID);
                        if (configData.Global.debug) Puts($"Adding {player.displayName}'s ID {player.userID} to currentMounts for seat {mountable.net.ID.ToString()} on {id}");
                        currentMounts.Add(mountable.net.ID, player.userID);
                        break;
                    }
                    id++;
                }
            }
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null) return null;
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                if (!Physics.Raycast(new Ray(mountable.transform.position, Vector3.down), configData.Global.minDismountHeight, layerMask))
                {
                    // Is this one of ours?
                    if (storedData.playerminiID.ContainsValue(mountable.net.ID - 1))
                    {
                        if (!configData.Global.allowDriverDismountWhileFlying)
                        {
                            if (configData.Global.debug) Puts("DENY PILOT DISMOUNT");
                            return false;
                        }
                        ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                        currentMounts.Remove(myKey);
                    }
                    else if (storedData.playerminiID.ContainsValue(mountable.net.ID - 2))
                    {
                        if (!configData.Global.allowPassengerDismountWhileFlying)
                        {
                            if (configData.Global.debug) Puts("DENY PASSENGER DISMOUNT");
                            return false;
                        }
                        ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                        currentMounts.Remove(myKey);
                    }
                }
            }
            return null;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            MiniCopter mini = mountable.GetComponentInParent<MiniCopter>();
            if (mini != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} dismounted seat id {mountable.net.ID.ToString()}");
                uint id = mountable.net.ID - 2;
                for (int i = 0; i < 3; i++)
                {
                    // Find copter and seats in storedData
                    if (configData.Global.debug) Puts($"Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerminiID.ContainsValue(id))
                    {
                        if (configData.Global.debug) Puts($"Removing {player.displayName}'s ID {player.userID} from currentMounts for seat {mountable.net.ID.ToString()} on {id}");
                        currentMounts.Remove(mountable.net.ID);
                        break;
                    }
                    id++;
                }
            }
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
        }

        // On kill - tell owner
        private void OnEntityKill(MiniCopter entity)
        {
            if (entity == null) return;
            if (entity.net.ID == 0) return;

            if (storedData == null) return;
            if (storedData.playerminiID == null) return;
            ulong todelete = new ulong();

            if (!storedData.playerminiID.ContainsValue(entity.net.ID))
            {
                if (configData.Global.debug) Puts("KILLED non-plugin minicopter");
                return;
            }
            foreach (KeyValuePair<ulong, uint> item in storedData.playerminiID)
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
                currentMounts.Remove(entity.net.ID);
                currentMounts.Remove(entity.net.ID + 1);
                currentMounts.Remove(entity.net.ID + 2);
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
                        if (configData.Global.debug) Puts($"Enabling standard decay for spawned minicopter {entity.net.ID.ToString()}.");
                    }
                    else
                    {
                        if (configData.Global.debug) Puts($"Disabling decay for spawned minicopter {entity.net.ID.ToString()}.");
                        hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
                    }
                    return null;
                }
                else if (!configData.Global.allowDamage)
                {
                    return true;
                }
            }
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!configData.Global.killOnSleep) return;
            if (player == null) return;

            if (storedData.playerminiID.ContainsKey(player.userID))
            {
                uint findPlayerId;
                storedData.playerminiID.TryGetValue(player.userID, out findPlayerId);
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(findPlayerId);
                if (tokill == null) return; // Didn't find it

                // Check for mounted players
                BaseVehicle copter = tokill as BaseVehicle;
                for (int i = 0; i < copter.mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = copter.mountPoints[i];
                    if (mountPointInfo.mountable != null)
                    {
                        BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                        if (mounted)
                        {
                            if (configData.Global.debug) Puts("Copter owner sleeping but another one is mounted - cannot destroy copter");
                            return;
                        }
                    }
                }
                if (configData.Global.debug) Puts("Copter owner sleeping - destroying copter");
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

        #region config
        public class Global
        {
            public bool allowWhenBlocked;
            public bool allowRespawnWhenActive;
            public bool useCooldown;
            public bool copterDecay;
            public bool allowDamage;
            public bool killOnSleep;
            public bool allowFuelIfUnlimited;
            public bool allowDriverDismountWhileFlying;
            public bool allowPassengerDismountWhileFlying;
            public bool debug;
            public float stdFuelConsumption;
            public float cooldownmin;
            public float mindistance;
            public float gminidistance;
            public float minDismountHeight;
            public float startingFuel;
            public string Prefix; // Chat prefix
        }

        public class ConfigData
        {
            public Global Global;
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
                    Prefix = "[My MiniCopter]: "
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
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (storedData == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }
        #endregion
    }
}
