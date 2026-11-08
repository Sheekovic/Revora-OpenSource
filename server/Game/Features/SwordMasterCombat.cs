using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using E=Revora.Game.Features.SwordMasterEncounter;
using S=Revora.Game.Features.SwordMasterAdmission;

namespace Revora.Game.Features
{
    internal static class SwordMasterCombat
    {
        private sealed class Cast {internal int Phase;internal uint Target;internal ushort X,Y;internal bool Preparing;internal DateTime Due,Next;}
        private static readonly ConditionalWeakTable<E.Actor,Cast> casts=new ConditionalWeakTable<E.Actor,Cast>();
        private static readonly ushort[] Spells={10369,10504,10500,10505,10506,10503,10374};
        private static readonly string[] Names={"Slash","SwordBeam","Immolation","RevengePunch","BladeVortex","BladeStorm","Heal"};
        internal static void Initialize() {
            var rows=File.ReadLines(Path.Combine(Constants.DMapsPath,"magictype.txt")).Select(s=>s.Split(new[]{"@@"},StringSplitOptions.None)).Where(s=>s.Length>8).ToArray();
            for(int n=0;n<Spells.Length;n++)if(rows.Count(r=>r[1]==Spells[n].ToString() && r[3]==Names[n] && r[8]=="0")!=1)throw new InvalidOperationException("Sword Master skill catalog changed.");
        }
        internal static bool Contains(int phase,int dx,int dy) {
            long distance=(long)dx*dx+(long)dy*dy;
            return phase==0?distance<=9:phase==1?distance<=144:phase==2?distance<=4:phase==3?distance<=9:phase==4?distance<=36:phase==5 && distance>=16 && distance<=100;
        }
        internal static void Tick(E.Actor a) {
            Cast c;lock(E.Gate)if(!E.Current(a) || !casts.TryGetValue(a,out c) || !c.Preparing || S.Clock()<c.Due)return;
            Execute(a.Body,null);
        }
        internal static bool Execute(Entity body,Entity target) {
            if(!E.IsEncounter(body))return false;E.Actor a;
            if(!E.Actors.TryGetValue(body.MapID,out a) || !ReferenceEquals(a.Body,body) || !Monitor.TryEnter(a.Owner.ItemSyncRoot))return true;
            try {lock(E.Gate) {
                if(!E.Current(a))return true;
                var c=casts.GetValue(a,_=>new Cast());var now=S.Clock();if(now<c.Next)return true;
                if(!c.Preparing) {
                    if(target==null || !ReferenceEquals(target.Owner==null?null:target.Owner.Entity,target) || !E.Eligible(a,target.Owner,true)
                        || !target.Owner.Attackable || Kernel.GetDistance(body.X,body.Y,target.X,target.Y)>12)return true;
                    c.Target=target.UID;c.X=c.Phase==2 || c.Phase==3?target.X:body.X;c.Y=c.Phase==2 || c.Phase==3?target.Y:body.Y;
                    c.Preparing=true;c.Due=now.AddSeconds(2);body.Facing=Kernel.GetAngle(body.X,body.Y,target.X,target.Y);body.MonsterInfo.LastMove=Time32.Now.AddSeconds(2);
                    string warning=c.Phase==2?"Flames gather at ("+c.X+","+c.Y+"). Move clear!":c.Phase==3?"The Sword Master draws back his fist. Spread out!":c.Phase==4?"Blades whirl around the Sword Master. Step away!":c.Phase==5?"A storm of blades gathers. Close in or retreat!":c.Phase==6?"The Sword Master gathers strength to heal.":c.Phase==1?"The Sword Master readies Sword Beam. Break away!":"The Sword Master raises his blade.";
                    foreach(uint uid in a.Run.Party){GameState member;if(Kernel.GamePool.TryGetValue(uid,out member) && E.Eligible(a,member,false))member.Send(warning);}return true;
                }
                if(now<c.Due)return true;int phase=c.Phase;uint victim=c.Target;ushort x=c.X,y=c.Y;c.Preparing=false;c.Phase=(phase+1)%7;c.Next=now.AddSeconds(4);
                var spell=new SpellUse(true){Attacker=body.UID,SpellID=Spells[phase],X=x,Y=y};
                if(phase==6) {
                    uint heal=Math.Min(body.MaxHitpoints-body.Hitpoints,body.MaxHitpoints/50);body.Hitpoints+=heal;spell.AddTarget(body.UID,heal,null);
                }else foreach(uint uid in a.Run.Party) {
                    GameState p;if(!Kernel.GamePool.TryGetValue(uid,out p) || !Monitor.TryEnter(p.ItemSyncRoot))continue;
                    try {
                        if(!E.Current(a) || !E.Eligible(a,p,true) || !p.Attackable || phase<2 && uid!=victim
                            || !Contains(phase,p.Entity.X-x,p.Entity.Y-y))continue;
                        var hit=new Attack(true){Attacker=body.UID,Attacked=uid,AttackType=Attack.Magic};
                        uint damage=phase==2 || phase==5?Attacking.Calculate.Magic(body,p.Entity,new SpellInformation{ID=Spells[phase],Level=0,Power=0},ref hit):Attacking.Calculate.Melee(body,p.Entity,ref hit);
                        if(phase==1)damage=(uint)Math.Min(uint.MaxValue,(ulong)damage*2);
                        WarriorModes.Counter(body,p.Entity,hit,damage,Time32.Now);if(!E.Current(a))break;
                        damage=Math.Min(damage,p.Entity.Hitpoints);if(damage>=p.Entity.Hitpoints)p.Entity.Die(body.UID);else if(damage>0)p.Entity.Hitpoints-=damage;
                        spell.AddTarget(uid,damage,hit);
                    }finally{Monitor.Exit(p.ItemSyncRoot);}
                }
                if(E.Current(a))body.MonsterInfo.SendScreen(spell);return true;
            }}finally{Monitor.Exit(a.Owner.ItemSyncRoot);}
        }
    }
}
