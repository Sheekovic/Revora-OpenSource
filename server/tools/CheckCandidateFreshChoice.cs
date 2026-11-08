using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshChoiceSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'choices',(SELECT json_agg(r ORDER BY pack_uid) FROM revora_p7_choice_uses r WHERE uid=@uid))::text","career",FreshAdvancedSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshChoiceOpen(GameState p,uint item) {
        var frame=new ItemUsage(true){UID=item,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);ItemUsage.Process(frame,p);
    }
    static void FreshChoiceSelect(GameState p,byte option) {Npcs.GetDialog(new NpcRequest(true){NpcID=3006233,OptionID=option},p);}
    static void FreshChoiceProcess(string phase) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(phase.EndsWith("menu")) {
            Require(FreshAdvancedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_advanced_probe")) && (bool)Sql("SELECT to_regclass('revora_fresh_choice_probe') IS NULL"));
            var pack=p.Inventory.Objects.Single(i=>i.ID==3006233);Require(pack.Bound);var choices=ConquerItemInformation.BaseInformations.Where(i=>i.Value.PurificationLevel==7).Select(i=>i.Key).OrderBy(i=>i).ToArray();Require(choices.Length>7);
            string once=FreshChoiceSnapshot(p);FreshChoiceOpen(p,pack.UID);Require(p.ActiveNpc==3006233);FreshChoiceSelect(p,249);Require(once==FreshChoiceSnapshot(p));
            FreshChoiceSelect(p,251);FreshChoiceSelect(p,250);FreshChoiceSelect(p,255);FreshChoiceSelect(p,1);Require(once==FreshChoiceSnapshot(p));
            FreshChoiceOpen(p,pack.UID);FreshChoiceSelect(p,251);Require(once==FreshChoiceSnapshot(p));
            Sql("CREATE TABLE revora_fresh_choice_probe(uid bigint PRIMARY KEY,pack bigint,result_type integer,stage text,snapshot text);INSERT INTO revora_fresh_choice_probe VALUES(@uid,@pack,@type,'offered',@snapshot)","uid",(long)uid,"pack",(long)pack.UID,"type",(int)choices[7],"snapshot",once);
        } else {
            Require(FreshChoiceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_choice_probe")));
            uint pack=Convert.ToUInt32(Sql("SELECT pack FROM revora_fresh_choice_probe")),type=Convert.ToUInt32(Sql("SELECT result_type FROM revora_fresh_choice_probe"));
            if(phase.EndsWith("choose")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_choice_probe"))=="offered" && p.Inventory.ContainsUID(pack));
                string before=FreshChoiceSnapshot(p);FreshChoiceSelect(p,2);Require(before==FreshChoiceSnapshot(p)); // Offers do not survive disconnect/restart.
                int count=p.Inventory.Count;uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
                FreshChoiceOpen(p,pack);Require(p.ActiveNpc==3006233);FreshChoiceSelect(p,251);FreshChoiceSelect(p,2);
                Require(!p.Inventory.ContainsUID(pack) && p.Inventory.Count==count && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver && !PirateFenced(p));
                uint result=Convert.ToUInt32(Sql("SELECT result_uid FROM revora_p7_choice_uses WHERE uid=@uid AND pack_uid=@pack AND result_type=@type","uid",(long)uid,"pack",(long)pack,"type",(int)type));
                var reward=p.Inventory.Objects.Single(i=>i.UID==result);Require(reward.ID==type && reward.Bound && ConquerItemInformation.BaseInformations[type].PurificationLevel==7);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_choice_probe SET stage='chosen',snapshot=@snapshot","snapshot",FreshChoiceSnapshot(p));
            }
            Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_choice_probe"))=="chosen" && !p.Inventory.ContainsUID(pack));
            string once=FreshChoiceSnapshot(p);FreshChoiceSelect(p,2);FreshChoiceOpen(p,pack);Require(!PirateFenced(p) && once==FreshChoiceSnapshot(p));
            Require((long)Sql("SELECT count(*) FROM revora_p7_choice_uses u JOIN items i ON i.uid=u.result_uid AND i.entityid=u.uid AND i.id=u.result_type WHERE u.uid=@uid AND u.pack_uid=@pack AND u.result_type=@type AND i.bound=1","uid",(long)uid,"pack",(long)pack,"type",(int)type)==1);
            Console.WriteLine("[fresh-choice] consumed-pack="+pack+" bound-soul="+type);
        }
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh choice did not close.");
    }
}
