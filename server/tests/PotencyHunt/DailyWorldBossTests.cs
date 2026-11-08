using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ProtoBuf;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console = System.Console;

internal static partial class PotencyHuntTests
{
    static readonly Type Daily = typeof(Entity).Assembly.GetType("Revora.Game.Features.DailyWorldBosses");
    static DateTime dailyNow;
    static Entity DailyActive()
    {
        var active=Daily.GetField("Active",Hidden).GetValue(null);
        return active==null?null:(Entity)active.GetType().GetField("Entity",Instance).GetValue(active);
    }
    static void DailyTick(DateTime now)
    { dailyNow=now; Daily.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue); Call(Daily,null,"Tick"); }
    static void DailyFailure(GameState player, Action action)
    {
        // Exercise real fencing/disconnect without starting production timers.
        typeof(Screen).GetField("DisposalSyncRoot",Instance).SetValue(player.Screen,new object());
        var previous=Server.Thread;
        if(previous==null)Server.Thread=(Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        try { action(); } finally { Server.Thread=previous; }
    }
    static DateTime[] DailySchedule()
    {
        using(var c=DataHolder.MySqlConnection) {
            c.Open();using(var q=new NpgsqlCommand("SELECT planned FROM revora_daily_boss_runs ORDER BY planned",c))
            using(var r=q.ExecuteReader()) {
                var list=new System.Collections.Generic.List<DateTime>();
                while(r.Read()) list.Add(DateTime.SpecifyKind(r.GetDateTime(0),DateTimeKind.Utc)); return list.ToArray();
            }
        }
    }
    static void BossHealthPackets(Entity boss)
    {
        foreach(uint hp in new[]{boss.MaxHitpoints,boss.MaxHitpoints/2,1u,0u})
        {
            boss.Hitpoints=hp;boss.ApplyMonsterBossHud();
            using(var buffer=new MemoryStream()) {
                Serializer.Serialize(buffer,boss.CharBuffer);buffer.Position=0;
                var decoded=Serializer.Deserialize<Entity.CMsgPlayer>(buffer);
                Check(decoded._Hitpoints==hp && decoded._Boss==1,boss.Name+" spawn carries actual HP "+hp);
            }
            var packet=(FlagsUpdate)Call(typeof(Entity),boss,"MonsterHealthUpdate",hp);
            var bytes=packet.ToArray();
            using(var buffer=new MemoryStream(bytes,4,bytes.Length-12)) {
                var decoded=Serializer.Deserialize<FlagsUpdate.CMsgUserAttrib>(buffer);
                Check(decoded.UID==boss.UID && decoded.Values.Single().value==hp,
                    boss.Name+" damage update carries actual HP "+hp);
            }
        }
        boss.Hitpoints=boss.MaxHitpoints;
    }
    static void VerifyDailyBosses(string runtime)
    {
        var info=MonsterInformation.MonsterInformations[2719].Copy();
        var specter=new Entity(EntityFlag.Monster,false){UID=490001,MonsterInfo=info,Name=info.Name,MaxHitpoints=5000000};
        info.Owner=specter;info.Boss=true;BossHealthPackets(specter);
        var rush=typeof(Revora.Game.Features.BossRushSystem);
        foreach(var definition in (Array)rush.GetField("Bosses",Hidden).GetValue(null)) {
            var type=definition.GetType();
            uint id=(uint)type.GetField("Id").GetValue(definition);
            var template=MonsterInformation.MonsterInformations[id].Copy();template.Boss=true;
            var entity=new Entity(EntityFlag.Monster,false){UID=490002,MonsterInfo=template,
                Name=(string)type.GetField("Name").GetValue(definition),MaxHitpoints=(uint)type.GetField("Hp").GetValue(definition)};
            template.Owner=entity;BossHealthPackets(entity);
        }
        foreach(ushort id in new ushort[]{1011,1020,1000}) World(runtime,id);
        var ids=new ushort[]{1002,1011,1020,1000};
        var spawnX=new ushort[]{438,748,616,400};
        var spawnY=new ushort[]{664,491,507,500};
        var names=new[]{"Moonfang King","Dawnfire King","Sunscar Tyrant","Nightveil King"};
        dailyNow=new DateTime(2030,1,12,0,0,0,DateTimeKind.Local).ToUniversalTime();
        Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));
        Call(Daily,null,"Create");
        var schedule=DailySchedule();
        Check(schedule.Length==4 && schedule.Select(t=>t.ToLocalTime().Hour).SequenceEqual(new[]{3,9,15,21}),"four durable random-minute daily windows use server local time");
        Check(DailyActive()==null,"no daily boss spawns before its window");
        Daily.GetField("Initialized",Hidden).SetValue(null,false);Call(Daily,null,"Create");
        Check(DailySchedule().SequenceEqual(schedule),"restart preserves all chosen spawn minutes");
        for(int slot=0;slot<4;slot++)
        {
            DailyTick(schedule[slot]);
            var boss=DailyActive();var map=Kernel.Maps[ids[slot]];
            Check(boss!=null && boss.Name==names[slot] && boss.MapID==map.ID && map.Entities.ContainsKey(boss.UID),names[slot]+" spawns once on its real map");
            Check(map.Floor[boss.X,boss.Y,MapObjectType.Player] && boss.MaxHitpoints==100000000 && boss.Hitpoints==100000000 && boss.Boss==1,
                names[slot]+" has valid terrain, 100M real HP and boss HUD flag");
            Console.WriteLine("Landing: "+boss.Name+" map="+map.ID+" x="+boss.X+" y="+boss.Y);
            Check(boss.X==spawnX[slot] && boss.Y==spawnY[slot],names[slot]+" uses the requested spawn coordinates");
            BossHealthPackets(boss);
            var player=Player(map);player.Entity.X=boss.X;player.Entity.Y=boss.Y;
            for(int i=0;i<100;i++) {
                object[] args={boss,player.Entity,0u};
                if(!(bool)Call(Daily,null,"TryHit",args) || (uint)args[2]<6500 || (uint)args[2]>7500) throw new Exception("Daily boss damage range");
            }
            Check(true,names[slot]+" hits ordinary players for 6500-7500 before later combat effects");
            if(slot==0)
            {
                boss.Hitpoints=45678901;DailyTick(schedule[slot].AddSeconds(35));
                Call(Daily,null,"RemoveActive");Daily.GetField("Initialized",Hidden).SetValue(null,false);Call(Daily,null,"Create");
                boss=DailyActive();
                Check(boss!=null && boss.Hitpoints==45678901 && DailySchedule().SequenceEqual(schedule),"active boss resumes checkpoint HP after restart");
            }
            string key=schedule[slot].ToLocalTime().ToString("yyyy-MM-dd")+"/"+slot;
            uint cp=player.Entity.ConquerPoints,rp=player.Entity.RevoraPoint;ulong gold=player.Entity.Money;
            if(slot==2)
            {
                dailyNow=schedule[slot].AddHours(2);
                Call(Daily,null,"Die",boss,player.Entity);
                Check(DailyActive()==null && player.Entity.ConquerPoints==cp && Scalar("SELECT count(*) FROM revora_daily_boss_loot WHERE runkey='2030-01-12/2'",0)==0,
                    "a kill at the two-hour deadline expires without bounty or loot");
            }
            else if(slot==3)
            {
                DailyTick(schedule[slot].AddHours(2));
                Check(DailyActive()==null && !map.Entities.ContainsKey(boss.UID),"undefeated boss is removed after two hours");
                DailyTick(schedule[slot].AddHours(2).AddMinutes(1));
                Check(DailyActive()==null,"expired boss cannot respawn through scheduler");
            }
            else
            {
                // Exercise lock contention: scheduler must not wait for inventory while owning its gate.
                var held=new ManualResetEventSlim(false);var release=new ManualResetEventSlim(false);
                var holder=Task.Run(()=>{lock(player.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    var pending=Task.Run(()=>Call(Daily,null,"TryPay",player,key));
                    Check(pending.Wait(2000) && !(bool)pending.Result,"busy inventory defers payout without lock inversion");
                } finally {release.Set();holder.Wait();held.Dispose();release.Dispose();}
                if(slot==0) {
                    Query("ALTER TABLE revora_daily_boss_loot ADD CONSTRAINT fixture_reject_loot CHECK(itemid<0)");
                    Call(Daily,null,"Die",boss,player.Entity);
                    Check(DailyActive()==boss && player.Entity.ConquerPoints==cp && Scalar("SELECT count(*) FROM revora_daily_boss_loot",0)==0,
                        "failed kill transaction leaves no bounty or partial loot and permits retry");
                    Query("ALTER TABLE revora_daily_boss_loot DROP CONSTRAINT fixture_reject_loot");
                    Query("CREATE FUNCTION suppress_daily_loot() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.ordinal=2 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_daily_loot BEFORE INSERT ON revora_daily_boss_loot FOR EACH ROW EXECUTE FUNCTION suppress_daily_loot()");
                    try { Call(Daily,null,"Die",boss,player.Entity); }
                    finally { Query("DROP TRIGGER suppress_daily_loot ON revora_daily_boss_loot;DROP FUNCTION suppress_daily_loot()"); }
                    Check(DailyActive()==boss && player.Entity.ConquerPoints==cp
                        && Scalar("SELECT count(*) FROM revora_daily_boss_loot",0)==0
                        && Scalar("SELECT state FROM revora_daily_boss_runs WHERE runkey='2030-01-12/0'",0)==1,
                        "silently suppressed third soul rolls back the kill and every earlier loot record");
                }
                if(slot==1) {
                    player.Entity.Money=Entity.MaximumSilver;Call(Daily,null,"Die",boss,player.Entity);
                    Check(!Fenced(player) && player.Entity.ConquerPoints==cp && !(bool)Call(Daily,null,"TryPay",player,key),
                        "daily boss bounty waits when its silver would exceed durable storage");
                    player.Entity.Money=gold;
                    Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)player.Entity.UID);
                    DailyFailure(player,()=>Call(Daily,null,"TryPay",player,key));
                    Check(Fenced(player) && player.Entity.ConquerPoints==cp && player.Entity.RevoraPoint==rp
                        && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",player.Entity.UID)==rp+7
                        && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE runkey='2030-01-12/1' AND paid",0)==0,
                        "stale RVP balance leaves the newer saved credit and unpaid bounty intact");
                    DemonReset(player);player.Entity.RevoraPoint=rp+7;rp+=7;
                    Call(Daily,null,"TryPay",player,key);
                }
                Call(Daily,null,"Die",boss,player.Entity);
                Call(Daily,null,"Die",boss,player.Entity);
                Check(DailyActive()==null && player.Entity.ConquerPoints==cp+1000000 && player.Entity.RevoraPoint==rp+50000 && player.Entity.Money==gold+10000000,
                    "ordinary killer receives exactly one 1M CP / 50K RVP / 10M gold bounty");
                Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",player.Entity.UID)==player.Entity.ConquerPoints
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",player.Entity.UID)==player.Entity.RevoraPoint
                    && (ulong)Scalar("SELECT money FROM entities WHERE uid=@uid",player.Entity.UID)==player.Entity.Money,"all three boss currencies persist together");
                EntityTable.UpdateMoney(player);
                Check(!Fenced(player),"boss payout advances the durable silver preimage for the next save");
                Parallel.For(0,12,i=>Call(Daily,null,"TryPay",player,key));
                Check(player.Entity.ConquerPoints==cp+1000000,"parallel reward replay does not pay twice");
                var drops=map.FloorItems.Values.Where(d=>(string)typeof(FloorItem).GetField("DailyBossRun",Instance).GetValue(d)==key).ToArray();
                Check(drops.Length==4 && drops.All(d=>ConquerItemInformation.BaseInformations[d.Item.ID].PurificationLevel==7),"exactly four real random P7 souls appear on the ground");
                if(slot==0) {
                    var originalIds=drops.Select(d=>d.Item.ID).OrderBy(id=>id).ToArray();
                    foreach(var drop in drops) map.RemoveFloorItem(drop);
                    ((System.Collections.IDictionary)Daily.GetField("Ground",Hidden).GetValue(null)).Clear();
                    Daily.GetField("Initialized",Hidden).SetValue(null,false);Call(Daily,null,"Create");
                    drops=map.FloorItems.Values.Where(d=>(string)typeof(FloorItem).GetField("DailyBossRun",Instance).GetValue(d)==key).ToArray();
                    Check(drops.Length==4 && drops.Select(d=>d.Item.ID).OrderBy(id=>id).SequenceEqual(originalIds),
                        "restart restores the same four unclaimed souls without rerolling them");
                }
                var stranger=Player(map);stranger.Entity.X=drops[0].X;stranger.Entity.Y=drops[0].Y;
                Call(Daily,null,"Pickup",stranger,drops[0]);
                Check(stranger.Inventory.Count==0 && !drops[0].PickedUpAlready,"ordinary non-owner cannot steal newly dropped soul");
                int initial=player.Inventory.Count;
                if(slot==0) {
                    player.Entity.X=drops[0].X;player.Entity.Y=drops[0].Y;
                    Query("CREATE FUNCTION suppress_daily_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_daily_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_daily_item()");
                    try { DailyFailure(player,()=>Call(Daily,null,"Pickup",player,drops[0])); }
                    finally { Query("DROP TRIGGER suppress_daily_item ON items;DROP FUNCTION suppress_daily_item()"); }
                    Check(Fenced(player) && !drops[0].PickedUpAlready && player.Inventory.Count==initial
                        && Scalar("SELECT count(*) FROM revora_daily_boss_loot WHERE claimedby=@uid",player.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",player.Entity.UID)==initial,
                        "silently suppressed soul inventory insert rolls back its claim and keeps floor loot available");
                    DemonReset(player);
                }
                foreach(var drop in drops) {
                    player.Entity.X=drop.X;player.Entity.Y=drop.Y;
                    Call(Daily,null,"Pickup",player,drop);Call(Daily,null,"Pickup",player,drop);
                }
                Check(player.Inventory.Count==initial+4 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",player.Entity.UID)==initial+4,
                    "four soul pickups persist for ordinary player and duplicate clicks do not duplicate items");
                Daily.GetField("Initialized",Hidden).SetValue(null,false);Call(Daily,null,"Create");
                Check(DailyActive()==null && player.Entity.ConquerPoints==cp+1000000 && map.FloorItems.Values.All(d=>!drops.Contains(d)),
                    "restart cannot recreate defeated boss, paid bounty or claimed loot");
            }
        }
        Check(Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE state IN(2,3)",0)==4,"all four daily runs finish durably");
        Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
    }
}
