using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildSeasonPrizes
    {
        private sealed class Token {internal ConquerItem Item;internal uint Uid;internal string Day;internal DateTime Stamp;}
        private sealed class Offer {internal Guid Id;internal int Tier;internal string Season;internal DateTime Until,Expiry;internal List<Token> Tokens;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static readonly uint[] types={0,2100055,2100065,2100075},cps={0,3000,6000,20000};
        private static readonly string[] names={"","Bronze Prize","Silver Prize","Gold Prize"};
        internal static void Initialize() {
            foreach(uint id in types.Skip(1))if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Guild prize catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_prizes(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),season varchar(10) NOT NULL REFERENCES revora_guild_seasons(id),
tier integer NOT NULL CHECK(tier BETWEEN 1 AND 3),item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),
cps integer NOT NULL CHECK(cps IN(3000,6000,20000)),created_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,
rolled_at timestamptz,trophy_uid bigint UNIQUE);
CREATE TABLE IF NOT EXISTS revora_guild_prize_tokens(
prize uuid NOT NULL REFERENCES revora_guild_prizes(id),day varchar(10) NOT NULL UNIQUE REFERENCES revora_guild_token_claims(day),
item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),PRIMARY KEY(prize,day));");}
        }
        private static bool Usable(ConquerItem i) {
            return i!=null && i.ID==GuildSeasonClaims.Token && i.Position==0 && i.Warehouse==0 && i.StackSize==1 && i.Bound
                && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==35;
        }
        private static bool Same(GameState p,Token token,DateTime expiry) {
            ConquerItem item;return p.Inventory.TryGetItem(token.Uid,out item) && ReferenceEquals(item,token.Item) && item.UID==token.Uid && Usable(item)
                && item.DayStamp==token.Stamp && item.DayStamp.AddDays(35).ToUniversalTime()==expiry;
        }
        internal static void Clear(GameState p){offers.Remove(p);}
        private static void Prepare(GameState p,int tier,Npcs d) {
            Clear(p);var now=GuildSeasonWar.Clock();var selected=new List<Token>();string season=null;DateTime expiry=DateTime.MinValue;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT c.day,c.season,c.item_uid,c.expires_at
FROM revora_guild_token_claims c JOIN revora_guild_seasons s ON s.id=c.season
WHERE c.uid=@uid AND c.spent_at IS NULL AND c.claimed_at<=@now AND c.expires_at=s.ends_at AND s.ends_at>@now ORDER BY c.day",
                "uid",(long)p.Entity.UID,"now",now))using(var r=q.ExecuteReader())while(r.Read()) {
                    ConquerItem item;uint uid=Convert.ToUInt32(r[2]);var end=r.GetFieldValue<DateTimeOffset>(3).UtcDateTime;
                    if(!p.Inventory.TryGetItem(uid,out item) || !Usable(item) || item.DayStamp.AddDays(35).ToUniversalTime()!=end)continue;
                    if(season!=null && season!=r.GetString(1))continue;
                    season=r.GetString(1);expiry=end;selected.Add(new Token{Item=item,Uid=uid,Day=r.GetString(0),Stamp=item.DayStamp});if(selected.Count==tier)break;
                }}
            if(selected.Count!=tier){d.Text("Bring "+tier+" earned Lord Token"+(tier==1?"":"s")+" from this season.");return;}
            if(p.Inventory.Count-selected.Count>=40){d.Text("Make room for your prize.");return;}
            offers.Add(p,new Offer{Id=Guid.NewGuid(),Tier=tier,Season=season,Expiry=expiry,Until=now.AddMinutes(2),Tokens=selected});
            d.Text("Exchange "+tier+" Lord Token"+(tier==1?"":"s")+" for a bound "+names[tier]+" and "+cps[tier].ToString("N0",CultureInfo.InvariantCulture)+" CPs?");
            if(tier==3)d.Text("When the next season begins, your Gold Prize becomes a permanent Gold Trophy.");
            d.Option("Claim my prize.",85);d.Option("Not yet.",80);
        }
        private static void Redeem(GameState p,Npcs d) {
            Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Choose the prize you seek first.");return;}Clear(p);
            var now=GuildSeasonWar.Clock();
            if(now>=offer.Until || now>=offer.Expiry || !offer.Tokens.All(i=>Same(p,i,offer.Expiry))){d.Text("Show me your Lord Tokens again.");return;}
            if(p.Inventory.Count-offer.Tokens.Count>=40){d.Text("Make room for your prize.");return;}
            uint before=p.Entity.ConquerPoints;ulong next=(ulong)before+cps[offer.Tier];
            if(next>uint.MaxValue){d.Text("Make room in your CP purse first.");return;}
            var item=Q.New(types[offer.Tier],true);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT conquerpoints,mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt64(r[0])!=before || Convert.ToUInt32(r[1])!=1002)throw new InvalidOperationException("Guild prize owner changed.");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Guild prize inventory requires reload.");
                foreach(var token in offer.Tokens.OrderBy(i=>i.Uid)) {
                    if(Q.Execute(c,t,@"UPDATE revora_guild_token_claims SET spent_at=@now WHERE day=@day AND season=@season AND uid=@uid
AND item_uid=@item AND spent_at IS NULL AND claimed_at<=@now AND expires_at=@end AND expires_at>@now",
                        "now",now,"day",token.Day,"season",offer.Season,"uid",(long)p.Entity.UID,"item",(long)token.Uid,"end",offer.Expiry)!=1)
                        throw new InvalidOperationException("Guild token receipt changed.");
                    if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND id=723467 AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=1 AND bound=1
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND days='35' AND daystamp=@stamp",
                        "item",(long)token.Uid,"uid",(long)p.Entity.UID,"stamp",token.Stamp.ToBinary())!=1)throw new InvalidOperationException("Guild token ownership changed.");
                }
                if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,1,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                    "dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)throw new InvalidOperationException("Guild prize not saved.");
                if(Q.Execute(c,t,@"INSERT INTO revora_guild_prizes(id,uid,season,tier,item_uid,cps,created_at,ends_at)
VALUES(@id,@uid,@season,@tier,@item,@cps,@now,@end)","id",offer.Id,"uid",(long)p.Entity.UID,"season",offer.Season,"tier",offer.Tier,
                    "item",(long)item.UID,"cps",(int)cps[offer.Tier],"now",now,"end",offer.Expiry)!=1)throw new InvalidOperationException("Guild prize receipt not saved.");
                foreach(var token in offer.Tokens)
                    if(Q.Execute(c,t,"INSERT INTO revora_guild_prize_tokens(prize,day,item_uid) VALUES(@prize,@day,@item)",
                        "prize",offer.Id,"day",token.Day,"item",(long)token.Uid)!=1)throw new InvalidOperationException("Guild prize material receipt not saved.");
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before",
                    "after",(long)next,"uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Guild prize CPs not saved.");
                if(!GuildSeasonClaims.Near(p) || p.Entity.ConquerPoints!=before || !offer.Tokens.All(i=>Same(p,i,offer.Expiry)))throw new InvalidOperationException("Guild prize session changed.");
                t.Commit();
            }}
            p.Entity.ApplyCommittedConquerPoints((uint)next);Q.Install(p,offer.Tokens.Select(i=>i.Uid),new[]{item});d.Text("Your "+names[offer.Tier]+" and CPs are yours. Carry your guild's honor well.");
        }
        internal static void Menu(GameState p,byte option,Npcs d) {
            if(option>=82 && option<=84)Prepare(p,option-81,d);else if(option==85)Redeem(p,d);
        }
    }
}
