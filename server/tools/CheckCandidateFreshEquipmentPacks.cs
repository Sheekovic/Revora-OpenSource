using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshEquipmentPacksSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'uses',(SELECT json_agg(r ORDER BY item_type,id) FROM revora_fixed_output_pack_uses r WHERE uid=@uid))::text","prior",FreshTempestSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshEquipmentPacksProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && p.Entity.Class==25);
        uint[] expected={120046,131027,151077,510097,111057,120158,150178,510108,160118,111068,131078,111089,120189,160199,150219,131109};
        Func<ConquerItem[]> owned=()=>p.Inventory.Objects.Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).ToArray();
        if((bool)Sql("SELECT to_regclass('revora_fresh_equipment_packs_probe') IS NULL")){
            Require(phase.EndsWith("open") && FreshTempestSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tempest_probe")) && (long)Sql("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid=@uid AND item_type BETWEEN 728236 AND 728251","uid",(long)p.Entity.UID)==0);
            var packs=owned().Where(i=>i.ID>=728236 && i.ID<=728251).OrderBy(i=>i.ID).ToArray();Require(packs.Length==16 && packs.Select(i=>i.ID).Distinct().Count()==16 && packs.All(i=>i.Bound && i.StackSize==1));
            Sql("CREATE TABLE revora_fresh_equipment_packs_probe(stage text,snapshot text,inputs bigint[]);INSERT INTO revora_fresh_equipment_packs_probe VALUES('new',@snapshot,@inputs)","snapshot",FreshEquipmentPacksSnapshot(p),"inputs",packs.Select(i=>(long)i.UID).ToArray());
        }
        Require(FreshEquipmentPacksSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_equipment_packs_probe")));var inputs=(long[])Sql("SELECT inputs FROM revora_fresh_equipment_packs_probe");
        if(phase.EndsWith("open")){
            Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_equipment_packs_probe"))=="new");var prior=owned().Where(i=>!inputs.Contains((long)i.UID)).Select(i=>i.UID).ToArray();
            for(int index=0;index<16;index++){
                uint type=728236u+(uint)index;var pack=owned().Single(i=>i.ID==type);Require(pack.UID==inputs[index]);
                if(!p.Inventory.ContainsUID(pack.UID)){
                    var bank=p.Warehouses.Single(w=>w.Value.ContainsUID(pack.UID));uint banker=(uint)bank.Key;Require(banker==8 || banker==10012);FreshClassicTalk(p,banker==8?(ushort)1002:(ushort)1011,banker,0);
                    var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(banker,4,frame);frame[12]=2;frame[13]=10;Revora.Network.Writer.WriteUInt32(pack.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(p.Inventory.ContainsUID(pack.UID) && !bank.Value.ContainsUID(pack.UID));
                }
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(pack.UID) && !PirateFenced(p));
                var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(added.Length==(index==15?2:1) && added.All(i=>i.Bound && i.Plus==0 && i.Bless==0 && i.StackSize==1) && added.Count(i=>i.ID==expected[index])==1 && (index!=15 || added.Count(i=>i.ID==723911)==1));
                var ids=(long[])Sql("SELECT rewards FROM revora_fixed_output_pack_uses WHERE uid=@uid AND item_uid=@item AND item_type=@type AND bound","uid",(long)p.Entity.UID,"item",(long)pack.UID,"type",(int)type);Require(ids.OrderBy(i=>i).SequenceEqual(added.Select(i=>(long)i.UID).OrderBy(i=>i)));
                string after=FreshEquipmentPacksSnapshot(p);RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(after==FreshEquipmentPacksSnapshot(p));
            }
            Require(prior.All(uid=>owned().Any(i=>i.UID==uid)) && EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_equipment_packs_probe SET stage='opened',snapshot=@snapshot","snapshot",FreshEquipmentPacksSnapshot(p));
        }else Require(phase.EndsWith("reload") && Convert.ToString(Sql("SELECT stage FROM revora_fresh_equipment_packs_probe"))=="opened");
        Require(!owned().Any(i=>inputs.Contains((long)i.UID)) && (long)Sql("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid=@uid AND item_uid=ANY(@inputs)","uid",(long)p.Entity.UID,"inputs",inputs)==16);
        for(int index=0;index<16;index++){
            var ids=(long[])Sql("SELECT rewards FROM revora_fixed_output_pack_uses WHERE uid=@uid AND item_uid=@item","uid",(long)p.Entity.UID,"item",inputs[index]);var rewards=owned().Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==(index==15?2:1) && rewards.All(i=>i.Bound && i.Plus==0 && i.Bless==0) && rewards.Count(i=>i.ID==expected[index])==1 && (index!=15 || rewards.Count(i=>i.ID==723911)==1));
            RetainedUpgradeUsage(p,checked((uint)inputs[index]),0,ItemUsage.Mode.EquipItem);
        }
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931 AND source='equipment-quests-revora-v1'","uid",(long)p.Entity.UID)==1 && FreshEquipmentPacksSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_equipment_packs_probe")) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" packs=16 outputs=17 bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh equipment packs did not close.");
    }
}
