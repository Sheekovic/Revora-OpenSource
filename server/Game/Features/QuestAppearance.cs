using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Cosmetic only: no Body, Face, class, transformation or health mutation.
    // Each coordinator must verify saved eligibility and own rollback of provisional leases.
    internal static class QuestAppearance
    {
        internal const uint BeautyMesh=2562005,MouseMesh=30040,MonkeyMesh=156,ZombieMesh=4352,BanditMesh=201;
        private static DateTime Clock(uint mesh){return mesh==BanditMesh?FamilyFeudStore.Clock():mesh==ZombieMesh?MadManScenes.Clock():mesh==MonkeyMesh?FranticMonkeysScenes.Clock():SpunGoldAppearance.Clock();}
        private sealed class Look {internal GameState Owner;internal uint Mesh;internal ushort TrialMap;internal Guid Token=Guid.NewGuid();internal DateTime Until;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Look> looks=new Dictionary<uint,Look>();
        private static bool Ready(GameState p,uint mesh,ushort trialMap){return (mesh==BeautyMesh || mesh==MouseMesh || mesh==MonkeyMesh || mesh==ZombieMesh || mesh==BanditMesh) && Q.Player(p) && ReferenceEquals(p.Entity.Owner,p)
            && p.Entity.Level>=(mesh==BanditMesh?95:mesh==ZombieMesh?60:mesh==MonkeyMesh?50:mesh==MouseMesh?48:30) && p.Entity.Reborn<=2
            && (mesh==BanditMesh?p.Entity.MapID==1015:mesh==ZombieMesh?p.Entity.MapID==1002:mesh==MonkeyMesh?trialMap>=11000 && trialMap<60000 && (p.Entity.MapID==trialMap || p.Entity.MapID==1002 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,440,343)<=6):p.Entity.MapID==1011)
            && p.Entity.TransformationID==0 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Current(Look look){return Ready(look.Owner,look.Mesh,look.TrialMap) && Clock(look.Mesh)<look.Until;}
        internal static Guid Apply(GameState p,uint mesh,Func<bool> context)
        {
            if(mesh==MonkeyMesh || mesh==ZombieMesh || mesh==BanditMesh)throw new InvalidOperationException("Timed quest appearance requires its specific admission.");
            return ApplyCore(p,mesh,0,Clock(mesh).AddMinutes(5),context);
        }
        internal static Guid ApplyBandit(GameState p,DateTime until,Func<bool> context){
            var now=Clock(BanditMesh);if(until.Kind!=DateTimeKind.Utc || until<=now || until>now.AddSeconds(90))throw new InvalidOperationException("Bandit appearance deadline changed.");
            var run=FamilyFeudStore.Load(p);if(run==null || run.Stage!=7 || run.DisguiseUntil!=until)return Guid.Empty;
            return ApplyCore(p,BanditMesh,0,until,context);
        }
        internal static Guid ApplyZombie(GameState p,DateTime until,Func<bool> context)
        {
            var now=Clock(ZombieMesh);if(until.Kind!=DateTimeKind.Utc || until<=now || until>now.AddSeconds(20))throw new InvalidOperationException("Mad zombie expiry changed.");
            return ApplyCore(p,ZombieMesh,0,until,context);
        }
        internal static Guid ApplyMonkey(GameState p,FranticMonkeysScenes.Scene scene,DateTime until,Func<bool> context)
        {
            var now=Clock(MonkeyMesh);
            if(!FranticMonkeysScenes.Registered(scene,p) || until.Kind!=DateTimeKind.Utc || until<=now || until>now.AddSeconds(60) || until>=scene.Until)
                throw new InvalidOperationException("Monkey appearance reservation changed.");
            return ApplyCore(p,MonkeyMesh,scene.Map.ID,until,context);
        }
        private static Guid ApplyCore(GameState p,uint mesh,ushort trialMap,DateTime until,Func<bool> context)
        {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Ready(p,mesh,trialMap) || context==null || !context())throw new InvalidOperationException("Quest appearance disguise owner changed.");
            var now=Clock(mesh);if(now.Kind!=DateTimeKind.Utc || until.Kind!=DateTimeKind.Utc || until<=now)throw new InvalidOperationException("Quest appearance requires valid UTC expiry.");
            Guid token;
            lock(gate){
                Look current;
                if(looks.TryGetValue(p.Entity.UID,out current)){
                    if(!ReferenceEquals(current.Owner,p))return Guid.Empty;
                    if(Current(current))return current.Mesh==mesh && current.TrialMap==trialMap?current.Token:Guid.Empty;
                }
                if(current==null && looks.Count>=64)return Guid.Empty;
                if(!Ready(p,mesh,trialMap) || !context())throw new InvalidOperationException("Quest appearance disguise context changed.");
                var look=new Look{Owner=p,Mesh=mesh,TrialMap=trialMap,Until=until};looks[p.Entity.UID]=look;token=look.Token;
            }
            p.Entity.CharBuffer._Mesh=mesh;p.Entity.Update((byte)FlagsUpdate.DataType.Mesh,mesh,true);return token;
        }
        internal static bool Owns(GameState p,Guid token,uint mesh)
        {
            if(p==null || p.Entity==null)return false;
            lock(gate){Look look;return looks.TryGetValue(p.Entity.UID,out look) && ReferenceEquals(look.Owner,p) && look.Token==token && look.Mesh==mesh && Current(look) && p.Entity.Mesh==mesh;}
        }
        internal static Guid Token(GameState p,uint mesh){if(p==null || p.Entity==null)return Guid.Empty;lock(gate){Look look;
            return looks.TryGetValue(p.Entity.UID,out look) && ReferenceEquals(look.Owner,p) && Current(look) && look.Mesh==mesh && p.Entity.Mesh==mesh?look.Token:Guid.Empty;}}
        internal static uint Project(Entity body,uint ordinary)
        {
            if(body==null || body.EntityFlag!=EntityFlag.Player)return ordinary;
            lock(gate){Look look;if(!looks.TryGetValue(body.UID,out look) || !ReferenceEquals(look.Owner.Entity,body))return ordinary;
                if(Current(look))return look.Mesh;looks.Remove(body.UID);return ordinary;}
        }
        internal static void Clear(GameState p,Guid token,uint mesh)
        {
            if(p==null || p.Entity==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Quest appearance cleanup requires owner lock.");
            lock(gate){Look look;if(!looks.TryGetValue(p.Entity.UID,out look) || !ReferenceEquals(look.Owner,p) || look.Token!=token || look.Mesh!=mesh)return;looks.Remove(p.Entity.UID);}
            Restore(p,mesh);
        }
        private static void Restore(GameState p,uint mesh)
        {
            if(p.Entity.Mesh!=mesh)return;
            uint ordinary=(uint)(p.Entity.TransformationID*10000000+p.Entity.Face*10000+p.Entity.Body);
            p.Entity.CharBuffer._Mesh=ordinary;
            if(Q.Player(p,false))p.Entity.Update((byte)FlagsUpdate.DataType.Mesh,ordinary,true);
        }
        internal static void Tick()
        {
            Look[] pending;lock(gate)pending=looks.Values.ToArray();
            foreach(var look in pending)lock(look.Owner.ItemSyncRoot){
                lock(gate){Look current;if(!looks.TryGetValue(look.Owner.Entity.UID,out current) || !ReferenceEquals(current,look) || Current(look))continue;looks.Remove(look.Owner.Entity.UID);}
                Restore(look.Owner,look.Mesh);
            }
        }
    }
}
