using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PaidReincarnation
    {
        private sealed class Offer {
            internal bool Buy; internal Guid Id; internal DateTime Until; internal uint Before,Cost,Dew;
            internal string Cache,Items,Skills,Profs;
        }
        private sealed class Gear { internal ConquerItem Item; internal uint Type; internal byte Position; }
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_reincarnations(
id uuid PRIMARY KEY,sequence bigserial UNIQUE,uid bigint NOT NULL,
first_class integer NOT NULL,first_level integer NOT NULL,old_class integer NOT NULL,old_level integer NOT NULL CHECK(old_level>=110),
old_experience bigint NOT NULL CHECK(old_experience>=0),new_class integer NOT NULL,old_body integer NOT NULL,new_body integer NOT NULL,
dew_uid bigint NOT NULL,dew_stack integer NOT NULL,bound boolean NOT NULL,attribute_budget bigint NOT NULL CHECK(attribute_budget>=0),
cost bigint NOT NULL CHECK(cost>=0),balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after=balance_before-cost AND balance_after>=0),
created_at timestamptz NOT NULL DEFAULT now(),recovered_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_reincarnation_pending ON revora_reincarnations(uid) WHERE recovered_at IS NULL;
CREATE INDEX IF NOT EXISTS revora_reincarnation_latest ON revora_reincarnations(uid,sequence DESC);
CREATE TABLE IF NOT EXISTS revora_oblivion_purchases(id uuid PRIMARY KEY,uid bigint NOT NULL,item_uid bigint NOT NULL UNIQUE,
cost bigint NOT NULL CHECK(cost=2000),balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after=balance_before-cost AND balance_after>=0),
created_at timestamptz NOT NULL DEFAULT now());");}
        }
        private static bool Near(GameState p) {
            return Q.Near(p,HeavenlyStoneCrafting.Sereth,1002) && p.Map.ID==1002 && p.Map.BaseID==1002
                && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);
        }
        private static bool Dew(ConquerItem i) {
            return i.ID==711083 && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;
        }
        private static bool Recovered(GameState p) {
            ReincarnationRecovery.RestoreReady(p);
            var old=ReincarnationRecovery.Snapshot(p);
            if(old!=null && !old.Recovered)return false;
            // The paid receipt already checked older recovery obligations when it
            // committed. Later history belongs to it, not the original second rite.
            if(old!=null && old.PaidReincarnation)return true;
            var second=SecondRebirthRecovery.Snapshot(p);
            return second==null || second.Recovered;
        }
        private static void Prepare(GameState p,bool buy,Npcs dialog) {
            offers.Remove(p);
            if(!buy && !Recovered(p)){dialog.Text("Recover your former strength before beginning another life.");return;}
            var dew=p.Inventory.Objects.Where(Dew).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
            uint cost=buy?2000u:Config.SerethReincarnationCp;
            if(p.Entity.ConquerPoints<cost || !buy && dew==null){dialog.Text("Bring "+cost.ToString("N0")+" CPs"+(buy?" for the dew.":" and unlocked Oblivion Dew."));return;}
            if(buy && p.Inventory.Count>=40){dialog.Text("Make room in your bag for the dew.");return;}
            var offer=new Offer{Buy=buy,Id=Guid.NewGuid(),Until=Clock().AddMinutes(2),Before=p.Entity.ConquerPoints,Cost=cost,Dew=dew==null?0:dew.UID,Cache=FirstRebirth.Cache(p)};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);FirstRebirth.AssertLoaded(c,t,p);
                offer.Items=FirstRebirth.Rows(c,t,p,"items");offer.Skills=FirstRebirth.Rows(c,t,p,"skills");offer.Profs=FirstRebirth.Rows(c,t,p,"profs");t.Commit();
            }}
            offers.Add(p,offer);
            if(buy){dialog.Text("One Oblivion Dew costs 2,000 CPs. Shall we trade?");dialog.Option("Buy the dew.",7);}
            else {
                p.Send(new Data(true){UID=p.Entity.UID,ID=Data.OpenWindow,dwParam=Data.WindowCommands.Reincarnation,wParam1=p.Entity.X,wParam2=p.Entity.Y});
                dialog.Text("Choose your next path. The rite costs "+cost.ToString("N0")+" CPs and one Oblivion Dew.");
            }
        }
        private static bool Same(GameState p,Offer o) {
            return Near(p) && Clock()<o.Until && p.Entity.ConquerPoints==o.Before && o.Cache==FirstRebirth.Cache(p)
                && (o.Buy || o.Cost==Config.SerethReincarnationCp);
        }
        private static void Preimage(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Offer o) {
            SkillTable.LockOwner(c,t,p);
            if(FirstRebirth.Rows(c,t,p,"items")!=o.Items || FirstRebirth.Rows(c,t,p,"skills")!=o.Skills || FirstRebirth.Rows(c,t,p,"profs")!=o.Profs)
                throw new InvalidOperationException("Reincarnation belongings changed.");
            if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND COALESCE(conquerpoints,0)=@before AND mapid=1002 AND body=@body",
                "after",(long)(o.Before-o.Cost),"uid",(long)p.Entity.UID,"before",(long)o.Before,"body",(int)p.Entity.Body)!=1)
                throw new InvalidOperationException("Reincarnation payment or owner changed.");
        }
        private static void Buy(GameState p,Npcs dialog) {
            Offer o;if(!offers.TryGetValue(p,out o))return;offers.Remove(p);
            if(!o.Buy || !Same(p,o) || p.Inventory.Count>=40)return;
            var item=Q.New(711083,false);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Preimage(c,t,p,o);
                if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,711083,@uid,0,0,1,0,@dur,@max)","item",(long)item.UID,"uid",(long)p.Entity.UID,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1
                    || Q.Execute(c,t,"INSERT INTO revora_oblivion_purchases(id,uid,item_uid,cost,balance_before,balance_after) VALUES(@id,@uid,@item,@cost,@before,@after)",
                        "id",o.Id,"uid",(long)p.Entity.UID,"item",(long)item.UID,"cost",(long)o.Cost,"before",(long)o.Before,"after",(long)(o.Before-o.Cost))!=1)
                    throw new InvalidOperationException("Oblivion purchase was not saved.");
                if(!Same(p,o))throw new InvalidOperationException("Oblivion purchase session changed.");t.Commit();
            }}
            p.Entity.ApplyCommittedConquerPoints(o.Before-o.Cost);Q.Install(p,new uint[0],new[]{item});dialog.Text("The dew is yours. Choose your next life wisely.");
        }
        internal static Spell[] Skills(GameState p,byte next) {
            // Only the immediately previous life's summon survives reincarnation.
            ushort summon=p.Entity.Class/10==1?(ushort)4050:p.Entity.Class/10==2?(ushort)4060:p.Entity.Class/10==4?(ushort)4070:
                p.Entity.Class/10==13?(ushort)4010:p.Entity.Class/10==14?(ushort)4020:(ushort)0;
            ushort[] summons={4010,4020,4050,4060,4070};
            return SecondRebirthSkills.PlanHistory(p,p.Entity.SecondRebornClass,p.Entity.Class,next)
                .Where(s=>!summons.Contains(s.ID) || s.ID==summon).ToArray();
        }
        internal static void Process(byte[] packet,GameState p) {
            if(!Q.Player(p))return;
            lock(p.ItemSyncRoot)try {
                Offer o;if(!offers.TryGetValue(p,out o))return;offers.Remove(p);
                if(o.Buy || !Same(p,o) || !ReincarnationRules.Eligible(p.Entity.Class,p.Entity.Level,p.Entity.Reborn)
                    || Array.IndexOf(RebirthRules.Classes,packet[8])<0)return;
                ushort body;
                try{body=ReincarnationRules.Body(p.Entity.Body,packet[12]);}catch(ArgumentException){return;}
                var dew=p.Inventory.Objects.SingleOrDefault(i=>i.UID==o.Dew && Dew(i));if(dew==null)return;
                var before=CharacterProgression.Capture(p);var profile=ReincarnationRules.Profile(before,packet[8],Config.AttributePointsPerLevel);
                var gear=p.Equipment.GetItems().Select(i=>new Gear{Item=i,Type=FirstRebirth.Lower(i),Position=i.Position}).ToArray();
                if(p.Inventory.Count-(dew.StackSize<=1?1:0)+gear.Count(g=>g.Position==5 || g.Position==25)>40){p.Send("Make room in your bag for off-hand equipment.");return;}
                var skills=Skills(p,packet[8]);
                var profs=p.Proficiencies.Values.Select(s=>new Proficiency(true){ID=s.ID,PreviousLevel=Math.Max(s.Level,s.PreviousLevel),Level=0,Experience=0,Available=s.Available}).ToArray();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Preimage(c,t,p,o);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM reincarnation WHERE uid=@uid","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Former reincarnation recovery is pending.");
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid AND old_level>=130 AND recovered_at IS NULL","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Former second-life recovery is pending.");
                    AchievementCatalog.PreserveMasteries(c, t, p);
                    CharacterProgression.Persist(c,t,p,profile);
                    if(Q.Execute(c,t,"UPDATE entities SET body=@body WHERE uid=@uid AND body=@old","body",(int)body,"uid",(long)p.Entity.UID,"old",(int)p.Entity.Body)!=1)
                        throw new InvalidOperationException("Reincarnation body changed.");
                    string debit=dew.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,debit+" WHERE uid=@item AND entityid=@uid AND id=711083 AND position=0 AND warehouse=0 AND COALESCE(stacksize,0)=@stack",
                        "item",(long)dew.UID,"uid",(long)p.Entity.UID,"stack",(int)dew.StackSize)!=1)throw new InvalidOperationException("Reincarnation dew changed.");
                    foreach(var g in gear)if(Q.Execute(c,t,"UPDATE items SET id=@type,position=@next WHERE uid=@item AND entityid=@uid AND id=@old AND position=@slot AND warehouse=0",
                        "type",(int)g.Type,"next",g.Position==5 || g.Position==25?0:(int)g.Position,"item",(long)g.Item.UID,"uid",(long)p.Entity.UID,"old",(int)g.Item.ID,"slot",(int)g.Position)!=1)
                        throw new InvalidOperationException("Reincarnation equipment changed.");
                    foreach(var old in p.Spells.Values.Where(s=>!SkillTable.DerivedRuneSkill(s.ID) && !skills.Any(n=>n.ID==s.ID)))
                        if(old.Available && Q.Execute(c,t,"DELETE FROM skills WHERE entityid=@uid AND id=@id","uid",(long)p.Entity.UID,"id",(int)old.ID)!=1)
                            throw new InvalidOperationException("Reincarnation skill removal failed.");
                    foreach(var skill in skills)SkillTable.Persist(c,t,p,skill);
                    foreach(var prof in profs) {
                        string sql=prof.Available?"UPDATE profs SET level=0,previouslevel=@previous,experience=0 WHERE entityid=@uid AND id=@id"
                            :"INSERT INTO profs(entityid,id,level,previouslevel,experience) VALUES(@uid,@id,0,@previous,0)";
                        if(Q.Execute(c,t,sql,"uid",(long)p.Entity.UID,"id",(int)prof.ID,"previous",(int)prof.PreviousLevel)!=1)
                            throw new InvalidOperationException("Reincarnation proficiency reset failed.");
                    }
                    if(Q.Execute(c,t,"INSERT INTO reincarnation(uid,level,experience) VALUES(@uid,@level,@exp)","uid",(long)p.Entity.UID,"level",(int)p.Entity.Level,"exp",checked((long)p.Entity.Experience))!=1
                        || Q.Execute(c,t,@"INSERT INTO revora_reincarnations(id,uid,first_class,first_level,old_class,old_level,old_experience,new_class,old_body,new_body,
dew_uid,dew_stack,bound,attribute_budget,cost,balance_before,balance_after) VALUES(@id,@uid,@first,@firstlevel,@old,@level,@exp,@next,@oldbody,@body,@dew,@stack,@bound,@budget,@cost,@before,@after)",
                            "id",o.Id,"uid",(long)p.Entity.UID,"first",(int)p.Entity.SecondRebornClass,"firstlevel",(int)p.Entity.SecondRebornLevel,
                            "old",(int)p.Entity.Class,"level",(int)p.Entity.Level,"exp",checked((long)p.Entity.Experience),"next",(int)packet[8],"oldbody",(int)p.Entity.Body,"body",(int)body,
                            "dew",(long)dew.UID,"stack",(int)dew.StackSize,"bound",dew.Bound,"budget",profile.Values.Skip(2).Take(5).Sum(),
                            "cost",(long)o.Cost,"before",(long)o.Before,"after",(long)(o.Before-o.Cost))!=1)
                        throw new InvalidOperationException("Reincarnation recovery receipt was not saved.");
                    if(!Same(p,o))throw new InvalidOperationException("Reincarnation session changed.");t.Commit();
                }}
                PaidReincarnationRecovery.Invalidate(p);
                Kernel.ReincarnatedCharacters[p.Entity.UID]=new Features.Reincarnation.ReincarnateInfo{UID=p.Entity.UID,Level=p.Entity.Level,Experience=p.Entity.Experience};
                p.Entity.ApplyCommittedConquerPoints(o.Before-o.Cost);
                Q.Install(p,dew.StackSize<=1?new[]{dew.UID}:new uint[0],new ConquerItem[0]);
                if(dew.StackSize>1){dew.StackSize--;dew.Mode=Enums.ItemMode.Update;dew.Send(p);dew.Mode=Enums.ItemMode.Default;}
                foreach(var g in gear){g.Item.ID=g.Type;if(g.Position==5 || g.Position==25){
                    if(!p.Equipment.Remove(g.Position,true) || !p.Inventory.Add(g.Item,Enums.ItemUse.None))throw new InvalidOperationException("Committed reincarnation equipment requires reload.");
                }g.Item.Mode=Enums.ItemMode.Update;g.Item.Send(p);g.Item.Mode=Enums.ItemMode.Default;}
                foreach(var old in p.Spells.Values.ToArray())if(!skills.Any(s=>s.ID==old.ID)){p.Spells.Remove(old.ID);p.Send(new Data(true){UID=p.Entity.UID,dwParam=old.ID,ID=0x6d});}
                foreach(var s in skills){s.Available=true;p.Spells[s.ID]=s;if(s.ID!=3060)s.Send(p);}
                foreach(var s in profs){s.Available=true;p.Proficiencies[s.ID]=s;s.Send(p);}
                p.Entity.Body=body;CharacterProgression.ApplyCommitted(p,profile);
                RebirthSkillRules.ClearAssassinModes(p);RebirthSkillRules.ClearNinjaModes(p);RebirthSkillRules.ClearMonkModes(p);RebirthSkillRules.ClearPirateModes(p);WarriorModes.Clear(p);
                p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();p.Equipment.UpdateEntityPacket();
                PathHuntSystem.SyncQuestList(p);SoulTrialSystem.SyncQuestList(p);CourierLoopSystem.SyncQuestList(p);Q.Refresh(p);
                p.Send("Your new life begins. Reach level 110 to recover your former strength.");
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"reincarnation",ex);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ItemSyncRoot==null)return false;
            bool handled=p.ActiveNpc==HeavenlyStoneCrafting.Sereth && (request.OptionID==3 || request.OptionID==5 || request.OptionID==6 || request.OptionID==7);
            lock(p.ItemSyncRoot){
                if(!handled){offers.Remove(p);return false;}
                if(request.NpcID!=0 && request.NpcID!=HeavenlyStoneCrafting.Sereth || !Near(p)){offers.Remove(p);return true;}
                try {
                    var dialog=new Npcs(p);dialog.Avatar(15);
                    if(!ReincarnationRules.Eligible(p.Entity.Class,p.Entity.Level,p.Entity.Reborn)){offers.Remove(p);dialog.Text("Return in your third life at level 110.");}
                    else if(request.OptionID==3){offers.Remove(p);dialog.Text("Oblivion Dew and "+Config.SerethReincarnationCp.ToString("N0")+" CPs open another path. Your strength returns at level 110.");dialog.Option("Choose my next life.",5);dialog.Option("Buy Oblivion Dew.",6);}
                    else if(request.OptionID==7)Buy(p,dialog);else {
                        Prepare(p,request.OptionID==6,dialog);
                        Offer opened;if(request.OptionID==5 && offers.TryGetValue(p,out opened) && !opened.Buy)return true;
                    }
                    dialog.Option("Farewell.",255);dialog.Send();
                }catch(Exception ex){offers.Remove(p);Q.Fail(p,"reincarnation-npc",ex);}
            }return true;
        }
    }
}
