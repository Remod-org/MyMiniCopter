#region License (GPL v2)
/*
    DESCRIPTION
    Copyright (c) 2020-2024 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation version 2

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
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("My Scrap Heli", "RFC1920", "0.0.5")]
    [Description("Spawn a Scrap Helicopter")]
    internal class MyScrapHeli : RustPlugin
    {
        [PluginReference]
        private readonly Plugin NoEscape, Friends, Clans;

        private const string prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";

        private ConfigData configData;

        private const string ScrapHeliSpawn = "myscrapheli.spawn";
        private const string ScrapHeliFetch = "myscrapheli.fetch";
        private const string ScrapHeliWhere = "myscrapheli.where";
        private const string ScrapHeliAdmin = "myscrapheli.admin";
        private const string ScrapHeliCooldown = "myscrapheli.cooldown";
        private const string ScrapHeliUnlimited = "myscrapheli.unlimited";

        private static LayerMask layerMask = LayerMask.GetMask("Terrain", "World", "Construction");

        private Dictionary<ulong, ulong> currentMounts = new();
        private static readonly DateTime epoch = new(1970, 1, 1, 0, 0, 0);

        private class StoredData
        {
            public Dictionary<ulong, NetworkableId> playerheliID = new();
            public Dictionary<ulong, double> playercounter = new();
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

            AddCovalenceCommand("myscrap", "SpawnMyScrapHeliCommand");
            AddCovalenceCommand("noscrap", "KillMyScrapHeliCommand");
            AddCovalenceCommand("gscrap", "GetMyScrapHeliCommand");
            AddCovalenceCommand("wscrap", "WhereisMyScrapHeliCommand");
            AddCovalenceCommand("rescrap", "ReSpawnMyScrapHeliCommand");
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

            foreach (KeyValuePair<ulong, NetworkableId> x in storedData.playerheliID)
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

        private void Unload() => SaveData();
        #endregion

        #region Messages
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MyScrapHelp", "Spawn scrap heli in front of you." },
                {"NoScrapHelp", "Destroy your scrap heli if in range ({0} meters)." },
                {"WScraphelp", "Find your scrap heli." },
                {"GetheliHelp", "Retrieve your scrap heli." },
                {"AlreadyMsg", "You already have a heli scrap heli.\nUse command '/noscrap' to remove it."},
                {"SpawnedMsg", "Your scrap heli has spawned !\nUse command '/noscrap' to remove it."},
                {"KilledMsg", "Your scrap heli has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"SpawnUsage", "You need to supply a valid SteamId."},
                {"NoFoundMsg", "You do not have an active copter."},
                {"FoundMsg", "Your copter is located at {0}."},
                {"CooldownMsg", "You must wait {0} seconds before spawning a new scrap heli."},
                {"DistanceMsg", "You must be within {0} meters of your scrap heli."},
                {"RunningMsg", "Your scrap heli is currently flying and cannot be fetched."},
                {"BlockedMsg", "You cannot spawn or fetch your scrap heli while building blocked."}
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MyScrapHelp", "Créez un hélicoptère devant vous." },
                {"NoScrapHelp", "Détruisez votre hélicoptère si il est à portée. ({0} mètres)." },
                {"GetScrapHelp", "Récupérez votre hélicoptère." },
                {"AlreadyMsg", "Vous avez déjà un hélicoptère\nUtilisez la commande '/noscrap' pour le supprimer."},
                {"SpawnedMsg", "Votre hélico est arrivé !\nUtilisez la commande '/noscrap' pour le supprimer."},
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
        [Command("myscrap")]
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
                        int secsleft = Math.Abs((int)((configData.Global.cooldownmin * 60) - (secondsSinceEpoch - count)));

                        if (secsleft > 0)
                        {
                            if (configData.Global.debug) Puts($"Player DID NOT reach cooldown. Still {secsleft} secs left.");
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
        [Command("gscrap")]
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
                NetworkableId findme;
                storedData.playerheliID.TryGetValue(bplayer.userID, out findme);
                BaseNetworkable foundit = BaseNetworkable.serverEntities.Find(findme);
                if (foundit != null)
                {
                    BaseNetworkable ent = BaseNetworkable.serverEntities.Find(findme);

                    // Distance check
                    if (configData.Global.gscrapdistance > 0f && Vector3.Distance(bplayer.transform.position, ent.transform.position) > configData.Global.gscrapdistance)
                    {
                        Message(player, "DistanceMsg", configData.Global.gscrapdistance);
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
                                mounted.ClientRPC(RpcTarget.Player("ForcePositionTo", bplayer), player_pos);
                                mountPointInfo.mountable._mounted = null;
                            }
                        }
                    }
                    Vector3 newLoc = new(bplayer.transform.position.x + 2f, bplayer.transform.position.y + 2f, bplayer.transform.position.z + 2f);
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
        [Command("wscrap")]
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
                NetworkableId findme;
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
        [Command("rescrap")]
        private void ReSpawnMyScrapHeliCommand(IPlayer player, string command, string[] args)
        {
            KillMyScrapHeliCommand(player, "noscrap", new string[0]);
            SpawnMyScrapHeliCommand(player, "myscrap", new string[0]);
        }

        [Command("noscrap")]
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

            VIPSettings vipsettings;
            GetVIPSettings(player, out vipsettings);
            bool vip = vipsettings != null;

            Quaternion rotation = player.GetNetworkRotation();
            Vector3 forward = rotation * Vector3.forward;
            // Make straight perpendicular to up axis so we don't spawn into ground or above player's head.
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 position = player.transform.position + (straight * 5f);
            position.y = player.transform.position.y + 2.5f;

            if (position == default) return;
            BaseVehicle vehicleheli = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleheli == null) return;

            ScrapTransportHelicopter heliCopter = vehicleheli as ScrapTransportHelicopter;
            heliCopter.OwnerID = player.userID;
            vehicleheli.Spawn();

            if (permission.UserHasPermission(player.UserIDString, ScrapHeliUnlimited) || (vip && vipsettings.unlimited))
            {
                // Set fuel requirements to 0
                DoLog("Setting fuel requirements to zero");
                heliCopter.fuelPerSec = 0f;
                if (!configData.Global.allowFuelIfUnlimited && !(vip && vipsettings.canloot))
                {
                    // If the player is not allowed to use the fuel container, add 1 fuel so the copter will start.
                    // Also lock fuel container since there is no point in adding/removing fuel
                    IFuelSystem fuelCan = heliCopter?.GetFuelSystem();
                    if (fuelCan != null)
                    {
                        fuelCan?.AddFuel(1);
                        // LOCKED by CanLootEntity hook
                    }
                }
            }
            else if (configData.Global.startingFuel > 0 || (vip && vipsettings.startingFuel > 0))
            {
                IFuelSystem fuelCan = heliCopter?.GetFuelSystem();
                if (fuelCan != null)
                {
                    float sf = vip ? vipsettings.startingFuel : configData.Global.startingFuel;
                    fuelCan.AddFuel((int)sf);
                }
            }
            else
            {
                heliCopter.fuelPerSec = vip ? vipsettings.stdFuelConsumption : configData.Global.stdFuelConsumption;
            }

            SendReply(player, Lang("SpawnedMsg"));
            NetworkableId helicopteruint = vehicleheli.net.ID;
            if (configData.Global.debug) Puts($"SPAWNED SCRAPCOPTER {helicopteruint} for player {player.displayName} OWNER {vehicleheli.OwnerID}");
            storedData.playerheliID.Remove(player.userID);
            ulong myKey = currentMounts.FirstOrDefault(x => x.Value == player.userID).Key;
            currentMounts.Remove(myKey);
            storedData.playerheliID.Add(player.userID, helicopteruint);
            SaveData();

            vehicleheli = null;
            heliCopter = null;
        }

        // Kill helicopter hook
        private void KillMyScrapheliPlease(BasePlayer player, bool killalways = false)
        {
            bool foundcopter = false;
            if (configData.Global.mindistance == 0f || killalways)
            {
                foundcopter = true;
            }
            else
            {
                List<BaseEntity> copterlist = new();
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
                storedData.playerheliID.TryGetValue(player.userID, out NetworkableId findPlayerId);
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
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
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
            vipsettings = null;
            if (player?.userID.IsSteamId() != true)
            {
                DoLog("User has no VIP settings");
                return;
            }
            if (configData.VIPSettings == null) return;

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
        }

        private bool IsRaidBlocked(BasePlayer player)
        {
            if (configData.Global.useNoEscape && NoEscape)
            {
                return (bool)NoEscape?.CallHook("IsRaidBlocked", player);
            }
            return false;
        }
        #endregion

        #region hooks
        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player?.userID == 0) return null;
            ScrapTransportHelicopter heli = container.GetParentEntity() as ScrapTransportHelicopter;
            if (heli != null)
            {
                if (storedData.playerheliID.ContainsKey(player.userID) && heli?.net.ID.Value == storedData.playerheliID[player.userID].Value)
                {
                    GetVIPSettings(player, out VIPSettings vipsettings);
                    bool vip = vipsettings != null;
                    bool unlimited = permission.UserHasPermission(player.UserIDString, ScrapHeliUnlimited) || (vip && vipsettings.unlimited);
                    if (!unlimited) return null;
                    if (!(unlimited && configData.Global.allowFuelIfUnlimited))
                    {
                        Message(player.IPlayer, "NoPermMsg");
                        return true;
                    }
                }
                return null;
            }
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null) return null;
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            NetworkableId currentseat = new(heli.net.ID.Value);
            currentseat.Value += 3; // Start with driver seat
            for (int i = 0; i < 2; i++)
            {
                // Find copter and seats in storedData
                DoLog($"  Is this our copter with ID {heli.net.ID.Value}?");
                if (storedData.playerheliID.ContainsValue(heli.net.ID))
                {
                    DoLog("    yes, it is...");
                    if (player?.userID.IsSteamId() != true) return true; // Block mounting by NPCs
                    BaseVehicle helimount = BaseNetworkable.serverEntities.Find(heli.net.ID) as BaseVehicle;
                    DoLog($"Does {player.userID} match {helimount?.OwnerID}, or are they a friend?");
                    if (!IsFriend(player.userID, helimount.OwnerID))
                    {
                        DoLog("Player does not own scrapcopter, and is not a friend of the owner.");
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                DoLog($"OnEntityMounted: Player {player.userID} mounted seat id {mountable.net.ID}");
                // Check this seat's ID to see if the copter is one of ours
                NetworkableId currentseat = new(heli.net.ID.Value);
                currentseat.Value += 3; // Start with driver seat
                for (int i = 0; i < 2; i++)
                {
                    // Find copter in storedData
                    DoLog($"Is this our copter with ID {heli.net.ID.Value}?");
                    if (storedData.playerheliID.ContainsValue(heli.net.ID))
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
            ScrapTransportHelicopter heli = mountable?.GetComponentInParent<ScrapTransportHelicopter>();
            DoLog($"CanDismountEntity: Player {player.userID} wants to dismount seat id {mountable.net.ID}");

            // Only operates if scrapheli is not null and if we are flying above scrapmum height
            if (heli != null && !Physics.Raycast(new Ray(mountable.transform.position, Vector3.down), configData.Global.minDismountHeight, layerMask))
            {
                DoLog($"Is this our copter with ID {heli.net.ID.Value}?");
                NetworkableId passenger = new(heli.net.ID.Value);
                passenger.Value += 4;
                NetworkableId driver = new(heli.net.ID.Value);
                driver.Value += 3;
                if (storedData.playerheliID.ContainsValue(heli.net.ID))
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
                else if (storedData.playerheliID.ContainsValue(passenger))
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
            ScrapTransportHelicopter heli = mountable.GetComponentInParent<ScrapTransportHelicopter>();
            if (heli != null)
            {
                DoLog($"OnEntityDismounted: Player {player.userID} dismounted seat id {mountable.net.ID}");
                NetworkableId currentseat = new(heli.net.ID.Value);
                currentseat.Value += 3; // Start with driver seat
                for (int i = 0; i < 2; i++)
                {
                    // Find copter and seats in storedData
                    DoLog($"Is this our copter with ID {heli.net.ID.Value}?");
                    if (storedData.playerheliID.ContainsValue(heli.net.ID))
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
        private void OnEntityKill(ScrapTransportHelicopter entity)
        {
            if (entity == null) return;
            if (entity.net.ID.Value == 0) return;

            if (storedData == null) return;
            if (storedData.playerheliID == null) return;
            ulong todelete = new();

            if (!storedData.playerheliID.ContainsValue(entity.net.ID))
            {
                DoLog("KILLED non-plugin scrapcopter");
                return;
            }
            foreach (KeyValuePair<ulong, NetworkableId> item in storedData.playerheliID)
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
                currentMounts.Remove(entity.net.ID.Value);
                currentMounts.Remove(entity.net.ID.Value + 1);
                currentMounts.Remove(entity.net.ID.Value + 2);
                SaveData();
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity?.net?.ID == null) return null;
            if (hitInfo?.damageTypes == null) return null;

            if (storedData?.playerheliID?.ContainsValue(entity.net.ID) == true)
            {
                if (hitInfo?.damageTypes?.GetMajorityDamageType().ToString() == "Decay")
                {
                    if (configData.Global.copterDecay)
                    {
                        DoLog($"Enabling standard decay for spawned scrapcopter {entity.net.ID}.");
                    }
                    else
                    {
                        DoLog($"Disabling decay for spawned scrapcopter {entity.net.ID}.");
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

            if (storedData.playerheliID.ContainsKey(player.userID))
            {
                NetworkableId findScrapId;
                storedData.playerheliID.TryGetValue(player.userID, out findScrapId);
                BaseNetworkable tokill = BaseNetworkable.serverEntities.Find(findScrapId);
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
            StringBuilder sb = new();
            sb.Append("<color=#05eb59>").Append(Name).Append(' ').Append(Version).Append("</color> · Spawn a Scrap Helicopter\n");
            sb.Append("  · ").Append("/myscrap: ").AppendLine(Lang("MyScrapHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/noscrap: ").AppendLine(Lang("NoScrapHelp", null, configData.Global.mindistance));
            sb.Append("  · ").Append("/wscrap: ").AppendLine(Lang("WScrapHelp"));

            if (permission.UserHasPermission(player.UserIDString, ScrapHeliFetch))
            {
                sb.Append("  · ").Append("/gscrap: ").AppendLine(Lang("GetScrapHelp"));
            }
            player.ChatMessage(sb.ToString());
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
            public bool killOnSleep;
            public bool allowFuelIfUnlimited;
            public bool allowDamage;
            public bool allowDriverDismountWhileFlying;
            public bool allowPassengerDismountWhileFlying;
            public bool debug;
            public float stdFuelConsumption;
            public float cooldownmin;
            public float mindistance;
            public float gscrapdistance;
            public float minDismountHeight;
            public float startingFuel;
            public string Prefix; // Chat prefix
        }

        public class VIPSettings
        {
            public bool unlimited;
            public bool FastStart;
            public bool canloot;
            public bool allowDamage;
            public float stdFuelConsumption;
            public float startingFuel;
            public float cooldownmin;
            public float mindistance;
            public float gscrapdistance;
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
            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new()
            {
                Global = new Global()
                {
                    allowWhenBlocked = false,
                    allowRespawnWhenActive = false,
                    useCooldown = true,
                    useClans = false,
                    useFriends = false,
                    useNoEscape = false,
                    useTeams = false,
                    allowDamage = true,
                    copterDecay = false,
                    killOnSleep = false,
                    allowFuelIfUnlimited = false,
                    allowDriverDismountWhileFlying = true,
                    allowPassengerDismountWhileFlying = true,
                    stdFuelConsumption = 0.25f,
                    cooldownmin = 60f,
                    mindistance = 0f,
                    gscrapdistance = 0f,
                    minDismountHeight = 7f,
                    startingFuel = 0f,
                    Prefix = "[My ScrapHeli]: ",
                    debug = false
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
