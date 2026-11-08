using System;
using System.Globalization;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildSeasonClaims
    {
        internal const uint Director=10003,Token=723467;
        private static bool initialized;
        internal static void Initialize() {
            ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(Token,out info) || info.Name!="LordToken")throw new InvalidOperationException("Guild token catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_token_claims(
day varchar(10) PRIMARY KEY REFERENCES revora_guild_battles(day),season varchar(10) NOT NULL REFERENCES revora_guild_seasons(id),
guild_id bigint NOT NULL CHECK(guild_id>0),uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),
claimed_at timestamptz NOT NULL,expires_at timestamptz NOT NULL,spent_at timestamptz);
CREATE INDEX IF NOT EXISTS revora_guild_token_owner ON revora_guild_token_claims(uid,season) WHERE spent_at IS NULL;");}initialized=true;
        }
        internal static bool ClaimWindow(string day,string zone,DateTime now) {
            if(now.Kind!=DateTimeKind.Utc)return false;DateTime sunday;
            if(!DateTime.TryParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out sunday) || sunday.DayOfWeek!=DayOfWeek.Sunday)return false;
            var tz=TimeZoneInfo.FindSystemTimeZoneById(zone);
            return now>=TimeZoneInfo.ConvertTimeToUtc(sunday.AddDays(1).AddMinutes(90),tz)
                && now<TimeZoneInfo.ConvertTimeToUtc(sunday.AddDays(5),tz);
        }
        internal static bool Near(GameState p) {
            return Q.Near(p,Director,1002) && p.Map.ID==1002 && p.Map.BaseID==1002 && p.Map.Npcs[Director].Mesh==150;
        }
        private static string Claim(GameState p) {
            if(!GuildLeadership.Leader(p))return "Only the guild's current leader may claim its banner's honor.";
            var guild=p.Guild;
            lock(GuildLeadership.Gate(guild)) {
                if(!Near(p) || !GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,guild))return "Bring your guild's leader to me.";
                if(p.Inventory.Count>=40)return "Make room in your bag for the Lord Token.";
                var now=GuildSeasonWar.Clock();string day=null,season=null;DateTime expiry=DateTime.MinValue;
                var item=Q.New(Token,true);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT name,guildid,mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())if(!r.Read() || r.GetString(0)!=p.Entity.Name || Convert.ToUInt32(r[1])!=guild.ID || Convert.ToUInt32(r[2])!=1002)
                        throw new InvalidOperationException("Guild claim character ownership changed.");
                    using(var q=Q.Command(c,t,"SELECT leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                        if(!Equals(q.ExecuteScalar(),p.Entity.Name))return "The guild's current leader must claim this honor.";
                    using(var q=Q.Command(c,t,@"SELECT b.day,b.season,s.ends_at,s.timezone FROM revora_guild_battles b
JOIN revora_guild_seasons s ON s.id=b.season WHERE b.keeper=@guild AND b.state='won' AND b.captures>0 AND s.ends_at>@now
AND NOT EXISTS(SELECT 1 FROM revora_guild_token_claims c WHERE c.day=b.day) ORDER BY b.day DESC LIMIT 5 FOR UPDATE OF b",
                        "guild",(long)guild.ID,"now",now))using(var r=q.ExecuteReader())while(r.Read()) {
                            if(!ClaimWindow(r.GetString(0),r.GetString(3),now))continue;
                            day=r.GetString(0);season=r.GetString(1);expiry=r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;break;
                        }
                    if(day==null)return "Claim a winning Sunday's Lord Token from Monday at 01:30 until Thursday ends.";
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Guild claim inventory requires reload.");
                    item.Days=35;item.DayStamp=expiry.ToLocalTime().AddDays(-35);item.TimeLeftInMinutes=checked((uint)(expiry-now).TotalSeconds);
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,1,@dur,@max,'35',@stamp)","item",(long)item.UID,"type",(int)Token,"uid",(long)p.Entity.UID,
                        "dur",(int)item.Durability,"max",(int)item.MaximDurability,"stamp",item.DayStamp.ToBinary())!=1)throw new InvalidOperationException("Guild token not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_guild_token_claims(day,season,guild_id,uid,item_uid,claimed_at,expires_at)
VALUES(@day,@season,@guild,@uid,@item,@now,@end)","day",day,"season",season,"guild",(long)guild.ID,"uid",(long)p.Entity.UID,
                        "item",(long)item.UID,"now",now,"end",expiry)!=1)throw new InvalidOperationException("Guild token claim not saved.");
                    if(!Near(p) || !GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,guild))throw new InvalidOperationException("Guild claim session changed.");
                    t.Commit();
                }}
                Q.Install(p,new uint[0],new[]{item});return "Your Lord Token bears this season's seal. Keep it for your guild-war prize.";
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!initialized || p==null || request==null || p.ActiveNpc!=Director || (request.OptionID<80 || request.OptionID>88))return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if(request.NpcID!=0 && request.NpcID!=Director || !Near(p))return true;
                var d=new Npcs(p);d.Avatar(7);
                if(request.OptionID==80) {
                    GuildSeasonPrizes.Clear(p);HolyGrailCrafting.Clear(p);
                    d.Text("A guild that holds the pole earns a Lord Token. Its leader may claim it Monday through Thursday.");
                    d.Option("Claim our Lord Token.",81);d.Option("Collect my war purse.",88);d.Option("Bronze Prize: 1 token.",82);d.Option("Silver Prize: 2 tokens.",83);d.Option("Gold Prize: 3 tokens.",84);d.Option("Forge a Holy Grail.",86);d.Option("Return to guild business.",0);d.Option("Farewell.",255);
                }else if(request.OptionID==81) {GuildSeasonPrizes.Clear(p);HolyGrailCrafting.Clear(p);d.Text(Claim(p));d.Option("Return to guild business.",0);}
                else if(request.OptionID<=85) {HolyGrailCrafting.Clear(p);GuildSeasonPrizes.Menu(p,request.OptionID,d);d.Option("Guild War honors.",80);}
                else if(request.OptionID==88) {GuildSeasonPrizes.Clear(p);HolyGrailCrafting.Clear(p);d.Text(GuildWarPurses.Claim(p));d.Option("Guild War honors.",80);}
                else {GuildSeasonPrizes.Clear(p);HolyGrailCrafting.Menu(p,request.OptionID,d);d.Option("Guild War honors.",80);}
                d.Send();
            }catch(Exception ex){Q.Fail(p,"guild-season-claim",ex);}return true;
        }
    }
}
