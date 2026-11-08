using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Network;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void VerifyQuestPackets(Map town) {
        bool pages=true;
        for(int count=0;count<=80;count++) {
            var packet=new MsgTaskStatus(true,count){Action=MsgTaskStatus.QuestAction.List};
            for(int i=0;i<count;i++)packet.Apend(MsgTaskStatus.QuestData.Create((QuestID)(90000+i),MsgTaskStatus.QuestData.QuestStatus.Accepted,(uint)i));
            var bytes=packet.ToArray();pages&=bytes.Length==16+12*count && System.BitConverter.ToUInt16(bytes,0)==bytes.Length-8;
            pages&=bytes.Skip(bytes.Length-8).All(b=>b==0);
            for(int i=0;i<count;i++)pages&=(uint)packet[i].UID==90000+i && packet[i].Time==i;
        }
        Check(pages,"zero through eighty quest records round-trip without overwriting the transport suffix");
        bool negative=false,oversize=false,append=false,index=false;
        try{new MsgTaskStatus(true,-1);}catch(ArgumentOutOfRangeException){negative=true;}
        try{new MsgTaskStatus(true,81);}catch(ArgumentOutOfRangeException){oversize=true;}
        var full=new MsgTaskStatus(true,1);full.Apend(MsgTaskStatus.QuestData.Create((QuestID)90001,MsgTaskStatus.QuestData.QuestStatus.Available,0));
        try{full.Apend(default(MsgTaskStatus.QuestData));}catch(InvalidOperationException){append=true;}
        try{full[1]=default(MsgTaskStatus.QuestData);}catch(ArgumentOutOfRangeException){index=true;}
        Check(negative && oversize && append && index,"quest allocation and writes reject invalid counts or record positions");
        var p=ProgressPlayer(town,"");p.Quests=new Quests(p);QuestListSystem.EnsureCatalog();
        for(int length=0;length<28;length++) {
            var bad=new byte[length];if(length>=8){Writer.WriteUInt16((ushort)length,0,bad);Writer.WriteUInt16(1134,2,bad);Writer.WriteUInt16(1,4,bad);Writer.WriteUInt16(1,6,bad);}
            MsgTaskStatus.Process(bad,p);
        }
        Check(!p.Quests.Items.Any(),"truncated and unregistered Begin packets are ignored without indexing absent records");
        var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};
        begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)90001,MsgTaskStatus.QuestData.QuestStatus.Finished,0));
        var wrongType=(byte[])begin.ToArray().Clone();Writer.WriteUInt16(1111,2,wrongType);MsgTaskStatus.Process(wrongType,p);
        var badLength=(byte[])begin.ToArray().Clone();Writer.WriteUInt16(25,0,badLength);MsgTaskStatus.Process(badLength,p);
        var many=(byte[])begin.ToArray().Clone();Writer.WriteUInt16(80,6,many);MsgTaskStatus.Process(many,p);
        MsgTaskStatus.Process(new byte[977],p);MsgTaskStatus.Process(null,p);
        Check(!p.Quests.Items.Any(),"wrong packet type, invalid framing, excessive counts and oversized requests are rejected");
        p.Fake=true;MsgTaskStatus.Process(begin.ToArray(),p);p.Fake=false;
        Check(!p.Quests.Items.Any(),"fake player cannot import quest UI state");
        MsgTaskStatus.Process(begin.ToArray(),p);MsgTaskStatus.Process(begin.ToArray(),p);
        Check(p.Quests.Items.Count()==1 && p.Quests.CheckQuest((QuestID)90001)==MsgTaskStatus.QuestData.QuestStatus.Accepted,"known Begin creates one accepted UI entry and ignores client-authored completion status");
        var plain=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};plain.Apend(MsgTaskStatus.QuestData.Create((QuestID)90002,MsgTaskStatus.QuestData.QuestStatus.Accepted,0));
        MsgTaskStatus.Process(plain.ToArray().Take(20).ToArray(),p);
        Check(p.Quests.HasQuest((QuestID)90002),"already-framed request without transport suffix accepts its complete record");
        for(int i=0;i<90;i++)p.Quests.Accept((QuestID)(80000+i),0);
        p.Quests.SendFullGUI();Check(p.Quests.Items.Count()==92,"full quest GUI safely pages more than eighty server-owned entries");
        Query("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        var soul=Pack(p,720842,1);int actors=town.Entities.Count;uint cp=p.Entity.ConquerPoints;
        ItemHandler.UseItem(soul,p);
        Check(p.ActiveNpc!=720842 && p.Inventory.Objects.Any(i=>i.UID==soul.UID) && !p.Entity.SwordSoul,
            "dormant Sword Soul remains owned without issuing the unsafe legacy confirmation");
        for(int attempt=0;attempt<3;attempt++) {
            p.ActiveNpc=720842;SoulWire(p,1,720842);
        }
        Check(!Fenced(p) && p.ActiveNpc==0 && town.Entities.Count==actors && !p.Entity.SwordSoul
            && p.Entity.ConquerPoints==cp && p.Inventory.Objects.Any(i=>i.UID==soul.UID)
            && Revora.Database.ConquerItemTable.LoadItem(soul.UID).ID==720842,
            "stale Sword Soul confirmations cannot spawn a public boss, consume the item or spend the daily attempt");
        foreach(bool bound in new[]{false,true}) {
            var banshee=Pack(p,3004259,2);banshee.Bound=bound;Revora.Database.ConquerItemTable.UpdateBound(banshee,p);
            int count=p.Inventory.Count;uint[] before=p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).ToArray();
            ItemHandler.UseItem(banshee,p);ItemHandler.UseItem(banshee,p);
            Check(!Fenced(p) && p.ActiveNpc==0 && p.Inventory.Count==count && banshee.StackSize==2
                && p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(before),
                "Banshee Spirit cannot fall through into Promotion Pack consumption: bound="+bound);
            for(int attempt=0;attempt<3;attempt++){p.ActiveNpc=3004259;SoulWire(p,1,3004259);}
            var saved=Revora.Database.ConquerItemTable.LoadItem(banshee.UID);
            Check(!Fenced(p) && p.ActiveNpc==0 && town.Entities.Count==actors && !p.Entity.BansheeSpirit
                && saved.ID==3004259 && saved.StackSize==2 && saved.Bound==bound && p.Entity.ConquerPoints==cp
                && p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(before),
                "stale Banshee confirmations preserve the saved item, binding, allowance and world: bound="+bound);
        }


    }
}
