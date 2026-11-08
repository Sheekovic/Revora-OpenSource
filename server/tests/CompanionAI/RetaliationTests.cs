using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class RetaliationTests
{
    static int checks;
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static object Fixture(string name,params object[] args){return typeof(CompanionTests).GetMethod(name,Hidden).Invoke(null,args);}
    static object Call(string name,params object[] args){return typeof(LivingWorldSystem).GetMethod(name,Hidden).Invoke(null,args);}
    static void Set(object actor,string name,object value){actor.GetType().GetField(name).SetValue(actor,value);}
    static T Get<T>(object actor,string name){return (T)actor.GetType().GetField(name).GetValue(actor);}
    static void Check(bool okay,string name){if(!okay)throw new Exception("FAILED: "+name);checks++;Console.WriteLine("PASS: "+name);}
    static GameState Player(Map map,bool fake,ushort x,ushort y,byte cls)
    {
        var p=(GameState)Fixture("Player",map,fake,x,y,cls);
        p.Attackable=true;p.Account.State=AccountTable.AccountState.Player;
        p.Entity.PKMode=Enums.PKMode.PK;
        if(!fake){
            p.Socket.Alive=true;
            typeof(Revora.Network.Sockets.ClientWrapper).GetField("SendQueue",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Socket,new Queue<byte[]>());
            typeof(Revora.Network.Sockets.ClientWrapper).GetField("SendSyncRoot",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Socket,new object());
        }
        MapsTable.MapInformations[map.ID]=new MapsTable.MapInformation{ID=map.ID,BaseID=map.ID,Status=2};
        return p;
    }
    static void Hit(GameState p,GameState body,uint damage)
    {
        var attack=new Attack(true){Attacker=p.Entity.UID,Attacked=body.Entity.UID,AttackType=Attack.Melee};
        Handle.ReceiveAttack(p.Entity,body.Entity,attack,ref damage,null);
    }
    public static int Main(string[] args)
    {
        try {
            CompanionTests.LoadBackfire(args[0]);
            foreach(string line in File.ReadLines(args[0])) {
                string[] fields=line.Split(new[]{"@@"," "},StringSplitOptions.RemoveEmptyEntries);
                if(fields.Length>2 && (fields[1]=="1000" || fields[1]=="1002" || fields[1]=="1005"))SpellTable.Parse(line);
            }
            Fixture("Reset");var map=(Map)Fixture("World",(ushort)1002);
            var p=Player(map,false,30,20,15);var body=Player(map,true,20,20,15);var actor=Fixture("Actor",body);
            Check(Handle.CanAttack(p.Entity,body.Entity,null,true),"ordinary player can deliberately provoke an adventurer in Twin City");
            p.Entity.PKMode=Enums.PKMode.Peace;Hit(p,body,100);
            Check(body.Entity.Hitpoints==10000 && Get<uint>(actor,"AggressorUid")==0,"peace-mode requests do not hurt or provoke adventurers");
            p.Entity.PKMode=Enums.PKMode.PK;Hit(p,body,0);
            Check(Get<uint>(actor,"AggressorUid")==0,"misses and zero damage do not start retaliation");
            map.Floor.Locations[25,20]=Floor.DMapPointFlag.Invalid;Hit(p,body,100);
            Check(Get<uint>(actor,"AggressorUid")==0,"blocked sight cannot provoke through a wall");
            map.Floor.Locations[25,20]=0;Hit(p,body,100);
            Check(body.Entity.Hitpoints==9900 && Get<uint>(actor,"AggressorUid")==p.Entity.UID,"accepted damage starts focused retaliation through the real receive handler");
            var other=Player(map,false,21,21,15);uint otherHp=other.Entity.Hitpoints;
            Call("DriveRetaliation",actor,DateTime.UtcNow);
            Check(body.Entity.Action==Enums.ConquerAction.Jump && body.Entity.X>20 && body.Entity.X<=26,"retaliation closes distance with a bounded jump immediately");
            body.Entity.X=29;body.Entity.Y=20;Set(actor,"NextReaction",DateTime.MinValue);Set(actor,"NextCombat",DateTime.MinValue);
            uint hp=p.Entity.Hitpoints;DateTime now=DateTime.UtcNow;Call("DriveRetaliation",actor,now);
            Check(p.Entity.Hitpoints<hp && other.Entity.Hitpoints==otherHp,"adventurer attacks only its aggressor, never the nearby bystander");
            hp=p.Entity.Hitpoints;Call("DriveRetaliation",actor,now.AddMilliseconds(200));
            Check(p.Entity.Hitpoints==hp,"reaction updates cannot bypass the attack cooldown");
            DateTime deadline=Get<DateTime>(actor,"NextCombat");Hit(p,body,1);
            Check(Get<DateTime>(actor,"NextCombat")==deadline,"being hit again cannot reset offensive cooldown");
            Call("DriveRetaliation",actor,now.AddMilliseconds(500));
            Check(p.Entity.Hitpoints<hp,"melee can retaliate again in half a second");
            Fixture("Command",p,actor,"hire");Check(Get<uint>(actor,"OwnerUid")==0,"aggressor cannot cancel a fight by hiring the adventurer");
            p.Socket.Alive=false;Call("DriveRetaliation",actor,DateTime.UtcNow);
            Check(Get<uint>(actor,"AggressorUid")==0,"logout immediately releases the target");p.Socket.Alive=true;
            Hit(p,body,1);Call("DriveRetaliation",actor,DateTime.UtcNow.AddSeconds(31));
            Check(Get<uint>(actor,"AggressorUid")==0,"retaliation expires after thirty seconds without another hit");
            Hit(p,body,1);p.Entity.X=70;Call("DriveRetaliation",actor,DateTime.UtcNow);
            Check(Get<uint>(actor,"AggressorUid")==0,"escaping the fight's leash ends pursuit");p.Entity.X=30;
            Hit(p,body,1);p.Entity.Hitpoints=0;Call("DriveRetaliation",actor,DateTime.UtcNow);
            Check(Get<uint>(actor,"AggressorUid")==0,"dead targets are immediately forgotten");p.Entity.Hitpoints=10000;
            Set(actor,"OwnerUid",p.Entity.UID);uint before=body.Entity.Hitpoints;Hit(p,body,1);
            Check(body.Entity.Hitpoints==before && Get<uint>(actor,"AggressorUid")==0,"a companion cannot be provoked against its own employer");Set(actor,"OwnerUid",0u);
            var market=(Map)Fixture("World",(ushort)1036);p.BindWorldMap(1036);body.BindWorldMap(1036);Hit(p,body,1);
            Check(Get<uint>(actor,"AggressorUid")==0,"Market remains protected");p.BindWorldMap(1002);body.BindWorldMap(1002);
            Hit(p,body,1);p.BindWorldMap(1036);Call("DriveRetaliation",actor,DateTime.UtcNow);
            Check(Get<uint>(actor,"AggressorUid")==0 && body.Entity.MapID==1002,"retaliation never follows a teleport between maps");p.BindWorldMap(1002);
            // Ranged tactics use the same targeting and timing state.
            var bow=Player(map,true,29,20,45);var bowActor=Fixture("Actor",bow);Hit(p,bow,100);
            hp=p.Entity.Hitpoints;Call("DriveRetaliation",bowActor,DateTime.UtcNow);
            Check(bow.Entity.Action==Enums.ConquerAction.Jump && Kernel.GetDistance(bow.Entity.X,bow.Entity.Y,p.Entity.X,p.Entity.Y)>3 && p.Entity.Hitpoints<hp,"archer evades melee range and shoots during the same decision");
            var mage=Player(map,true,29,20,145);var mageActor=Fixture("Actor",mage);Call("GiveSkill",mage,(ushort)1002);Hit(p,mage,1);
            ushort mana=mage.Entity.Mana;hp=p.Entity.Hitpoints;Call("DriveRetaliation",mageActor,DateTime.UtcNow);
            Check(mage.Entity.Mana<mana && p.Entity.Hitpoints<hp,"Fire Taoist retaliates with its learned spell and pays mana");
            mage.Entity.Mana=0;Set(mageActor,"NextCombat",DateTime.MinValue);Set(mageActor,"NextReaction",DateTime.MinValue);hp=p.Entity.Hitpoints;
            Call("DriveRetaliation",mageActor,DateTime.UtcNow);Check(p.Entity.Hitpoints==hp,"spell retaliation cannot attack with insufficient mana");
            // A legal finishing hit cleans up the adventurer without awarding fake-player rewards.
            body.Entity.Hitpoints=5;Hit(p,body,5);
            Check(body.Entity.Dead && Get<uint>(actor,"AggressorUid")==0,"adventurers remain killable and death clears their fight");
            var finisher=Player(map,true,29,20,15);var finishingActor=Fixture("Actor",finisher);
            p.Entity.Hitpoints=1;Hit(p,finisher,1);
            typeof(LivingWorldSystem).GetField("Ready",Hidden).SetValue(null,true);
            LivingWorldSystem.TickRetaliation();
            Check(p.Entity.Dead && Get<uint>(finishingActor,"AggressorUid")==0,"fast production timer executes a real player death and clears pursuit");
            Check(finisher.Entity.ConquerPoints==0 && finisher.Entity.Money==0,"retaliation kills award no synthetic CP or gold bounty");
            typeof(LivingWorldSystem).GetField("Ready",Hidden).SetValue(null,false);
            foreach(bool kill in new[]{false,true}){
                var defender=Player(map,false,30,20,25);var foe=Player(map,true,29,20,15);var foeActor=Fixture("Actor",foe);
                foe.Entity.Hitpoints=foe.Entity.MaxHitpoints=200000;Hit(defender,foe,1);CompanionTests.ArmBackfire(defender);
                if(kill)foe.Entity.Hitpoints=1000;
                Call("DriveRetaliation",foeActor,DateTime.UtcNow);
                Check(defender.Entity.Hitpoints<100000 && defender.Entity.Stamina==30 && !defender.Entity.OnBackFire()
                    && (kill?foe.Entity.Dead && Get<uint>(foeActor,"AggressorUid")==0:foe.Entity.Hitpoints==149999),"adventurer retaliation triggers one counter and preserves death cleanup: lethal="+kill);
                Check(defender.Entity.ConquerPoints==0 && defender.Entity.Money==0,"counter grants no fake-player currency bounty: lethal="+kill);
            }
            Console.WriteLine("Retaliation checks passed: "+checks);return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
        finally {Fixture("Reset");}
    }
}
