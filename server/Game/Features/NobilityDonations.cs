using System;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class NobilityDonations
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_nobility_reset(id boolean PRIMARY KEY CHECK(id),week date NOT NULL);
CREATE TABLE IF NOT EXISTS revora_nobility_payments(
id uuid PRIMARY KEY,uid bigint NOT NULL,currency integer NOT NULL CHECK(currency BETWEEN 0 AND 2),
amount bigint NOT NULL CHECK(amount>0),donation bigint NOT NULL CHECK(donation>0),
before_donation bigint NOT NULL CHECK(before_donation>=0),after_donation bigint NOT NULL,
paid_at timestamptz NOT NULL,CHECK(after_donation=before_donation+donation));
CREATE INDEX IF NOT EXISTS revora_nobility_recent ON revora_nobility_payments(uid,currency,donation,paid_at DESC);");}
        }
        private sealed class Offer { internal uint Npc,Amount;internal ushort Map;internal bool Bound;internal DateTime Until; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameState,Offer> Offers=new System.Runtime.CompilerServices.ConditionalWeakTable<GameState,Offer>();
        internal static bool HandleNpc(GameState p,Revora.Network.GamePackets.NpcRequest request) {
            if(p==null || request==null)return false;
            uint npc=request.NpcID==0?p.ActiveNpc:request.NpcID;
            if(npc!=900042 && npc!=761124){Offers.Remove(p);return false;}
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                Offer offer;Offers.TryGetValue(p,out offer);Offers.Remove(p);
                if(!Ready(p) || npc!=p.ActiveNpc || !Q.Near(p,npc,p.Entity.MapID) || request.OptionID==255)return true;
                bool bound=npc==761124 || npc==900042 && (request.OptionID==3 || request.OptionID==4);uint minimum=bound?2000000u:100000u;ulong rate=bound?10UL:50000UL;
                var dialog=new Npcs(p);
                if(request.OptionID==1 || npc==900042 && request.OptionID==4) {
                    uint amount;
                    if(uint.TryParse(request.Input,out amount) && amount>=minimum && amount<=(bound?p.Entity.BoundCps:p.Entity.ConquerPoints)) {
                        Offers.Add(p,new Offer{Npc=npc,Amount=amount,Map=p.Entity.MapID,Bound=bound,Until=Clock().AddMinutes(1)});
                        dialog.Text("Offer "+amount+" "+(bound?"Bound CPs":"CPs")+" for "+((ulong)amount*rate)+" nobility donation?");
                        dialog.Option("Make the offering.",2);dialog.Option("Not now.",255);dialog.Send();return true;
                    }
                    dialog.Text("Bring at least "+minimum+" "+(bound?"Bound CPs":"CPs")+" for an offering.");
                } else if(request.OptionID==2) {
                    if(offer!=null && offer.Npc==npc && offer.Map==p.Entity.MapID && Clock()<offer.Until)
                        Pay(p,(ulong)offer.Amount*(offer.Bound?10UL:50000UL),offer.Bound?2u:1u);
                    return true;
                } else if(request.OptionID!=0 && !(npc==900042 && request.OptionID==3))return true;
                else dialog.Text("I accept "+(bound?"Bound CPs":"CPs")+" for nobility. Each is worth "+rate+" donation; the minimum offering is "+minimum+".");
                dialog.Input("Your offering:",(byte)(npc==900042 && bound?4:1),10);
                if(npc==900042)dialog.Option(bound?"Offer CPs instead.":"Offer Bound CPs instead.",(byte)(bound?0:3));
                dialog.Option("Another time.",255);dialog.Send();return true;
            }
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.NobilityInformation!=null && p.Entity.MapID!=3031
            && p.Booth==null && (p.Trade==null || !p.Trade.InTrade);}
        // Currency 2 is available through NPC offers only; the wire handler permits 0/1.
        internal static bool Pay(GameState p,ulong donation,uint currency) {
            if(!Ready(p) || donation==0 || donation>(ulong)long.MaxValue || currency>2)return false;
            ulong amount=currency==0?donation:currency==1?donation/Nobility.CpSilverValue+(donation%Nobility.CpSilverValue==0?0UL:1UL):donation/10;
            if(amount==0 || currency==2 && donation%10!=0 || currency!=0 && amount>uint.MaxValue)return false;
            lock(p.ItemSyncRoot)lock(Nobility.ResetGate)try {
                if(!Ready(p))return false;
                Nobility.Pulse();
                ulong before=p.NobilityInformation.Donation;
                if(before>(ulong)long.MaxValue-donation)return false;
                ulong balance=currency==0?p.Entity.Money:currency==1?p.Entity.ConquerPoints:p.Entity.BoundCps;
                if(balance<amount)return false;
                ulong after=before+donation,next=balance-amount;var now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SELECT pg_advisory_xact_lock(67112064)");
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_nobility_payments
WHERE uid=@uid AND currency=@currency AND donation=@donation AND paid_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"currency",(int)currency,"donation",(long)donation,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return false;
                    bool exists=false;long saved=0;
                    using(var q=Q.Command(c,t,"SELECT donation FROM nobility WHERE entityuid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(r.Read()){exists=true;saved=Convert.ToInt64(r[0]);if(r.Read())throw new InvalidOperationException("Ambiguous nobility owner.");}}
                    if(saved<0 || (ulong)saved!=before)throw new InvalidOperationException("Nobility donation changed.");
                    if(currency==0)SilverBalance.Persist(c,t,p,next);
                    else {
                        string column=currency==1?"conquerpoints":"boundcps";
                        long old=currency==1?PurseBalance.PreimageCp(p):(long)balance;
                        if(Q.Execute(c,t,"UPDATE entities SET "+column+"=@next WHERE uid=@uid AND COALESCE("+column+",0)=@old",
                            "next",(long)next,"uid",(long)p.Entity.UID,"old",old)!=1)throw new InvalidOperationException("Nobility purse changed.");
                    }
                    int changed=exists?Q.Execute(c,t,"UPDATE nobility SET donation=@next WHERE entityuid=@uid AND donation=@before",
                        "next",(long)after,"uid",(long)p.Entity.UID,"before",saved):Q.Execute(c,t,@"INSERT INTO nobility(entityuid,entityname,donation,gender,rey,mesh,duracionrey)
VALUES(@uid,@name,@donation,@gender,0,@mesh,'')","uid",(long)p.Entity.UID,"name",p.Entity.Name,"donation",(long)after,
                        "gender",(int)(p.Entity.Body%10),"mesh",(long)p.Entity.Mesh);
                    if(changed!=1)throw new InvalidOperationException("Nobility donation was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_nobility_payments(id,uid,currency,amount,donation,before_donation,after_donation,paid_at)
VALUES(@id,@uid,@currency,@amount,@donation,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"currency",(int)currency,
                        "amount",(long)amount,"donation",(long)donation,"before",(long)before,"after",(long)after,"now",now)!=1)
                        throw new InvalidOperationException("Nobility payment receipt was not saved.");
                    // Rank evidence uses committed payment inputs and the same UID tie rule as SortCore.
                    if(after>=Nobility.MinimumRankDonation) {
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10318,"nobility-donation",after.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                        long ahead;
                        using(var q=Q.Command(c,t,"SELECT count(*) FROM nobility WHERE donation>@donation OR (donation=@donation AND entityuid<@uid)",
                            "donation",(long)after,"uid",(long)p.Entity.UID))ahead=Convert.ToInt64(q.ExecuteScalar());
                        if(ahead<Nobility.KingCount)
                            ProgressionEvidence.Complete(c,t,p.Entity.UID,11120,"nobility-ranked-king",after.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                    }
                    t.Commit();
                }}
                if(currency==0)SilverBalance.ApplyCommitted(p,next);
                else if(currency==1)p.Entity.ApplyCommittedConquerPoints((uint)next);
                else p.Entity.ApplyCommittedBoundCps((uint)next);
                var entry=p.NobilityInformation;entry.Name=p.Entity.Name;entry.EntityUID=p.Entity.UID;entry.Mesh=p.Entity.Mesh;
                entry.Gender=(byte)(p.Entity.Body%10);entry.Donation=after;p.Entity.NobalityDonation=after;
                Nobility.Board[p.Entity.UID]=entry;Nobility.Sort(p.Entity.UID);
                if(after>=Nobility.MinimumRankDonation)Q.Refresh(p);
                return true;
            }catch(Exception ex){Q.Fail(p,"nobility-donation",ex);return false;}
        }
    }
}
