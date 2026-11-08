using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Revora.Game.Features
{
    // Loaded player health and persisted pill costs share the whole calculation
    // boundary with medicine and saves. Unmanaged fixtures retain their old path.
    internal sealed class HealthMutationLease : IDisposable
    {
        private readonly List<object> gates=new List<object>();
        private uint huntSession;
        internal bool Allowed {get;private set;}
        private bool Own(object gate){
            if(gate==null || !Monitor.TryEnter(gate))return false;
            gates.Add(gate);return true;
        }
        internal static HealthMutationLease Acquire(Entity a,Entity b){
            var lease=new HealthMutationLease();
            try{
                var actors=new[]{a,b}.Where(x=>x!=null && x.EntityFlag==EntityFlag.Player && (x.HealthSynchronizationRequired || HealthBalance.Managed(x.Owner))).Distinct().OrderBy(x=>x.UID).ToArray();
                // Quest death handlers require the killer's session lease. Acquire
                // it before health/item locks so a busy session cannot lose a kill
                // while its quest credit is refused. Contention refuses this hit.
                if(b!=null && b.EntityFlag==EntityFlag.Monster && actors.Contains(a)){
                    if(!SessionInstall.TryEnter(a.UID))return lease;
                    lease.huntSession=a.UID;
                }
                foreach(var body in actors){
                    if(body.Owner==null || !ReferenceEquals(body.Owner.Entity,body) || !lease.Own(body.Owner.ItemSyncRoot))return lease;
                }
                foreach(var body in actors)if(!lease.Own(body.HealthMutationSync))return lease;
                lease.Allowed=true;return lease;
            }catch{lease.Dispose();throw;}
        }
        internal static uint Heal(Entity target,uint requested){
            using(var lease=Acquire(target,null)){
                if(!lease.Allowed || target==null || target.Dead || target.Hitpoints>=target.MaxHitpoints)return 0;
                uint amount=Math.Min(requested,target.MaxHitpoints-target.Hitpoints);
                target.Hitpoints+=amount;return amount;
            }
        }
        public void Dispose(){
            for(int i=gates.Count-1;i>=0;i--)Monitor.Exit(gates[i]);gates.Clear();
            if(huntSession!=0){SessionInstall.Exit(huntSession);huntSession=0;}
        }
    }
}
