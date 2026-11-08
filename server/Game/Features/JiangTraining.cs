using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class JiangTraining
    {
        internal static Action<GameState> BeforeCommit, BeforeInstall;
        internal static Func<int,int> PillRoll=max=>Kernel.Random.Next(max);
        internal static void InitializePills() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(3003126))throw new InvalidOperationException("Senior Training Pill catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_jiang_pill_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),item_uid bigint NOT NULL,
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
armed_at timestamptz NOT NULL,consumed_at timestamptz,quality integer,
CHECK((consumed_at IS NULL AND quality IS NULL) OR (consumed_at IS NOT NULL AND quality BETWEEN 1 AND 6)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_jiang_pill_pending ON revora_jiang_pill_uses(uid) WHERE consumed_at IS NULL;");}
        }
        internal static void ArmPill(GameState p,ConquerItem item) {
            if(!Ready(p) || item==null || item.ID!=3003126 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try {lock(p.ItemSyncRoot) {
                ConquerItem owned;
                if(!Ready(p) || p.Entity.MyJiang.SaveBlocked || !p.Entity.MyJiang.HasSavedData
                    || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned)
                    || item.Position!=0 || item.Warehouse!=0 || item.Lock!=0 || item.Suspicious || item.InWardrobe
                    || item.Days!=0 || item.TimeLeftInMinutes!=0)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Jiang pill owner missing.");
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL)","uid",(long)p.Entity.UID))
                        if((bool)q.ExecuteScalar()){p.Send("Your next training is already strengthened.");return;}
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=3003126 AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"stack",(int)item.StackSize,"bound",item.Bound?1:0)!=1)
                        throw new InvalidOperationException("Jiang pill ownership changed.");
                    Q.Execute(c,t,@"INSERT INTO revora_jiang_pill_uses(id,uid,item_uid,before_stack,bound,armed_at)
VALUES(@id,@uid,@item,@stack,@bound,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "stack",(int)item.StackSize,"bound",item.Bound,"now",DateTime.UtcNow);
                    var check=BeforeCommit;if(check!=null)check(p);
                    if(!Ready(p) || p.Entity.MyJiang.SaveBlocked)throw new InvalidOperationException("Jiang pill session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);
                if(item.StackSize<=1)Q.Install(p,new[]{item.UID},new ConquerItem[0]);
                else {item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                p.Send("Your next Jiang Hu training is strengthened. The blessing lasts until you train.");
            }}catch(Exception ex){Q.Fail(p,"jiang-pill",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static bool Ready(GameState p) {
            return Q.Player(p) && p.Entity.MyJiang!=null && p.Entity.SubClasses!=null
                && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;
        }
        internal static void Create(GameState p,string name) {
            if(!Q.Player(p) || name==null || name.Length<1 || name.Length>16 || !Game.JiangHu.AllowNameCaracters(name)
                || p.Entity.MyJiang!=null || p.Entity.SubClasses==null || p.Trade==null || p.Trade.InTrade || p.Booth!=null
                || !SessionInstall.TryEnter(p.Entity.UID))return;
            uint uid=p.Entity.UID;Game.JiangHu proposed=null;
            try {lock(p.ItemSyncRoot) {
                if(!Q.Player(p) || p.Entity.MyJiang!=null || p.Trade.InTrade || p.Booth!=null)return;
                ushort study=p.Entity.SubClasses.StudyPoints;ushort nextStudy;int award;string after;
                proposed=new Game.JiangHu(0){UID=uid,OwnName=p.Entity.Name.Replace("#"," "),JiangName=name,Level=p.Entity.Level};
                proposed.Stagers[0].Activate=true;proposed.CreateTime();after=proposed.ToString();
                using(var connection=DataHolder.MySqlConnection){connection.Open();using(var transaction=connection.BeginTransaction()) {
                    Q.Execute(connection,transaction,"SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'");
                    using(var command=Q.Command(connection,transaction,"SELECT studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid)) {
                        var value=command.ExecuteScalar();if(value==null || value==DBNull.Value || Convert.ToUInt16(value)!=study)
                            throw new InvalidOperationException("Jiang creation Study Points changed.");
                    }
                    bool exists;
                    using(var command=Q.Command(connection,transaction,"SELECT data FROM jianghu WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                    using(var reader=command.ExecuteReader()) {
                        exists=reader.Read();
                        if(exists && !reader.IsDBNull(0) && !string.IsNullOrEmpty(reader.GetString(0)))return;
                    }
                    // Existing blank legacy rows do not prove an unclaimed starting gift.
                    award=exists?0:100;if(study>ushort.MaxValue-award)return;nextStudy=(ushort)(study+award);
                    if(Q.Execute(connection,transaction,"UPDATE entities SET studypoints=@study WHERE uid=@uid","study",(int)nextStudy,"uid",(long)uid)!=1)
                        throw new InvalidOperationException("Jiang creation purse missing.");
                    string sql=exists?"UPDATE jianghu SET data=@data WHERE uid=@uid":"INSERT INTO jianghu(uid,data) VALUES(@uid,@data)";
                    if(Q.Execute(connection,transaction,sql,"uid",(long)uid,"data",after)!=1)throw new InvalidOperationException("Jiang creation row missing.");
                    var beforeCommit=BeforeCommit;if(beforeCommit!=null)beforeCommit(p);
                    if(!Q.Player(p) || p.Entity.MyJiang!=null || p.Entity.SubClasses.StudyPoints!=study || p.Trade.InTrade || p.Booth!=null)
                        throw new InvalidOperationException("Jiang creation session changed.");
                    transaction.Commit();
                }}
                var beforeInstall=BeforeInstall;if(beforeInstall!=null)beforeInstall(p);
                proposed.SavedData=after;proposed.HasSavedData=true;p.Entity.SubClasses.StudyPoints=nextStudy;
                Game.JiangHu.JiangHuClients[uid]=proposed;p.Entity.MyJiang=proposed;
                proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.SetName,uid.ToString(),proposed.Stage.ToString(),proposed.Star.ToString());
                proposed.SendStatus(p,p);proposed.SendStatusMode(p);
                proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.UpdateTime,proposed.FreeCourse.ToString(),proposed.Time.ToString());
                proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.UpdateTalent,uid.ToString(),proposed.Talent.ToString());
                if(award!=0)p.Send(new SubClassShowFull(0){ID=8,Study=nextStudy,StudyReceive=(ushort)award}.ToArray());
                Q.Refresh(p);
            }}catch(Exception ex) {
                if(proposed!=null) {proposed.SaveBlocked=true;Game.JiangHu.JiangHuClients.TryAdd(uid,proposed);if(p.Entity.MyJiang==null)p.Entity.MyJiang=proposed;}
                Q.Fail(p,"jiang-create",ex);
            }finally {SessionInstall.Exit(uid);}
        }
        internal static void Train(GameState p,byte action,byte star,byte stage) {
            if(action>1 || star<1 || star>9 || stage<1 || stage>9)return;
            Change(p,action,star,stage);
        }
        internal static void Restore(GameState p) { Change(p,2,0,0); }
        private static void Change(GameState p,byte action,byte star,byte stage) {
            if(!Ready(p)
                || !SessionInstall.TryEnter(p.Entity.UID))return;
            uint uid=p.Entity.UID;Game.JiangHu original=null;
            try {lock(p.ItemSyncRoot) {
                if(!Ready(p))return;original=p.Entity.MyJiang;
                lock(original.sync) {
                    Game.JiangHu.OldStar previous=null;
                    if(action==2) {
                        previous=original.MyOldStar;
                        if(previous==null || previous.Star==null || !previous.Star.Activate
                            || previous.Stage<1 || previous.Stage>9 || previous.PositionStar<1 || previous.PositionStar>9)return;
                        stage=previous.Stage;star=previous.PositionStar;
                    }
                    Revora.Interfaces.IJiangHu registered;
                    if(original.UID!=uid || original.SaveBlocked || !original.HasSavedData
                        || !Game.JiangHu.JiangHuClients.TryGetValue(uid,out registered) || !ReferenceEquals(original,registered)
                        || !original.Stagers[stage-1].Activate)return;
                    uint cp=p.Entity.ConquerPoints;ushort study=p.Entity.SubClasses.StudyPoints;
                    uint cost=action==2?Config.JiangHuRestoreCp:action==1?Game.JiangHu.GetTrainingCost(original.RoundBuyPoints):0;
                    if(cp<cost)return;
                    if(action==1 && (cp<cost || study>ushort.MaxValue-20 || original.FreeCourse>uint.MaxValue-10000))return;
                    if(action==0 && (study<Config.JiangHuFreeTrainingStudyPoints || original.Talent<1 || original.FreeCourse<10000))return;
                    string current=original.ToString(),saved=original.SavedData;
                    var proposed=new Game.JiangHu(0);proposed.Load(current);
                    if(!proposed.HasSavedData || proposed.UID!=uid)throw new InvalidOperationException("Jiang working state invalid.");
                    proposed.Random=original.Random;
                    proposed.JiangSetting=original.JiangSetting;proposed.FreeTimeTodey=original.FreeTimeTodey;
                    proposed.StartCountDwon=original.StartCountDwon;proposed.CountDownEnd=original.CountDownEnd;
                    proposed.TimerStamp=original.TimerStamp;proposed.MyNewStart=original.MyNewStart;
                    proposed.RemoveJiangMod=original.RemoveJiangMod;
                    if(action!=2) {proposed.RemoveJiangMod=DateTime.Now;proposed.OnJiangHu=true;}
                    ushort nextStudy=study;
                    if(action==1) {
                        proposed.RoundBuyPoints=(ushort)Math.Min(49,proposed.RoundBuyPoints+1);
                        proposed.FreeCourse+=10000;proposed.Talent=(byte)Math.Min(5,proposed.Talent+1);nextStudy=(ushort)(study+20);
                    } else if(action==0) {
                        proposed.Level=p.Entity.Level;proposed.Talent--;proposed.FreeCourse-=10000;
                        nextStudy=(ushort)(study-Config.JiangHuFreeTrainingStudyPoints);
                    }
                    string after;
                    long? savedCp,savedRvp;PurseBalance.CopySaved(p,out savedCp,out savedRvp);
                    using(var connection=DataHolder.MySqlConnection){connection.Open();using(var transaction=connection.BeginTransaction()) {
                        Q.Execute(connection,transaction,"SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'");
                        using(var command=Q.Command(connection,transaction,"SELECT conquerpoints,studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                        using(var reader=command.ExecuteReader()) {
                            if(!reader.Read() || (reader.IsDBNull(0)?(long?)null:Convert.ToInt64(reader[0]))!=savedCp
                                || reader.IsDBNull(1) || Convert.ToUInt16(reader[1])!=study)throw new InvalidOperationException("Jiang purse changed.");
                        }
                        using(var command=Q.Command(connection,transaction,"SELECT data FROM jianghu WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                        using(var reader=command.ExecuteReader()) {
                            if(!reader.Read() || (reader.IsDBNull(0)?null:reader.GetString(0))!=saved)throw new InvalidOperationException("Jiang saved data changed.");
                        }
                        Guid? pill=null;
                        if(action!=2)using(var command=Q.Command(connection,transaction,
                            "SELECT id FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL FOR UPDATE","uid",(long)uid)) {
                            object value=command.ExecuteScalar();if(value!=null && value!=DBNull.Value)pill=(Guid)value;
                        }
                        if(action==2)proposed.PrepareRestoration(previous);
                        else proposed.PrepareTrainingRoll(star,stage,pill.HasValue);
                        after=proposed.ToString();
                        if(pill.HasValue && Q.Execute(connection,transaction,
                            "UPDATE revora_jiang_pill_uses SET consumed_at=@now,quality=@quality WHERE id=@id AND uid=@uid AND consumed_at IS NULL",
                            "now",DateTime.UtcNow,"quality",(int)proposed.Stagers[stage-1].Stars[star-1].Level,"id",pill.Value,"uid",(long)uid)!=1)
                            throw new InvalidOperationException("Jiang pill pending state changed.");
                        if(Q.Execute(connection,transaction,"UPDATE entities SET conquerpoints=@cp,studypoints=@study WHERE uid=@uid",
                            "cp",(long)(cp-cost),"study",(int)nextStudy,"uid",(long)uid)!=1
                            || Q.Execute(connection,transaction,"UPDATE jianghu SET data=@data WHERE uid=@uid","data",after,"uid",(long)uid)!=1)
                            throw new InvalidOperationException("Jiang training was not saved.");
                        var beforeCommit=BeforeCommit;if(beforeCommit!=null)beforeCommit(p);
                        if(!Ready(p) || !ReferenceEquals(p.Entity.MyJiang,original) || original.SaveBlocked || original.SavedData!=saved
                            || (action==2 && !ReferenceEquals(original.MyOldStar,previous))
                            || original.ToString()!=current || p.Entity.ConquerPoints!=cp || p.Entity.SubClasses.StudyPoints!=study)
                            throw new InvalidOperationException("Jiang session changed.");
                        transaction.Commit();
                    }}
                    var beforeInstall=BeforeInstall;if(beforeInstall!=null)beforeInstall(p);
                    p.Entity.ApplyCommittedConquerPoints(cp-cost);p.Entity.SubClasses.StudyPoints=nextStudy;
                    proposed.SavedData=after;proposed.HasSavedData=true;
                    Game.JiangHu.JiangHuClients[uid]=proposed;p.Entity.MyJiang=proposed;
                    p.LoadItemStats();Game.JiangHu.JiangHuRanking.UpdateRank(proposed);
                    // A full new star list before 2702 overwrites the client's previous
                    // skill before it can compare the roll. Training uses the delta;
                    // restoration can refresh the full accepted state.
                    if(action==2)proposed.SendStatus(p,p);
                    proposed.SendStatusMode(p);
                    if(proposed.Stage!=original.Stage)proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.OpenStage,proposed.Stage.ToString());
                    proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.UpdateTime,proposed.FreeCourse.ToString(),proposed.Time.ToString());
                    proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.UpdateStar,stage.ToString(),star.ToString());
                    proposed.SendInfo(p,Revora.Network.GamePackets.JiangHu.UpdateTalent,uid.ToString(),proposed.Talent.ToString());
                    p.Send(new JiangHuUpdate{Atribute=proposed.Stagers[stage-1].Stars[star-1].UID,
                        FreeCourse=proposed.FreeCourse,Stage=stage,Star=star,FreeTimeTodeyUsed=(byte)proposed.FreeTimeTodeyUsed,
                        RoundBuyPoints=proposed.RoundBuyPoints}.ToArray());
                    if(action==1)p.Send(new SubClassShowFull(0){ID=8,Study=nextStudy,StudyReceive=20}.ToArray());
                    Q.Refresh(p);
                }
            }} catch(Exception ex) {
                if(original!=null)original.SaveBlocked=true;
                if(p.Entity.MyJiang!=null)p.Entity.MyJiang.SaveBlocked=true;
                Q.Fail(p,action==2?"jiang-restore":"jiang-training",ex);
            } finally {SessionInstall.Exit(uid);}
        }
    }
}
