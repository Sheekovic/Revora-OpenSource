using System;
using System.Threading;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Game.ConquerStructures.Society;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class GuildArsenalInscriptions {
        internal static int Limit(byte level,byte reborn){return reborn>=2?30:reborn==1?21:level>=100?14:7;}
        private static bool Member(GameState p,Arsenal arsenal){
            Revora.Game.ConquerStructures.Society.Guild live;
            Revora.Game.ConquerStructures.Society.Guild.Member member;
            var g=p.Guild;
            return Q.Player(p,false) && g!=null && ReferenceEquals(g.Arsenal,arsenal) && p.Entity.GuildID==g.ID
                && Kernel.Guilds.TryGetValue(g.ID,out live) && ReferenceEquals(g,live)
                && g.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(p.AsMember,member) && member.GuildID==g.ID
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;
        }
        internal static bool Change(GameState p,Arsenal arsenal,uint itemUid,Arsenal_ID slot,bool remove){
            if(!Q.Player(p,false) || (byte)slot<1 || (byte)slot>8 || !SessionInstall.TryEnter(p.Entity.UID))return false;
            try{lock(p.ItemSyncRoot){
                if(!Member(p,arsenal))return false;var g=p.Guild;
                if(!Monitor.TryEnter(GuildLeadership.Gate(g)))return false;
                try{
                    ConquerItem item;Arsenal_State state;Arsenal_Client old=null;
                    if(!Member(p,arsenal) || !arsenal.Arsenals.TryGetValue(slot,out state) || !state.Unlocked
                        || (!p.Inventory.TryGetItem(itemUid,out item) && !p.Equipment.TryGetItem(itemUid,out item))
                        || item.Warehouse!=0 || item.StackSize>1 || Arsenal.MetoEditionars.ArsenalPosition(item.ID)!=(int)slot)return false;
                    if(remove){if(!item.Inscribed || !state.Inscribed.TryGetValue(itemUid,out old) || old.UID!=p.Entity.UID)return false;}
                    else if(item.Inscribed || state.Inscribed.ContainsKey(itemUid))return false;
                    if(!remove && (item.ID%10<8 || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID)))return false;
                    uint donation=remove?old.donation:arsenal.GetDonation(item);
                    ulong before=p.AsMember.Arsenal_Donation;
                    if(remove && before<donation)return false;
                    ulong after=remove?before-donation:checked(before+donation);
                    ulong total=checked(p.AsMember.TotalDoantion-before+after);
                    if(after>long.MaxValue || total>long.MaxValue)return false;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        SkillTable.LockOwner(c,t,p);
                        using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)g.ID))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Inscription guild missing.");
                        using(var q=Q.Command(c,t,"SELECT arsenal_unlocked FROM pt_arsenal WHERE syn_id=@guild AND arsenal_id=@slot FOR UPDATE","guild",(long)g.ID,"slot",(int)slot))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=1 || r.Read())throw new InvalidOperationException("Inscription slot changed.");
                        if(!remove){
                            int limit;
                            using(var q=Q.Command(c,t,"SELECT level,reborn FROM entities WHERE uid=@uid AND guildid=@guild","uid",(long)p.Entity.UID,"guild",(long)g.ID))
                            using(var r=q.ExecuteReader()){
                                if(!r.Read())throw new InvalidOperationException("Inscription membership changed.");
                                limit=Limit(Convert.ToByte(r[0]),Convert.ToByte(r[1]));
                            }
                            using(var q=Q.Command(c,t,"SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid AND syn_id=@guild AND iten_atype=@slot",
                                "uid",(long)p.Entity.UID,"guild",(long)g.ID,"slot",(int)slot))
                                if(Convert.ToInt64(q.ExecuteScalar())>=limit)return false;
                        }
                        if(Q.Execute(c,t,@"UPDATE items SET inscribed=@next WHERE uid=@item AND entityid=@uid AND id=@type
AND position=@pos AND warehouse=0 AND COALESCE(stacksize,0)<=1 AND COALESCE(inscribed,0)=@before
AND COALESCE(plus,0)=@plus AND COALESCE(socketone,0)=@one AND COALESCE(sockettwo,0)=@two",
                            "next",remove?0:1,"item",(long)itemUid,"uid",(long)p.Entity.UID,"type",(long)item.ID,"pos",(int)item.Position,
                            "before",remove?1:0,"plus",(int)item.Plus,"one",(int)item.SocketOne,"two",(int)item.SocketTwo)!=1)
                            throw new InvalidOperationException("Inscription item ownership changed.");
                        if(remove){
                            if(Q.Execute(c,t,"DELETE FROM pt_arsenal_inscribed WHERE uid=@uid AND iten_id=@item AND syn_id=@guild AND iten_atype=@slot AND arsenal_donation::numeric=@donation",
                                "uid",(long)p.Entity.UID,"item",(long)itemUid,"guild",(long)g.ID,"slot",(int)slot,"donation",(long)donation)!=1)
                                throw new InvalidOperationException("Inscription removal changed.");
                        }else{
                            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM pt_arsenal_inscribed WHERE iten_id=@item)","item",(long)itemUid))
                                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Item already inscribed.");
                            if(Q.Execute(c,t,"INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,@slot,@guild,@donation)",
                                "uid",(long)p.Entity.UID,"name",p.Entity.Name,"item",(long)itemUid,"slot",(int)slot,"guild",(long)g.ID,"donation",(long)donation)!=1)
                                throw new InvalidOperationException("Inscription was not saved.");
                        }
                        if(Q.Execute(c,t,"UPDATE entities SET arsenaldonation=@after,totaldoantionguild=@total WHERE uid=@uid AND guildid=@guild AND COALESCE(arsenaldonation,0)=@before",
                            "after",(long)after,"total",(long)total,"uid",(long)p.Entity.UID,"guild",(long)g.ID,"before",(long)before)!=1)
                            throw new InvalidOperationException("Inscription membership changed.");
                        if(Q.Execute(c,t,@"UPDATE pt_arsenal SET arsenal_donation=(SELECT COALESCE(sum(arsenal_donation::numeric),0) FROM pt_arsenal_inscribed WHERE syn_id=@guild AND iten_atype=@slot)
WHERE syn_id=@guild AND arsenal_id=@slot","guild",(long)g.ID,"slot",(int)slot)!=1)throw new InvalidOperationException("Arsenal donation was not saved.");
                        if(!remove)ProgressionEvidence.Complete(c,t,p.Entity.UID,10226,"guild-inscription-v1",itemUid.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                        if(!Member(p,arsenal))throw new InvalidOperationException("Inscription session changed.");t.Commit();
                    }}
                    lock(state.Inscribed){if(remove)state.Inscribed.Remove(itemUid);else state.Inscribed.Add(itemUid,new Arsenal_Client{Item=item,iUID=itemUid,UID=p.Entity.UID,Name=p.Entity.Name,donation=donation});}
                    p.AsMember.Arsenal_Donation=after;item.Inscribed=!remove;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;
                    GuildTable.BuildDonationRankings(g);
                    GuildTable.RebuildLevel(g);GuildArsenalPurchases.Rebuild(g);g.Arsenal.Version++;
                    p.Send(ArsenalPacket.GuildArsenal(p));Q.Refresh(p);return true;
                }catch(Exception ex){Q.Fail(p,"guild-inscription",ex);return false;}
                finally{Monitor.Exit(GuildLeadership.Gate(g));}
            }}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
    internal static class GuildArsenalPurchases {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,
                "DO $revora_column$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='pt_arsenal'::regclass AND attname='enhancement_expires_utc_ticks' AND NOT attisdropped) THEN ALTER TABLE pt_arsenal ADD COLUMN enhancement_expires_utc_ticks bigint NOT NULL DEFAULT 0; END IF; END $revora_column$");}
        }
        // Call under the guild gate, or before publishing a newly loaded guild.
        internal static byte DonationPower(ulong donation) {
            return (byte)(donation>=10000000?3:donation>=4000000?2:donation>=2000000?1:0);
        }
        private static bool Active(Arsenal_State state,DateTime now) {
            return state.EnhancementExpDate==default(DateTime) || now<state.EnhancementExpDate;
        }
        internal static void Rebuild(Guild guild) { RebuildAt(guild,Clock().ToUniversalTime()); }
        private static void RebuildAt(Guild guild,DateTime now) {
            var arsenal=guild.Arsenal;int budget=0;var powers=new System.Collections.Generic.List<int>(8);
            bool changed=false;
            foreach(var pair in arsenal.Arsenals) {
                var state=pair.Value;bool valid=(byte)pair.Key>=1 && (byte)pair.Key<=8 && state.ID==pair.Key && state.Unlocked;
                byte natural=valid?DonationPower(state.Donation):(byte)0;
                if(state.Potency!=natural){state.Potency=natural;changed=true;}
                int enhancement=valid && Active(state,now)?(int)Math.Min(2u,state.Enhancement):0;
                budget+=enhancement;powers.Add(Math.Max(natural,enhancement));
            }
            byte total=(byte)powers.OrderByDescending(v=>v).Take(5).Sum();
            if(arsenal.BattlePower!=total){arsenal.BattlePower=total;changed=true;}
            guild.maxenchant=(uint)Math.Min(5,budget);guild.getbp=false;
            if(changed)arsenal.Version++;
        }
        internal static void Refresh(Guild guild) {
            DateTime now=Clock().ToUniversalTime();var arsenal=guild.Arsenal;
            var due=arsenal.Arsenals.Values.Where(s=>s.EnhancementExpDate!=default(DateTime) && s.EnhancementExpDate<=now).ToArray();
            // Even a failed settlement cannot keep an expired bonus in combat.
            try {
                if(due.Length==0)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Enhancement guild missing.");
                    foreach(var state in due) {
                        if(Q.Execute(c,t,@"UPDATE pt_arsenal SET arsenal_enhance=0,syn_id_bp=0,enhancement_expires_utc_ticks=0
WHERE syn_id=@guild AND arsenal_id=@slot AND arsenal_enhance::bigint=@level AND syn_id_bp::bigint=@bp AND enhancement_expires_utc_ticks=@expires",
                            "guild",(long)guild.ID,"slot",(int)state.ID,"level",(long)state.Enhancement,
                            "bp",(long)state.ars_bp,"expires",state.EnhancementExpDate.Ticks)!=1)
                            throw new InvalidOperationException("Enhancement expiry requires reload.");
                    }
                    long remaining;
                    using(var q=Q.Command(c,t,@"SELECT LEAST(5,COALESCE(sum(LEAST(2,GREATEST(0,arsenal_enhance::bigint))),0)) FROM pt_arsenal
WHERE syn_id=@guild AND arsenal_unlocked::int=1 AND arsenal_id BETWEEN 1 AND 8
AND (enhancement_expires_utc_ticks=0 OR enhancement_expires_utc_ticks>@now)","guild",(long)guild.ID,"now",now.Ticks))
                        remaining=Convert.ToInt64(q.ExecuteScalar());
                    if(Q.Execute(c,t,"UPDATE guilds SET maxenchant=@value WHERE id=@guild","value",remaining,"guild",(long)guild.ID)!=1)
                        throw new InvalidOperationException("Enhancement total was not settled.");
                    t.Commit();
                }}
                foreach(var state in due){state.Enhancement=0;state.ars_bp=0;state.EnhancementExpDate=default(DateTime);}
                arsenal.Version++;
            } finally { RebuildAt(guild,now); }
        }
        private sealed class Projection { internal Arsenal Arsenal;internal long Version; }
        private static readonly ConditionalWeakTable<GameState,Projection> projections=new ConditionalWeakTable<GameState,Projection>();
        // The maintenance loop holds the player item lock and guild gate here.
        internal static void Publish(GameState player) {
            var arsenal=player.Guild.Arsenal;var projection=projections.GetValue(player,key=>new Projection());
            if(player.Entity.GuildSharedBp!=arsenal.BattlePower)player.Entity.GuildSharedBp=arsenal.BattlePower;
            if(!ReferenceEquals(projection.Arsenal,arsenal) || projection.Version!=arsenal.Version) {
                player.Send(ArsenalPacket.GuildArsenal(player));projection.Arsenal=arsenal;projection.Version=arsenal.Version;
            }
        }
        internal static bool Enhance(GameState p,Arsenal arsenal,Arsenal_ID slot,uint level) {
            if(slot==Arsenal_ID.None)slot=Arsenal_ID.Headgear;
            if((byte)slot<1 || (byte)slot>8 || level<1 || level>2 || !GuildLeadership.Leader(p))return false;
            uint uid=p.Entity.UID;if(!SessionInstall.TryEnter(uid))return false;
            try{lock(p.ItemSyncRoot) {
                var g=p.Guild;if(!GuildLeadership.Leader(p) || !ReferenceEquals(g.Arsenal,arsenal) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
                if(!Monitor.TryEnter(GuildLeadership.Gate(g)))return false;
                try {
                    Refresh(g);
                    Arsenal_State state;
                    if(!GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g) || !arsenal.Arsenals.TryGetValue(slot,out state)
                        || !state.Unlocked || state.Potency>=3 || state.Enhancement>=level || g.maxenchant>=5 || arsenal.BattlePower>=15)return false;
                    uint increase=level-state.Enhancement;
                    if(increase>5-g.maxenchant || level<=state.Potency)return false;decimal fund;uint nextMax=g.maxenchant+increase;
                    DateTime expires=Clock().ToUniversalTime().AddDays(30);
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        SkillTable.LockOwner(c,t,p);
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank=@rank AND name=@name FOR UPDATE",
                            "uid",(long)uid,"guild",(long)g.ID,"rank",(int)Enums.GuildMemberRank.GuildLeader,"name",p.Entity.Name))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Enhancement leader membership changed.");
                        using(var q=Q.Command(c,t,"SELECT silverfund,maxenchant FROM guilds WHERE id=@guild AND leadername=@name FOR UPDATE","guild",(long)g.ID,"name",p.Entity.Name))
                        using(var r=q.ExecuteReader()) {
                            if(!r.Read() || Convert.ToUInt32(r[1])!=g.maxenchant)throw new InvalidOperationException("Enhancement guild authority changed.");fund=Convert.ToDecimal(r[0]);
                        }
                        using(var q=Q.Command(c,t,"SELECT arsenal_unlocked,arsenal_enhance,syn_id_bp,enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=@guild AND arsenal_id=@slot FOR UPDATE",
                            "guild",(long)g.ID,"slot",(int)slot))using(var r=q.ExecuteReader()) {
                            if(!r.Read() || Convert.ToInt32(r[0])!=1 || Convert.ToUInt32(r[1])!=state.Enhancement || Convert.ToUInt32(r[2])!=state.ars_bp || Convert.ToInt64(r[3])!=state.EnhancementExpDate.Ticks || r.Read())
                                throw new InvalidOperationException("Enhancement slot requires reload.");
                        }
                        long price=level*50000L;if(fund<price)return false;fund-=price;if(fund>ulong.MaxValue)return false;
                        if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@fund,maxenchant=@max WHERE id=@guild","fund",fund,"max",(long)nextMax,"guild",(long)g.ID)!=1)
                            throw new InvalidOperationException("Enhancement fee was not saved.");
                        if(Q.Execute(c,t,"UPDATE pt_arsenal SET arsenal_enhance=@level,syn_id_bp=@level,enhancement_expires_utc_ticks=@expires WHERE syn_id=@guild AND arsenal_id=@slot",
                            "level",(long)level,"expires",expires.Ticks,"guild",(long)g.ID,"slot",(int)slot)!=1)throw new InvalidOperationException("Enhancement was not saved.");
                        if(!GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g) || !ReferenceEquals(g.Arsenal,arsenal))throw new InvalidOperationException("Enhancement session changed.");t.Commit();
                    }}
                    g.SilverFund=(ulong)fund;state.Enhancement=level;state.ars_bp=level;
                    state.EnhancementExpDate=expires;arsenal.Version++;Rebuild(g);g.SendGuild(p);p.Send(ArsenalPacket.GuildArsenal(p));return true;
                }catch(Exception ex){Q.Fail(p,"guild-arsenal-enhance",ex);return false;}
                finally{Monitor.Exit(GuildLeadership.Gate(g));}
            }}finally{SessionInstall.Exit(uid);}
        }
        internal static bool Unlock(GameState p,Arsenal arsenal,Arsenal_ID slot) {
            if(slot==Arsenal_ID.None)slot=Arsenal_ID.Headgear;
            if((byte)slot<1 || (byte)slot>8 || !GuildLeadership.Leader(p))return false;
            uint uid=p.Entity.UID;if(!SessionInstall.TryEnter(uid))return false;
            try{lock(p.ItemSyncRoot) {
                var g=p.Guild;if(!GuildLeadership.Leader(p) || !ReferenceEquals(g.Arsenal,arsenal) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
                if(!Monitor.TryEnter(GuildLeadership.Gate(g)))return false;
                try {
                    if(!GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g) || arsenal.Arsenals.ContainsKey(slot))return false;
                    decimal funds;int count;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        SkillTable.LockOwner(c,t,p);
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank=@rank AND name=@name FOR UPDATE",
                            "uid",(long)uid,"guild",(long)g.ID,"rank",(int)Enums.GuildMemberRank.GuildLeader,"name",p.Entity.Name))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Arsenal leader membership changed.");
                        using(var q=Q.Command(c,t,"SELECT silverfund FROM guilds WHERE id=@guild AND leadername=@name FOR UPDATE","guild",(long)g.ID,"name",p.Entity.Name)) {
                            var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Arsenal guild leader changed.");funds=Convert.ToDecimal(value);
                        }
                        using(var q=Q.Command(c,t,"SELECT arsenal_id FROM pt_arsenal WHERE syn_id=@guild ORDER BY arsenal_id FOR UPDATE","guild",(long)g.ID))
                        using(var r=q.ExecuteReader()) {
                            count=0;while(r.Read()){var saved=(Arsenal_ID)Convert.ToByte(r[0]);if(saved==slot)return false;
                                if(!arsenal.Arsenals.ContainsKey(saved))throw new InvalidOperationException("Arsenal ownership requires reload.");count++;}
                        }
                        if(count!=arsenal.Arsenals.Count || count>=8)throw new InvalidOperationException("Arsenal count requires reload.");
                        long price=count<2?5000000:count<5?10000000:count<7?15000000:20000000;
                        if(funds<price)return false;funds-=price;if(funds>ulong.MaxValue)return false;
                        if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@fund WHERE id=@guild","fund",funds,"guild",(long)g.ID)!=1)throw new InvalidOperationException("Arsenal fee not saved.");
                        if(Q.Execute(c,t,"INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(@guild,@slot,1,0,0)",
                            "guild",(long)g.ID,"slot",(int)slot)!=1)throw new InvalidOperationException("Arsenal unlock not saved.");
                        if(!GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g) || !ReferenceEquals(g.Arsenal,arsenal))throw new InvalidOperationException("Arsenal session changed.");t.Commit();
                    }}
                    g.SilverFund=(ulong)funds;arsenal.Arsenals.Add(slot,new Arsenal_State{ID=slot,Unlocked=true});arsenal.Version++;Rebuild(g);g.SendGuild(p);p.Send(ArsenalPacket.GuildArsenal(p));return true;
                }catch(Exception ex){Q.Fail(p,"guild-arsenal-unlock",ex);return false;}
                finally{Monitor.Exit(GuildLeadership.Gate(g));}
            }}finally{SessionInstall.Exit(uid);}
        }
    }
}
