using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSecondRiteSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'profile',(SELECT json_build_array(level,class,reborn,firstrebornclass,firstrebornlevel,secondrebornclass,secondrebornlevel,strength,agility,vitality,spirit,atributes) FROM entities WHERE uid=@uid),
'double_exp',(SELECT doubleexptime FROM entities WHERE uid=@uid),'potions',(SELECT json_agg(r ORDER BY used_at,id) FROM revora_exp_potion_uses r WHERE uid=@uid),'rite',(SELECT row_to_json(r) FROM revora_second_rebirths r WHERE uid=@uid),'purchases',(SELECT json_agg(r ORDER BY purchased_at,id) FROM revora_exemption_purchases r WHERE uid=@uid))::text","career",FreshRebirthSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSecondRiteSave(GameState p,string stage) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_second_rite_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshSecondRiteSnapshot(p));}
    static void FreshSecondRiteProcess(string phase) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(phase.EndsWith("inspect")) {
            var ids=(long[])Sql("SELECT rewards FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2 AND milestone=1","uid",(long)uid);
            Console.WriteLine("[second-gear] level="+p.Entity.Level+" class="+p.Entity.Class+" strength="+p.Entity.Strength+" agility="+p.Entity.Agility+" free="+p.Entity.Atributes);
            foreach(var item in p.Inventory.Objects.Concat(p.Equipment.GetItems()).Where(i=>ids.Contains((long)i.UID))) {
                var info=ConquerItemInformation.BaseInformations[item.ID];Console.WriteLine("[second-gear] uid="+item.UID+" type="+item.ID+" slot="+item.Position+" required-level="+info.Level+" required-class="+info.Class+" required-strength="+info.Strength+" required-agility="+info.Agility+" required-prof="+info.Proficiency+" equipable="+ItemHandler.Equipable(item,p));
            }
            System.IO.File.WriteAllText("fresh-second-rite-current.json",FreshSecondRiteSnapshot(p));System.IO.File.WriteAllText("fresh-second-rite-prior.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_rite_probe")));
            Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Gear inspection did not close.");
        }
        if(phase.EndsWith("recover")) {
            string prior=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_rite_probe")),current=FreshSecondRiteSnapshot(p);
            using(var sha=System.Security.Cryptography.SHA256.Create()) {
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(prior))).Replace("-","").ToLowerInvariant()=="808d5461ba948aefe5457f0f3392999f80c9ab6e1ea0d1baabf9fb2fcc435486" && System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(current))).Replace("-","").ToLowerInvariant()=="1d6846063c5818952bb06a2883fe26d27947dff9a4196b69f5cfed9abb2c6e95");
            }
            // Full-row recovery audit: only final claim, two P7 choices and four ordinary swaps differ.
            // The level-40 shield refusal was correct; never reissue the already committed rewards.
            FreshSecondRiteSave(p,"claimed");FreshSecondStarterEquip(p);FreshSecondRiteSave(p,"claimed");
            Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Starter recovery did not close.");
        }
        if(phase.EndsWith("offer")) {
            Require(p.Entity.Reborn==1 && p.Entity.Level==120 && p.Entity.Class==15 && FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_ready_probe")) && (bool)Sql("SELECT to_regclass('revora_fresh_second_rite_probe') IS NULL"));
            Require(!p.Inventory.Contains(723701,1));uint cps=p.Entity.ConquerPoints;uint price=ConquerItemInformation.BaseInformations[723701].ConquerPointsWorth;
            Require(price==888);CareerBuy(p,723701);var token=p.Inventory.Objects.Single(i=>i.ID==723701);Require(!token.Bound && p.Entity.ConquerPoints==cps-price);
            Require((long)Sql("SELECT count(*) FROM revora_exemption_purchases WHERE uid=@uid AND item_type=723701 AND cost=888 AND balance_before=@before AND balance_after=@after AND @item=ANY(item_uids)","uid",(long)uid,"before",(long)cps,"after",(long)p.Entity.ConquerPoints,"item",(long)token.UID)==1);
            p.Entity.Teleport(1002,396,366);HeartTalk(p,59558,2);HeartTalk(p,59558,171);
            Require(p.Entity.Reborn==1 && p.Inventory.ContainsUID(token.UID) && !PirateFenced(p));Require(EntityTable.SaveEntity(p));
            Sql("CREATE TABLE revora_fresh_second_rite_probe(uid bigint PRIMARY KEY,token bigint,starter bigint,stage text,snapshot text);INSERT INTO revora_fresh_second_rite_probe VALUES(@uid,@token,@starter,'offered',@snapshot)","uid",(long)uid,"token",(long)token.UID,"starter",(long)p.Inventory.Objects.Single(i=>i.ID==727990).UID,"snapshot",FreshSecondRiteSnapshot(p));
        } else {
            Require(FreshSecondRiteSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_rite_probe")));
            uint token=Convert.ToUInt32(Sql("SELECT token FROM revora_fresh_second_rite_probe")),starter=Convert.ToUInt32(Sql("SELECT starter FROM revora_fresh_second_rite_probe"));
            if(phase.EndsWith("complete")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_second_rite_probe"))=="offered" && p.Entity.Reborn==1);
                string before=FreshSecondRiteSnapshot(p);HeartTalk(p,59558,180);Require(before==FreshSecondRiteSnapshot(p));
                uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
                foreach(byte option in new byte[]{2,171,180})HeartTalk(p,59558,option);
                Require(!PirateFenced(p) && p.Entity.Reborn==2 && p.Entity.Level==15 && p.Entity.Class==21 && p.Entity.FirstRebornClass==15 && p.Entity.FirstRebornLevel==120 && p.Entity.SecondRebornClass==15 && p.Entity.SecondRebornLevel==120 && !p.Inventory.ContainsUID(token));
                Require(p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
                Require((long)Sql("SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid AND payment_kind='exemption-token' AND token_uid=@token AND new_class=21 AND first_class=15 AND first_level=120 AND old_class=15 AND old_level=120","uid",(long)uid,"token",(long)token)==1);
                uint soul=Convert.ToUInt32(Sql("SELECT soul_uid FROM revora_second_rebirths WHERE uid=@uid","uid",(long)uid));var reward=p.Inventory.Objects.Single(i=>i.UID==soul);Require(reward.Bound && reward.Days==0 && ConquerItemInformation.BaseInformations[reward.ID].PurificationLevel==7);
                Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10316 AND source='second-rebirth-v1'","uid",(long)uid)==1 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11031","uid",(long)uid)==0);
                FreshSecondRiteSave(p,"reborn");
            }
            if(phase.EndsWith("starter")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_second_rite_probe"))=="reborn" && p.Entity.Class==21 && p.Entity.Reborn==2);
                var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();int count=p.Inventory.Count;uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
                p.Filtering=true;ChoiceWireOpen(p,starter);
                Require(!PirateFenced(p) && !p.Inventory.ContainsUID(starter) && p.Inventory.Count==count+8 && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound+100000 && p.Entity.Money==silver+5000000);
                var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==9 && added.All(i=>i.Bound));
                var gear=added.Where(i=>i.ID!=3006233).ToArray();Require(gear.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{111009,131009,120009,150009,160019,410009,900009}.OrderBy(i=>i)) && gear.All(i=>i.Plus==12 && i.Bless==7 && i.SocketOne==Enums.Gem.SuperDragonGem && i.SocketTwo==Enums.Gem.SuperDragonGem));
                var packs=added.Where(i=>i.ID==3006233).OrderBy(i=>i.UID).ToArray();Require(packs.Length==2);
                var choices=ConquerItemInformation.BaseInformations.Where(i=>i.Value.PurificationLevel==7).Select(i=>i.Key).OrderBy(i=>i).ToArray();
                for(int i=0;i<2;i++) {
                    ChoiceWireOpen(p,packs[i].UID);Require(p.ActiveNpc==3006233);ChoiceWireSend(p,ChoiceWireFrame((byte)(i+1)));
                    Require(!p.Inventory.ContainsUID(packs[i].UID) && !PirateFenced(p));
                    Require((long)Sql("SELECT count(*) FROM revora_p7_choice_uses r JOIN items i ON i.uid=r.result_uid AND i.entityid=r.uid AND i.id=r.result_type WHERE r.uid=@uid AND r.pack_uid=@pack AND r.result_type=@type AND i.bound=1","uid",(long)uid,"pack",(long)packs[i].UID,"type",(int)choices[i])==1);
                    string once=FreshSecondRiteSnapshot(p);ChoiceWireOpen(p,packs[i].UID);ChoiceWireSend(p,ChoiceWireFrame((byte)(i+1)));Require(once==FreshSecondRiteSnapshot(p));
                }
                FreshSecondRiteSave(p,"claimed");FreshSecondStarterEquip(p);
                Require((bool)Sql("SELECT NOT revora_starter_pending FROM revora_character_creation WHERE character_uid=@uid","uid",(long)uid));
                FreshSecondRiteSave(p,"claimed");
            }
            Require(p.Entity.Reborn==2 && p.Entity.Level==15 && p.Entity.Class==21 && !p.Inventory.ContainsUID(token));
            string saved=FreshSecondRiteSnapshot(p);HeartTalk(p,59558,180);Require(saved==FreshSecondRiteSnapshot(p));
            if(phase.EndsWith("starter") || phase.EndsWith("reload")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_second_rite_probe"))=="claimed");
                p.Filtering=true;ChoiceWireOpen(p,starter);Call(Feature("RevoraStarterPack"),"OnLogin",p);Require(saved==FreshSecondRiteSnapshot(p) && !p.Inventory.Contains(727990,1) && !PirateFenced(p));
                Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2 AND milestone=1","uid",(long)uid)==1 && p.Entity.BoundCps==150000 && p.Entity.Money==6100000);
            }
        }
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" reborn="+p.Entity.Reborn+" cps="+p.Entity.ConquerPoints+" bound="+p.Entity.BoundCps+" silver="+p.Entity.Money);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh second rite did not close.");
    }
    static void FreshSecondStarterEquip(GameState p) {
        var ids=(long[])Sql("SELECT rewards FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2 AND milestone=1","uid",(long)p.Entity.UID);p.Filtering=true;
        var gear=p.Inventory.Objects.Concat(p.Equipment.GetItems()).Where(i=>ids.Contains((long)i.UID)).ToArray();
        foreach(var entry in new[]{Tuple.Create(111009u,1u),Tuple.Create(120009u,2u),Tuple.Create(131009u,3u),Tuple.Create(410009u,4u),Tuple.Create(150009u,6u),Tuple.Create(160019u,8u)}) {
            var item=gear.Single(i=>i.ID==entry.Item1);
            if(item.Position!=entry.Item2){var frame=new ItemUsage(true){UID=item.UID,dwParam=entry.Item2,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);ChoiceWireSend(p,frame);}
            Require(p.Equipment.GetItems().Any(i=>i.UID==item.UID && i.Position==entry.Item2) && !PirateFenced(p));
        }
        var shield=gear.Single(i=>i.ID==900009);Require(p.Entity.Level==15 && shield.Position==0 && ConquerItemInformation.BaseInformations[shield.ID].Level==40 && !ItemHandler.Equipable(shield,p));
    }

}
