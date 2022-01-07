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
    [Info("My Scrap Copter", "RFC1920", "0.0.2")]
    [Description("Spawn a Scrap Helicopter")]
    internal class MyScrapHeli : RustPlugin
    {
        private const string prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";

        private ConfigData configData;

        private const string ScrapHeliSpawn = "myscrapheli.spawn";
        private const string ScrapHeliFetch = "myscrapheli.fetch";
        private const string ScrapHeliWhere = "myscrapheli.where";
        private const string ScrapHeliAdmin = "myscrapheli.admin";
        private const string ScrapHeliCooldown = "myscrapheli.cooldown";
        private const string ScrapHeliUnlimited = "myscrapheli.unlimited";

        private static LayerMask layerMask = LayerMask.GetMask("Terrain", "World", "Construction");

        private Dictionary<ulong, ulong> currentMounts = new Dictionary<ulong, ulong>();
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private class StoredData
        {
            public Dictionary<ulong, uint> playerheliID = new Dictionary<ulong, uint>();
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
            if (((configData.Global.cooldownmin * 60) <= 120) && configData.Global.useCooldown)
            {
                PrintError("Please set a longer cooldown time. helimum is 2 min.");
                configData.Global.cooldownmin = 2;
            }

            AddCovalenceCommand("myheli", "SpawnMyScrapHeliCommand");
            AddCovalenceCommand("noheli", "KillMyScrapHeliCommand");
            AddCovalenceCommand("gheli",  "GetMyScrapHeliCommand");
            AddCovalenceCommand("wheli",  "WhereisMyScrapHeliCommand");
            AddCovalenceCommand("reheli", "ReSpawnMyScrapHeliCommand");
        }

        private void OnNewSave()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void Loaded()
        {
            LoadConfigVariables();
            permission.RegisterPermission(ScrapHeliSpawn, this);
            permission.RegisterPermission(ScrapHeliFetch, this);
            permission.RegisterPermission(ScrapHeliWhere, this);
            permission.RegisterPermission(ScrapHeliAdmin, this);
            permission.RegisterPermission(ScrapHeliCooldown, this);
            permission.RegisterPermission(ScrapHeliUnlimited, this);
            LoadData();

            foreach (KeyValuePair<ulong, uint> x in storedData.playerheliID)
            {
                BaseNetworkable vehicleheli = BaseNetworkable.serverEntities.Find(x.Value);
                if (vehicleheli == null) continue;
                ScrapTransportHelicopter heliCopter = vehicleheli as ScrapTransportHelicopter;
                if (heliCopter == null) continue;
                IPlayer player = covalence.Players.FindPlayer(x.Key.ToString());
                if (player == null) continue;

                heliCopter.fuelPerSec = permission.UserHasPermission(player.Id, ScrapHeliUnlimited) ? 0f : configData.Global.stdFuelConsumption;
            }
            SaveConfig(configData);
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
                {"MyheliHelp", "Spawn scrap copter in front of you." },
                {"NoheliHelp", "Destroy your scrap copter if in range ({0} meters)." },
                {"WheliHelp", "Find your scrap copter." },
                {"GetheliHelp", "Retrieve your scrap copter." },
                {"AlreadyMsg", "You already have a heli scrap copter.\nUse command '/noheli' to remove it."},
                {"SpawnedMsg", "Your scrap copter has spawned !\nUse command '/noheli' to remove it."},
                {"KilledMsg", "Your scrap copter has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"SpawnUsage", "You need to supply a valid SteamId."},
                {"NoFoundMsg", "You do not have an active copter."},
                {"FoundMsg", "Your copter is located at {0}."},
                {"CooldownMsg", "You must wait {0} seconds before spawning a new scrap copter."},
                {"DistanceMsg", "You must be within {0} meters of your scrap copter."},
                {"RunningMsg", "Your scrap copter is currently flying and cannot be fetched."},
                {"BlockedMsg", "You cannot spawn or fetch your scrap copter while building blocked."}
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MyheliHelp", "Créez un hélicoptère devant vous." },
                {"NoheliHelp", "Détruisez votre hélicoptère si il est à portée. ({0} mètres)." },
                {"GetheliHelp", "Récupérez votre hélicoptère." },
                {"AlreadyMsg", "Vous avez déjà un hélicoptère\nUtilisez la commande '/noheli' pour le supprimer."},
                {"SpawnedMsg", "Votre hélico est arrivé !\nUtilisez la commande '/noheli' pour le supprimer."},
                {"KilledMsg", "Votre hélico a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"SpawnUsage", "Vous devez fournir un SteamId valide."},
                {"NoFoundMsg", "Vous n'avez pas de hélico actif"},
                {"FoundMsg", "Votre hélico est situé à {0}."},
                {"CooldownMsg", "Vous devez attendre {0} secondes avant de créer un nouveau hélico."},
                {"DistanceMsg", "Vous devez être à moins de {0} mètres de votre hélico."},
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
        [Command("myheli")]
        private void SpawnMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            double secondsSinceEpoch = DateTime.UtcNow.Subtract(epoch).TotalSeconds;

            if (!player.HasPermission(ScrapHeliSpawn))
            {
                Message(player, "NoPermMsg");
                return;
            }
            BasePlayer bplayer = player.Object as BasePlayer;
            if (storedData.playerheliID.ContainsKey(bplayer.userID))
            {
                if (configData.Global.allowRespawnWhenActive)
                {
                    Message(player, "AlreadyMsg");
                    return;
                }
                KillMyScrapheliPlease(bplayer, true);
            }

            if (bplayer.IsBuildingBlocked() && !configData.Global.allowWhenBlocked)
            {
                Message(player, "BlockedMsg");
                return;
            }

            bool hascooldown = player.HasPermission(ScrapHeliCooldown);
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
            SpawnMyScrapHeli(bplayer);
        }

        // Fetch copter
        [Command("gheli")]
        private void GetMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer bplayer = player.Object as BasePlayer;
            if (bplayer.IsBuildingBlocked() && !configData.Global.allowWhenBlocked)
            {
                Message(player, "BlockedMsg");
                return;
            }

            bool canspawn = player.HasPermission(ScrapHeliSpawn);
            bool canfetch = player.HasPermission(ScrapHeliFetch);
            if (!(canspawn && canfetch))
            {
                Message(player, "NoPermMsg");
                return;
            }
            if (storedData.playerheliID.ContainsKey(bplayer.userID))
            {
                uint findme;
                storedData.playerheliID.TryGetValue(bplayer.userID, out findme);
                BaseNetworkable foundit = BaseNetworkable.serverEntities.Find(findme);
                if (foundit != null)
                {
                    BaseNetworkable ent = BaseNetworkable.serverEntities.Find(findme);

                    // Distance check
                    if (configData.Global.ghelidistance > 0f && Vector3.Distance(bplayer.transform.position, ent.transform.position) > configData.Global.ghelidistance)
                    {
                        Message(player, "DistanceMsg", configData.Global.ghelidistance);
                        return;
                    }

                    ScrapTransportHelicopter copter = ent as ScrapTransportHelicopter;
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
        [Command("wheli")]
        private void WhereisMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(ScrapHeliWhere))
            {
                Message(player, "NoPermMsg");
                return;
            }
            BasePlayer bplayer = player.Object as BasePlayer;
            if (storedData.playerheliID.ContainsKey(bplayer.userID))
            {
                uint findme;
                storedData.playerheliID.TryGetValue(bplayer.userID, out findme);
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
        [Command("reheli")]
        private void ReSpawnMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            KillMyScrapHeliCommand(player, "noheli", new string[0]);
            SpawnMyScrapHeliCommand(player, "myheli", new string[0]);
        }

        [Command("noheli")]
        private void KillMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(ScrapHeliSpawn))
            {
                Message(player, "NoPermMsg");
                return;
            }
            KillMyScrapheliPlease(player.Object as BasePlayer);
        }
        #endregion

        #region consolecommands
        // Console spawn
        [ConsoleCommand("spawnhelicopter")]
        private void SpawnMyScrapHeliConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                if (arg.Args == null)
                {
                    Puts("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if (!HasPermission(arg, ScrapHeliAdmin))
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
                    SpawnMyScrapHeli(player);
                }
            }
        }

        // Console despawn
        [ConsoleCommand("killhelicopter")]
        private void KillMyScrapHeliConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                if (arg.Args == null)
                {
                    Puts("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if (!HasPermission(arg, ScrapHeliAdmin))
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
                    KillMyScrapheliPlease(player);
                }
            }
        }
        #endregion

        #region ourhooks
        // Spawn hook
        private void SpawnMyScrapHeli(BasePlayer player)
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
            BaseVehicle vehicleheli = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleheli == null) return;
            BaseEntity heliEntity = vehicleheli as BaseEntity;
            heliEntity.OwnerID = player.userID;

            ScrapTransportHelicopter heliCopter = vehicleheli as ScrapTransportHelicopter;

            vehicleheli.Spawn();
            if (permission.UserHasPermission(player.UserIDString, ScrapHeliUnlimited))
            {
                // Set fuel requirements to 0
                heliCopter.fuelPerSec = 0f;
                if (!configData.Global.allowFuelIfUnlimited)
                {
                    // If the player is not allowed to use the fuel container, add 1 fuel so the copter will start.
                    // Also lock fuel container since there is no point in adding/removing fuel
                    StorageContainer fuelCan = heliCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                    if (fuelCan?.IsValid() == true)
                    {
                        ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelCan.inventory);
                        fuelCan.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
            }
            else if (configData.Global.startingFuel > 0)
            {
                StorageContainer fuelCan = heliCopter?.GetFuelSystem().fuelStorageInstance.Get(true);
                if (fuelCan?.IsValid() == true)
                {
                    ItemManager.CreateByItemID(-946369541, Convert.ToInt32(configData.Global.startingFuel))?.MoveToContainer(fuelCan.inventory);
                }
            }
            else
            {
                heliCopter.fuelPerSec = configData.Global.stdFuelConsumption;
            }

            SendReply(player, Lang("SpawnedMsg"));
            uint helicopteruint = vehicleheli.net.ID;
            if (configData.Global.debug) Puts($"SPAWNED SCRAPCOPTER {helicopteruint.ToString()} for player {player.displayName} OWNER {heliEntity.OwnerID}");
            storedData.playerheliID.Remove(player.userID);
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
            storedData.playerheliID.Add(player.userID, helicopteruint);
            SaveData();

            heliEntity = null;
            heliCopter = null;
        }

        // Kill helicopter hook
        private void KillMyScrapheliPlease(BasePlayer player, bool killalways=false)
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
                    ScrapTransportHelicopter foundent = p.GetComponentInParent<ScrapTransportHelicopter>();
                    if (foundent != null)
                    {
                        foundcopter = true;
                    }
                }
            }

            if (storedData.playerheliID.ContainsKey(player.userID) && foundcopter)
            {
                uint findPlayerId;
                storedData.playerheliID.TryGetValue(player.userID, out findPlayerId);
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(findPlayerId);
                tokill?.Kill(BaseNetworkable.DestroyMode.Gib);
                storedData.playerheliID.Remove(player.userID);
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} wants to mount seat id {mountable.net.ID.ToString()}");
                uint id = mountable.net.ID - 2;
                for (int i = 0; i < 3; i++)
                {
                    // Find copter and seats in storedData
                    if (configData.Global.debug) Puts($"  Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerheliID.ContainsValue(id))
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} mounted seat id {mountable.net.ID.ToString()}");
                // Check this seat's ID to see if the copter is one of ours
                uint id = mountable.net.ID - 2; // max seat == copter.net.ID + 2, e.g. passenger seat id - 2 == copter id
                for (int i = 0; i < 3; i++)
                {
                    // Find copter in storedData
                    if (configData.Global.debug) Puts($"Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerheliID.ContainsValue(id))
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                if (!Physics.Raycast(new Ray(mountable.transform.position, Vector3.down), configData.Global.minDismountHeight, layerMask))
                {
                    // Is this one of ours?
                    if (storedData.playerheliID.ContainsValue(mountable.net.ID - 1))
                    {
                        if (!configData.Global.allowDriverDismountWhileFlying)
                        {
                            if (configData.Global.debug) Puts("DENY PILOT DISMOUNT");
                            return false;
                        }
                        ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
                        currentMounts.Remove(myKey);
                    }
                    else if (storedData.playerheliID.ContainsValue(mountable.net.ID - 2))
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                if (configData.Global.debug) Puts($"Player {player.userID.ToString()} dismounted seat id {mountable.net.ID.ToString()}");
                uint id = mountable.net.ID - 2;
                for (int i = 0; i < 3; i++)
                {
                    // Find copter and seats in storedData
                    if (configData.Global.debug) Puts($"Is this our copter with ID {id.ToString()}?");
                    if (storedData.playerheliID.ContainsValue(id))
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
        private void OnEntityKill(ScrapTransportHelicopter entity)
        {
            if (entity == null) return;
            if (entity.net.ID == 0) return;

            if (storedData == null) return;
            if (storedData.playerheliID == null) return;
            ulong todelete = new ulong();

            if (!storedData.playerheliID.ContainsValue(entity.net.ID))
            {
                if (configData.Global.debug) Puts("KILLED non-plugin helicopter");
                return;
            }
            foreach (KeyValuePair<ulong, uint> item in storedData.playerheliID)
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
                storedData.playerheliID.Remove(todelete);
                currentMounts.Remove(entity.net.ID);
                currentMounts.Remove(entity.net.ID + 1);
                currentMounts.Remove(entity.net.ID + 2);
                SaveData();
            }
        }

        // Disable decay for our copters if so configured
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity?.net?.ID == null) return;
            if (hitInfo?.damageTypes == null) return;
            if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;
            if (storedData == null) return;

            if (storedData.playerheliID?.ContainsValue(entity.net.ID) == true)
            {
                if (configData.Global.copterDecay)
                {
                    if (configData.Global.debug) Puts($"Enabling standard decay for spawned helicopter {entity.net.ID.ToString()}.");
                }
                else
                {
                    if (configData.Global.debug) Puts($"Disabling decay for spawned helicopter {entity.net.ID.ToString()}.");
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
                }
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!configData.Global.killOnSleep) return;
            if (player == null) return;

            if (storedData.playerheliID.ContainsKey(player.userID))
            {
                uint findPlayerId;
                storedData.playerheliID.TryGetValue(player.userID, out findPlayerId);
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
                storedData.playerheliID.Remove(player.userID);
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
            sb.Append("<color=#05eb59>").Append(Name).Append(' ').Append(Version).Append("</color> · Spawn a heli Helicopter\n");
            sb.Append("  · ").Append("/myheli: ").AppendLine(Lang("MyheliHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/noheli: ").AppendLine(Lang("NoheliHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/wheli: ").AppendLine(Lang("WheliHelp"));

            if (permission.UserHasPermission(player.UserIDString, ScrapHeliFetch))
            {
                sb.Append("  · ").Append("/gheli: ").AppendLine(Lang("GetheliHelp"));
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
            public bool killOnSleep;
            public bool allowFuelIfUnlimited;
            public bool allowDriverDismountWhileFlying;
            public bool allowPassengerDismountWhileFlying;
            public bool debug;
            public float stdFuelConsumption;
            public float cooldownmin;
            public float mindistance;
            public float ghelidistance;
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
                    killOnSleep = false,
                    allowFuelIfUnlimited = false,
                    allowDriverDismountWhileFlying = true,
                    allowPassengerDismountWhileFlying = true,
                    stdFuelConsumption = 0.25f,
                    cooldownmin = 60f,
                    mindistance = 0f,
                    ghelidistance = 0f,
                    minDismountHeight = 7f,
                    startingFuel = 0f,
                    Prefix = "[My ScrapHeli]: "
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
            // Save the data file as we add/remove helicopters.
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }
        #endregion
    }
}
