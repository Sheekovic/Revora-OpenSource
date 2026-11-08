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
    static string FreshCtfSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'opponent_guild',@opponent::json,'run',(SELECT row_to_json(r) FROM revora_ctf_runs r WHERE run='ctf-20410803'),'members',(SELECT json_agg(r ORDER BY uid) FROM revora_ctf_members r WHERE run='ctf-20410803'),'deliveries',(SELECT json_agg(r ORDER BY token) FROM revora_ctf_deliveries r WHERE run='ctf-20410803'),'prizes',(SELECT json_agg(r ORDER BY uid) FROM revora_ctf_prizes r WHERE run='ctf-20410803'),'awards',(SELECT json_agg(r ORDER BY title_key) FROM revora_event_awards r WHERE uid=@uid AND source='ctf-local-wardrobe-v1'))::text","opponent",GuildFounderSnapshot(players[3]),"career",FreshRuneSnapshot(p,players),"uid",(long)p.Entity.UID));}
    static void FreshCtfJourney(string phase){
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim"),prepare=phase.EndsWith("prepare");
        var p=FreshLoadSaved(true,true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Call(typeof(Revora.Database.JiangHu),"LoadClient",p);Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)) && p.Entity.Class==25);
        var peer=players[3];Require(!p.Entity.Dead && !peer.Entity.Dead && p.Guild!=null);
        if(prepare){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_ctf_probe') IS NULL") && FreshRuneSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rune_probe")) && peer.Entity.GuildID==0 && peer.Entity.Level>=90 && peer.Entity.ConquerPoints>=(uint)Config.GuildCreateConquerPoints);
            uint cp=peer.Entity.ConquerPoints;FreshClassicTalk(peer,1002,10003,9);Revora.Network.PacketHandler.HandlePacket(FreshFounderFrame(10003,2,"FreshRival"),peer);Require(peer.Guild!=null && peer.Guild.ID!=p.Guild.ID && peer.Entity.GuildRank==1000 && peer.Entity.ConquerPoints==cp-(uint)Config.GuildCreateConquerPoints);
            foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("CREATE TABLE revora_fresh_ctf_probe(stage int,snapshot text);INSERT INTO revora_fresh_ctf_probe VALUES(0,@snapshot)","snapshot",FreshCtfSnapshot(p,players));Console.WriteLine("[fresh-ctf] opponent-guild-cp="+Config.GuildCreateConquerPoints);Console.WriteLine("[career-process] "+phase+"-passed");Environment.Exit(0);return;
        }
        if(phase.EndsWith("inspect")){System.IO.File.WriteAllText("fresh-ctf-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ctf_probe")));System.IO.File.WriteAllText("fresh-ctf-current.json",FreshCtfSnapshot(p,players));Console.WriteLine("[fresh-ctf] peer-guild="+peer.Entity.GuildID+" loaded="+(peer.Guild!=null)+" main-rank="+p.Entity.GuildRank);Console.WriteLine("[career-process] "+phase+"-passed");Environment.Exit(0);return;}
        var old=Server.Thread.CTF;var flags=BindingFlags.Instance|BindingFlags.NonPublic;var gate=typeof(CaptureTheFlag).GetField("sync",flags).GetValue(old);lock(gate)typeof(CaptureTheFlag).GetField("nextTick",flags).SetValue(old,DateTime.MaxValue);
        if(earn){
            Require((int)Sql("SELECT stage FROM revora_fresh_ctf_probe")==0 && FreshCtfSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ctf_probe")) && peer.Guild!=null && peer.Guild.ID!=p.Guild.ID && p.Entity.GuildRank==1000);
            DateTime now=new DateTime(2041,8,4,4,0,1,DateTimeKind.Utc);var map=Kernel.Maps[2057];var ctf=(CaptureTheFlag)Activator.CreateInstance(typeof(CaptureTheFlag),flags,null,new object[]{map,(Func<DateTime>)(()=>now)},null);Server.Thread.CTF=ctf;ctf.Tick();Require(CaptureTheFlag.IsWar);
            foreach(var player in new[]{p,peer}){HeartTalk(player,380,11);Require(player.Entity.MapID==2057);}
            var bases=map.Npcs.Values.OfType<SobNpcSpawn>().Where(n=>n.UID>=20571 && n.UID<=20573).OrderBy(n=>n.UID).ToArray();Require(bases.Length==3);
            var enemy=bases[1];peer.Entity.X=(ushort)(enemy.X+1);peer.Entity.Y=enemy.Y;peer.Screen.Add(enemy);peer.CantAttack=Time32.Now.AddSeconds(-1);peer.Entity.AttackStamp=Time32.Now.AddSeconds(-2);uint hp=enemy.Hitpoints;
            Attack.HandleAttack(new Attack(true){Attacker=peer.Entity.UID,Attacked=enemy.UID,X=enemy.X,Y=enemy.Y,AttackType=Attack.Melee},peer);Require(enemy.Hitpoints<hp);
            var target=bases[0];p.Entity.X=(ushort)(target.X+1);p.Entity.Y=target.Y;p.Screen.Add(target);p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;var until=DateTime.UtcNow.AddSeconds(60);
            while(target.Name!=p.Guild.Name && hits<20000 && DateTime.UtcNow<until){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.UID,X=target.X,Y=target.Y,AttackType=Attack.Melee},p);hits++;}
            Console.WriteLine("[fresh-ctf] base-hits="+hits+" remaining-hp="+target.Hitpoints+" captured="+(target.Name==p.Guild.Name));Require(target.Name==p.Guild.Name);
            var flag=map.StaticEntities.Values.First(f=>f.NpsEffect.Mesh==StaticEntity.FlagMesh);CtfWalkTo(p,flag.X,flag.Y);Require(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CarryingFlag));
            bool delivered=false;for(int dx=-8;dx<=8 && !delivered;dx++)for(int dy=-8;dy<=8 && !delivered;dy++){ushort x=(ushort)(target.X+dx),y=(ushort)(target.Y+dy);if(!map.Floor[x,y,MapObjectType.Player] || Kernel.GetDistance(x,y,target.X,target.Y)>=9)continue;CtfWalkTo(p,x,y);delivered=!p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CarryingFlag);}
            Require(delivered && (long)Sql("SELECT count(*) FROM revora_ctf_deliveries WHERE run='ctf-20410803' AND uid=@uid","uid",(long)p.Entity.UID)==1);
            now=new DateTime(2041,8,4,4,30,0,DateTimeKind.Utc);ctf.Tick();Require(!CaptureTheFlag.IsWar && p.Entity.MapID==1002 && peer.Entity.MapID==1002);
            Require((bool)Sql("SELECT state='complete' AND outcome='winner' AND winner=@guild FROM revora_ctf_runs WHERE run='ctf-20410803'","guild",(long)p.Guild.ID));
            Require((long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20410803' AND uid=@uid AND claimed_at IS NULL","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20410803' AND uid=@uid","uid",(long)peer.Entity.UID)==0);
            foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_ctf_probe SET stage=1,snapshot=@snapshot","snapshot",FreshCtfSnapshot(p,players));
        }else{
            Require(FreshCtfSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ctf_probe")) && (int)Sql("SELECT stage FROM revora_fresh_ctf_probe")==(claim?1:2));
            var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong money=p.Entity.Money;HeartTalk(p,380,12);HeartTalk(p,380,12);
            Require(p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rp && p.Entity.Money==money && p.Inventory.Count==before.Length+(claim?1:0));
            if(claim){var soul=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7 && (bool)Sql("SELECT item_uid=@item AND claimed_at IS NOT NULL FROM revora_ctf_prizes WHERE run='ctf-20410803' AND uid=@uid","item",(long)soul.UID,"uid",(long)p.Entity.UID));}
            Require(EntityTable.SaveEntity(p));if(claim)Sql("UPDATE revora_fresh_ctf_probe SET stage=2,snapshot=@snapshot","snapshot",FreshCtfSnapshot(p,players));else Require(FreshCtfSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ctf_probe")));
        }
        if(phase.EndsWith("reload")){uint prize=Convert.ToUInt32(Sql("SELECT item_uid FROM revora_ctf_prizes WHERE run='ctf-20410803' AND uid=@uid AND claimed_at IS NOT NULL","uid",(long)p.Entity.UID));var soul=p.Inventory.Objects.Single(i=>i.UID==prize);Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);}
        foreach(int id in new[]{10230,10301})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='ctf-local-v1' AND source_key='ctf-20410803'","uid",(long)p.Entity.UID,"id",id)==1);
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='ctf-local-wardrobe-v1' AND title_key IN(20030004,60020021) AND expires_at IS NULL","uid",(long)p.Entity.UID)==2);
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained CTF did not close.");
    }
}
