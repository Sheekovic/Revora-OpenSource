using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSiegeSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'battle',(SELECT row_to_json(b) FROM revora_guild_battles b WHERE day='2041-03-17'),'receipt',(SELECT row_to_json(r) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id=10231))::text","career",FreshArenaPacksSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSiegeJourney(bool reload){
        var war=Feature("GuildSeasonWar");DateTime now=new DateTime(2041,3,17,14,0,0,DateTimeKind.Local).ToUniversalTime();war.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));war.GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);var purse=Feature("GuildWarPurses");lock(purse.GetField("pollGate",Hidden).GetValue(null))purse.GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && p.Guild!=null && p.Guild.Name=="FreshBanner" && !p.Entity.Dead);
        if(!reload){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_siege_probe') IS NULL") && FreshArenaPacksSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_packs_probe")));
            Require(p.Inventory.Count<40);HeartTalk(p,91008,0);uint cp=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[721261].ConquerPointsWorth;Require(price>0);var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();var buy=new ItemUsage(true){UID=91008,dwParam=721261,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
            var bomb=p.Inventory.Objects.Single(i=>!old.Contains(i.UID));Require(bomb.ID==721261 && p.Entity.ConquerPoints==cp-price);uint item=bomb.UID;
            now=now.AddHours(1);GuildWar.Start();Require(GuildWar.IsWar);HeartTalk(p,380,6);Require(p.Entity.MapID==1038);var gate=GuildWar.LeftGate;p.Entity.X=(ushort)(gate.X+1);p.Entity.Y=gate.Y;uint hp=gate.Hitpoints,damage=(uint)Math.Min((ulong)hp,((ulong)gate.MaxHitpoints+3)/4);
            ItemHandler.UseItem(bomb,p);Require(!p.Inventory.ContainsUID(item) && p.Entity.Dead && gate.Hitpoints==hp-damage);ItemHandler.UseItem(bomb,p);Require(gate.Hitpoints==hp-damage && p.Entity.ConquerPoints==cp-price);
            p.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},p);Require(!p.Entity.Dead && p.Entity.MapID==1002);
            now=now.AddHours(1);GuildWar.End();Require(!GuildWar.IsWar && (bool)Sql("SELECT state='empty' AND captures=0 AND keeper=0 FROM revora_guild_battles WHERE day='2041-03-17'") && (long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2041-03-17'")==0);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_siege_probe(snapshot text,item bigint,price bigint);INSERT INTO revora_fresh_siege_probe VALUES(@snapshot,@item,@price)","snapshot",FreshSiegeSnapshot(p),"item",(long)item,"price",(long)price);Console.WriteLine("[fresh-siege] paid-cp="+price+" gate-damage="+damage);
        }else{
            Require(FreshSiegeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_siege_probe")));uint item=Convert.ToUInt32(Sql("SELECT item FROM revora_fresh_siege_probe"));Require(!p.Inventory.ContainsUID(item) && (long)Sql("SELECT count(*) FROM items WHERE uid=@item","item",(long)item)==0);
            var packet=new ItemUsage(true){UID=item,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);Require(EntityTable.SaveEntity(p) && FreshSiegeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_siege_probe")));
        }
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10231 AND source='guild-siege-bomb-v1'","uid",(long)p.Entity.UID)==1);
        Console.WriteLine("[career-process] career-fresh-siege-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Siege career did not close.");
    }
}
