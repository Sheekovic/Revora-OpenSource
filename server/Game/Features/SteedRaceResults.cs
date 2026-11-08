using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class SteedRaceResults {
        internal const string RunLowKey="steed-claim-run-low",RunHighKey="steed-claim-run-high";
        internal static Guid ClaimRun(VariableVault vault) {var bytes=new byte[16];System.BitConverter.GetBytes(vault.ReadULong(RunLowKey)).CopyTo(bytes,0);System.BitConverter.GetBytes(vault.ReadULong(RunHighKey)).CopyTo(bytes,8);return new Guid(bytes);}
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action AfterFinishCommit;
        private static readonly object Gate=new object();
        private static readonly Dictionary<uint,GameState> admitted=new Dictionary<uint,GameState>();
        private static Guid active;private static ushort map,finishX,finishY;private static bool opened,initialized;
        internal static void Initialize() {
            lock(Gate){if(initialized)return;
                using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_steed_runs(
id uuid PRIMARY KEY,map integer NOT NULL,finish_x integer NOT NULL,finish_y integer NOT NULL,
state text NOT NULL CHECK(state IN('active','closed','aborted')),opened_at timestamptz,closes_at timestamptz NOT NULL,
next_rank integer NOT NULL DEFAULT 1,cp bigint[] NOT NULL,rvp bigint[] NOT NULL,gold bigint[] NOT NULL,
CHECK(cardinality(cp)=3 AND cardinality(rvp)=3 AND cardinality(gold)=3));
CREATE TABLE IF NOT EXISTS revora_steed_results(
run uuid NOT NULL REFERENCES revora_steed_runs(id),uid bigint NOT NULL,rank integer,
cp bigint NOT NULL DEFAULT 0,rvp bigint NOT NULL DEFAULT 0,gold bigint NOT NULL DEFAULT 0,
finished_at timestamptz,claimed_at timestamptz,PRIMARY KEY(run,uid),UNIQUE(run,rank));
ALTER TABLE revora_steed_runs ADD COLUMN IF NOT EXISTS soul_types bigint[] NOT NULL DEFAULT ARRAY[0,0,0]::bigint[];
ALTER TABLE revora_steed_results ADD COLUMN IF NOT EXISTS soul_type bigint NOT NULL DEFAULT 0;
ALTER TABLE revora_steed_runs ADD COLUMN IF NOT EXISTS start_x integer;
ALTER TABLE revora_steed_runs ADD COLUMN IF NOT EXISTS start_y integer;
ALTER TABLE revora_steed_runs ADD COLUMN IF NOT EXISTS finish_radius integer;
ALTER TABLE revora_steed_results ADD COLUMN IF NOT EXISTS course_steps integer NOT NULL DEFAULT 0;
ALTER TABLE revora_steed_results ADD COLUMN IF NOT EXISTS course_tiles integer NOT NULL DEFAULT 0;
UPDATE revora_steed_runs SET state='aborted' WHERE state='active';");
                    // Initialization runs before Account/Game listeners. Return only
                    // saved riders on their recorded, ended course; prizes stay intact.
                    Q.Execute(c,null,@"UPDATE entities e SET mapid=@town,x=@x,y=@y
WHERE e.mapid=ANY(@tracks) AND EXISTS(
 SELECT 1 FROM revora_steed_results r JOIN revora_steed_runs b ON b.id=r.run
 WHERE r.uid=e.uid AND b.map=e.mapid AND b.state IN('aborted','closed'))",
                        "town",(int)SteedRaceSystem.TwinCityMap,"x",(int)SteedRaceSystem.TwinCityX,"y",(int)SteedRaceSystem.TwinCityY,
                        "tracks",new int[]{9929,2066,1950,2064,2061,2063,2062});
                }active=Guid.Empty;opened=false;admitted.Clear();SteedRaceCourse.Reset();initialized=true;
            }
        }
        internal static Guid Begin(ushort track,ushort x,ushort y) {
            lock(Gate){if(!initialized)throw new InvalidOperationException("Steed results are not initialized.");
                Guid id=Guid.NewGuid();using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"UPDATE revora_steed_runs SET state='closed' WHERE state='active'");
                    if(Q.Execute(c,t,@"INSERT INTO revora_steed_runs(id,map,finish_x,finish_y,state,closes_at,cp,rvp,gold,soul_types)
VALUES(@id,@map,@x,@y,'active',@until,@cp,@rp,@gold,@souls)","id",id,"map",(int)track,"x",(int)x,"y",(int)y,"until",Clock().AddMinutes(10),
                        "souls",new long[]{QuestSoulReward.Create().ID,QuestSoulReward.Create().ID,QuestSoulReward.Create().ID},
                        "cp",new long[]{Config.SteedRaceFirstCp,Config.SteedRaceSecondCp,Config.SteedRaceThirdCp},
                        "rp",new long[]{Config.SteedRaceFirstRevoraPoints,Config.SteedRaceSecondRevoraPoints,Config.SteedRaceThirdRevoraPoints},
                        "gold",new long[]{checked((long)Config.SteedRaceFirstGold),checked((long)Config.SteedRaceSecondGold),checked((long)Config.SteedRaceThirdGold)})!=1)
                        throw new InvalidOperationException("Steed run was not saved.");t.Commit();}}
                SteedRaceCourse.Reset();active=id;map=track;finishX=x;finishY=y;opened=false;admitted.Clear();return id;
            }
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.Variables!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade && p.Entity.PokerTable==0
            && p.Spells.ContainsKey(SteedRaceSystem.RideSpell) && p.Equipment!=null && !p.Equipment.Free(ConquerItem.Steed);}
        internal static bool Admit(Guid run,GameState p) {
            if(!Ready(p))return false;
            lock(p.ItemSyncRoot)lock(Gate)try{
                if(!Ready(p) || run==Guid.Empty || run!=active || !SteedRaceCourse.Configured(run) || opened || admitted.Count>=SteedRaceSystem.Capacity || admitted.ContainsKey(p.Entity.UID)
                    || p.Variables.ReadULong(SteedRaceSystem.ClaimKey)!=0)return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_steed_runs WHERE id=@id AND state='active' AND opened_at IS NULL AND closes_at>@now FOR UPDATE","id",run,"now",Clock()))
                        if(q.ExecuteScalar()==null)return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_steed_results(run,uid) VALUES(@run,@uid)","run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Steed admission was not saved.");t.Commit();}}
                admitted.Add(p.Entity.UID,p);SteedRaceCourse.Admit(run,p);return true;
            }catch(Exception ex){Q.Fail(p,"steed-admission",ex);return false;}
        }
        internal static bool Open(Guid run) {
            lock(Gate){if(run==Guid.Empty || run!=active || opened)return false;
                using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_steed_runs SET opened_at=@now WHERE id=@id AND state='active' AND opened_at IS NULL AND closes_at>@now","now",Clock(),"id",run)!=1)return false;}
                opened=true;SteedRaceCourse.Open(run);return true;}
        }
        internal static uint Prize(Guid run,uint uid) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT cp FROM revora_steed_results WHERE run=@run AND uid=@uid AND rank BETWEEN 1 AND 3","run",run,"uid",(long)uid)){
                object value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Steed prize snapshot missing.");return checked(Convert.ToUInt32(value));}}
        }
        internal static void Withdraw(GameState p){if(p==null || p.Entity==null)return;lock(Gate){GameState current;if(admitted.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p))admitted.Remove(p.Entity.UID);SteedRaceCourse.Remove(p);}}
        internal static void End(Guid run) {
            lock(Gate){if(run==Guid.Empty || run!=active)return;
                using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_steed_runs SET state='closed' WHERE id=@id AND state='active'","id",run);}
                active=Guid.Empty;opened=false;admitted.Clear();SteedRaceCourse.Reset();}
        }
        internal static int Finish(Guid run,GameState p) {
            if(!Ready(p))return 0;
            lock(p.ItemSyncRoot)lock(p.Variables.SyncRoot)lock(Gate)try{
                GameState current;
                if(!Ready(p) || run==Guid.Empty || run!=active || !opened || !admitted.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p)
                    || p.Entity.MapID!=map || p.Map.ID!=map || !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,finishX,finishY)>22 || p.Variables.ReadULong(SteedRaceSystem.ClaimKey)!=0)return 0;
                int courseTiles;int courseSteps=SteedRaceCourse.Completed(run,p,out courseTiles);if(courseSteps==0)return 0;
                int rank;bool refresh=false;byte[] merged=null;DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);long cp,rp,gold,soul;
                    using(var q=Q.Command(c,t,"SELECT next_rank,cp,rvp,gold,soul_types FROM revora_steed_runs WHERE id=@id AND state='active' AND opened_at IS NOT NULL AND opened_at<=@now AND closes_at>@now FOR UPDATE","id",run,"now",now))
                    using(var r=q.ExecuteReader()){if(!r.Read())return 0;rank=Convert.ToInt32(r[0]);if(rank<1 || rank>SteedRaceSystem.Capacity)throw new InvalidOperationException("Steed rank outside capacity.");
                        cp=rank<=3?((long[])r[1])[rank-1]:0;rp=rank<=3?((long[])r[2])[rank-1]:0;gold=rank<=3?((long[])r[3])[rank-1]:0;soul=rank<=3?((long[])r[4])[rank-1]:0;}
                    if(Q.Execute(c,t,@"UPDATE revora_steed_results SET rank=@rank,cp=@cp,rvp=@rp,gold=@gold,soul_type=@soul,finished_at=@now,course_steps=@steps,course_tiles=@tiles
WHERE run=@run AND uid=@uid AND rank IS NULL","soul",soul,"steps",courseSteps,"tiles",courseTiles,"rank",rank,"cp",cp,"rp",rp,"gold",gold,"now",now,"run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Steed finish was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_steed_runs SET next_rank=next_rank+1 WHERE id=@id AND next_rank=@rank","id",run,"rank",rank)!=1)
                        throw new InvalidOperationException("Steed finish order was not saved.");
                    if(rank<=3){byte[] before;using(var q=Q.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))before=q.ExecuteScalar() as byte[];
                        if(before==null || before.Length<4)throw new InvalidOperationException("Steed finish vault is missing.");
                        var saved=new VariableVault();saved.Deserialize(before);
                        if(saved.ReadULong(SteedRaceSystem.ClaimKey)!=0 || !p.Variables.TryMerge(before,new[]{SteedRaceSystem.ClaimKey,RunLowKey,RunHighKey},new DynamicVariable[]{(DynamicVariable)(uint)rank,(DynamicVariable)System.BitConverter.ToUInt64(run.ToByteArray(),0),(DynamicVariable)System.BitConverter.ToUInt64(run.ToByteArray(),8)},out merged))
                            throw new InvalidOperationException("Steed finish cannot replace another prize.");
                        if(Q.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before","data",merged,"uid",(long)p.Entity.UID,"before",before)!=1)
                            throw new InvalidOperationException("Steed prize marker was not saved.");}
                    refresh=ProgressionEvidence.Complete(c,t,p.Entity.UID,11113,"steed-course-v1",run.ToString("N"),1);
                    if(rank==1)refresh=ProgressionEvidence.Complete(c,t,p.Entity.UID,11122,"steed-course-v1",run.ToString("N"),1) || refresh;
                    if(!Ready(p))throw new InvalidOperationException("Steed finisher changed.");t.Commit();}}
                admitted.Remove(p.Entity.UID);SteedRaceCourse.Remove(p);if(AfterFinishCommit!=null)AfterFinishCommit();if(merged!=null)p.Variables.ReplaceContents(merged);if(refresh)Q.Refresh(p);return rank;
            }catch(Exception ex){Q.Fail(p,"steed-finish",ex);return 0;}
        }
    }
}
