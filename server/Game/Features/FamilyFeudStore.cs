using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class FamilyFeudStore
    {
        internal sealed class Run {internal int Stage;internal uint Soul;internal DateTime? DisguiseUntil;}
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=95 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("FamilyFeud context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{710562,710563,710564,710565,710566,710567,728248,723911})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Family Feud catalog missing.");
            uint[] families={55,79,84};string[] names={"BanditL97","BanditL98","Banditti"};byte[] levels={97,98,98};
            for(int i=0;i<families.Length;i++){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(families[i],out m) || m.Name!=names[i] || m.Level!=levels[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Family Feud family changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_family_feud(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 10),
soul bigint NOT NULL,disguise_until timestamptz,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_family_feud_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type BETWEEN 710562 AND 710567),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_family_feud(uid,life));
CREATE TABLE IF NOT EXISTS revora_family_feud_kills(uid bigint NOT NULL,life smallint NOT NULL,stage smallint NOT NULL CHECK(stage IN(2,4,5)),
kills integer NOT NULL CHECK(kills BETWEEN 0 AND 50),PRIMARY KEY(uid,life,stage),FOREIGN KEY(uid,life) REFERENCES revora_family_feud(uid,life));
CREATE TABLE IF NOT EXISTS revora_family_feud_encounter_kills(attempt uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
kind smallint NOT NULL CHECK(kind BETWEEN 0 AND 3),body bigint NOT NULL,won boolean NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_family_feud(uid,life));
CREATE TABLE IF NOT EXISTS revora_family_feud_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_family_feud(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,disguise_until FROM revora_family_feud WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),DisguiseUntil=r.IsDBNull(2)?(DateTime?)null:r.GetDateTime(2).ToUniversalTime()}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("FamilyFeud saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("FamilyFeud bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_family_feud_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_family_feud_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_family_feud_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("FamilyFeud material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("FamilyFeud material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_family_feud_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("FamilyFeud consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_family_feud SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("FamilyFeud stage changed.");
        }
        // Stage 1 referral; stages 2..6 hand in the five earned colored tokens.
        // Chamber completion is deliberately not exposed through this NPC operation.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);var removed=new List<uint>();ConquerItem added=null;ulong silver=p.Entity.Money;
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Family Feud clock must be UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>6 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_family_feud(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Family Feud acceptance failed.");
                }else{
                    if(action>=2){uint type=710560u+(uint)action;uint item=Materials(c,t,p,type).FirstOrDefault(id=>Material(c,t,p,id,type));if(item==0)return false;
                        Consume(c,t,p,item,type);removed.Add(item);
                    }
                    if(action==6){added=Q.New(710567,true);Track(c,t,p,added);
                        if(Q.Execute(c,t,"UPDATE revora_family_feud SET disguise_until=@until WHERE uid=@uid AND life=@life AND stage=6","until",now.AddSeconds(90),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Family Feud disguise not saved.");
                    }
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;
                    SilverBalance.Persist(c,t,p,silver);Save(c,t,p,action,action+1);
                }
                Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added==null?new ConquerItem[0]:new[]{added});if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        // A missed journey may be retried at Felix; an active attempt cannot be extended.
        internal static bool RenewDisguise(GameState p,Func<bool> context){
            Ready(p,context);DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Family Feud clock must be UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=7 || !run.DisguiseUntil.HasValue || now<=run.DisguiseUntil.Value)return false;
                if(!Materials(c,t,p,710567).Any(id=>Material(c,t,p,id,710567)))return false;
                if(Q.Execute(c,t,"UPDATE revora_family_feud SET disguise_until=@until WHERE uid=@uid AND life=@life AND stage=7 AND disguise_until<@now","until",now.AddSeconds(90),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now)!=1)throw new InvalidOperationException("Family Feud disguise changed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
    }
}
