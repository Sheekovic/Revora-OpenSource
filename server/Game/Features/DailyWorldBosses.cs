using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class DailyWorldBosses
    {
        internal const uint Health = 100000000, RewardCp = 1000000, RewardRvp = 50000;
        internal const ulong RewardGold = 10000000;
        internal const int LootSeconds = 120;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static readonly object Gate = new object();
        private static readonly Random Random = new Random();
        internal sealed class Definition
        {
            internal int Hour;
            internal uint Template;
            internal string Name, Region;
            internal ushort Map, X, Y;
            internal Definition(int hour, uint template, string name, string region, ushort map, ushort x, ushort y)
            { Hour=hour; Template=template; Name=name; Region=region; Map=map; X=x; Y=y; }
        }
        internal static readonly Definition[] Bosses = {
            new Definition(3,39,"Moonfang King","Twin City outskirts",1002,438,664),
            new Definition(9,7486,"Dawnfire King","Phoenix Castle outskirts",1011,748,491),
            new Definition(15,4220,"Sunscar Tyrant","Ape Mountain",1020,616,507),
            new Definition(21,2719,"Nightveil King","Desert",1000,400,500)
        };
        private sealed class Run
        {
            internal string Key;
            internal int Slot, State;
            internal DateTime Planned, Until;
            internal uint Hp, Killer;
            internal ushort X,Y;
            internal bool Paid;
        }
        private sealed class ActiveBoss
        {
            internal Run Run;
            internal Entity Entity;
            internal bool DeathHandled;
        }
        private static ActiveBoss Active;
        private static uint[] SoulPool;
        private static readonly Dictionary<string, FloorItem> Ground = new Dictionary<string, FloorItem>();
        private static DateTime NextPoll, NextSave;
        private static DateTime ScheduledDay;
        private static bool Initialized;

        private static NpgsqlCommand Command(NpgsqlConnection c, string sql, params object[] args)
        {
            var q=new NpgsqlCommand(sql,c);
            for(int i=0;i<args.Length;i+=2) q.Parameters.AddWithValue((string)args[i],args[i+1]);
            return q;
        }
        private static int Execute(NpgsqlConnection c,string sql,params object[] args)
        { using(var q=Command(c,sql,args)) return q.ExecuteNonQuery(); }
        private static DateTime Utc(DateTime value) { return DateTime.SpecifyKind(value,DateTimeKind.Utc); }
        internal static DateTime PlannedUtc(DateTime day,int slot,int minute)
        {
            if(slot<0 || slot>=4 || minute<0 || minute>59) throw new ArgumentOutOfRangeException();
            return DateTime.SpecifyKind(day.Date.AddHours(Bosses[slot].Hour).AddMinutes(minute),DateTimeKind.Local).ToUniversalTime();
        }
        internal static void Create()
        {
            lock(Gate)
            {
                if(Initialized) return;
                SoulPool=ConquerItemInformation.BaseInformations.Values
                    .Where(i=>i.PurificationLevel==7 && i.ID>=800000 && i.ID<900000).Select(i=>i.ID).OrderBy(i=>i).ToArray();
                if(SoulPool.Length<4) throw new InvalidOperationException("Daily bosses require the P7 soul catalog");
                foreach(var b in Bosses)
                    if(!MonsterInformation.MonsterInformations.ContainsKey(b.Template) || !Kernel.Maps.ContainsKey(b.Map))
                        throw new InvalidOperationException("Daily boss template or map unavailable");
                using(var c=DataHolder.MySqlConnection)
                {
                    c.Open();
                    Execute(c,"CREATE TABLE IF NOT EXISTS revora_daily_boss_runs (runkey text PRIMARY KEY,slot integer NOT NULL CHECK(slot BETWEEN 0 AND 3),planned timestamp NOT NULL,untilutc timestamp NOT NULL,state integer NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 3),hp bigint NOT NULL DEFAULT 100000000,killer bigint NOT NULL DEFAULT 0,paid boolean NOT NULL DEFAULT false,x integer NOT NULL DEFAULT 0,y integer NOT NULL DEFAULT 0)");
                    Execute(c,"CREATE TABLE IF NOT EXISTS revora_daily_boss_loot (runkey text NOT NULL REFERENCES revora_daily_boss_runs(runkey),ordinal integer NOT NULL CHECK(ordinal BETWEEN 0 AND 3),itemid bigint NOT NULL,expires timestamp NOT NULL,claimedby bigint NOT NULL DEFAULT 0,PRIMARY KEY(runkey,ordinal))");
                }
                Initialized=true;
                NextPoll=NextSave=DateTime.MinValue;
                ScheduledDay=DateTime.MinValue;
            }
            Tick();
        }
        private static void EnsureDay(NpgsqlConnection c,DateTime now)
        {
            DateTime day=now.ToLocalTime().Date;
            if(day==ScheduledDay) return;
            using(var tx=c.BeginTransaction())
            {
                for(int slot=0;slot<4;slot++)
                {
                    DateTime planned=PlannedUtc(day,slot,Random.Next(60));
                    Execute(c,"INSERT INTO revora_daily_boss_runs(runkey,slot,planned,untilutc) VALUES(@key,@slot,@planned,@until) ON CONFLICT(runkey) DO NOTHING",
                        "key",day.ToString("yyyy-MM-dd")+"/"+slot,"slot",slot,"planned",planned,"until",planned.AddHours(2));
                }
                tx.Commit();
            }
            ScheduledDay=day;
        }
        private static List<Run> ReadRuns(NpgsqlConnection c,DateTime now)
        {
            var result=new List<Run>();
            using(var q=Command(c,"SELECT runkey,slot,planned,untilutc,state,hp,killer,paid,x,y FROM revora_daily_boss_runs WHERE untilutc>@from OR (state=2 AND NOT paid) ORDER BY planned","from",now.AddDays(-1)))
            using(var r=q.ExecuteReader()) while(r.Read()) result.Add(new Run {Key=r.GetString(0),Slot=r.GetInt32(1),Planned=Utc(r.GetDateTime(2)),Until=Utc(r.GetDateTime(3)),State=r.GetInt32(4),Hp=checked((uint)r.GetInt64(5)),Killer=checked((uint)r.GetInt64(6)),Paid=r.GetBoolean(7),X=checked((ushort)r.GetInt32(8)),Y=checked((ushort)r.GetInt32(9))});
            return result;
        }
        internal static void Tick()
        {
            if(!Initialized) return;
            lock(Gate)
            {
                DateTime now=Clock();
                if(now<NextPoll) return;
                NextPoll=now.AddSeconds(5);
                try
                {
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open(); EnsureDay(c,now);
                        if(Active!=null && now>=Active.Run.Until)
                        {
                            Execute(c,"UPDATE revora_daily_boss_runs SET state=3 WHERE runkey=@key AND state=1","key",Active.Run.Key);
                            Announce(Active.Entity.Name+" has withdrawn. The hunt is over."); RemoveActive();
                        }
                        Execute(c,"UPDATE revora_daily_boss_runs SET state=3 WHERE state IN (0,1) AND untilutc<=@now","now",now);
                        var runs=ReadRuns(c,now);
                        foreach(var run in runs)
                        {
                            // Recover a committed kill even when its connection failed at commit.
                            if(Active!=null && Active.Run.Key==run.Key && run.State>=2) RemoveActive();
                            if(run.State==2 && !run.Paid)
                            {
                                GameState player;
                                if(Kernel.GamePool.TryGetValue(run.Killer,out player)) TryPay(player,run.Key);
                            }
                            if(Active==null && run.State<2 && now>=run.Planned && now<run.Until) Spawn(c,run);
                        }
                        if(Active!=null && !Active.DeathHandled && now>=NextSave)
                        {
                            Execute(c,"UPDATE revora_daily_boss_runs SET hp=@hp,x=@x,y=@y WHERE runkey=@key AND state=1",
                                "hp",(long)Math.Max(1,Active.Entity.Hitpoints),"x",(int)Active.Entity.X,"y",(int)Active.Entity.Y,"key",Active.Run.Key);
                            NextSave=now.AddSeconds(30);
                        }
                        RestoreLoot(c,now);
                        WriteStatus(runs,now);
                    }
                }
                catch(Exception error) { Console.WriteLine("[daily-boss] tick_failure="+error.GetType().Name); }
            }
        }
        internal static bool Landing(Map map,ushort centerX,ushort centerY,out ushort x,out ushort y)
        {
            for(int radius=0;radius<=30;radius++)
                for(int dx=-radius;dx<=radius;dx++) for(int dy=-radius;dy<=radius;dy++)
                {
                    int tx=centerX+dx,ty=centerY+dy;
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius || !map.Floor[tx,ty,MapObjectType.Player] || !map.Floor[tx,ty,MapObjectType.Monster]) continue;
                    if(map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)tx,(ushort)ty)<12)) continue;
                    if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==map.ID && Kernel.GetDistance(p.Entity.X,p.Entity.Y,(ushort)tx,(ushort)ty)<5)) continue;
                    x=(ushort)tx;y=(ushort)ty;return true;
                }
            x=y=0; return false;
        }
        private static void Spawn(NpgsqlConnection c,Run run)
        {
            var b=Bosses[run.Slot]; var map=Kernel.Maps[b.Map]; ushort x,y;
            if(!Landing(map,run.State==1 && run.X!=0?run.X:b.X,run.State==1 && run.Y!=0?run.Y:b.Y,out x,out y)) return;
            if(Execute(c,"UPDATE revora_daily_boss_runs SET state=1,x=@x,y=@y WHERE runkey=@key AND state IN (0,1)","x",(int)x,"y",(int)y,"key",run.Key)!=1) return;
            var info=MonsterInformation.MonsterInformations[b.Template].Copy();
            info.ID=(uint)(9912000+run.Slot); info.Name=b.Name; info.Boss=true; info.Guard=info.Reviver=false;
            info.Level=140; info.Hitpoints=Health; info.MinAttack=6500; info.MaxAttack=7500; info.Defence=7000;
            info.AttackSpeed=2000; info.AttackRange=3; info.ViewRange=18; info.SpellID=0; info.RespawnTime=int.MaxValue/2;
            info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            var entity=new Entity(EntityFlag.Monster,false){MonsterInfo=info,Name=b.Name,UID=map.EntityUIDCounter.Next,MapID=b.Map,X=x,Y=y,
                MapObjType=MapObjectType.Monster,Body=info.Mesh,Level=info.Level,MinAttack=6500,MaxAttack=7500,MagicAttack=7500,Defence=7000,MagicDefence=40};
            info.Owner=entity; entity.MaxHitpoints=Health; entity.Hitpoints=Math.Max(1,Math.Min(Health,run.Hp)); entity.ApplyMonsterBossHud(); entity.SendUpdates=true;
            run.State=1;run.X=x;run.Y=y;
            Active=new ActiveBoss{Run=run,Entity=entity}; map.AddEntity2(entity);
            foreach(var p in Kernel.GamePool.Values) if(p!=null && p.Entity!=null && p.Entity.MapID==map.ID && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<Constants.nScreenDistance) entity.SendSpawn(p,false);
            Announce(b.Name+" has appeared in "+b.Region+" ("+x+","+y+")! Slay it before "+run.Until.ToLocalTime().ToString("HH:mm")+" to claim its bounty.");
        }
        internal static bool IsDaily(Entity entity)
        { return entity!=null && entity.EntityFlag==EntityFlag.Monster && entity.MonsterInfo!=null && entity.MonsterInfo.ID>=9912000 && entity.MonsterInfo.ID<9912004; }
        internal static bool TryHit(Entity attacker,Entity target,out uint damage)
        {
            damage=0;
            if(!IsDaily(attacker) || target==null || target.EntityFlag!=EntityFlag.Player || target.MapID!=attacker.MapID) return false;
            lock(Gate)
            {
                if(Active==null || !ReferenceEquals(Active.Entity,attacker) || Active.DeathHandled) return false;
                damage=(uint)Random.Next(6500,7501); return true;
            }
        }
        internal static bool Die(Entity monster,Entity killer)
        {
            if(!IsDaily(monster)) return false;
            lock(Gate)
            {
                if(Active==null || !ReferenceEquals(monster,Active.Entity) || Active.DeathHandled) return true;
                var run=Active.Run;
                if(Clock()>=run.Until) { NextPoll=DateTime.MinValue; Tick(); return true; }
                var player=killer==null?null:killer.Owner;
                bool eligible=player!=null && !player.Fake && player.Action==2 && killer.EntityFlag==EntityFlag.Player && killer.MapID==monster.MapID;
                bool itemLease=false;
                try
                {
                    // Combat can already own this lock. Never wait while owning Gate:
                    // scheduler payout uses the same nonblocking lock order.
                    if(eligible) {
                        if(player.ItemSyncRoot==null || !System.Threading.Monitor.TryEnter(player.ItemSyncRoot))return true;
                        itemLease=true;
                    }
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open(); using(var tx=c.BeginTransaction())
                        {
                            if(eligible)SkillTable.LockOwner(c,tx,player);
                            if(Execute(c,"UPDATE revora_daily_boss_runs SET state=@state,killer=@killer,hp=0,x=@x,y=@y WHERE runkey=@key AND state=1",
                                "state",eligible?2:3,"killer",eligible?(long)killer.UID:0L,"x",(int)monster.X,"y",(int)monster.Y,"key",run.Key)!=1) return true;
                            if(eligible) for(int i=0;i<4;i++)
                                if(Execute(c,"INSERT INTO revora_daily_boss_loot(runkey,ordinal,itemid,expires) VALUES(@key,@index,@item,@expires)",
                                    "key",run.Key,"index",i,"item",(long)SoulPool[Random.Next(SoulPool.Length)],"expires",Clock().AddSeconds(LootSeconds))!=1)
                                    throw new InvalidOperationException("Boss loot choice was not saved");
                            if(eligible)EquipmentAchievements.RecordUnarmedVictory(c,tx,player,monster,killer,"daily-boss:"+run.Key.Replace('/',':'));
                            tx.Commit();
                        }
                    }
                }
                catch(Exception error)
                {
                    Console.WriteLine("[daily-boss] death_commit_failure="+error.GetType().Name);
                    // The kill is not acknowledged without its durable outcome; permit a retry.
                    return true;
                }
                finally { if(itemLease)System.Threading.Monitor.Exit(player.ItemSyncRoot); }
                Active.DeathHandled=true;
                if(eligible) {
                    if(player.Entity.MyAchievement!=null)player.Entity.MyAchievement.Invalidate();
                    TryPay(player,run.Key);
                }
                try { monster.DieCore(killer); }
                finally { RemoveActive(); NextPoll=DateTime.MinValue; }
                if(eligible && QuestInventory.Player(player) && player.Entity.MyAchievement!=null
                    && System.Threading.Monitor.TryEnter(player.ItemSyncRoot)) {
                    try { player.Entity.MyAchievement.Send(); }
                    catch(Exception error) { QuestInventory.Fail(player,"daily-boss-achievement",error); }
                    finally { System.Threading.Monitor.Exit(player.ItemSyncRoot); }
                }
                if(eligible) Announce(killer.Name+" has slain "+monster.Name+"! The killer earns 1,000,000 CPs, 50,000 RVPs and 10,000,000 gold. Four bound P7 souls lie at the battlefield.");
                Tick(); return true;
            }
        }
        private static bool PlayerReady(GameState p)
        { return p!=null && !p.Fake && p.Action==2 && p.Entity!=null && !p.Entity.PersistenceRequiresReload && p.ItemSyncRoot!=null; }
        internal static bool TryPay(GameState player,string key)
        {
            if(!PlayerReady(player)) return false;
            // Combat may already own ItemSyncRoot when entering Gate. Polling must
            // never wait for that lock while holding Gate; unpaid awards retry later.
            if(!System.Threading.Monitor.TryEnter(player.ItemSyncRoot)) return false;
            try
            {
                if(!PlayerReady(player)) return false;
                var e=player.Entity;
                if(e.ConquerPoints>uint.MaxValue-RewardCp || e.RevoraPoint>uint.MaxValue-RewardRvp || e.Money>Revora.Game.Entity.MaximumSilver-RewardGold) return false;
                uint cp=e.ConquerPoints+RewardCp,rp=e.RevoraPoint+RewardRvp; ulong gold=e.Money+RewardGold;
                try
                {
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open(); using(var tx=c.BeginTransaction())
                        {
                            if(Execute(c,"UPDATE revora_daily_boss_runs SET paid=true WHERE runkey=@key AND state=2 AND killer=@uid AND NOT paid","key",key,"uid",(long)e.UID)!=1) return false;
                            SilverBalance.AssertSaved(c,tx,player);
                            if(Execute(c,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold WHERE uid=@uid AND conquerpoints=@before AND revorapoint=@beforeRvp",
                                "cp",(long)cp,"rp",(long)rp,"gold",(decimal)gold,"uid",(long)e.UID,"before",(long)e.ConquerPoints,"beforeRvp",(long)e.RevoraPoint)!=1) throw new InvalidOperationException("Boss award balance conflict");
                            tx.Commit();
                        }
                    }
                    e.ApplyCommittedConquerPoints(cp);e.ApplyCommittedRevoraPoints(rp);SilverBalance.ApplyCommitted(player,gold);
                    player.Send("Your boss bounty: 1,000,000 CPs, 50,000 RVPs and 10,000,000 gold."); return true;
                }
                catch(Exception error) { Fence(player,error);return false; }
            }
            finally { System.Threading.Monitor.Exit(player.ItemSyncRoot); }
        }
        private static void RemoveActive()
        {
            if(Active==null) return;
            var e=Active.Entity; var packet=new Data(true){UID=e.UID,ID=Data.RemoveEntity};
            e.MonsterInfo.SendScreen(packet); Kernel.Maps[e.MapID].RemoveEntity(e); Active=null;
        }
        private static void RestoreLoot(NpgsqlConnection c,DateTime now)
        {
            foreach(var pair in Ground.ToArray()) if(now>=pair.Value.DailyBossExpiresUtc || pair.Value.PickedUpAlready)
            {
                var item=pair.Value; Map map;
                if(Kernel.Maps.TryGetValue(item.MapID,out map) && map.FloorItems.ContainsKey(item.UID))
                { item.Type=FloorItem.Remove; map.RemoveFloorItem(item); foreach(var p in Kernel.GamePool.Values) if(p!=null && p.Entity!=null && p.Entity.MapID==map.ID) p.RemoveScreenSpawn(item,true); }
                Ground.Remove(pair.Key);
            }
            using(var q=Command(c,"SELECT l.runkey,l.ordinal,l.itemid,l.expires,r.slot,r.x,r.y,r.killer FROM revora_daily_boss_loot l JOIN revora_daily_boss_runs r USING(runkey) WHERE l.claimedby=0 AND l.expires>@now","now",now))
            using(var r=q.ExecuteReader()) while(r.Read())
            {
                string key=r.GetString(0);int ordinal=r.GetInt32(1);string lookup=key+"/"+ordinal;
                if(Ground.ContainsKey(lookup)) continue;
                uint id=(uint)r.GetInt64(2);DateTime expires=Utc(r.GetDateTime(3));var b=Bosses[r.GetInt32(4)];
                var map=Kernel.Maps[b.Map];ushort x=(ushort)r.GetInt32(5),y=(ushort)r.GetInt32(6);
                if(!map.SelectCoordonates(ref x,ref y)) continue;
                var info=ConquerItemInformation.BaseInformations[id];uint owner=(uint)r.GetInt64(7);GameState ownerPlayer;Kernel.GamePool.TryGetValue(owner,out ownerPlayer);
                var item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=id,Bound=true,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability,MobDropped=true,Color=Enums.Color.Orange};
                var drop=new FloorItem(true){UID=FloorItem.FloorUID.Next,MapID=b.Map,X=x,Y=y,Item=item,ItemID=id,Owner=ownerPlayer,OwnerUID=owner,
                    Type=FloorItem.Drop,MapObjType=MapObjectType.Item,ValueType=FloorItem.FloorValueType.Item,OnFloor=Time32.Now,ItemColor=item.Color,
                    DailyBossRun=key,DailyBossLootIndex=ordinal,DailyBossExpiresUtc=expires};
                map.AddFloorItem(drop);Ground.Add(lookup,drop);
                foreach(var p in Kernel.GamePool.Values) if(p!=null && p.Entity!=null && p.Entity.MapID==b.Map && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<Constants.nScreenDistance) drop.SendSpawn(p,false);
            }
        }
        internal static void Pickup(GameState player,FloorItem drop)
        {
            if(!PlayerReady(player) || drop==null || drop.DailyBossRun==null) return;
            lock(player.ItemSyncRoot) lock(drop)
            {
                var map=player.Map;FloorItem current;
                if(!PlayerReady(player) || player.Entity.Dead || player.Trade==null || player.Trade.InTrade || player.Booth!=null || map==null || map.ID!=drop.MapID
                    || player.Entity.X!=drop.X || player.Entity.Y!=drop.Y || Clock()>=drop.DailyBossExpiresUtc || drop.PickedUpAlready
                    || !map.FloorItems.TryGetValue(drop.UID,out current) || !ReferenceEquals(current,drop)) return;
                if(drop.OwnerUID!=player.Entity.UID && Time32.Now<drop.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    && !(player.Team!=null && player.Team.PickupItems && player.Team.IsTeammate(drop.OwnerUID))) { player.Send(Constants.FloorItemNotAvailable);return; }
                if(player.Inventory.Count>=40) { player.Send(Constants.FullInventory);return; }
                try
                {
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open();using(var tx=c.BeginTransaction())
                        {
                            if(Execute(c,"UPDATE revora_daily_boss_loot SET claimedby=@uid WHERE runkey=@key AND ordinal=@index AND itemid=@item AND claimedby=0 AND expires>@now",
                                "uid",(long)player.Entity.UID,"key",drop.DailyBossRun,"index",drop.DailyBossLootIndex,"item",(long)drop.Item.ID,"now",Clock())!=1) return;
                            if(Execute(c,"INSERT INTO items(uid,id,entityid,stacksize,durability,maximdurability,color,bound) VALUES(@uid,@id,@owner,1,@durability,@maximum,@color,1)",
                                "uid",(long)drop.Item.UID,"id",(int)drop.Item.ID,"owner",(long)player.Entity.UID,"durability",(int)drop.Item.Durability,"maximum",(int)drop.Item.MaximDurability,"color",(int)drop.Item.Color)!=1)
                                throw new InvalidOperationException("Boss soul was not saved");
                            tx.Commit();
                        }
                    }
                    drop.PickedUpAlready=true;drop.Type=FloorItem.Remove;map.RemoveFloorItem(drop);
                    if(!player.Inventory.Add(drop.Item,Enums.ItemUse.None)) throw new InvalidOperationException("Committed boss loot requires reload");
                    drop.Item.Send(player);player.RemoveScreenSpawn(drop,true);player.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[drop.Item.ID].Name));
                }
                catch(Exception error) { Fence(player,error); }
            }
        }
        private static void Fence(GameState player,Exception error)
        {
            Console.WriteLine("[daily-boss] award_failure uid="+player.Entity.UID+" type="+error.GetType().Name);
            player.Entity.PersistenceRequiresReload=true;player.Action=0;player.Send("Please reconnect to collect your boss reward safely.");player.Disconnect();
        }
        private static void Announce(string text)
        { Kernel.SendWorldMessage(new Message(text,Color.Gold,Message.Center),Server.GamePool); }
        private static void WriteStatus(List<Run> runs,DateTime now)
        {
            string path="database/daily-boss-status.json";
            string rows=string.Join(",",runs.Where(r=>r.Planned.ToLocalTime().Date==now.ToLocalTime().Date).Select(r=>"{\"name\":\""+Bosses[r.Slot].Name+"\",\"plannedUtc\":\""+r.Planned.ToString("o")+"\",\"state\":"+r.State+"}"));
            string json="{\"utc\":\""+now.ToString("o")+"\",\"timeZone\":\""+TimeZoneInfo.Local.Id+"\",\"activeUid\":"+(Active==null?0:Active.Entity.UID)+",\"activeHp\":"+(Active==null?0:Active.Entity.Hitpoints)+",\"runs\":["+rows+"]}";
            System.IO.File.WriteAllText(path+".pending",json);
            if(System.IO.File.Exists(path))System.IO.File.Replace(path+".pending",path,null);else System.IO.File.Move(path+".pending",path);
        }
    }
}
