using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.DragonSlayerWorld;

namespace Revora.Game.Features
{
    internal static class DragonSlayerHunt
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Run {internal Guid Id;internal int Branch,Mask;internal string Phase;internal uint Material;}
        internal static void Initialize() {
            foreach(var b in W.Branches)foreach(uint id in new[]{b.Grass,b.Pellet,b.Pill})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Dragon Slayer item catalog is incomplete.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dragon_hunts(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),branch integer NOT NULL CHECK(branch BETWEEN 0 AND 6),
phase varchar(12) NOT NULL CHECK(phase IN('minions','pellet','ghoul','victory','complete')),
mask integer NOT NULL CHECK(mask BETWEEN 0 AND 511),material bigint NOT NULL CHECK(material>=0),
created_at timestamptz NOT NULL DEFAULT now(),updated_at timestamptz NOT NULL DEFAULT now(),
CHECK((phase IN('minions','pellet') AND material>0) OR (phase IN('ghoul','victory','complete') AND material=0)),
CHECK(phase='minions' OR mask=511));
CREATE UNIQUE INDEX IF NOT EXISTS revora_dragon_active_hunt ON revora_dragon_hunts(uid) WHERE phase<>'complete';
CREATE TABLE IF NOT EXISTS revora_dragon_hunt_kills(
run uuid NOT NULL REFERENCES revora_dragon_hunts(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 9),
monster_uid bigint NOT NULL,at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(run,slot));");}
            W.Initialize();
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade && p.Entity.MapID==W.MapId && p.Map.ID==W.MapId && p.Map.BaseID==W.MapId;}
        private static bool Guardian(GameState p) {return Ready(p) && Q.Near(p,DragonPillCrafting.Guardian,W.MapId);}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(Convert.ToUInt32(q.ExecuteScalar())!=W.MapId)throw new InvalidOperationException("Dragon Slayer owner left the floor.");
            using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Dragon Slayer inventory requires reload.");
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT id,branch,phase,mask,material FROM revora_dragon_hunts WHERE uid=@uid AND phase<>'complete' FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Id=r.GetGuid(0),Branch=r.GetInt32(1),Phase=r.GetString(2),Mask=r.GetInt32(3),Material=checked((uint)r.GetInt64(4))}:null;
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            if(Q.Execute(c,t,"UPDATE revora_dragon_hunts SET phase=@phase,mask=@mask,material=@item,updated_at=now() WHERE id=@id AND uid=@uid",
                "phase",r.Phase,"mask",r.Mask,"item",(long)r.Material,"id",r.Id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Dragon Slayer progress was not saved.");
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,1,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                "dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)throw new InvalidOperationException("Dragon Slayer item was not saved.");
        }
        private static bool Owned(GameState p,Run r,uint type,out ConquerItem item) {
            return p.Inventory.TryGetItem(r.Material,out item) && item.ID==type && item.Position==0 && item.Warehouse==0 && item.StackSize==1
                && item.Bound && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0;
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0
AND stacksize=1 AND bound=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                "item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Dragon Slayer material ownership changed.");
        }
        private static string Status(Run r) {
            if(r==null)return "Choose a ghoul to hunt.";var b=W.Branches[r.Branch];
            return r.Phase=="minions"?(r.Mask==511?"The nine minions have fallen. Bring the grass back for your pellet.":"Use your grass on this floor. Defeat "+(9-W.Count(r.Mask))+" more "+b.Name+" Minions, then bring it back.")
                :r.Phase=="pellet"?"Use your pellet near the "+b.Name+" statue at ("+b.X+","+b.Y+")."
                :r.Phase=="ghoul"?"Defeat your "+b.Name+" Ghoul. Its statue can awaken it again if your hunt was interrupted."
                :"Your ghoul is defeated. Make room and claim its pill.";
        }
        private static void Begin(GameState p,int branch,Npcs dialog) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);var old=Read(c,t,p);if(old!=null){dialog.Text(Status(old));return;}
                if(p.Inventory.Count>=40){dialog.Text("Make one space for the grass.");return;}
                var b=W.Branches[branch];var item=Q.New(b.Grass);
                Insert(c,t,p,item);if(Q.Execute(c,t,"INSERT INTO revora_dragon_hunts(id,uid,branch,phase,mask,material) VALUES(@id,@uid,@branch,'minions',0,@item)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"branch",branch,"item",(long)item.UID)!=1)throw new InvalidOperationException("Dragon Slayer hunt was not saved.");
                t.Commit();Q.Install(p,new uint[0],new[]{item});dialog.Text("Use this grass on the sixth floor to lure nine "+b.Name+" Minions.");
            }}
        }
        private static ConquerItem Reward(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            if(r.Phase!="victory" || p.Inventory.Count>=40)return null;
            var item=Q.New(W.Branches[r.Branch].Pill);Insert(c,t,p,item);r.Phase="complete";return item;
        }
        private static void TurnIn(GameState p,bool recover,Npcs dialog) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);var r=Read(c,t,p);if(r==null){dialog.Text(Status(null));return;}
                ConquerItem added=null;uint removed=0;
                if(!recover && r.Phase=="minions" && r.Mask==511) {
                    ConquerItem grass;if(!Owned(p,r,W.Branches[r.Branch].Grass,out grass)){dialog.Text("Bring back your grass.");return;}
                    Consume(c,t,p,grass);removed=grass.UID;added=Q.New(W.Branches[r.Branch].Pellet);Insert(c,t,p,added);r.Material=added.UID;r.Phase="pellet";
                }else if(r.Phase=="victory")added=Reward(c,t,p,r);
                else if(recover && (r.Phase=="minions" || r.Phase=="pellet")) {
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)","item",(long)r.Material))
                        if(Equals(q.ExecuteScalar(),false) && !p.Inventory.ContainsUID(r.Material) && p.Inventory.Count<40) {
                            added=Q.New(r.Phase=="minions"?W.Branches[r.Branch].Grass:W.Branches[r.Branch].Pellet);Insert(c,t,p,added);r.Material=added.UID;
                        }
                }
                Save(c,t,p,r);t.Commit();if(added!=null)Q.Install(p,removed==0?new uint[0]:new[]{removed},new[]{added});
                if(r.Phase=="pellet" || r.Phase=="complete")W.Release(p);
                dialog.Text(r.Phase=="complete"?"Your ghoul pill is yours. Bring all seven types for a Dragon Pill.":Status(r));
            }}
        }
        internal static bool Menu(GameState p,byte option,Npcs dialog) {
            if(option!=20 && option!=21 && option!=40 && option!=42 && (option<30 || option>36))return false;
            if(!Guardian(p))return true;
            if(option==20){dialog.Text("Choose one ghoul hunt. Use its grass to lure nine minions, then return for a pellet.");foreach(var b in W.Branches)dialog.Option(b.Name+" hunt.",(byte)(30+b.Index));}
            else if(option>=30 && option<=36)Begin(p,option-30,dialog);
            else if(option==40 || option==42)TurnIn(p,option==42,dialog);
            else {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);var r=Read(c,t,p);t.Commit();dialog.Text(Status(r));}}
                dialog.Option("Exchange my grass.",40);dialog.Option("Claim or recover my hunt item.",42);}
            return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || !(item.ID>=711189 && item.ID<=711195 || item.ID>=720591 && item.ID<=720597))return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p))return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);var r=Read(c,t,p);ConquerItem owned;
                    if(r==null || !Owned(p,r,item.ID,out owned) || !ReferenceEquals(item,owned)){p.Send("Ask the Guardian for your own hunt.");return true;}
                    var b=W.Branches[r.Branch];bool ghoul=r.Phase=="pellet" && item.ID==b.Pellet;
                    if(!ghoul && (r.Phase!="minions" || item.ID!=b.Grass || r.Mask==511)){p.Send(Status(r));return true;}
                    if(ghoul && !NearStatue(p,b)){p.Send(Status(r));return true;}
                    lock(W.Gate) {
                        var hunt=W.Prepare(p,r.Id,r.Branch,r.Mask,ghoul);if(hunt==null){p.Send("There is no room for another hunt here. Try again shortly.");return true;}
                        try {
                            if(ghoul){Consume(c,t,p,item);r.Material=0;r.Phase="ghoul";Save(c,t,p,r);}
                            t.Commit();if(ghoul)Q.Install(p,new[]{item.UID},new ConquerItem[0]);W.Publish(hunt,r.Mask);
                        }catch{W.Release(hunt);throw;}
                    }
                }}
            }catch(Exception ex){Q.Fail(p,"dragon-hunt-use",ex);}return true;
        }
        private static bool NearStatue(GameState p,W.Branch b) {
            Revora.Interfaces.INpc npc;return Ready(p) && p.Map.Npcs.TryGetValue(b.Statue,out npc) && npc.X==b.X && npc.Y==b.Y && npc.Mesh==b.StatueType*10
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.X,b.Y)<=6;
        }
        internal static bool HandleStatue(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc<W.StatueFirst || p.ActiveNpc>=W.StatueFirst+7)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                var b=W.Branches[p.ActiveNpc-W.StatueFirst];if((request.NpcID!=0 && request.NpcID!=b.Statue) || !NearStatue(p,b))return true;
                if(request.OptionID==0){var d=new Npcs(p);d.Text("Use the matching pellet here. If you already offered it, I can awaken your ghoul again.");d.Option("Resume my ghoul hunt.",1);d.Option("Leave the seal.",255);d.Send();return true;}
                if(request.OptionID!=1)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);var r=Read(c,t,p);t.Commit();
                    if(r==null || r.Branch!=b.Index || r.Phase!="ghoul"){p.Send(Status(r));return true;}
                    lock(W.Gate){var hunt=W.Prepare(p,r.Id,r.Branch,r.Mask,true);if(hunt==null)p.Send("There is no room for another hunt here. Try again shortly.");
                        else try{W.Publish(hunt,r.Mask);}catch{W.Release(hunt);throw;}}
                }}
            }catch(Exception ex){Q.Fail(p,"dragon-hunt-statue",ex);}return true;
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!W.IsEncounter(mob))return false;W.Actor actor;if(!W.Actors.TryGetValue(mob.UID,out actor) || !ReferenceEquals(actor.Body,mob))return true;
            var p=actor.Hunt.Player;if(!Ready(p) || !W.OwnFighter(p,killer))return true;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p) || !W.OwnFighter(p,killer) || actor.Hunt.Released || mob.Dead || Clock()>=actor.Hunt.Until || Kernel.GetDistance(p.Entity.X,p.Entity.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                Entity canonical;if(!p.Map.Entities.TryGetValue(mob.UID,out canonical) || !ReferenceEquals(canonical,mob))return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);var r=Read(c,t,p);if(r==null || r.Id!=actor.Hunt.Run || r.Branch!=actor.Hunt.Branch.Index || r.Phase!=(actor.Hunt.Ghoul?"ghoul":"minions"))return true;
                    if(!actor.Hunt.Ghoul && (r.Mask&(1<<actor.Slot))!=0)return true;
                    if(Q.Execute(c,t,"INSERT INTO revora_dragon_hunt_kills(run,slot,monster_uid) VALUES(@run,@slot,@mob)","run",r.Id,"slot",actor.Slot,"mob",(long)mob.UID)!=1)
                        throw new InvalidOperationException("Dragon Slayer defeat was not recorded.");
                    if(actor.Hunt.Ghoul)r.Phase="victory";else r.Mask|=1<<actor.Slot;
                    var reward=Reward(c,t,p,r);Save(c,t,p,r);
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,mob,killer,"dragon:"+r.Id.ToString("N")+":"+actor.Slot);
                    t.Commit();if(reward!=null)Q.Install(p,new uint[0],new[]{reward});
                    mob.DieCore(p.Entity);Q.Refresh(p);W.Remove(actor);if(r.Phase=="complete" || r.Phase=="victory")W.Release(actor.Hunt);
                    p.Send(r.Phase=="complete"?"You recovered the "+W.Branches[r.Branch].Name+" Pill.":Status(r));
                }}
            }catch(Exception ex){Q.Fail(p,"dragon-hunt-defeat",ex);}return true;
        }
    }
}
