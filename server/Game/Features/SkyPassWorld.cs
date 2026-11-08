using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SkyPassWorld
    {
        internal const ushort MapId=1040,Home=1012;
        internal const uint Daniel=30055,Cloud=30054;
        internal sealed class Challenge {internal int Stage,Route;internal uint Gate,Guard,Monster;internal ushort X,Y;}
        internal static readonly List<Challenge> Challenges=new List<Challenge>();
        internal static Challenge At(int stage,int route) {return Challenges.Single(c=>c.Stage==stage && c.Route==route);}
        internal static bool Actor(uint id) {return id==Daniel || id==Cloud || Challenges.Any(c=>c.Gate==id || c.Guard==id);}
        internal static void Initialize() {
            Challenges.Clear();
            for(int stage=1;stage<=5;stage++)for(int route=0;route<5;route++) {
                uint gate=stage<5?(uint)(30001+(stage-1)*10+route):new uint[]{30043,30044,30045,30051,30052}[route];
                uint guard=stage==1?(uint)(30010-route):stage==5?(uint)(30050-route):(uint)(30016+(stage-2)*10+route);
                Challenges.Add(new Challenge{Stage=stage,Route=route,Gate=gate,Guard=guard,Monster=(uint)(3000+(stage-1)*5+route)});
            }
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT npctype,bound_x,bound_y,bound_cx,bound_cy FROM monsterspawns WHERE mapid=1040 AND npctype BETWEEN 3000 AND 3024 ORDER BY npctype"))
            using(var r=q.ExecuteReader()) {
                var seen=new HashSet<uint>();while(r.Read()) {
                    uint type=Convert.ToUInt32(r[0]);if(!seen.Add(type))throw new InvalidOperationException("Sky challenge spawn is ambiguous.");
                    var match=Challenges.Single(a=>a.Monster==type);match.X=(ushort)(Convert.ToInt32(r[1])+Convert.ToInt32(r[3])/2);match.Y=(ushort)(Convert.ToInt32(r[2])+Convert.ToInt32(r[4])/2);
                }
                if(seen.Count!=25)throw new InvalidOperationException("Sky challenge spawn catalog incomplete.");
            }}
            foreach(uint id in Enumerable.Range(721100,10).Select(n=>(uint)n))if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Sky pass item missing.");
            foreach(var challenge in Challenges)if(!MonsterInformation.MonsterInformations.ContainsKey(challenge.Monster))throw new InvalidOperationException("Sky challenge monster missing.");
        }
        internal static void PlaceNpcs() {
            var actors=new List<NpcSpawn>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,name,lookface,mapid,cellx,celly FROM npcs WHERE id BETWEEN 30001 AND 30055 ORDER BY id"))
            using(var r=q.ExecuteReader())while(r.Read()) {
                uint id=Convert.ToUInt32(r[0]);if(!Actor(id))continue;
                actors.Add(new NpcSpawn{UID=id,Name=r.GetString(1),Mesh=Convert.ToUInt16(r[2]),MapID=id==Daniel?Home:MapId,X=Convert.ToUInt16(r[4]),Y=Convert.ToUInt16(r[5]),Type=Enums.NpcType.Talker});
            }}
            if(actors.Count!=52)throw new InvalidOperationException("Sky gate and guard catalog incomplete.");
            foreach(var npc in actors) {
                foreach(var m in Kernel.Maps.Values){INpc old;if(m.Npcs.TryGetValue(npc.UID,out old))m.RemoveNpc(old);}
                var map=Kernel.Maps[npc.MapID];ushort x=npc.X,y=npc.Y;
                if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Sky actor landing blocked.");
                npc.X=x;npc.Y=y;map.AddNpc(npc);
            }
            foreach(var challenge in Challenges) {
                ushort x=challenge.X,y=challenge.Y;
                if(!PotencyGuide.FindLanding(Kernel.Maps[MapId],ref x,ref y))throw new InvalidOperationException("Sky battle landing blocked.");challenge.X=x;challenge.Y=y;
            }
        }
        internal static void Travel(GameState p,int stage,int route,bool prize=false,bool heal=false) {
            ushort map=stage==0?Home:MapId,x,y;
            uint near=0;
            if(stage>0 && route>=0 && !prize){var challenge=At(stage,route);x=challenge.X;y=challenge.Y;}
            else {near=stage==0?Daniel:prize?Cloud:At(stage,0).Gate;var actor=Kernel.Maps[map].Npcs[near];x=actor.X;y=actor.Y;}
            bool found=false;var destination=Kernel.Maps[map];
            for(int radius=0;radius<=16 && !found;radius++)for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
                int tx=x+dx,ty=y+dy;
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius || !destination.Floor[tx,ty,MapObjectType.Player] || !destination.Floor[tx,ty,MapObjectType.Monster])continue;
                if(near!=0 && Kernel.GetDistance((ushort)tx,(ushort)ty,destination.Npcs[near].X,destination.Npcs[near].Y)>SkyPassQuest.DialogRange)continue;
                x=(ushort)tx;y=(ushort)ty;found=true;
            }
            if(!found)throw new InvalidOperationException("Sky destination blocked.");
            p.Entity.Teleport(map,x,y);
            if(p.Entity.MapID!=map || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Sky transition did not arrive.");
            if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}
        }
    }
}
