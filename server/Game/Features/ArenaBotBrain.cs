using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal sealed class ArenaBotBrain
    {
        private readonly GameState owner;
        private readonly Random random;
        private readonly Dictionary<ushort,DateTime> attempts=new Dictionary<ushort,DateTime>();
        private DateTime nextDecision,nextMove,nextAttack,chargeUntil;
        private ushort observedX,observedY,priorX,priorY;
        private bool observed,stopped;
        internal int SpellAttempts,BasicAttempts,Movements,RejectedSpells;
        private sealed class CastReceipt { internal GameState Owner; internal ushort Id; internal bool Prepared; }
        [ThreadStatic] private static CastReceipt receipt;
        internal static void Prepared(GameState owner,ushort id)
        { if(receipt!=null && ReferenceEquals(receipt.Owner,owner) && receipt.Id==id)receipt.Prepared=true; }
        internal static bool ObserveCast(GameState owner,ushort id,Action cast)
        {
            var previous=receipt;var current=new CastReceipt{Owner=owner,Id=id};receipt=current;
            try {cast();return current.Prepared;} finally {receipt=previous;}
        }
        internal static bool RefreshTarget(GameState owner,GameState opponent)
        {
            if(owner==null || opponent==null || owner.Entity==null || opponent.Entity==null || owner.Screen==null
                || owner.Entity.MapID!=opponent.Entity.MapID || opponent.Entity.Invisable
                || Kernel.GetDistance(owner.Entity.X,owner.Entity.Y,opponent.Entity.X,opponent.Entity.Y)>Constants.pScreenDistance)return false;
            owner.Screen.Add(opponent.Entity);
            Entity found;return owner.Screen.TryGetValue(opponent.Entity.UID,out found) && ReferenceEquals(found,opponent.Entity);
        }
        internal ArenaBotBrain(GameState p){owner=p;random=new Random(unchecked((int)p.Entity.UID^Environment.TickCount));}
        internal void Stop(){stopped=true;}
        internal void Tick(GameState opponent)
        {
            var group=owner.QualifierGroup;var now=DateTime.UtcNow;
            if(stopped || now<nextDecision || group==null || group.Done || group.EntryRecoveryRequired || !group.ParticipationRecorded
                || !group.OwnsImportedMap || !ReferenceEquals(group.OppositeClient(owner),opponent))return;
            var players=new[]{owner,opponent}.OrderBy(p=>p.Entity.UID).ToArray();int acquired=0;
            try
            {
                foreach(var p in players){if(!Monitor.TryEnter(p.ItemSyncRoot))return;acquired++;}
                if(!ArenaActors.Player(owner) || !QuestInventory.Player(opponent) || !group.OwnsImportedMap)return;
                nextDecision=now.AddMilliseconds(ArenaBotPolicy.ReactionMilliseconds+random.Next(0,121));
                if(unchecked(Time32.Now.Value-group.ImportTime.Value)<5000)return;
                owner.Attackable=true;
                var e=owner.Entity;var enemy=opponent.Entity;
                // Decide using the previous observation: no access to the opponent's next input.
                ushort tx=observed?observedX:enemy.X,ty=observed?observedY:enemy.Y;
                observedX=enemy.X;observedY=enemy.Y;observed=true;
                if(e.Stunned || e.ContainsFlag((ulong)FlagsUpdate.Flags.Freeze))return;
                var weapon=owner.Weapons.Item1;
                bool knives=weapon!=null && weapon.ID/1000==613;
                var profile=ArenaBotPolicy.For(e.Class,knives,!e.IsStomper2());
                int distance=Kernel.GetDistance(e.X,e.Y,tx,ty);
                bool targetVisible=RefreshTarget(owner,opponent);
                int hp=(int)(100UL*e.Hitpoints/Math.Max(1u,e.MaxHitpoints));
                int mp=100*e.Mana/Math.Max(1,(int)e.MaxMana);
                bool harmful=e.ToxicFogLeft>0 || e.ScurbyBomb>0 || e.DragonFuryTime>0;
                if(now<chargeUntil)
                {
                    if(distance>=5 && hp>=45)return;
                    // Break the setup to survive pressure instead of standing still for a combo.
                    chargeUntil=DateTime.MinValue;e.SpiritFocus=false;nextAttack=now;nextMove=now;
                }
                if(knives && !e.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow) && now>=nextAttack
                    && Cast(11620,opponent,tx,ty,true,now)) {nextAttack=now.AddMilliseconds(650);return;}
                if(knives && distance>=8 && hp>=65 && !e.SpiritFocus && now>=nextAttack
                    && Cast(11670,opponent,tx,ty,true,now))
                {
                    chargeUntil=now.AddSeconds(5);nextAttack=chargeUntil;nextMove=chargeUntil;return;
                }
                if(now>=nextAttack && targetVisible)
                {
                    foreach(ushort id in ArenaBotPolicy.Priorities(profile,hp,mp,harmful,distance<=4).Distinct())
                    {
                        bool self=profile.Recovery.Contains(id) || profile.Defenses.Contains(id);
                        if(self && !Useful(id,hp,mp,harmful))continue;
                        if(Cast(id,opponent,tx,ty,self,now))
                        {
                            nextAttack=now.AddMilliseconds(650+random.Next(0,151));
                            nextMove=nextAttack;return;
                        }
                    }
                    if(now>=nextAttack && distance<=((e.IsBowEquipped || knives)?12:Math.Max(1,(int)e.AttackRange)))
                    {
                        BasicAttempts++;
                        e.Action=Enums.ConquerAction.None;
                        new Attacking.Handle(new Attack(true){Attacker=e.UID,Attacked=enemy.UID,AttackType=(e.IsBowEquipped || knives)?Attack.Ranged:Attack.Melee,X=tx,Y=ty,Decoded=true},e,enemy);
                        nextAttack=now.AddMilliseconds(850+random.Next(0,151));
                        nextMove=now.AddMilliseconds(450);return;
                    }
                }
                if(now<nextMove || e.IsDefensiveStance || e.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))return;
                bool retreat=e.Stamina<15;
                int preferred=retreat?9:PreferredRange(profile,now);
                bool sight=CompanionNavigation.HasSight(new System.Drawing.Point(e.X,e.Y),new System.Drawing.Point(tx,ty),
                    (px,py)=>owner.Map.Floor[px,py,MapObjectType.Player,null]);
                if(ArenaBotPolicy.HoldAttackPosition(distance,preferred,retreat,sight))return;
                if(e.Stamina<25 && distance>=8)
                {
                    e.Action=Enums.ConquerAction.Sit;nextMove=now.AddMilliseconds(900);return;
                }
                Move(tx,ty,preferred,now);
            }
            finally{for(int i=acquired-1;i>=0;i--)Monitor.Exit(players[i].ItemSyncRoot);}
        }
        private bool Useful(ushort id,int hp,int mp,bool harmful)
        {
            var e=owner.Entity;
            switch(id)
            {
                case 1195:return mp<35;
                case 1175:case 1005:case 1190:return hp<65;
                case 12950:return hp<65 && !e.ContainsFlag((ulong)FlagsUpdate.Flags.HealingSnow);
                case 10425:return harmful;
                case 11960:return e.Stamina<35;
                case 1090:return !e.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield);
                case 1095:return !e.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma);
                case 10395:case 10410:return !e.Aura_isActive;
                case 6003:return !e.CounterKillSwitch;
                // Stationary Assassin charge needs a dedicated state, never spam it as a dodge.
                case 11620:return !e.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow);
                case 11670:return false;
                default:return true;
            }
        }
        private SpellInformation ReadySpell(ushort id,DateTime now)
        {
            ISkill learned;DateTime retry;
            if(!owner.Spells.TryGetValue(id,out learned) || !learned.Available || learned.ColdTime>Time32.Now
                || attempts.TryGetValue(id,out retry) && now<retry)return null;
            var spell=SpellTable.GetSpell(id,learned.Level);var body=owner.Entity;
            if(spell==null || spell.NeedXP==1 || body.Mana<spell.UseMana || body.Stamina<spell.UseStamina
                || !TrojanEpicTraining.CanPerform(owner,id))return null;
            var weapons=owner.Weapons;
            if(!ArenaBotPolicy.WeaponAllows(spell,weapons.Item1==null?0:weapons.Item1.ID,weapons.Item2==null?0:weapons.Item2.ID))return null;
            return spell;
        }
        private int PreferredRange(ArenaBotPolicy.Profile profile,DateTime now)
        {
            foreach(ushort id in profile.Attacks) {
                if(id==6001 || id==6002 || id==11120)continue; // Debuffs are not sustained attack range.
                var spell=ReadySpell(id,now);
                if(spell!=null)return Math.Max(1,Math.Min(profile.Range,(int)spell.Distance));
            }
            return (owner.Entity.IsBowEquipped || owner.Entity.Assassin())?9:Math.Max(1,(int)owner.Entity.AttackRange);
        }
        private bool Cast(ushort id,GameState enemy,ushort x,ushort y,bool self,DateTime now)
        {
            var spell=ReadySpell(id,now);if(spell==null)return false;
            var body=owner.Entity;
            if(id==6001 && enemy.Entity.ToxicFogLeft>0 || id==11120 && enemy.Entity.IsBlackSpotted)return false;
            if(id==6004 && !enemy.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))return false;
            if(!self && (Kernel.GetDistance(body.X,body.Y,x,y)>Math.Max(1,(int)spell.Distance)
                || spell.OnlyGround && enemy.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly)))return false;
            if(!self && !CompanionNavigation.HasSight(new System.Drawing.Point(body.X,body.Y),new System.Drawing.Point(x,y),
                (tx,ty)=>owner.Map.Floor[tx,ty,MapObjectType.Player,null]))return false;
            attempts[id]=now.AddMilliseconds(self?8000:1200);
            SpellAttempts++;
            body.Action=Enums.ConquerAction.None;
            var target=self?body:enemy.Entity;
            bool accepted=ObserveCast(owner,id,()=>new Attacking.Handle(new Attack(true){Attacker=body.UID,Attacked=target.UID,AttackType=Attack.Magic,
                Damage=id,SpellID=id,X=self?body.X:x,Y=self?body.Y:y,Decoded=true},body,target));
            if(id==11620 && body.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow))accepted=true;
            if(!accepted){RejectedSpells++;attempts[id]=now.AddSeconds(3);}
            return accepted;
        }
        private void Move(ushort tx,ushort ty,int preferred,DateTime now)
        {
            var e=owner.Entity;
            int best=ArenaBotPolicy.MovementScore(Kernel.GetDistance(e.X,e.Y,tx,ty),preferred,false,false,1);
            ushort bx=e.X,by=e.Y;
            for(int step=1;step<=5;step++)
            for(int i=0;i<16;i++)
            {
                double angle=(i+random.NextDouble()*.25)*Math.PI/8;
                int x=e.X+(int)Math.Round(Math.Cos(angle)*step),y=e.Y+(int)Math.Round(Math.Sin(angle)*step);
                if(x<1 || y<1 || x>=owner.Map.Floor.Bounds.Width || y>=owner.Map.Floor.Bounds.Height)continue;
                bool clear=CompanionNavigation.HasSight(new System.Drawing.Point(e.X,e.Y),new System.Drawing.Point(x,y),
                    (px,py)=>owner.Map.Floor[px,py,MapObjectType.Player,null]);
                bool line=Math.Abs(x-tx)<=1 || Math.Abs(y-ty)<=1 || Math.Abs(Math.Abs(x-tx)-Math.Abs(y-ty))<=1;
                int score=ArenaBotPolicy.MovementScore(Kernel.GetDistance((ushort)x,(ushort)y,tx,ty),preferred,preferred>2 && line,!clear,x==priorX && y==priorY?1:0);
                if(score>best){best=score;bx=(ushort)x;by=(ushort)y;}
            }
            if(bx!=e.X || by!=e.Y)
            {
                Movements++;
                priorX=e.X;priorY=e.Y;
                if(Kernel.GetDistance(e.X,e.Y,bx,by)<3)WorldActorMotion.Walk(e,bx,by,false);
                else WorldActorMotion.JumpClear(e,bx,by);
            }
            nextMove=now.AddMilliseconds(600+random.Next(0,241));
        }
    }
}
