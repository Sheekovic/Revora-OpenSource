using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Revora.Network;
internal static partial class PotencyHuntTests {
    static byte[] DonationPacket(uint amount,bool cp=false) {
        var b=new byte[36];Writer.WriteUInt16(28,0,b);Writer.WriteUInt16(1107,2,b);Writer.WriteUInt32(cp?GuildCommand.DonateConquerPoints:GuildCommand.DonateSilvers,4,b);
        Writer.WriteUInt32(amount,8,b);Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,28,b);return b;
    }
    static void VerifyGuildDonations() {
        Query("ALTER TABLE guilds ADD COLUMN silverfund bigint NOT NULL DEFAULT 1000000,ADD COLUMN conquerpointfund bigint NOT NULL DEFAULT 100");
        var leader=PrizeLeader(240);var p=PurseMember(leader,Enums.GuildMemberRank.Member);p.Entity.Money=10000;p.Entity.ConquerPoints=1000;EntityTable.SaveEntity(p);
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildDonations",true);DateTime now=DateTime.UtcNow;
        type.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Query("UPDATE guilds SET silverfund=silverfund-500000 WHERE id=240");p.Guild.SilverFund=1000000;
            var packet=DonationPacket(1000);GuildCommand.Process(packet,p);
            Check(!Fenced(p) && p.Entity.Money==9000 && p.Guild.SilverFund==501000 && p.AsMember.SilverDonation==1000 && p.AsMember.SilverDoantion==1000,
                "ordinary member donation projects committed currency, both donation counters and actual guild funds");
            Check(Scalar("SELECT silverfund FROM guilds WHERE id=240",0)==501000 && Scalar("SELECT guildsilverdonation FROM entities WHERE uid=@uid",p.Entity.UID)==1000,
                "donation does not overwrite an intervening summon debit with cached funds");
            Parallel.For(0,8,i=>GuildCommand.Process(packet,p));Check(p.Entity.Money==9000 && p.Guild.SilverFund==501000,"rapid repeated donation packets charge once");
            GuildCommand.Process(DonationPacket(100,true),p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==900 && p.AsMember.ConquerPointDonation==100 && p.Guild.ConquerPointFund==200
                && Scalar("SELECT silverfund FROM guilds WHERE id=240",0)==501000,"CP donations update their own fund and preserve the committed silver balance");
            Check(p.AsMember.CPSList.Single(row=>row.EntityID==p.Entity.UID).cpdoantion==2000
                && leader.AsMember.CPSList.Single(row=>row.EntityID==p.Entity.UID).cpdoantion==2000
                && p.Guild.ListMember.Any(member=>ReferenceEquals(member,p.AsMember)),
                "committed CP donation refreshes both donor and peer ranking views from actual membership");
            var committedView=p.AsMember.CPSList;
            GuildCommand.Process(DonationPacket(100,true),p);
            Check(ReferenceEquals(committedView,p.AsMember.CPSList) && p.AsMember.ConquerPointDonation==100,
                "suppressed duplicate donation cannot republish or increase a ranking");
            now=now.AddSeconds(2);GuildCommand.Process(packet,p);
            Check(p.Entity.Money==8000 && p.AsMember.SilverDonation==2000,"an intentional later donation remains available");
            var gate=Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildLeadership",true),null,"Gate",p.Guild);
            lock(gate){var attempt=Task.Run(()=>GuildCommand.Process(DonationPacket(25),p));Check(attempt.Wait(TimeSpan.FromSeconds(5)) && p.Entity.Money==8000,"busy guild ownership defers donation without debit or deadlock");}
            var invalid=DonationPacket(1000);invalid[28]=0;GuildCommand.Process(invalid,p);
            GuildCommand.Process(DonationPacket(0),p);GuildCommand.Process(DonationPacket(100000),p);
            Check(p.Entity.Money==8000 && p.Guild.SilverFund==502000,"invalid seal, zero and unaffordable donations leave balances intact");
            p.Fake=true;GuildCommand.Process(DonationPacket(50),p);p.Fake=false;
            Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)p.Entity.UID);GuildCommand.Process(DonationPacket(50),p);
            Check(Fenced(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==8000 && Scalar("SELECT silverfund FROM guilds WHERE id=240",0)==502000,
                "fake or stale saved membership cannot donate through a cached guild");
            var failed=PurseMember(leader,Enums.GuildMemberRank.Member);failed.Entity.Money=10000;EntityTable.SaveEntity(failed);
            Query("CREATE FUNCTION refuse_donation_fund() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER refuse_donation_fund BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION refuse_donation_fund()");
            try{GuildCommand.Process(DonationPacket(500),failed);}finally{Query("DROP TRIGGER refuse_donation_fund ON guilds;DROP FUNCTION refuse_donation_fund()");}
            Check(Fenced(failed) && failed.Entity.Money==10000 && Scalar("SELECT money FROM entities WHERE uid=@uid",failed.Entity.UID)==10000
                && Scalar("SELECT guildsilverdonation FROM entities WHERE uid=@uid",failed.Entity.UID)==0,"failed guild credit rolls back player debit and donation progress");
            var fresh=PurseMember(leader,Enums.GuildMemberRank.Member);fresh.Entity.Money=10000;EntityTable.SaveEntity(fresh);
            GuildCommand.Process(DonationPacket(500),fresh);fresh=ReloadGold(fresh);
            Check(!Fenced(fresh) && fresh.Entity.Money==9500 && Scalar("SELECT guildsilverdonation FROM entities WHERE uid=@uid",fresh.Entity.UID)==500,
                "ordinary character reload retains the donation debit and saved progress");
            var viewer=PurseMember(leader,Enums.GuildMemberRank.Member);
            viewer.AsMember.SilverDonation=viewer.AsMember.SilverDoantion=3000000;viewer.AsMember.ConquerPointDonation=200000;
            viewer.AsMember.GuideDoantion=123;viewer.Guild.SilverFund=17;viewer.Guild.ConquerPointFund=19;
            viewer.Entity.PKPoints=10;EntityTable.SaveEntity(viewer);
            var profile=new byte[68];Writer.WriteUInt16(60,0,profile);Writer.WriteUInt16(PacketID.TopGuild,2,profile);Writer.WriteUInt64(PacketHandler.ClientSeal,60,profile);
            for(int i=0;i<4;i++)PacketHandler.HandlePacket(profile,viewer);
            Check(viewer.Guild.SilverFund==17 && viewer.Guild.ConquerPointFund==19 && viewer.AsMember.Rank==Enums.GuildMemberRank.Member
                && viewer.Entity.GuildRank==(ushort)Enums.GuildMemberRank.Member,"repeated guild profile requests cannot refill spent funds or promote the viewer");
            Check(System.BitConverter.ToUInt32(profile,8)==3000000 && System.BitConverter.ToUInt32(profile,12)==200000 && System.BitConverter.ToUInt32(profile,16)==123,
                "guild profile reports donation and guide counters independently of treasury balance");
            Check(Scalar("SELECT silverfund FROM guilds WHERE id=240",0)==502500 && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",viewer.Entity.UID)==(long)Enums.GuildMemberRank.Member,
                "viewing the guild profile leaves saved treasury and rank unchanged");
            var shortProfile=new byte[60];Writer.WriteUInt16(52,0,shortProfile);Writer.WriteUInt16(PacketID.TopGuild,2,shortProfile);Writer.WriteUInt64(PacketHandler.ClientSeal,52,shortProfile);
            PacketHandler.HandlePacket(shortProfile,viewer);
            Check(viewer.Guild.SilverFund==17 && viewer.AsMember.Rank==Enums.GuildMemberRank.Member,"short sealed profile is rejected before writing response fields");
            Query("CREATE TABLE IF NOT EXISTS pt_arsenal AS TABLE public.pt_arsenal WITH NO DATA");
            var enhancements=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildArsenalPurchases");
            Call(enhancements,null,"Initialize");Call(enhancements,null,"Initialize");
            // CREATE TABLE AS omits the production column default when the catalog already has this field.
            Query("ALTER TABLE pt_arsenal ALTER COLUMN enhancement_expires_utc_ticks SET DEFAULT 0");
            var buyer=PrizeLeader(250);Query("UPDATE guilds SET silverfund=9500000 WHERE id=250");buyer.Guild.SilverFund=10000000;
            Action<GameState,byte> unlock=(owner,slot)=>{var bytes=new byte[28];Writer.WriteUInt16(20,0,bytes);Writer.WriteUInt16(PacketID.ArsenalPackets,2,bytes);bytes[8]=slot;Writer.WriteUInt64(PacketHandler.ClientSeal,20,bytes);PacketHandler.HandlePacket(bytes,owner);};
            Parallel.For(0,8,i=>unlock(buyer,0));
            Check(!Fenced(buyer) && buyer.Guild.SilverFund==4500000 && buyer.Guild.Arsenal.Arsenals.ContainsKey(Revora.Game.Features.Arsenal_ID.Headgear)
                && Scalar("SELECT count(*) FROM pt_arsenal WHERE syn_id=250",0)==1,"ordinary guild leader unlocks once under repeated concurrent packets without restoring a prior fee");
            unlock(buyer,1);Check(buyer.Guild.SilverFund==4500000 && Scalar("SELECT count(*) FROM pt_arsenal WHERE syn_id=250",0)==1,"insufficient saved guild funds cannot buy another arsenal");
            var ordinary=PurseMember(buyer,Enums.GuildMemberRank.Member);unlock(ordinary,1);unlock(buyer,9);
            Check(Scalar("SELECT count(*) FROM pt_arsenal WHERE syn_id=250",0)==1,"member and invalid-slot requests cannot unlock an arsenal");
            Query("UPDATE guilds SET silverfund=20000000 WHERE id=250");
            Query("CREATE FUNCTION refuse_arsenal_unlock() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER refuse_arsenal_unlock BEFORE INSERT ON pt_arsenal FOR EACH ROW EXECUTE FUNCTION refuse_arsenal_unlock()");
            try{unlock(buyer,1);}finally{Query("DROP TRIGGER refuse_arsenal_unlock ON pt_arsenal;DROP FUNCTION refuse_arsenal_unlock()");}
            Check(Fenced(buyer) && Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==20000000 && buyer.Guild.Arsenal.Arsenals.Count==1,
                "refused arsenal insert rolls back its fee and leaves runtime ownership unchanged");
            buyer=ReloadGold(buyer);unlock(buyer,1);
            Check(!Fenced(buyer) && buyer.Guild.SilverFund==15000000 && buyer.Guild.Arsenal.Arsenals.Count==2,"reloaded ordinary leader can retry the rolled-back unlock");
            unlock(buyer,2);Check(buyer.Guild.SilverFund==5000000 && buyer.Guild.Arsenal.Arsenals.Count==3,"third arsenal uses the established ten-million silver price");
            ArsenalTable.LoadArsenal(buyer.Guild);unlock(buyer,2);
            Check(buyer.Guild.Arsenal.Arsenals.Count==3 && Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==5000000,"production arsenal reload retains purchased slots and repeated unlock does not charge");
            Query("ALTER TABLE guilds ADD COLUMN maxenchant bigint NOT NULL DEFAULT 0");
            Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(240,8,1,0,0)");
            Query("UPDATE guilds SET silverfund=silverfund-500000 WHERE id=250");
            Action<GameState,byte,uint> enhance=(owner,slot,level)=>{var bytes=new byte[28];Writer.WriteUInt16(20,0,bytes);Writer.WriteUInt16(PacketID.ArsenalPackets,2,bytes);bytes[4]=3;bytes[8]=slot;Writer.WriteUInt32(level,16,bytes);Writer.WriteUInt64(PacketHandler.ClientSeal,20,bytes);PacketHandler.HandlePacket(bytes,owner);};
            var purchaseUtc=new DateTime(2035,2,1,12,30,0,DateTimeKind.Utc);
            enhancements.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>purchaseUtc));
            try { Parallel.For(0,8,i=>enhance(buyer,0,2)); }
            finally { enhancements.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow)); }
            Check(Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=8",0)==purchaseUtc.AddDays(30).Ticks
                && buyer.Guild.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear].EnhancementExpDate==purchaseUtc.AddDays(30),
                "enhancement deadline is committed with the fee using the authoritative UTC clock");
            Check(!Fenced(buyer) && buyer.Guild.SilverFund==4400000 && buyer.Guild.maxenchant==2 && buyer.Guild.Arsenal.BattlePower==2,
                "ordinary guild leader enhancement charges once from saved funds and projects bounded battle power");
            Check(Scalar("SELECT arsenal_enhance FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=8",0)==2
                && Scalar("SELECT arsenal_enhance FROM pt_arsenal WHERE syn_id=240 AND arsenal_id=8",0)==0,"enhancement cannot update the same slot in a different guild");
            enhance(ordinary,1,1);enhance(buyer,1,0);enhance(buyer,1,uint.MaxValue);enhance(buyer,0,1);
            Check(Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==4400000,"member, zero, overflow and downgrade enhancement requests do not charge");
            Query("CREATE FUNCTION refuse_arsenal_enhance() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER refuse_arsenal_enhance BEFORE UPDATE ON pt_arsenal FOR EACH ROW EXECUTE FUNCTION refuse_arsenal_enhance()");
            try{enhance(buyer,1,1);}finally{Query("DROP TRIGGER refuse_arsenal_enhance ON pt_arsenal;DROP FUNCTION refuse_arsenal_enhance()");}
            Check(Fenced(buyer) && Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==4400000 && Scalar("SELECT maxenchant FROM guilds WHERE id=250",0)==2,
                "failed enhancement write rolls back fee and saved guild power");
            Check(Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=1",0)==0,
                "failed enhancement does not leave a deadline behind");
            buyer=ReloadGold(buyer);enhance(buyer,1,1);
            Check(!Fenced(buyer) && buyer.Guild.SilverFund==4350000 && buyer.Guild.maxenchant==3,"reloaded ordinary leader retries a rolled-back enhancement");
            ArsenalTable.LoadArsenal(buyer.Guild);enhance(buyer,1,1);
            var recovered=buyer.Guild.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear].EnhancementExpDate;
            Check(recovered==purchaseUtc.AddDays(30) && recovered.Kind==DateTimeKind.Utc,
                "production reload preserves the exact purchased UTC deadline without renewing it");
            Check(Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=240 AND arsenal_id=8",0)==0,
                "legacy and unrelated guild slots receive no invented expiry");
            Check(Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==4350000 && buyer.Guild.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Armor].Enhancement==1,
                "production arsenal reload retains enhancement and rejects another fee for the same level");
            long armorDeadline=Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=1",0);
            Query("UPDATE pt_arsenal SET enhancement_expires_utc_ticks=enhancement_expires_utc_ticks+1 WHERE syn_id=250 AND arsenal_id=1");
            enhance(buyer,1,2);
            Check(Fenced(buyer) && Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==4350000
                && Scalar("SELECT arsenal_enhance FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=1",0)==1,
                "stale saved deadline rejects an upgrade without charging or renewing it");
            Query("UPDATE pt_arsenal SET enhancement_expires_utc_ticks=@deadline WHERE syn_id=250 AND arsenal_id=1","deadline",armorDeadline);
            buyer=ReloadGold(buyer);
            string oldBulletin=buyer.Guild.Bulletin;ulong oldFund=buyer.Guild.SilverFund;
            var ad=new byte[292];Writer.WriteUInt16(284,0,ad);Writer.WriteUInt16(2225,2,ad);Writer.WriteUInt64(500000,264,ad);Writer.WriteUInt64(PacketHandler.ClientSeal,284,ad);
            PacketHandler.HandlePacket(ad,buyer);Guild_AdvertiseRecruit.Process(ad,buyer);
            Check(buyer.Guild.SilverFund==oldFund && buyer.Guild.Bulletin==oldBulletin && Scalar("SELECT silverfund FROM guilds WHERE id=250",0)==4350000,
                "unimplemented advertisement publishing cannot consume guild funds through either legacy adapter");
            VerifyGuildInscriptions(buyer);
            VerifyArsenalExpiry();
            VerifyGuildAppointments();
        }finally{type.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
    static void VerifyArsenalExpiry() {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildArsenalPurchases",true);
        var g=PrizeLeader(254);var arsenal=g.Guild.Arsenal;
        var now=new DateTime(2036,1,1,0,0,0,DateTimeKind.Utc);
        service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            ulong[] thresholds={0,1999999,2000000,3999999,4000000,9999999,10000000,ulong.MaxValue};
            byte[] expected={0,0,1,1,2,2,3,3};
            for(int i=0;i<thresholds.Length;i++)Check((byte)Call(service,null,"DonationPower",thresholds[i])==expected[i],"arsenal donation power boundary "+thresholds[i]);
            for(byte slot=1;slot<=8;slot++) {
                var state=new Revora.Game.Features.Arsenal_State{ID=(Revora.Game.Features.Arsenal_ID)slot,Unlocked=true};
                state.Inscribed.Add(slot,new Revora.Game.Features.Arsenal_Client{donation=2000000});arsenal.Arsenals.Add(state.ID,state);
            }
            Call(service,null,"Rebuild",g.Guild);
            Check(arsenal.BattlePower==5,"eight one-power slots contribute only the strongest five");
            foreach(var state in arsenal.Arsenals.Values)state.Inscribed.Values.First().donation=10000000;
            Call(service,null,"Rebuild",g.Guild);Check(arsenal.BattlePower==15,"five strongest max-power slots cap guild power at fifteen");
            foreach(var state in arsenal.Arsenals.Values)state.Inscribed.Clear();
            Call(service,null,"Rebuild",g.Guild);Check(arsenal.BattlePower==0 && arsenal.Arsenals.Values.All(v=>v.Potency==0),"donation removal rebuilds down to zero without stale threshold flags");
            var head=arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear];head.Enhancement=2;head.ars_bp=2;head.EnhancementExpDate=now.AddDays(1);
            head.Inscribed.Add(1,new Revora.Game.Features.Arsenal_Client{donation=2000000});
            Call(service,null,"Rebuild",g.Guild);Check(arsenal.BattlePower==2 && head.Potency==1,"level-two enhancement raises a one-power slot to two rather than stacking to three");
            head.Unlocked=false;Call(service,null,"Rebuild",g.Guild);Check(arsenal.BattlePower==0,"locked slots contribute neither donation nor enhancement power");
            Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp,enhancement_expires_utc_ticks) VALUES(254,8,1,2,2,@end),(254,1,1,1,1,@end),(254,2,1,2,2,@later)",
                "end",now.AddDays(1).Ticks,"later",now.AddDays(2).Ticks);
            Query("UPDATE guilds SET maxenchant=5 WHERE id=254");
            ArsenalTable.LoadArsenal(g.Guild);ArsenalTable.LoadArsenal2(g.Guild);arsenal=g.Guild.Arsenal;
            Check(arsenal.BattlePower==5 && g.Guild.maxenchant==5,"production slot and inscription reload reconstructs enhancement power before any maintenance tick");
            Call(service,null,"Publish",g);Check(g.Entity.GuildSharedBp==5,"ordinary leader receives reconstructed shared power");
            now=now.AddDays(1).AddTicks(-1);Call(service,null,"Refresh",g.Guild);
            Check(arsenal.BattlePower==5 && Scalar("SELECT sum(arsenal_enhance::bigint) FROM pt_arsenal WHERE syn_id=254",0)==5,"enhancement remains active immediately before expiry");
            now=now.AddTicks(1);
            Query("CREATE FUNCTION refuse_arsenal_expiry() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.id=254 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER refuse_arsenal_expiry BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION refuse_arsenal_expiry()");
            bool failed=false;
            try{Call(service,null,"Refresh",g.Guild);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER refuse_arsenal_expiry ON guilds;DROP FUNCTION refuse_arsenal_expiry()");}
            Check(failed && Scalar("SELECT sum(arsenal_enhance::bigint) FROM pt_arsenal WHERE syn_id=254",0)==5
                && Scalar("SELECT maxenchant FROM guilds WHERE id=254",0)==5,"failed guild-total settlement rolls back both expired slot updates");
            Check(arsenal.BattlePower==2,"failed settlement cannot prolong an expired bonus in combat");
            ArsenalTable.LoadArsenal(g.Guild);ArsenalTable.LoadArsenal2(g.Guild);arsenal=g.Guild.Arsenal;
            Check(arsenal.BattlePower==2,"reload excludes expired saved bonuses before retrying their settlement");
            Call(service,null,"Refresh",g.Guild);Call(service,null,"Publish",g);
            Check(g.Entity.GuildSharedBp==2 && g.Guild.maxenchant==2 && arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear].Enhancement==0
                && Scalar("SELECT maxenchant FROM guilds WHERE id=254",0)==2 && Scalar("SELECT sum(arsenal_enhance::bigint) FROM pt_arsenal WHERE syn_id=254",0)==2,
                "retry settles all due slots and refreshes ordinary-player power at the exact deadline");
            long treasury=Scalar("SELECT silverfund FROM guilds WHERE id=254",0);
            Call(service,null,"Refresh",g.Guild);ArsenalTable.LoadArsenal(g.Guild);ArsenalTable.LoadArsenal2(g.Guild);
            Check(g.Guild.Arsenal.BattlePower==2 && Scalar("SELECT silverfund FROM guilds WHERE id=254",0)==treasury,
                "repeated settlement and reload neither charge nor resurrect expired bonuses");
            Check(Scalar("SELECT arsenal_enhance FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=8",0)==2,"expiry cannot clear the same slot in another guild");
            Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(254,3,1,1,1)");
            ArsenalTable.LoadArsenal(g.Guild);ArsenalTable.LoadArsenal2(g.Guild);now=now.AddDays(10);Call(service,null,"Refresh",g.Guild);
            Check(g.Guild.Arsenal.BattlePower==1 && Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=254 AND arsenal_id=3",0)==0,
                "undated legacy enhancement retains its existing benefit without an invented renewal");
            Query("UPDATE guilds SET silverfund=500000 WHERE id=254");
            bool bought=(bool)Call(service,null,"Enhance",g,g.Guild.Arsenal,Revora.Game.Features.Arsenal_ID.Headgear,2u);
            Check(bought && !Fenced(g) && g.Guild.SilverFund==400000 && g.Guild.Arsenal.BattlePower==3
                && Scalar("SELECT enhancement_expires_utc_ticks FROM pt_arsenal WHERE syn_id=254 AND arsenal_id=8",0)==now.AddDays(30).Ticks,
                "ordinary leader can buy a fresh thirty-day enhancement after the original expired");
            var peer=PurseMember(g,Enums.GuildMemberRank.Member);Call(service,null,"Publish",peer);
            Check(peer.Entity.GuildSharedBp==3,"ordinary non-leader peer receives the updated shared power");
            Call(service,null,"Enhance",g,g.Guild.Arsenal,Revora.Game.Features.Arsenal_ID.Headgear,2u);
            Check(Scalar("SELECT silverfund FROM guilds WHERE id=254",0)==400000,"replaying the renewed enhancement cannot charge again");
            VerifyArsenalRecoveryDeadline(now.AddDays(30).Ticks);
        } finally {service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
    static void VerifyArsenalRecoveryDeadline(long deadline) {
        var identity=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildIdentity",true);
        Query("CREATE TABLE IF NOT EXISTS itemadding AS TABLE public.itemadding WITH NO DATA");
        Call(identity,null,"EnsureSchema");
        var snapshotType=identity.GetNestedType("Snapshot",System.Reflection.BindingFlags.NonPublic);
        Func<object> snapshot=()=>{var value=Activator.CreateInstance(snapshotType,true);snapshotType.GetField("Incarnation").SetValue(value,Guid.NewGuid());return value;};
        Func<object,object> read=value=>{
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var marks=new System.Collections.Generic.List<string>{"arsenal:1:","arsenal:2:","arsenal:3:","arsenal:8:"};
                return Call(identity,null,"ReadArsenal",c,t,value,254u,marks);
            }}
        };
        object saved=snapshot();Check(read(saved)==null,"guild recovery reads the dated arsenal with matching association marks");
        var rows=(Array)snapshotType.GetField("Arsenal").GetValue(saved);
        var rowType=rows.GetType().GetElementType();var ticks=rowType.GetField("EnhancementExpiresUtcTicks");
        var head=rows.Cast<object>().Single(r=>(int)rowType.GetField("Id").GetValue(r)==8);
        Check((long)ticks.GetValue(head)==deadline,"guild recovery snapshot retains the exact saved UTC deadline");
        var copy=(Array)Call(identity,null,"CopyArsenal",new object[]{rows});
        ticks.SetValue(head,0L);
        var copiedHead=copy.Cast<object>().Single(r=>(int)rowType.GetField("Id").GetValue(r)==8);
        Check((long)ticks.GetValue(copiedHead)==deadline,"copied recovery arsenal cannot lose its deadline through snapshot mutation");
        foreach(long bad in new[]{-1L,DateTime.MaxValue.Ticks+1}) {
            Query("UPDATE pt_arsenal SET enhancement_expires_utc_ticks=@ticks WHERE syn_id=254 AND arsenal_id=8","ticks",bad);
            try {
                var invalid=snapshot();Check(read(invalid)!=null && ((Array)snapshotType.GetField("Arsenal").GetValue(invalid)).Length==0,
                    "invalid saved deadline rejects the recovery arsenal: "+bad);
                ticks.SetValue(head,bad);bool refused=false;
                try{Call(identity,null,"CopyArsenal",new object[]{rows});}catch(InvalidOperationException){refused=true;}
                Check(refused,"invalid supplied deadline cannot be copied into recovery projection: "+bad);
            } finally {Query("UPDATE pt_arsenal SET enhancement_expires_utc_ticks=@ticks WHERE syn_id=254 AND arsenal_id=8","ticks",deadline);}
        }
    }
    static byte[] InscriptionPacket(byte action,byte slot,uint uid){
        var b=new byte[28];Writer.WriteUInt16(20,0,b);Writer.WriteUInt16(PacketID.ArsenalPackets,2,b);b[4]=action;b[8]=slot;
        Writer.WriteUInt32(uid,12,b);Writer.WriteUInt64(PacketHandler.ClientSeal,20,b);return b;
    }
    static void VerifyGuildInscriptions(GameState leader){
        Query("CREATE TABLE IF NOT EXISTS pt_arsenal_inscribed AS TABLE public.pt_arsenal_inscribed WITH NO DATA");
        var inscriptions=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildArsenalInscriptions",true);
        Check((int)Call(inscriptions,null,"Limit",(byte)99,(byte)0)==7 && (int)Call(inscriptions,null,"Limit",(byte)100,(byte)0)==14
            && (int)Call(inscriptions,null,"Limit",(byte)15,(byte)1)==21 && (int)Call(inscriptions,null,"Limit",(byte)15,(byte)2)==30,
            "arsenal capacity follows level 100 and rebirth boundaries");
        var p=PurseMember(leader,Enums.GuildMemberRank.Member);var other=PurseMember(leader,Enums.GuildMemberRank.Member);
        var item=Pack(p,111009,1);var foreign=Pack(other,111009,1);var packet=InscriptionPacket(1,0,item.UID);
        var lowQuality=Pack(p,111007,1);PacketHandler.HandlePacket(InscriptionPacket(1,0,lowQuality.UID),p);
        Check(!Fenced(p) && !lowQuality.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",p.Entity.UID)==0,"below-Elite equipment cannot be inscribed");
        PacketHandler.HandlePacket(InscriptionPacket(1,0,foreign.UID),p);
        PacketHandler.HandlePacket(InscriptionPacket(1,1,item.UID),p);
        Check(!Fenced(p) && !item.Inscribed && !foreign.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed",0)==0,"foreign equipment and wrong arsenal slot cannot be inscribed");
        PacketHandler.HandlePacket(packet,p);
        Check(!Fenced(p) && item.Inscribed && p.AsMember.Arsenal_Donation==16660 && HasAchievement(p,10226),"ordinary member inscription updates item, donation and achievement immediately");
        Check(other.AsMember.ArsenalsList.Single(row=>row.EntityID==p.Entity.UID).arsenaldoantion==16660
            && leader.AsMember.TotalDonationsList.Single(row=>row.EntityID==p.Entity.UID).totaldoantion==16660,
            "committed inscription publishes Arsenal and Total rankings to ordinary peers immediately");
        var inscribedView=other.AsMember.ArsenalsList;
        Parallel.For(0,8,n=>PacketHandler.HandlePacket(packet,p));
        Check(ReferenceEquals(inscribedView,other.AsMember.ArsenalsList),"duplicate inscription cannot republish ranking snapshots");
        Check(Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",p.Entity.UID)==1 && p.AsMember.Arsenal_Donation==16660,"concurrent inscription replay cannot duplicate donation");
        Check(Scalar("SELECT arsenal_donation FROM pt_arsenal WHERE syn_id=250 AND arsenal_id=8",0)==16660
            && Scalar("SELECT COALESCE(arsenal_donation::numeric,0) FROM pt_arsenal WHERE syn_id=240 AND arsenal_id=8",0)==0,"inscription donation is scoped to the owning guild");
        PacketHandler.HandlePacket(InscriptionPacket(2,0,item.UID),other);
        Check(item.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",p.Entity.UID)==1,"another guild member cannot remove the owner's inscription");
        p=ReloadBall(p);p.Entity.MyAchievement.Send();p.Inventory.TryGetItem(item.UID,out item);
        ArsenalTable.LoadArsenal(p.Guild);ArsenalTable.LoadArsenal2(p.Guild);
        Check(item!=null && item.Inscribed && HasAchievement(p,10226) && p.Guild.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear].Inscribed.ContainsKey(item.UID),"character and production arsenal reload retain inscription and achievement");
        PacketHandler.HandlePacket(InscriptionPacket(2,0,item.UID),p);
        Check(!Fenced(p) && !item.Inscribed && p.AsMember.Arsenal_Donation==0 && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",p.Entity.UID)==0 && HasAchievement(p,10226),"owner removal reverses donation but preserves earned achievement");
        Check(other.AsMember.ArsenalsList.Single(row=>row.EntityID==p.Entity.UID).arsenaldoantion==0
            && !leader.AsMember.TotalDonationsList.Any(row=>row.EntityID==p.Entity.UID),
            "committed removal clears the Arsenal score and removes a zero Total contributor");
        var failed=PurseMember(leader,Enums.GuildMemberRank.Member);var failItem=Pack(failed,111009,1);
        Query("CREATE FUNCTION refuse_inscription_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_inscription_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10226) EXECUTE FUNCTION refuse_inscription_evidence()");
        try{PacketHandler.HandlePacket(InscriptionPacket(1,0,failItem.UID),failed);}finally{Query("DROP TRIGGER refuse_inscription_evidence ON revora_achievement_evidence;DROP FUNCTION refuse_inscription_evidence()");}
        Check(Fenced(failed) && !failItem.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",failed.Entity.UID)==0
            && Scalar("SELECT inscribed FROM items WHERE uid=@uid",failItem.UID)==0,"failed achievement persistence rolls back inscription, item flag and donation");
        failed=ReloadBall(failed);PacketHandler.HandlePacket(InscriptionPacket(1,0,failItem.UID),failed);
        Check(!Fenced(failed) && HasAchievement(failed,10226),"ordinary member can retry inscription after reload");
        var fast=Pack(p,130009,1);
        var bulk=new byte[21];Writer.WriteUInt16(13,0,bulk);Writer.WriteUInt16(PacketID.FastInscribeItem,2,bulk);bulk[4]=2;
        Writer.WriteUInt32(foreign.UID,5,bulk);Writer.WriteUInt32(fast.UID,9,bulk);Writer.WriteUInt64(PacketHandler.ClientSeal,13,bulk);
        PacketHandler.HandlePacket(bulk,p);
        Check(!Fenced(p) && fast.Inscribed && !foreign.Inscribed,"bulk inscription uses owned equipment only");
        var malformed=(byte[])bulk.Clone();malformed[4]=40;PacketHandler.HandlePacket(malformed,p);
        PacketHandler.HandlePacket(new byte[4],p);
        Check(!Fenced(p) && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",p.Entity.UID)==1,"truncated bulk count cannot read or mutate more items");
        var equipped=Pack(p,111009,1);p.Inventory.Remove(equipped.UID,Enums.ItemUse.None,true);equipped.Position=ConquerItem.Head;
        p.Equipment.Add(equipped,Enums.ItemUse.None);ConquerItemTable.UpdatePosition(equipped);
        PacketHandler.HandlePacket(InscriptionPacket(1,0,equipped.UID),p);
        Check(!Fenced(p) && equipped.Inscribed,"owned worn equipment can be inscribed without unequipping");
        Query("CREATE FUNCTION refuse_inscription_remove() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_inscription_remove BEFORE DELETE ON pt_arsenal_inscribed FOR EACH ROW EXECUTE FUNCTION refuse_inscription_remove()");
        ulong before=p.AsMember.Arsenal_Donation;
        var beforeRemovalView=other.AsMember.ArsenalsList;
        try{PacketHandler.HandlePacket(InscriptionPacket(2,0,equipped.UID),p);}finally{Query("DROP TRIGGER refuse_inscription_remove ON pt_arsenal_inscribed;DROP FUNCTION refuse_inscription_remove()");}
        Check(Fenced(p) && equipped.Inscribed && p.AsMember.Arsenal_Donation==before && Scalar("SELECT inscribed FROM items WHERE uid=@uid",equipped.UID)==1,"failed removal preserves saved flag and donation");
        Check(ReferenceEquals(beforeRemovalView,other.AsMember.ArsenalsList)
            && other.AsMember.ArsenalsList.Single(row=>row.EntityID==p.Entity.UID).arsenaldoantion==before,
            "rolled-back inscription removal preserves the last committed peer ranking");
        p=ReloadBall(p);PacketHandler.HandlePacket(InscriptionPacket(2,0,equipped.UID),p);
        Check(!Fenced(p) && Scalar("SELECT inscribed FROM items WHERE uid=@uid",equipped.UID)==0,"reloaded owner can remove an equipped inscription");
        Check(other.AsMember.ArsenalsList.Single(row=>row.EntityID==p.Entity.UID).arsenaldoantion==p.AsMember.Arsenal_Donation
            && p.AsMember.Arsenal_Donation<before,
            "reloaded owner's successful retry publishes the reduced committed contribution");
        var mismatch=PurseMember(leader,Enums.GuildMemberRank.Member);var stolen=Pack(mismatch,111009,1);
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)other.Entity.UID,"uid",(long)stolen.UID);
        PacketHandler.HandlePacket(InscriptionPacket(1,0,stolen.UID),mismatch);
        Check(Fenced(mismatch) && !stolen.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",mismatch.Entity.UID)==0,"saved item ownership overrides stale inventory references");
        var unqualified=PurseMember(leader,Enums.GuildMemberRank.Member);var blocked=Pack(unqualified,111009,1);
        unqualified.Fake=true;PacketHandler.HandlePacket(InscriptionPacket(1,0,blocked.UID),unqualified);unqualified.Fake=false;
        var head=unqualified.Guild.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Headgear];head.Unlocked=false;
        try{PacketHandler.HandlePacket(InscriptionPacket(1,0,blocked.UID),unqualified);}finally{head.Unlocked=true;}
        Check(!Fenced(unqualified) && !blocked.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",unqualified.Entity.UID)==0,"simulated players and locked arsenals cannot earn inscription credit");
        var capped=PurseMember(leader,Enums.GuildMemberRank.Member);capped.Entity.Level=99;capped.Entity.Reborn=0;EntityTable.SaveEntity(capped);
        ConquerItem last=null;
        for(int n=0;n<7;n++){last=Pack(capped,111009,1);PacketHandler.HandlePacket(InscriptionPacket(1,0,last.UID),capped);}
        var excess=Pack(capped,111009,1);PacketHandler.HandlePacket(InscriptionPacket(1,0,excess.UID),capped);
        Check(!Fenced(capped) && !excess.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",capped.Entity.UID)==7,"non-reborn level 99 member stops at seven inscriptions in one arsenal");
        PacketHandler.HandlePacket(InscriptionPacket(2,0,last.UID),capped);PacketHandler.HandlePacket(InscriptionPacket(1,0,excess.UID),capped);
        Check(!Fenced(capped) && excess.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",capped.Entity.UID)==7,"removing an inscription releases capacity");
        var at100=Pack(capped,111009,1);capped.Entity.Level=100;EntityTable.SaveEntity(capped);PacketHandler.HandlePacket(InscriptionPacket(1,0,at100.UID),capped);
        Check(!Fenced(capped) && at100.Inscribed && Scalar("SELECT count(*) FROM pt_arsenal_inscribed WHERE uid=@uid",capped.Entity.UID)==8,"saved level 100 unlocks the next inscription allowance");
        VerifyGuildLevels(leader);
    }

    static void PrepareGuildMaterialization() {
        // Extend the deliberately minimal guild fixture for the actual startup materializer.
        foreach(string column in new[]{"unionid","enrollmentdate","bulletindate","levelrequirement","rebornrequirement","adlevelrequirement","adrebornrequirement","adclassrequirement","classrequirement","ctfpoints","ctfreward","ctfdonationcps","ctfdonationsilver","ctfdonationsilverold","ctfdonationcpsold","autorecruit","malefemale","ad_rate","adend"})
            Query("ALTER TABLE guilds ADD COLUMN IF NOT EXISTS "+column+" bigint NOT NULL DEFAULT 0");
        foreach(string column in new[]{"name","bulletin","adv"})Query("ALTER TABLE guilds ADD COLUMN IF NOT EXISTS "+column+" text NOT NULL DEFAULT ''");
    }

    static void VerifyGuildLevels(GameState leader) {
        PrepareGuildMaterialization();
        var sample=new Revora.Game.ConquerStructures.Society.Guild("level-fixture");
        sample.Arsenal=new Revora.Game.Features.Arsenal(1);
        Call(typeof(GuildTable),null,"RebuildLevel",sample);
        Check(sample.Level==1,"empty guild has base level one");
        for(byte slot=1;slot<=8;slot++) {
            var id=(Revora.Game.Features.Arsenal_ID)slot;
            var state=new Revora.Game.Features.Arsenal_State{ID=id,Unlocked=true};sample.Arsenal.Arsenals[id]=state;
            state.Inscribed[1]=new Revora.Game.Features.Arsenal_Client{donation=4999999};
            Call(typeof(GuildTable),null,"RebuildLevel",sample);
            Check(sample.Level==slot,"just below five million adds no level: "+slot);
            state.Inscribed[1].donation=5000000;
            for(int repeat=0;repeat<3;repeat++)Call(typeof(GuildTable),null,"RebuildLevel",sample);
            Check(sample.Level==slot+1,"exactly five million qualifies once and remains stable: "+slot);
            state.Unlocked=false;sample.Level=255;Call(typeof(GuildTable),null,"RebuildLevel",sample);
            Check(sample.Level==slot,"locked arsenal and corrupted prior level cannot inflate level: "+slot);
            state.Unlocked=true;
        }
        Call(typeof(GuildTable),null,"RebuildLevel",sample);Check(sample.Level==9,"all eight qualifying arsenals yield level nine");
        sample.Arsenal.Arsenals.Clear();Call(typeof(GuildTable),null,"RebuildLevel",sample);
        Check(sample.Level==1,"removing all arsenals returns to base without byte underflow");

        var owner=PurseMember(leader,Enums.GuildMemberRank.Member);var items=new System.Collections.Generic.List<uint>();
        for(int n=0;n<4;n++) {
            var item=Pack(owner,111009,1);item.Plus=12;item.SocketOne=item.SocketTwo=Enums.Gem.EmptySocket;
            ConquerItemTable.UpdatePlus(item);ConquerItemTable.UpdateSockets(item);items.Add(item.UID);
            PacketHandler.HandlePacket(InscriptionPacket(1,0,item.UID),owner);
        }
        Check(!Fenced(owner) && owner.Guild.Level==2,"ordinary committed inscriptions cross the threshold and update level immediately");
        owner=ReloadBall(owner);
        Check(!Fenced(owner) && owner.Guild.Level==2,"ordinary character login cannot reset its shared guild level");
        var loaded=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",leader.Guild.ID);
        Check(loaded.Level==2,"full guild reload reconstructs level from saved inscription donations");
        Query("CREATE FUNCTION refuse_level_remove() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER refuse_level_remove BEFORE DELETE ON pt_arsenal_inscribed FOR EACH ROW EXECUTE FUNCTION refuse_level_remove()");
        try{PacketHandler.HandlePacket(InscriptionPacket(2,0,items[0]),owner);}
        finally{Query("DROP TRIGGER refuse_level_remove ON pt_arsenal_inscribed;DROP FUNCTION refuse_level_remove()");}
        Check(Fenced(owner) && owner.Guild.Level==2,"rolled-back removal cannot lower guild level");
        owner=ReloadBall(owner);PacketHandler.HandlePacket(InscriptionPacket(2,0,items[0]),owner);
        Check(!Fenced(owner) && owner.Guild.Level==1,"committed removal below threshold lowers the level after recovery");
        loaded=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",leader.Guild.ID);
        Check(loaded.Level==1,"reloaded guild retains the reduced level from saved contributions");
    }

    static void AppointGuild(GameState actor,GameState target,Enums.GuildMemberRank rank){
        var b=GuildTransferPacket(target.AsMember);Writer.WriteUInt16((ushort)rank,8,b);GuildCommand.Process(b,actor);
    }
    static void VerifyGuildAppointments(){
        var leader=PrizeLeader(290);leader.Entity.ConquerPoints=5000;EntityTable.SaveEntity(leader);
        foreach(var rank in new[]{Enums.GuildMemberRank.HDeputyLeader,Enums.GuildMemberRank.HonoraryManager,Enums.GuildMemberRank.HonorarySuperv,Enums.GuildMemberRank.HonorarySteward}){
            var p=PurseMember(leader,Enums.GuildMemberRank.Member);uint before=leader.Entity.ConquerPoints;
            uint cost=(uint)Call(Leadership,null,"Price",rank);var b=GuildTransferPacket(p.AsMember);Writer.WriteUInt16((ushort)rank,8,b);
            Parallel.For(0,8,n=>GuildCommand.Process(b,leader));
            Check(!Fenced(leader) && !Fenced(p) && p.AsMember.Rank==rank && p.Entity.GuildRank==(ushort)rank
                && leader.Entity.ConquerPoints==before-cost && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",leader.Entity.UID)==before-cost,
                "honorary appointment charges the leader's personal CPs once: "+rank);
            Check(HasAchievement(p,10221) && !HasAchievement(p,10223) && !HasAchievement(p,10224),"honorary appointment grants only its own achievement: "+rank);
            p=ReloadGold(p);p.Entity.MyAchievement.Send();Check(p.Entity.GuildRank==(ushort)rank && HasAchievement(p,10221),"honorary recipient persists across character login: "+rank);
        }
        var deputy=PurseMember(leader,Enums.GuildMemberRank.Member);AppointGuild(leader,deputy,Enums.GuildMemberRank.DeputyLeader);
        Check(!Fenced(leader) && deputy.AsMember.Rank==Enums.GuildMemberRank.DeputyLeader && HasAchievement(deputy,10222),"leader appoints an ordinary deputy with saved achievement");
        var steward=PurseMember(leader,Enums.GuildMemberRank.Member);AppointGuild(deputy,steward,Enums.GuildMemberRank.Steward);
        Check(!Fenced(deputy) && steward.AsMember.Rank==Enums.GuildMemberRank.Steward && HasAchievement(steward,10225),"deputy can appoint an ordinary steward");
        var denied=PurseMember(leader,Enums.GuildMemberRank.Member);uint funds=leader.Entity.ConquerPoints;
        AppointGuild(steward,denied,Enums.GuildMemberRank.DeputyLeader);AppointGuild(deputy,denied,Enums.GuildMemberRank.HonorarySteward);
        AppointGuild(leader,denied,Enums.GuildMemberRank.Manager);AppointGuild(leader,denied,(Enums.GuildMemberRank)999);
        Check(denied.AsMember.Rank==Enums.GuildMemberRank.Member && leader.Entity.ConquerPoints==funds,"unauthorized, donation-ranked and unknown appointments are rejected");
        var offline=PurseMember(leader,Enums.GuildMemberRank.Member);Kernel.GamePool.Remove(offline.Entity.UID);AppointGuild(leader,offline,Enums.GuildMemberRank.HonorarySteward);
        Check(!Fenced(leader) && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",offline.Entity.UID)==(int)Enums.GuildMemberRank.HonorarySteward,"offline recipient's appointment is saved");
        offline=ReloadGold(offline);offline.Entity.MyAchievement.Send();Check(HasAchievement(offline,10221),"offline honorary recipient earns achievement on login");
        var failed=PurseMember(leader,Enums.GuildMemberRank.Member);funds=leader.Entity.ConquerPoints;
        Query("CREATE FUNCTION refuse_office_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_office_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10221) EXECUTE FUNCTION refuse_office_evidence()");
        try{AppointGuild(leader,failed,Enums.GuildMemberRank.HonoraryManager);}finally{Query("DROP TRIGGER refuse_office_evidence ON revora_achievement_evidence;DROP FUNCTION refuse_office_evidence()");}
        Check(Fenced(leader) && Fenced(failed) && leader.Entity.ConquerPoints==funds && failed.AsMember.Rank==Enums.GuildMemberRank.Member
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",leader.Entity.UID)==funds
            && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",failed.Entity.UID)==(int)Enums.GuildMemberRank.Member,"evidence failure rolls back honorary fee and office");
        leader=ReloadGold(leader);failed=ReloadGold(failed);AppointGuild(leader,failed,Enums.GuildMemberRank.HonoraryManager);
        Check(!Fenced(leader) && HasAchievement(failed,10221),"reloaded ordinary sessions retry the rolled-back appointment");
        var poor=PrizeLeader(291);poor.Entity.ConquerPoints=99;EntityTable.SaveEntity(poor);var candidate=PurseMember(poor,Enums.GuildMemberRank.Member);
        AppointGuild(poor,candidate,Enums.GuildMemberRank.HonorarySteward);
        Check(!Fenced(poor) && poor.Entity.ConquerPoints==99 && candidate.AsMember.Rank==Enums.GuildMemberRank.Member,"insufficient personal CPs cannot buy honorary office");
        AppointGuild(leader,steward,Enums.GuildMemberRank.Member);
        Check(steward.AsMember.Rank==Enums.GuildMemberRank.Member && HasAchievement(steward,10225),"demotion preserves earned office achievement");
        for(int n=0;n<5;n++){var p=PurseMember(leader,Enums.GuildMemberRank.Member);AppointGuild(leader,p,Enums.GuildMemberRank.DeputyLeader);}
        var seventh=PurseMember(leader,Enums.GuildMemberRank.Member);AppointGuild(leader,seventh,Enums.GuildMemberRank.DeputyLeader);
        Check(!Fenced(leader) && seventh.AsMember.Rank==Enums.GuildMemberRank.Member && Scalar("SELECT count(*) FROM entities WHERE guildid=290 AND guildrank=990",0)==6,"saved deputy capacity rejects a seventh appointment");
        var malformed=GuildTransferPacket(seventh.AsMember);Writer.WriteUInt16((ushort)Enums.GuildMemberRank.HonorarySteward,8,malformed);malformed[25]=255;GuildCommand.Process(malformed,leader);
        Check(!Fenced(leader) && seventh.AsMember.Rank==Enums.GuildMemberRank.Member,"malformed appointment name length is rejected before mutation");
        var discharge=GuildTransferPacket(deputy.AsMember);Writer.WriteUInt32(GuildCommand.Discharge,4,discharge);GuildCommand.Process(discharge,leader);
        Check(!Fenced(leader) && deputy.AsMember.Rank==Enums.GuildMemberRank.Member && HasAchievement(deputy,10222),"discharge uses the saved appointment transaction and retains earned credit");
        AppointGuild(leader,deputy,Enums.GuildMemberRank.DeputyLeader);
        poor.Entity.ConquerPoints=1000;EntityTable.SaveEntity(poor);Query("UPDATE entities SET guildrank=200 WHERE uid=@uid","uid",(long)poor.Entity.UID);
        AppointGuild(poor,candidate,Enums.GuildMemberRank.HonorarySteward);
        Check(Fenced(poor) && Fenced(candidate) && poor.Entity.ConquerPoints==1000
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",poor.Entity.UID)==1000
            && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",candidate.Entity.UID)==200,"stale runtime leadership cannot authorize a paid office");
        PrepareGuildMaterialization();
        failed.Entity.PKPoints=99;
        Query("UPDATE entities SET guildsilverdonation=11,guildconquerpointdonation=37,guildpkdonation=7,arsenaldonation=0 WHERE uid=@uid","uid",(long)failed.Entity.UID);
        Query("UPDATE entities SET guildsilverdonation=11,guildconquerpointdonation=37,guildpkdonation=7,arsenaldonation=0,guildrouses=19,guildlilies=12,guildorchids=13,guildtulips=14 WHERE uid=@uid","uid",(long)steward.Entity.UID);
        var restored=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",(uint)290);
        Check(restored.Members[failed.Entity.UID].Rank==Enums.GuildMemberRank.HonoraryManager
            && restored.Members[steward.Entity.UID].Rank==Enums.GuildMemberRank.Member && restored.RanksCounts[990]==6,
            "production guild materialization preserves paid offices, demotions and rank counts");
        var loadedDonation=restored.Members[failed.Entity.UID];
        VerifyGuildContributionScores();
        VerifyGuildRankingPages(leader);
        VerifyLiveGuildRankingRefresh();
        VerifyGuildFlowerRankings();
        VerifyGuildManagers();
        VerifyGuildSupervisors();
        Check(loadedDonation.CPSList.Single(x=>x.EntityID==failed.Entity.UID).cpdoantion==740
            && loadedDonation.TotalDonationsList.Single(x=>x.EntityID==failed.Entity.UID).totaldoantion==747
            && !loadedDonation.SilversList.Any(x=>x.EntityID==failed.Entity.UID),
            "loaded ranking converts raw currency amounts to contribution points without inventing fractional silver credit");

        var categories=new Func<Revora.Game.ConquerStructures.Society.Guild.Member,System.Collections.Generic.List<Revora.Game.ConquerStructures.Society.RankingList>>[]{
            m=>m.SilversList,m=>m.TotalDonationsList,m=>m.CPSList,m=>m.PKSList,m=>m.GuidesList,
            m=>m.ArsenalsList,m=>m.RosesList,m=>m.LiliesList,m=>m.OrchidsList,m=>m.TulipsList};
        for(int category=0;category<categories.Length;category++) {
            var select=categories[category];var expected=select(loadedDonation).Select(x=>x.EntityID).ToArray();
            Check(restored.Members.Values.All(m=>select(m).Select(x=>x.EntityID).SequenceEqual(expected)
                && select(m).Select(x=>x.rank).SequenceEqual(Enumerable.Range(0,expected.Length))),
                "every loaded guild member sees identical contiguous ranks in category "+category);
        }
        var equalIds=new[]{failed.Entity.UID,steward.Entity.UID}.OrderBy(x=>x).ToArray();
        Check(loadedDonation.PKSList.Where(x=>equalIds.Contains(x.EntityID)).Select(x=>x.EntityID).SequenceEqual(equalIds),
            "equal saved donation scores use character UID as deterministic tie-breaker");
        var flowers=restored.Members[steward.Entity.UID];
        Check(flowers.RosesList.Single(x=>x.EntityID==steward.Entity.UID).rosedoantion==19
            && flowers.LiliesList.Single(x=>x.EntityID==steward.Entity.UID).liliesdoantion==12
            && flowers.OrchidsList.Single(x=>x.EntityID==steward.Entity.UID).orchidesdoantion==13
            && flowers.TulipsList.Single(x=>x.EntityID==steward.Entity.UID).tuplisedoantion==14,
            "guild flower ranking rows preserve all four saved flower counters");
        var untouched=flowers.PKSList.Count;loadedDonation.PKSList.Clear();
        Check(flowers.PKSList.Count==untouched,"one member's mutable ranking list cannot alter another member's snapshot");
        Call(typeof(GuildTable),null,"BuildDonationRankings",restored);

        Check(loadedDonation.PkDonation==7 && loadedDonation.PKDoantion==7 && loadedDonation.GuideDoantion==0
            && loadedDonation.ConquerPointDonation==37 && loadedDonation.TotalDoantion==747,
            "guild loader preserves saved PK donations and excludes PK penalties and fabricated guide credit");
        Check(loadedDonation.PKSList.Single(x=>x.EntityID==failed.Entity.UID).pkdoantion==7
            && loadedDonation.GuidesList.Single(x=>x.EntityID==failed.Entity.UID).guidedoantion==0,
            "guild ranking projections use actual PK donations without duplicating CP into Guide");
        var previousMember=failed.AsMember;failed.AsMember=loadedDonation;
        try {
            Check(EntityTable.SaveEntity(failed) && Scalar("SELECT guildpkdonation FROM entities WHERE uid=@uid",failed.Entity.UID)==7
                && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid",failed.Entity.UID)==99,
                "guild materialization followed by autosave preserves donation and PK penalty independently");
        } finally {failed.AsMember=previousMember;}

    }

    static void VerifyGuildManagers() {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildManagers",true);Call(type,null,"Initialize");
        var leader=PrizeLeader(49001);leader.Entity.ConquerPoints=5000;EntityTable.SaveEntity(leader);
        var first=PurseMember(leader,Enums.GuildMemberRank.Member);var second=PurseMember(leader,Enums.GuildMemberRank.Member);
        var third=PurseMember(leader,Enums.GuildMemberRank.Member);
        foreach(var p in new[]{first,second,third}){p.Entity.ConquerPoints=1000;EntityTable.SaveEntity(p);}
        Func<bool> reconcile=()=> (bool)Call(type,null,"Reconcile",leader.Guild);
        Check(reconcile() && new[]{first,second,third}.All(p=>p.AsMember.Rank==Enums.GuildMemberRank.Member),"zero contributions cannot mint Manager earning");
        GuildCommand.Process(DonationPacket(100,true),first);
        type.GetField("cursor",Hidden).SetValue(null,49000u);Call(type,null,"Tick");
        Check(first.AsMember.Rank==Enums.GuildMemberRank.Manager && first.Entity.GuildRank==890
            && HasAchievement(first,10223) && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",first.Entity.UID)==890,
            "ordinary committed donation earns Manager and its durable achievement together");
        Parallel.For(0,8,n=>reconcile());
        Check(Scalar("SELECT count(*) FROM revora_guild_managers WHERE guild_id=49001",0)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10223",first.Entity.UID)==1,
            "concurrent Manager reconciliation preserves one owner and one achievement");
        GuildCommand.Process(DonationPacket(200,true),second);
        Check(reconcile() && second.AsMember.Rank==Enums.GuildMemberRank.Manager && first.AsMember.Rank==Enums.GuildMemberRank.Member
            && HasAchievement(first,10223) && HasAchievement(second,10223),"higher saved contribution displaces automatic Manager without erasing earned history");
        GuildCommand.Process(DonationPacket(200,true),third);
        Check(reconcile() && second.AsMember.Rank==Enums.GuildMemberRank.Manager && third.AsMember.Rank==Enums.GuildMemberRank.Member,
            "equal Manager scores use ascending UID instead of request order");
        leader.Guild.Level=9;
        Check(reconcile() && Scalar("SELECT count(*) FROM revora_guild_managers WHERE guild_id=49001",0)==1,
            "forged runtime guild level cannot create slots beyond saved qualifying arsenals");
        Call(typeof(GuildTable),null,"RebuildLevel",leader.Guild);
        Query("UPDATE entities SET guildconquerpointdonation=777 WHERE uid=@uid","uid",(long)third.Entity.UID);
        Check(!reconcile() && second.AsMember.Rank==Enums.GuildMemberRank.Manager,"stale contribution cache defers automatic changes until recovery");
        Query("UPDATE entities SET guildconquerpointdonation=200 WHERE uid=@uid","uid",(long)third.Entity.UID);
        Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)third.Entity.UID);
        Check(!reconcile() && second.AsMember.Rank==Enums.GuildMemberRank.Manager,"stale saved guild membership cannot authorize a new reconciliation");
        Query("UPDATE entities SET guildid=49001 WHERE uid=@uid","uid",(long)third.Entity.UID);
        Kernel.GamePool[third.Entity.UID]=first;
        try{Check(!reconcile(),"replaced member session cannot authorize Manager mutation");}
        finally{Kernel.GamePool[third.Entity.UID]=third;}
        var offline=PurseMember(leader,Enums.GuildMemberRank.Member);offline.Entity.ConquerPoints=1000;EntityTable.SaveEntity(offline);
        GuildCommand.Process(DonationPacket(500,true),offline);GameState removed;Kernel.GamePool.TryRemove(offline.Entity.UID,out removed);
        try {
            Query("CREATE FUNCTION refuse_manager_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER refuse_manager_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10223) EXECUTE FUNCTION refuse_manager_evidence()");
            try{Check(!reconcile(),"failed Manager evidence rejects the whole reconciliation");}
            finally{Query("DROP TRIGGER refuse_manager_evidence ON revora_achievement_evidence;DROP FUNCTION refuse_manager_evidence()");}
            Check(second.AsMember.Rank==Enums.GuildMemberRank.Manager && offline.AsMember.Rank==Enums.GuildMemberRank.Member
                && Scalar("SELECT uid FROM revora_guild_managers WHERE guild_id=49001",0)==second.Entity.UID
                && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",offline.Entity.UID)==200,
                "failed evidence rolls back rival demotion, offline promotion and ownership receipt");
            Check(reconcile() && offline.AsMember.Rank==Enums.GuildMemberRank.Manager
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10223",offline.Entity.UID)==1,
                "offline ordinary member earns saved Manager and achievement on retry");
        } finally {Kernel.GamePool[offline.Entity.UID]=removed;}
        offline=ReloadBall(offline);
        new TitleStorage().Login(offline);
        Check(offline.Entity.GuildRank==890 && HasAchievement(offline,10223),"ordinary login restores offline Manager rank and earned evidence");
        AppointGuild(leader,offline,Enums.GuildMemberRank.HonoraryManager);
        Check(reconcile() && offline.AsMember.Rank==Enums.GuildMemberRank.HonoraryManager && second.AsMember.Rank==Enums.GuildMemberRank.Manager
            && Scalar("SELECT count(*) FROM revora_guild_managers WHERE uid=@uid",offline.Entity.UID)==0,
            "paid honorary appointment survives reconciliation and retires obsolete automatic ownership");
        var loaded=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",leader.Guild.ID);
        Check(loaded.Members[second.Entity.UID].Rank==Enums.GuildMemberRank.Manager
            && loaded.Members[offline.Entity.UID].Rank==Enums.GuildMemberRank.HonoraryManager,"full guild reload retains automatic and paid office identities");
        lock(third.ItemSyncRoot){var busy=Task.Run(()=>reconcile());Check(busy.Wait(TimeSpan.FromSeconds(5)) && !busy.Result,"busy member ownership defers Manager work without deadlock");}
        Query("UPDATE entities SET guildrank=890 WHERE uid=@uid","uid",(long)first.Entity.UID);first.AsMember.Rank=Enums.GuildMemberRank.Manager;first.Entity.GuildRank=890;
        Check(reconcile() && first.AsMember.Rank==Enums.GuildMemberRank.Manager && second.AsMember.Rank==Enums.GuildMemberRank.Member,
            "untracked legacy Manager reserves its slot and cannot be automatically demoted or backfilled");
        Check(Scalar("SELECT count(*) FROM revora_guild_managers WHERE guild_id=49001",0)==0,"legacy Manager ownership is never fabricated");
        for(byte level=1;level<=9;level++)Check((int)Call(type,null,"Capacity",level)==new[]{1,1,2,2,4,4,6,6,8}[level-1],"Manager capacity for level "+level);
    }

    static void VerifyGuildSupervisors() {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildManagers",true);
        var leader=PrizeLeader(49002);var guild=leader.Guild;
        leader.Entity.ConquerPoints=2000;EntityTable.SaveEntity(leader);
        Func<bool> reconcile=()=> (bool)Call(type,null,"Reconcile",guild);
        // Controlled saved level eligibility; actual donations and flowers use player handlers.
        for(int slot=1;slot<=3;slot++) {
            Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(49002,@slot,1,0,0)","slot",slot);
            Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,'level fixture',@item,@slot,49002,5000000)",
                "uid",(long)leader.Entity.UID,"item",(long)(880000+slot),"slot",slot);
        }
        var a=PurseMember(leader,Enums.GuildMemberRank.Member);var b=PurseMember(leader,Enums.GuildMemberRank.Member);
        var c=PurseMember(leader,Enums.GuildMemberRank.Member);var d=PurseMember(leader,Enums.GuildMemberRank.Member);
        var players=new[]{a,b,c,d};var amounts=new uint[]{500,400,100,100};
        for(int i=0;i<players.Length;i++) {players[i].Entity.ConquerPoints=2000;EntityTable.SaveEntity(players[i]);GuildCommand.Process(DonationPacket(amounts[i],true),players[i]);}
        Check(players.All(p=>p.Account.State==AccountTable.AccountState.Player && !p.Fake),"Supervisor donors are ordinary players");
        Check(reconcile() && a.AsMember.Rank==Enums.GuildMemberRank.Manager && b.AsMember.Rank==Enums.GuildMemberRank.Manager
            && c.AsMember.Rank==Enums.GuildMemberRank.CPSupervisor && d.AsMember.Rank==Enums.GuildMemberRank.Member
            && HasAchievement(c,10224),"Managers take priority; highest remaining positive CP donor earns Supervisor with UID ties");
        long receiptCount=Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10224",0);
        Parallel.For(0,8,n=>reconcile());
        Check(Scalar("SELECT count(*) FROM revora_guild_supervisors WHERE guild_id=49002",0)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10224",0)==receiptCount,"repeated simultaneous reconciliation does not duplicate Supervisor earning");
        GuildCommand.Process(DonationPacket(700,true),c);
        Check(reconcile() && c.AsMember.Rank==Enums.GuildMemberRank.Manager && b.AsMember.Rank==Enums.GuildMemberRank.CPSupervisor
            && HasAchievement(c,10224) && HasAchievement(c,10223),"automatic Supervisor can become Manager and displaced Manager can become Supervisor atomically");
        var flower=PurseMember(leader,Enums.GuildMemberRank.Member);leader.Entity.Body=1003;flower.Entity.Body=2001;
        leader.Entity.MyFlowers=new Revora.Game.Features.Flowers(leader.Entity.UID,leader.Entity.Name);
        flower.Entity.MyFlowers=new Revora.Game.Features.Flowers(flower.Entity.UID,flower.Entity.Name);
        EntityTable.SaveEntity(leader);EntityTable.SaveEntity(flower);
        foreach(uint itemType in new uint[]{751001,752001,753003,754001}) {
            var item=Pack(leader,itemType,1);Revora.Game.Features.Flowers.SendFlower(leader,FlowerRequest(leader,flower,item.UID));
        }
        Query("CREATE FUNCTION refuse_supervisor_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER refuse_supervisor_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10224) EXECUTE FUNCTION refuse_supervisor_evidence()");
        try{Check(!reconcile(),"failed Supervisor achievement write rejects its rank transaction");}
        finally{Query("DROP TRIGGER refuse_supervisor_evidence ON revora_achievement_evidence;DROP FUNCTION refuse_supervisor_evidence()");}
        Check(flower.AsMember.Rank==Enums.GuildMemberRank.Member && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",flower.Entity.UID)==200
            && Scalar("SELECT count(*) FROM revora_guild_supervisors WHERE uid=@uid",flower.Entity.UID)==0,"failed Supervisor evidence leaves saved and online office unchanged");
        GameState removed;Kernel.GamePool.TryRemove(flower.Entity.UID,out removed);
        try{Check(reconcile() && flower.AsMember.Rank==Enums.GuildMemberRank.TSupervisor,"offline flower recipient earns highest-priority supported category once");}
        finally{Kernel.GamePool[flower.Entity.UID]=removed;}
        flower=ReloadBall(flower);new TitleStorage().Login(flower);
        Check(flower.Entity.GuildRank==859 && HasAchievement(flower,10224),"ordinary login restores offline Supervisor office and achievement");
        var loaded=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",49002u);
        Check(loaded.Members[flower.Entity.UID].Rank==Enums.GuildMemberRank.TSupervisor && loaded.Members[c.Entity.UID].Rank==Enums.GuildMemberRank.Manager,"full guild reload retains automatic offices together");
        AppointGuild(leader,flower,Enums.GuildMemberRank.HonorarySuperv);
        Check(reconcile() && flower.AsMember.Rank==Enums.GuildMemberRank.HonorarySuperv
            && Scalar("SELECT count(*) FROM revora_guild_supervisors WHERE uid=@uid",flower.Entity.UID)==0,"manual honorary replacement survives and retires Supervisor ownership");
        // An untracked category office reserves its seat but receives no fabricated evidence.
        Query("UPDATE entities SET guildrank=857 WHERE uid=@uid","uid",(long)d.Entity.UID);d.AsMember.Rank=Enums.GuildMemberRank.CPSupervisor;d.Entity.GuildRank=857;
        Check(reconcile() && d.AsMember.Rank==Enums.GuildMemberRank.CPSupervisor && b.AsMember.Rank==Enums.GuildMemberRank.Member
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10224",d.Entity.UID)==0,"legacy Supervisor reserves capacity without inferred ownership or achievement");
        Query("UPDATE entities SET guildrank=200 WHERE uid=@uid","uid",(long)d.Entity.UID);d.AsMember.Rank=Enums.GuildMemberRank.Member;d.Entity.GuildRank=200;
        Check(reconcile() && b.AsMember.Rank==Enums.GuildMemberRank.CPSupervisor,"released legacy seat permits legitimate automatic earning");
        Query("UPDATE pt_arsenal SET arsenal_unlocked=0 WHERE syn_id=49002 AND arsenal_id=3");
        Check(reconcile() && b.AsMember.Rank==Enums.GuildMemberRank.Member && HasAchievement(b,10224)
            && Scalar("SELECT count(*) FROM revora_guild_supervisors WHERE guild_id=49002",0)==0,"level three withdraws automatic Supervisor seats while preserving historical achievement");
        for(byte level=0;level<=10;level++)Check((int)Call(type,null,"SupervisorCapacity",level)==(level>=8 && level<=9?2:level>=4 && level<=7?1:0),"Supervisor capacity boundary "+level);
        Check(Scalar("SELECT count(*) FROM revora_guild_supervisors WHERE rank=854",0)==0,"Guide Supervisor is not fabricated");
    }

    static void VerifyGuildFlowerRankings() {
        Call(FlowerStore,null,"Initialize");
        var leader=PrizeLeader(298);var sender=PurseMember(leader,Enums.GuildMemberRank.Member);
        var receiver=PurseMember(leader,Enums.GuildMemberRank.Member);
        sender.Entity.Body=1003;receiver.Entity.Body=2001;
        sender.Entity.MyFlowers=new Revora.Game.Features.Flowers(sender.Entity.UID,sender.Entity.Name);
        receiver.Entity.MyFlowers=new Revora.Game.Features.Flowers(receiver.Entity.UID,receiver.Entity.Name);
        receiver.AsMember.Rouses=77;receiver.AsMember.Lilies=78;receiver.AsMember.Orchids=79;receiver.AsMember.Tulips=80;
        EntityTable.SaveEntity(sender);EntityTable.SaveEntity(receiver);
        var type=typeof(Revora.Game.ConquerStructures.Society.GuildDoantion);
        Func<byte,byte[]> page=category=>{
            var request=new byte[20];Writer.WriteUInt16(12,0,request);Writer.WriteUInt16(2101,2,request);request[4]=category;
            Writer.WriteUInt64(PacketHandler.ClientSeal,12,request);return (byte[])Call(type,null,"BuildResponse",request,leader);
        };
        foreach(var spec in new[]{new[]{751001,5,44},new[]{752001,7,48},new[]{753003,6,52},new[]{754001,8,56}}) {
            var before=page((byte)spec[1]);
            Check(before!=null && System.BitConverter.ToUInt32(before,spec[2])==0,"legacy guild counters cannot invent lifetime flowers: "+spec[0]);
            var item=Pack(sender,(uint)spec[0],1);var frame=FlowerRequest(sender,receiver,item.UID);
            Revora.Game.Features.Flowers.SendFlower(sender,frame);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            var after=page((byte)spec[1]);
            Check(!Fenced(sender) && !Fenced(receiver) && after!=null && System.BitConverter.ToUInt32(after,12)==receiver.Entity.UID
                && System.BitConverter.ToUInt32(after,spec[2])==spec[0]%1000,
                "ordinary gift immediately ranks its saved category once: "+spec[0]);
        }
        Check(Deliveries(sender)==4 && receiver.AsMember.Rouses==77 && EntityTable.SaveEntity(receiver)
            && Scalar("SELECT guildrouses FROM entities WHERE uid=@uid",receiver.Entity.UID)==77,
            "lifetime ranking preserves separate legacy counters through autosave");
        var saved=page(5);
        Query("CREATE FUNCTION refuse_guild_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER refuse_guild_flower BEFORE INSERT ON revora_flower_deliveries FOR EACH ROW EXECUTE FUNCTION refuse_guild_flower()");
        try{Revora.Game.Features.Flowers.SendFlower(sender,FlowerRequest(sender,receiver,0));}
        finally{Query("DROP TRIGGER refuse_guild_flower ON revora_flower_deliveries;DROP FUNCTION refuse_guild_flower()");}
        Check(Fenced(sender) && Fenced(receiver) && Deliveries(sender)==4 && page(5).SequenceEqual(saved),
            "failed flower receipt rolls back the gift and cannot publish an uncommitted guild score");
        Query("UPDATE revora_flower_balances SET totals[1]=1 WHERE uid=@uid","uid",(long)sender.Entity.UID);
        Check(System.BitConverter.ToUInt32(page(5),12)==Math.Min(sender.Entity.UID,receiver.Entity.UID),
            "equal lifetime flower scores use deterministic UID order");
        Query("UPDATE revora_flower_balances SET totals[1]=0 WHERE uid=@uid","uid",(long)sender.Entity.UID);
        GameState removed;Kernel.GamePool.TryRemove(receiver.Entity.UID,out removed);
        try {
            receiver.Entity.MyFlowers.RedRoses=uint.MaxValue;receiver.AsMember.Rouses=uint.MaxValue;
            Check(page(5).SequenceEqual(saved),"offline member and corrupted in-memory totals cannot change committed ranking");
            var originalGuild=leader.Guild;var originalMember=leader.AsMember;
            var loaded=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",298u);
            try {
                Kernel.Guilds[298]=loaded;leader.Guild=loaded;leader.AsMember=loaded.Members[leader.Entity.UID];
                Check(page(5).SequenceEqual(saved),"full guild materialization preserves saved lifetime flower ranking");
            } finally {Kernel.Guilds[298]=originalGuild;leader.Guild=originalGuild;leader.AsMember=originalMember;}
            Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)receiver.Entity.UID);
            var departed=page(5);
            Check(System.BitConverter.ToUInt16(departed,6)==2 && System.BitConverter.ToUInt32(departed,44)==0,
                "saved departed member is excluded even when guild membership cache is stale");
            Query("UPDATE entities SET guildid=298 WHERE uid=@uid","uid",(long)receiver.Entity.UID);
            Check(page(5).SequenceEqual(saved),"rejoined member retains lifetime flowers without a second award");
            Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)leader.Entity.UID);
            Check(System.BitConverter.ToUInt16(page(5),6)==0,"stale saved viewer membership cannot expose a guild flower roster");
            Query("UPDATE entities SET guildid=298 WHERE uid=@uid","uid",(long)leader.Entity.UID);
        } finally {Kernel.GamePool[receiver.Entity.UID]=removed;}
    }

    static void VerifyLiveGuildRankingRefresh() {
        var leader=PrizeLeader(297);var donor=PurseMember(leader,Enums.GuildMemberRank.Member);
        var peer=PurseMember(leader,Enums.GuildMemberRank.Member);donor.Entity.Money=20000;donor.Entity.ConquerPoints=1000;EntityTable.SaveEntity(donor);
        GuildCommand.Process(DonationPacket(10000),donor);
        Check(donor.AsMember.SilversList.Single(row=>row.EntityID==donor.Entity.UID).silverdoantion==1
            && peer.AsMember.TotalDonationsList.Single(row=>row.EntityID==donor.Entity.UID).totaldoantion==1,
            "first whole silver contribution point appears in donor and peer rankings immediately");
        var before=peer.AsMember.TotalDonationsList;
        Query("CREATE FUNCTION refuse_live_rank() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_live_rank BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION refuse_live_rank()");
        try{GuildCommand.Process(DonationPacket(100,true),donor);}finally{Query("DROP TRIGGER refuse_live_rank ON guilds;DROP FUNCTION refuse_live_rank()");}
        Check(Fenced(donor) && ReferenceEquals(before,peer.AsMember.TotalDonationsList)
            && Scalar("SELECT guildconquerpointdonation FROM entities WHERE uid=@uid",donor.Entity.UID)==0,
            "failed guild fund write cannot publish an uncommitted contribution rank");
        donor=ReloadGold(donor);GuildCommand.Process(DonationPacket(100,true),donor);
        Check(!Fenced(donor) && peer.AsMember.TotalDonationsList.Single(row=>row.EntityID==donor.Entity.UID).totaldoantion==2001,
            "reloaded donor retries once and publishes the committed mixed contribution score");
    }

    static void VerifyGuildRankingPages(GameState viewer) {
        var type=typeof(Revora.Game.ConquerStructures.Society.GuildDoantion);
        foreach(int size in new[]{0,1,9,10,11,19,20,21,99,100,101}) {
            var rows=Enumerable.Range(0,size).Select(i=>new Revora.Game.ConquerStructures.Society.RankingList{
                EntityID=(uint)(200000+i),Name="Rider"+i,cpdoantion=ulong.MaxValue,postion=Enums.GuildMemberRank.Member}).ToArray();
            for(byte category=0;category<10;category++)for(byte page=0;page<(category==9?10:2);page++) {
                var bytes=(byte[])Call(type,null,"SerializePage",rows,category,page);
                int total=Math.Min(size,category==9?100:20),count=Math.Max(0,Math.Min(10,total-page*10));
                bool ok=bytes.Length==20+count*68 && System.BitConverter.ToUInt16(bytes,0)==bytes.Length-8
                    && bytes[4]==category && bytes[5]==page && System.BitConverter.ToUInt16(bytes,6)==count
                    && System.BitConverter.ToUInt32(bytes,8)==total && System.Text.Encoding.ASCII.GetString(bytes,bytes.Length-8,8)=="TQServer";
                for(int row=0;row<count;row++)ok=ok && System.BitConverter.ToUInt32(bytes,12+row*68)==200000+page*10+row
                    && System.BitConverter.ToUInt32(bytes,20+row*68)==page*10+row
                    && System.BitConverter.ToUInt32(bytes,28+row*68)==uint.MaxValue;
                Check(ok,"guild ranking bounded page: size="+size+" category="+category+" page="+page);
            }
        }
        var request=new byte[20];Writer.WriteUInt16(12,0,request);Writer.WriteUInt16(2101,2,request);request[4]=1;
        Writer.WriteUInt64(PacketHandler.ClientSeal,12,request);
        Check(Call(type,null,"BuildResponse",request,viewer)!=null,"ordinary guild member can request an owned ranking response");
        for(int length=0;length<16;length++)Check(Call(type,null,"BuildResponse",new byte[length],viewer)==null,"short ranking request rejected: "+length);
        foreach(string bad in new[]{"seal","length","family","category","page"}) {
            var bytes=(byte[])request.Clone();
            if(bad=="seal")bytes[19]^=1;if(bad=="length")bytes[0]++;if(bad=="family")bytes[2]++;
            if(bad=="category")bytes[4]=10;if(bad=="page")bytes[5]=2;
            Check(Call(type,null,"BuildResponse",bytes,viewer)==null,"malformed ranking request rejected: "+bad);
        }
        viewer.Fake=true;Check(Call(type,null,"BuildResponse",request,viewer)==null,"fake character cannot request guild ranking");viewer.Fake=false;
        var realMember=viewer.AsMember;viewer.AsMember=new Revora.Game.ConquerStructures.Society.Guild.Member(viewer.Guild.ID){ID=viewer.Entity.UID};
        try{Check(Call(type,null,"BuildResponse",request,viewer)==null,"forged member object cannot read an owned guild response");}
        finally{viewer.AsMember=realMember;}
        ushort actualGuild=viewer.Entity.GuildID;viewer.Entity.CharBuffer._GuildID=0;
        try{Check(Call(type,null,"BuildResponse",request,viewer)==null,"stale character membership cannot request guild ranking");}
        finally{viewer.Entity.CharBuffer._GuildID=actualGuild;}

        Kernel.GamePool.Remove(viewer.Entity.UID);try{Check(Call(type,null,"BuildResponse",request,viewer)==null,"stale session cannot request guild ranking");}
        finally{Kernel.GamePool[viewer.Entity.UID]=viewer;}
        var gate=Call(Leadership,null,"Gate",viewer.Guild);
        lock(gate) { var task=Task.Run(()=>Call(type,null,"BuildResponse",request,viewer));
            Check(task.Wait(TimeSpan.FromSeconds(5)) && task.Result==null,"busy guild ownership defers ranking without blocking its donor"); }
    }

    static void VerifyGuildContributionScores(){
        var member=new Revora.Game.ConquerStructures.Society.Guild.Member(1);
        ulong[] paid={0,9999,10000,2999999999,3000000000,3000099999,3000100000,ulong.MaxValue};
        ulong[] expected={0,0,1,299999,300000,300000,300001,300000+(ulong.MaxValue-3000000000UL)/100000};
        for(int i=0;i<paid.Length;i++) {
            member.SilverDoantion=paid[i];
            Check(member.SilverDoantionMath==expected[i] && member.SilverDoantion==paid[i],"silver contribution boundary preserves raw amount: "+paid[i]);
        }
        member.SilverDoantion=10000;member.ConquerPointDonation=37;member.Arsenal_Donation=11;member.PKDoantion=7;member.GuideDoantion=13;
        member.Rouses=member.Lilies=member.Orchids=member.Tulips=uint.MaxValue;
        Check(member.ConquerPointDonationMath==740 && member.TotalDoantion==772 && member.TotalDonation==772,
            "total guild score combines five contribution types and excludes flowers");
        member.ConquerPointDonation=ulong.MaxValue;
        Check(member.ConquerPointDonationMath==ulong.MaxValue && member.TotalDoantion==ulong.MaxValue && member.TotalDonation==uint.MaxValue,
            "extreme guild donation totals saturate display widths rather than wrapping");
    }

    static void VerifyGuildBombs(){
        var leader=PrizeLeader(295);var field=Kernel.Maps[1038];
        Func<GameState> bomber=()=>{
            var p=FullRunePlayer(Kernel.Maps[1002],15);p.Guild=leader.Guild;p.Entity.GuildID=(ushort)leader.Guild.ID;
            p.AsMember=new Revora.Game.ConquerStructures.Society.Guild.Member(leader.Guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Rank=Enums.GuildMemberRank.Member};
            leader.Guild.Members[p.Entity.UID]=p.AsMember;p.Entity.GuildRank=(ushort)p.AsMember.Rank;p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);
            p.BindWorldMap(1038);p.Entity.X=(ushort)(GuildWar.LeftGate.X+1);p.Entity.Y=GuildWar.LeftGate.Y;return p;
        };
        guildNow=new DateTime(2034,1,22,14,59,0,DateTimeKind.Local).ToUniversalTime();
        var early=bomber();var earlyBomb=Pack(early,721261,1);uint initial=GuildWar.LeftGate.Hitpoints;ItemHandler.UseItem(earlyBomb,early);
        Check(!early.Entity.Dead && early.Inventory.ContainsUID(earlyBomb.UID) && GuildWar.LeftGate.Hitpoints==initial,"siege bombs cannot be spent outside an active battle");
        guildNow=guildNow.AddMinutes(1);GuildWar.Start();Check(GuildWar.IsWar,"siege bomb fixture starts the scheduled real battlefield");
        var gate=GuildWar.LeftGate;uint full=gate.MaxHitpoints;
        var remote=bomber();remote.BindWorldMap(1002);var remoteBomb=Pack(remote,721261,1);ItemHandler.UseItem(remoteBomb,remote);
        Check(!remote.Entity.Dead && remote.Inventory.ContainsUID(remoteBomb.UID) && gate.Hitpoints==full,"matching coordinates on another map cannot bomb the guild gate");
        var impostor=bomber();impostor.Fake=true;var fakeBomb=Pack(impostor,721261,1);ItemHandler.UseItem(fakeBomb,impostor);impostor.Fake=false;
        var owner=bomber();ItemHandler.UseItem(fakeBomb,owner);
        Check(gate.Hitpoints==full && impostor.Inventory.ContainsUID(fakeBomb.UID) && !owner.Entity.Dead,"fake actors and foreign bomb objects cannot mutate a gate");
        var failed=bomber();var failedBomb=Pack(failed,721261,1);
        Query("CREATE FUNCTION refuse_bomb_credit() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_bomb_credit BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10231) EXECUTE FUNCTION refuse_bomb_credit()");
        try{ItemHandler.UseItem(failedBomb,failed);}finally{Query("DROP TRIGGER refuse_bomb_credit ON revora_achievement_evidence;DROP FUNCTION refuse_bomb_credit()");}
        Check(Fenced(failed) && !failed.Entity.Dead && gate.Hitpoints==full && failed.Inventory.ContainsUID(failedBomb.UID)
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",failedBomb.UID)==1,"failed bomb evidence rolls back consumption without gate damage or death");
        uint quarter=(uint)(((ulong)full+3)/4);
        for(int i=0;i<4;i++){
            var p=bomber();var bomb=Pack(p,721261,(ushort)(i==0?2:1));uint before=gate.Hitpoints;ItemHandler.UseItem(bomb,p);
            Check(!Fenced(p) && p.Entity.Dead && gate.Hitpoints==before-Math.Min(before,quarter) && HasAchievement(p,10231),"accepted bomb damages a quarter of maximum HP and kills only its carrier: "+i);
            Check(i==0?bomb.StackSize==1 && Scalar("SELECT stacksize FROM items WHERE uid=@uid",bomb.UID)==1:!p.Inventory.ContainsUID(bomb.UID),"accepted bomb consumes exactly one unit: "+i);
            uint after=gate.Hitpoints;Parallel.For(0,8,n=>ItemHandler.UseItem(bomb,p));Check(gate.Hitpoints==after,"dead carrier/replayed item use cannot duplicate a detonation: "+i);
            if(i==0){p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(HasAchievement(p,10231),"siege achievement survives ordinary login");}
        }
        Check(gate.Hitpoints==0 && gate.Mesh/10==25,"four accepted bombs open the full left gate");
        var open=bomber();var unused=Pack(open,721261,1);ItemHandler.UseItem(unused,open);
        Check(!open.Entity.Dead && open.Inventory.ContainsUID(unused.UID),"an already open gate cannot consume a bomb");
        var right=bomber();right.Entity.X=(ushort)(GuildWar.RightGate.X+1);right.Entity.Y=GuildWar.RightGate.Y;
        GuildWar.RightGate.Hitpoints=1;var finishing=Pack(right,721261,1);ItemHandler.UseItem(finishing,right);
        Check(!Fenced(right) && right.Entity.Dead && GuildWar.RightGate.Hitpoints==0 && GuildWar.RightGate.Mesh/10==28,"a bomb finishes a nearly destroyed right gate instead of doing zero damage");
        guildNow=guildNow.AddHours(1);GuildWar.End();
    }

}
