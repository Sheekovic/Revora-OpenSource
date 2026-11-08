using System;
using System.Collections.Generic;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type StanceState=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorStance");
    static GameState StancePlayer(Map town) {
        var p=DefenderPlayer(town);var npc=Kernel.Maps[1004].Npcs[10001];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10001;EntityTable.SaveEntity(p);SoulWire(p,10,10001);
        if(!p.Spells.ContainsKey(11160) || Fenced(p))throw new Exception("Ordinary Defensive Stance instruction failed.");
        p.Entity.Teleport(1002,408,330);p.Entity.Defence=1000;return p;
    }
    static void StanceCast(GameState p,ushort id=11160) {
        p.Entity.AttackStamp=default(Time32);new Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
    }
    static bool StanceActive(GameState p){return (bool)Call(StanceState,null,"Active",p);}
    static int StanceDefence(GameState p,int raw,Time32 now){return (int)Call(StanceState,null,"Defence",p.Entity,raw,now);}
    static void VerifyWarriorStance(Map town) {
        foreach(byte level in new byte[]{0,1}) {
            var p=StancePlayer(town);p.Spells[11160].Level=level;StanceCast(p);var start=p.Entity.DefensiveStanceStamp;int duration=level==0?120:240;
            Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.IsDefensiveStance && StanceActive(p) && p.Entity.Stamina==100 && p.Entity.DefensiveStance==duration,"ordinary decoded stance activates with a shield and no stamina cost: "+level);
            Check(StanceDefence(p,1000,start)==(level==0?2000:3000) && StanceDefence(p,int.MaxValue,start)==int.MaxValue,"stance multiplies total physical defense safely: "+level);
            p.Entity.FatigueSecs=255;Check(StanceDefence(p,1000,start)==(level==0?2000:3000),"unrelated fatigue cannot alter stance strength: "+level);
            Call(StanceState,null,"Tick",p,start.AddMilliseconds(-1));Check(StanceActive(p),"old timer observation cannot retire a newer stance: "+level);
            p.Spells[11160].Level=(byte)(1-level);Check(StanceDefence(p,1000,start)==(level==0?2000:3000),"stance retains the accepted cast's strength when skill training changes: "+level);
            Check(StanceDefence(p,1000,start.AddSeconds(duration).AddMilliseconds(-1))==(level==0?2000:3000) && StanceDefence(p,1000,start.AddSeconds(duration))==1000 && !p.Entity.IsDefensiveStance,"owned stance expires exactly at its catalog boundary: "+level);
        }
        var toggle=StancePlayer(town);StanceCast(toggle);StanceCast(toggle);
        Check(!StanceActive(toggle) && toggle.Entity.DefensiveStance==0 && !toggle.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.DefensiveStance),"second decoded cast cancels stance and its detail");
        foreach(string bad in new[]{"class","skill","shield","broken","ride","fly","transformed","dead","fake","session"}) {
            var p=StancePlayer(town);var other=RebirthPlayer(town,15);
            if(bad=="class")p.Entity.Class=15;if(bad=="skill")p.Spells.Remove(11160);if(bad=="shield")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).ID=410009;
            if(bad=="broken")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;if(bad=="ride")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ride,30,true);
            if(bad=="fly")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,30,true);if(bad=="dead")p.Entity.Hitpoints=0;if(bad=="fake")p.Fake=true;
            if(bad=="transformed")p.Entity.TransformationID=1000;
            if(bad=="session")Kernel.GamePool[p.Entity.UID]=other;
            Call(StanceState,null,"Toggle",p);Check(!StanceActive(p) && StanceDefence(p,1000,Time32.Now)==1000,"invalid stance request grants no defensive authority: "+bad);
            if(bad=="session")Kernel.GamePool[p.Entity.UID]=p;
        }
        var mover=StancePlayer(town);StanceCast(mover);ushort x=mover.Entity.X,y=mover.Entity.Y;
        var jump=new Data(true){UID=mover.Entity.UID,ID=Data.Jump,wParam1=(ushort)(x+2),wParam2=y};GeneralData.PlayerJump(jump,mover);
        foreach(uint kind in new[]{GroundMovement.Run,GroundMovement.Slide})GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=mover.Entity.UID,GroundMovementType=kind,Direction=Enums.ConquerAngle.East},mover);
        Check(mover.Entity.X==x && mover.Entity.Y==y && StanceActive(mover),"real jump, run and mounted-slide handlers reject displacement while stance remains active");
        bool walked=false;foreach(Enums.ConquerAngle angle in Enum.GetValues(typeof(Enums.ConquerAngle)))if(mover.Entity.Move(angle)){walked=true;break;}
        Check(walked && (mover.Entity.X!=x || mover.Entity.Y!=y) && StanceActive(mover),"ordinary one-tile movement remains possible in stance");
        mover.Spells[7001]=new Spell(true){ID=7001,Level=0,Available=true};RebirthGear(mover,ConquerItem.Steed,ConquerItemInformation.BaseInformations.Keys.First(id=>id/1000==300));
        StanceCast(mover,7001);Check(!mover.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) && mover.Entity.Stamina==100 && StanceActive(mover),"actual mount cast cannot bypass stance or spend riding stamina");
        foreach(string end in new[]{"map","skill","shield","death","cleanup"}) {
            var p=StancePlayer(town);var foe=RebirthPlayer(town,15);StanceCast(p);
            if(end=="map")p.Entity.Teleport(1004,38,42);if(end=="skill")p.Spells.Remove(11160);if(end=="shield")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;
            if(end=="death")p.Entity.Die(foe.Entity);if(end=="cleanup")lock(p.ItemSyncRoot)Call(WarriorState,null,"Clear",p);
            Check(!StanceActive(p) && StanceDefence(p,1000,Time32.Now)==1000 && !p.Entity.IsDefensiveStance,"stance authority and display retire after "+end);
        }
        var defender=StancePlayer(town);var mob=LootMonster(defender);mob.Hitpoints=mob.MaxHitpoints=100000;mob.MinAttack=mob.MaxAttack=10000;mob.Level=130;
        var hit=new Attack(true);uint melee=Calculate.Melee(mob,defender.Entity,ref hit),ranged=Calculate.Ranged(mob,defender.Entity,ref hit);
        StanceCast(defender);uint stanceMelee=Calculate.Melee(mob,defender.Entity,ref hit),stanceRanged=Calculate.Ranged(mob,defender.Entity,ref hit);
        Check(melee>stanceMelee && ranged>stanceRanged,"actual melee and ranged calculations apply the increased defense once");
        var spell=SpellTable.GetSpell(1000,0);mob.MagicAttack=10000;uint magic=Calculate.Magic(mob,defender.Entity,spell,ref hit);StanceCast(defender);
        Check(Calculate.Magic(mob,defender.Entity,spell,ref hit)==magic,"physical stance does not change magic defense");
        mob.TransformationID=1000;mob.TransformationMinAttack=mob.TransformationMaxAttack=10000;
        uint transformedHit=Calculate.Melee(mob,defender.Entity,ref hit);StanceCast(defender);
        Check(Calculate.Melee(mob,defender.Entity,ref hit)<transformedHit,"a transformed attacker still faces the defender's actual stance defense");
        var rite=StancePlayer(town);var sereth=town.Npcs[59558];rite.Entity.Teleport(1002,sereth.X,sereth.Y);rite.ActiveNpc=59558;EntityTable.SaveEntity(rite);StanceCast(rite);
        var sent=new Queue<byte[]>();typeof(Revora.Network.Sockets.ClientWrapper).GetField("SendQueue",Instance).SetValue(rite.Socket,sent);
        var syntheticKey=Enumerable.Range(0,16).Select(i=>(byte)i).ToArray();rite.Cryptography=new Revora.Network.Cryptography.GameCryptography(syntheticKey);
        var receive=new Revora.Network.Cryptography.GameCryptography(syntheticKey);rite.Cryptography.SetIvs(new byte[8],new byte[8]);receive.SetIvs(new byte[8],new byte[8]);
        typeof(Revora.Network.Sockets.ClientWrapper).GetField("SendSyncRoot",Instance).SetValue(rite.Socket,new object());rite.Socket.Alive=true;
        RebirthOffer(rite,21);StoneNpc(rite,214);Check(!Fenced(rite) && rite.Entity.Reborn==1 && rite.Spells.ContainsKey(11160) && !StanceActive(rite),"actual first rebirth retains the earned stance art but retires the old life's activation");
        bool reset=false;foreach(var bytes in sent) {
            receive.Decrypt(bytes,bytes.Length);if(bytes.Length<4 || System.BitConverter.ToUInt16(bytes,2)!=10017)continue;
            var packet=new FlagsUpdate(false);packet.Deserialize(bytes);if(packet.UID==rite.Entity.UID && packet.Buffer.Values.Any(v=>v.Type==(byte)FlagsUpdate.DataType.DefensiveStance && v.ICOID==126 && v.Time==0))reset=true;
        }
        Check(reset,"committed rebirth emits the stance detail reset after retiring its active state");rite.Socket.Alive=false;
        rite=ReloadBall(rite);Check(rite.Spells.ContainsKey(11160) && rite.Spells[11160].Level==0 && !rite.Entity.IsDefensiveStance,"cold reload keeps the stance lesson and reset level without reviving the old effect");
    }
}
