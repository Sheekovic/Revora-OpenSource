using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;

class ArenaBotChecks
{
    static int checks;
    static Type Feature(string n){return typeof(Entity).Assembly.GetType("Revora.Game.Features."+n,true);}
    static object Call(string t,string method,params object[] args)
    {
        try{return Feature(t).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
        catch(TargetInvocationException e){System.Console.WriteLine(t+"."+method+": "+e.InnerException);throw e.InnerException;}
    }
    static void Check(bool result,string message){if(!result)throw new Exception(message);checks++;}
    static uint Varint(byte[] packet,ref int at) {
        uint value=0;int shift=0;byte part;
        do {part=packet[at++];value|=(uint)(part&127)<<shift;shift+=7;}while((part&128)!=0);
        return value;
    }
    static uint ProtoValue(byte[] packet,uint field) {
        int at=4,end=System.BitConverter.ToUInt16(packet,0);
        while(at<end) {uint key=Varint(packet,ref at);if((key&7)==2){int size=(int)Varint(packet,ref at);at+=size;continue;}
            uint value=Varint(packet,ref at);if(key>>3==field)return value;}
        return uint.MaxValue;
    }
    static GameState Actor(uint uid)
    {
        var p=new GameState(null);
        p.Entity=new Entity(EntityFlag.Player,false){Owner=p,UID=uid,Name="fixture"};
        p.ArenaStatistic=new QualifierStatistic(true){EntityID=uid};
        p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=uid};
        p.Equipment=new Equipment(p);p.Inventory=new Inventory(p);
        p.Screen=(Screen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Screen));
        GC.SuppressFinalize(p.Screen);
        p.Screen.Owner=p;
        var dict=typeof(Screen).GetField("_objectDictionary",BindingFlags.Instance|BindingFlags.NonPublic);
        dict.SetValue(p.Screen,Activator.CreateInstance(dict.FieldType));
        typeof(Screen).GetField("_objects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p.Screen,new Revora.Interfaces.IMapObject[0]);
        typeof(Screen).GetField("DisposalSyncRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p.Screen,new object());
        p.Entity.MaxHitpoints=1000;p.Entity.Hitpoints=1000;
        p.Entity.FullyLoaded=true;p.Action=2;return p;
    }
    static void Main(string[] args)
    {
        for(int count=-1;count<=15;count++)foreach(int wait in new[]{-1,0,4999,5000,60000})
            Check((bool)Call("ArenaBotPolicy","ShouldFill",count,wait)==(count>0 && count<10 && wait>=5000),"signup cutoff/wait");
        foreach(byte cls in new byte[]{15,25,45,55,65,75,85,135,145,165})
        {
            var profile=Call("ArenaBotPolicy","For",cls,false,false);
            var attacks=(ushort[])profile.GetType().GetField("Attacks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(profile);
            Check(attacks.Length>0 && !attacks.Contains((ushort)1025),"all classes have non-Superman attacks");
        }
        Check((int)Call("ArenaBotPolicy","MovementScore",4,4,false,true,0)==int.MinValue,"blocked movement cannot win");
        Check((int)Call("ArenaBotPolicy","MovementScore",8,8,false,false,0)>(int)Call("ArenaBotPolicy","MovementScore",2,8,false,false,0),"ranged spacing");
        Check((int)Call("ArenaBotPolicy","MovementScore",3,3,false,false,0)>(int)Call("ArenaBotPolicy","MovementScore",3,3,true,false,0),"avoid threatened line");
        var roster=new System.Collections.Generic.HashSet<uint>();
        for(int cursor=0;cursor<9;cursor++) {
            uint next=(uint)Call("ArenaBotPolicy","SelectRoster",cursor,new Func<uint,bool>(id=>true));
            Check(roster.Add(next),"rotation visits each roster identity before repeating");
        }
        Check((uint)Call("ArenaBotPolicy","SelectRoster",0,new Func<uint,bool>(id=>id==1300003))==1300003,"occupied roster entries are skipped");
        Check((uint)Call("ArenaBotPolicy","SelectRoster",0,new Func<uint,bool>(id=>false))==0,"full roster cannot create an extra actor");
        Check((bool)Call("ArenaBotPolicy","HoldAttackPosition",1,1,false,true),"melee holds a hittable tile");
        Check(!(bool)Call("ArenaBotPolicy","HoldAttackPosition",3,1,false,true),"melee closes from outside range");
        Check(!(bool)Call("ArenaBotPolicy","HoldAttackPosition",1,1,false,false),"blocked sight requires repositioning");
        var restricted=new Revora.Database.SpellInformation{ID=1045,OnlyWithThisWeaponSubtype=new System.Collections.Generic.List<ushort>{410}};
        Check((bool)Call("ArenaBotPolicy","WeaponAllows",restricted,410339u,0u),"learned blade skill accepts blade");
        Check(!(bool)Call("ArenaBotPolicy","WeaponAllows",restricted,601439u,601439u),"illegal weapon skill cannot consume AI action slot");
        var observer=Actor(1300008);var peer=Actor(1111112);
        observer.Entity.MapID=700;peer.Entity.MapID=700;
        observer.Entity.X=50;observer.Entity.Y=50;peer.Entity.X=55;peer.Entity.Y=50;
        Entity seen;
        Check(!observer.Screen.TryGetValue(peer.Entity.UID,out seen),"newly approached opponent starts absent from screen");
        Check((bool)Call("ArenaBotBrain","RefreshTarget",observer,peer) && observer.Screen.TryGetValue(peer.Entity.UID,out seen),"bot movement refreshes combat target lookup");
        peer.Entity.MapID=1002;
        Check(!(bool)Call("ArenaBotBrain","RefreshTarget",observer,peer),"other-map opponent cannot become a combat target");
        Check(!(bool)Call("ArenaBotBrain","ObserveCast",observer,(ushort)6000,new Action(()=>{})),"rejected handler does not consume successful attack turn");
        Check((bool)Call("ArenaBotBrain","ObserveCast",observer,(ushort)6000,new Action(()=>Call("ArenaBotBrain","Prepared",observer,(ushort)6000))),"prepared cast consumes attack turn");
        Check(!(bool)Call("ArenaBotBrain","ObserveCast",observer,(ushort)6000,new Action(()=>Call("ArenaBotBrain","Prepared",peer,(ushort)6000))),"another actor cannot acknowledge a cast");
        Check(!(bool)Call("ArenaBotBrain","ObserveCast",observer,(ushort)6000,new Action(()=>Call("ArenaBotBrain","Prepared",observer,(ushort)6001))),"another spell cannot acknowledge a cast");
        foreach(byte own in new byte[]{15,25,45,55,65,75,85,145,165}) {
            var recent=new System.Collections.Generic.List<byte>();
            for(int match=0;match<24;match++) {
                byte selected=(byte)Call("ArenaBotSnapshot","ChooseFreshClass",own,match*17,recent);
                Check(selected/10!=own/10 && !recent.Skip(Math.Max(0,recent.Count-7)).Contains(selected),"class rotation excludes own class and last seven opponents");
                recent.Add(selected);
            }
            if(own!=55)Check(recent.Take(8).Contains((byte)55),"Ninja appears within eight eligible class offers");
        }
        var source=Actor(1111111);source.Entity.FullyLoaded=false;
        foreach(byte cls in new byte[]{15,25,45,55,65,75,85,135,145,165})
            for(int pick=0;pick<9;pick++)Check((byte)Call("ArenaBotSnapshot","ChooseClass",cls,pick)!=cls,"never mirror opponent class");
        var close=new Qualifier.ArenaSignup{DialogID=Qualifier.ArenaSignup.MainIDs.Match,OptionID=Qualifier.ArenaSignup.DialogButton.MatchOff}.BuildPacket();
        Check(System.BitConverter.ToUInt16(close,2)==2205 && System.BitConverter.ToUInt32(close,4)==6 && System.BitConverter.ToUInt32(close,8)==3,"Arena close action and option");
        foreach(bool won in new[]{false,true}) {
            var ended=(byte[])typeof(Qualifier.ArenaSignup).GetMethod("EndMatchPacket",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{new QualifierStatistic(true){EntityID=1111111,Name="fixture"},won});
            Check(System.BitConverter.ToUInt16(ended,2)==2205 && System.BitConverter.ToUInt32(ended,4)==10
                && System.BitConverter.ToUInt32(ended,8)==(won?1u:0u),"terminal result uses legacy action 10 for both outcomes");
        }
        var target=Actor(1300001);target.Entity.FullyLoaded=false;
        var body=(Entity)Call("ArenaBotSnapshot","CreateBody",target,1300001u);
        var inspected=new _String(true){UID=body.UID,Type=16,Texts=new System.Collections.Generic.List<string>{body.Spouse}};
        Check(inspected.ToArray().Length>0 && body.Spouse=="None","bot equipment inspection has a serializable spouse string");
        source.Entity.FullyLoaded=target.Entity.FullyLoaded=true;
        Check(!(bool)Call("ArenaActors","Player",target,true),"unregistered fake cannot participate");
        Check((bool)Call("ArenaActors","Install",target),"reserved canonical bot installs");
        var timerType=typeof(Entity).Assembly.GetType("Revora.Thread",true);
        var timer=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(timerType);
        GC.SuppressFinalize(timer);
        var validTimer=timerType.GetMethod("Valid",BindingFlags.Instance|BindingFlags.NonPublic);
        Check((bool)validTimer.Invoke(timer,new object[]{target}),"registered bot can run owned timers without a socket");
        Check(!(bool)validTimer.Invoke(timer,new object[]{source}),"unregistered fake cannot run player timers");
        var spawn=(byte[])typeof(Entity).GetMethod("ToReviewed6711PlayerSpawn",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target.Entity,null);
        Check(ProtoValue(spawn,72)==target.Entity.Class,"registered bot spawn includes its current class");
        Check(ProtoValue(spawn,70)==target.Entity.FirstRebornClass && ProtoValue(spawn,71)==target.Entity.SecondRebornClass,"spawn includes rebirth class identity");
        Check((bool)Call("ArenaActors","Player",target,true),"registered bot accepted only by Arena");
        Check(!(bool)Call("QuestInventory","Player",target,true),"bot cannot use authenticated player services");
        Check(!(bool)Call("ArenaActors","Install",source),"nonreserved fake rejected");
        var impostor=Actor(target.Entity.UID);
        Check(!(bool)Call("ArenaActors","Install",impostor),"same UID cannot replace bot");
        Kernel.GamePool[target.Entity.UID]=impostor;
        Check(!(bool)Call("ArenaActors","Player",target,true) && !(bool)Call("ArenaActors","Player",impostor,true),"stale and replacement sessions rejected");
        Kernel.GamePool[target.Entity.UID]=target;
        typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target.Entity,true);
        Check(!(bool)Call("ArenaActors","Player",target,false),"fenced bot cannot continue combat");
        typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target.Entity,false);
        target.Entity.Hitpoints=0;
        Check(!(bool)Call("ArenaActors","Player",target,true) && (bool)Call("ArenaActors","Player",target,false),"dead bot eligible only for result handling");
        Call("ArenaActors","Remove",target);
        Check(!(bool)validTimer.Invoke(timer,new object[]{target}),"retired bot timers become ineligible");
        Check(!Kernel.GamePool.ContainsKey(target.Entity.UID),"retired bot leaves session pool");
        Check(!(bool)Call("ArenaActors","Player",target,false),"retired bot cannot earn another result");
        if(args.Length>0) {
            System.IO.Directory.SetCurrentDirectory(args[0]);
            Revora.Database.ConquerItemInformation.Load();Revora.Database.SoulProtection.Load();
            Revora.Database.SpellTable.Load();Revora.Database.JiangHu.LoadStatus();PerfectionManager.Load();
            foreach(byte cls in new byte[]{15,25,45,55,65,75,85,145,165}) {
                string previous=null;
                for(int seed=1;seed<=3;seed++) {
                    var bot=Actor(1300001);bot.Entity.FullyLoaded=false;bot.Account=new Revora.Database.AccountTable(null);
                    bot.Spells=new System.Collections.Generic.SafeDictionary<ushort,Revora.Interfaces.ISkill>();
                    bot.Proficiencies=new System.Collections.Generic.SafeDictionary<ushort,Revora.Interfaces.IProf>();
                    Call("ArenaBotSnapshot","BuildProfile",bot,cls,(byte)140,seed);
                    Check(bot.Entity.Mesh==371006,"supported genuine 6711 world model");
                    Check(bot.Equipment.TryGetItem(18)!=null && bot.Equipment.TryGetItem(19)!=null,"complete crop and wing equipment slots");
                    Check(bot.Equipment.GetItems().Where(i=>!ItemHandler.IsArrow(i.ID)).All(i=>i.Plus==12 && i.PerfectionLevel>=48),"endgame default never rolls low-tier enhancement/perfection");
                    Check(bot.Entity.Class==cls && bot.Entity.NobilityRank==NobilityRank.Serf,"independent class and nobility");
                    Check(bot.Entity.MaxHitpoints>0 && (cls==145?bot.Entity.MagicAttack:bot.Entity.MaxAttack)>0,"normal stat pipeline builds viable combat stats");
                    Check(bot.ChiPowers.Count==4 && bot.ChiPowers.All(c=>c.Attributes.Select(a=>a.Type).Distinct().Count()==4 && c.Points<400),"independent legal Chi rolls");
                    Check(bot.Entity.MyJiang!=null && !Revora.Game.JiangHu.JiangHuClients.ContainsKey(bot.Entity.UID),"Jiang profile never registers player persistence or rankings");
                    Check(bot.Spells.Count>0,"class has learned combat skills");
                    if(cls==45) {
                        Check(bot.Weapons.Item1.ID/1000==613 && bot.Weapons.Item2.ID/1000==613 && !bot.Entity.IsBowEquipped,"Assassin has two throwing knives and no bow");
                        Check(new ushort[]{11620,11650,11660,11670}.All(id=>bot.Spells.ContainsKey(id)) && !bot.Spells.ContainsKey(8000),"Assassin learns stance and attacks instead of bow skills");
                    }
                    Check(bot.Spells.Values.All(skill=>Revora.Database.SpellTable.GetSpell(skill.ID,skill.Level).NeedXP!=1),"XP-only skills excluded, ordinary NeedXP=2 skills allowed");
                    if(cls==15)Check(bot.Spells.ContainsKey(1045),"FastBlade is not blocked by its CP training price");
                    foreach(var gear in bot.Equipment.GetItems().Where(i=>i!=null)) {
                        Check(gear.UID>=0x70000000u && gear.PerfectionOwnerGuid==0,"synthetic equipment identity");
                        if(gear.Purification.PurificationItemID!=0)Check((bool)Call("ArtifactApplication","Fits",gear.ID,gear.Purification.PurificationItemID),"generated soul is legal for equipment");
                    }
                    if(seed==1) {
                        foreach(var item in bot.Equipment.GetItems().Where(i=>!ItemHandler.IsArrow(i.ID))) {item.Plus=12;item.PerfectionLevel=54;}
                        var tier=Call("ArenaBotSnapshot","MeasureTier",bot);
                        Check((int)tier.GetType().GetField("Plus",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tier)==12,"full +12 measured as +12 tier");
                        Check((int)tier.GetType().GetField("Perfection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tier)==54,"max perfection measured without copying slots");
                        var other=Actor(1300002);other.Entity.FullyLoaded=false;other.Account=new Revora.Database.AccountTable(null);
                        other.Spells=new System.Collections.Generic.SafeDictionary<ushort,Revora.Interfaces.ISkill>();
                        other.Proficiencies=new System.Collections.Generic.SafeDictionary<ushort,Revora.Interfaces.IProf>();
                        Call("ArenaBotSnapshot","BuildTieredProfile",other,cls,(byte)140,91,tier);
                        Check(other.Equipment.GetItems().Where(i=>!ItemHandler.IsArrow(i.ID)).All(i=>i.Plus==12 && i.PerfectionLevel>=51),"matched max-tier equipment stays within three perfection levels");
                        Check(other.Equipment.GetItems().Any(i=>i.PerfectionLevel!=54 && !ItemHandler.IsArrow(i.ID)),"max-tier build has varied perfection rather than copied levels");
                        Check(!ReferenceEquals(other.Entity.MyJiang,bot.Entity.MyJiang) && !ReferenceEquals(other.ChiPowers,bot.ChiPowers),"cultivation profiles remain independent");
                    }
                    bot.Entity.Mana=bot.Entity.MaxMana;bot.Entity.Stamina=100;
                    var brainType=Feature("ArenaBotBrain");
                    var brain=Activator.CreateInstance(brainType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{bot},null);
                    var policy=Call("ArenaBotPolicy","For",cls,bot.Entity.Assassin(),true);
                    foreach(var learned in bot.Spells.Values)learned.ColdTime=Time32.Now.AddSeconds(60);
                    int fallback=(int)brainType.GetMethod("PreferredRange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(brain,new object[]{policy,DateTime.UtcNow});
                    Check(fallback==((bot.Entity.IsBowEquipped || bot.Entity.Assassin())?9:Math.Max(1,(int)bot.Entity.AttackRange)),"unavailable spells use actual basic attack range");
                    string fingerprint=string.Join(";",bot.Equipment.GetItems().Where(i=>i!=null).Select(i=>i.ID+":"+i.Plus+":"+i.PerfectionLevel))
                        +"|"+string.Join(";",bot.ChiPowers.SelectMany(c=>c.Attributes).Select(a=>((int)a).ToString()));
                    Check(fingerprint!=previous,"different seeds give different independent builds");previous=fingerprint;
                    System.Console.WriteLine("profile class="+cls+" seed="+seed+" hp="+bot.Entity.MaxHitpoints+" atk="+bot.Entity.MaxAttack+" magic="+bot.Entity.MagicAttack);
                }
            }
        }
        System.Console.WriteLine(checks+" Arena bot checks passed; no database access or player changes.");
    }
}
