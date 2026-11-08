using System;
using Npgsql;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class EquipmentAchievements
    {
        // Called only inside an owned encounter's victory transaction, before commit.
        internal static void RecordUnarmedVictory(NpgsqlConnection c,NpgsqlTransaction t,Revora.Client.GameState p,Entity monster,Entity killer,string key) {
            if(!QuestInventory.Player(p) || !ReferenceEquals(killer,p.Entity) || killer.Companion || monster==null || monster.Companion
                || monster.EntityFlag!=EntityFlag.Monster || monster.MonsterInfo==null || monster.MonsterInfo.Level<120 || p.Equipment==null)return;
            if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Unarmed victory requires its owner lock.");
            byte right=p.AlternateEquipment?ConquerItem.AlternateRightWeapon:ConquerItem.RightWeapon;
            byte left=p.AlternateEquipment?ConquerItem.AlternateLeftWeapon:ConquerItem.LeftWeapon;
            if(p.Equipment.TryGetItem(right)!=null || p.Equipment.TryGetItem(left)!=null)return;
            using(var q=QuestInventory.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position IN(@right,@left) FOR UPDATE",
                "uid",(long)p.Entity.UID,"right",(int)right,"left",(int)left))
            using(var r=q.ExecuteReader())if(r.Read())return;
            ProgressionEvidence.Complete(c,t,p.Entity.UID,10525,"unarmed-monster-victory",key,1);
        }
        internal static void Record(NpgsqlConnection c, NpgsqlTransaction t, uint uid, DateTime utc)
        {
            RecordBank(c,t,uid,utc,0,null);
        }
        // Called under the item lock after an actual primary -> alternate transition.
        internal static void RecordSwitch(Revora.Client.GameState p)
        {
            if(!QuestInventory.Player(p) || !p.AlternateEquipment)return;
            if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Equipment switch requires its owner lock.");
            try {
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    RecordBank(c,t,p.Entity.UID,DateTime.UtcNow,20,p);
                    t.Commit();
                }}
                QuestInventory.Refresh(p);
            } catch(Exception ex) {QuestInventory.Fail(p,"alternate-equipment",ex);}
        }
        private static void RecordBank(NpgsqlConnection c,NpgsqlTransaction t,uint uid,DateTime utc,int offset,Revora.Client.GameState player)
        {
            bool garment=false,talisman=false,accessory=false,super=false,enchanted=false,socketed=false;
            const int core=(1<<1)|(1<<2)|(1<<3)|(1<<4)|(1<<6)|(1<<8);
            int required=core,seen=0,superGems=0;
            bool invalidSet=false,uniqueSet=true,eliteSet=true,superSet=true,plusSet=true,blessSet=true,socketSet=true,enchantSet=true,combinedSet=true;
            using(var q=QuestInventory.Command(c,t,@"SELECT id,position-@offset,COALESCE(NULLIF(days,''),'0'),daystamp,
COALESCE(enchant,0),COALESCE(socketone,0),COALESCE(sockettwo,0),COALESCE(plus,0),COALESCE(bless,0),
COALESCE(stacksize,0),count(*) OVER(PARTITION BY position),uid
FROM items WHERE entityid=@uid AND warehouse=0 AND position-@offset IN(1,2,3,4,5,6,8,9,10,11,15,16,18,19)","uid",(long)uid,"offset",offset))
            using(var r=q.ExecuteReader()) while(r.Read()) {
                uint id=Convert.ToUInt32(r[0]);int slot=Convert.ToInt32(r[1]);byte days;
                bool setSlot=(core&(1<<slot))!=0 || slot==5;
                bool arrow=slot==5 && ItemHandler.IsArrow(id);
                if(slot==5 && !arrow)required|=1<<5;
                // Inspect every occupied slot before filtering: a malformed offhand must
                // not silently become an absent, optional offhand.
                bool validRow=Convert.ToInt64(r[10])==1 && Convert.ToInt32(r[9])==1;
                if(player!=null && setSlot) {
                    var worn=player.Equipment.TryGetItem((byte)(slot+offset));
                    validRow &= worn!=null && worn.UID==Convert.ToUInt32(r[11]) && worn.ID==id && worn.Position==slot+offset;
                }
                int category=(int)ItemHandler.GetPositionFromID(id);
                bool validSlot=category==slot || (slot==15 || slot==16) && ItemHandler.IsAccessory(id)
                    || slot==5 && category==4 && !ItemHandler.IsTwoHand(id) && id/1000!=421;
                if(!validRow || !ConquerItemInformation.BaseInformations.ContainsKey(id)
                    || !byte.TryParse(Convert.ToString(r[2]),out days)
                    || !validSlot){if(setSlot)invalidSet=true;continue;}
                if(days>0) {
                    if(r.IsDBNull(3)){if(setSlot)invalidSet=true;continue;}
                    try {if(DateTime.FromBinary(Convert.ToInt64(r[3])).AddDays(days).ToUniversalTime()<=utc){if(setSlot)invalidSet=true;continue;}}
                    catch(ArgumentException){if(setSlot)invalidSet=true;continue;}
                }
                if(slot==9)garment=true;
                if(slot==10 || slot==11)talisman=true;
                if(slot==15 || slot==16)accessory=true;
                // Cosmetics have design-number suffixes, not equipment quality.
                if(slot!=9 && slot!=15 && slot!=16 && id%10==9)super=true;
                int enchant=Convert.ToInt32(r[4]);
                if((slot>=1 && slot<=6 || slot==8) && enchant>0 && enchant<=byte.MaxValue)enchanted=true;
                int first=Convert.ToInt32(r[5]),second=Convert.ToInt32(r[6]);
                if((slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11)
                    && ValidSocket(first) && (second==0 || ValidSocket(second)))socketed=true;
                if(setSlot && !arrow) {
                    seen|=1<<slot;
                    uint quality=id%10;
                    bool two=ValidSocket(first) && ValidSocket(second);
                    if(ValidSocket(first) && (second==0 || ValidSocket(second))) {
                        if(SuperGem(first))superGems++;
                        if(SuperGem(second))superGems++;
                    }
                    bool qualityValid=quality>=7 && quality<=9;
                    uniqueSet&=qualityValid;eliteSet&=qualityValid && quality>=8;superSet&=quality==9;
                    plusSet&=Convert.ToInt32(r[7])==12;blessSet&=Convert.ToInt32(r[8])==7;
                    socketSet&=ValidSocket(first) && (second==0 || ValidSocket(second));
                    enchantSet&=enchant>0 && enchant<=byte.MaxValue;
                    combinedSet&=quality==9 && two && Convert.ToInt32(r[7])==12 && Convert.ToInt32(r[8])==7;
                }
            }
            if(offset==20) {
                // An in-memory offhand without a matching durable row is not an absent offhand.
                var offhand=player==null?null:player.Equipment.TryGetItem(ConquerItem.AlternateLeftWeapon);
                if(offhand!=null && !ItemHandler.IsArrow(offhand.ID))required|=1<<5;
                if(!invalidSet && (seen&required)==required)
                    ProgressionEvidence.Complete(c,t,uid,10432,"equipment-switch-v1","alternate",1);
                return;
            }
            if(!invalidSet && superGems>=12)
                ProgressionEvidence.Complete(c,t,uid,10428,"equipment-gems-v1","twelve-super",1);
            if(garment)ProgressionEvidence.Complete(c,t,uid,10419,"equipment-state-v1","garment",1);
            if(talisman)ProgressionEvidence.Complete(c,t,uid,10418,"equipment-state-v1","talisman",1);
            if(accessory)ProgressionEvidence.Complete(c,t,uid,10503,"equipment-state-v1","weapon-accessory",1);
            if(super)ProgressionEvidence.Complete(c,t,uid,10409,"equipment-state-v1","super-item",1);
            if(enchanted)ProgressionEvidence.Complete(c,t,uid,10430,"equipment-state-v1","enchanted-item",1);
            if(socketed)ProgressionEvidence.Complete(c,t,uid,10429,"equipment-state-v1","socketed-item",1);
            if(!invalidSet && (seen&required)==required) {
                if(uniqueSet)ProgressionEvidence.Complete(c,t,uid,10410,"equipment-set-v1","unique",1);
                if(eliteSet)ProgressionEvidence.Complete(c,t,uid,10411,"equipment-set-v1","elite",1);
                if(superSet)ProgressionEvidence.Complete(c,t,uid,10412,"equipment-set-v1","super",1);
                if(plusSet)ProgressionEvidence.Complete(c,t,uid,10413,"equipment-set-v1","plus12",1);
                if(blessSet)ProgressionEvidence.Complete(c,t,uid,10414,"equipment-set-v1","bless7",1);
                if(socketSet)ProgressionEvidence.Complete(c,t,uid,10415,"equipment-set-v1","socketed",1);
                if(enchantSet)ProgressionEvidence.Complete(c,t,uid,10416,"equipment-set-v1","enchanted",1);
                if(combinedSet)ProgressionEvidence.Complete(c,t,uid,10417,"equipment-set-v1","perfect",1);
            }
        }
        private static bool SuperGem(int value)
        {
            return value>=3 && value<=73 && value%10==3;
        }
        private static bool ValidSocket(int value)
        {
            return value>0 && value<=byte.MaxValue && Enum.IsDefined(typeof(Enums.Gem),(byte)value);
        }
    }
    // Generic deaths and explicitly opted-in managed progress share a life decision.
    internal static class WildMonsterVictory
    {
        private sealed class Life { internal readonly Guid Id=Guid.NewGuid(); internal bool Attempted,Refresh; internal int? ProgressRoll; internal Func<NpgsqlConnection,NpgsqlTransaction,bool> Progress; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity,Life> lives=
            new System.Runtime.CompilerServices.ConditionalWeakTable<Entity,Life>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Revora.Client.GameState,Entity> earned=
            new System.Runtime.CompilerServices.ConditionalWeakTable<Revora.Client.GameState,Entity>();
        internal static Action AfterCommit;
        private static void Mark(Revora.Client.GameState p) { earned.Remove(p);earned.Add(p,p.Entity); }
        internal static void Revived(Entity monster) { lives.Remove(monster); }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_unarmed_wild_victories(
life uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),map integer NOT NULL,monster bigint NOT NULL,
family bigint NOT NULL,level integer NOT NULL CHECK(level>=120),unarmed boolean NOT NULL DEFAULT true,created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
ALTER TABLE revora_unarmed_wild_victories ADD COLUMN IF NOT EXISTS unarmed boolean NOT NULL DEFAULT true");}
        }
        private static bool Ordinary(Entity m,Entity k) {
            Entity owned;var p=k==null?null:k.Owner;
            return m!=null && m.EntityFlag==EntityFlag.Monster && m.MonsterInfo!=null && m.MonsterInfo.Level>=120
                && !m.Companion && !m.MonsterInfo.Guard && !m.MonsterInfo.Reviver
                && k!=null && k.EntityFlag==EntityFlag.Player && !k.Companion && QuestInventory.Player(p)
                && ReferenceEquals(p.Entity,k) && p.Map!=null && p.Map.ID==m.MapID && k.MapID==m.MapID
                && p.Map.Entities.TryGetValue(m.UID,out owned) && ReferenceEquals(owned,m);
        }
        internal static Guid ProgressIdentity(Entity m) { return lives.GetOrCreateValue(m).Id; }
        internal static int RollProgress(Entity m,Func<int> roll) {
            if(!System.Threading.Monitor.IsEntered(m.MonsterLifeSyncRoot))throw new InvalidOperationException("Progress roll requires monster life ownership.");
            var life=lives.GetOrCreateValue(m);
            if(!life.ProgressRoll.HasValue)life.ProgressRoll=roll();
            return life.ProgressRoll.Value;
        }
        internal static void Die(Entity m,Entity k) { DieWithProgress(m,k,null); }
        internal static void DieWithProgress(Entity m,Entity k,Func<NpgsqlConnection,NpgsqlTransaction,bool> progress) {
            if(m.EntityFlag==EntityFlag.Monster && m.Dead)return;
            if(!Ordinary(m,k)){
                Life pending;
                // Do not bypass an unresolved accepted-life decision through a
                // companion, stale session or other ineligible death caller.
                if(lives.TryGetValue(m,out pending) && pending.Attempted)return;
                m.DieCore(k);return;
            }
            if(!System.Threading.Monitor.TryEnter(m.MonsterLifeSyncRoot))return;
            try {
                if(m.Dead || !System.Threading.Monitor.TryEnter(k.Owner.ItemSyncRoot))return;
                try {
                    if(!Ordinary(m,k))return;
                    var current=lives.GetOrCreateValue(m);current.Progress=progress;
                    try { m.DieWildCore(k); } finally { current.Progress=null; }
                    Life completed;
                    if(m.Dead && lives.TryGetValue(m,out completed) && completed.Refresh && k.Owner.Entity.MyAchievement!=null){
                        try{k.Owner.Entity.MyAchievement.Send();completed.Refresh=false;}
                        catch(Exception ex){QuestInventory.Fail(k.Owner,"wild-unarmed-refresh",ex);}
                    }
                }finally{System.Threading.Monitor.Exit(k.Owner.ItemSyncRoot);}
            }finally{System.Threading.Monitor.Exit(m.MonsterLifeSyncRoot);}
        }
        internal static bool Accept(Entity m,Entity k) {
            var p=k.Owner;var life=lives.GetOrCreateValue(m);
            if(!System.Threading.Monitor.IsEntered(m.MonsterLifeSyncRoot) || !System.Threading.Monitor.IsEntered(p.ItemSyncRoot))
                throw new InvalidOperationException("Wild victory requires life and equipment ownership.");
            byte right=p.AlternateEquipment?ConquerItem.AlternateRightWeapon:ConquerItem.RightWeapon;
            byte left=p.AlternateEquipment?ConquerItem.AlternateLeftWeapon:ConquerItem.LeftWeapon;
            bool empty=p.Equipment!=null && p.Equipment.TryGetItem(right)==null && p.Equipment.TryGetItem(left)==null;
            Entity known;bool progress=life.Progress!=null,grant=false;
            if(!life.Attempted && !progress && (!empty || earned.TryGetValue(p,out known) && ReferenceEquals(known,k)))return true;
            life.Attempted=true;
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    long prior=0;bool priorUnarmed=false;
                    using(var q=QuestInventory.Command(c,t,"SELECT uid,unarmed FROM revora_unarmed_wild_victories WHERE life=@life","life",life.Id))
                    using(var r=q.ExecuteReader())if(r.Read()){prior=r.GetInt64(0);priorUnarmed=r.GetBoolean(1);}
                    if(prior!=0){
                        if(prior!=(long)k.UID)return false;
                        if(priorUnarmed){Mark(p);life.Refresh=true;if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();}
                        return true;
                    }
                    if(empty){
                        using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE entityid=@uid AND position IN(@right,@left))",
                            "uid",(long)k.UID,"right",(int)right,"left",(int)left))empty=!Equals(q.ExecuteScalar(),true);
                        if(empty)using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525)","uid",(long)k.UID)) {
                            grant=!Equals(q.ExecuteScalar(),true);if(!grant)Mark(p);
                        }
                    }
                    if(!progress && !grant)return true;
                    bool changed=progress && life.Progress(c,t);
                    if(!changed && !grant)return true;
                    if(QuestInventory.Execute(c,t,"INSERT INTO revora_unarmed_wild_victories(life,uid,map,monster,family,level,unarmed) VALUES(@life,@uid,@map,@monster,@family,@level,@unarmed)",
                        "life",life.Id,"uid",(long)k.UID,"map",(int)m.MapID,"monster",(long)m.UID,"family",(long)m.MonsterInfo.ID,"level",(int)m.MonsterInfo.Level,"unarmed",grant)!=1)
                        throw new InvalidOperationException("Wild victory was not saved.");
                    if(grant){
                        EquipmentAchievements.RecordUnarmedVictory(c,t,p,m,k,"wild:"+life.Id.ToString("N"));
                        using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525)","uid",(long)k.UID))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Wild unarmed evidence missing.");
                    }
                    t.Commit();
                    var after=AfterCommit;if(after!=null)after();
                }}
                if(grant){Mark(p);life.Refresh=true;if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();}
                return true;
            }catch(Exception ex){
                // No purse, item or loaded death state changed. The same life can
                // safely retry; the saved life identity resolves uncertain commits.
                Console.WriteLine("[quest] source=wild-unarmed uid="+k.UID+" error="+ex.GetType().Name);
                return false;
            }
        }
    }

}
