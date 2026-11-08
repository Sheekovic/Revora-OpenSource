using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static class EquipmentProficiencyTests {
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Main(){
        ConquerItemInformation.BaseInformations=new SafeDictionary<uint,ConquerItemBaseInformation>();
        ConquerItemInformation.PlusInformations=new SafeDictionary<uint,SafeDictionary<byte,ConquerItemPlusInformation>>();
        var p=(GameState)FormatterServices.GetUninitializedObject(typeof(GameState));
        p.Entity=new Entity(EntityFlag.Player,false);p.Entity.SendUpdates=false;p.Entity.Body=1003;p.Entity.Level=140;p.Entity.Class=15;
        p.Proficiencies=new SafeDictionary<ushort,Revora.Interfaces.IProf>();
        var info=new ConquerItemBaseInformation{ID=410003,Level=71,Proficiency=5};ConquerItemInformation.BaseInformations.Add(info.ID,info);
        var item=new ConquerItem(true){ID=info.ID};
        foreach(byte reborn in new byte[]{0,1,2}){
            p.Entity.Reborn=reborn;p.Proficiencies.Clear();info.Level=71;info.Proficiency=5;
            Check(!ItemHandler.Equipable(item,p),"missing proficiency, life "+reborn);
            var prof=new Proficiency(true){ID=410,Level=4,PreviousLevel=12};p.Proficiencies.Add(410,prof);
            Check(!ItemHandler.Equipable(item,p),"low current proficiency cannot borrow history");
            prof.Level=5;Check(ItemHandler.Equipable(item,p),"exact required proficiency");
            prof.Level=6;Check(ItemHandler.Equipable(item,p),"above required proficiency");
            p.Proficiencies.Clear();info.Proficiency=0;Check(ItemHandler.Equipable(item,p),"zero requirement without record");
            info.Level=70;info.Proficiency=5;Check(ItemHandler.Equipable(item,p)==(reborn>0),"low item rebirth exemption");
        }
        p.Entity.Reborn=1;p.Entity.Level=69;info.Level=60;Check(!ItemHandler.Equipable(item,p),"reborn under70 still needs proficiency");
        p.Entity.Level=70;info.Level=70;Check(ItemHandler.Equipable(item,p),"reborn threshold70");
        p.Entity.Reborn=0;p.Entity.Level=140;info.Level=71;info.Proficiency=0;
        info.Gender=2;Check(!ItemHandler.Equipable(item,p),"gender guard");info.Gender=0;
        info.Class=25;Check(!ItemHandler.Equipable(item,p),"class guard");info.Class=0;
        info.Strength=1;Check(!ItemHandler.Equipable(item,p),"stat guard");info.Strength=0;
        info.Level=141;Check(!ItemHandler.Equipable(item,p),"level guard");
        var arrow=new ConquerItemBaseInformation{ID=1050000,Level=1,Proficiency=5};ConquerItemInformation.BaseInformations.Add(arrow.ID,arrow);
        Check(ItemHandler.Equipable(new ConquerItem(true){ID=arrow.ID},p),"nonreborn arrow exemption");
        var mount=new ConquerItemBaseInformation{ID=200449,Level=140,Proficiency=1};ConquerItemInformation.BaseInformations.Add(mount.ID,mount);
        var cosmetic=new ConquerItem(true){ID=mount.ID};p.Entity.Level=140;p.Proficiencies.Clear();
        foreach(byte life in new byte[]{0,1,2}){p.Entity.Reborn=life;Check(ItemHandler.Equipable(cosmetic,p),"mount armor needs no weapon proficiency in life "+life);}
        mount.Level=141;Check(!ItemHandler.Equipable(cosmetic,p),"mount armor retains level requirement");mount.Level=140;
        mount.Gender=2;Check(!ItemHandler.Equipable(cosmetic,p),"mount armor retains gender requirement");
        foreach(uint id in new uint[]{111009,131009}){
            var armor=new ConquerItemBaseInformation{ID=id,Level=70,Class=20};ConquerItemInformation.BaseInformations.Add(id,armor);
            var gear=new ConquerItem(true){ID=id};p.Entity.Class=41;p.Entity.Level=70;p.Entity.Reborn=2;
            Check(ItemHandler.EquipPassRbReq(armor,p) && ItemHandler.Equipable(gear,p),"reborn Archer low Warrior gear "+id);
            p.Entity.Level=69;Check(!ItemHandler.EquipPassRbReq(armor,p) && !ItemHandler.Equipable(gear,p),"below reborn level threshold "+id);
            p.Entity.Level=70;p.Entity.Reborn=0;Check(!ItemHandler.EquipPassRbReq(armor,p) && !ItemHandler.Equipable(gear,p),"nonreborn class guard "+id);
            p.Entity.Reborn=2;p.Entity.Level=140;armor.Level=71;Check(!ItemHandler.EquipPassRbReq(armor,p) && !ItemHandler.Equipable(gear,p),"high Warrior gear class guard "+id);
            armor.Level=70;armor.Gender=2;Check(!ItemHandler.Equipable(gear,p),"low reborn gear retains gender guard "+id);
        }
        System.Console.WriteLine(checks+" equipment proficiency checks passed; synthetic profile/catalog, no database or listener.");
    }
}
