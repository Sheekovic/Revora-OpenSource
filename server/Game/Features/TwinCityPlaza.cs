using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    /// <summary>
    /// Twin City plaza layout. Leftover private-server event desks are jailed
    /// off the square and their handlers stay closed so map 6000 cannot run them.
    /// </summary>
    public static class TwinCityPlaza
    {
        public const uint NobilityDonorId = 900042;
        public const ushort NobilityDonorX = 394, NobilityDonorY = 350;
        public const uint ChiJiangId = 900043;
        public const ushort ChiJiangX = 396;
        public const ushort ChiJiangY = 348;
        public const uint PackingClerkId = 11275;
        public const ushort PackingClerkX = 304;
        public const ushort PackingClerkY = 433;
        public const uint BoxerId = 181;
        public const ushort BoxerX = 428;
        public const ushort BoxerY = 282;

        // Currently standing on Twin City. Parked to jail map 6000 on boot.
        public static readonly uint[] PlazaRemoveIds = new uint[]
        {
            8713, 66712, 417171, 801717, 42251, 44885,
            115522, 500010, 122222, 445532, 10021253, 10021254, 101895, 101897, 6330,
            900020, 900174, 900022, 900025, 900021, 900023, 900024, 54248,
            115523, 900169, 900047, 10620, 900176, 900028, 127123, 54259,
            900001, 900034, 900040, 141198, 137, 900037, 900183, 900032,
            35123, 54249, 900005
        };

        // Handlers stay closed even if a player reaches the jail tile.
        public static readonly uint[] ClosedDeskIds = new uint[]
        {
            8713, 66712, 417171, 42251, 44885,
            500010, 122222, 445532, 10021253, 10021254, 6330,
            900020, 900174, 900022, 900025, 900021, 900023, 900024, 54248,
            115523, 900169, 900047, 10620, 900176, 127123, 54259,
            900001, 900034, 900040, 141198, 137, 900037, 900032,
            35123, 54249, 900005,
            900018, 899986, 900173, 900035, 900046, 9631, 9630,
            899979, 899980, // Retired New Year desks: hidden options must not charge or exchange cards.
            900033 // Common PK reward clerk: its admission desk 900032 is retired.
        };

        private static readonly HashSet<uint> Closed = new HashSet<uint>(ClosedDeskIds);

        public static bool IsClosed(uint npcId)
        {
            return Closed.Contains(npcId);
        }

        public static bool HandleClosed(GameState client, NpcRequest request)
        {
            if (client == null || client.Entity == null || !IsClosed(client.ActiveNpc))
                return false;
            Npcs dialog = new Npcs(client);
            dialog.Text("This desk is closed. Twin City Revora clerks handle hunts, events, rebirth, and shops.");
            dialog.Option("Alright.", 255);
            dialog.Send();
            return true;
        }

        public static void Apply(Action<uint, ushort, ushort, ushort> park)
        {
            if (park == null)
                return;
            ushort x = 50;
            ushort y = 74;
            for (int i = 0; i < PlazaRemoveIds.Length; i++)
            {
                // Keep the historical slot spacing for other desks. The restored
                // Anniversary, Elite Guild War and team services restore their own NPCs.
                if(PlazaRemoveIds[i]!=AnniversaryQuest.Npc && PlazaRemoveIds[i]!=115522 && PlazaRemoveIds[i]!=101895 && PlazaRemoveIds[i]!=101897 && PlazaRemoveIds[i]!=Sistemas.Quarantine.NpcId && PlazaRemoveIds[i]!=Tournaments.LocalTeamPkLedger.NpcId && PlazaRemoveIds[i]!=Tournaments.LocalTeamPkLedger.SkillNpcId)park(PlazaRemoveIds[i], 6000, x, y);
                x += 4;
                if (x > 118)
                {
                    x = 50;
                    y += 4;
                }
            }
            park(ChiJiangId, 1002, ChiJiangX, ChiJiangY);
            park(PackingClerkId, 1002, PackingClerkX, PackingClerkY);
            park(BoxerId, 1002, BoxerX, BoxerY);
            RestoreClanEnvoy(park,101895,297,279);
            RestoreClanEnvoy(park,101897,293,279);
        }

        private static void RestoreClanEnvoy(Action<uint,ushort,ushort,ushort> park,uint id,ushort anchorX,ushort anchorY)
        {
            Map town;
            if(!Kernel.Maps.TryGetValue(1002,out town) || town.BaseID!=1002)throw new InvalidOperationException("Clan envoy public map is unavailable.");
            Revora.Interfaces.INpc actor=null;
            foreach(var map in Kernel.Maps.Values) {
                Revora.Interfaces.INpc found;
                if(!map.Npcs.TryGetValue(id,out found))continue;
                if(actor!=null || found.Type!=Enums.NpcType.ClanInfo || found.MapID!=1002 && found.MapID!=6000)
                    throw new InvalidOperationException("Clan envoy identity collision.");
                actor=found;
            }
            if(actor==null)throw new InvalidOperationException("Clan envoy catalog actor is missing.");
            // AddNpc marks the actor's own tile invalid. Retain an already restored
            // actor instead of treating that occupancy as terrain failure and drifting.
            if(actor.MapID==1002 && Kernel.GetDistance(actor.X,actor.Y,anchorX,anchorY)<=6
                && ClanEnvoyApproach(town,actor.X,actor.Y)) {
                bool conflict=false;
                foreach(var other in town.Npcs.Values)if(other.UID!=id && Kernel.GetDistance(actor.X,actor.Y,other.X,other.Y)<2){conflict=true;break;}
                if(!conflict)return;
            }
            // Deterministic bounded placement, including recovery of database rows parked
            // by older boots. Keep original mesh/type and never reuse another actor's tile.
            for(int radius=0;radius<=6;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                ushort x=(ushort)(anchorX+dx),y=(ushort)(anchorY+dy);
                if(!town.Floor[x,y,MapObjectType.Player] || !ClanEnvoyApproach(town,x,y))continue;
                bool occupied=false;
                foreach(var other in town.Npcs.Values)if(other.UID!=id && Kernel.GetDistance(x,y,other.X,other.Y)<2){occupied=true;break;}
                if(occupied)continue;
                park(id,1002,x,y);return;
            }
            throw new InvalidOperationException("Clan envoy site is occupied.");
        }
        private static bool ClanEnvoyApproach(Map map,ushort x,ushort y) {
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                if((dx!=0 || dy!=0) && map.Floor[x+dx,y+dy,MapObjectType.Player])return true;
            return false;
        }
    }
}
