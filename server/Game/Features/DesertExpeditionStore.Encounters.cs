using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertExpeditionStore
    {
        internal sealed class Trap {internal Guid Id;internal int Site;internal ushort X,Y;internal DateTime Until;}
        private static readonly ushort[,] TrapSites={{172,399},{102,292},{109,369}};
        private static Trap ReadTrap(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT id,site,until_at FROM revora_desert_expedition_traps WHERE uid=@uid AND life=@life AND NOT used FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader()){
                if(!r.Read())return null;int site=r.GetInt16(1);var until=r.GetDateTime(2).ToUniversalTime();
                var now=PillNow();if(now<until.AddMinutes(-5))throw new InvalidOperationException("Desert Expedition trap clock moved backwards.");
                return now>=until?null:new Trap{Id=r.GetGuid(0),Site=site,X=TrapSites[site,0],Y=TrapSites[site,1],Until=until};
            }
        }
        internal static Trap ActiveTrap(GameState p){
            if(!Eligible(p))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return ReadTrap(c,null,p);}
        }
        internal static bool PlaceTrap(GameState p,uint mirror,int site,Func<bool> context){
            Ready(p,context);if(site<0 || site>=3 || p.Entity.MapID!=1001 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,TrapSites[site,0],TrapSites[site,1])>3)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || !Material(c,t,p,mirror,721921) || Materials(c,t,p,710261).Count>=3 || ReadTrap(c,t,p)!=null)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_expedition_encounter_kills WHERE uid=@uid AND life=@life AND kind=0 AND won AND site=@site)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"site",site))
                    if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_desert_expedition_traps(uid,life,id,site,until_at,used) VALUES(@uid,@life,@id,@site,@until,false)
ON CONFLICT(uid,life) DO UPDATE SET id=@id,site=@site,until_at=@until,used=false",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"id",Guid.NewGuid(),"site",site,"until",PillNow().AddMinutes(5))!=1)
                    throw new InvalidOperationException("Desert Expedition trap placement changed.");
                Ready(p,()=>context() && p.Entity.MapID==1001 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,TrapSites[site,0],TrapSites[site,1])<=3);t.Commit();
            }}return true;
        }
        internal static int EncounterKind(GameState p){
            if(!Eligible(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);
                if(run.Stage==3 && Materials(c,null,p,710261).Count<3 && Materials(c,null,p,721921).Any(id=>Material(c,null,p,id,721921)) && ReadTrap(c,null,p)!=null)return 0;
                return run.Stage==4 && Materials(c,null,p,710260).Count==0?1:-1;
            }
        }
        internal static bool EncounterKill(GameState p,Entity body,Guid attempt,int kind,Guid trapId,Func<bool> context){
            Ready(p,context);if(!DesertExpeditionEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(kind<0 || kind>1 || run.Stage!=(kind==0?3:4))return false;
                uint type=kind==0?710261u:710260u;if(Materials(c,t,p,type).Count>=(kind==0?3:1))return false;
                var trap=kind==0?ReadTrap(c,t,p):null;
                bool won=kind==1 || trap!=null && trap.Id==trapId && Kernel.GetDistance(body.X,body.Y,trap.X,trap.Y)<=1;
                if(kind==0 && !Materials(c,t,p,721921).Any(id=>Material(c,t,p,id,721921)))return false;
                if(won && p.Inventory.Count>=40)return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_desert_expedition_encounter_kills(attempt,uid,life,kind,body,won,site) VALUES(@attempt,@uid,@life,@kind,@body,@won,@site) ON CONFLICT DO NOTHING",
                    "attempt",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kind",kind,"body",(long)body.UID,"won",won,"site",trap==null?-1:trap.Site)!=1)return false;
                if(won){
                    if(kind==0 && Q.Execute(c,t,"UPDATE revora_desert_expedition_traps SET used=true WHERE uid=@uid AND life=@life AND id=@id AND NOT used","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"id",trap.Id)!=1)
                        throw new InvalidOperationException("Desert Expedition trap capture changed.");
                    added=Q.New(type,true);Track(c,t,p,added);
                }
                Ready(p,()=>context() && DesertExpeditionEncounters.Owns(p,body,attempt,kind));t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
