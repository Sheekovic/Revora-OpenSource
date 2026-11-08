using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.DisCityWorld;

namespace Revora.Game.Features
{
    internal static class DisCityQuest
    {
        internal const uint Stone=723085,Mirror=3006981,Scrap=3006983,Token=3006984;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=n=>Kernel.Random.Next(n);
        private static bool enabled;private static DateTime scan;
        private static readonly ConcurrentDictionary<uint,GameState> deaths=new ConcurrentDictionary<uint,GameState>();
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private sealed class AdmissionOffer { internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState,AdmissionOffer> offers=new ConditionalWeakTable<GameState,AdmissionOffer>();
        private sealed class Run {
            internal Guid Id;internal string Week,Phase;internal int Stage,Progress,Quota,Step;internal bool Guard,Sword,Valuable;
            internal uint Mirror,Token;internal ushort LastMap;internal long[] Items;internal int[] Order;internal DateTime Started;
        }
        internal static string Week(DateTime utc) {var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);return local.Date.AddDays(-((int)local.DayOfWeek+6)%7).ToString("yyyyMMdd");}
        internal static int Quota(byte job) {return job/10==4?600:job>=100 && job<=145?300:400;}
        private static bool Admission() {var day=TimeZoneInfo.ConvertTimeFromUtc(Clock(),Zone).DayOfWeek;return day==DayOfWeek.Monday || day==DayOfWeek.Wednesday || day==DayOfWeek.Friday;}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT run,week,phase,stage,progress,quota,step,guard,mirror,token,items,crystal_order,started_at,last_map,star_sword,valuable_reward FROM revora_dis_runs WHERE uid=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Id=r.GetGuid(0),Week=r.GetString(1),Phase=r.GetString(2),Stage=r.GetInt32(3),Progress=r.GetInt32(4),Quota=r.GetInt32(5),Step=r.GetInt32(6),Guard=r.GetBoolean(7),
                Mirror=Convert.ToUInt32(r[8]),Token=Convert.ToUInt32(r[9]),Items=r.GetFieldValue<long[]>(10),Order=r.GetFieldValue<int[]>(11),Started=r.GetFieldValue<DateTimeOffset>(12).UtcDateTime,LastMap=Convert.ToUInt16(r[13]),Sword=r.GetBoolean(14),Valuable=r.GetBoolean(15)}:null;
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Run r,string action,uint mob=0,Guid? epoch=null) {
            r.Step++;
            if(Q.Execute(c,t,@"INSERT INTO revora_dis_runs(uid,run,week,phase,stage,progress,quota,step,guard,mirror,token,items,crystal_order,started_at,star_sword,valuable_reward)
VALUES(@uid,@run,@week,@phase,@stage,@progress,@quota,@step,@guard,@mirror,@token,@items,@order,@started,@sword,@valuable)
ON CONFLICT(uid) DO UPDATE SET run=excluded.run,week=excluded.week,phase=excluded.phase,stage=excluded.stage,progress=excluded.progress,quota=excluded.quota,
step=excluded.step,guard=excluded.guard,mirror=excluded.mirror,token=excluded.token,items=excluded.items,crystal_order=excluded.crystal_order,started_at=excluded.started_at,star_sword=excluded.star_sword,valuable_reward=excluded.valuable_reward",
                "uid",(long)uid,"run",r.Id,"week",r.Week,"phase",r.Phase,"stage",r.Stage,"progress",r.Progress,"quota",r.Quota,"step",r.Step,"guard",r.Guard,
                "mirror",(long)r.Mirror,"token",(long)r.Token,"items",r.Items,"order",r.Order,"started",r.Started,"sword",r.Sword,"valuable",r.Valuable)!=1)throw new InvalidOperationException("Dis City run was not saved.");
            if(Q.Execute(c,t,@"INSERT INTO revora_dis_steps(run,step,uid,stage,progress,action,monster_uid,epoch) VALUES(@run,@step,@uid,@stage,@progress,@action,@mob,@epoch)",
                "run",r.Id,"step",r.Step,"uid",(long)uid,"stage",r.Stage,"progress",r.Progress,"action",action,"mob",(long)mob,"epoch",epoch??Guid.Empty)!=1)throw new InvalidOperationException("Dis City step was not saved.");
        }
        internal static void Initialize() {
            W.Initialize();foreach(uint id in new[]{Stone,Mirror,Scrap,Token,3008057u,3008058u,3008059u})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Dis City quest item catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dis_runs(
uid bigint PRIMARY KEY,run uuid UNIQUE NOT NULL,week varchar(8) NOT NULL,phase varchar(12) NOT NULL CHECK(phase IN ('hunt','token','boss','victory','complete')),
stage integer NOT NULL CHECK(stage BETWEEN 1 AND 4),progress integer NOT NULL CHECK(progress>=0),quota integer NOT NULL CHECK(quota IN (300,400,600)),
step integer NOT NULL,guard boolean NOT NULL,mirror bigint NOT NULL,token bigint NOT NULL,items bigint[] NOT NULL,crystal_order integer[] NOT NULL CHECK(cardinality(crystal_order)=9),started_at timestamptz NOT NULL,last_map integer NOT NULL DEFAULT 0);
ALTER TABLE revora_dis_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_dis_runs ADD COLUMN IF NOT EXISTS star_sword boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_dis_steps(run uuid NOT NULL,step integer NOT NULL,uid bigint NOT NULL,stage integer NOT NULL,progress integer NOT NULL,
action varchar(24) NOT NULL,monster_uid bigint NOT NULL,epoch uuid NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(run,step));
CREATE UNIQUE INDEX IF NOT EXISTS revora_dis_death_once ON revora_dis_steps(run,epoch,monster_uid) WHERE monster_uid<>0;
CREATE TABLE IF NOT EXISTS revora_dis_stage_claims(run uuid NOT NULL,stage integer NOT NULL,uid bigint NOT NULL,rewards bigint[] NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(run,stage));
CREATE TABLE IF NOT EXISTS revora_dis_entries(
run uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),stage integer NOT NULL CHECK(stage BETWEEN 1 AND 4),
map integer NOT NULL CHECK(map BETWEEN 1 AND 65535),entered_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_dis_entries_owner ON revora_dis_entries(uid);
CREATE TABLE IF NOT EXISTS revora_dis_completions(uid bigint NOT NULL,week varchar(8) NOT NULL,run uuid UNIQUE NOT NULL,elapsed_seconds bigint NOT NULL,
completed_at timestamptz NOT NULL,PRIMARY KEY(uid,week));");}
            DisCityRunRules.Initialize();DisCityTreasury.Initialize();DisCityRelics.Initialize();DisCityDarkHorn.Initialize();DisCityRanking.Initialize();deaths.Clear();scan=DateTime.MinValue;enabled=true;
        }
        private static bool Completed(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_dis_completions WHERE uid=@uid AND week=@week)","uid",(long)uid,"week",Week(Clock())))return Equals(q.ExecuteScalar(),true);
        }
        private static string Objective(Run r) {
            if(r==null)return "Five Soul Stones open the Hell Gate. Beyond it, Pluto's legions await.";
            if(r.Phase=="complete")return "Pluto has fallen. Rest until the next week's battle.";
            if(r.Stage==1)return "Gather five Soul Stones from these demons. You have earned "+r.Progress+" of five.";
            if(r.Stage==2)return "Break the legions of Hell Hall: "+r.Progress+" of "+r.Quota+" demons defeated.";
            if(r.Stage==3)return r.Progress==9?"All nine crystals are sealed. Solar Saint will guide you onward.":"Seal Hell Crystal "+(r.Order[r.Progress]+1)+" next. "+(r.Guard?"Its guardian has fallen.":"Defeat its Hell Wraith first.");
            return r.Phase=="hunt"?"Gather four token scraps from the Syren, then unite them. You have earned "+r.Progress+".":r.Phase=="token"?"Use your Pluto Token here to summon the ruler of Dis City.":r.Phase=="boss"?"Ultimate Pluto walks this battlefield. Defeat him.":"Pluto has fallen. Return to Solar Saint for your reward.";
        }
        private static bool ObjectiveReady(Run r) {
            return r.Phase=="complete" || (r.Stage==1?r.Progress>=5 && r.Items.Length==5:r.Stage==2?r.Progress>=r.Quota:r.Stage==3?r.Progress>=9:r.Phase=="victory");
        }
        private static void Projection(GameState p,Run r) {
            if(p.Quests==null || r==null)return;
            bool cleared=false;
            for(int stage=1;stage<=4;stage++) {
                uint mission=(uint)(6471+stage);
                if(stage<r.Stage || r.Phase=="complete")QuestListSystem.MarkCompleted(p,mission);
                else if(stage==r.Stage)QuestListSystem.MarkProgress(p,mission,ObjectiveReady(r)?1u:0u,(uint)r.Progress,(uint)(r.Stage==1?5:r.Stage==2?r.Quota:r.Stage==3?9:4));
                else if(p.Quests.HasQuest((QuestID)mission)){QuestListSystem.MarkAvailable(p,mission);cleared=true;}
            }
            if(cleared)p.Quests.SendFullGUI();
        }
        private static void Populate(W.Instance i,Run r) {
            foreach(var mob in i.Monsters.Values.Where(e=>e.Body.Dead).ToArray())W.Remove(i,mob.Body);
            if(r.Phase=="complete" || r.Phase=="victory" || r.Phase=="token")return;
            if(r.Phase=="boss") {if(i.Monsters.Count==0)W.Spawn(i,-1,true);return;}
            if(r.Stage==3){if(r.Progress<9 && !r.Guard && i.Monsters.Count==0)W.Spawn(i,r.Order[r.Progress]);return;}
            int target=r.Stage==1?5:r.Stage==2?r.Quota:4;if(r.Progress>=target)return;
            while(i.Monsters.Count<24)W.Spawn(i);
        }
        private static W.Instance Enter(GameState p,Run r) {
            return W.Enter(p,r.Id,r.Stage,Clock(),i=>{
                using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_dis_runs SET last_map=@map WHERE uid=@uid AND run=@run",
                    "map",(int)i.Map.ID,"uid",(long)p.Entity.UID,"run",r.Id)!=1)throw new InvalidOperationException("Dis City map recovery record changed.");}
                r.LastMap=i.Map.ID;
            });
        }
        private static string Begin(GameState p,int difficulty=0) {
            if(p.Entity.Level<110)return "Reach level 110 before entering Dis City.";
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest before entering Dis City.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Dis City character missing.");
                if(Completed(c,t,p.Entity.UID))return "You have claimed this week's victory. Return next week.";
                var r=Read(c,t,p.Entity.UID);ConquerItem mirror=null;
                if(r==null || r.Phase=="complete") {
                    if(!Admission())return "The gates open on Monday, Wednesday and Friday. An unfinished journey may still be resumed.";
                    if(p.Inventory.Count>=40)return "Make one place for the Mirror of Hell.";
                    if(W.Players.Count>=W.Capacity)return "All paths into Dis City are occupied. Return shortly.";
                    var order=Enumerable.Range(0,9).ToArray();for(int n=8;n>0;n--){int j=Roll(n+1);if(j<0 || j>n)throw new InvalidOperationException("Dis City crystal draw invalid.");int v=order[n];order[n]=order[j];order[j]=v;}
                    mirror=Q.New(Mirror);Q.Insert(c,t,p.Entity.UID,mirror);
                    r=new Run{Valuable=true,Id=Guid.NewGuid(),Week=Week(Clock()),Phase="hunt",Stage=1,Quota=Quota(p.Entity.Class),Mirror=mirror.UID,Items=new long[0],Order=order,Started=Clock()};
                    Save(c,t,p.Entity.UID,r,"begin");
                    DisCityRunRules.Admit(c,t,p.Entity.UID,r.Id,difficulty);
                }
                t.Commit();if(mirror!=null)Q.Install(p,new uint[0],new[]{mirror});var instance=Enter(p,r);Populate(instance,r);RecordEntry(p,r,instance);Projection(p,r);return DisCityRunRules.Name(instance.Rules.Difficulty)+": "+Objective(r);
            }}
        }
        private static void RecordEntry(GameState p,Run r,W.Instance instance){
            if(!Q.Player(p) || instance==null || instance.Released || instance.Player!=p || instance.Owner!=p.Entity.UID
                || instance.Run!=r.Id || instance.Stage!=r.Stage || !ReferenceEquals(p.Map,instance.Map) || p.Entity.MapID!=instance.Map.ID)
                throw new InvalidOperationException("Dis City arrival ownership changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Dis City arrival owner missing.");
                using(var q=Q.Command(c,t,"SELECT run FROM revora_dis_runs WHERE uid=@uid AND run=@run AND stage=@stage FOR UPDATE","uid",(long)p.Entity.UID,"run",r.Id,"stage",r.Stage))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Dis City arrival run changed.");
                Q.Execute(c,t,"INSERT INTO revora_dis_entries(run,uid,stage,map,entered_at) VALUES(@run,@uid,@stage,@map,@now) ON CONFLICT(run) DO NOTHING",
                    "run",r.Id,"uid",(long)p.Entity.UID,"stage",r.Stage,"map",(int)instance.Map.ID,"now",Clock());
                using(var q=Q.Command(c,t,"SELECT uid FROM revora_dis_entries WHERE run=@run","run",r.Id))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Entity.UID)throw new InvalidOperationException("Dis City arrival receipt changed.");
                t.Commit();
            }}
        }
        private static ConquerItem[] Collect(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            if(r.Phase!="hunt" || r.Stage!=1 && r.Stage!=4)return new ConquerItem[0];
            int count=Math.Min(r.Progress-r.Items.Length,40-p.Inventory.Count);if(count<=0)return new ConquerItem[0];
            var items=Enumerable.Range(0,count).Select(n=>Q.New(r.Stage==1?Stone:Scrap)).ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
            r.Items=r.Items.Concat(items.Select(i=>(long)i.UID)).ToArray();return items;
        }
        private static void Evidence(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            string key=r.Id.ToString("N");switch(r.Stage) {
                case 1:ProgressionEvidence.Complete(c,t,p.Entity.UID,11002,"dis-city-v1",key,1);break;
                case 2:ProgressionEvidence.Complete(c,t,p.Entity.UID,11003,"dis-city-v1",key,1);break;
                case 3:ProgressionEvidence.Complete(c,t,p.Entity.UID,11004,"dis-city-v1",key,1);break;
                case 4:ProgressionEvidence.Complete(c,t,p.Entity.UID,11005,"dis-city-v1",key,1);break;
            }
        }
        private static string Advance(GameState p,Run r,NpgsqlConnection c,NpgsqlTransaction t) {
            if(r.Stage==1 && (r.Progress<5 || r.Items.Length<5))return "Collect all five of your earned Soul Stones before passing the gate.";
            if(r.Stage==2 && r.Progress<r.Quota || r.Stage==3 && r.Progress<9)return Objective(r);
            if(r.Stage==4 && r.Phase!="victory")return Objective(r);
            if(r.Phase=="complete")return Objective(r);
            if(r.Stage==4 && Completed(c,t,p.Entity.UID))return "Your victory reward must wait until next week.";
            var removed=r.Stage==1?r.Items.Select(i=>(uint)i).ToArray():r.Stage==4 && r.Mirror!=0 && Q.Owns(c,t,p,r.Mirror,Mirror)?new[]{r.Mirror}:new uint[0];
            uint[] types=r.Stage<=2?new[]{3008057u}:r.Stage==3?new[]{3008058u}:new[]{3008058u,3008059u,DisCityTreasury.Key,DisCityDarkHorn.Horn};
            if(r.Stage==4 && r.Sword)types=types.Concat(new[]{DisCityRelics.Sword}).ToArray();
            bool valuable=r.Stage==4 && r.Valuable;
            if(p.Inventory.Count-removed.Length+types.Length+(valuable?1:0)>40)return "Make room for your reward. Your progress is safe.";
            foreach(uint uid in removed)Q.Consume(c,t,p,uid,r.Stage==1?Stone:Mirror);
            var rewards=types.Select(id=>Q.New(id)).Concat(valuable?new[]{QuestSoulReward.Create()}:new ConquerItem[0]).ToArray();foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
            Q.Execute(c,t,"INSERT INTO revora_dis_stage_claims(run,stage,uid,rewards,claimed_at) VALUES(@run,@stage,@uid,@items,@now)",
                "run",r.Id,"stage",r.Stage,"uid",(long)p.Entity.UID,"items",rewards.Select(i=>(long)i.UID).ToArray(),"now",Clock());Evidence(c,t,p,r);
            bool final=r.Stage==4;
            if(final)DisCityTreasury.Issue(c,t,p.Entity.UID,r.Id,rewards.Single(item=>item.ID==DisCityTreasury.Key));
            if(final)DisCityRelics.Issue(c,t,p.Entity.UID,r.Id,rewards.Single(item=>item.ID==DisCityDarkHorn.Horn));
            if(final && r.Sword)DisCityRelics.Issue(c,t,p.Entity.UID,r.Id,rewards.Single(item=>item.ID==DisCityRelics.Sword));
            if(final){Q.Execute(c,t,"INSERT INTO revora_dis_completions(uid,week,run,elapsed_seconds,completed_at) VALUES(@uid,@week,@run,@elapsed,@now)",
                "uid",(long)p.Entity.UID,"week",Week(Clock()),"run",r.Id,"elapsed",Math.Max(0L,(long)(Clock()-r.Started).TotalSeconds),"now",Clock());r.Phase="complete";r.Mirror=0;}
            else{r.Stage++;r.Progress=0;r.Guard=false;r.Items=new long[0];r.Phase="hunt";}
            Save(c,t,p.Entity.UID,r,final?"complete":"advance");t.Commit();Q.Install(p,removed,rewards);Q.Refresh(p);Projection(p,r);
            W.Instance instance;if(final){W.Home(p);if(W.Players.TryGetValue(p.Entity.UID,out instance))W.Release(instance);}
            else {instance=Enter(p,r);Populate(instance,r);}
            return final?(valuable?"A bound P7 soul joins your spoils. ":"")+(r.Sword?"Pluto has fallen. Your spoils include a Dark Horn and Star Sword. Bring them to me in Twin City.":"Pluto is defeated. Take your spoils. Bring the Dark Horn to me in Twin City for experience, or use your key to visit the treasury."):Objective(r);
        }
        private static string Crystal(GameState p,Run r,int index,NpgsqlConnection c,NpgsqlTransaction t) {
            if(r.Stage!=3 || r.Phase!="hunt" || r.Progress>=9 || r.Order[r.Progress]!=index)return "This crystal is not the next seal. Consult your Mirror of Hell.";
            if(!r.Guard)return "The Hell Wraith still binds this crystal.";
            r.Progress++;r.Guard=false;Save(c,t,p.Entity.UID,r,"seal-crystal");t.Commit();W.Instance i;if(W.Players.TryGetValue(p.Entity.UID,out i))Populate(i,r);Projection(p,r);return Objective(r);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || p.ActiveNpc!=W.Saint && (p.ActiveNpc<W.CrystalFirst || p.ActiveNpc>=W.CrystalFirst+9))return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Q.Near(p,p.ActiveNpc,p.Entity.MapID))return true;
                var d=new Npcs(p);byte option=request.OptionID;if(option==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                if(p.Entity.MapID==1002 && p.ActiveNpc==W.Saint) {
                    if(option==7){offers.Remove(p);offers.Add(p,new AdmissionOffer{Until=Clock().AddMinutes(2)});d.Text("Choose your trial. Veteran demons have twice the vitality; Nightmare demons have three times. Their blows grow stronger. Rewards are equal, honors separate. An unfinished trial keeps its difficulty.");d.Option("Standard.",10);d.Option("Veteran.",11);d.Option("Nightmare.",12);}
                    else if(option>=10 && option<=12){AdmissionOffer offer;if(offers.TryGetValue(p,out offer) && offer.Until>Clock()){offers.Remove(p);d.Text(Begin(p,option-10));}else d.Text("Ask me about the trials again.");}
                    else if(option==1)d.Text(Begin(p));else if(option==3)d.Text(DisCityTreasury.Enter(p));else{d.Text("Dis City opens on Monday, Wednesday and Friday. Face its four trials and defeat Pluto. New expeditions also earn a bound P7 soul. I reward one victory each week.");d.Option("Standard trial / resume.",1);d.Option("Choose a harder trial.",7);d.Option("Visit Pluto's treasury.",3);d.Option("Exchange a Star Sword.",4);d.Option("Exchange a Dark Horn.",5);d.Option("The swiftest heroes.",6);}
                }else {
                    W.Instance instance;if(!W.Maps.TryGetValue(p.Entity.MapID,out instance) || instance.Player!=p || instance.Released)return true;
                    if(option==9){W.Home(p);W.Release(instance);d.Text("Rest here. Your unfinished journey will wait.");}
                    else if(instance.Stage==5)d.Text("The chest holds Pluto's hoard. Each earned key opens it once; every fifth opening breaks the greater seal.");
                    else using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        var r=Read(c,t,p.Entity.UID);if(r==null || r.Id!=instance.Run || r.Stage!=instance.Stage)return true;
                        if(p.ActiveNpc>=W.CrystalFirst){d.Text(option==1?Crystal(p,r,(int)(p.ActiveNpc-W.CrystalFirst),c,t):Objective(r));if(option!=1)d.Option("Seal the crystal.",1);}
                        else if(option==1)d.Text(Advance(p,r,c,t));
                        else if(option==2){var items=Collect(c,t,p,r);if(items.Length>0){Save(c,t,p.Entity.UID,r,"collect");t.Commit();Q.Install(p,new uint[0],items);Projection(p,r);}d.Text(items.Length>0?"Keep these earned relics safe.":"You have no uncollected relics, or your bag is full.");}
                        else {d.Text(Objective(r));d.Option("Open the next gate.",1);if(r.Stage==1 || r.Stage==4)d.Option("Collect my earned relics.",2);}
                    }}
                    d.Option("Return to Twin City.",9);
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"dis-city-npc",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=Mirror && item.ID!=Scrap && item.ID!=Token)return false;
            if(!enabled || !Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;W.Instance i;
                if(p.Booth!=null || p.Trade==null || p.Trade.InTrade || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item)
                    || !W.Maps.TryGetValue(p.Entity.MapID,out i) || i.Player!=p || i.Released)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var r=Read(c,t,p.Entity.UID);if(r==null || r.Id!=i.Run || r.Stage!=i.Stage)return true;
                    if(item.ID==Mirror) {
                        if(r.Mirror!=item.UID || !Q.Owns(c,t,p,item.UID,Mirror))return true;t.Commit();var saint=i.Map.Npcs[W.Saint];W.Travel(p,i.Map,new System.Drawing.Point(saint.X+3,saint.Y+3));p.Send(Objective(r));return true;
                    }
                    if(r.Stage!=4)return true;
                    if(item.ID==Scrap) {
                        if(r.Phase!="hunt" || r.Progress!=4 || r.Items.Length!=4 || !r.Items.Contains((long)item.UID))return true;
                        foreach(long uid in r.Items)Q.Consume(c,t,p,(uint)uid,Scrap);var token=Q.New(Token);Q.Insert(c,t,p.Entity.UID,token);
                        uint[] removed=r.Items.Select(uid=>(uint)uid).ToArray();r.Items=new long[0];r.Token=token.UID;r.Phase="token";Save(c,t,p.Entity.UID,r,"combine-token");t.Commit();Q.Install(p,removed,new[]{token});
                        foreach(var mob in i.Monsters.Values.ToArray())W.Remove(i,mob.Body);p.Send(Objective(r));
                    }else {
                        if(r.Phase!="token" || r.Token!=item.UID)return true;Q.Consume(c,t,p,item.UID,Token);r.Token=0;r.Phase="boss";Save(c,t,p.Entity.UID,r,"summon");t.Commit();Q.Install(p,new[]{item.UID},new ConquerItem[0]);Populate(i,r);p.Send(Objective(r));
                    }
                }}
            }catch(Exception ex){Q.Fail(p,"dis-city-item",ex);}return true;
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!enabled || mob==null || mob.MonsterInfo==null || mob.MonsterInfo.ID<9914001 || mob.MonsterInfo.ID>9914004)return false;
            W.Instance i;if(!W.Maps.TryGetValue(mob.MapID,out i))return true;
            var p=killer==null?null:killer.Owner;if(!Q.Player(p) || !ReferenceEquals(p.Entity,killer) || i.Player!=p || p.Entity.MapID!=mob.MapID)return true;
            lock(p.ItemSyncRoot)try {
                W.Encounter encounter;if(!Q.Player(p) || i.Released || !i.Monsters.TryGetValue(mob.UID,out encounter) || !ReferenceEquals(encounter.Body,mob) || mob.Dead
                    || Clock()>=i.Until || Kernel.GetDistance(p.Entity.X,p.Entity.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    var r=Read(c,t,p.Entity.UID);if(r==null || r.Id!=i.Run || r.Stage!=i.Stage)return true;
                    if(encounter.Boss){if(r.Phase!="boss")return true;r.Sword=DisCityRelics.DrawSword();r.Phase="victory";}
                    else if(r.Stage==3){if(r.Phase!="hunt" || r.Progress>=9 || r.Order[r.Progress]!=encounter.Crystal || r.Guard)return true;r.Guard=true;}
                    else if(r.Phase=="hunt") {
                        if(r.Stage==2)r.Progress=Math.Min(r.Quota,r.Progress+1);
                        else if(r.Progress<(r.Stage==1?5:4) && Roll(100)<25)r.Progress++;
                    }else return true;
                    var items=Collect(c,t,p,r);Save(c,t,p.Entity.UID,r,"defeat",mob.UID,i.Epoch);if(encounter.Boss)DisCityRanking.Record(c,t,p,r.Id,r.Step,r.Started);
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,mob,killer,"dis:"+r.Id.ToString("N")+":"+r.Step);
                    t.Commit();if(items.Length>0)Q.Install(p,new uint[0],items);
                    mob.DieCore(killer);W.Remove(i,mob);Q.Refresh(p);Projection(p,r);p.Send(Objective(r));
                }}
            }catch(Exception ex){Q.Fail(p,"dis-city-defeat",ex);}return true;
        }
        internal static void OnDeath(GameState p) {if(enabled && Q.Player(p,false) && W.Maps.ContainsKey(p.Entity.MapID))deaths[p.Entity.UID]=p;}
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                GameState ignored;deaths.TryRemove(p.Entity.UID,out ignored);W.Instance i;W.Players.TryGetValue(p.Entity.UID,out i);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,p.Entity.UID);t.Commit();
                    if(i!=null || r!=null && p.Entity.MapID==r.LastMap || DisCityTreasury.WasInside(c,null,p))W.Home(p,true);if(i!=null)W.Release(i);Projection(p,r);}}
            }catch(Exception ex){Q.Fail(p,"dis-city-login",ex);}
        }
        internal static void Refresh(GameState p) {if(!enabled || !Q.Player(p,false))return;lock(p.ItemSyncRoot)try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,p.Entity.UID);t.Commit();Projection(p,r);}}}catch(Exception ex){Q.Fail(p,"dis-city-refresh",ex);}}
        internal static void Tick() {
            if(!enabled || Clock()<scan)return;scan=Clock().AddSeconds(2);
            foreach(uint uid in deaths.Keys.Where(uid=>!W.Players.ContainsKey(uid)).ToArray()){GameState ignored;deaths.TryRemove(uid,out ignored);}
            foreach(var i in W.Players.Values.ToArray()) {
                var p=i.Player;
                if(!System.Threading.Monitor.TryEnter(p.ItemSyncRoot))continue;
                try {
                    W.Instance current;if(!W.Players.TryGetValue(i.Owner,out current) || !ReferenceEquals(current,i))continue;
                    if(!Q.Player(p,false)){W.Release(i);continue;}
                    GameState death;
                    if(p.Entity.MapID!=i.Map.ID || Clock()>=i.Until || deaths.TryRemove(i.Owner,out death)) {if(p.Entity.MapID==i.Map.ID)W.Home(p,true);W.Release(i);continue;}
                    if(i.Stage==5)continue;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,i.Owner);t.Commit();if(r!=null && r.Id==i.Run && r.Stage==i.Stage)Populate(i,r);}}
                }catch(Exception ex){Q.Fail(p,"dis-city-tick",ex);}finally{System.Threading.Monitor.Exit(p.ItemSyncRoot);}
            }
        }
    }
}
