using System;
using System.Reflection;
using Revora.Client;
using Revora.Game;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Sistemas.Quarantine;
internal static class HitEligibilityTests {
    static int checks;
    static readonly MethodInfo eligible=typeof(Q).GetMethod("EligibleHit",BindingFlags.Static|BindingFlags.NonPublic);
    static GameState Player(uint uid,uint garment) {
        var p=new GameState(null);p.Equipment=new Revora.Game.ConquerStructures.Equipment(p);p.Inventory=new Revora.Game.ConquerStructures.Inventory(p);
        p.Entity=new Entity(EntityFlag.Player,false);p.Entity.SendUpdates=false;p.Entity.UID=uid;
        p.Entity.Hitpoints=100;p.Entity.Owner=p;
        typeof(Entity).GetField("_mapid",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Entity,Q.Map);
        p.Equipment.Objects[8]=new ConquerItem(true){ID=garment};return p;
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static bool Eligible(Entity a,Entity b,SpellInformation s){return (bool)eligible.Invoke(null,new object[]{a,b,s});}
    static void Reject(Entity a,Entity b,SpellInformation s,string name){
        Check(!Eligible(a,b,s),name);int w=Q.WhiteScore,k=Q.BlackScore;
        Check(!Q.HandleQuarantine(a,b,s) && Q.WhiteScore==w && Q.BlackScore==k,name+" no score");
    }
    static int Main(){
        var a=Player(910001,181325);var b=Player(910002,181525);var spell=new SpellInformation{ID=1045};
        Q.Started=true;Q.White.Add(a.Entity.UID,a);Q.Black.Add(b.Entity.UID,b);
        foreach(ushort id in Q.AllowedSpells){spell.ID=id;Check(Eligible(a.Entity,b.Entity,spell),"white legal spell");Check(Eligible(b.Entity,a.Entity,spell),"black legal spell");}
        Reject(null,b.Entity,spell,"null attacker");Reject(a.Entity,null,spell,"null target");Reject(a.Entity,b.Entity,null,"no spell");
        spell.ID=1000;Reject(a.Entity,b.Entity,spell,"forbidden spell");spell.ID=1045;
        Q.Started=false;Reject(a.Entity,b.Entity,spell,"closed event");Q.Started=true;
        Reject(a.Entity,a.Entity,spell,"self hit");
        var map=typeof(Entity).GetField("_mapid",BindingFlags.NonPublic|BindingFlags.Instance);
        map.SetValue(b.Entity,(ushort)1002);Reject(a.Entity,b.Entity,spell,"target outside");map.SetValue(b.Entity,Q.Map);
        map.SetValue(a.Entity,(ushort)1002);Reject(a.Entity,b.Entity,spell,"attacker outside");map.SetValue(a.Entity,Q.Map);
        var garment=a.Equipment.Objects[8];a.Equipment.Objects[8]=null;Reject(a.Entity,b.Entity,spell,"missing garment");a.Equipment.Objects[8]=garment;
        garment.ID=281325;Reject(a.Entity,b.Entity,spell,"same suffix wrong item");garment.ID=181325;
        Q.Black.Remove(b.Entity.UID);Reject(a.Entity,b.Entity,spell,"unregistered target");Q.White.Add(b.Entity.UID,b);Reject(a.Entity,b.Entity,spell,"teammate");Q.White.Remove(b.Entity.UID);Q.Black.Add(b.Entity.UID,b);
        Q.Black.Add(a.Entity.UID,a);Reject(a.Entity,b.Entity,spell,"dual team");Q.Black.Remove(a.Entity.UID);
        Q.Black.Remove(b.Entity.UID);Q.Black.Add(b.Entity.UID,Player(b.Entity.UID,181525));Reject(a.Entity,b.Entity,spell,"stale session");
        Check(a.quarantineKill==0 && b.quarantineDeath==0,"invalid hits do not alter player counters");
        var newcomer=Player(910003,187405);newcomer.Equipment.Objects[2]=new ConquerItem(true){ID=130009};
        map.SetValue(newcomer.Entity,(ushort)1002);
        var original=newcomer.Equipment.Objects[8];var mode=newcomer.Entity.PKMode;
        var bag=(System.Collections.Generic.Dictionary<uint,ConquerItem>)typeof(Revora.Game.ConquerStructures.Inventory).GetField("inventory",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(newcomer.Inventory);
        for(uint i=1;i<=40;i++)bag.Add(i,new ConquerItem(true){UID=i,ID=1088000});
        typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(newcomer.Inventory,new System.Collections.Generic.List<ConquerItem>(bag.Values).ToArray());
        Check(newcomer.Inventory.Count==40,"full inventory fixture");
        Q.Join(newcomer);
        Check(ReferenceEquals(original,newcomer.Equipment.Objects[8]),"full bag preserves exact equipped garment");
        Check(newcomer.Entity.MapID==1002 && newcomer.Entity.PKMode==mode,"full bag preserves location and PK mode");
        Check(!Q.White.ContainsKey(newcomer.Entity.UID) && !Q.Black.ContainsKey(newcomer.Entity.UID) && bag.Count==40,"full bag preserves roster and items");
        var registered=a.Equipment.Objects[8];Q.Join(a);
        Check(ReferenceEquals(registered,a.Equipment.Objects[8]) && Q.White[a.Entity.UID]==a && !Q.Black.ContainsKey(a.Entity.UID),"duplicate join preserves team and uniform");
        var remember=typeof(Q).GetMethod("RememberUniform",BindingFlags.NonPublic|BindingFlags.Static);
        var remove=typeof(Q).GetMethod("RemoveUniform",BindingFlags.NonPublic|BindingFlags.Static);
        var uniform=new ConquerItem(true){UID=880011,ID=181325,Position=9};
        newcomer.Equipment.Objects[8]=uniform;remember.Invoke(null,new object[]{newcomer,uniform});
        Check((bool)remove.Invoke(null,new object[]{newcomer}) && newcomer.Equipment.Objects[8]==null && newcomer.Inventory.Count==40,"owned uniform removed with full bag without granting an item");
        Check(!(bool)remove.Invoke(null,new object[]{newcomer}) && newcomer.Inventory.Count==40,"uniform cleanup replay inert");
        newcomer.Equipment.Objects[8]=original;remember.Invoke(null,new object[]{newcomer,uniform});
        Check(!(bool)remove.Invoke(null,new object[]{newcomer}) && ReferenceEquals(newcomer.Equipment.Objects[8],original),"replacement garment preserved");
        remember.Invoke(null,new object[]{newcomer,uniform});
        var foreign=Player(newcomer.Entity.UID,181325);
        Check(!(bool)remove.Invoke(null,new object[]{foreign}) && foreign.Equipment.Objects[8]!=null,"stale-session cleanup cannot touch replacement owner");
        bag.Clear();bag.Add(uniform.UID,uniform);typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(newcomer.Inventory,new[]{uniform});
        Check(!(bool)remove.Invoke(null,new object[]{newcomer}) && newcomer.Inventory.Count==0 && ReferenceEquals(newcomer.Equipment.Objects[8],original),"unequipped owned uniform removed without touching worn replacement");
        Revora.Server.GamePool=new GameState[0];
        var gate=typeof(Q).GetField("lifecycleOwner",BindingFlags.Static|BindingFlags.NonPublic);
        var oldWhite=Q.White;var oldBlack=Q.Black;Q.WhiteScore=12;Q.BlackScore=8;
        gate.SetValue(null,1);
        try {
            Q.End();Q.Start();Q.Join(newcomer);Q.Check(newcomer);
            Check(!Q.HandleQuarantine(a.Entity,b.Entity,spell),"busy lifecycle refuses combat");
            Check(Q.Started && Q.WhiteScore==12 && Q.BlackScore==8 && ReferenceEquals(oldWhite,Q.White) && ReferenceEquals(oldBlack,Q.Black),"busy lifecycle preserves active state");
        } finally {gate.SetValue(null,0);}
        Q.End();Check(!Q.Started && Q.WhiteScore==0 && Q.BlackScore==0 && Q.White.Count==0 && Q.Black.Count==0,"owned end resets empty runtime");
        System.Threading.Tasks.Parallel.For(0,32,i=>Q.Start());
        Check(Q.Started && (int)gate.GetValue(null)==0,"concurrent start releases ownership");
        oldWhite=Q.White;oldBlack=Q.Black;Q.WhiteScore=3;
        System.Threading.Tasks.Parallel.For(0,32,i=>Q.Start());
        Check(Q.WhiteScore==3 && ReferenceEquals(oldWhite,Q.White) && ReferenceEquals(oldBlack,Q.Black),"repeated concurrent start preserves round");
        System.Threading.Tasks.Parallel.For(0,32,i=>Q.End());
        Check(!Q.Started && Q.WhiteScore==0 && (int)gate.GetValue(null)==0,"concurrent end releases ownership");
        Q.Start();Q.End();Check(!Q.Started && (int)gate.GetValue(null)==0,"later lifecycle remains available");
        Check(!Revora.Game.Features.TwinCityPlaza.IsClosed(Q.NpcId),"restored tournament desk is open");
        bool parked=false;Revora.Game.Features.TwinCityPlaza.Apply((id,m,x,y)=>{if(id==Q.NpcId)parked=true;});
        Check(!parked,"plaza cannot park the restored tournament clerk");
        a.ActiveNpc=Q.NpcId;var beforeCount=Q.White.Count+Q.Black.Count;
        Check(Q.HandleNpc(a,new NpcRequest(true){OptionID=1}) && Q.White.Count+Q.Black.Count==beforeCount,"noncanonical remote NPC request cannot join");
        Console.WriteLine("Quarantine eligibility/rejection checks passed: "+checks);return 0;
    }
}
