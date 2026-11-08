using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static bool freshChoiceWireFixture;
    const uint ChoiceWireUid=4294919101;
    static string ChoiceWireSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('purse',(SELECT json_build_object('cp',conquerpoints,'bound',boundcps,'silver',money) FROM entities WHERE uid=@uid),'items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid=@uid),'choices',(SELECT json_agg(r ORDER BY pack_uid) FROM revora_p7_choice_uses r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));}
    static byte[] ChoiceWireFrame(byte option) {var f=new byte[28];Writer.WriteUInt16(20,0,f);Writer.WriteUInt16(2032,2,f);Writer.WriteUInt32(3006233,8,f);f[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,f);return f;}
    static void ChoiceWireSend(GameState p,byte[] f) {System.Threading.Thread.Sleep(550);PacketHandler.HandlePacket(f,p);}
    static void ChoiceWireOpen(GameState p,uint uid) {var f=new ItemUsage(true){UID=uid,Action=ItemUsage.Mode.EquipItem}.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,f.Length-8,f);ChoiceWireSend(p,f);}
    static void ChoiceWireProcess(string phase) {
        bool first=phase.EndsWith("menu");freshChoiceWireFixture=true;nextUid=ChoiceWireUid;bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)ChoiceWireUid)==1;var p=Player(Kernel.Maps[1002],!first || exists);p.Account.Username=p.Entity.Name;
        GameState prior;Require(Kernel.GamePool.TryRemove(p.Entity.UID,out prior) && ReferenceEquals(prior,p));Require((bool)Call(Feature("SessionInstall"),"TryClaim",p.Entity.UID,p));p.Filtering=true;
        if(first) {
            Require((bool)Sql("SELECT to_regclass('revora_choice_wire_probe') IS NULL"));
            // Separate supplied-item regression character; never fresh-career earning credit.
            var item=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3006233);if(item==null){item=(ConquerItem)Call(Feature("QuestInventory"),"New",3006233u,true);Require(p.Inventory.Add(item,Enums.ItemUse.CreateAndAdd));}
            var choices=ConquerItemInformation.BaseInformations.Where(i=>i.Value.PurificationLevel==7).Select(i=>i.Key).OrderBy(i=>i).ToArray();Require(choices.Length>7);
            string before=ChoiceWireSnapshot(p);ChoiceWireOpen(p,item.UID);Require(p.ActiveNpc==3006233);
            var extra=ChoiceWireFrame(1).Concat(new byte[]{0}).ToArray();ChoiceWireSend(p,extra);Require(before==ChoiceWireSnapshot(p));
            var overrun=ChoiceWireFrame(1);overrun[17]=3;ChoiceWireSend(p,overrun);Require(before==ChoiceWireSnapshot(p));
            ChoiceWireSend(p,ChoiceWireFrame(249));ChoiceWireSend(p,ChoiceWireFrame(255));ChoiceWireSend(p,ChoiceWireFrame(1));Require(before==ChoiceWireSnapshot(p));
            ChoiceWireOpen(p,item.UID);ChoiceWireSend(p,ChoiceWireFrame(251));Require(before==ChoiceWireSnapshot(p));
            Sql("CREATE TABLE revora_choice_wire_probe(pack bigint,result_type integer,stage text,snapshot text);INSERT INTO revora_choice_wire_probe VALUES(@pack,@type,'offered',@snapshot)","pack",(long)item.UID,"type",(int)choices[7],"snapshot",before);
        } else {
            Require(ChoiceWireSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_choice_wire_probe")));uint pack=Convert.ToUInt32(Sql("SELECT pack FROM revora_choice_wire_probe")),type=Convert.ToUInt32(Sql("SELECT result_type FROM revora_choice_wire_probe"));
            if(phase.EndsWith("choose")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_choice_wire_probe"))=="offered");
                string before=ChoiceWireSnapshot(p);p.ActiveNpc=3006233;ChoiceWireSend(p,ChoiceWireFrame(2));Require(before==ChoiceWireSnapshot(p));
                ChoiceWireOpen(p,pack);
                ChoiceWireSend(p,ChoiceWireFrame(1).Concat(new byte[]{0}).ToArray());var malformed=ChoiceWireFrame(1);malformed[17]=3;ChoiceWireSend(p,malformed);
                ChoiceWireSend(p,ChoiceWireFrame(249));ChoiceWireSend(p,ChoiceWireFrame(251));ChoiceWireSend(p,ChoiceWireFrame(250));ChoiceWireSend(p,ChoiceWireFrame(255));ChoiceWireSend(p,ChoiceWireFrame(1));Require(before==ChoiceWireSnapshot(p));
                ChoiceWireOpen(p,pack);ChoiceWireSend(p,ChoiceWireFrame(251));ChoiceWireSend(p,ChoiceWireFrame(2));
                Require(!p.Inventory.ContainsUID(pack) && p.Inventory.Objects.Single().ID==type && p.Inventory.Objects.Single().Bound && !PirateFenced(p));
                Sql("UPDATE revora_choice_wire_probe SET stage='chosen',snapshot=@snapshot","snapshot",ChoiceWireSnapshot(p));
            }
            Require(Convert.ToString(Sql("SELECT stage FROM revora_choice_wire_probe"))=="chosen" && !p.Inventory.ContainsUID(pack));
            string once=ChoiceWireSnapshot(p);p.ActiveNpc=3006233;ChoiceWireSend(p,ChoiceWireFrame(2));ChoiceWireOpen(p,pack);Require(once==ChoiceWireSnapshot(p) && !PirateFenced(p));
            Require((long)Sql("SELECT count(*) FROM revora_p7_choice_uses u JOIN items i ON i.uid=u.result_uid AND i.entityid=u.uid WHERE u.uid=@uid AND u.pack_uid=@pack AND u.result_type=@type AND i.bound=1","uid",(long)p.Entity.UID,"pack",(long)pack,"type",(int)type)==1);
        }
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Action==2 && (bool)Call(Feature("SessionInstall"),"IsCurrent",p.Entity.UID,p));
        Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID);Server.CommandsAI("@close");throw new InvalidOperationException("Choice wire did not close.");
    }
}
