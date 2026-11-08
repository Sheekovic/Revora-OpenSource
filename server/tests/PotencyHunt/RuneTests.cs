using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static ConquerItem RuneItem(GameState p,uint id,byte position=211,ushort stack=1)
    {
        var item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=id,Position=position,StackSize=stack,
            Plus=(byte)(id>=730001 && id<=730008?id-730000:0)};
        Query("INSERT INTO items(uid,id,entityid,position,stacksize,plus,runesexp,bound) VALUES(@uid,@id,@owner,@position,@stack,@plus,0,0)",
            "uid",(long)item.UID,"id",(int)id,"owner",(long)p.Entity.UID,"position",(int)position,"stack",(int)stack,"plus",(int)item.Plus);
        if(position==0)p.Inventory.Add(item,Enums.ItemUse.None);else p.RunesItem.Add(item.UID,item);
        return item;
    }
    static GameState RunePlayer(Map map)
    {
        var p=Player(map);p.RunesItem=new SafeDictionary<uint,ConquerItem>();p.ChiData=new ChiTable.ChiData { Powers=p.ChiPowers };return p;
    }
    static byte[] ComposePacket(uint uid,params uint[] materials)
    {
        var packet=new byte[99+materials.Length*4];
        Writer.WriteUInt16((ushort)(packet.Length-8),0,packet);Writer.WriteUInt16(1009,2,packet);
        Writer.WriteUInt32(uid,8,packet);Writer.WriteUInt16(63,20,packet);
        Writer.WriteUInt32((uint)materials.Length,26,packet);
        for(int i=0;i<materials.Length;i++)Writer.WriteUInt32(materials[i],91+i*4,packet);
        return packet;
    }
    static long SavedRune(ConquerItem rune,string column)
    {return Scalar("SELECT "+column+" FROM items WHERE uid=@uid",rune.UID);}
    static void VerifyRunes(Map map)
    {
        Query("ALTER TABLE entities ADD COLUMN level int DEFAULT 140,ADD COLUMN class int DEFAULT 15,ADD COLUMN reborn int DEFAULT 2");
        ConquerItem.ItemUID=new Counter(3100000);
        var rt=typeof(Entity).Assembly.GetType("Revora.Network.GamePackets.RuneProgress");
        var now=new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc);
        rt.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        var p=RunePlayer(map);p.Entity.Reborn=2;p.Entity.FullyLoaded=true;var blue=RuneItem(p,4020101);var yellow=RuneItem(p,4030101);
        Check(p.Account.State==AccountTable.AccountState.Player && !p.Fake,"rune upgrades use an ordinary player");
        var stone=RuneItem(p,730008,0);RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,stone.UID));
        Check(!Fenced(p) && blue.ID==4020110 && blue.RunesEXP==2760,"+8 stone gives 29160 EXP with correct blue level and remainder");
        Check(SavedRune(blue,"id")==blue.ID && SavedRune(blue,"runesexp")==2760 && p.Inventory.Count==0,"rune ID, EXP and stone removal persist together");
        stone=RuneItem(p,730008,0);RunesSystem.HandlePacket1009(p,ComposePacket(yellow.UID,stone.UID));
        Check(yellow.ID==4030106 && yellow.RunesEXP==1660 && !Fenced(p),"yellow rune uses its own cumulative EXP curve");
        var stacked=RuneItem(p,730001,0,3);var request=ComposePacket(blue.UID,stacked.UID);
        Parallel.For(0,12,i=>RunesSystem.HandlePacket1009(p,request));
        Check(stacked.StackSize==2 && blue.RunesEXP==2770 && SavedRune(stacked,"stacksize")==2,"concurrent replay consumes one stone from a stack exactly once");
        now=now.AddSeconds(2);RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,stacked.UID));
        Check(stacked.StackSize==1 && blue.RunesEXP==2780,"a new deliberate request can consume the next stacked stone");
        byte[][] bad={new byte[0],new byte[25],ComposePacket(blue.UID),ComposePacket(blue.UID,blue.UID),
            ComposePacket(blue.UID,stacked.UID,stacked.UID),ComposePacket(blue.UID,0),ComposePacket(blue.UID,uint.MaxValue)};
        foreach(var packet in bad)RunesSystem.HandlePacket1009(p,packet);
        request=ComposePacket(blue.UID,stacked.UID);Writer.WriteUInt32(uint.MaxValue,26,request);RunesSystem.HandlePacket1009(p,request);
        request=ComposePacket(blue.UID,stacked.UID);request[0]--;RunesSystem.HandlePacket1009(p,request);
        Check(stacked.StackSize==1 && blue.RunesEXP==2780,"short, oversized-count, self, duplicate, unknown and mismatched-length inputs consume nothing");
        var outsider=RunePlayer(map);var foreign=RuneItem(outsider,730008,0);
        RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,foreign.UID));
        var wrong=RuneItem(p,1088000,0);RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,wrong.UID));
        stacked.Lock=1;RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,stacked.UID));stacked.Lock=0;
        Check(blue.RunesEXP==2780 && SavedRune(foreign,"id")==730008 && SavedRune(wrong,"id")==1088000,"foreign, unrelated and locked materials are rejected");
        var cap=RuneItem(p,4020126);cap.RunesEXP=29999;Query("UPDATE items SET runesexp=29999 WHERE uid=@uid","uid",(long)cap.UID);
        RunesSystem.HandlePacket1009(p,ComposePacket(cap.UID,stacked.UID));
        Check(cap.ID==4020127 && cap.RunesEXP==0,"crossing the maximum level stops without looping or overflowing");
        stone=RuneItem(p,730008,0);RunesSystem.HandlePacket1009(p,ComposePacket(cap.UID,stone.UID));
        var red=RuneItem(p,4010001);RunesSystem.HandlePacket1009(p,ComposePacket(red.UID,stone.UID));
        Check(SavedRune(stone,"id")==730008 && cap.ID==4020127 && red.ID==4010001,"max-level blue and fixed-level red runes never consume a stone");
        stone.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)stone.UID);
        RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,stone.UID));
        Check(blue.Bound && SavedRune(blue,"bound")==1,"bound material produces a durably bound rune");
        var sacrifice=RuneItem(p,4020102);sacrifice.RunesEXP=37;Query("UPDATE items SET runesexp=37 WHERE uid=@uid","uid",(long)sacrifice.UID);
        uint old=blue.RunesEXP;RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,sacrifice.UID));
        Check(!p.RunesItem.ContainsKey(sacrifice.UID) && blue.RunesEXP==old+537,"same-color rune materials retain their invested EXP");

        red.Position=101;blue.Position=102;yellow.Position=103;
        foreach(var item in new[]{red,blue,yellow})Query("UPDATE items SET position=@pos WHERE uid=@uid","pos",(int)item.Position,"uid",(long)item.UID);
        var entry=PrestigeRanking.Update(p);
        int runeScore=entry.Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value);
        Check(runeScore>0 && entry.Abilities.Abilities.Length==23,"equipped rune categories appear in prestige and its total");
        var bag=RuneItem(p,4030109);var before=PrestigeRanking.Update(p).Total;
        Check(before==entry.Total,"unequipped high-level rune contributes no prestige");
        Call(rt,null,"RefreshSkills",p);ushort skill=(ushort)(ConquerItemInformation.BaseInformations[blue.ID].subtype/100);
        Check(p.Spells[skill].Level==ConquerItemInformation.BaseInformations[blue.ID].subtype%100,"equipped skills rebuild from the persisted rune level");
        p.Spells[skill].Level=1;Call(rt,null,"RefreshSkills",p);
        Check(p.Spells[skill].Level==ConquerItemInformation.BaseInformations[blue.ID].subtype%100,"reconnect repairs a stale saved rune skill");
        p.LoadItemStats();
        stone=RuneItem(p,730008,0);RunesSystem.HandlePacket1009(p,ComposePacket(blue.UID,stone.UID));
        Check(!Fenced(p) && p.Spells[skill].Level==ConquerItemInformation.BaseInformations[blue.ID].subtype%100,
            "equipped rune upgrade immediately refreshes its skill and combat stats");
        entry=PrestigeRanking.Update(p);runeScore=entry.Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value);
        Query("ALTER TABLE entities ADD COLUMN face int DEFAULT 0,ADD COLUMN body int DEFAULT 1003,ADD COLUMN hairstyle int DEFAULT 0,ADD COLUMN my_title int DEFAULT 0,ADD COLUMN strength int DEFAULT 0,ADD COLUMN agility int DEFAULT 0,ADD COLUMN vitality int DEFAULT 0,ADD COLUMN spirit int DEFAULT 0,ADD COLUMN guildid int DEFAULT 0");
        Query("CREATE TABLE subclasses(id bigint); CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        p.Fake=true;outsider.Fake=true;PrestigeRanking.LoadSavedPlayers();
        var offline=PrestigeRanking.Snapshot(null).Single(x=>x.Profile.UID==p.Entity.UID);
        Check(offline.Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value)==runeScore,"offline prestige reload preserves the same equipped rune score");
        var invalidOffline=RunePlayer(map);invalidOffline.Fake=true;
        RuneItem(invalidOffline,4010001,101);RuneItem(invalidOffline,4020127,102);
        RuneItem(invalidOffline,4030209,103);RuneItem(invalidOffline,4030209,104);
        RuneItem(invalidOffline,4030309,105,2);RuneItem(invalidOffline,4030109,106);
        Query("UPDATE entities SET class=25 WHERE uid=@uid","uid",(long)invalidOffline.Entity.UID);
        PrestigeRanking.LoadSavedPlayers();
        Check(PrestigeRanking.Snapshot(null).Single(x=>x.Profile.UID==invalidOffline.Entity.UID).Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value)==0,
            "offline score excludes wrong-class pairs, repeated yellow types, invalid stacks and retired extra slots");
        Query("UPDATE entities SET class=15 WHERE uid=@uid","uid",(long)invalidOffline.Entity.UID);PrestigeRanking.LoadSavedPlayers();
        Check(PrestigeRanking.Snapshot(null).Single(x=>x.Profile.UID==invalidOffline.Entity.UID).Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value)==34000,
            "offline reload restores only legal class runes while preserving corrupt saved rows");
        p.Fake=false;
        var fail=RunePlayer(map);fail.Entity.Reborn=2;fail.Entity.FullyLoaded=true;var target=RuneItem(fail,4020101);var material=RuneItem(fail,730008,0);
        Query("CREATE TABLE rune_fixture_reference(uid bigint REFERENCES items(uid)); INSERT INTO rune_fixture_reference VALUES(@uid)","uid",(long)material.UID);
        try {RunesSystem.HandlePacket1009(fail,ComposePacket(target.UID,material.UID));}catch(NullReferenceException){}catch(ArgumentNullException){}
        Check(target.ID==4020101 && SavedRune(target,"id")==4020101 && SavedRune(target,"runesexp")==0 && SavedRune(material,"id")==730008,"late material-delete failure rolls back the target upgrade and keeps the stone");
        Check(Fenced(fail),"persistence failure requires reconnect before another mutation");
    }
}
