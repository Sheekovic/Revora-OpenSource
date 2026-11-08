using System;
using System.Collections.Generic;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network;
using Revora.Network.GamePackets;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

internal static partial class PotencyHuntTests
{
    static readonly Type AbyssJournal = typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialJournal");
    static List<MsgTaskStatus.QuestData> AbyssList(GameState p) {
        var list=new List<MsgTaskStatus.QuestData>(); QuestListSystem.Fill(p,list); return list;
    }
    static byte[] AbyssQuit(uint mission,int body=20) {
        var packet=new byte[body+8]; Writer.WriteUInt16((ushort)body,0,packet); Writer.WriteUInt16(2032,2,packet);
        Writer.WriteUInt32(QuestInfo.ActionBase+mission,8,packet); packet[15]=102; Writer.WriteUInt64(PacketHandler.ClientSeal,body,packet); return packet;
    }
    static void VerifyAbyssJournal(string runtime,Map town) {
        VerifyAbyssStore(runtime,town); Call(AbyssJournal,null,"Initialize");
        var map=Kernel.Maps[1700]; var p=SecondPlayer(town); p.Quests=new Quests(p); p.Quests.Load();
        for(uint mission=2414;mission<=2419;mission++) { p.Quests.Accept((QuestID)mission); p.Quests.IncreaseQuestKills((QuestID)mission,mission-2400); }
        p.Quests.Save(); var legacy=p.Quests.Items.Where(i=>(uint)i.QItem.UID>=2414 && (uint)i.QItem.UID<=2419).ToDictionary(i=>(uint)i.QItem.UID,i=>i.Kills);
        AbyssWrite(p,"Enroll"); Call(AbyssJournal,null,"Refresh",p,true);
        var list=AbyssList(p).Where(i=>(uint)i.UID>=2414 && (uint)i.UID<=2419).ToArray();
        Check(list.Length==6 && list.Select(i=>i.UID).Distinct().Count()==6 && list[0].Status==Status.Accepted
            && list.Skip(1).All(i=>i.Status==Status.Available),"selected Abyss journal projects six unique saved statuses over conflicting Cup entries");
        for(uint mission=2414;mission<=2419;mission++) {
            var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin}; begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,Status.Finished,0));
            MsgTaskStatus.Process(begin.ToArray(),p); PacketHandler.HandlePacket(AbyssQuit(mission),p);
            Check(p.Quests.GetQuest((QuestID)mission).Kills==legacy[mission] && p.Quests.CheckQuest((QuestID)mission)==Status.Accepted
                && AbyssVersion(p)==0,"client Begin and quit cannot author Abyss state or overwrite Cup data: "+mission);
        }
        foreach(int body in new[]{19,121}) PacketHandler.HandlePacket(AbyssQuit(2414,body),p);
        Check(!Fenced(p) && AbyssVersion(p)==0 && p.Quests.GetQuest((QuestID)2414).Kills==14,"bounded malformed journal quits leave saved and legacy state untouched");
        AbyssWrite(p,"SelectCup"); Call(AbyssJournal,null,"Refresh",p,true);
        list=AbyssList(p).Where(i=>(uint)i.UID>=2414 && (uint)i.UID<=2419).ToArray();
        Check(list.Length==6 && list.All(i=>i.Status==Status.Accepted) && !(bool)Call(AbyssJournal,null,"Protect",p,2414u),"selecting Cup restores its original accepted journal entries");
        AbyssWrite(p,"Enroll"); Call(AbyssJournal,null,"Refresh",p,true);
        Pack(p,722731,3); AbyssAt(p,map,3601); var vigor=AbyssVigor(p); AbyssWrite(p,"TurnIn",AbyssVersion(p),3601u,vigor); AbyssApply(p,vigor,null);
        Call(AbyssJournal,null,"Refresh",p,true); list=AbyssList(p).ToArray();
        Check(list.Single(i=>(uint)i.UID==2414).Status==Status.Finished && list.Single(i=>(uint)i.UID==2415).Status==Status.Accepted
            && list.Single(i=>(uint)i.UID==2416).Status==Status.Available,"Arthur's saved offering opens Bryan's introduction before grievance");
        AbyssAt(p,map,3602); AbyssWrite(p,"Introduce",AbyssVersion(p)); Call(AbyssJournal,null,"Refresh",p,true); list=AbyssList(p).ToArray();
        Check(list.Single(i=>(uint)i.UID==2415).Status==Status.Finished && list.Single(i=>(uint)i.UID==2416).Status==Status.Accepted,"Bryan's saved acknowledgement starts the displayed grievance trial");
        AbyssWrite(p,"WildDeath",Guid.NewGuid(),3603u,99); Call(AbyssJournal,null,"Refresh",p,false);
        var details=(MsgTaskDetailInfo[])Call(AbyssJournal,null,"Details",AbyssRead(p));
        Check(details.Single(d=>(uint)d.UID==2416)[1]==500 && System.BitConverter.ToUInt32(details[0].ToArray(),12)==500,
            "native grievance objective receives the actual saved meters at the existing packet offset");
        p.Quests.Save(); var loaded=ReloadBall(p); SkillTable.LoadProficiencies(loaded); Call(AbyssJournal,null,"Refresh",loaded,true);
        Check(AbyssList(loaded).Single(i=>(uint)i.UID==2416).Status==Status.Accepted
            && legacy.All(pair=>loaded.Quests.GetQuest((QuestID)pair.Key).Kills==pair.Value),"character reload restores the overlay while every serialized Cup objective remains intact");
        Check(!(bool)Call(AbyssJournal,null,"Selected",p) && (bool)Call(AbyssJournal,null,"Selected",loaded),"superseded connection cannot reuse a journal cache owned by the replacement session");
        VerifyAbyssJournalStages(loaded);
    }
    static void VerifyAbyssJournalStages(GameState p) {
        var run=AbyssRead(p); Type type=run.GetType();
        foreach(int count in new[]{0,1,7,8,9,15,16,63,64,71,72}) {
            type.GetField("Progress",Instance).SetValue(run,AbyssProgress(3,40000,count)); type.GetField("Introduced",Instance).SetValue(run,true);
            var packets=(MsgTaskDetailInfo[])Call(AbyssJournal,null,"Details",run); var detail=packets.Single(x=>(uint)x.UID==2418);
            Check(detail[1]==count/8 && detail[2]==(count==0?0:(count-1)%8+1)
                && System.BitConverter.ToUInt16(detail.ToArray(),2)==1135,"binding curse shows complete rounds and current ordered progress: "+count);
        }
        foreach(int stage in new[]{1,2,3,4,5}) foreach(bool introduced in new[]{false,true}) {
            type.GetField("Progress",Instance).SetValue(run,AbyssProgress(stage,stage>=3?40000:0,stage>=4?72:0,stage==5?3:0));
            type.GetField("Introduced",Instance).SetValue(run,introduced);
            var statuses=Enumerable.Range(2414,6).Select(id=>(Status)Call(AbyssJournal,null,"StatusOf",run,(uint)id)).ToArray();
            int active=stage==1?0:stage==2?(introduced?2:1):stage==3?(introduced?4:3):stage==4?5:-1;
            Check(statuses.Count(s=>s==Status.Accepted)==(stage==5?0:1)
                && (active<0?statuses.All(s=>s==Status.Finished):statuses[active]==Status.Accepted),"each saved stage has exactly its one actionable native mission: "+stage+"/"+introduced);
        }
        type.GetField("Selected",Instance).SetValue(run,false);
        Check(((MsgTaskDetailInfo[])Call(AbyssJournal,null,"Details",run)).Length==0,"deselected Abyss route emits no objective packets over the Cup journal");
    }
}
