using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Recurring weekend admission; previously issued beads remain finishable.
    internal static class AnniversaryQuest
    {
        internal const uint Npc=900028,BeadType=3001172,Card=3006048,Need=15000;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        internal static bool Weekend(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Anniversary calendar requires UTC.");
            var day=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone).DayOfWeek;
            return day==DayOfWeek.Saturday || day==DayOfWeek.Sunday;
        }
        internal static void PlaceNpc() {
            using(var hash=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1002.map")))
                if(System.BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")!="9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10")
                    throw new InvalidOperationException("Anniversary terrain changed.");
            Map map;MapsTable.MapInformation info;
            if(!Kernel.Maps.TryGetValue(1002,out map) || map.ID!=1002 || map.BaseID!=1002
                || !MapsTable.MapInformations.TryGetValue(1002,out info) || info.ID!=1002 || info.BaseID!=1002 || info.Owner!=0)throw new InvalidOperationException("Anniversary public site is unavailable.");
            Map retiredMap=null;Revora.Interfaces.INpc retired=null;int identities=0;
            foreach(var world in Kernel.Maps.Values) {
                Revora.Interfaces.INpc existing;if(!world.Npcs.TryGetValue(Npc,out existing))continue;
                if(++identities>1)throw new InvalidOperationException("Anniversary NPC identity collision.");
                if(ReferenceEquals(world,map) && existing.X==319 && existing.Y==311 && existing.Mesh==19936 && existing.Type==Enums.NpcType.Talker)continue;
                // Exact retired catalog profile, including the two observed
                // jail placements. Do not replace an unrelated NPC sharing its ID.
                if(world.ID!=6000 || world.BaseID!=6000 || existing.MapID!=6000 || existing.Name!="AdmAniversario"
                    || existing.Mesh!=34240 || existing.Type!=Enums.NpcType.Talker
                    || !((existing.X==102 && existing.Y==78) || (existing.X==90 && existing.Y==78)))
                    throw new InvalidOperationException("Anniversary NPC identity collision.");
                retiredMap=world;retired=existing;
            }
            if(map.Npcs.ContainsKey(Npc))return;
            if(!map.Floor[319,311,MapObjectType.Player] || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,319,311)<2))throw new InvalidOperationException("Anniversary NPC site is occupied.");
            if(retired!=null) {
                Revora.Interfaces.INpc secondary;
                if(Map.SecoundNpc.TryGetValue(Npc,out secondary) && !ReferenceEquals(secondary,retired))throw new InvalidOperationException("Anniversary secondary NPC identity collision.");
                retiredMap.RemoveNpc(retired);Map.SecoundNpc.Remove(Npc);
            }
            map.AddNpc(new NpcSpawn{UID=Npc,MapID=1002,X=319,Y=311,Mesh=19936,Name="AnniversaryEnvoy",Type=Enums.NpcType.Talker});
        }
        private static bool enabled;
        private sealed class Run {internal Guid Id;internal string Day,Done;internal uint Bead,Spirits;internal bool Bound,Valuable;}
        private sealed class Life {internal bool Credited;}
        private static readonly ConditionalWeakTable<GameState,Run> displays=new ConditionalWeakTable<GameState,Run>();
        private static readonly ConditionalWeakTable<Entity,Life> lives=new ConditionalWeakTable<Entity,Life>();
        private static readonly uint[] dailyBeads={729611,729612,729613,729614,729703};
        private static string Day(DateTime now){return MagnoliaQuest.Day(now);}
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Entity.Level>=140 && p.Entity.Reborn==2 && p.Entity.VIPLevel>=6
            && p.Entity.SubClasses!=null && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Site(GameState p) {
            Map map;MapsTable.MapInformation info;Revora.Interfaces.INpc npc;
            return Q.Player(p) && p.Entity.MapID==1002 && Kernel.Maps.TryGetValue(1002,out map) && ReferenceEquals(p.Map,map) && map.BaseID==1002
                && MapsTable.MapInformations.TryGetValue(1002,out info) && info.ID==1002 && info.BaseID==1002 && info.Owner==0
                && Q.Near(p,Npc,1002) && map.Npcs.TryGetValue(Npc,out npc) && npc.X==319 && npc.Y==311 && npc.Mesh==19936 && npc.Type==Enums.NpcType.Talker;
        }
        private static bool Near(GameState p){return Ready(p) && Site(p);}
        internal static void Initialize(){
            foreach(uint id in new[]{BeadType,Card})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Anniversary catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_anniversary_runs(
id uuid PRIMARY KEY,uid bigint NOT NULL,day varchar(8) NOT NULL,completed_day varchar(8) NOT NULL DEFAULT '',legacy boolean NOT NULL DEFAULT false,
bead bigint NOT NULL,bound boolean NOT NULL DEFAULT false,spirits bigint NOT NULL CHECK(spirits BETWEEN 0 AND 15000),replaced_beads bigint[] NOT NULL DEFAULT '{}',
reward_uids bigint[] NOT NULL DEFAULT '{}',created_at timestamptz NOT NULL,completed_at timestamptz,UNIQUE(uid,day));
ALTER TABLE revora_anniversary_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS revora_anniversary_active ON revora_anniversary_runs(uid) WHERE completed_day='';");}enabled=true;
        }
        internal static bool BlocksSoul(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now){
            if(!enabled)return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_anniversary_runs WHERE uid=@uid AND (completed_day='' OR completed_day>=@day))","uid",(long)p.Entity.UID,"day",Day(now)))return Equals(q.ExecuteScalar(),true);
        }
        private static bool SoulBusy(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now){
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_soul_ball_runs WHERE uid=@uid AND (completed_day='' OR completed_day>=@day))","uid",(long)p.Entity.UID,"day",Day(now)))return Equals(q.ExecuteScalar(),true);
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT level,reborn,viplevel FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])<140 || Convert.ToInt32(r[1])!=2 || Convert.ToInt32(r[2])<6)throw new InvalidOperationException("Anniversary eligibility changed.");
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT id,day,completed_day,bead,spirits,bound,valuable_reward FROM revora_anniversary_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read())return new Run{Id=r.GetGuid(0),Day=r.GetString(1),Done=r.GetString(2),Bead=Convert.ToUInt32(r[3]),Spirits=Convert.ToUInt32(r[4]),Bound=r.GetBoolean(5),Valuable=r.GetBoolean(6)};return null;
        }
        private static bool Active(Run r,DateTime now){return r!=null && r.Done=="" && string.CompareOrdinal(r.Day,Day(now))<=0;}
        private static bool Usable(ConquerItem i){return i!=null && i.ID==BeadType && i.Position==0 && i.Warehouse==0 && i.StackSize<=1 && !i.InWardrobe && i.Lock==0 && !i.Suspicious && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static ConquerItem Bead(GameState p,Run r){ConquerItem i;return r!=null && p.Inventory.TryGetItem(r.Bead,out i) && Usable(i)?i:null;}
        private static void Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i,bool remove){
            string where=@" WHERE uid=@item AND id=3001172 AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'";
            using(var q=Q.Command(c,t,(remove?"DELETE FROM items":"SELECT count(*) FROM items")+where,"item",(long)i.UID,"uid",(long)p.Entity.UID,"stack",(int)i.StackSize,"bound",i.Bound?1:0))
                if((remove?q.ExecuteNonQuery():Convert.ToInt32(q.ExecuteScalar()))!=1)throw new InvalidOperationException("Anniversary bead ownership changed.");
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Anniversary bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Anniversary bag requires reload.");
        }
        private static void Gift(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i){
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,"bound",i.Bound?1:0,"dur",(int)i.Durability,"max",(int)i.MaximDurability)!=1)throw new InvalidOperationException("Anniversary gift was not saved.");
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r,bool legacy,DateTime now){
            if(Q.Execute(c,t,@"INSERT INTO revora_anniversary_runs(id,uid,day,completed_day,legacy,bead,spirits,bound,created_at,valuable_reward)
VALUES(@id,@uid,@day,@done,@legacy,@bead,@spirits,@bound,@now,@valuable)","id",r.Id,"uid",(long)p.Entity.UID,"day",r.Day,"done",r.Done,"legacy",legacy,"bead",(long)r.Bead,"spirits",(long)r.Spirits,"bound",r.Bound,"now",now,"valuable",r.Valuable)!=1)throw new InvalidOperationException("Anniversary run was not saved.");
        }
        private static Run Import(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now){
            var r=Read(c,t,p);if(r!=null)return r;
            bool done=false;uint spirits=0;
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.spiritbeads,d.spiritbeadscount FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read()){
                done=!d.IsDBNull(0) && !d.IsDBNull(1) && DateTime.FromBinary(Convert.ToInt64(d[0])).Date==now.ToLocalTime().Date && Convert.ToBoolean(d[1]);
                spirits=d.IsDBNull(2)?0:Convert.ToUInt32(d[2]);
            }
            r=new Run{Id=Guid.NewGuid(),Day=Day(now),Done=done?Day(now):""};
            if(done){Insert(c,t,p,r,true,now);return r;}
            var beads=p.Inventory.Objects.Where(i=>i.ID==BeadType).ToArray();
            if(beads.Length!=1 || !Usable(beads[0]) || p.Inventory.Objects.Any(i=>dailyBeads.Contains(i.ID)) || SoulBusy(c,t,p,now))return null;
            Owned(c,t,p,beads[0],false);r.Bead=beads[0].UID;r.Bound=beads[0].Bound;r.Spirits=Math.Min(spirits,Need);Insert(c,t,p,r,true,now);return r;
        }
        private static void Project(GameState p,Run r,DateTime now){displays.Remove(p);if(r==null)return;displays.Add(p,r);p.Entity.spiritBeadsCount=r.Done==""?r.Spirits:0;if(r.Done!="" && string.CompareOrdinal(r.Done,Day(now))>=0)p.Entity.SpiritBeads=true;}
        internal static bool CompletedToday(GameState p){Run r;return p!=null && displays.TryGetValue(p,out r) && r.Done!="" && string.CompareOrdinal(r.Done,Day(Clock()))>=0;}
        internal static void Refresh(GameState p){
            if(!enabled || !Q.Player(p,false))return;lock(p.ItemSyncRoot)try{using(var c=DataHolder.MySqlConnection){c.Open();Project(p,Read(c,null,p),Clock());}}
            catch(Exception ex){Q.Fail(p,"anniversary-refresh",ex);}
        }
        private static string Issue(GameState p,DateTime now,bool replace){
            if(!replace && !Weekend(now))return "New beads are entrusted on Saturdays and Sundays, Pacific time. You may still finish the bead you carry.";
            Run r;ConquerItem bead;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);r=Import(c,t,p,now);
                if(SoulBusy(c,t,p,now))return "Your Spirit Ball commission is still active, or you have already earned today's reward.";
                if(r!=null && (r.Done!="" && string.CompareOrdinal(r.Done,Day(now))>=0 || string.CompareOrdinal(r.Day,Day(now))>0))return "Return tomorrow for another bead.";
                if(p.Inventory.Objects.Any(i=>i.ID==BeadType || dailyBeads.Contains(i.ID)))return "Keep the bead you carry. Use it to check your spirits.";
                bool active=Active(r,now);if(active && !replace)return "Your bead is already entrusted to you. Check your storage, or ask for a lost bead.";
                if(!active && replace)return "You have no lost bead to replace.";
                if(p.Inventory.Count>=40)return "Make room for your bead.";Bag(c,t,p);
                if(active)using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@bead)","bead",(long)r.Bead))if(Equals(q.ExecuteScalar(),true))return "Your original bead still exists. Check your storage.";
                bead=Q.New(BeadType,active && r.Bound);Gift(c,t,p,bead);
                if(active){if(Q.Execute(c,t,"UPDATE revora_anniversary_runs SET bead=@bead,replaced_beads=array_append(replaced_beads,@old) WHERE id=@id AND completed_day='' AND bead=@old","bead",(long)bead.UID,"old",(long)r.Bead,"id",r.Id)!=1)throw new InvalidOperationException("Anniversary replacement changed.");r.Bead=bead.UID;}
                else {r=new Run{Valuable=true,Id=Guid.NewGuid(),Day=Day(now),Done="",Bead=bead.UID,Spirits=3};Insert(c,t,p,r,false,now);}
                if(!Near(p))throw new InvalidOperationException("Anniversary issuer changed.");
                if(!active && (!Weekend(Clock()) || Day(Clock())!=Day(now)))return "The rite has turned to a new day. Speak with me again.";t.Commit();
            }}Q.Install(p,new uint[0],new[]{bead});Project(p,r,now);return "Carry this bead on your hunts. Release its spirits when it holds 15,000.";
        }
        private static string Complete(GameState p,ConquerItem requested,DateTime now){
            Run r;ConquerItem bead;ConquerItem[] cards;uint rvp,bless;ulong money;ushort study;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);r=Import(c,t,p,now);bead=Bead(p,r);
                if(!Active(r,now) || bead==null || !ReferenceEquals(bead,requested)){t.Commit();Project(p,r,now);return "Use the original unlocked bead entrusted to you.";}
                if(SoulBusy(c,t,p,now) || p.Inventory.Objects.Any(i=>dailyBeads.Contains(i.ID)))return "Resolve your Spirit Ball commission first.";
                if(r.Spirits<Need){t.Commit();Project(p,r,now);return "Your Anniversary bead holds "+r.Spirits+" of 15,000 spirits.";}
                if(p.Inventory.Count-1+(r.Valuable?21:20)>40)return r.Valuable?"Leave room for twenty cards and a bound P7 soul.":"Leave room for twenty Anniversary Cards. Your bead will be consumed.";
                if((ulong)p.Entity.RevoraPoint+50000>uint.MaxValue || p.Entity.Money>(ulong)long.MaxValue-50000000 || p.Entity.SubClasses.StudyPoints+100>ushort.MaxValue || (ulong)p.Entity.HeavenBlessing+172800>uint.MaxValue)return "Spend some points, silver or blessing time before claiming your reward.";
                Bag(c,t,p);rvp=p.Entity.RevoraPoint+50000;money=p.Entity.Money+50000000;study=(ushort)(p.Entity.SubClasses.StudyPoints+100);bless=p.Entity.HeavenBlessing+172800;
                if(Q.Execute(c,t,@"UPDATE entities SET revorapoint=@rvp,studypoints=@study WHERE uid=@uid AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy",
                    "rvp",(long)rvp,"study",(int)study,"uid",(long)p.Entity.UID,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)throw new InvalidOperationException("Anniversary balances changed.");
                SilverBalance.Persist(c,t,p,money);BlessingBalance.Persist(c,t,p,bless);Owned(c,t,p,bead,true);
                cards=Enumerable.Range(0,20).Select(_=>Q.New(Card,bead.Bound)).Concat(r.Valuable?new[]{QuestSoulReward.Create()}:new ConquerItem[0]).ToArray();foreach(var card in cards)Gift(c,t,p,card);
                if(Q.Execute(c,t,"UPDATE revora_anniversary_runs SET completed_day=@day,completed_at=@now,reward_uids=@cards WHERE id=@id AND uid=@uid AND completed_day='' AND spirits=15000","day",Day(now),"now",now,"cards",cards.Select(i=>(long)i.UID).ToArray(),"id",r.Id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Anniversary receipt was not saved.");
                StudyRewardEvidence.Record(c,t,p,100,"anniversary:"+r.Id.ToString("N"));
                if(!Ready(p))throw new InvalidOperationException("Anniversary claimant changed.");t.Commit();
            }}Q.Install(p,new[]{bead.UID},cards);p.Entity.ApplyCommittedRevoraPoints(rvp);SilverBalance.ApplyCommitted(p,money);BlessingBalance.ApplyCommitted(p,bless);p.Entity.SubClasses.StudyPoints=study;
            r.Done=Day(now);Project(p,r,now);Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=study,StudyReceive=100}.ToArray());return (r.Valuable?"A bound P7 soul is yours. ":"")+"The spirits are free. Take your twenty cards, 50,000 RVPs, 50 million silver, 100 Study Points and two days of blessing.";
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=BeadType)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{if(Ready(p))p.Send(Complete(p,item,Clock()));}catch(Exception ex){Q.Fail(p,"anniversary-reward",ex);}return true;
        }
        internal static bool HandleNpc(GameState p,NpcRequest req){
            if(p==null || req==null || p.ActiveNpc!=Npc)return false;
            if(req.IsDialogueClose){p.ActiveNpc=0;return true;}
            if(!Site(p) || req.NpcID!=0 && req.NpcID!=Npc)return true;
            lock(p.ItemSyncRoot)try{
                if(!Site(p))return true;var d=new Npcs(p);d.Avatar(84);
                if(!Ready(p))d.Text("Return at level 140, twice reborn, with VIP 6 or higher.");
                else if(req.OptionID==8 || req.OptionID==35)d.Text(Issue(p,Clock(),req.OptionID==35));
                else if(req.OptionID==9)d.Text("Release 15,000 spirits for twenty cards, 50,000 RVPs, 50 million silver, 100 Study Points and two days of blessing. New beads also earn a bound P7 soul.");
                else if(req.OptionID==0 || req.OptionID==7){
                    d.Text(Weekend(Clock())?"The weekend rite is open. Gather 15,000 spirits for a bound P7 soul and Anniversary prizes. This shares your daily Spirit Ball reward.":"New beads await on Saturdays and Sundays, Pacific time. Finish the bead you carry, or ask me to replace a lost one.");
                    if(Weekend(Clock()))d.Option("Entrust me with a bead.",8);d.Option("I lost my bead.",35);d.Option("Show me the rewards.",9);
                }
                else return true;
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"anniversary-npc",ex);}return true;
        }
        internal static void Revived(Entity monster){if(monster!=null && !monster.Dead)lock(monster)lives.Remove(monster);}
        internal static void OnDeath(Entity monster,Entity killer){
            var p=killer==null?null:killer.Owner;
            if(!Ready(p) || !DailySoulBall.WildernessDeath(monster,killer))return;
            lock(p.ItemSyncRoot)lock(monster)try{
                if(!Ready(p) || !DailySoulBall.WildernessDeath(monster,killer))return;var life=lives.GetValue(monster,_=>new Life());if(life.Credited)return;
                if(!p.Inventory.Objects.Any(i=>i.ID==BeadType)){life.Credited=true;return;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var r=Import(c,t,p,Clock());var bead=Bead(p,r);
                    if(!Active(r,Clock()) || bead==null || r.Spirits>=Need || SoulBusy(c,t,p,Clock()) || p.Inventory.Objects.Any(i=>dailyBeads.Contains(i.ID))){life.Credited=true;return;}
                    Owned(c,t,p,bead,false);byte level=monster.MonsterInfo.Level;uint gain=level<=70?1u:level<100?2u:level<120?3u:4u;uint next=Math.Min(Need,r.Spirits+gain);
                    if(Q.Execute(c,t,"UPDATE revora_anniversary_runs SET spirits=@next WHERE id=@id AND spirits=@old AND completed_day=''","next",(long)next,"id",r.Id,"old",(long)r.Spirits)!=1)throw new InvalidOperationException("Anniversary spirits were not saved.");
                    if(!Ready(p) || !DailySoulBall.WildernessDeath(monster,killer))throw new InvalidOperationException("Anniversary hunter changed.");t.Commit();life.Credited=true;r.Spirits=next;Project(p,r,Clock());
                    p.Send(next==Need?"Your Anniversary bead is full. Use it to release its spirits.":"Your Anniversary bead holds "+next+" of 15,000 spirits.");
                }}
            }catch(Exception ex){Q.Fail(p,"anniversary-hunt",ex);}
        }
    }
}
