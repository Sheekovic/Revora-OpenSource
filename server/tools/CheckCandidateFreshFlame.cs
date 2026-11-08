using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshFlameSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'runs',(SELECT json_agg(r ORDER BY war_day) FROM revora_flame_runs r WHERE uid=@uid),'steps',(SELECT json_agg(s ORDER BY s.run,s.generation,s.stage) FROM revora_flame_steps s JOIN revora_flame_runs r ON r.id=s.run WHERE r.uid=@uid),'claims',(SELECT json_agg(c ORDER BY c.run) FROM revora_flame_claims c JOIN revora_flame_runs r ON r.id=c.run WHERE r.uid=@uid))::text","career",FreshSiegeSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshFlameJourney(string phase){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var war=Feature("GuildSeasonWar");DateTime now=new DateTime(2041,3,24,15,5,0,DateTimeKind.Local).ToUniversalTime();bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");if(!earn)now=now.AddHours(1);war.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Action buyScroll=()=>{if(p.Inventory.Contains(1060020,1))return;var shop=ShopFile.Shops.Values.First(v=>v.MoneyType==ShopFile.MoneyType.Gold && v.Items.Contains(1060020) && p.Map.Npcs.ContainsKey(v.UID));CareerSilverBuy(p,1060020,shop.UID);};
        Action town=()=>{ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==1060020),p);Require(p.Entity.MapID==1002);};
        if(earn){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_flame_probe') IS NULL") && FreshSiegeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_siege_probe")) && p.Inventory.Count<=38);buyScroll();
            Call(war,"Start");Require(Revora.Game.GuildWar.IsWar);HeartTalk(p,4452,0);HeartTalk(p,4452,1);Require(p.Inventory.Objects.Any(i=>i.ID==729960));
            ushort[] maps={1002,1038,1038,1038,1038,1038,1038,1038,1002,1038};
            for(int n=0;n<10;n++){
                if(p.Entity.MapID!=maps[n]){if(maps[n]==1038){HeartTalk(p,380,1);Require(p.Entity.MapID==1038);}else{town();buyScroll();}}
                uint npc=(uint)(4453+n);var rune=p.Inventory.Objects.Single(i=>i.ID==729960u+(uint)n);
                if(n==9){HeartTalk(p,npc,0);string early=FreshFlameSnapshot(p);HeartTalk(p,npc,1);Require(early==FreshFlameSnapshot(p));now=now.AddMinutes(25);}
                HeartTalk(p,npc,0);HeartTalk(p,npc,1);Require(!p.Inventory.ContainsUID(rune.UID) && p.Inventory.Objects.Any(i=>i.ID==729961u+(uint)n));string after=FreshFlameSnapshot(p);ItemHandler.UseItem(rune,p);Require(after==FreshFlameSnapshot(p));
            }
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11016","uid",(long)p.Entity.UID)==1);town();HeartTalk(p,4452,0);string unpaid=FreshFlameSnapshot(p);HeartTalk(p,4452,2);Require(unpaid==FreshFlameSnapshot(p));Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_flame_probe(done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_flame_probe VALUES(false,@snapshot)","snapshot",FreshFlameSnapshot(p));
            Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        Require(FreshFlameSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_flame_probe")));
        if(claim){Require((long)Sql("SELECT count(*) FROM revora_guild_battles WHERE day='2041-03-24' AND state='cancelled'")==1);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();byte level=p.Entity.Level;ulong exp=p.Entity.Experience;HeartTalk(p,4452,0);HeartTalk(p,4452,2);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7 && (p.Entity.Level>level || p.Entity.Experience>exp) && !p.Inventory.Objects.Any(i=>i.ID==729970));Require((long)Sql("SELECT count(*) FROM revora_flame_claims c JOIN revora_flame_runs r ON r.id=c.run WHERE r.uid=@uid AND c.units=12 AND c.experience>0 AND r.claimed AND c.soul_uid=@item","uid",(long)p.Entity.UID,"item",(long)added[0].UID)==1);string after=FreshFlameSnapshot(p);HeartTalk(p,4452,2);HeartTalk(p,4452,0);HeartTalk(p,4452,2);Require(after==FreshFlameSnapshot(p));Console.WriteLine("[fresh-flame] level="+level+"->"+p.Entity.Level+" exp-award="+Sql("SELECT c.experience FROM revora_flame_claims c JOIN revora_flame_runs r ON r.id=c.run WHERE r.uid=@uid","uid",(long)p.Entity.UID));Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_flame_probe SET done=true,snapshot=@snapshot","snapshot",FreshFlameSnapshot(p));}
        else{Require((bool)Sql("SELECT done FROM revora_fresh_flame_probe"));uint soul=Convert.ToUInt32(Sql("SELECT c.soul_uid FROM revora_flame_claims c JOIN revora_flame_runs r ON r.id=c.run WHERE r.uid=@uid","uid",(long)p.Entity.UID));Require(p.Inventory.ContainsUID(soul));HeartTalk(p,4452,0);HeartTalk(p,4452,2);Require(FreshFlameSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_flame_probe")));}
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career Flame did not close.");
    }
}
