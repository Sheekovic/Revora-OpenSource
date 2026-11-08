using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool fiveArtFixture;
    const uint FiveArtFirst=4294909201u;
    static string FiveArtSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@base::json,
'history',(SELECT json_build_array(experience,firstrebornclass,secondrebornclass,firstrebornlevel,secondrebornlevel,body) FROM entities WHERE uid=@uid),
'first',(SELECT json_agg(r ORDER BY uid) FROM revora_first_rebirths r WHERE uid=@uid),
'second',(SELECT json_agg(r ORDER BY uid) FROM revora_second_rebirths r WHERE uid=@uid),
'lessons',(SELECT json_agg(r ORDER BY spell) FROM revora_taoist_training r WHERE uid=@uid))::text",
            "base",PirateRiteSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static long FiveArtReceiptCount(uint uid,int rite) {
        return Convert.ToInt64(Sql(@"SELECT CASE @rite
WHEN 0 THEN (SELECT count(*) FROM revora_first_rebirths WHERE uid=@uid)
WHEN 1 THEN (SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid)
ELSE (SELECT count(*) FROM revora_reincarnations WHERE uid=@uid) END","rite",rite,"uid",(long)uid));
    }
    static void FiveArtProcess(bool reload) {
        fiveArtFixture=true;
        bool absent=(bool)Sql("SELECT to_regclass('revora_five_art_process_probe') IS NULL");
        if(absent){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294909201 AND 4294909216")==0);
            Sql("CREATE TABLE revora_five_art_process_probe(uid bigint PRIMARY KEY CHECK(uid BETWEEN 4294909201 AND 4294909216),snapshot text)");}
        uint uid=FiveArtFirst;
        foreach(int rite in new[]{0,1,2})foreach(byte source in new byte[]{75,135,145})foreach(bool keep in new[]{true,false}) {
            if(rite==2 && source==75)continue;
            byte destination=keep?(source==75?(byte)71:source==135?(byte)142:(byte)132):(byte)11;
            ushort[] arts=source==75?PirateRiteArts:new ushort[]{10309};
            bool exists=(long)Sql("SELECT count(*) FROM revora_five_art_process_probe WHERE uid=@uid","uid",(long)uid)==1;
            nextUid=uid;
            if(!exists){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);Player(Kernel.Maps[1002]);
                Sql("UPDATE entities SET class=@class,level=130,reborn=@reborn,firstrebornclass=@class,secondrebornclass=@class,firstrebornlevel=130,secondrebornlevel=130,atributes=600,conquerpoints=50000 WHERE uid=@uid","class",(int)source,"reborn",rite,"uid",(long)uid);
                Sql("INSERT INTO revora_five_art_process_probe(uid) VALUES(@uid)","uid",(long)uid);
            }
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
            Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
            var saved=Sql("SELECT snapshot FROM revora_five_art_process_probe WHERE uid=@uid","uid",(long)uid);
            long count=FiveArtReceiptCount(uid,rite);
            if(saved==DBNull.Value){Require(!reload);
                if(count==0){
                    Require(p.Entity.Class==source && p.Entity.Level==130 && p.Entity.Reborn==rite);
                    if(source==75){PirateRiteTalk(p,1004,4272,15);
                        foreach(uint type in new uint[]{3307432,3307433,3307434}){
                            ushort art=type==3307432?(ushort)14680:type==3307433?(ushort)14720:(ushort)14710;
                            if(p.Spells.ContainsKey(art))continue;
                            if(!p.Inventory.Objects.Any(i=>i.ID==type))p.Inventory.Add(type,0,1);
                            var scroll=p.Inventory.Objects.Single(i=>i.ID==type);
                            var packet=new ItemUsage(true){UID=scroll.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();
                            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);
                        }
                    }else PirateRiteTalk(p,1004,10000,61);
                    Require(!PirateFenced(p) && arts.All(p.Spells.ContainsKey));
                    foreach(ushort id in arts){p.Spells[id].Level=1;p.Spells[id].PreviousLevel=2;p.Spells[id].Experience=90;}
                    SkillTable.SaveSpells(p);
                    byte[] classes={11,21,41,51,61,71,81,132,142,161};
                    if(rite==0){if(!p.Inventory.Contains(721259,1))p.Inventory.Add(721259,0,1);
                        PirateRiteTalk(p,1002,59558,1);PirateRiteTalk(p,1002,59558,(byte)(200+Array.IndexOf(classes,destination)));
                        PirateRiteTalk(p,1002,59558,210);PirateRiteTalk(p,1002,59558,212);PirateRiteTalk(p,1002,59558,214);PirateRiteTalk(p,1002,59558,214);
                    }else if(rite==1){if(!p.Inventory.Contains(723701,1))p.Inventory.Add(723701,0,1);
                        PirateRiteTalk(p,1002,59558,2);PirateRiteTalk(p,1002,59558,(byte)(170+Array.IndexOf(classes,destination)));PirateRiteTalk(p,1002,59558,180);PirateRiteTalk(p,1002,59558,180);
                    }else {
                        PirateRiteTalk(p,1002,59558,3);PirateRiteTalk(p,1002,59558,6);PirateRiteTalk(p,1002,59558,7);
                        PirateRiteTalk(p,1002,59558,3);PirateRiteTalk(p,1002,59558,5);CareerPaidRequest(p,destination);CareerPaidRequest(p,destination);
                    }
                    Require(!PirateFenced(p) && p.Entity.Class==destination && p.Entity.Level==15);Require(EntityTable.SaveEntity(p));
                }
                saved=FiveArtSnapshot(p);Sql("UPDATE revora_five_art_process_probe SET snapshot=@snapshot WHERE uid=@uid","snapshot",saved,"uid",(long)uid);
            }
            Require(p.Entity.Class==destination && p.Entity.Level==15 && p.Entity.Reborn==(rite==0?1:2));
            Require(arts.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].PreviousLevel==2 && p.Spells[id].Experience==0:!p.Spells.ContainsKey(id)));
            Require(FiveArtReceiptCount(uid,rite)==1);
            Require(p.Entity.ConquerPoints==(rite==2?48000-Config.SerethReincarnationCp:50000));
            if(rite==0)Require(!p.Inventory.Contains(721259,1) && p.Inventory.Objects.Any(i=>i.ID==187405 && !i.Bound && i.Days==0));
            if(rite==1)Require(!p.Inventory.Contains(723701,1) && (long)Sql("SELECT count(*) FROM revora_second_rebirths r JOIN items i ON i.uid=r.soul_uid AND i.entityid=r.uid WHERE r.uid=@uid AND i.bound=1","uid",(long)uid)==1);
            Require(FiveArtSnapshot(p)==Convert.ToString(saved));
            System.Console.WriteLine("[five-art] rite="+rite+" source="+source+" destination="+destination+" exact-state-passed");uid++;
        }
        Require(uid==FiveArtFirst+16);
        System.Console.WriteLine("[pirate-process] pirate-five-art-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Five-art process did not close.");
    }
}
