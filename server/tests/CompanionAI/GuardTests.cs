using System;
using System.Collections;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using System.Collections.Generic;
using Console=System.Console;
internal static class GuardTests
{
    static int checks;
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Type Guards=typeof(CityGuardSystem);
    static object Call(string name,params object[] args){return Guards.GetMethod(name,Hidden).Invoke(null,args);}
    static void Set(object obj,string field,object value){obj.GetType().GetField(field).SetValue(obj,value);}
    static T Get<T>(object obj,string field){return (T)obj.GetType().GetField(field).GetValue(obj);}
    static object Fixture(string name,params object[] args){return typeof(CompanionTests).GetMethod(name,Hidden).Invoke(null,args);}
    static GameState Player(Map map,bool fake,ushort x,ushort y){return (GameState)typeof(RetaliationTests).GetMethod("Player",Hidden).Invoke(null,new object[]{map,fake,x,y,(byte)15});}
    static void EmptyCollections(object owner)
    {
        foreach(var field in owner.GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(field.GetValue(owner)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(owner,Activator.CreateInstance(field.FieldType));
    }
    static object Register(GameState p,bool elite)
    {
        var unit=Activator.CreateInstance(Guards.GetNestedType("GuardUnit",BindingFlags.NonPublic),true);
        Set(unit,"State",p);Set(unit,"Elite",elite);Set(unit,"MapId",p.Entity.MapID);Set(unit,"PostX",p.Entity.X);Set(unit,"PostY",p.Entity.Y);Set(unit,"CombatRange",(short)2);
        ((IDictionary)Guards.GetField("ByUid",Hidden).GetValue(null)).Add(p.Entity.UID,unit);
        ((IList)Guards.GetField("Units",Hidden).GetValue(null)).Add(unit);
        return unit;
    }
    static object Incident(uint uid){return ((IDictionary)Guards.GetField("Incidents",Hidden).GetValue(null))[uid];}
    static void Check(bool okay,string name){if(!okay)throw new Exception("FAILED: "+name);checks++;Console.WriteLine("PASS: "+name);}
    public static int Main(string[] args)
    {
        try {
            CompanionTests.LoadBackfire(args[0]);
            Fixture("Reset");var map=(Map)Fixture("World",(ushort)1002);
            var p=Player(map,false,30,20);var regular=Player(map,true,20,20);var unit=Register(regular,false);
            regular.Entity.MaxHitpoints=90000;regular.Entity.Hitpoints=45000;
            Check((bool)Call("RestoreCombatProfile",regular.Entity) && regular.Entity.MaxHitpoints==1000000,"regular guards retain at least one million HP after equipment stats");
            Check(regular.Entity.Defence>=16000 && regular.Entity.MagicDefence>=8000 && regular.Entity.MagicDefencePercent>=40,"regular guards have physical and magical defenses");
            Check(regular.Entity.Immunity>=6000 && regular.Entity.Counteraction>=600 && regular.Entity.Block>=2500,"regular guards resist criticals, breakthrough and physical attacks");
            Call("RestoreCombatProfile",regular.Entity);Check(regular.Entity.MaxHitpoints==1000000 && regular.Entity.Hitpoints==45000,"reapplying guard floors does not stack bonuses or heal damage");
            p.Entity.MaxHitpoints=10000;Check(!(bool)Call("RestoreCombatProfile",p.Entity) && p.Entity.MaxHitpoints==10000,"ordinary players receive no guard bonuses");
            regular.RunesItem=new SafeDictionary<uint,ConquerItem>();regular.ChiData=new ChiTable.ChiData{Powers=regular.ChiPowers};
            regular.LoadItemStats();Check(regular.Entity.MaxHitpoints>=1000000 && regular.Entity.Hitpoints==45000,"actual stat recalculation preserves guard durability and current wounds");
            regular.LoadItemStats();Check(regular.Entity.MaxHitpoints==1000000 && regular.Entity.Hitpoints==45000,"repeated recalculation stays idempotent");
            uint hit=100;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,regular.Entity,new Attack(true),ref hit,null);
            var incident=Incident(p.Entity.UID);
            Check(incident!=null && regular.Entity.Hitpoints==44900 && p.Entity.MapID==1002,"accepted combat hit damages the officer and starts a street incident without teleporting");
            DateTime warning=Get<DateTime?>(incident,"WarningUntil").Value;
            hit=100;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,regular.Entity,new Attack(true),ref hit,null);
            Check(Get<DateTime?>(incident,"WarningUntil").Value==warning && regular.Entity.Hitpoints==44800,"repeated hits cannot restart the five-second warning");
            Check(Get<DateTime?>(incident,"WarningUntil").Value-Get<DateTime>(incident,"Started")>=TimeSpan.FromSeconds(4.99),"blue-name warning lasts five seconds");
            Check(!CityGuardSystem.IsGuardTarget(p,regular.Entity),"guard waits during the warning");
            Set(incident,"WarningUntil",(DateTime?)DateTime.Now.AddMilliseconds(-1));
            Check(CityGuardSystem.IsGuardTarget(p,regular.Entity),"street guard acquires the offender when five seconds expire");
            ushort oldX=regular.Entity.X;CityGuardSystem.Tick();
            Check(regular.Entity.X>oldX && Get<uint>(unit,"Sight")==p.Entity.UID,"the real guard driver pursues the warned player");
            var second=Player(map,true,29,21);Register(second,false);
            Check(CityGuardSystem.IsGuardTarget(p,second.Entity),"second nearby guard can join pursuit");
            var third=Player(map,true,29,22);Register(third,false);
            Check(!CityGuardSystem.IsGuardTarget(p,third.Entity),"at most two guards pursue one offender");
            second.Entity.Hitpoints=0;
            Check(CityGuardSystem.IsGuardTarget(p,third.Entity),"a fallen assigned guard does not block replacement pursuit");
            third.Entity.X=70;third.Entity.Y=70;
            var fourth=Player(map,true,29,23);Register(fourth,false);
            Check(CityGuardSystem.IsGuardTarget(p,fourth.Entity),"an out-of-range guard releases its pursuit slot");
            uint hp=regular.Entity.Hitpoints;p.Entity.PKMode=Enums.PKMode.Peace;hit=100;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,regular.Entity,new Attack(true),ref hit,null);
            Check(hit==0 && regular.Entity.Hitpoints==hp,"peace mode cannot damage a guard through the shared hit path");p.Entity.PKMode=Enums.PKMode.PK;
            p.Socket.Alive=false;Check(!CityGuardSystem.IsGuardTarget(p,regular.Entity),"disconnected offenders are not pursued");p.Socket.Alive=true;
            var arena=(Map)Fixture("World",(ushort)1700);Guards.GetField("ArenaMap",Hidden).SetValue(null,arena);
            p.BindWorldMap(arena.ID);p.Entity.X=50;p.Entity.Y=50;
            var darius=Player(arena,true,40,50);var elite=Register(darius,true);
            darius.Entity.MaxHitpoints=90000;Call("RestoreCombatProfile",darius.Entity);
            Check(darius.Entity.MaxHitpoints==3000000 && darius.Entity.Defence>=24000 && darius.Entity.MagicDefencePercent>=55,"Darius retains three million HP and elite defenses even above the old 50000 fallback");
            Set(incident,"Arena",true);Set(incident,"WarningUntil",(DateTime?)DateTime.Now.AddSeconds(5));
            Check(!CityGuardSystem.IsGuardTarget(p,darius.Entity),"Darius allows the arena warning before attacking");
            Set(incident,"WarningUntil",(DateTime?)DateTime.Now.AddMilliseconds(-1));
            Check(CityGuardSystem.IsGuardTarget(p,darius.Entity),"Darius can target the sentenced player inside his own arena");
            oldX=darius.Entity.X;Call("DriveGuard",elite,DateTime.Now);
            Check(darius.Entity.X>oldX && Get<uint>(elite,"Sight")==p.Entity.UID,"Darius actually closes distance after the warning");
            var bystander=Player(arena,false,50,51);
            Check(!CityGuardSystem.IsGuardTarget(bystander,darius.Entity),"arena visitors without a sentence are not attacked");
            bystander.Entity.PKPoints=100;Check(!CityGuardSystem.IsGuardTarget(bystander,darius.Entity),"wanted arena visitors also need a sentence before Darius can attack");
            uint eliteHp=darius.Entity.Hitpoints;hit=100;
            Revora.Game.Attacking.Handle.ReceiveAttack(bystander.Entity,darius.Entity,new Attack(true),ref hit,null);
            Check(hit==0 && darius.Entity.Hitpoints==eliteHp,"visitors cannot damage Darius outside an active sentence");hit=100;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,darius.Entity,new Attack(true),ref hit,null);
            Check(darius.Entity.Hitpoints==eliteHp-100 && p.Entity.MapID==arena.ID,"sentenced players can damage Darius without their attacks being discarded");
            regular.BindWorldMap(arena.ID);regular.Entity.X=49;regular.Entity.Y=50;
            Check(!CityGuardSystem.IsGuardTarget(p,regular.Entity),"street officers cannot take over the elite arena challenge");
            p.Attackable=false;Check(!CityGuardSystem.IsGuardTarget(p,darius.Entity),"unattackable players cannot be pursued");p.Attackable=true;
            Set(incident,"Deadline",DateTime.Now.AddSeconds(-1));Check(!CityGuardSystem.IsGuardTarget(p,darius.Entity),"expired arena incidents cannot authorize attacks");
            // Exercise the real lethal-hit, sentence, teleport and release paths using only in-memory maps.
            EmptyCollections(map);EmptyCollections(arena);EmptyCollections(p.Screen);
            Kernel.GamePool.Remove(bystander.Entity.UID);
            Guards.GetField("ArenaElite",Hidden).SetValue(null,elite);
            regular.BindWorldMap(map.ID);regular.Entity.X=30;regular.Entity.Y=20;regular.Entity.Hitpoints=100;
            p.BindWorldMap(map.ID);p.Entity.X=29;p.Entity.Y=20;p.Attackable=true;hit=500;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,regular.Entity,new Attack(true),ref hit,null);
            Check(regular.Entity.Dead && hit==100 && p.Entity.MapID==arena.ID && Get<bool>(Incident(p.Entity.UID),"Arena"),"killing an officer clamps damage and sends the killer to Darius's arena");
            Check(Get<DateTime>(unit,"ReviveAt")>DateTime.Now.AddSeconds(10),"fallen street officers retain their twelve-second respawn");
            p.Attackable=true;darius.Entity.Hitpoints=100;hit=500;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,darius.Entity,new Attack(true),ref hit,null);
            Check(darius.Entity.Dead && hit==100 && p.Entity.MapID==1002 && Incident(p.Entity.UID)==null,"defeating Darius releases the player through the real death path");
            // Reuse the actual guard strike, sentence and release routes.
            var defender=Player(map,false,29,20);EmptyCollections(defender.Screen);CompanionTests.ArmBackfire(defender);
            var officer=Player(map,true,30,20);var counterUnit=Register(officer,false);officer.Entity.Hitpoints=officer.Entity.MaxHitpoints=200000;
            officer.Entity.MinAttack=1000;officer.Entity.MaxAttack=1001;Call("Strike",counterUnit,officer.Entity,defender.Entity);
            Check(officer.Entity.Hitpoints==150000 && defender.Entity.Hitpoints==99000 && defender.Entity.Stamina==30 && !defender.Entity.OnBackFire(),"actual guard strike triggers one HP counter and preserves its own damage");
            Call("Strike",counterUnit,officer.Entity,defender.Entity);Check(officer.Entity.Hitpoints==150000 && defender.Entity.Stamina==30,"later guard strikes cannot replay the activation");
            var impostor=Player(map,true,30,20);CompanionTests.ArmBackfire(defender);
            Check(!CompanionTests.CounterBackfire(impostor.Entity,defender.Entity) && defender.Entity.OnBackFire(),"an unregistered fake actor cannot consume Backfire");
            Kernel.GamePool[officer.Entity.UID]=impostor;
            Check(!CompanionTests.CounterBackfire(officer.Entity,defender.Entity) && defender.Entity.OnBackFire(),"replaced guard session cannot consume Backfire");Kernel.GamePool[officer.Entity.UID]=officer;
            Console.WriteLine("Guard checks passed: "+checks);return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
