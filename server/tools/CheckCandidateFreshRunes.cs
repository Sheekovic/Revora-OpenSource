using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshRuneSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'runes',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,position,runesexp,bound FROM items WHERE entityid=@uid AND id BETWEEN 4010000 AND 4039999) r))::text","prior",FreshSubclassSnapshot(p,players),"uid",(long)p.Entity.UID));}
    static void FreshRuneJourney(bool reload){
        var p=FreshLoadSaved(true,true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Call(typeof(Revora.Database.JiangHu),"LoadClient",p);Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)) && p.Entity.Class==25);
        if((bool)Sql("SELECT to_regclass('revora_fresh_rune_probe') IS NULL")){
            Require(!reload && FreshSubclassSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_subclass_probe WHERE stage=1")));
            Sql("CREATE TABLE revora_fresh_rune_probe(snapshot text,items bigint[] NOT NULL);INSERT INTO revora_fresh_rune_probe VALUES(@snapshot,ARRAY[]::bigint[])","snapshot",FreshRuneSnapshot(p,players));
        }
        Require(FreshRuneSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rune_probe")));
        if(!reload){
            byte[] choices={181,191,200,201,202};uint[] types={4010101,4020201,4030101,4030201,4030301};uint[] prices={1000,750,500,500,500};
            var uids=((long[])Sql("SELECT items FROM revora_fresh_rune_probe")).ToList();
            for(int ix=uids.Count;ix<5;ix++){
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint rvp=p.Entity.RevoraPoint;FreshClassicTalk(p,1002,900000014,choices[ix]);var item=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(item.ID==types[ix] && p.Entity.RevoraPoint==rvp-prices[ix]);
                CareerRuneMove(p,item,61);Require(item.Position==211 && p.RunesItem.ContainsKey(item.UID));CareerRuneMove(p,item,57,(uint)(101+ix));Require(item.Position==101+ix);
                uids.Add(item.UID);SkillTable.SaveSpells(p);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_rune_probe SET items=@items,snapshot=@snapshot","items",uids.ToArray(),"snapshot",FreshRuneSnapshot(p,players));
            }
            uint stoneType=EShopFile.Shops[2888].Items.Where(id=>id>=730001 && id<=730008 && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0).OrderByDescending(id=>id).First();
            var clock=Feature("ExemptionTokenShop").GetField("Clock",Hidden);var savedClock=clock.GetValue(null);var latest=Sql("SELECT extract(epoch FROM max(purchased_at)) FROM revora_exemption_purchases WHERE uid=@uid AND item_type=@type","uid",(long)p.Entity.UID,"type",(int)stoneType);DateTime buyTime=latest==DBNull.Value?DateTime.UtcNow:new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Convert.ToDouble(latest));if(buyTime<DateTime.UtcNow)buyTime=DateTime.UtcNow;
            var until=DateTime.UtcNow.AddSeconds(180);int used=0;
            try{clock.SetValue(null,(Func<DateTime>)(()=>buyTime));
            foreach(long uid in uids){var rune=p.RunesItem[(uint)uid];uint terminal=rune.ID/10000==401?1u:rune.ID/10000==402?27u:9u;uint cp=p.Entity.ConquerPoints;
                while(rune.ID%100<terminal && DateTime.UtcNow<until && used<500){
                    buyTime=buyTime.AddSeconds(2);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,stoneType);var stone=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(stone.ID==stoneType && stone.Plus==stoneType-730000);
                    var packet=new byte[103];Revora.Network.Writer.WriteUInt16(95,0,packet);Revora.Network.Writer.WriteUInt16(1009,2,packet);Revora.Network.Writer.WriteUInt32(rune.UID,8,packet);Revora.Network.Writer.WriteUInt16(63,20,packet);Revora.Network.Writer.WriteUInt32(1,26,packet);Revora.Network.Writer.WriteUInt32(stone.UID,91,packet);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,95,packet);
                    uint type=rune.ID,xp=rune.RunesEXP;RunesSystem.HandlePacket1009(p,packet);Require(p.Action==2 && !p.Inventory.ContainsUID(stone.UID) && (rune.ID>type || rune.RunesEXP>xp));used++;
                    string once=FreshRuneSnapshot(p,players);RunesSystem.HandlePacket1009(p,packet);Require(once==FreshRuneSnapshot(p,players));
                    SkillTable.SaveSpells(p);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_rune_probe SET snapshot=@snapshot","snapshot",FreshRuneSnapshot(p,players));
                }
                Console.WriteLine("[career-fresh-rune] id="+rune.ID+" xp="+rune.RunesEXP+" stone="+stoneType+" cp="+(cp-p.Entity.ConquerPoints));
            }
            }finally{clock.SetValue(null,savedClock);}
        }
        var owned=(long[])Sql("SELECT items FROM revora_fresh_rune_probe");Require(owned.Length==5 && owned.All(uid=>p.RunesItem.ContainsKey((uint)uid)));var equipped=owned.Select(uid=>p.RunesItem[(uint)uid]).ToArray();Require(equipped.Select(i=>i.Position).OrderBy(i=>i).SequenceEqual(new byte[]{101,102,103,104,105}));
        foreach(int id in new[]{11310,11311})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='rune-loadout-v1'","uid",(long)p.Entity.UID,"id",id)==1);
        bool complete=equipped.All(i=>i.ID%100==(i.ID/10000==401?1u:i.ID/10000==402?27u:9u));Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11313 AND source='rune-full-level-v1'","uid",(long)p.Entity.UID)==(complete?1:0));
        if(reload){Require(complete);foreach(var rune in equipped){Require((long)Sql("SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=@position AND runesexp=@exp AND bound=@bound AND warehouse=0","item",(long)rune.UID,"uid",(long)p.Entity.UID,"type",(int)rune.ID,"position",(int)rune.Position,"exp",(long)rune.RunesEXP,"bound",rune.Bound?1:0)==1);uint skill=ConquerItemInformation.BaseInformations[rune.ID].subtype;Require(p.Spells.ContainsKey((ushort)(skill/100)) && p.Spells[(ushort)(skill/100)].Level==skill%100);}}
        Require(FreshRuneSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rune_probe")));Console.WriteLine("[career-fresh-rune] five-max="+complete);
        Console.WriteLine("[career-process] career-fresh-rune-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career runes did not close.");
    }
}
