using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using Q = Revora.Game.Features.QuestInventory;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class CourierLoopSystem
    {
        public const uint NpcId = 900000017;
        public const string NpcName = "Cerys";
        public const ushort TwinCityMap = 1002;
        public const ushort ClaimX = 319;
        public const ushort ClaimY = 303;
        public const ushort Mesh = 50350;
        public const ushort PhoenixMap = 1011;
        // Loaded 1011 terrain: clear landing connected to the Bandit fields.
        public const ushort PhoenixX = 112;
        public const ushort PhoenixY = 272;
        public const byte StageAccept = 1;
        public const byte StageHunt = 2;
        public const byte StageClerk = 3;
        public const byte StageTurnIn = 4;
        public const byte CommanderOption = 10;
        public const byte ClerkOption = 10;

        public const string DoneKey = "courier-loop";
        public const string LifeKey = "courier-loop-life";
        public const string StageKey = "courier-loop-stage";
        public const string BanditKey = "courier-loop-bandit";

        private static readonly ConditionalWeakTable<Entity, object> countedDeaths = new ConditionalWeakTable<Entity, object>();
        internal static void InitializeRewards()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_courier_claims(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),soul_uid bigint NOT NULL UNIQUE,
cp bigint NOT NULL CHECK(cp>=0),rvp bigint NOT NULL CHECK(rvp>=0),silver bigint NOT NULL CHECK(silver>=0),
vip_level smallint NOT NULL,vip_days smallint NOT NULL,vip_level_days smallint NOT NULL,
target_level smallint NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,life))");
            }
        }
        public static void Create()
        {
            InitializeRewards();
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, ClaimX, ClaimY, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            else
                Console.WriteLine(NpcName + " spawn skipped: map 1002 not ready");
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            lock (client.ItemSyncRoot ?? client)
            try
            {
                if (!Q.Near(client, NpcId, TwinCityMap) || client.Variables == null) return true;
                RestorePaidLife(client);
                Npcs dialog = new Npcs(client);
                switch (request.OptionID)
                {
                    case 0:
                        ShowGreeting(client, dialog);
                        return true;
                    case 1:
                        Accept(client, dialog);
                        return true;
                    case 2:
                        TurnIn(client, dialog);
                        return true;
                    case 3:
                        TryTeleport(client, dialog, TwinCityMap, CityGuardSystem.CommanderX, CityGuardSystem.CommanderY);
                        return true;
                    case 4:
                        TryTeleport(client, dialog, PhoenixMap, PhoenixX, PhoenixY);
                        return true;
                    case 5:
                        TryTeleport(client, dialog, TwinCityMap, CpShopClerkSystem.X, CpShopClerkSystem.Y);
                        return true;
                }
                return true;
            }
            catch (Exception ex) { Q.Fail(client, "courier-loop", ex); return true; }
        }

        public static bool AwaitingCommander(GameState client)
        {
            return OnThisLife(client) && !AlreadyDone(client) && Stage(client) == StageAccept;
        }

        public static bool AwaitingClerk(GameState client)
        {
            return OnThisLife(client) && !AlreadyDone(client) && Stage(client) == StageClerk;
        }

        public static void OfferCommander(Npcs dialog)
        {
            dialog.Option("Cerys's satchel", CommanderOption);
        }

        public static void OfferClerk(Npcs dialog)
        {
            dialog.Option("Cerys's satchel", ClerkOption);
        }

        public static bool HandleCommander(GameState client, Npcs dialog)
        {
            if (client == null || client.Entity == null)
                return false;
            lock (client.ItemSyncRoot ?? client)
            try
            {
                if (!Q.Near(client, CityGuardSystem.CommanderNpcId, TwinCityMap) || client.Variables == null) return true;
                RestorePaidLife(client);
                if (client.Entity.Dead)
                {
                    dialog.Text("Rise first.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                }
                if (!AwaitingCommander(client))
                {
                    dialog.Text("I have no report from Cerys.");
                    dialog.Option("Understood.", 255);
                    dialog.Send();
                    return true;
                }
                client[StageKey] = StageHunt;
                EntityVariableTable.Save(client);
                QuestListSystem.MarkProgress(client, QuestListSystem.CourierLoop, 1, 0, 0, 0);
                dialog.Text("Cerys's satchel was cut on the Phoenix Castle road. Hunt " + Config.CourierLoopBanditKills + " Bandits there, then show PurseClerk. Cerys can send you.");
                dialog.Option("Understood.", 255);
                dialog.Send();
                return true;
            }
            catch (Exception ex) { Q.Fail(client, "courier-loop", ex); return true; }
        }

        public static bool HandleClerk(GameState client, Npcs dialog)
        {
            if (client == null || client.Entity == null)
                return false;
            lock (client.ItemSyncRoot ?? client)
            try
            {
                if (!Q.Near(client, CpShopClerkSystem.NpcId, TwinCityMap) || client.Variables == null) return true;
                RestorePaidLife(client);
                if (client.Entity.Dead)
                {
                    dialog.Text("Rise first.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                }
                if (!AwaitingClerk(client))
                {
                    dialog.Text("I have no satchel from Cerys.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                }
                client[StageKey] = StageTurnIn;
                EntityVariableTable.Save(client);
                QuestListSystem.MarkProgress(client, QuestListSystem.CourierLoop, 1, Config.CourierLoopBanditKills, 1, 0);
                dialog.Text("The purse is noted. I do not pay here. Return to Cerys at 319,303. She holds the recovered purse.");
                dialog.Option("I will go.", 255);
                dialog.Send();
                return true;
            }
            catch (Exception ex) { Q.Fail(client, "courier-loop", ex); return true; }
        }

        internal static void Revived(Entity monster) {if(monster!=null && !monster.Dead)lock(monster)countedDeaths.Remove(monster);}
        public static void OnMonsterKill(GameState client, Entity monster)
        {
            if(!Q.Player(client) || client.Variables==null || monster==null)return;
            lock(client.ItemSyncRoot)lock(monster)try {
                if(!DailySoulBall.WildernessDeath(monster,client.Entity) || monster.MapID!=PhoenixMap
                    || Kernel.GetDistance(client.Entity.X,client.Entity.Y,monster.X,monster.Y)>Constants.pScreenDistance
                    || !OnThisLife(client) || AlreadyDone(client) || Stage(client)!=StageHunt
                    || !monster.MonsterInfo.Name.StartsWith("bandit",StringComparison.OrdinalIgnoreCase))return;
                object prior;if(countedDeaths.TryGetValue(monster,out prior))return;
                uint need=Config.CourierLoopBanditKills,count=(uint)client[BanditKey];if(count>=need)return;
                lock(client.Variables.SyncRoot) {
                    client[BanditKey]=++count;if(count==need)client[StageKey]=StageClerk;
                    EntityVariableTable.Save(client);
                }
                countedDeaths.Add(monster,new object());
                if(count==need || count%5==0)client.Send(new Message("Courier Loop: Bandits "+count+"/"+need+".",Color.Yellow,Message.TopLeft));
                PushProgress(client);
            }catch(Exception ex){Q.Fail(client,"courier-progress",ex);}
        }

        private static void RestorePaidLife(GameState client)
        {
            if(AlreadyDone(client))return;
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var q=Q.Command(c,null,
                "SELECT EXISTS(SELECT 1 FROM revora_courier_claims WHERE uid=@uid AND life=@life)",
                "uid",(long)client.Entity.UID,"life",Math.Min(2,(int)client.Entity.Reborn)))if(!Equals(q.ExecuteScalar(),true))return;}
            lock(client.Variables.SyncRoot) {
                client[DoneKey]=(uint)client[DoneKey]|LifeBit(client.Entity.Reborn);client[LifeKey]=0u;client[StageKey]=0u;client[BanditKey]=0u;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkCompleted(client,QuestListSystem.CourierLoop);
        }

        public static void OnReborn(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            lock (client.ItemSyncRoot ?? client)
            lock (client.Variables.SyncRoot)
            {
                client[LifeKey] = 0;
                client[StageKey] = 0;
                client[BanditKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAvailable(client, QuestListSystem.CourierLoop);
        }

        public static void SyncQuestList(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            if (AlreadyDone(client))
                QuestListSystem.MarkCompleted(client, QuestListSystem.CourierLoop);
            else if (OnThisLife(client) && Stage(client) != 0)
            {
                QuestListSystem.MarkAccepted(client, QuestListSystem.CourierLoop);
                PushProgress(client);
            }
            else
                QuestListSystem.MarkAvailable(client, QuestListSystem.CourierLoop);
        }

        private static void PushProgress(GameState client)
        {
            byte stage = Stage(client);
            uint commander = stage >= StageHunt ? 1u : 0u;
            uint bandits = (uint)client[BanditKey];
            uint clerk = stage >= StageTurnIn ? 1u : 0u;
            QuestListSystem.MarkProgress(client, QuestListSystem.CourierLoop, commander, bandits, clerk, 0);
        }

        private static void ShowGreeting(GameState client, Npcs dialog)
        {
            if (AlreadyDone(client))
            {
                dialog.Text("You closed the loop in this life, " + client.Entity.Name + ". Reborn, then I will send you again.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            byte stage = Stage(client);
            if (OnThisLife(client) && stage != 0)
            {
                dialog.Text(ProgressText(client));
                if (stage == StageTurnIn)
                    dialog.Option("Turn in", 2);
                TravelOptions(dialog, stage);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return;
            }
            dialog.Text("I am Cerys. I run the Twin City satchel. Last run, Bandits cut it open on the Phoenix Castle road. Report the theft to the City Commander, hunt " + Config.CourierLoopBanditKills + " Bandits there, show PurseClerk, then return. The city pays the recovered purse: " + Config.CourierLoopCp + " CPs, " + Config.CourierLoopGold + " gold, " + Config.CourierLoopRevoraPoints + " Revora Points, a bound class P7 soul, seven days of VIP " + Config.CourierLoopVipLevel + ", and a puff to " + Config.CourierLoopPuff + ".");
            dialog.Option("I will close the loop", 1);
            dialog.Option("Goodbye", 255);
            dialog.Send();
        }

        private static void Accept(GameState client, Npcs dialog)
        {
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (AlreadyDone(client))
            {
                dialog.Text("You closed the loop in this life. Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (OnThisLife(client) && Stage(client) != 0)
            {
                dialog.Text(ProgressText(client));
                TravelOptions(dialog, Stage(client));
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            lock (client.Variables.SyncRoot)
            {
                client[LifeKey] = (uint)(client.Entity.Reborn + 1);
                client[StageKey] = StageAccept;
                client[BanditKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAccepted(client, QuestListSystem.CourierLoop);
            PushProgress(client);
            dialog.Text("Start with the City Commander at 442,339. Tell him the satchel was cut. I can send you.");
            dialog.Option("City Commander", 3);
            dialog.Option("I will walk.", 255);
            dialog.Send();
        }

        private static void TurnIn(GameState client, Npcs dialog)
        {
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (AlreadyDone(client))
            {
                dialog.Text("You closed the loop in this life. Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (!OnThisLife(client) || Stage(client) != StageTurnIn)
            {
                dialog.Text(OnThisLife(client) ? ProgressText(client) : "Accept the loop first.");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            uint soul = PickClassSoul(client.Entity.Class);
            if (soul == 0)
            {
                dialog.Text("I have no P7 soul for your class in this world's catalog.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (client.Inventory == null || client.Inventory.Count + 1 > 40)
            {
                dialog.Text("Clear one inventory space first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            bool puff = client.Entity.Level < Config.CourierLoopPuff;
            string vipNote = VipNote(client);
            long nextCp = (long)client.Entity.ConquerPoints + Config.CourierLoopCp;
            long nextRvp = (long)client.Entity.RevoraPoint + Config.CourierLoopRevoraPoints;
            decimal nextGold = (decimal)client.Entity.Money + Config.CourierLoopGold;
            if (nextCp > uint.MaxValue || nextRvp > uint.MaxValue || nextGold > Entity.MaximumSilver) {
                dialog.Text("Spend some of your saved funds, then return.");dialog.Option("Alright.",255);dialog.Send();return;
            }
            byte vip=client.Entity.VIPLevel,days=client.Entity.VIPDays,levelDays=client.Entity.VIPLevelDays;
            if (vip < Config.CourierLoopVipLevel || (vip == Config.CourierLoopVipLevel && days != 0)) {
                vip=Config.CourierLoopVipLevel;days=Math.Max(days,Config.CourierLoopVipDays);levelDays=Config.CourierLoopVipDays;
            }
            lock (client.Variables.SyncRoot) {
                var profile=PlanLevel(client,puff?Config.CourierLoopPuff:client.Entity.Level);
                var vars=new VariableVault();vars.Deserialize(client.Variables.Serialize());
                byte[] savedVariables;
                vars[DoneKey]=(uint)client[DoneKey]|LifeBit(client.Entity.Reborn);vars[LifeKey]=0u;vars[StageKey]=0u;vars[BanditKey]=0u;
                var item=Q.New(soul,true);
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    SilverBalance.AssertSaved(c,t,client);
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold
WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp", "cp",nextCp,"rvp",nextRvp,"gold",nextGold,
                        "uid",(long)client.Entity.UID,"oldcp",(long)client.Entity.ConquerPoints,"oldrvp",(long)client.Entity.RevoraPoint)!=1)
                        throw new InvalidOperationException("Courier purse changed.");
                    CharacterProgression.Persist(c,t,client,profile);
                    VipState.Persist(c,t,client,vip,days,levelDays);
                    Q.Insert(c,t,client.Entity.UID,item);
                    savedVariables=EntityVariableTable.PersistRewardVariables(client,c,t,new[]{DoneKey,LifeKey,StageKey,BanditKey},new DynamicVariable[]{vars[DoneKey],vars[LifeKey],vars[StageKey],vars[BanditKey]});
                    if(Q.Execute(c,t,@"INSERT INTO revora_courier_claims(uid,life,soul_uid,cp,rvp,silver,vip_level,vip_days,vip_level_days,target_level,claimed_at)
VALUES(@uid,@life,@soul,@cp,@rvp,@gold,@vip,@days,@leveldays,@level,@now)","uid",(long)client.Entity.UID,
                        "life",Math.Min(2,(int)client.Entity.Reborn),"soul",(long)item.UID,"cp",(long)Config.CourierLoopCp,
                        "rvp",(long)Config.CourierLoopRevoraPoints,"gold",(long)Config.CourierLoopGold,"vip",(int)vip,
                        "days",(int)days,"leveldays",(int)levelDays,"level",(int)profile.Values[0],"now",DateTime.UtcNow)!=1)
                        throw new InvalidOperationException("Courier receipt was not saved.");
                    t.Commit();
                }}
                client.Variables.ReplaceContents(savedVariables);
                CharacterProgression.ApplyCommitted(client,profile);
                client.Entity.ApplyCommittedConquerPoints((uint)nextCp);client.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                SilverBalance.ApplyCommitted(client,(ulong)nextGold);VipState.ApplyCommitted(client,vip,days,levelDays);
                Q.Install(client,new uint[0],new[]{item});client.Send(new VipStatus().ToArray());
                if(puff) {
                    client.CalculateStatBonus();client.CalculateHPBonus();client.GemAlgorithm();client.Entity.Hitpoints=client.Entity.MaxHitpoints;
                    client.Entity.Mana=(ushort)Math.Min(client.Entity.MaxMana,Math.Max(client.Entity.Mana,client.Entity.Spirit*5));
                }
            }
            string soulName = SoulName(soul);
            string puffText = puff
                ? " I raised you to level " + client.Entity.Level + "."
                : " You were already at " + Config.CourierLoopPuff + ".";
            QuestListSystem.MarkCompleted(client, QuestListSystem.CourierLoop);
            dialog.Text("The loop is closed. A " + soulName + " is in your bag." + puffText + " " + vipNote);
            dialog.Option("Thank you.", 255);
            dialog.Send();
            client.Send(new Message("Cerys paid the Courier Loop: " + Config.CourierLoopCp + " CPs, " + Config.CourierLoopGold + " gold, " + Config.CourierLoopRevoraPoints + " Revora Points, " + soulName + ".", Color.Yellow, Message.TopLeft));
        }

        private static void TryTeleport(GameState client, Npcs dialog, ushort map, ushort x, ushort y)
        {
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (!OnThisLife(client) || AlreadyDone(client) || Stage(client) == 0)
            {
                dialog.Text("Accept the loop first.");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            client.Entity.Teleport(map, x, y);
        }

        private static void TravelOptions(Npcs dialog, byte stage)
        {
            if (stage == StageAccept)
                dialog.Option("City Commander", 3);
            if (stage == StageHunt)
                dialog.Option("Phoenix Castle Bandits", 4);
            if (stage == StageClerk)
                dialog.Option("PurseClerk", 5);
        }

        private static string ProgressText(GameState client)
        {
            byte stage = Stage(client);
            uint need = Config.CourierLoopBanditKills;
            uint have = (uint)client[BanditKey];
            if (stage == StageAccept)
                return "Report the cut satchel to the City Commander at 442,339.";
            if (stage == StageHunt)
                return "Hunt Bandits in Phoenix Castle. " + have + "/" + need + ".";
            if (stage == StageClerk)
                return "The Bandits are down. Show PurseClerk at 418,330.";
            if (stage == StageTurnIn)
                return "PurseClerk noted the satchel. Turn in for the recovered purse.";
            return "Accept the loop first.";
        }

        private static string VipNote(GameState client)
        {
            byte want = Config.CourierLoopVipLevel;
            if (client.Entity.VIPLevel > want)
                return "You already hold a higher VIP. I left it.";
            if (client.Entity.VIPLevel == want && client.Entity.VIPDays == 0)
                return "You already hold lifetime VIP " + want + ".";
            return "VIP " + want + " for " + Config.CourierLoopVipDays + " days is yours.";
        }

        private static bool AlreadyDone(GameState client)
        {
            if (client.Variables == null)
                return false;
            return ((uint)client[DoneKey] & LifeBit(client.Entity.Reborn)) != 0;
        }

        private static bool OnThisLife(GameState client)
        {
            if (client.Variables == null)
                return false;
            return (uint)client[LifeKey] == (uint)(client.Entity.Reborn + 1);
        }

        private static byte Stage(GameState client)
        {
            if (client.Variables == null)
                return 0;
            uint value = (uint)client[StageKey];
            if (value > 255)
                return 0;
            return (byte)value;
        }

        private static uint LifeBit(byte reborn)
        {
            if (reborn > 2)
                reborn = 2;
            return 1u << reborn;
        }

        private static CharacterProgression.Profile PlanLevel(GameState client, byte target)
        {
            var before = CharacterProgression.Capture(client);
            var values = (long[])before.Values.Clone();
            if (values[0] >= target) return before;
            if (client.Entity.Reborn == 0 && target <= 120)
            {
                byte statsClass = client.Entity.Class;
                if (statsClass / 10 == 8) statsClass = 20;
                var stats = DataHolder.StatsFor(statsClass, target);
                CharacterProgression.AssignBaseStats(values, stats, statsClass, (byte)values[0]);
            }
            else values[6] = Math.Min(ushort.MaxValue, values[6] + (target - values[0]) * Config.AttributePointsPerLevel);
            values[0] = target; values[1] = 0;
            return new CharacterProgression.Profile(values);
        }

        private static uint PickClassSoul(byte cls)
        {
            List<uint> pool = new List<uint>(12);
            bool taoist = cls / 10 == 10 || cls / 10 == 13 || cls / 10 == 14;
            if (taoist)
            {
                Offer(pool, 820076);
                Offer(pool, 822072);
                Offer(pool, 821034);
                Offer(pool, 823060);
                Offer(pool, 824020);
            }
            else
            {
                Offer(pool, 820073);
                Offer(pool, 822071);
                Offer(pool, 821033);
                Offer(pool, 823058);
                Offer(pool, 823059);
                Offer(pool, 823061);
                Offer(pool, 823062);
                Offer(pool, 824018);
                Offer(pool, 824019);
            }
            switch (cls / 10)
            {
                case 1:
                    Offer(pool, 800020);
                    break;
                case 2:
                    Offer(pool, 800111);
                    Offer(pool, 800422);
                    break;
                case 4:
                    Offer(pool, 800618);
                    break;
                case 5:
                    Offer(pool, 800142);
                    Offer(pool, 800917);
                    break;
                case 6:
                    Offer(pool, 800725);
                    break;
                case 7:
                    Offer(pool, 800810);
                    Offer(pool, 800811);
                    break;
                case 8:
                    Offer(pool, 801004);
                    break;
                case 10:
                case 13:
                    Offer(pool, 800522);
                    Offer(pool, 801104);
                    break;
                case 14:
                    Offer(pool, 800215);
                    Offer(pool, 800255);
                    Offer(pool, 800522);
                    Offer(pool, 801104);
                    break;
                case 16:
                    Offer(pool, 801308);
                    break;
            }
            if (pool.Count == 0)
                return 0;
            return pool[Kernel.Random.Next(0, pool.Count)];
        }

        private static void Offer(List<uint> pool, uint id)
        {
            ConquerItemBaseInformation item;
            if (ConquerItemInformation.BaseInformations.TryGetValue(id, out item) && item.PurificationLevel == 7)
                pool.Add(id);
        }

        private static string SoulName(uint id)
        {
            ConquerItemBaseInformation info;
            if (ConquerItemInformation.BaseInformations.TryGetValue(id, out info) && info != null && !string.IsNullOrEmpty(info.Name))
                return "bound P7 " + info.Name;
            return "bound P7 soul";
        }
    }
}
