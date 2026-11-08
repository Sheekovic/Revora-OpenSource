using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class CheckKingdomCrown {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    static Type fixtures;static int checks;
    static object Invoke(Type type,string method,params object[] args){try{return type.GetMethods(Hidden).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Helper(string name,params object[] args){return Invoke(fixtures,name,args);}
    static object Sql(string sql,params object[] values){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<values.Length;i+=2)q.Parameters.AddWithValue((string)values[i],values[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static long Count(string sql,params object[] values){return Convert.ToInt64(Sql(sql,values));}
    static byte[] Packet(string name){var text=System.Text.Encoding.Default.GetBytes(name);var bytes=new byte[28+text.Length];Revora.Network.Writer.WriteUInt16((ushort)(bytes.Length-8),0,bytes);Revora.Network.Writer.WriteUInt16((ushort)PacketID.MsgLeagueOpt,2,bytes);Revora.Network.Writer.WriteUInt16(7,4,bytes);bytes[18]=1;bytes[19]=(byte)text.Length;Array.Copy(text,0,bytes,20,text.Length);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,bytes.Length-8,bytes);return bytes;}
    static void Create(GameState p,byte[] bytes){Revora.Network.GamePackets.Union.Union.Prosses(bytes,p);}
    static void Action(GameState p,ushort action,uint target,uint parameter=0){var bytes=Packet("");Revora.Network.Writer.WriteUInt16(action,4,bytes);Revora.Network.Writer.WriteUInt32(target,10,bytes);Revora.Network.Writer.WriteUInt32(parameter,14,bytes);Create(p,bytes);}
    static void NamedAction(GameState p,ushort action,string name,uint rank){var bytes=Packet(name);Revora.Network.Writer.WriteUInt16(action,4,bytes);Revora.Network.Writer.WriteUInt32(rank,14,bytes);Create(p,bytes);}
    static DateTime crownNow;
    static readonly Type Field=typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCompetitionField",true);
    static readonly Type Crown=typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCrown",true);
    static System.Collections.Generic.Dictionary<int,bool> Titles(GameState p){return (System.Collections.Generic.Dictionary<int,bool>)Helper("OwnedTitles",p);}
    static void Talk(GameState p,uint npc,byte option){p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static void Earn(GameState p){p.Entity.Teleport(1002,316,325);Talk(p,903980,1);p.Entity.X=502;p.Entity.Y=300;Talk(p,903982,1);crownNow=crownNow.AddSeconds(5);Invoke(Field,"Tick");p.Entity.X=302;p.Entity.Y=153;Talk(p,903981,1);}
    static void EquipCrown(GameState p){new TitleStorage().Login(p);((TitleStorage)Helper("TitleRequest",11000,TitleStorage.Action.Equip)).Handle(p);((TitleStorage)Helper("TitleRequest",40011000,TitleStorage.Action.Equip)).Handle(p);}
    static void NullableUnionFields(GameState p){Sql("UPDATE entities SET unionid=NULLIF(unionid,0),offical=NULLIF(offical,0),harem=NULLIF(harem,0),guards=NULLIF(guards,0) WHERE uid=@uid","uid",(long)p.Entity.UID);}
    static void VerifyOffices(GameState leader,GameState p,GameState foreign) {
        NullableUnionFields(leader);NullableUnionFields(p);
        var union=leader.Union;var q=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);Action(q,8,0,union.ID);
        NamedAction(p,28,q.Entity.Name,2000);NamedAction(leader,28,foreign.Entity.Name,2000);NamedAction(leader,28,leader.Entity.Name,2000);
        Check(p.Offical==0 && q.Offical==0 && foreign.Offical==1 && leader.Offical==1,"nonleaders, foreign members and the ruler cannot receive unauthorized office changes");
        foreach(uint rank in new[]{2001u,2071u,67536u})NamedAction(leader,28,p.Entity.Name,rank);
        foreach(uint rank in new[]{4001u,4010u,4020u,4028u})NamedAction(leader,41,p.Entity.Name,rank);
        var invalid=Packet(p.Entity.Name);Revora.Network.Writer.WriteUInt16(28,4,invalid);invalid[19]=255;Create(leader,invalid);
        Check(p.Offical==0 && p.Harem==0 && leader.Action==2,"undefined and overflowing rank IDs plus malformed names are refused safely");
        Sql("UPDATE entities SET offical=0 WHERE uid=@uid","uid",(long)leader.Entity.UID);NamedAction(leader,28,p.Entity.Name,2000);
        Check(p.Offical==0,"saved ruler office defeats stale cached appointment authority");Sql("UPDATE entities SET offical=1 WHERE uid=@uid","uid",(long)leader.Entity.UID);
        foreach(ushort action in new ushort[]{28,24,41})foreach(string table in new[]{"unions","entities"}) {
            var before=Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101"));
            Sql("CREATE FUNCTION reject_office() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_office BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_office()");
            try{NamedAction(leader,action,p.Entity.Name,action==28?2000u:action==41?4000u:0u);}finally{Sql("DROP TRIGGER reject_office ON "+table+";DROP FUNCTION reject_office()");}
            Check(leader.Action==0 && p.Action==0 && p.Offical==0 && p.Harem==0 && p.Guards==0
                && !union.Coreofficals.Contains(p.Entity.UID) && !union.Harem.Contains(p.Entity.UID) && !union.Guards.Contains(p.Entity.UID)
                && before==Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101")),"suppressed "+table+" write rolls back office appointment action "+action);
            leader=(GameState)Helper("ReloadGold",leader);p=(GameState)Helper("ReloadGold",p);
        }
        System.Threading.Tasks.Parallel.For(0,8,n=>NamedAction(leader,28,p.Entity.Name,2000));NamedAction(leader,24,p.Entity.Name,0);NamedAction(leader,41,p.Entity.Name,4000);
        Check(p.Offical==2 && p.Guards==512 && p.Harem==1024 && union.Coreofficals.Count(id=>id==p.Entity.UID)==1
            && p.Entity.CharBuffer.Offical_Harem_Guards==1538,"concurrent appointment grants each role once and updates the combined visible rank immediately");
        NamedAction(leader,47,q.Entity.Name,2000);NamedAction(leader,49,q.Entity.Name,4000);
        Check(q.Offical==0 && q.Harem==0 && p.Offical==2 && p.Harem==1024,"occupied fixed seats cannot displace their current holder");
        NamedAction(leader,49,q.Entity.Name,401);NamedAction(leader,49,q.Entity.Name,402);NamedAction(leader,48,q.Entity.Name,0);
        Check(q.Harem==2048 && union.Harem[1]==q.Entity.UID && union.Harem.Count(id=>id==q.Entity.UID)==1 && q.Guards==512,"automatic harem group and guard aliases grant one seat without duplicate membership");
        Action(p,26,q.Entity.UID);Action(p,30,q.Entity.UID);Action(leader,30,0);Action(leader,26,0);
        Check(p.Offical==0 && p.Guards==0 && p.Harem==1024 && q.Guards==512 && leader.Offical==1,"resignation clears only the requesting member's selected office and preserves the ruler");
        NamedAction(leader,28,p.Entity.Name,2000);NamedAction(leader,24,p.Entity.Name,0);Check(EntityTable.SaveEntity(p),"office holder saves before going offline");Kernel.GamePool.Remove(p.Entity.UID);p.Action=0;
        Sql("CREATE FUNCTION reject_office() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_office BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_office()");
        try{Action(leader,25,p.Entity.UID);}finally{Sql("DROP TRIGGER reject_office ON entities;DROP FUNCTION reject_office()");}
        Check(leader.Action==0 && union.Guards.Contains(p.Entity.UID) && Count("SELECT guards FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==512,"offline removal failure rolls back both saved rank and roster");leader=(GameState)Helper("ReloadGold",leader);
        foreach(ushort action in new ushort[]{29,25,44})Action(leader,action,p.Entity.UID);p=(GameState)Helper("ReloadGold",p);
        Check(p.Union==union && p.Offical==0 && p.Harem==0 && p.Guards==0 && q.Harem==2048 && q.Guards==512,"offline removal of all office families survives ordinary login without affecting another member");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionOffices",true);var hook=service.GetField("BeforeProjection",Hidden);
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected office projection failure");}));
        try{NamedAction(leader,24,p.Entity.Name,0);}finally{hook.SetValue(null,null);}
        Check(leader.Action==0 && p.Action==0 && Count("SELECT guards FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==512,"postcommit appointment failure fences both sessions without undoing the earned office");
        leader=(GameState)Helper("ReloadGold",leader);p=(GameState)Helper("ReloadGold",p);Check(p.Guards==512 && union.Guards.Contains(p.Entity.UID),"normal reconnect recovers the committed appointment");
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected resignation projection failure");}));
        try{Action(p,26,0);}finally{hook.SetValue(null,null);}
        Check(p.Action==0 && Count("SELECT guards FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==0 && !union.Guards.Contains(p.Entity.UID),"postcommit resignation failure retains the committed removal");p=(GameState)Helper("ReloadGold",p);
        var guards=new System.Collections.Generic.List<GameState>();
        for(int i=0;i<19;i++){var guard=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);Action(guard,8,0,union.ID);NamedAction(leader,24,guard.Entity.Name,0);guards.Add(guard);}
        Check(union.Guards.All(id=>id!=0) && guards.All(guard=>guard.Guards==512),"twenty distinct ordinary members fill the complete guard roster");
        NamedAction(leader,24,p.Entity.Name,0);Check(p.Guards==0 && leader.Action==2 && union.Guards.Count(id=>id!=0)==20,"full guard roster refuses the twenty-first appointment without indexing past capacity");
        Action(leader,25,guards[0].Entity.UID);NamedAction(leader,24,p.Entity.Name,0);
        Check(guards[0].Guards==0 && p.Guards==512 && union.Guards.Count(id=>id!=0)==20,"removing a guard makes exactly one seat available again");
        NamedAction(leader,49,guards[0].Entity.Name,401);NamedAction(leader,49,guards[1].Entity.Name,401);NamedAction(leader,49,guards[1].Entity.Name,402);NamedAction(leader,47,p.Entity.Name,2070);
        Check(union.Harem[2]==guards[0].Entity.UID && guards[0].Harem==2048 && union.Harem[3]==guards[1].Entity.UID && guards[1].Harem==4096
            && p.Offical==256 && union.Coreofficals[7]==p.Entity.UID,"harem group capacity and the eighth core office use the exact supported slot mappings");
    }
    static void AdvertiseJoin(GameState p,uint guild){var bytes=new byte[20];Revora.Network.Writer.WriteUInt16(12,0,bytes);Revora.Network.Writer.WriteUInt16(2227,2,bytes);bytes[4]=1;Revora.Network.Writer.WriteUInt32(guild,8,bytes);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,12,bytes);Revora.Network.PacketHandler.HandlePacket(bytes,p);}
    static void VerifyAdmission(GameState ruler,GameState other) {
        var chief=(GameState)Helper("PrizeLeader",924u);NullableUnionFields(chief);Action(chief,11,0,ruler.UnionID);var guild=chief.Guild;var union=ruler.Union;
        var p=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);
        NullableUnionFields(p);
        Check(p.Account.State==AccountTable.AccountState.Player && !p.Fake && guild.UnionID==union.ID,"ordinary applicant targets a guild with earned union affiliation");
        guild.LevelRequirement=255;AdvertiseJoin(p,guild.ID);guild.LevelRequirement=0;
        guild.BlackList.Add(p.Entity.UID);guild.AddMember(p);guild.BlackList.Remove(p.Entity.UID);
        Check(p.Guild==null && p.Union==null && !guild.Members.ContainsKey(p.Entity.UID),"level requirement and central blacklist guards refuse admission without mutations");
        p.Entity.PledgeStamp=DateTime.Now;AdvertiseJoin(p,guild.ID);Check(p.Guild==null && p.Union==null,"guild recruitment cannot bypass the session union rejoin cooldown");p.Entity.PledgeStamp=DateTime.Now.AddMinutes(-2);
        var foreign=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);Action(foreign,8,0,other.UnionID);AdvertiseJoin(foreign,guild.ID);
        Check(foreign.Guild==null && foreign.UnionID==other.UnionID && Count("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid","uid",(long)foreign.Entity.UID)==0,"conflicting independent union membership is preserved and guild admission refused");
        Sql("UPDATE entities SET guildid=922 WHERE uid=@uid","uid",(long)p.Entity.UID);AdvertiseJoin(p,guild.ID);
        Check(p.Guild==null,"saved guild membership defeats a stale guildless client");Sql("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)p.Entity.UID);
        Sql("UPDATE guilds SET unionid=0 WHERE id=924");AdvertiseJoin(p,guild.ID);Check(p.Guild==null && p.Union==null,"stale cached guild allegiance cannot authorize admission");Sql("UPDATE guilds SET unionid=101 WHERE id=924");
        union.Guards[0]=p.Entity.UID;Sql("UPDATE unions SET guards=@guards WHERE id=101","guards",string.Join("#",union.Guards));
        foreach(string table in new[]{"unions","entities"}) {
            var beforeRow=Sql("SELECT to_jsonb(e)::text FROM entities e WHERE uid=@uid","uid",(long)p.Entity.UID);
            Sql("CREATE FUNCTION reject_admission() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_admission BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_admission()");
            try{AdvertiseJoin(p,guild.ID);}finally{Sql("DROP TRIGGER reject_admission ON "+table+";DROP FUNCTION reject_admission()");}
            Check(p.Action==0 && p.Guild==null && p.Union==null && !guild.Members.ContainsKey(p.Entity.UID) && union.Guards.Contains(p.Entity.UID)
                && Count("SELECT count(*) FROM entities WHERE uid=@uid AND COALESCE(guildid,0)=0 AND COALESCE(unionid,0)=0","uid",(long)p.Entity.UID)==1 && Equals(beforeRow,Sql("SELECT to_jsonb(e)::text FROM entities e WHERE uid=@uid","uid",(long)p.Entity.UID)),"suppressed "+table+" write rolls back guild, union and orphan-office cleanup");p=(GameState)Helper("ReloadGold",p);
        }
        System.Threading.Tasks.Parallel.For(0,12,n=>{if(n%3==0)EntityTable.SaveEntity(p);else AdvertiseJoin(p,guild.ID);});
        Check(ReferenceEquals(p.Guild,guild) && p.Entity.GuildID==guild.ID && p.AsMember.Rank==Enums.GuildMemberRank.Member && p.UnionID==union.ID
            && guild.MemberCount==guild.Members.Count && guild.Members.Count==2 && !union.Guards.Contains(p.Entity.UID) && p.Guards==0,"advertisement replay and concurrent save install guild and union membership exactly once without stale offices");
        Check(EntityTable.SaveEntity(p),"ordinary save retains combined admission");p=(GameState)Helper("ReloadGold",p);
        Check(p.Guild==guild && p.AsMember!=null && p.Entity.GuildRank==(ushort)Enums.GuildMemberRank.Member && p.UnionID==union.ID,"normal reconnect restores the committed guild member and union");
        var same=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);Action(same,8,0,union.ID);NamedAction(ruler,28,same.Entity.Name,2000);NamedAction(ruler,24,same.Entity.Name,0);AdvertiseJoin(same,guild.ID);
        Check(same.Guild==guild && same.UnionID==union.ID && same.Offical!=0 && same.Guards==512 && union.Coreofficals.Contains(same.Entity.UID) && union.Guards.Contains(same.Entity.UID),"joining a guild in the same union preserves earned offices");
        foreach(bool invitationFirst in new[]{false,true}) {
            var applicant=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);
            if(invitationFirst){Helper("Review343Command",chief,GuildCommand.InviteRequest,applicant.Entity.UID);Helper("Review343Command",applicant,GuildCommand.JoinRequest,chief.Entity.UID);}
            else{Helper("Review343Command",applicant,GuildCommand.JoinRequest,chief.Entity.UID);Helper("Review343Command",chief,GuildCommand.InviteRequest,applicant.Entity.UID);}
            Check(applicant.Guild==guild && applicant.UnionID==union.ID && applicant.AsMember.Rank==Enums.GuildMemberRank.Member,invitationFirst?"invitation then acceptance uses the same atomic admission":"application then acceptance uses the same atomic admission");
        }
        var recovering=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildAdmission",true);var hook=service.GetField("BeforeProjection",Hidden);
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected admission projection failure");}));
        try{AdvertiseJoin(recovering,guild.ID);}finally{hook.SetValue(null,null);}
        Check(recovering.Action==0 && recovering.Guild==null && guild.Members.ContainsKey(recovering.Entity.UID)
            && Count("SELECT count(*) FROM entities WHERE uid=@uid AND guildid=924 AND unionid=101","uid",(long)recovering.Entity.UID)==1,"postcommit admission failure fences the player and retains the durable guild and union");
        recovering=(GameState)Helper("ReloadGold",recovering);uint count=guild.MemberCount;AdvertiseJoin(recovering,guild.ID);
        Check(recovering.Guild==guild && recovering.UnionID==union.ID && guild.MemberCount==count,"normal reconnect recovers committed admission and replay does not duplicate the roster");
        var neutral=(GameState)Helper("PrizeLeader",925u);var neutralApplicant=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);
        var legacy=new Guild_AdvertiseCommends{Action=Guild_AdvertiseCommends.Join,GuildID=neutral.Guild.ID};Guild_AdvertiseCommends.Process(legacy.ToArray(),neutralApplicant);
        Check(neutralApplicant.Guild==neutral.Guild && neutralApplicant.UnionID==0,"alternate advertisement handler uses atomic admission for an unaffiliated guild");
    }
    static void ExpelGuild(GameState leader,uint guild){var bytes=Packet("");Revora.Network.Writer.WriteUInt16(13,4,bytes);Revora.Network.Writer.WriteUInt32(guild,6,bytes);Create(leader,bytes);}
    static void VerifyGuildMembership(GameState ruler,GameState other) {
        var chief=(GameState)Helper("PrizeLeader",924u);var online=(GameState)Helper("PurseMember",chief,Enums.GuildMemberRank.Member);
        var offline=(GameState)Helper("PurseMember",chief,Enums.GuildMemberRank.Member);var guild=chief.Guild;var union=ruler.Union;
        NullableUnionFields(chief);NullableUnionFields(online);NullableUnionFields(offline);
        Kernel.GamePool.Remove(offline.Entity.UID);offline.Action=0;
        Action(online,11,0,union.ID);Action(chief,11,0,uint.MaxValue);
        Check(guild.UnionID==0 && chief.Union==null,"nonleader and unknown-union guild admission requests are refused");
        Sql("UPDATE entities SET unionid=@other WHERE uid=@uid","other",(long)other.UnionID,"uid",(long)offline.Entity.UID);Action(chief,11,0,union.ID);
        Check(guild.UnionID==0 && chief.Union==null,"guild admission cannot steal an offline member from another union");Sql("UPDATE entities SET unionid=0 WHERE uid=@uid","uid",(long)offline.Entity.UID);
        var removed=guild.Members[offline.Entity.UID];guild.Members.Remove(offline.Entity.UID);
        try{Action(chief,11,0,union.ID);Check(guild.UnionID==0,"saved roster mismatch refuses a partial guild admission");}finally{guild.Members.Add(offline.Entity.UID,removed);}
        foreach(string table in new[]{"entities","guilds"}) {
            Sql("CREATE TABLE rejected_guild_uid(uid bigint NOT NULL);INSERT INTO rejected_guild_uid VALUES(@uid)","uid",(long)offline.Entity.UID);
            string body=table=="entities"?"IF NEW.uid=(SELECT uid FROM rejected_guild_uid) THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            Sql("CREATE FUNCTION reject_guild_union() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER reject_guild_union BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_guild_union()");
            try{Action(chief,11,0,union.ID);}finally{Sql("DROP TRIGGER reject_guild_union ON "+table+";DROP FUNCTION reject_guild_union();DROP TABLE rejected_guild_uid");}
            Check(chief.Action==0 && online.Action==0 && guild.UnionID==0 && !union.Guilds.Contains(guild.ID)
                && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid<>0")==0 && Count("SELECT unionid FROM guilds WHERE id=924")==0,"suppressed "+table+" admission write rolls back every earlier guild member");
            chief=(GameState)Helper("ReloadGold",chief);online=(GameState)Helper("ReloadGold",online);
        }
        System.Threading.Tasks.Parallel.For(0,8,n=>Action(chief,11,0,union.ID));
        Check(chief.UnionID==union.ID && online.UnionID==union.ID && online.Entity.CharBuffer._UnionID==union.ID && guild.UnionID==union.ID
            && union.Guilds.Count(id=>id==guild.ID)==1 && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid=101")==3,"concurrent guild admission links online and offline members exactly once");
        Check(EntityTable.SaveEntity(chief) && EntityTable.SaveEntity(online),"ordinary saves retain the committed guild admission");
        chief=(GameState)Helper("ReloadGold",chief);online=(GameState)Helper("ReloadGold",online);
        Check(chief.UnionID==union.ID && online.UnionID==union.ID && chief.Guild.UnionID==union.ID,"normal character reconnect retains guild union membership");
        guild.Disband();Check(guild.UnionID==union.ID && guild.Members.Count==3 && Count("SELECT count(*) FROM guilds WHERE id=924")==1
            && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid=101")==3,"linked guild dissolution must use the atomic withdrawal route first");
        Action(ruler,12,0);ExpelGuild(ruler,ruler.Guild.ID);
        Check(ruler.Union==union && ruler.Guild.UnionID==union.ID && ruler.Offical==1,"guild departure and expulsion cannot orphan the union ruler");
        ExpelGuild(other,guild.ID);Action(online,12,0);Check(guild.UnionID==union.ID,"foreign rulers and ordinary guild members cannot remove the guild");
        NamedAction(ruler,28,online.Entity.Name,2000);NamedAction(ruler,24,online.Entity.Name,0);
        Sql("CREATE FUNCTION reject_guild_union() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_guild_union BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION reject_guild_union()");
        try{Action(chief,12,0);}finally{Sql("DROP TRIGGER reject_guild_union ON guilds;DROP FUNCTION reject_guild_union()");}
        Check(chief.Action==0 && online.Action==0 && guild.UnionID==union.ID && union.Coreofficals.Contains(online.Entity.UID) && union.Guards.Contains(online.Entity.UID)
            && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid=101")==3,"failed final guild unlink rolls back all member removals and offices");
        chief=(GameState)Helper("ReloadGold",chief);online=(GameState)Helper("ReloadGold",online);Action(chief,12,0);
        Check(guild.UnionID==0 && chief.UnionID==0 && online.UnionID==0 && online.Offical==0 && online.Guards==0 && !union.Guilds.Contains(guild.ID)
            && !union.Coreofficals.Contains(online.Entity.UID) && !union.Guards.Contains(online.Entity.UID) && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid<>0")==0,"voluntary guild departure clears online and offline membership plus appointed offices");
        chief.Entity.PledgeStamp=DateTime.Now.AddMinutes(-2);Action(chief,11,0,union.ID);
        Sql("UPDATE entities SET unionid=@other WHERE uid=@uid","other",(long)other.UnionID,"uid",(long)offline.Entity.UID);ExpelGuild(ruler,guild.ID);
        Check(guild.UnionID==0 && chief.UnionID==0 && online.UnionID==0 && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid=101")==0
            && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)offline.Entity.UID)==other.UnionID,"guild expulsion clears its union while preserving an independently affiliated offline member");
        Sql("UPDATE entities SET unionid=0 WHERE uid=@uid","uid",(long)offline.Entity.UID);
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionGuildMembership",true);var hook=service.GetField("BeforeProjection",Hidden);
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected guild membership projection failure");}));
        try{Action(chief,11,0,union.ID);}finally{hook.SetValue(null,null);}
        Check(chief.Action==0 && online.Action==0 && guild.UnionID==union.ID && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid=101")==3,"postcommit guild join failure fences online members while preserving the whole committed roster");
        chief=(GameState)Helper("ReloadGold",chief);online=(GameState)Helper("ReloadGold",online);
        Check(chief.UnionID==union.ID && online.UnionID==union.ID,"ordinary reconnect recovers guild admission after projection failure");
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected guild expulsion projection failure");}));
        try{ExpelGuild(ruler,guild.ID);}finally{hook.SetValue(null,null);}
        Check(ruler.Action==0 && chief.Action==0 && online.Action==0 && guild.UnionID==0 && Count("SELECT count(*) FROM entities WHERE guildid=924 AND unionid<>0")==0,"postcommit guild expulsion failure preserves durable removal and fences affected online sessions");
        ruler=(GameState)Helper("ReloadGold",ruler);chief=(GameState)Helper("ReloadGold",chief);online=(GameState)Helper("ReloadGold",online);offline=(GameState)Helper("ReloadGold",offline);ExpelGuild(ruler,guild.ID);
        Check(chief.UnionID==0 && online.UnionID==0 && offline.UnionID==0 && offline.Offical==0 && ReferenceEquals(offline.Guild,guild)
            && ruler.Union==union && ruler.Offical==1,"all former members reconnect without union membership, retain their guild, and replay cannot remove the ruler");
    }
    static void VerifyExpulsion(GameState leader,GameState p,GameState other) {
        NullableUnionFields(leader);NullableUnionFields(p);
        var union=leader.Union;var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionExpulsion",true);
        NamedAction(leader,28,p.Entity.Name,2000);NamedAction(leader,24,p.Entity.Name,0);
        Check(union.Coreofficals.Contains(p.Entity.UID) && union.Guards.Contains(p.Entity.UID),"ordinary target has appointed offices before expulsion");
        Action(p,10,0,leader.Entity.UID);Action(leader,10,0,leader.Entity.UID);Action(leader,10,0,other.Entity.UID);Action(leader,10,0,uint.MaxValue);
        Check(leader.Union==union && p.Union==union && other.UnionID!=union.ID && leader.Action==2,"nonleaders, self-expulsion, foreign and missing targets cannot change membership");
        Sql("UPDATE entities SET offical=0 WHERE uid=@uid","uid",(long)leader.Entity.UID);Action(leader,10,0,p.Entity.UID);
        Check(p.Union==union && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==union.ID,"saved leader office overrides a stale runtime authority claim");Sql("UPDATE entities SET offical=1 WHERE uid=@uid","uid",(long)leader.Entity.UID);
        Sql("UPDATE unions SET leaderuid=@other WHERE id=101","other",(long)other.Entity.UID);Action(leader,10,0,p.Entity.UID);
        Check(p.Union==union && union.Guards.Contains(p.Entity.UID),"saved union leadership overrides stale cached leadership");Sql("UPDATE unions SET leaderuid=@uid WHERE id=101","uid",(long)leader.Entity.UID);
        foreach(string table in new[]{"unions","entities"}) {
            var before=Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101"));
            Sql("CREATE FUNCTION reject_expulsion() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_expulsion BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_expulsion()");
            try{Action(leader,10,0,p.Entity.UID);}finally{Sql("DROP TRIGGER reject_expulsion ON "+table+";DROP FUNCTION reject_expulsion()");}
            Check(leader.Action==0 && p.Action==0 && p.Union==union && union.Coreofficals.Contains(p.Entity.UID) && union.Guards.Contains(p.Entity.UID)
                && before==Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101"))
                && Count("SELECT count(*) FROM entities WHERE uid=@uid AND unionid=101 AND offical<>0 AND guards<>0","uid",(long)p.Entity.UID)==1,"suppressed "+table+" write rolls back an online expulsion completely");
            leader=(GameState)Helper("ReloadGold",leader);p=(GameState)Helper("ReloadGold",p);
        }
        System.Threading.Tasks.Parallel.For(0,8,n=>Action(leader,10,0,p.Entity.UID));
        Check(p.Union==null && p.UnionID==0 && p.Entity.CharBuffer._UnionID==0 && p.Offical==0 && p.Harem==0 && p.Guards==0
            && !union.Coreofficals.Contains(p.Entity.UID) && !union.Harem.Contains(p.Entity.UID) && !union.Guards.Contains(p.Entity.UID),"concurrent online expulsions clear membership, office arrays and spawn identity once");
        Check(EntityTable.SaveEntity(p),"ordinary save retains committed online expulsion");p=(GameState)Helper("ReloadGold",p);
        Check(p.Union==null && p.UnionID==0 && p.Guards==0,"ordinary reconnect retains expelled status");
        Action(p,8,0,union.ID);NamedAction(leader,28,p.Entity.Name,2000);NamedAction(leader,24,p.Entity.Name,0);
        var hook=service.GetField("BeforeProjection",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected expulsion projection failure");}));
        try{Action(leader,10,0,p.Entity.UID);}finally{hook.SetValue(null,null);}
        Check(leader.Action==0 && p.Action==0 && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==0,"postcommit online projection failure fences both sessions without undoing expulsion");
        leader=(GameState)Helper("ReloadGold",leader);p=(GameState)Helper("ReloadGold",p);
        Check(p.Union==null && p.Offical==0 && p.Guards==0 && leader.Offical==1,"both sessions recover committed expulsion after projection failure");
        Action(p,8,0,union.ID);NamedAction(leader,28,p.Entity.Name,2000);NamedAction(leader,24,p.Entity.Name,0);
        Check(EntityTable.SaveEntity(p),"offline target is saved with its appointed offices");Kernel.GamePool.Remove(p.Entity.UID);p.Action=0;
        var leases=typeof(Entity).Assembly.GetType("Revora.Game.Features.SessionInstall",true);
        Check((bool)Invoke(leases,"TryEnter",p.Entity.UID),"fixture reserves the target login lease");
        try{System.Threading.Tasks.Task.Run(()=>Action(leader,10,0,p.Entity.UID)).GetAwaiter().GetResult();Check(Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==union.ID,"offline expulsion defers while target login owns its lease");}finally{Invoke(leases,"Exit",p.Entity.UID);}
        Sql("CREATE FUNCTION reject_expulsion() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_expulsion BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_expulsion()");
        try{Action(leader,10,0,p.Entity.UID);}finally{Sql("DROP TRIGGER reject_expulsion ON entities;DROP FUNCTION reject_expulsion()");}
        Check(leader.Action==0 && union.Guards.Contains(p.Entity.UID) && Count("SELECT count(*) FROM entities WHERE uid=@uid AND unionid=101 AND offical<>0 AND guards<>0","uid",(long)p.Entity.UID)==1,"offline character-write failure preserves membership and offices");leader=(GameState)Helper("ReloadGold",leader);
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected offline expulsion projection failure");}));
        try{Action(leader,10,0,p.Entity.UID);}finally{hook.SetValue(null,null);}
        Check(leader.Action==0 && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==0 && !union.Coreofficals.Contains(p.Entity.UID) && !union.Guards.Contains(p.Entity.UID),"offline expulsion remains committed after leader notification failure");
        leader=(GameState)Helper("ReloadGold",leader);p=(GameState)Helper("ReloadGold",p);Action(leader,10,0,p.Entity.UID);
        Check(p.Union==null && p.UnionID==0 && p.Offical==0 && p.Harem==0 && p.Guards==0 && p.Action==2,"offline expelled member logs in without stale offices and replay is harmless");
    }
    static void VerifyMembership(GameState leader,GameState p,GameState other) {
        NullableUnionFields(leader);NullableUnionFields(p);
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionMembership",true);
        var union=leader.Union;
        Action(leader,9,0);Check(leader.Union==union && leader.Offical==1 && Count("SELECT count(*) FROM entities WHERE unionid=101 AND offical=1")==1,"ruler cannot abandon leadership through voluntary departure");
        var outsider=(GameState)Helper("TravelPlayer",Kernel.Maps[1002],(ushort)317,(ushort)322);
        Action(outsider,8,0,uint.MaxValue);Check(outsider.Union==null && outsider.Action==2,"unknown union ID is refused without an exception or fencing");
        Sql("UPDATE entities SET guildid=921 WHERE uid=@uid","uid",(long)outsider.Entity.UID);Action(outsider,8,0,union.ID);
        Check(outsider.Union==null,"saved guild membership prevents a stale guildless join");Sql("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        NamedAction(leader,28,p.Entity.Name,2000);NamedAction(leader,24,p.Entity.Name,0);
        Check(union.Coreofficals.Contains(p.Entity.UID) && union.Guards.Contains(p.Entity.UID),"departing ordinary member holds real appointed offices");
        foreach(string table in new[]{"unions","entities"}) {
            var before=Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101"));
            Sql("CREATE FUNCTION reject_membership() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_membership BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_membership()");
            try{Action(p,9,0);}finally{Sql("DROP TRIGGER reject_membership ON "+table+";DROP FUNCTION reject_membership()");}
            Check(p.Action==0 && p.Union==union && union.Coreofficals.Contains(p.Entity.UID) && union.Guards.Contains(p.Entity.UID)
                && before==Convert.ToString(Sql("SELECT to_jsonb(u)::text FROM unions u WHERE id=101")) && Count("SELECT count(*) FROM entities WHERE uid=@uid AND unionid=101 AND offical<>0 AND guards<>0","uid",(long)p.Entity.UID)==1,"suppressed "+table+" write rolls back membership and every office array");
            p=(GameState)Helper("ReloadGold",p);
        }
        System.Threading.Tasks.Parallel.For(0,8,n=>Action(p,9,0));
        Check(p.Union==null && p.UnionID==0 && p.Entity.CharBuffer._UnionID==0 && p.Offical==0 && p.Guards==0 && p.Harem==0
            && !union.Coreofficals.Contains(p.Entity.UID) && !union.Harem.Contains(p.Entity.UID) && !union.Guards.Contains(p.Entity.UID),"concurrent quit clears membership, offices and immediate spawn identity once");
        Action(p,8,0,union.ID);Check(p.Union==null,"existing one-minute session cooldown refuses immediate rejoin");
        Check(EntityTable.SaveEntity(p),"ordinary save cannot resurrect the departed union");p=(GameState)Helper("ReloadGold",p);
        Check(p.Union==null && p.UnionID==0 && p.Offical==0 && p.Guards==0,"normal reconnect retains cleared membership and offices");
        p.Entity.PledgeStamp=DateTime.Now.AddMinutes(-2);
        Sql("CREATE FUNCTION reject_membership() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_membership BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_membership()");
        try{Action(p,8,0,union.ID);}finally{Sql("DROP TRIGGER reject_membership ON entities;DROP FUNCTION reject_membership()");}
        Check(p.Action==0 && p.Union==null && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==0,"failed join cannot publish membership before commit");p=(GameState)Helper("ReloadGold",p);
        var hook=service.GetField("BeforeProjection",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected membership projection failure");}));
        try{Action(p,8,0,union.ID);}finally{hook.SetValue(null,null);}
        Check(p.Action==0 && p.Union==null && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==union.ID,"postcommit join failure fences the client and preserves committed membership");p=(GameState)Helper("ReloadGold",p);
        Action(p,8,0,other.UnionID);Check(p.UnionID==union.ID && p.Offical==0 && p.Guards==0,"reconnect recovers joined membership and replay cannot switch unions");
        hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected departure projection failure");}));
        try{Action(p,9,0);}finally{hook.SetValue(null,null);}
        Check(p.Action==0 && Count("SELECT unionid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==0,"postcommit quit failure preserves durable departure");p=(GameState)Helper("ReloadGold",p);
        Check(p.Union==null && p.UnionID==0 && p.Entity.CharBuffer._UnionID==0,"reconnect after failed departure projection restores empty union identity");
    }
    static void VerifyRecovery(string runtime) {
        fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"KingdomField.Helpers.exe")).GetType("PotencyHuntTests",true);
        Check(Count("SELECT process FROM crown_checkpoint")!=System.Diagnostics.Process.GetCurrentProcess().Id,"verification runs in a different executable process");
        Check(Convert.ToBoolean(Sql("SELECT started <> pg_postmaster_start_time() FROM crown_checkpoint")),"verification follows an actual PostgreSQL restart");
        Check(Equals(Sql("SELECT contests FROM crown_checkpoint"),Sql("SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c")),"cold database retains the exact contest result and expiry");
        Check(Equals(Sql("SELECT wardrobe FROM crown_checkpoint"),Sql("SELECT jsonb_agg(to_jsonb(t) ORDER BY id)::text FROM titles t")),"cold database retains exact wardrobe ownership and equip records");
        ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
        var companion=fixtures.Assembly.GetType("CompanionTests",true);Invoke(companion,"Reset");
        // Keep login-shell fixture inserts separate from every saved character.
        companion.GetField("nextUid",Hidden).SetValue(null,checked((uint)Count("SELECT max(uid)+100 FROM entities")));
        var town=(Map)Helper("World",runtime,(ushort)1002);Helper("World",runtime,(ushort)3998);Server.Vars=new VariableVault();
        Revora.Union.UnionTable.Load();ArenaTable.Load();TeamArenaTable.Load();ChampionTable.Load();
        crownNow=new DateTime(Count("SELECT ticks FROM crown_checkpoint"),DateTimeKind.Utc).AddMinutes(1);
        Field.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>crownNow));typeof(Entity).Assembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>crownNow));Invoke(Field,"Initialize");
        uint uid=(uint)Count("SELECT ruler FROM crown_checkpoint");
        var p=(GameState)Helper("TravelPlayer",town,(ushort)317,(ushort)322);uint placeholder=p.Entity.UID;
        p.Account.EntityID=uid;p.Account.Username="crown_recovery_fixture";p.Quests=new Quests(p);
        Check((bool)Invoke(typeof(EntityTable),"LoadEntityState",p),"fresh ordinary character login loads the saved successor");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[uid]=p;p.Action=2;p.Entity.FullyLoaded=true;
        new TitleStorage().Login(p);
        Check(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.UnionID==101 && p.Offical==1 && p.Union.LeaderUID==uid && p.Union.IsKingdom(),"cold authority recognizes the ordinary successor rather than the historical winner");
        Check(Titles(p).ContainsKey(11000) && Titles(p)[11000] && Titles(p).ContainsKey(40011000) && Titles(p)[40011000]
            && p.Entity.EquippedTitle==11000 && p.Entity.EquippedWing==40011000,"cold wardrobe login restores earned ownership and both equipped visuals");
        var ledger=typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCompetitionLedger",true);
        Invoke(ledger,"Settle","2026-10-12",crownNow);Invoke(Crown,"Tick");
        Check(Equals(Sql("SELECT contests FROM crown_checkpoint"),Sql("SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c")),"cold settlement replay preserves original winner and fixed expiry");
        crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,13,18,0,0,DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        Check(!p.Union.IsKingdom(),"recovered authority expires at its original deadline before a pulse");
        Invoke(Crown,"Tick");new TitleStorage().Login(p);
        Check(!Titles(p).ContainsKey(11000) && !Titles(p).ContainsKey(40011000) && p.Entity.EquippedTitle!=11000 && p.Entity.EquippedWing!=40011000,"recovered expired rewards lose ownership and equipped visuals");
    }
    static int Main(string[] args){
        if(args.Length>1 || args.Length==1 && args[0]!="capture" && args[0]!="verify" && args[0]!="membership" && args[0]!="expulsion" && args[0]!="guild" && args[0]!="admission" && args[0]!="offices")throw new ArgumentException("Use capture, verify, membership, expulsion, guild, admission, offices, or no arguments for the full journey.");
        string phase=args.Length==0?"journey":args[0];
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Fixed private fixture required.");
        DataHolder.ConnectionString=b.ConnectionString;string prior=Environment.CurrentDirectory;bool owned=false,completed=false;
        try {
            if(phase!="verify")Sql("CREATE SCHEMA revora_kingdom_crown_fixture;COMMENT ON SCHEMA revora_kingdom_crown_fixture IS 'revora-owned-kingdom-crown-v1'");
            if(!Equals(Sql("SELECT obj_description(oid,'pg_namespace') FROM pg_namespace WHERE nspname='revora_kingdom_crown_fixture'"),"revora-owned-kingdom-crown-v1"))throw new InvalidOperationException("Fixture ownership mismatch.");owned=true;
            b.SearchPath="revora_kingdom_crown_fixture";DataHolder.ConnectionString=b.ConnectionString;
            const string runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";Environment.CurrentDirectory=runtime;
            if(phase=="verify"){VerifyRecovery(runtime);completed=true;Console.WriteLine("Kingdom crown cold recovery checks passed: "+checks);return 0;}
            foreach(string table in new[]{"entities","items","titles","skills","arena","teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","chi","npcs","sobnpcs","monsterspawns","furniture","unions"})Sql(table=="entities"?"CREATE TABLE entities AS TABLE public.entities WITH NO DATA":"CREATE TABLE "+table+" (LIKE public."+table+" INCLUDING ALL)");
            Sql("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL,leadername text NOT NULL DEFAULT '',unionid bigint NOT NULL DEFAULT 0);CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"KingdomField.Helpers.exe")).GetType("PotencyHuntTests",true);
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence",true),"Initialize");Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
            Invoke(fixtures.Assembly.GetType("CompanionTests",true),"Reset");var town=(Map)Helper("World",runtime,(ushort)1002);Server.Vars=new VariableVault();
            Sql("ALTER TABLE entities ALTER COLUMN unionid SET DEFAULT 0,ALTER COLUMN offical SET DEFAULT 0,ALTER COLUMN harem SET DEFAULT 0,ALTER COLUMN guards SET DEFAULT 0;CREATE TABLE configuration(unionid bigint NOT NULL);INSERT INTO configuration VALUES(100)");
            var a=(GameState)Helper("PrizeLeader",921u);var bOwner=(GameState)Helper("PrizeLeader",922u);var failed=(GameState)Helper("PrizeLeader",923u);
            var member=(GameState)Helper("PurseMember",a,Enums.GuildMemberRank.Member);
            Sql("UPDATE entities SET unionid=NULL,offical=NULL,harem=NULL,guards=NULL WHERE uid=@uid","uid",(long)a.Entity.UID);
            Check(a.Account.State==AccountTable.AccountState.Player && !a.Fake,"creation uses an ordinary non-GM guild leader");
            foreach(byte[] invalid in new[]{new byte[0],new byte[19],new byte[600],Packet(""),Packet("   "),Packet("bad\nname")})Create(a,invalid);
            var damaged=Packet("BadLength");damaged[19]=255;Create(a,damaged);damaged=Packet("BadSeal");damaged[damaged.Length-1]^=1;Create(a,damaged);
            Check(Count("SELECT count(*) FROM unions")==0,"malformed frames, lengths, seal and blank/control names have no effects");
            Create(member,Packet("NotLeader"));a.Fake=true;Create(a,Packet("NotReal"));a.Fake=false;
            Check(Count("SELECT count(*) FROM unions")==0,"ordinary member and fake founder cannot create unions");
            Sql("UPDATE entities SET guildrank=200 WHERE uid=@uid","uid",(long)a.Entity.UID);Create(a,Packet("StaleRank"));
            Check(Count("SELECT count(*) FROM unions")==0 && a.Union==null,"saved rank overrides a stale runtime leader claim");Sql("UPDATE entities SET guildrank=1000 WHERE uid=@uid","uid",(long)a.Entity.UID);
            var oldCache=Kernel.Unions;var request=Packet("FirstRealm");System.Threading.Tasks.Parallel.For(0,8,n=>Create(a,request));
            Check(Count("SELECT count(*) FROM unions")==1 && a.Union!=null && a.UnionID==101 && a.Offical==1 && a.Entity.CharBuffer._UnionID==101,"concurrent creation produces one union and immediate founder projection");
            Check(!ReferenceEquals(oldCache,Kernel.Unions) && oldCache.Count==0 && a.Guild.UnionID==101,"cache publication preserves readers of the prior dictionary");
            Check(Count("SELECT count(*) FROM entities WHERE unionid=101 AND offical=1")==1 && Count("SELECT count(*) FROM guilds WHERE unionid=101")==1 && Count("SELECT unionid FROM configuration")==101,"union, founder, guild and allocator commit together");
            Create(a,Packet("Again"));Create(bOwner,Packet("FirstRealm"));Check(Count("SELECT count(*) FROM unions")==1 && bOwner.Union==null,"request replay and duplicate union name do not allocate another identity");
            Sql("CREATE FUNCTION reject_formation() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_formation BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION reject_formation()");
            Create(failed,Packet("RetryRealm"));
            Check(Count("SELECT count(*) FROM unions")==1 && Count("SELECT count(*) FROM entities WHERE unionid>0")==1 && Count("SELECT unionid FROM configuration")==101 && failed.Union==null && failed.Guild.UnionID==0,"suppressed guild link rolls back prior union/founder writes and allocator");
            Sql("DROP TRIGGER reject_formation ON guilds;DROP FUNCTION reject_formation()");failed=(GameState)Helper("ReloadGold",failed);Create(failed,Packet("RetryRealm"));
            Check(failed.UnionID==102 && Count("SELECT count(*) FROM unions")==2,"ordinary reconnect retries the rolled-back creation once");
            var formation=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionFormation",true);var hook=formation.GetField("BeforeProjection",Hidden);
            hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected postcommit projection failure");}));
            try{Create(bOwner,Packet("CommittedRealm"));}finally{hook.SetValue(null,null);}
            Check(Count("SELECT count(*) FROM unions")==3 && bOwner.Union==null && bOwner.Action==0 && Kernel.Unions.ContainsKey(103),"postcommit projection failure fences the founder without rolling back committed identity");
            bOwner=(GameState)Helper("ReloadGold",bOwner);Create(bOwner,Packet("CommittedRealm"));
            Check(bOwner.UnionID==103 && bOwner.Offical==1 && Count("SELECT count(*) FROM unions")==3 && Count("SELECT unionid FROM configuration")==103,"normal reconnect recovers a committed formation without allocating twice");
            a=(GameState)Helper("ReloadGold",a);Check(a.UnionID==101 && a.Offical==1 && a.Entity.CharBuffer._UnionID==101 && a.Union.LeaderUID==a.Entity.UID,"founder union and office survive normal character login");
            var successor=(GameState)Helper("TravelPlayer",town,(ushort)317,(ushort)322);NullableUnionFields(successor);Check(EntityTable.SaveEntity(successor),"ordinary guildless successor is saved");
            Action(successor,8,0,a.UnionID);Check(successor.UnionID==101 && successor.Union==a.Union,"ordinary allegiance request joins the created union");
            if(phase=="membership"){VerifyMembership(a,successor,bOwner);completed=true;Console.WriteLine("Union membership checks passed: "+checks);return 0;}
            if(phase=="expulsion"){VerifyExpulsion(a,successor,bOwner);completed=true;Console.WriteLine("Union expulsion checks passed: "+checks);return 0;}
            if(phase=="guild"){VerifyGuildMembership(a,bOwner);completed=true;Console.WriteLine("Guild union membership checks passed: "+checks);return 0;}
            if(phase=="admission"){VerifyAdmission(a,bOwner);completed=true;Console.WriteLine("Guild admission checks passed: "+checks);return 0;}
            if(phase=="offices"){VerifyOffices(a,successor,bOwner);completed=true;Console.WriteLine("Union office checks passed: "+checks);return 0;}
            NamedAction(a,28,successor.Entity.Name,2000);NamedAction(a,24,successor.Entity.Name,0);
            Check(a.Union.Coreofficals.Contains(successor.Entity.UID) && a.Union.Guards.Contains(successor.Entity.UID),"ordinary leader appointments fill successor offices before transfer");
            Helper("World",runtime,(ushort)3998);
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,12,17,0,0,DateTimeKind.Unspecified),zone);
            Field.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>crownNow));typeof(Entity).Assembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>crownNow));Invoke(Field,"Initialize");
            Kernel.ServerKingdom=101;Check(!a.Union.IsKingdom() && !((System.Collections.Generic.HashSet<int>)Helper("EligibleTitles",a)).Contains(11000),"legacy kingdom flag cannot authorize a competitive crown");Invoke(Crown,"Tick");
            if(!town.Npcs.ContainsKey(10003))town.AddNpc(new NpcSpawn{UID=10003,MapID=1002,X=317,Y=321,Mesh=10,Type=Enums.NpcType.Talker});
            a.Entity.RevoraPoint=300000;a.Union.UnionFund=4000000000;var rvp=a.Entity.RevoraPoint;var funds=a.Union.UnionFund;Talk(a,10003,99);
            Check(a.Entity.RevoraPoint==rvp && a.Union.UnionFund==funds && !a.Union.IsKingdom(),"retired paid promotion spends no RVP or union funds and grants no crown");
            bOwner.Union.GoldBricks=1000000;Earn(a);crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,12,18,0,0,DateTimeKind.Unspecified),zone);Invoke(Field,"Tick");
            Check(Kernel.ServerKingdom==101 && a.Union.IsKingdom() && Titles(a).ContainsKey(11000) && Titles(a).ContainsKey(40011000) && !Titles(bOwner).ContainsKey(11000),"ordinary quarry deposit and settlement earn Overlord and Solar Dragon despite a richer rival union");
            crownNow=crownNow.AddSeconds(-1);Check(!a.Union.IsKingdom() && !((System.Collections.Generic.HashSet<int>)Helper("EligibleTitles",a)).Contains(11000),"cached authority refuses time before the contest end after a clock rollback");crownNow=crownNow.AddSeconds(1);
            var fixedExpiry=Convert.ToString(Sql("SELECT expires FROM revora_kingdom_contests WHERE day='2026-10-12'"));
            Sql("ALTER TABLE revora_kingdom_contests RENAME TO crown_unavailable");bool readFailed=false;
            try{Invoke(Crown,"Tick");}catch(NpgsqlException){readFailed=true;}finally{Sql("ALTER TABLE crown_unavailable RENAME TO revora_kingdom_contests");}
            Check(readFailed && Kernel.ServerKingdom==0 && !a.Union.IsKingdom(),"authority read failure immediately clears cached crown authorization");Invoke(Crown,"Tick");
            Check(a.Union.IsKingdom() && fixedExpiry==Convert.ToString(Sql("SELECT expires FROM revora_kingdom_contests WHERE day='2026-10-12'")),"authority recovery restores the same result without renewing expiry");
            EquipCrown(a);Check(a.Entity.EquippedTitle==11000 && a.Entity.EquippedWing==40011000,"ordinary winning leader equips both earned crown rewards");
            Action(member,14,successor.Entity.UID);Action(a,14,bOwner.Entity.UID);Action(a,14,a.Entity.UID);
            Check(a.Union.LeaderUID==a.Entity.UID,"nonleader, foreign-union and self succession requests are refused");
            Sql("CREATE FUNCTION reject_succession() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_succession BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_succession()");
            Action(a,14,successor.Entity.UID);
            Check(Count("SELECT leaderuid FROM unions WHERE id=101")==a.Entity.UID && a.Union.Coreofficals.Contains(successor.Entity.UID) && a.Union.Guards.Contains(successor.Entity.UID),"suppressed character write rolls back leadership and office-array changes");
            Sql("DROP TRIGGER reject_succession ON entities;DROP FUNCTION reject_succession()");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);
            System.Threading.Tasks.Parallel.For(0,8,n=>Action(a,14,successor.Entity.UID));
            Check(a.Union.LeaderUID==successor.Entity.UID && successor.Offical==1 && successor.Harem==0 && successor.Guards==0 && a.Offical==0,"concurrent succession installs one new leader and updates both players immediately");
            Check(!Titles(a).ContainsKey(11000) && !Titles(a).ContainsKey(40011000) && a.Entity.EquippedTitle!=11000 && a.Entity.EquippedWing!=40011000
                && Titles(successor).ContainsKey(11000) && Titles(successor).ContainsKey(40011000),"successful succession immediately revokes old crown ownership and equipped visuals and awards the successor");
            Check(Count("SELECT leader FROM revora_kingdom_contests WHERE day='2026-10-12'")==a.Entity.UID,"succession preserves the frozen original contest winner record");
            if(phase=="capture") {
                EquipCrown(successor);Check(EntityTable.SaveEntity(a) && EntityTable.SaveEntity(successor),"capture saves former leader and equipped successor");
                Sql("CREATE TABLE crown_checkpoint(ruler bigint NOT NULL,ticks bigint NOT NULL,process integer NOT NULL,started timestamptz NOT NULL,contests text NOT NULL,wardrobe text NOT NULL);INSERT INTO crown_checkpoint SELECT @uid,@ticks,@pid,pg_postmaster_start_time(),(SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c),(SELECT jsonb_agg(to_jsonb(t) ORDER BY id)::text FROM titles t)","uid",(long)successor.Entity.UID,"ticks",crownNow.Ticks,"pid",System.Diagnostics.Process.GetCurrentProcess().Id);
                completed=true;Console.WriteLine("Kingdom crown capture checks passed: "+checks);return 0;
            }
            Check(a.Union.Coreofficals.All(x=>x!=successor.Entity.UID) && a.Union.Harem.All(x=>x!=successor.Entity.UID) && a.Union.Guards.All(x=>x!=successor.Entity.UID)
                && Count("SELECT count(*) FROM entities WHERE unionid=101 AND offical=1")==1,"successor loses former office slots and exactly one saved leader office remains");
            Action(a,14,a.Entity.UID);NamedAction(a,28,a.Entity.Name,2000);Action(successor,29,successor.Entity.UID);
            Check(successor.Offical==1 && a.Offical==0 && a.Union.LeaderUID==successor.Entity.UID,"old-leader authority and legacy office removal cannot undo succession");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);
            Check(a.Offical==0 && successor.Offical==1 && successor.Union.LeaderUID==successor.Entity.UID,"normal login preserves the successor and former leader offices");
            var succession=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionSuccession",true);var transferHook=succession.GetField("BeforeProjection",Hidden);
            transferHook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected succession projection failure");}));
            try{Action(successor,14,a.Entity.UID);}finally{transferHook.SetValue(null,null);}
            Check(Count("SELECT leaderuid FROM unions WHERE id=101")==a.Entity.UID && successor.Action==0 && a.Action==0,"postcommit succession failure fences both clients while keeping durable leadership");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);Action(successor,14,a.Entity.UID);
            Check(a.Offical==1 && successor.Offical==0 && a.Union.LeaderUID==a.Entity.UID,"both clients recover committed succession and an old request cannot replay it");
            new TitleStorage().Login(a);new TitleStorage().Login(successor);
            Check(Titles(a).ContainsKey(40011000) && !Titles(successor).ContainsKey(40011000),"postcommit reconnect restores exactly the current ruler's rewards");EquipCrown(a);
            crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,13,17,0,0,DateTimeKind.Unspecified),zone);Earn(bOwner);
            crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,13,18,0,0,DateTimeKind.Unspecified),zone);
            Check(!a.Union.IsKingdom() && !((System.Collections.Generic.HashSet<int>)Helper("EligibleTitles",a)).Contains(11000),"fixed contest expiry denies old crown eligibility before the timer pulse");
            ((TitleStorage)Helper("TitleRequest",40011000,TitleStorage.Action.Equip)).Handle(a);Check(a.Entity.EquippedWing!=40011000,"expired crown cannot be re-equipped between pulses");Invoke(Field,"Tick");
            Check(Kernel.ServerKingdom==103 && Titles(bOwner).ContainsKey(40011000) && !Titles(a).ContainsKey(40011000),"next earned contest replaces the ruler and revokes the previous rewards");
            crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,14,17,0,0,DateTimeKind.Unspecified),zone);a.Entity.Teleport(1002,316,325);Talk(a,903980,1);
            crownNow=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,14,18,0,0,DateTimeKind.Unspecified),zone);Invoke(Field,"Tick");
            Check(Kernel.ServerKingdom==0 && !Titles(bOwner).ContainsKey(40011000),"an empty finalized contest produces no ruler and no stale crown fallback");
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCompetitionLedger",true),"Settle","2026-10-12",crownNow);Invoke(Crown,"Tick");
            Check(Kernel.ServerKingdom==0 && !Titles(a).ContainsKey(11000),"historical settlement replay cannot resurrect an expired crown");
            var unionRows=Convert.ToString(Sql("SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u"));Kernel.Unions=new System.Collections.Generic.Dictionary<uint,Revora.Network.GamePackets.Union.UnionClass>();Revora.Union.UnionTable.Load();
            Check(unionRows==Convert.ToString(Sql("SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u")) && Kernel.Unions[101].Guilds.Contains(a.Guild.ID) && Kernel.Unions[102].Coreofficals.Length==8,"fresh union cache restores exact saved formation and complete offices without rewriting rows");
            completed=true;Console.WriteLine("Kingdom crown journey checks passed: "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1;}
        finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            if(owned && (phase=="journey" || phase=="membership" || phase=="expulsion" || phase=="guild" || phase=="admission" || phase=="offices" || phase=="verify" && completed))Sql("DROP SCHEMA revora_kingdom_crown_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
