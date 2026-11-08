using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ReleaseSoulsQuest
    {
        internal const uint Guardian=301,LegacyGuardian=15140,Mission=6049;
        internal const int Required=3;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private sealed class Run {internal bool RuneReward;internal string State;internal long[] Seen;internal uint Cp,Rvp,Fee,Chi;internal ushort Study;}
        private sealed class Inspection {internal GameState Player;internal uint Npc;internal ushort X,Y;internal string Day;internal DateTime End;}
        private sealed class Offer {internal GameState Player;internal string Day;internal DateTime End;}
        private static readonly ConcurrentDictionary<uint,Inspection> inspecting=new ConcurrentDictionary<uint,Inspection>();
        private static readonly ConcurrentDictionary<uint,Offer> offers=new ConcurrentDictionary<uint,Offer>();
        private static bool enabled;
        internal static string Day(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Soul quest timestamps require UTC.");
            return TimeZoneInfo.ConvertTimeFromUtc(utc,Zone).ToString("yyyyMMdd",CultureInfo.InvariantCulture);
        }
        internal static bool Handles(uint id) { return id==Guardian || id==LegacyGuardian || Remains(id); }
        private static bool Remains(uint id) {return id>=65822 && id<=65840;}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            using(var q=Q.Command(c,t,"SELECT state,seen,cp,rvp,fee,study,chi,valuable_reward,rune_reward FROM revora_soul_runs WHERE uid=@uid AND day=@day FOR UPDATE","uid",(long)uid,"day",day))
            using(var r=q.ExecuteReader())return r.Read()?new Run{State=r.GetString(0),Seen=r.GetFieldValue<long[]>(1),Cp=Convert.ToUInt32(r[2]),Rvp=Convert.ToUInt32(r[3]),Fee=Convert.ToUInt32(r[4]),Study=Convert.ToUInt16(r[5]),Chi=Convert.ToUInt32(r[6]),RuneReward=r.GetBoolean(8)}:null;
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p,string day) {
            using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                var level=q.ExecuteScalar();if(level==null || level==DBNull.Value || Convert.ToInt32(level)<70)throw new InvalidOperationException("Soul quest owner changed.");
            }
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_soul_runs WHERE uid=@uid AND day>@day)","uid",(long)p.Entity.UID,"day",day))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Soul quest day moved backward.");
        }
        private static void Current(GameState p,string day,uint npc,Offer offer=null) {
            var now=Clock();
            if(!Q.Near(p,npc,1000) || !Handles(npc) || p.Entity.Level<70 || day!=Day(now)
                || (offer!=null && (!ReferenceEquals(offer.Player,p) || offer.Day!=day || now>=offer.End)))
                throw new InvalidOperationException("Soul quest request expired.");
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())ids.Add(Convert.ToUInt32(r[0]));
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Soul quest inventory changed.");
        }
        private static void InsertItem(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                "bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)throw new InvalidOperationException("Soul quest gift was not saved.");
        }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Release Souls yellow rune catalog is missing or class restricted.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_soul_runs(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,state varchar(16) NOT NULL CHECK(state IN ('active','complete','paid')),
seen bigint[] NOT NULL DEFAULT '{}',cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),
fee bigint NOT NULL CHECK(fee BETWEEN 0 AND 4294967295),study integer NOT NULL CHECK(study BETWEEN 0 AND 65535),chi bigint NOT NULL,
rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,day),CHECK(cardinality(seen)<=3));
ALTER TABLE revora_soul_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_soul_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;");}
            QuestInfo.Register((QuestID)Mission,QuestType.DailyQuest);enabled=true;
        }
        internal static void PlaceNpcs() {
            var actors=new List<NpcSpawn>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,lookface,mapid,cellx,celly FROM npcs WHERE id=15140 OR id BETWEEN 65822 AND 65840"))
            using(var r=q.ExecuteReader())while(r.Read())actors.Add(new NpcSpawn{UID=Convert.ToUInt32(r[0]),Mesh=Convert.ToUInt16(r[1]),MapID=Convert.ToUInt16(r[2]),X=Convert.ToUInt16(r[3]),Y=Convert.ToUInt16(r[4]),Type=Enums.NpcType.Talker});}
            if(actors.Count!=20)throw new InvalidOperationException("Release the Souls NPC set incomplete.");
            foreach(var npc in actors) {
                bool guardian=npc.UID==LegacyGuardian;
                foreach(var map in Kernel.Maps.Values.ToArray()){INpc old;if(map.Npcs.TryGetValue(npc.UID,out old))map.RemoveNpc(old);}
                if(guardian){npc.UID=Guardian;npc.Name="DesertGuardian";}else npc.Name="SoldierRemains";
                var destination=Kernel.Maps[(ushort)1000];INpc occupied;
                if(destination.Npcs.TryGetValue(npc.UID,out occupied))throw new InvalidOperationException("Soul quest NPC identity collision.");
                ushort x=npc.X,y=npc.Y;if(!PotencyGuide.FindLanding(destination,ref x,ref y))throw new InvalidOperationException("Soul quest NPC landing blocked.");
                npc.MapID=1000;npc.X=x;npc.Y=y;destination.AddNpc(npc);
            }
        }
        private static void Projection(GameState p,Run r) {
            p.Entity.RelasesTheSoul=r!=null && r.State!="active";p.Entity.RelasesGuard=r!=null && r.State=="active"?(byte)(r.Seen.Length+1):(byte)0;
            if(r==null)QuestListSystem.MarkAvailable(p,Mission);
            else if(r.State=="active") {
                QuestListSystem.MarkProgress(p,Mission,(uint)r.Seen.Length);
                if(p.Quests!=null)p.Quests.GetQuest((QuestID)Mission).Kills=(uint)r.Seen.Length;
            }
            else QuestListSystem.MarkCompleted(p,Mission);
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,p.Entity.UID,Day(Clock()));t.Commit();Projection(p,r);}}
            }catch(Exception ex){Q.Fail(p,"release-souls-refresh",ex);}
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            Offer oldOffer;offers.TryRemove(p.Entity.UID,out oldOffer);Inspection ignored;inspecting.TryRemove(p.Entity.UID,out ignored);p.Entity.OnCheckGuard=false;Refresh(p);
        }
        private static string Begin(GameState p) {
            if(p.Entity.Level<70)return "Reach level 70 before helping us in the desert.";
            uint npc=p.ActiveNpc;string day=Day(Clock());using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p,day);
                Q.Execute(c,t,@"INSERT INTO revora_soul_runs(uid,day,state,cp,rvp,fee,study,chi,valuable_reward,rune_reward)
VALUES(@uid,@day,'active',@cp,@rvp,@fee,@study,@chi,false,true) ON CONFLICT(uid,day) DO NOTHING",
                    "uid",(long)p.Entity.UID,"day",day,"cp",(long)Config.DailyQuestCp,"rvp",(long)Config.DailyQuestRevoraPoints,"fee",(long)Config.DailyDesertSkipCp,
                    "study",(int)Config.DailyDesertGuardianStudy,"chi",(long)Config.DailyQuestChiToken);
                var r=Read(c,t,p.Entity.UID,day);if(r==null)throw new InvalidOperationException("Soul quest acceptance was not saved.");Current(p,day,npc);t.Commit();Projection(p,r);
                return r.State=="active"?"Three fallen soldiers still wait among the dunes. Release their souls, then return to me."+(r.RuneReward?" A bound Tortoise Breaker rune awaits you beside my other gifts.":""):"You have done enough today. Return after midnight Pacific.";
            }}
        }
        private static string Claim(GameState p,bool paid,Offer offer=null) {
            if(p.Entity.Level<70)return "Reach level 70 before helping us.";
            uint npc=p.ActiveNpc;string day=Day(Clock());using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                // Match the existing item/currency services' owner lock and character-row order.
                Owner(c,t,p,day);
                var r=Read(c,t,p.Entity.UID,day);if(r==null)return "Speak with me before seeking the soldiers.";
                if(r.State!="active")return "You have already received today's reward.";
                if(!paid && (r.Seen.Length!=Required || r.Seen.Distinct().Count()!=Required || r.Seen.Any(id=>!Remains((uint)id))))return "Three different soldiers need your help before I can reward you.";
                if(paid && p.Entity.ConquerPoints<r.Fee)return "You do not have enough CPs for the temple's help.";
                Bag(c,t,p);
                if(p.Inventory.Count+(r.RuneReward?3:2)>40)return r.RuneReward?"Make three spaces for your gifts first.":"Make two spaces for your gifts first.";
                long cp=(long)p.Entity.ConquerPoints+r.Cp-(paid?r.Fee:0),rvp=(long)p.Entity.RevoraPoint+r.Rvp;int study=p.Entity.SubClasses.StudyPoints+r.Study;
                if(cp>uint.MaxValue || rvp>uint.MaxValue || study>ushort.MaxValue)return "Spend some of your saved points before claiming these gifts.";
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study WHERE uid=@uid AND COALESCE(conquerpoints,0)=@before AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy",
                    "cp",cp,"rvp",rvp,"study",study,"uid",(long)p.Entity.UID,"before",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)throw new InvalidOperationException("Soul quest currency changed.");
                var rewards=r.RuneReward?new[]{Q.New(722136),Q.New(r.Chi),Q.New(4030101,true)}:new[]{Q.New(722136),Q.New(r.Chi)};foreach(var item in rewards)InsertItem(c,t,p,item);
                string state=paid?"paid":"complete";
                if(Q.Execute(c,t,"UPDATE revora_soul_runs SET state=@state,rewards=@rewards WHERE uid=@uid AND day=@day AND state='active' AND cardinality(rewards)=0","state",state,"rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"day",day)!=1)throw new InvalidOperationException("Soul quest completion was not saved.");
                if(!paid) {
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,11012,"release-souls-v1",day,1);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11012)","uid",(long)p.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Soul quest achievement was not saved.");
                }
                StudyRewardEvidence.Record(c,t,p,r.Study,"release-souls:"+day);Current(p,day,npc,offer);
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)cp);p.Entity.ApplyCommittedRevoraPoints((uint)rvp);p.Entity.SubClasses.StudyPoints=(ushort)study;
                Q.Install(p,new uint[0],rewards);p.Send(new SubClassShowFull(0){ID=8,Study=(ushort)study,StudyReceive=r.Study}.ToArray());
                r.State=state;Projection(p,r);Q.Refresh(p);
                return "May they find peace. Take "+r.Cp+" CPs, "+r.Rvp+" RVPs, "+r.Study+" study points, an EXP Ball and a Chi Token"+(r.RuneReward?", and a bound Tortoise Breaker rune.":".");
            }}
        }
        private static void Inspect(GameState p,Npcs d) {
            string day=Day(Clock());using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID,day);t.Commit();
                if(p.Entity.Level<70 || r==null || r.State!="active"){d.Text("The Desert Guardian knows why our souls linger here.");return;}
                if(r.Seen.Contains((long)p.ActiveNpc)){d.Text("This soldier is already at peace. Another soul still waits.");return;}
                if(r.Seen.Length>=Required){d.Text("Your duty is done. Return to the Desert Guardian.");return;}
                var pending=new Inspection{Player=p,Npc=p.ActiveNpc,X=p.Entity.X,Y=p.Entity.Y,Day=day,End=Clock().AddSeconds(3)};
                if(inspecting.TryAdd(p.Entity.UID,pending)){p.Entity.GuardUId=p.ActiveNpc;DialyQuestsEffect.CheckGuard(p);d.Text("Stand still a moment. Let this soldier hear your prayer.");}
                else d.Text("Finish your prayer before tending another soul.");
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null)return false;
            uint npc=p.ActiveNpc;bool envoy=npc==DailyQuestEnvoySystem.NpcId && (request.OptionID==3 || request.OptionID==24);
            if(npc!=Guardian && npc!=LegacyGuardian && !Remains(npc) && !envoy)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if((request.NpcID!=0 && request.NpcID!=npc) || !Q.Near(p,npc,envoy?(ushort)1002:(ushort)1000))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(option==255) { Offer canceled;offers.TryRemove(p.Entity.UID,out canceled);return true; }
                    if(envoy) {
                        Refresh(p);
                        if(option==3 && p.Entity.Level>=70){var target=Kernel.Maps[(ushort)1000].Npcs[Guardian];ushort x=target.X,y=target.Y;
                            if(!PotencyGuide.FindLanding(Kernel.Maps[(ushort)1000],ref x,ref y))throw new InvalidOperationException("Guardian landing blocked.");p.Entity.Teleport(1000,x,y);}
                        else {d.Text("The Desert Guardian seeks someone to release three fallen soldiers. Reach level 70 and I can send you to him.");d.Option("Take me to the Desert Guardian.",3);}
                    }else if(Remains(npc)){if(option==0 || option==1)Inspect(p,d);}
                    else if(option==1)d.Text(Begin(p));
                    else if(option==3)d.Text(Claim(p,false));
                    else if(option==2) {
                        d.Text(Begin(p));using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                            var r=Read(c,t,p.Entity.UID,Day(Clock()));t.Commit();if(p.Entity.Level>=70 && r!=null && r.State=="active") {
                                offers[p.Entity.UID]=new Offer{Player=p,Day=Day(Clock()),End=Clock().AddMinutes(2)};
                                d.Text("The temple can finish this duty for "+r.Fee+" CPs. You receive the gifts, but earn no quest achievement.");d.Option("Pay for the temple's help.",4);
                            }
                        }}
                    }else if(option==4) {Offer offer;if(offers.TryRemove(p.Entity.UID,out offer) && ReferenceEquals(offer.Player,p) && offer.Day==Day(Clock()) && Clock()<offer.End)d.Text(Claim(p,true,offer));else d.Text("Ask me again when you are ready for the temple's help.");}
                    else {
                        Refresh(p);d.Text("Our fallen soldiers lie among the dunes. Release three different souls, then return for my thanks.");
                        d.Option("I will help the soldiers.",1);d.Option("I have released their souls.",3);d.Option("Ask the temple to help.",2);
                    }
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"release-souls",ex);}
            }
            return true;
        }
        internal static void Tick() {
            var now=Clock();foreach(var entry in offers.ToArray())if(entry.Value.End<=now)((ICollection<KeyValuePair<uint,Offer>>)offers).Remove(entry);
            foreach(var entry in inspecting.ToArray()) {
                var pending=entry.Value;var p=pending.Player;
                if(Q.Player(p) && p.Entity.OnCheckGuard && p.Entity.MapID==1000 && p.Entity.X==pending.X && p.Entity.Y==pending.Y && now<pending.End)continue;
                if(!Q.Player(p)) {((ICollection<KeyValuePair<uint,Inspection>>)inspecting).Remove(entry);continue;}
                lock(p.ItemSyncRoot)try {
                    // Retire and settle the same prayer under the admission lock.
                    // Removing it before this lock lets another NPC request start
                    // a new prayer which this callback would then cancel.
                    if(!((ICollection<KeyValuePair<uint,Inspection>>)inspecting).Remove(entry))continue;
                    if(!Q.Player(p))continue;
                    var taken=pending;now=Clock();
                    bool valid=now>=taken.End && taken.Day==Day(now) && p.Entity.OnCheckGuard && p.Entity.X==taken.X && p.Entity.Y==taken.Y && Q.Near(p,taken.Npc,1000);
                    p.Entity.OnCheckGuard=false;DialyQuestsEffect.CheckCancelGuard(p);if(!valid)continue;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        Owner(c,t,p,taken.Day);var r=Read(c,t,p.Entity.UID,taken.Day);if(r==null || r.State!="active" || r.Seen.Length>=Required || r.Seen.Contains((long)taken.Npc))continue;
                        r.Seen=r.Seen.Concat(new[]{(long)taken.Npc}).ToArray();
                        if(Q.Execute(c,t,"UPDATE revora_soul_runs SET seen=@seen WHERE uid=@uid AND day=@day AND state='active'","seen",r.Seen,"uid",(long)p.Entity.UID,"day",taken.Day)!=1)throw new InvalidOperationException("Soul inspection was not saved.");
                        Current(p,taken.Day,taken.Npc);
                        if(p.Entity.X!=taken.X || p.Entity.Y!=taken.Y)throw new InvalidOperationException("Soul inspection moved before commit.");
                        t.Commit();Projection(p,r);p.Send(r.Seen.Length==Required?"Three souls are at peace. Return to the Desert Guardian.":"The soldier's soul is free. Seek another among the dunes.");
                    }}
                }catch(Exception ex){Q.Fail(p,"release-souls-inspection",ex);}
            }
        }
    }
}
