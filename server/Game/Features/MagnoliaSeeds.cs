using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class MagnoliaQuest
    {
        private sealed class SeedOffer { internal uint Uid,Type,Fee;internal DateTime Opened;internal string Day; }
        private static readonly ConditionalWeakTable<GameState,SeedOffer> seedOffers=new ConditionalWeakTable<GameState,SeedOffer>();
        internal static Func<int,int> SeedRoll=SeedRandom;
        private static int SeedRandom(int max) {
            if(max<=0)throw new ArgumentOutOfRangeException("max");
            using(var rng=RandomNumberGenerator.Create()){var bytes=new byte[4];uint value;ulong limit=(1UL<<32)/(uint)max*(uint)max;
                do{rng.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);return (int)(value%(uint)max);}
        }
        private static bool SeedType(uint id){return id>=729306 && id<=729310;}
        private static ushort GradeStudy(uint id){if(!SeedType(id))throw new ArgumentOutOfRangeException("id");return new ushort[]{10,20,50,100,300}[id-729306];}
        private static int GradeMinutes(uint id){if(!SeedType(id))throw new ArgumentOutOfRangeException("id");return new[]{60,90,120,180,300}[id-729306];}
        private static string GradeName(uint id){return new[]{"Normal","Refined","Unique","Elite","Super"}[id-729306];}
        private static uint DrawSeed(){
            // Explicit Revora distribution, not a claim about unpublished official odds.
            int roll=SeedRoll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Magnolia draw is outside its range.");
            return roll<50?729306u:roll<80?729307u:roll<95?729308u:roll<99?729309u:729310u;
        }
        private static void InitializeGrades(NpgsqlConnection c){
            Q.Execute(c,null,@"ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS rules_version integer NOT NULL DEFAULT 0 CHECK(rules_version IN(0,1));
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS exp_minutes integer NOT NULL DEFAULT 60 CHECK(exp_minutes IN(60,90,120,180,300));
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS current_seed bigint;
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS changes integer NOT NULL DEFAULT 0 CHECK(changes>=0);
CREATE TABLE IF NOT EXISTS revora_magnolia_changes(
uid bigint NOT NULL,day varchar(8) NOT NULL,step integer NOT NULL CHECK(step>0),before_seed bigint NOT NULL,after_seed bigint NOT NULL UNIQUE,
old_type integer NOT NULL CHECK(old_type BETWEEN 729306 AND 729310),new_type integer NOT NULL CHECK(new_type BETWEEN 729306 AND 729310),
fee bigint NOT NULL CHECK(fee>=0),forced boolean NOT NULL,changed_at timestamptz NOT NULL,
PRIMARY KEY(uid,day,step),FOREIGN KEY(uid,day) REFERENCES revora_magnolia_runs(uid,day));");
        }
        private static bool ChangeReady(GameState p){return Ready(p) && p.Entity.Level>=Config.DailyMagnoliaMinLevel
            && Q.Near(p,ExplorationWorld.GardenKeeper,1806) && p.Map.BaseID==1806;}
        private static bool Changeable(ConquerItem i){return i!=null && SeedType(i.ID) && i.StackSize<=1 && i.Position==0 && i.Warehouse==0
            && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static void SeedBag(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Magnolia bag is over capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Magnolia bag changed.");
        }
        private static string ChangeSeed(GameState p,bool forced,DateTime now){
            SeedOffer offer;seedOffers.TryGetValue(p,out offer);seedOffers.Remove(p);ConquerItem seed;
            if(offer==null || now<offer.Opened || now>=offer.Opened.AddMinutes(2) || Day(now)!=offer.Day
                || !p.Inventory.TryGetItem(offer.Uid,out seed) || !Changeable(seed) || seed.ID!=offer.Type)
                return "Show me your seed again.";
            if(seed.ID==729310)return "Your Super seed is ready for the flower bed.";
            uint fee=forced?offer.Fee:1;if(p.Entity.ConquerPoints<fee)return "You do not have enough CPs.";
            Run run;ConquerItem replacement;uint next=p.Entity.ConquerPoints-fee;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);run=Read(c,t,p,offer.Day,now);
                if(run!=null && (string.CompareOrdinal(run.Day,offer.Day)>0 || run.Day==offer.Day && run.Done))return "You have tended the garden today. Return tomorrow.";
                if(run==null || run.Day!=offer.Day){run=NewRun(offer.Day,seed.ID);run.Seed=seed.UID;Insert(c,t,p,run,seed,0);}
                if(run.Seed!=seed.UID || run.Type!=seed.ID)return "Bring the seed entrusted to you today.";
                SeedBag(c,t,p);
                if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@seed AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'",
                    "seed",(long)seed.UID,"uid",(long)p.Entity.UID,"type",(int)seed.ID,"stack",(int)seed.StackSize,"bound",seed.Bound?1:0)!=1)
                    throw new InvalidOperationException("Magnolia seed ownership changed.");
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                    "next",(long)next,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)throw new InvalidOperationException("Magnolia seed balance changed.");
                replacement=Q.New(forced?729310u:DrawSeed(),seed.Bound);InsertItem(c,t,p,replacement);
                int step=checked(run.Changes+1);ushort study=GradeStudy(replacement.ID);int minutes=GradeMinutes(replacement.ID);
                if(Q.Execute(c,t,@"UPDATE revora_magnolia_runs SET current_seed=@next,seed_type=@type,changes=@step,rules_version=1,study=@study,exp_minutes=@minutes,fee=fee+@fee
WHERE uid=@uid AND day=@day AND completed=false AND changes=@old AND COALESCE(current_seed,issued_seed,0)=@seed AND seed_type=@before AND fee+@fee<=4294967295",
                    "next",(long)replacement.UID,"type",(int)replacement.ID,"step",step,"study",(int)study,"minutes",minutes,"fee",(long)fee,
                    "uid",(long)p.Entity.UID,"day",offer.Day,"old",run.Changes,"seed",(long)seed.UID,"before",(int)seed.ID)!=1)
                    throw new InvalidOperationException("Magnolia seed terms changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_magnolia_changes(uid,day,step,before_seed,after_seed,old_type,new_type,fee,forced,changed_at)
VALUES(@uid,@day,@step,@before,@after,@old,@new,@fee,@forced,@now)","uid",(long)p.Entity.UID,"day",offer.Day,"step",step,
                    "before",(long)seed.UID,"after",(long)replacement.UID,"old",(int)seed.ID,"new",(int)replacement.ID,"fee",(long)fee,"forced",forced,"now",now)!=1)
                    throw new InvalidOperationException("Magnolia seed receipt was not saved.");
                if(!ChangeReady(p) || Clock()>=offer.Opened.AddMinutes(2) || Day(Clock())!=offer.Day)throw new InvalidOperationException("Magnolia seed offer expired.");t.Commit();
            }}
            Q.Install(p,new[]{seed.UID},new[]{replacement});p.Entity.ApplyCommittedConquerPoints(next);Projection(p,run,offer.Day);
            return "A "+GradeName(replacement.ID)+" seed for you. Plant it beside a flower bed.";
        }
        private static bool HandleSeedChange(GameState p,NpcRequest request){
            lock(p.ItemSyncRoot)try{
                if(!ChangeReady(p) || request.NpcID!=0 && request.NpcID!=ExplorationWorld.GardenKeeper)return true;
                offers.Remove(p);var d=new Npcs(p);d.Avatar(113);DateTime now=Clock();
                if(request.OptionID==5){
                    seedOffers.Remove(p);var seeds=p.Inventory.Objects.Where(i=>SeedType(i.ID)).Take(2).ToArray();
                    Refresh(p);if(p.Entity.PersistenceRequiresReload)return true;
                    if(p.Entity.MagniloQuest)d.Text("You have tended the garden today. Return tomorrow.");
                    else if(seeds.Length!=1 || !Changeable(seeds[0]))d.Text("Bring one unlocked, unstacked seed for me to change.");
                    else if(seeds[0].ID==729310)d.Text("Your Super seed is ready for the flower bed.");
                    else {
                        var seed=seeds[0];seedOffers.Add(p,new SeedOffer{Uid=seed.UID,Type=seed.ID,Fee=Config.DailyMagnoliaSuperSeedCp,Day=Day(now),Opened=now});
                        d.Text("Try another seed for 1 CP; its grade may fall. Chances: Normal 50%, Refined 30%, Unique 15%, Elite 4%, Super 1%. Or choose Super for "+Config.DailyMagnoliaSuperSeedCp+" CPs.");
                        d.Option("Try another seed (1 CP).",6);d.Option("Choose Super ("+Config.DailyMagnoliaSuperSeedCp+" CPs).",7);
                    }
                }else d.Text(ChangeSeed(p,request.OptionID==7,now));
                d.Option("Back to Twin City.",2);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"magnolia-change",ex);}return true;
        }
    }
}
