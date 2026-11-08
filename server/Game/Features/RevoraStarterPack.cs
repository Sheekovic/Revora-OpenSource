#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // The retained item is a handle to character-owned claims, never the entitlement itself.
    internal static class RevoraStarterPack
    {
        internal const uint ItemId = 727990;
        internal const uint OpeningBoundCps = 50000;
        internal const ulong OpeningSilver = 1000000;
        internal const uint FinalBoundCps = 100000;
        internal const ulong FinalSilver = 5000000;
        internal static Action? BeforeCommit = null;
        internal static Action? AfterCommit = null;
        internal static Func<int> SteedColorIndex = RandomSteedColorIndex;
        private static readonly uint[] SteedColors = { 0xff9600, 0x00ff96, 0x9600ff };
        private static bool enabled;

        private static int RandomSteedColorIndex()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var value = new byte[1];
                do { rng.GetBytes(value); } while (value[0] == 255);
                return value[0] % SteedColors.Length;
            }
        }

        internal static ConquerItem SteedReward()
        {
            int index = SteedColorIndex();
            if (index < 0 || index >= SteedColors.Length) throw new InvalidOperationException("Invalid steed color selection.");
            var item = Q.New(300000);
            item.Plus = 12;
            item.Effect = Enums.ItemEffect.Horse;
            item.SocketProgress = SteedColors[index];
            item.NextGreen = (byte)item.SocketProgress;
            item.NextBlue = (byte)(item.SocketProgress >> 8);
            item.NextRed = (byte)(item.SocketProgress >> 16);
            return item; // Steeds have neither blessing nor gem sockets.
        }

        private static uint[] MilestoneIds(int milestone)
        {
            switch (milestone)
            {
                case 40: return new uint[]{300000};
                case 70: return new uint[]{203009};
                case 100: return new uint[]{204009,201009,202009};
                case 120: return new uint[]{P7SoulChoicePack.ItemId};
                default: throw new ArgumentOutOfRangeException("milestone");
            }
        }

        private static ConquerItem[] MilestoneRewards(int milestone)
        {
            if (milestone == 40) return new[]{SteedReward()};
            return MilestoneIds(milestone).Select(id => {
                var item = Q.New(id);
                if (milestone == 120) return item;
                item.Plus = 12;
                if (id == 203009) return item; // Owner policy: crop has no blessing or sockets.
                item.Bless = (byte)(id == 204009 ? 0 : 1);
                item.SocketOne = id == 202009 ? Enums.Gem.SuperGloryGem : Enums.Gem.SuperThunderGem;
                item.SocketTwo = id == 201009 ? Enums.Gem.SuperThunderGem : Enums.Gem.SuperGloryGem;
                return item;
            }).ToArray();
        }

        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    CharacterCreation.Ensure(c, t);
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_custom_starter_items(
uid bigint PRIMARY KEY CHECK(uid>0), item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0));
CREATE TABLE IF NOT EXISTS revora_custom_starter_claims(
uid bigint NOT NULL CHECK(uid>0), reborn integer NOT NULL CHECK(reborn BETWEEN 0 AND 2),
milestone integer NOT NULL CHECK(milestone=1 OR (milestone BETWEEN 5 AND 140 AND milestone%5=0)),
class integer NOT NULL, bound_cps bigint NOT NULL CHECK(bound_cps>=0), silver bigint NOT NULL CHECK(silver>=0),
rewards bigint[] NOT NULL, claimed_at timestamptz NOT NULL, PRIMARY KEY(uid,reborn,milestone));");
                    t.Commit();
                }
            }
            enabled = true;
        }

        private static bool CatalogReady()
        {
            ConquerItemBaseInformation info;
            return ConquerItemInformation.BaseInformations != null
                && ConquerItemInformation.BaseInformations.TryGetValue(ItemId, out info)
                && info.Name == "RevoraStarterPack" && info.StackSize == 1;
        }

        private static bool Ready(GameState p)
        {
            return enabled && CatalogReady() && Q.Player(p) && p.Account != null
                && !string.IsNullOrEmpty(p.Account.Username) && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }

        // Explicit standard base variants: no custom high-level items whose level was lowered.
        internal static uint[]? GearIds(byte profession)
        {
            uint head, armor;
            uint[] weapons;
            switch (profession / 10)
            {
                case 1: head=118009; armor=130009; weapons=new uint[]{410009,420009}; break;
                case 2: head=111009; armor=131009; weapons=new uint[]{410009,900009}; break;
                case 4: head=113009; armor=133009; weapons=new uint[]{500009,1050000}; break;
                case 5: head=123009; armor=135009; weapons=new uint[]{601009,601009}; break;
                case 6: head=143009; armor=136009; weapons=new uint[]{610009,610009}; break;
                case 7: head=144009; armor=139009; weapons=new uint[]{611009,612009}; break;
                case 8: head=148009; armor=138009; weapons=new uint[]{617009,617009}; break;
                case 10: case 13: case 14: head=114009; armor=134009; weapons=new uint[]{421009}; break;
                case 16: head=170009; armor=101009; weapons=new uint[]{626009,626009}; break;
                default: return null;
            }
            bool taoist = IsTaoist(profession);
            return new uint[]{head, armor, taoist?121009u:120009u, taoist?152009u:150009u,160019}
                .Concat(weapons).ToArray();
        }

        private static bool IsTaoist(byte profession)
        {
            return profession / 10 == 10 || profession / 10 == 13 || profession / 10 == 14;
        }

        internal static ConquerItem[]? OpeningGear(byte profession)
        {
            uint[]? ids = GearIds(profession);
            if (ids == null || ids.Any(id => !ConquerItemInformation.BaseInformations.ContainsKey(id))) return null;
            var gems = IsTaoist(profession) ? Enums.Gem.SuperPhoenixGem : Enums.Gem.SuperDragonGem;
            return ids.Select(id => {
                var item = Q.New(id);
                if (id == 1050000) return item; // Ordinary bound ammunition, not socketed equipment.
                item.Plus = 12;
                item.Bless = (byte)(id == 201009 || id == 202009 ? 1 : 7);
                item.SocketOne = item.SocketTwo = id == 201009 ? Enums.Gem.SuperThunderGem
                    : id == 202009 ? Enums.Gem.SuperGloryGem : gems;
                return item;
            }).ToArray();
        }

        private static bool Entitled(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            using (var q = Q.Command(c, t, @"SELECT e.class FROM entities e
JOIN revora_character_creation r ON r.character_uid=e.uid AND r.account_name=lower(e.owner)
WHERE e.uid=@uid AND r.account_name=lower(@owner) AND r.revora_starter_pending FOR UPDATE OF e,r",
                "uid", (long)p.Entity.UID, "owner", p.Account.Username))
            {
                object cls = q.ExecuteScalar();
                return cls != null && cls != DBNull.Value && Convert.ToByte(cls) == p.Entity.Class;
            }
        }

        private static void CheckBag(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            var ids = new List<uint>();
            using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE entityid=@uid AND position=0
AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader())
                while (r.Read())
                {
                    if (ids.Count >= 40) throw new InvalidOperationException("Starter bag exceeds capacity.");
                    ids.Add(checked((uint)r.GetInt64(0)));
                }
            if (!ids.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i)))
                throw new InvalidOperationException("Starter bag changed.");
        }

        internal static void OnLogin(GameState p)
        {
            if (!Ready(p) || !SessionInstall.EnterCurrent(p.Entity.UID, p)) return;
            try
            {
                lock (p.ItemSyncRoot)
                {
                    if (!Ready(p) || p.Inventory.Count >= 40) return;
                    ConquerItem pack;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            if (!Entitled(c,t,p)) return;
                            using (var q = Q.Command(c,t,@"SELECT 1 FROM revora_custom_starter_items s
JOIN items i ON i.uid=s.item_uid WHERE s.uid=@uid AND i.entityid=@uid AND i.id=@type",
                                "uid",(long)p.Entity.UID,"type",(int)ItemId))
                                if (q.ExecuteScalar()!=null) return;
                            CheckBag(c,t,p);
                            pack=Q.New(ItemId);
                            Q.Insert(c,t,p.Entity.UID,pack);
                            if (Q.Execute(c,t,@"INSERT INTO revora_custom_starter_items(uid,item_uid) VALUES(@uid,@item)
ON CONFLICT(uid) DO UPDATE SET item_uid=EXCLUDED.item_uid",
                                "uid",(long)p.Entity.UID,"item",(long)pack.UID)!=1)
                                throw new InvalidOperationException("Starter item receipt failed.");
                            if (!Ready(p) || !SessionInstall.IsCurrent(p.Entity.UID,p)) return;
                            t.Commit();
                        }
                    }
                    Q.Install(p,new uint[0],new[]{pack});
                }
            }
            catch (Exception ex) { Q.Fail(p,"revora-starter-delivery",ex); }
            finally { SessionInstall.Exit(p.Entity.UID); }
        }

        internal static bool TryHandle(GameState p, byte[] packet)
        {
            ConquerItem item;
            if (p==null || p.Inventory==null || packet==null || packet.Length<12
                || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || item.ID!=ItemId) return false;
            if (packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || BitConverter.ToUInt16(packet,2)!=1009 || BitConverter.ToUInt16(packet,20)!=4
                || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal) return true;
            return TryUse(item,p);
        }

        private static void Complete(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ConquerItem item)
        {
            if (Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type
AND bound=1 AND stacksize=1 AND position=0 AND warehouse=0 AND locked=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)ItemId)!=1)
                throw new InvalidOperationException("Final starter pack removal failed.");
            if (Q.Execute(c,t,@"UPDATE revora_character_creation SET revora_starter_pending=false
WHERE character_uid=@uid AND account_name=lower(@owner) AND revora_starter_pending",
                "uid",(long)p.Entity.UID,"owner",p.Account.Username)!=1)
                throw new InvalidOperationException("Final starter entitlement closure failed.");
        }

        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item==null || item.ID!=ItemId) return false;
            if (!Ready(p) || !SessionInstall.EnterCurrent(p.Entity.UID,p)) return true;
            try
            {
                lock (p.ItemSyncRoot)
                {
                    ConquerItem owned;
                    if (!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned)
                        || !item.Bound || item.StackSize!=1 || item.Position!=0 || item.Warehouse!=0
                        || item.InWardrobe || item.Lock!=0 || item.Suspicious || item.Days!=0 || item.TimeLeftInMinutes!=0) return true;
                    bool opening;
                    bool finalClaim=false;
                    int milestone;
                    using (var c=DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t=c.BeginTransaction())
                        {
                            if (!Entitled(c,t,p)) return true;
                            using (var q=Q.Command(c,t,@"SELECT 1 FROM revora_custom_starter_items s JOIN items i ON i.uid=s.item_uid
WHERE s.uid=@uid AND s.item_uid=@item AND i.entityid=@uid AND i.id=@type AND i.bound=1 AND i.stacksize=1
AND i.position=0 AND i.warehouse=0 AND i.locked=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(i.suspicious,0)=0
AND COALESCE(NULLIF(i.days,''),'0')='0'", "uid",(long)p.Entity.UID,"item",(long)item.UID,"type",(int)ItemId))
                                if (q.ExecuteScalar()==null) return true;
                            using (var q=Q.Command(c,t,"SELECT 1 FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=0 AND milestone=1",
                                "uid",(long)p.Entity.UID))
                                opening=q.ExecuteScalar()==null;
                            milestone=opening?1:40;
                            int life=opening?0:p.Entity.Reborn;
                            if (!opening)
                            {
                                if (life>2) return true;
                                using (var q=Q.Command(c,t,"SELECT level,reborn FROM entities WHERE uid=@uid", "uid",(long)p.Entity.UID))
                                using (var r=q.ExecuteReader())
                                    if (!r.Read() || Convert.ToInt32(r[0])!=(int)p.Entity.Level || Convert.ToInt32(r[1])!=life)
                                        throw new InvalidOperationException("Starter progression changed; reload required.");
                                var claimed = new HashSet<int>();
                                using (var q=Q.Command(c,t,"SELECT milestone FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=@life",
                                    "uid",(long)p.Entity.UID,"life",life))
                                using (var r=q.ExecuteReader()) while(r.Read()) claimed.Add(r.GetInt32(0));
                                milestone = (life>0 ? new[]{1} : new[]{40,70,100,120}).FirstOrDefault(level => !claimed.Contains(level));
                                if (milestone==0)
                                { p.Send(life < 2 ? "You have claimed this life's rewards. Keep the pack for your next rebirth." : "You have claimed all starter rewards through your second rebirth."); return true; }
                                if (p.Entity.Level<milestone)
                                { p.Send("Your next starter-pack reward is at level "+milestone+"."); return true; }
                            }
                            bool firstRebirth = !opening && life==1;
                            finalClaim = !opening && life==2;
                            var gearIds=opening || firstRebirth || finalClaim ? GearIds(p.Entity.Class) : MilestoneIds(milestone);
                            if (firstRebirth && gearIds!=null) gearIds=gearIds.Concat(new[]{RebirthGarmentPack.ItemId}).ToArray();
                            if (finalClaim && gearIds!=null) gearIds=gearIds.Concat(new[]{P7SoulChoicePack.ItemId,P7SoulChoicePack.ItemId}).ToArray();
                            if (gearIds==null || gearIds.Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id)))
                            { p.Send("Your starter reward is not available yet."); return true; }
                            if (p.Inventory.Count+gearIds.Length-(finalClaim?1:0)>40) { p.Send("Make room for your starter reward, then open this pack again."); return true; }
                            uint grantCps=opening?OpeningBoundCps:finalClaim?FinalBoundCps:0;
                            ulong grantSilver=opening?OpeningSilver:finalClaim?FinalSilver:0;
                            ulong silver=checked(p.Entity.Money+grantSilver);
                            ulong cps=(ulong)p.Entity.BoundCps+grantCps;
                            if (silver>Entity.MaximumSilver || cps>uint.MaxValue)
                            { p.Send("Make room in your silver or bound CP balance before claiming."); return true; }
                            CheckBag(c,t,p);
                            var rewards=opening || firstRebirth || finalClaim ? OpeningGear(p.Entity.Class) : MilestoneRewards(milestone);
                            if (firstRebirth && rewards!=null) rewards=rewards.Concat(new[]{Q.New(RebirthGarmentPack.ItemId)}).ToArray();
                            if (finalClaim && rewards!=null) rewards=rewards.Concat(new[]{Q.New(P7SoulChoicePack.ItemId),Q.New(P7SoulChoicePack.ItemId)}).ToArray();
                            if (rewards==null) throw new InvalidOperationException("Starter catalog changed.");
                            foreach (var reward in rewards)
                                if (Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,bless,socketone,sockettwo,durability,maximdurability,effect,socketprogress,nextsteedcolor)
VALUES(@item,@type,@uid,0,0,1,1,@plus,@bless,@gem,@gem2,@dur,@dur,@effect,@color,@color)",
                                    "item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                                    // The steed red-preview byte aliases Bless in the wire buffer; it is not a blessing.
                                    "plus",(int)reward.Plus,"bless",reward.ID==300000?0:(int)reward.Bless,"gem",(int)reward.SocketOne,"dur",(int)reward.Durability,
                                    "gem2",(int)reward.SocketTwo,"effect",(int)reward.Effect,"color",(long)reward.SocketProgress)!=1)
                                    throw new InvalidOperationException("Starter gear insert failed.");
                            if (grantCps!=0 && Q.Execute(c,t,"UPDATE entities SET boundcps=@next WHERE uid=@uid AND COALESCE(boundcps,0)=@old",
                                "next",(long)cps,"old",(long)p.Entity.BoundCps,"uid",(long)p.Entity.UID)!=1)
                                throw new InvalidOperationException("Starter bound CP balance changed.");
                            if (grantSilver!=0) SilverBalance.Persist(c,t,p,silver);
                            if (Q.Execute(c,t,@"INSERT INTO revora_custom_starter_claims(uid,reborn,milestone,class,bound_cps,silver,rewards,claimed_at)
VALUES(@uid,@life,@milestone,@class,@cp,@silver,@rewards,@now)", "uid",(long)p.Entity.UID,"class",(int)p.Entity.Class,
                                "life",life,"milestone",milestone,"cp",(long)grantCps,"silver",(long)grantSilver,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",DateTime.UtcNow)!=1)
                                throw new InvalidOperationException("Starter claim receipt failed.");
                            if (finalClaim) Complete(c,t,p,item);
                            if (BeforeCommit!=null) BeforeCommit();
                            if (!Ready(p) || !SessionInstall.IsCurrent(p.Entity.UID,p)) throw new InvalidOperationException("Starter session changed.");
                            t.Commit();
                            if (AfterCommit!=null) AfterCommit();
                            Q.Install(p,finalClaim?new[]{item.UID}:new uint[0],rewards);
                            if (grantCps!=0) p.Entity.ApplyCommittedBoundCps((uint)cps);
                            if (grantSilver!=0) SilverBalance.ApplyCommitted(p,silver);
                        }
                    }
                    p.Send(opening
                        ? "Starter set, 50,000 bound CPs and 1,000,000 silver received. Next reward: a +12 steed at level 40. Equipment keeps its normal requirements."
                        : finalClaim ? "Your final class set, two bound P7 Dragon Soul Choice Packs, 100,000 bound CPs and 5,000,000 silver have arrived. Your RevoraStarterPack is complete and has been consumed."
                        : milestone==1 ? "Your new class equipment and bound Garment Pack have arrived. Your first-rebirth reward is complete. Keep the starter pack for second rebirth."
                        : "Your level "+milestone+" bound starter reward has arrived. Keep the pack for later milestones.");
                }
            }
            catch (Exception ex) { Q.Fail(p,"revora-starter-claim",ex); }
            finally { SessionInstall.Exit(p.Entity.UID); }
            return true;
        }
    }
}
