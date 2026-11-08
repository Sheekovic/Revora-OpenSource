using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owner-approved Revora expedition; these are not reconstructed original quest rules.
    internal static class ForgottenRealms
    {
        internal const uint Guide=900000196,Family=9926300,Rune=4030101;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action BeforeCommit=()=>{};
        private static readonly ushort[,] Sites={{290,294},{278,278},{340,340},{280,280},{338,338}};
        private static readonly string[] Names={"Deserted Wild","Flame Plain","Death Cave","Asura Hell","Corpse Forest"};
        private static readonly string[] Hashes={
            "afa0c18de59682432e94fe41be52aa29f707da6bc400f3a4d636de3703ce557a",
            "4b49c6f7de8ac2ad43ea4229fe7d3945097767cf5f42f61546ecd1e2fc27fdc9",
            "0cd3ce585c1192429169a5c480473f0242c2a105b25ff90b73ed0b4eedab21c5",
            "a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd",
            "58602738ab34ee02779ffd1ab4eede5dab092542ab8836a2985791b4f810eb23"};
        private static readonly Dictionary<ushort,Map> Worlds=new Dictionary<ushort,Map>();
        private static readonly HashSet<Entity> Sentries=new HashSet<Entity>();
        private static readonly ConditionalWeakTable<Entity,object> Deaths=new ConditionalWeakTable<Entity,object>();
        private static bool initialized;
        internal static void Initialize()
        {
            if(initialized)return;
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Rune) || ConquerItemInformation.BaseInformations[Rune].Class!=0)
                throw new InvalidOperationException("Forgotten Realms rune catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_forgotten_realms(
uid bigint NOT NULL CHECK(uid>0),map integer NOT NULL CHECK(map BETWEEN 1360 AND 1364),week date NOT NULL,
kills integer NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 10),accepted_at timestamptz NOT NULL,
reward_uid bigint UNIQUE,claimed_at timestamptz,PRIMARY KEY(uid,map,week),
CHECK((reward_uid IS NULL AND claimed_at IS NULL) OR (reward_uid IS NOT NULL AND claimed_at IS NOT NULL AND kills=10)));");}
            for(int i=0;i<5;i++){
                ushort id=(ushort)(1360+i);string path=Path.Combine(Constants.DMapsPath,"maps",id+".map");
                // Missing staged terrain keeps that destination closed, without fabricating a floor.
                if(!File.Exists(path)){Console.WriteLine("[forgotten-realms] map="+id+" closed=terrain-missing");continue;}
                using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))
                    if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=Hashes[i])
                        throw new InvalidOperationException("Forgotten Realms terrain changed.");
                Map map;MapsTable.MapInformation profile;
                if(!Kernel.Maps.TryGetValue(id,out map) || map.BaseID!=id || !MapsTable.MapInformations.TryGetValue(id,out profile)
                    || profile.BaseID!=id || profile.Owner!=0 || !map.Floor[Sites[i,0],Sites[i,1],MapObjectType.Player])
                    throw new InvalidOperationException("Forgotten Realms world unavailable.");
                if(map.Npcs.ContainsKey(Guide))throw new InvalidOperationException("Forgotten Realms guide identity collision.");
                var cells=PotencyHunt.SpawnCells(map.Floor,Sites[i,0],Sites[i,1],25).Where(v=>map.Floor[v.X,v.Y,MapObjectType.Monster] && Kernel.GetDistance((ushort)v.X,(ushort)v.Y,Sites[i,0],Sites[i,1])>=12).Take(10).ToArray();
                if(cells.Length!=10)throw new InvalidOperationException("Forgotten Realms connected hunting ground unavailable.");
                map.AddNpc(new NpcSpawn{UID=Guide,MapID=id,X=(ushort)(Sites[i,0]-2),Y=Sites[i,1],Mesh=50050,Name="TrailKeeper",Type=Enums.NpcType.Talker});
                foreach(var cell in cells){
                    var info=MonsterInformation.MonsterInformations[1413].Copy();
                    info.ID=Family+(uint)i;info.Name="LostSentry";info.Boss=info.Guard=info.Reviver=false;info.OwnItemRate=0;
                    info.Hitpoints=100000;info.MinAttack=1200;info.MaxAttack=1800;info.AttackRange=1;info.ViewRange=8;
                    info.SpellID=0;info.AttackType=1;info.MoveSpeed=800;info.RunSpeed=500;info.AttackSpeed=1500;
                    info.RespawnTime=45;info.BoundX=(ushort)cell.X;info.BoundY=(ushort)cell.Y;info.BoundCX=info.BoundCY=1;
                    var mob=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=id,X=(ushort)cell.X,Y=(ushort)cell.Y,
                        MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                        MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
                    while(map.Entities.ContainsKey(mob.UID) || Kernel.GamePool.ContainsKey(mob.UID))mob.UID=map.EntityUIDCounter.Next;
                    info.Owner=mob;mob.Hitpoints=mob.MaxHitpoints=info.Hitpoints;mob.SendUpdates=true;map.AddEntity2(mob);Sentries.Add(mob);
                }
                map.EnsureMonsterTimer();Worlds.Add(id,map);
            }
            initialized=true;
        }
        private static DateTime Week(DateTime now)
        {
            if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Expedition clock requires UTC.");
            return now.Date.AddDays(-((int)now.DayOfWeek+6)%7);
        }
        private static bool World(ushort id,out Map map)
        {
            Map current;return Worlds.TryGetValue(id,out map) && Kernel.Maps.TryGetValue(id,out current) && ReferenceEquals(map,current);
        }
        internal static void Offer(Npcs d){if(initialized)d.Option("Forgotten Realms expedition.",10);}
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(!initialized || p==null || p.Entity==null || request==null)return false;
            bool town=p.ActiveNpc==PathHuntSystem.NpcId;
            if(!town && p.ActiveNpc!=Guide || town && (request.OptionID<10 || request.OptionID>15))return false;
            ushort source=town?(ushort)1002:p.Entity.MapID;Map map;
            if(!town && !World(source,out map))return true;
            if(!Q.Near(p,p.ActiveNpc,source) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(!Q.Near(p,p.ActiveNpc,source))return true;
                if(!town && request.OptionID==1){p.Entity.Teleport(1002,PathHuntSystem.ClaimX,PathHuntSystem.ClaimY);return true;}
                if(town && request.OptionID>=11 && request.OptionID<=15){
                    ushort target=(ushort)(1360+request.OptionID-11);
                    if(p.Entity.Level<100){p.Send("Reach level 100 before taking this trail.");return true;}
                    if(!World(target,out map)){p.Send("That trail is closed while its passage is repaired.");return true;}
                    int i=target-1360;var now=Clock();
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        Q.Execute(c,t,@"INSERT INTO revora_forgotten_realms(uid,map,week,accepted_at) VALUES(@uid,@map,(@week AT TIME ZONE 'UTC')::date,@now)
ON CONFLICT(uid,map,week) DO NOTHING","uid",(long)p.Entity.UID,"map",(int)target,"week",Week(now),"now",now);
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_forgotten_realms WHERE uid=@uid AND map=@map AND week=(@week AT TIME ZONE 'UTC')::date)","uid",(long)p.Entity.UID,"map",(int)target,"week",Week(now)))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Expedition admission was not saved.");
                        if(!Q.Near(p,PathHuntSystem.NpcId,1002))return true;t.Commit();
                    }}
                    p.Entity.Teleport(target,Sites[i,0],Sites[i,1]);return true;
                }
                string note=!town && request.OptionID==2?Claim(p,source):null;
                if(!Q.Player(p))return true;
                var d=new Npcs(p);
                if(town){d.Text("Five forgotten lands await. Reach level 100, then slay ten Lost Sentries in each land for a bound rune. Each land pays once a week, resetting Monday at midnight UTC. Trail Keepers will bring you home.");
                    for(int i=0;i<5;i++)d.Option(Names[i],(byte)(11+i));}
                else{int kills=0;bool paid=false;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,
                    "SELECT kills,reward_uid IS NOT NULL FROM revora_forgotten_realms WHERE uid=@uid AND map=@map AND week=(@week AT TIME ZONE 'UTC')::date","uid",(long)p.Entity.UID,"map",(int)source,"week",Week(Clock())))using(var r=q.ExecuteReader())if(r.Read()){kills=r.GetInt32(0);paid=r.GetBoolean(1);}}
                    d.Text(note??(paid?"This week's reward is yours. Shall we return?":"Lost Sentries defeated: "+kills+"/10. Slay ten, then collect your rune. If the week has turned, speak to Rowan again."));
                    if(!paid)d.Option("Claim my rune.",2);d.Option("Return to Rowan.",1);}
                d.Option("Farewell.",255);d.Send();
            }}catch(Exception ex){Q.Fail(p,"forgotten-realms",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        private static string Claim(GameState p,ushort map)
        {
            ConquerItem gift;var now=Clock();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Expedition owner missing.");
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_forgotten_realms WHERE uid=@uid AND map=@map AND week=(@week AT TIME ZONE 'UTC')::date AND kills=10 AND reward_uid IS NULL)",
                    "uid",(long)p.Entity.UID,"map",(int)map,"week",Week(now)))if(!Equals(q.ExecuteScalar(),true))return "Finish your hunt first. Each land pays once this week.";
                var bag=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())while(r.Read())bag.Add(Convert.ToUInt32(r[0]));
                if(!bag.SequenceEqual(p.Inventory.Objects.Select(v=>v.UID).OrderBy(v=>v)))throw new InvalidOperationException("Expedition inventory changed.");
                if(bag.Count>=40)return "Make one space in your bag first.";
                gift=Q.New(Rune,true);Q.Insert(c,t,p.Entity.UID,gift);
                if(Q.Execute(c,t,"UPDATE revora_forgotten_realms SET reward_uid=@gift,claimed_at=@now WHERE uid=@uid AND map=@map AND week=(@week AT TIME ZONE 'UTC')::date AND kills=10 AND reward_uid IS NULL",
                    "gift",(long)gift.UID,"now",now,"uid",(long)p.Entity.UID,"map",(int)map,"week",Week(now))!=1)throw new InvalidOperationException("Expedition reward was not saved.");
                BeforeCommit();if(!Q.Near(p,Guide,map))throw new InvalidOperationException("Expedition claimant changed.");t.Commit();
            }}
            Q.Install(p,new uint[0],new[]{gift});Q.Refresh(p);return "Well fought. Your bound rune is yours.";
        }
        internal static void Revived(Entity mob){if(mob!=null && !mob.Dead)lock(mob)Deaths.Remove(mob);}
        internal static void OnDeath(GameState p,Entity mob)
        {
            if(!initialized || !Q.Player(p) || mob==null || !Sentries.Contains(mob) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(mob){
                Map map;Entity current;object prior;
                if(!Q.Player(p) || p.Entity.Level<100 || !mob.Dead || mob.Companion || mob.MonsterInfo==null || !ReferenceEquals(mob.MonsterInfo.Owner,mob)
                    || !ReferenceEquals(mob.Killer,p.Entity) || !World(mob.MapID,out map) || !ReferenceEquals(p.Map,map) || !map.Entities.TryGetValue(mob.UID,out current) || !ReferenceEquals(current,mob)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,mob.X,mob.Y)>Constants.pScreenDistance || Deaths.TryGetValue(mob,out prior))return;
                var now=Clock();using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"UPDATE revora_forgotten_realms SET kills=kills+1
WHERE uid=@uid AND map=@map AND week=(@week AT TIME ZONE 'UTC')::date AND kills<10 AND reward_uid IS NULL AND accepted_at<=@now",
                    "uid",(long)p.Entity.UID,"map",(int)map.ID,"week",Week(now),"now",now);}
                Deaths.Add(mob,new object());
            }}catch(Exception ex){Q.Fail(p,"forgotten-realms-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
