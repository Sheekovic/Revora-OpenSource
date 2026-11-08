using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly ushort[] ninjaNewArts={11170,11180,11230,12070,12080,12090,12110};
    static void NinjaInstructorMap(string runtime) {
        var map=World(runtime,1004);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=4720 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Wind Sage missing.");map.AddNpc(new NpcSpawn{UID=4720,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker});}}
    }
    static void LearnNinjaArts(GameState p) {
        p.Entity.Teleport(1004,38,42);p.ActiveNpc=4720;EntityTable.SaveEntity(p);
        foreach(uint item in new uint[]{3004459,3004460,3004461})NestedPack(p,item,1,true);
        foreach(byte option in new byte[]{60,61,69,80,81,82,83})SoulWire(p,option,4720);
        if(Fenced(p) || ninjaNewArts.Any(id=>!p.Spells.ContainsKey(id)))throw new Exception("Ninja rite training failed.");
        foreach(var id in ninjaNewArts){p.Spells[id].Level=2;p.Spells[id].PreviousLevel=3;p.Spells[id].Experience=51;p.Spells[id].Souls=(Spell.Soul_Level)1;}SkillTable.SaveSpells(p);
    }
    static Entity[] NinjaClones(GameState p) {
        var template=MonsterInformation.MonsterInformations[9003];var owner=template.Owner;
        Entity.AddClone(p,p.Entity.Name,3);Entity.AddClone(p,p.Entity.Name,10003);var clones=p.Entity.MyClones.Values.ToArray();
        Check(clones.Length==2 && clones.All(c=>c.IAmShadow && c.ShadowID==p.Entity.UID && ReferenceEquals(c.Owner,p) && ReferenceEquals(c.MonsterInfo.Owner,c))
            && !ReferenceEquals(clones[0].MonsterInfo,clones[1].MonsterInfo) && ReferenceEquals(template.Owner,owner),"actual Shadow Clone creation isolates both owners from the shared monster catalog");return clones;
    }
    static bool CloneRegistered(Entity clone) {var map=Kernel.Maps[clone.MapID];return map.Entities.ContainsKey(clone.UID) || map.Companions.ContainsKey(clone.UID);}
    static byte[] CloneWire(uint target,params uint[] clones) {
        var packet=new byte[9+clones.Length*4+8];Writer.WriteUInt16((ushort)(packet.Length-8),0,packet);Writer.WriteUInt16(2812,2,packet);Writer.WriteUInt32(target,4,packet);packet[8]=(byte)clones.Length;
        for(int i=0;i<clones.Length;i++)Writer.WriteUInt32(clones[i],9+i*4,packet);Writer.WriteUInt64(PacketHandler.ClientSeal,packet.Length-8,packet);return packet;
    }
    static void VerifyNinjaRebirth(string runtime,Map town) {
        VerifyPaidReincarnation(runtime,town);NinjaInstructorMap(runtime);
        foreach(int rite in new[]{0,1,2})foreach(bool keep in new[]{false,true})foreach(bool fail in new[]{false,true}) {
            var p=rite==0?RebirthPlayer(town,55):rite==1?SecondPlayer(town,55,55):PaidPlayer(town,51);if(rite==2)p.Entity.Class=55;
            LearnNinjaArts(p);Check(NinjaReceipts(p)==7,"ordinary Ninja learns the seven reviewed arts before rite "+rite);
            p.Entity.Teleport(1002,town.Npcs[59558].X,town.Npcs[59558].Y);p.ActiveNpc=59558;EntityTable.SaveEntity(p);
            var clones=NinjaClones(p);p.Entity.CounterKillSwitch=true;
            foreach(var flag in new[]{FlagsUpdate.Flags.FatalStrike,FlagsUpdate.Flags.ShurikenVortex})p.Entity.AddFlag((ulong)flag,StatusFlagsBigVector32.PermanentFlag,false);
            byte next=keep?(byte)51:(byte)21;if(rite==0)RebirthOffer(p,next);else if(rite==1)SecondOffer(p,next);else PaidOffer(p);
            if(fail)Query("CREATE FUNCTION reject_ninja_rite() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_ninja_rite BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_ninja_rite()");
            try{if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,next);}finally{if(fail)Query("DROP TRIGGER reject_ninja_rite ON entities;DROP FUNCTION reject_ninja_rite()");}
            if(fail) {
                Check(Fenced(p) && ninjaNewArts.All(id=>p.Spells.ContainsKey(id) && p.Spells[id].Experience==51) && p.Entity.MyClones.Count==2 && clones.All(CloneRegistered)
                    && p.Entity.CounterKillSwitch && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike),"failed Ninja rite preserves learned arts, summons and active modes: "+rite);
            } else {
                Check(!Fenced(p) && p.Entity.Class==next && p.Entity.MyClones.Count==0 && clones.All(c=>!CloneRegistered(c)) && !p.Entity.CounterKillSwitch
                    && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex),"committed Ninja rite clears only completed-life summons and active modes: "+rite);
                var before=p.Entity.AttackPacket;PacketHandler.HandlePacket(CloneWire(1,clones[0].UID,clones[1].UID),p);Entity.AddClone(p,p.Entity.Name,3);
                Check(p.Entity.MyClones.Count==0 && ReferenceEquals(p.Entity.AttackPacket,before),"removed Shadow Clone cannot be recast or replayed after rite "+rite);
                p=ReloadBall(p);
                Check(new ushort[]{12070,12090,12110}.All(id=>!p.Spells.ContainsKey(id)) && p.Spells.ContainsKey(12080) && p.Spells[12080].Level==0 && p.Spells[12080].PreviousLevel==3
                    && new ushort[]{11170,11180,11230}.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].PreviousLevel==3:!p.Spells.ContainsKey(id)),
                    "Ninja rite reload applies epic exceptions and destination-specific scythe arts: "+rite+" ninja="+keep);
                if(keep){p.Entity.Class=55;p.Entity.Level=130;p.Entity.Atributes+=345;p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();EntityTable.SaveEntity(p);
                    LearnNinjaArts(p);Check(NinjaReceipts(p)==10,"removed epic arts can be relearned from three newly paid scrolls after rite "+rite);}
            }
        }
        VerifyNinjaClonePackets(town);
    }
    static void VerifyNinjaClonePackets(Map town) {
        var p=RebirthPlayer(town,55);RebirthSkill(p,12090);var clones=NinjaClones(p);var original=new Attack(true){Attacker=p.Entity.UID,Attacked=123,AttackType=Attack.Melee};p.Entity.AttackPacket=original;
        foreach(string mode in new[]{"valid","empty","count","truncated","duplicate","foreign","unregistered","missing-skill"}) {
            var packet=CloneWire(1,clones[0].UID,clones[1].UID);
            if(mode=="empty")packet=CloneWire(1);if(mode=="count")packet[8]=255;if(mode=="truncated"){packet=CloneWire(1,clones[0].UID);packet[8]=2;}
            if(mode=="duplicate")packet=CloneWire(1,clones[0].UID,clones[0].UID);if(mode=="foreign")packet=CloneWire(1,clones[0].UID,999);
            if(mode=="unregistered")p.Map.RemoveEntity(clones[0]);if(mode=="missing-skill")p.Spells.Remove(12090);
            PacketHandler.HandlePacket(packet,p);
            Check(original.Attacker==p.Entity.UID && original.Attacked==123 && p.Entity.MyClones.Count==2,"clone frame cannot mutate the owner's queued attack: "+mode);
            if(mode=="unregistered")p.Map.AddEntity(clones[0]);if(mode=="missing-skill")RebirthSkill(p,12090);
        }
        var target=LootMonster(p);target.Hitpoints=target.MaxHitpoints=100000;p.Screen.Add(target);p.Entity.MinAttack=100;p.Entity.MaxAttack=101;
        PacketHandler.HandlePacket(CloneWire(target.UID,clones[0].UID,clones[0].UID),p);
        PacketHandler.HandlePacket(CloneWire(target.UID,clones[0].UID,999),p);
        Check(target.Hitpoints==100000,"duplicate or partly foreign clone lists cannot deliver even a partial attack");
        PacketHandler.HandlePacket(CloneWire(target.UID,clones[0].UID,clones[1].UID),p);
        Check(target.Hitpoints<100000 && original.Attacker==p.Entity.UID && original.Attacked==123,"canonical learned clones still attack through actual packet dispatch without corrupting the queued owner attack");
        uint hp=target.Hitpoints;p.Spells.Remove(12090);PacketHandler.HandlePacket(CloneWire(target.UID,clones[0].UID,clones[1].UID),p);
        Check(target.Hitpoints==hp,"a stale clone attack cannot deal damage after its learned skill is removed");RebirthSkill(p,12090);
        var other=RebirthPlayer(town,55);RebirthSkill(other,12090);var foreign=NinjaClones(other);p.Entity.MyClones[foreign[0].UID]=foreign[0];
        var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");lock(p.ItemSyncRoot)Call(rules,null,"ClearNinjaModes",p);
        Check(p.Entity.MyClones.Count==0 && clones.All(c=>!CloneRegistered(c)) && foreign.All(CloneRegistered) && other.Entity.MyClones.Count==2,"cleanup preserves another player's summons even if a stale foreign reference exists");
        var clean=RebirthPlayer(town,55);var plan=(Spell[])Call(rules,null,"Plan",clean,(byte)51);
        Check(!plan.Any(s=>ninjaNewArts.Contains(s.ID)),"rebirth retention never grants unlearned Ninja arts");
    }
}
