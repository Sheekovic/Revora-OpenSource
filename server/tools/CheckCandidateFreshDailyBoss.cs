using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static string FreshBossPrior(GameState p,Func<string> prior)
    {
        return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'paid',(SELECT json_agg(r ORDER BY sequence) FROM revora_reincarnations r WHERE uid=@uid),'dew',(SELECT json_agg(r ORDER BY created_at) FROM revora_oblivion_purchases r WHERE uid=@uid),'recovery',(SELECT json_agg(r) FROM reincarnation r WHERE uid=@uid))::text","prior",prior(),"uid",(long)p.Entity.UID));
    }
    static int FreshBossMedicine(GameState p)
    { return p.Inventory.Objects.Where(i=>i.ID==1000040).Sum(i=>Math.Max(1,(int)i.StackSize)); }

    static void FreshBossSupply(GameState p)
    {
        p.BindWorldMap(1002);var npc=p.Map.Npcs[3];p.Entity.X=npc.X;p.Entity.Y=(ushort)(npc.Y+1);
        int bought=0;
        while(p.Inventory.Count<40 && bought<200){
            int count=FreshBossMedicine(p);ulong silver=p.Entity.Money;
            var packet=new ItemUsage(true){UID=3,dwParam=1000040,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);
            Require(FreshBossMedicine(p)==count+1 && p.Entity.Money==silver-1000 && !PirateFenced(p));
            Sql("UPDATE revora_fresh_daily_boss_probe SET bought=bought+1");bought++;
        }
        Require(bought>0);Sql("UPDATE revora_fresh_daily_boss_probe SET trips=trips+1");
    }
    static void FreshBossHeal(GameState p)
    {
        var item=p.Inventory.Objects.First(i=>i.ID==1000040);
        int count=FreshBossMedicine(p);uint hp=p.Entity.Hitpoints;
        ItemHandler.UseItem(item,p);
        Require(!PirateFenced(p) && FreshBossMedicine(p)==count-1 && p.Entity.Hitpoints==Math.Min(p.Entity.MaxHitpoints,hp+7000));
        Sql("UPDATE revora_fresh_daily_boss_probe SET used=used+1");
    }
    static void FreshDailyBossCombat(GameState p,Func<string> prior,string phase)
    {
        var daily=Feature("DailyWorldBosses");
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'run',(SELECT json_agg(r) FROM revora_daily_boss_runs r WHERE runkey=(SELECT run FROM revora_fresh_daily_boss_probe)),'loot',(SELECT json_agg(r ORDER BY ordinal) FROM revora_daily_boss_loot r WHERE runkey=(SELECT run FROM revora_fresh_daily_boss_probe)))::text","prior",FreshBossPrior(p,prior)));
        Action save=()=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_daily_boss_probe SET snapshot=@snapshot","snapshot",snapshot());};
        if((bool)Sql("SELECT to_regclass('revora_fresh_daily_boss_probe') IS NULL")){
            Require(phase.EndsWith("prepare") && FreshBossPrior(p,prior)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_paid_transition_probe WHERE stage=3")));
            Require(p.Entity.Hitpoints==p.Entity.MaxHitpoints && p.Entity.Hitpoints>15000 && FreshBossMedicine(p)==0 && p.Inventory.Count<=40);
            Sql("CREATE TABLE revora_fresh_daily_boss_probe(stage int,run text,cp bigint,rvp bigint,silver bigint,bought int DEFAULT 0,used int DEFAULT 0,trips int DEFAULT 0,hits int DEFAULT 0,retaliations int DEFAULT 0,damage bigint DEFAULT 0,received bigint DEFAULT 0,elapsed_ms bigint DEFAULT 0,min_hp bigint,snapshot text);INSERT INTO revora_fresh_daily_boss_probe(stage,cp,rvp,silver,min_hp) VALUES(0,@cp,@rvp,@silver,@hp)","cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"silver",(long)p.Entity.Money,"hp",(long)p.Entity.Hitpoints);save();
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_boss_probe")));
        int stage=(int)Sql("SELECT stage FROM revora_fresh_daily_boss_probe");
        if(phase.EndsWith("prepare")){
            Require(stage==0);
            while(p.Inventory.Count>20){
                var bank=p.Warehouses.First(w=>w.Value.Count<w.Value.MaxCount);uint bankId=(uint)bank.Key;
                var map=Kernel.Maps.Values.First(m=>m.ID==m.BaseID && m.Npcs.ContainsKey(bankId));p.BindWorldMap(map.ID);
                var npc=map.Npcs[bankId];p.Entity.X=npc.X;p.Entity.Y=(ushort)(npc.Y+1);
                var item=p.Inventory.Objects.First(i=>i.Days==0 && i.ID!=727990);
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);
                Revora.Network.Writer.WriteUInt32(bankId,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);
                Require(!p.Inventory.ContainsUID(item.UID));save();
            }
            FreshBossSupply(p);Sql("UPDATE revora_fresh_daily_boss_probe SET stage=1");save();
        }else if(phase.EndsWith("combat")){
            Require(stage==1);
            DateTime now=DateTime.SpecifyKind((DateTime)Sql("SELECT max(planned) FROM revora_daily_boss_runs"),DateTimeKind.Utc).ToLocalTime().Date.AddDays(1).ToUniversalTime();
            daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            daily.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue);Call(daily,"Tick");
            string run=Convert.ToString(Sql("SELECT runkey FROM revora_daily_boss_runs WHERE planned>=@now AND state=0 ORDER BY planned LIMIT 1","now",now));
            DateTime planned=DateTime.SpecifyKind((DateTime)Sql("SELECT planned FROM revora_daily_boss_runs WHERE runkey=@run","run",run),DateTimeKind.Utc);
            now=planned.AddSeconds(1);daily.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue);Call(daily,"Tick");
            var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
            var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
            Require(boss.Hitpoints==100000000);Sql("UPDATE revora_fresh_daily_boss_probe SET run=@run,stage=2","run",run);
            long elapsed=1000,received=0,damage=0;int hits=0,retaliations=0;uint minimum=p.Entity.Hitpoints;
            p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);p.Screen.Add(boss);
            while(!boss.Dead && hits<14000){
                Require(!p.Entity.Dead && !PirateFenced(p) && elapsed<7199000);
                if(p.Entity.Hitpoints<=7500 || p.Entity.Hitpoints+7000<=p.Entity.MaxHitpoints){
                    if(FreshBossMedicine(p)==0){
                        FreshBossSupply(p);elapsed+=90000; // controlled round-trip allowance, not native movement proof
                        p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);p.Screen.Add(boss);
                    }
                    while(FreshBossMedicine(p)>0 && p.Entity.Hitpoints+7000<=p.Entity.MaxHitpoints)FreshBossHeal(p);
                }
                now=planned.AddMilliseconds(elapsed);uint hp=boss.Hitpoints;
                p.Entity.AttackStamp=Time32.Now.AddSeconds(-1);
                Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);
                Require(boss.Hitpoints<=hp);damage+=hp-boss.Hitpoints;hits++;elapsed+=500;
                if(!boss.Dead && hits%4==0){
                    uint before=p.Entity.Hitpoints;Require(before>7500);
                    new Revora.Game.Attacking.Handle(null,boss,p.Entity);
                    Require(p.Entity.Hitpoints<before && before-p.Entity.Hitpoints>=6500 && before-p.Entity.Hitpoints<=7500);
                    received+=before-p.Entity.Hitpoints;retaliations++;minimum=Math.Min(minimum,p.Entity.Hitpoints);
                }
                if(hits%100==0 || boss.Dead){
                    Sql("UPDATE revora_fresh_daily_boss_probe SET hits=@hits,retaliations=@ret,damage=@damage,received=@received,elapsed_ms=@elapsed,min_hp=@hp","hits",hits,"ret",retaliations,"damage",damage,"received",received,"elapsed",elapsed,"hp",(long)minimum);
                    daily.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue);Call(daily,"Tick");save();
                    System.Console.WriteLine("[fresh-daily] hits="+hits+" boss-hp="+boss.Hitpoints+" player-hp="+p.Entity.Hitpoints+" elapsed-ms="+elapsed);
                }
            }
            Require(boss.Dead && damage==100000000 && !p.Entity.Dead);
            FreshBossLoot(p,run,daily,save);
        }else if(phase.EndsWith("loot")){
            Require(stage==2 && (long)Sql("SELECT damage FROM revora_fresh_daily_boss_probe")==100000000);
            string run=Convert.ToString(Sql("SELECT run FROM revora_fresh_daily_boss_probe"));
            Require((long)Sql("SELECT count(*) FROM revora_daily_boss_runs WHERE runkey=@run AND state=2 AND paid AND killer=@uid AND hp=0","run",run,"uid",(long)p.Entity.UID)==1);
            DateTime now=DateTime.SpecifyKind((DateTime)Sql("SELECT planned FROM revora_daily_boss_runs WHERE runkey=@run","run",run),DateTimeKind.Utc).AddMilliseconds((long)Sql("SELECT elapsed_ms FROM revora_fresh_daily_boss_probe"));
            daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            daily.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue);Call(daily,"Tick");
            FreshBossLoot(p,run,daily,save);
        }else Require(phase.EndsWith("reload") && stage==3);
        if(!phase.EndsWith("prepare")){
            string run=Convert.ToString(Sql("SELECT run FROM revora_fresh_daily_boss_probe"));
            Require((bool)Sql("SELECT e.conquerpoints=p.cp+1000000 AND e.revorapoint=p.rvp+50000 AND e.money=p.silver-p.bought*1000+10000000 FROM entities e CROSS JOIN revora_fresh_daily_boss_probe p WHERE e.uid=@uid","uid",(long)p.Entity.UID));
            Require((long)Sql("SELECT count(*) FROM revora_daily_boss_loot WHERE runkey=@run AND claimedby=@uid","run",run,"uid",(long)p.Entity.UID)==4);
            Require((long)Sql("SELECT count(*) FROM items WHERE uid=ANY((SELECT loot_uids FROM revora_fresh_daily_boss_probe)::bigint[]) AND entityid=@uid AND bound=1","uid",(long)p.Entity.UID)==4);
            string once=snapshot();Call(daily,"TryPay",p,run);Require(once==snapshot());
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_boss_probe")));
        System.Console.WriteLine("[fresh-daily-result] "+Sql("SELECT json_build_object('stage',stage,'run',run,'hits',hits,'damage',damage,'retaliations',retaliations,'received',received,'elapsed_ms',elapsed_ms,'min_hp',min_hp,'bought',bought,'used',used,'trips',trips,'cp_before',cp,'rvp_before',rvp,'silver_before',silver)::text FROM revora_fresh_daily_boss_probe"));
        System.Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh boss did not close.");
    }
    static void FreshBossLoot(GameState p,string run,Type daily,Action save)
    {
        Sql("ALTER TABLE revora_fresh_daily_boss_probe ADD COLUMN IF NOT EXISTS loot_uids bigint[] NOT NULL DEFAULT '{}';");
        while(p.Inventory.Count>36 && FreshBossMedicine(p)>0)FreshBossHeal(p);
        var key=typeof(FloorItem).GetField("DailyBossRun",BindingFlags.Instance|BindingFlags.NonPublic);
        var index=typeof(FloorItem).GetField("DailyBossLootIndex",BindingFlags.Instance|BindingFlags.NonPublic);
        var drops=p.Map.FloorItems.Values.Where(d=>Convert.ToString(key.GetValue(d))==run).OrderBy(d=>(int)index.GetValue(d)).ToArray();
        Require(drops.Length==4 && drops.All(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.Item.ID].PurificationLevel==7));
        foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;Call(daily,"Pickup",p,drop);Call(daily,"Pickup",p,drop);Require(p.Inventory.ContainsUID(drop.Item.UID));}
        Sql("UPDATE revora_fresh_daily_boss_probe SET stage=3,loot_uids=@uids","uids",drops.Select(d=>(long)d.Item.UID).ToArray());save();
    }

}
