using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    internal static class RuneProgress
    {
        // Values checked against the approved 6711 ability_score and native +Stone preview.
        private static readonly int[] BlueScores = {0,1100,1300,1500,1800,2300,2800,3400,4000,4600,5300,6000,6900,7900,8900,9900,11000,13000,14000,15500,17000,18600,20500,22000,24500,26500,29000,31000};
        private static readonly int[] YellowScores = {0,1200,1600,2200,3000,4000,5300,6900,8800,11000};
        private static readonly uint[] StonePoints = {0,10,40,120,360,1080,3240,9720,29160};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class RecentRequests { internal readonly Queue<Tuple<string,DateTime>> Keys=new Queue<Tuple<string,DateTime>>(); }
        private static readonly ConditionalWeakTable<GameState,RecentRequests> Recent=new ConditionalWeakTable<GameState,RecentRequests>();
        internal static bool IsRune(uint id) { return id/10000>=401 && id/10000<=403; }
        internal static bool Equipped(ConquerItem item)
        {
            if(item==null || item.Warehouse!=0 || !IsRune(item.ID)) return false;
            return ValidSlot(item.ID,item.Position);
        }
        internal static bool ValidSlot(uint id,uint position)
        {
            if(!IsRune(id))return false;
            uint slot=position;
            if(slot>=121 && slot<=125) slot-=20;
            return id/10000==401 ? slot==101 : id/10000==402 ? slot==102 : slot>=103 && slot<=108;
        }
        // Keep ValidSlot as the legacy storage/recovery predicate. Only five slots
        // are usable in each installed 6711 combination; old 106-108 items remain removable.
        internal static bool UsableSlot(uint id,uint position) {
            return ValidSlot(id,position) && (position<=105 || position>=121 && position<=125);
        }
        internal static bool CanUse(byte profession,byte level,byte reborn,uint id) {
            ConquerItemBaseInformation info;
            if(level<15 || reborn<2 || !IsRune(id) || ConquerItemInformation.BaseInformations==null
                || !ConquerItemInformation.BaseInformations.TryGetValue(id,out info))return false;
            uint rank=id%100;if(rank<1 || rank>(id/10000==401?1u:id/10000==402?27u:9u))return false;
            if(id/10000==403)return info.Class==0;
            int required=info.Class;
            return required!=0 && profession>=required && profession<=required/10*10+5;
        }
        internal static bool CanUse(GameState p,uint id) {
            return p!=null && p.Entity!=null && CanUse(p.Entity.Class,p.Entity.Level,p.Entity.Reborn,id);
        }
        internal static ConquerItem[] LegalRunes(IEnumerable<ConquerItem> items,byte profession,byte level,byte reborn) {
            var rows=items.Where(i=>i!=null && i.Warehouse==0 && i.StackSize<=1 && UsableSlot(i.ID,i.Position)
                && CanUse(profession,level,reborn,i.ID)).ToArray();
            // Never choose a winner from corrupt duplicate slots/types. Keep both owned
            // items available for unequip, without applying either ambiguous effect.
            return rows.Where(i=>rows.Count(j=>j.Position==i.Position)==1
                && rows.Count(j=>(j.Position>=121)==(i.Position>=121) && j.ID/100==i.ID/100)==1).ToArray();
        }
        internal static ConquerItem[] LegalRunes(GameState p) {
            if(p==null || p.Entity==null || p.RunesItem==null)return new ConquerItem[0];
            return LegalRunes(p.RunesItem.Values,p.Entity.Class,p.Entity.Level,p.Entity.Reborn);
        }
        internal static void ProfileChanged(GameState p) {
            if(p==null || p.Entity==null || !p.Entity.FullyLoaded || p.Entity.PersistenceRequiresReload
                || p.RunesItem==null || p.RunesItem.Count==0)return;
            lock(p.ItemSyncRoot) {
                p.LoadItemStats();PrestigeRanking.SendSelf(p);
                Revora.Game.Features.QuestInventory.Refresh(p);
            }
        }
        internal static void RefreshSkills(GameState client)
        {
            lock(client.ItemSyncRoot ?? client) {
                if(client.Entity!=null && client.Entity.PersistenceRequiresReload)return;
                RefreshSkillsOwned(client);
            }
        }
        private static void RefreshSkillsOwned(GameState client)
        {
            if(client.RunesItem==null || client.Spells==null)return;
            if(client.ChampionGroup!=null)RunesSystem.ClearChampionEffects(client.Entity);
            var equipped=client.ChampionGroup!=null ? new ConquerItem[0] : LegalRunes(client).Where(i=>(i.Position>=121)==client.AlternateEquipment).ToArray();
            var desired=new HashSet<ushort>(equipped.Select(i=>(ushort)(ConquerItemInformation.BaseInformations[i.ID].subtype/100)));
            foreach(ushort id in client.Spells.Keys.Where(id=>RunesSystem.RunesSpells.Contains(id) || id>=40100 && id<40400).ToArray()) {
                if(!desired.Contains(id)) {
                    client.Spells.Remove(id);client.Send(new Data(true){UID=client.Entity.UID,dwParam=id,ID=109});
                    RunesSystem.RemoveRunesSkills(client.Entity);
                }
            }
            foreach(var item in equipped) {
                var skill=ConquerItemInformation.BaseInformations[item.ID].subtype;
                item.RuneSkill=skill;
                Revora.Interfaces.ISkill existing;ushort id=(ushort)(skill/100);byte level=(byte)(skill%100);
                if(!client.Spells.TryGetValue(id,out existing) || existing.Level!=level) {
                    client.Spells.Remove(id);
                    client.AddSpell(new Spell(true){ID=id,Level=level});
                }
            }
        }
        internal static int Prestige(ConquerItem item)
        {
            if(!Equipped(item)) return 0;
            uint level=item.ID%100;
            switch(item.ID/10000) {
                case 401:return level==1?3000:0;
                case 402:return level>0 && level<BlueScores.Length?BlueScores[level]:0;
                case 403:return level>0 && level<YellowScores.Length?YellowScores[level]:0;
                default:return 0;
            }
        }
        internal static bool TryRead(byte[] packet,out uint target,out uint[] materials)
        {
            target=0;materials=null;
            if(packet==null || packet.Length<103 || packet.Length>1119 || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,20)!=63) return false;
            uint count=BitConverter.ToUInt32(packet,26);
            if(count==0 || count>255 || packet.Length!=99+4*count) return false;
            target=BitConverter.ToUInt32(packet,8);if(target==0)return false;
            materials=new uint[count];var seen=new HashSet<uint>();
            for(int i=0;i<count;i++) {
                uint uid=BitConverter.ToUInt32(packet,91+4*i);
                if(uid==0 || uid==target || !seen.Add(uid))return false;
                materials[i]=uid;
            }
            return true;
        }
        internal static bool Advance(uint id,uint exp,ulong added,out uint nextId,out uint nextExp)
        {
            nextId=id;nextExp=exp;
            var levels=RunesSystem.AllRunesExp.Where(r=>r.RuneType==id/10000).OrderBy(r=>r.Level).ToArray();
            int level=(int)(id%100);var current=levels.SingleOrDefault(r=>r.Level==level);
            if(current==null || levels.Length<2 || level>=levels.Last().Level || added==0)return false;
            ulong total=(ulong)current.Exp+exp+added;
            var reached=levels.Last(r=>r.Exp<=total);
            nextId=id-id%100+reached.Level;
            nextExp=reached.Level==levels.Last().Level?0:checked((uint)(total-reached.Exp));
            return ConquerItemInformation.BaseInformations.ContainsKey(nextId);
        }
        private static bool Eligible(GameState c)
        {
            return Revora.Game.Features.QuestInventory.Player(c) && c.Entity.Reborn>=2 && c.Entity.Level>=15 && c.RunesItem!=null
                && c.Booth==null && c.Trade!=null && !c.Trade.InTrade;
        }
        internal static void Compose(GameState client,byte[] packet)
        {
            uint uid;uint[] materialIds;
            if(!Eligible(client) || !TryRead(packet,out uid,out materialIds))return;
            lock(client.ItemSyncRoot)
            {
                string key;
                using(var sha=SHA256.Create())key=Convert.ToBase64String(sha.ComputeHash(packet));
                var recent=Recent.GetValue(client,_=>new RecentRequests());
                DateTime now=Clock();
                // Native 6711 leaves offset 4 zero: identical stack requests have no unique request ID.
                // Suppress immediate duplicates, but allow deliberate later uses of the same stack.
                while(recent.Keys.Count>0 && recent.Keys.Peek().Item2<=now)recent.Keys.Dequeue();
                if(recent.Keys.Any(r=>r.Item1==key))return;
                ConquerItem target;
                if(!Eligible(client) || !client.RunesItem.TryGetValue(uid,out target) || !IsRune(target.ID) || target.UID!=uid || target.StackSize>1
                    || target.Warehouse!=0 || target.Suspicious || (target.Position!=211 && !Equipped(target)))return;
                if(Equipped(target) && !LegalRunes(client).Contains(target))return;
                var materials=new List<ConquerItem>();ulong added=0;bool bound=target.Bound;
                foreach(uint materialUid in materialIds) {
                    ConquerItem material;
                    bool inInventory=client.Inventory.TryGetItem(materialUid,out material);
                    if(!inInventory && !client.RunesItem.TryGetValue(materialUid,out material))return;
                    if(material.UID!=materialUid || material.Warehouse!=0 || material.Lock!=0 || material.Suspicious
                        || (inInventory?material.Position!=0:material.Position!=211))return;
                    uint points;
                    if(material.ID>=730001 && material.ID<=730008 && material.Plus==material.ID-730000)
                        points=StonePoints[material.Plus];
                    else if(IsRune(material.ID) && material.ID/10000==target.ID/10000) {
                        var row=RunesSystem.AllRunesExp.SingleOrDefault(r=>r.RuneType==material.ID/10000 && r.Level==material.ID%100);
                        if(row==null)return;
                        points=checked(row.Exp+material.RunesEXP);
                    } else return;
                    added+=points;bound|=material.Bound;materials.Add(material);
                }
                uint nextId,nextExp;
                if(!Advance(target.ID,target.RunesEXP,added,out nextId,out nextExp)) {
                    client.Send("This rune cannot be raised any further.");return;
                }
                try {
                    bool refreshAchievements=false;
                    using(var c=DataHolder.MySqlConnection) {
                        c.Open();using(var tx=c.BeginTransaction()) {
                            Revora.Game.Features.RuneLoadout.VerifyProfile(c,tx,client);
                            One(c,"UPDATE items SET id=@next,runesexp=@xp,bound=@bound WHERE uid=@uid AND entityid=@owner AND id=@id AND runesexp=@before AND position=@position AND warehouse=0 AND bound=@oldbound AND stacksize=@stack",
                                "next",(int)nextId,"xp",(long)nextExp,"bound",bound?1:0,"uid",(long)target.UID,"owner",(long)client.Entity.UID,
                                "id",(int)target.ID,"before",(long)target.RunesEXP,"position",(int)target.Position,"oldbound",target.Bound?1:0,"stack",(int)target.StackSize);
                            foreach(var m in materials) {
                                int left=Math.Max(1,(int)m.StackSize)-1;
                                One(c,(left==0?"DELETE FROM items":"UPDATE items SET stacksize=@left")+
                                    " WHERE uid=@uid AND entityid=@owner AND id=@id AND position=@position AND warehouse=0 AND stacksize=@stack AND plus=@plus AND bound=@bound AND locked=0 AND runesexp=@xp",
                                    "left",left,"uid",(long)m.UID,"owner",(long)client.Entity.UID,"id",(int)m.ID,"position",(int)m.Position,
                                    "stack",(int)m.StackSize,"plus",(int)m.Plus,"bound",m.Bound?1:0,"xp",(long)m.RunesEXP);
                            }
                            if(Equipped(target))refreshAchievements=Revora.Game.Features.RuneLoadout.RecordUpgrade(c,tx,client,target,nextId,nextExp,bound);
                            if(!Eligible(client))throw new InvalidOperationException("Rune session changed before commit.");
                            tx.Commit();
                        }
                    }
                    recent.Keys.Enqueue(Tuple.Create(key,now.AddSeconds(1)));if(recent.Keys.Count>64)recent.Keys.Dequeue();
                    foreach(var m in materials) {
                        if(m.StackSize>1) {m.StackSize--;m.Mode=Enums.ItemMode.Update;m.Send(client);m.Mode=Enums.ItemMode.Default;}
                        else if(m.Position==0)client.Inventory.Remove(m.UID,Enums.ItemUse.None,true);
                        else {
                            client.RunesItem.Remove(m.UID);
                            var remove=new ItemUsage(true){UID=m.UID,Action=ItemUsage.Mode.RemoveInventory};client.Send(remove);
                        }
                    }
                    target.ID=nextId;target.RunesEXP=nextExp;target.Bound=bound;
                    target.RuneSkill=ConquerItemInformation.BaseInformations[nextId].subtype;
                    target.Mode=Enums.ItemMode.Update;target.Send(client);target.Mode=Enums.ItemMode.Default;
                    if(Equipped(target)) {
                        RefreshSkills(client);
                        client.LoadItemStats();
                    }
                    // SendSelf also refreshes the shared ranking entry; no separate stored score can drift.
                    PrestigeRanking.SendSelf(client);
                    if(refreshAchievements)Revora.Game.Features.QuestInventory.Refresh(client);
                } catch(Exception error) {
                    Console.WriteLine("[rune] persistence-failure uid="+client.Entity.UID+" type="+error.GetType().Name);
                    client.Entity.PersistenceRequiresReload=true;client.Action=0;
                    client.Send("Please reconnect before upgrading another rune.");client.Disconnect();
                }
            }
        }
        private static void One(NpgsqlConnection c,string sql,params object[] args)
        {
            using(var q=new NpgsqlCommand(sql,c)) {
                for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);
                if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Rune persistence conflict");
            }
        }
    }
}
