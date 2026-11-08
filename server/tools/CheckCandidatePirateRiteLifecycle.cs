using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool pirateRiteFixture;
    const uint PirateRiteFirst=4294909101u;
    static readonly ushort[] PirateRiteArts={11140,14680,14710,14720};
    static void PirateRiteTalk(GameState p,ushort map,uint npc,byte option) {
        p.BindWorldMap(map);var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;
        Require(EntityTable.SaveEntity(p));
        var packet=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,packet);Revora.Network.Writer.WriteUInt16(2032,2,packet);
        Revora.Network.Writer.WriteUInt32(npc,8,packet);packet[14]=option;
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);
        Revora.Network.PacketHandler.HandlePacket(packet,p);
        Require(!PirateFenced(p));
    }
    static string PirateRiteSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'profile',(SELECT json_build_array(uid,class,level,reborn,conquerpoints,strength,agility,vitality,spirit,atributes) FROM entities WHERE uid=@uid),
'skills',(SELECT json_agg(s ORDER BY id) FROM skills s WHERE entityid=@uid),
'items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid=@uid),
'purchases',(SELECT json_agg(r ORDER BY item_uid) FROM revora_pirate_map_purchases r WHERE uid=@uid),
'searches',(SELECT json_agg(r ORDER BY input_uid) FROM revora_pirate_searches r WHERE uid=@uid),
'exchanges',(SELECT json_agg(r ORDER BY scroll_uid) FROM revora_pirate_scroll_exchanges r WHERE uid=@uid),
'training',(SELECT json_agg(r ORDER BY spell) FROM revora_pirate_training r WHERE uid=@uid),
'rites',(SELECT json_agg(r ORDER BY id) FROM revora_reincarnations r WHERE uid=@uid),
'dew',(SELECT json_agg(r ORDER BY id) FROM revora_oblivion_purchases r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));
    }
    static void PirateRiteProcess(bool reload) {
        pirateRiteFixture=true;
        Require((bool)Feature("PirateEpicWorld").GetProperty("Enabled",Hidden).GetValue(null,null));
        bool absent=(bool)Sql("SELECT to_regclass('revora_pirate_rite_process_probe') IS NULL");
        if(absent){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294909101 AND 4294909102")==0);
            Sql("CREATE TABLE revora_pirate_rite_process_probe(uid bigint PRIMARY KEY CHECK(uid BETWEEN 4294909101 AND 4294909102),snapshot text)");
        }
        var draw=Feature("PirateEpicStore").GetField("SearchDraw",Hidden);var original=draw.GetValue(null);
        try {draw.SetValue(null,(Func<int,int>)(n=>n-1));
            for(int index=0;index<2;index++) {
                uint uid=PirateRiteFirst+(uint)index;byte destination=index==0?(byte)71:(byte)11;
                bool exists=(long)Sql("SELECT count(*) FROM revora_pirate_rite_process_probe WHERE uid=@uid","uid",(long)uid)==1;
                nextUid=uid;
                if(!exists){
                    Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
                    Player(Kernel.Maps[1002]);
                    Sql("UPDATE entities SET class=75,level=110,reborn=2,firstrebornclass=75,secondrebornclass=75,firstrebornlevel=130,secondrebornlevel=130,atributes=600,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
                    Sql("INSERT INTO revora_pirate_rite_process_probe(uid) VALUES(@uid)","uid",(long)uid);
                }
                nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
                Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player);
                var saved=Sql("SELECT snapshot FROM revora_pirate_rite_process_probe WHERE uid=@uid","uid",(long)uid);
                if(saved==DBNull.Value){
                    Require(!reload && p.Entity.Class==75 && p.Entity.Reborn==2 && p.Entity.Level==110);
                    PirateRiteTalk(p,1004,4272,15);Require(p.Spells.ContainsKey(11140));
                    PirateRiteTalk(p,1004,4272,150);Require(p.Entity.MapID==10271);
                    foreach(byte choice in new byte[]{1,2,3}) {
                        ushort art=choice==1?(ushort)14680:choice==2?(ushort)14720:(ushort)14710;
                        if(p.Spells.ContainsKey(art))continue;
                        uint type=3307431u+choice;
                        while(!p.Inventory.Objects.Any(i=>i.ID==type) && TaoistCount(p,3307450)<10) {
                            Require((long)Sql("SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid","uid",(long)uid)<30);
                            if(!p.Inventory.Objects.Any(i=>i.ID==3307443)){PirateRiteTalk(p,10271,199590,1);PirateRiteTalk(p,10271,199590,20);PirateRiteTalk(p,10271,199590,20);}
                            PirateRiteTalk(p,10271,199592,1);PirateRiteTalk(p,10271,199592,20);PirateRiteTalk(p,10271,199592,20);
                        }
                        if(!p.Inventory.Objects.Any(i=>i.ID==type)){PirateRiteTalk(p,10271,199591,choice);PirateRiteTalk(p,10271,199591,20);PirateRiteTalk(p,10271,199591,20);}
                        var scroll=p.Inventory.Objects.Single(i=>i.ID==type);Require(scroll.Bound);
                        var use=new ItemUsage(true){UID=scroll.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();
                        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,use.Length-8,use);
                        ItemUsage.Process(use,p);ItemUsage.Process(use,p);Require(p.Spells.ContainsKey(art) && !p.Inventory.ContainsUID(scroll.UID) && !PirateFenced(p));
                    }
                    Require(p.Entity.ConquerPoints==49190 && PirateRiteArts.All(p.Spells.ContainsKey));
                    PirateRiteTalk(p,1002,59558,3);PirateRiteTalk(p,1002,59558,6);PirateRiteTalk(p,1002,59558,7);PirateRiteTalk(p,1002,59558,7);
                    Require(p.Entity.ConquerPoints==47190 && p.Inventory.Contains(711083,1));
                    PirateRiteTalk(p,1002,59558,3);PirateRiteTalk(p,1002,59558,5);CareerPaidRequest(p,destination);CareerPaidRequest(p,destination);
                    Require(!PirateFenced(p) && p.Entity.Class==destination);Require(EntityTable.SaveEntity(p));
                    saved=PirateRiteSnapshot(p);Sql("UPDATE revora_pirate_rite_process_probe SET snapshot=@snapshot WHERE uid=@uid","snapshot",saved,"uid",(long)uid);
                }
                Require(p.Entity.Class==destination && p.Entity.Level==15 && p.Entity.Reborn==2 && p.Entity.ConquerPoints==47190-Config.SerethReincarnationCp);
                Require(PirateRiteArts.All(id=>p.Spells.ContainsKey(id)==(destination==71)));
                Require((long)Sql("SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid","uid",(long)uid)==30 && (long)Sql("SELECT count(*) FROM revora_pirate_searches WHERE uid=@uid","uid",(long)uid)==30);
                Require((long)Sql("SELECT count(*) FROM revora_pirate_training t JOIN revora_pirate_scroll_exchanges e ON e.uid=t.uid AND e.scroll_uid=t.scroll_uid AND e.scroll_type=t.scroll_type WHERE t.uid=@uid","uid",(long)uid)==3);
                Require((long)Sql("SELECT count(*) FROM revora_reincarnations WHERE uid=@uid","uid",(long)uid)==1 && (long)Sql("SELECT count(*) FROM revora_oblivion_purchases WHERE uid=@uid","uid",(long)uid)==1);
                Require(PirateRiteSnapshot(p)==Convert.ToString(saved));
                System.Console.WriteLine("[pirate-rite] destination="+destination+" exact-state-passed");
            }
        }finally{draw.SetValue(null,original);}
        System.Console.WriteLine("[pirate-process] pirate-rite-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Pirate rite process did not close.");
    }
}
