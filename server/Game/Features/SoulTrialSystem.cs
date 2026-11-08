using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Q = Revora.Game.Features.QuestInventory;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class SoulTrialSystem
    {
        public const uint NpcId = 900000013;
        public const string NpcName = "Caelan";
        public const ushort TwinCityMap = 1002;
        public const ushort ClaimX = 319;
        public const ushort ClaimY = 295;
        public const ushort Mesh = 50970;
        public const ushort BirdMap = 1015;
        public const ushort BirdX = 717;
        public const ushort BirdY = 571;
        public const ushort ApeMap = 1020;
        public const ushort ApeX = 565;
        public const ushort ApeY = 562;
        public const ushort DesertMap = 1000;
        public const ushort DesertX = 500;
        public const ushort DesertY = 650;

        public const string DoneKey = "soul-trial";
        public const string LifeKey = "soul-trial-life";
        public const string BirdKey = "soul-trial-bird";
        public const string ApeKey = "soul-trial-ape";
        public const string SandKey = "soul-trial-sand";

        private static readonly ConditionalWeakTable<Entity, object> countedDeaths = new ConditionalWeakTable<Entity, object>();

        internal static void InitializeRewards()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_soul_trial_claims(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),
soul_uid bigint NOT NULL UNIQUE,rewards bigint[] NOT NULL CHECK(cardinality(rewards)>=1),
rvp bigint NOT NULL CHECK(rvp>=0),silver bigint NOT NULL CHECK(silver>=0),
target_level smallint NOT NULL CHECK(target_level BETWEEN 1 AND 255),
claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,life))");
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
                        TryTeleport(client, dialog, BirdMap, BirdX, BirdY);
                        return true;
                    case 4:
                        TryTeleport(client, dialog, ApeMap, ApeX, ApeY);
                        return true;
                    case 5:
                        TryTeleport(client, dialog, DesertMap, DesertX, DesertY);
                        return true;
                }
                return true;
            }
            catch (Exception ex) { Q.Fail(client, "soul-trial", ex); return true; }
        }

        private static void RestorePaidLife(GameState client)
        {
            if (AlreadyDone(client)) return;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null,
                    "SELECT EXISTS(SELECT 1 FROM revora_soul_trial_claims WHERE uid=@uid AND life=@life)",
                    "uid", (long)client.Entity.UID, "life", Math.Min(2, (int)client.Entity.Reborn)))
                    if (!Equals(q.ExecuteScalar(), true)) return;
            }
            // The receipt remains authoritative if an older session saved stale
            // legacy variables after the claim. Never offer its reward a second time.
            lock (client.Variables.SyncRoot)
            {
                client[DoneKey] = (uint)client[DoneKey] | LifeBit(client.Entity.Reborn);
                client[LifeKey] = 0u; client[BirdKey] = 0u; client[ApeKey] = 0u; client[SandKey] = 0u;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkCompleted(client, QuestListSystem.SoulTrial);
        }

        internal static void Revived(Entity monster)
        {
            if (monster != null && !monster.Dead) lock (monster) countedDeaths.Remove(monster);
        }

        public static void OnMonsterKill(GameState client, Entity monster)
        {
            if (!Q.Player(client) || client.Variables == null || monster == null) return;
            lock (client.ItemSyncRoot) lock (monster)
            try
            {
                if (!DailySoulBall.WildernessDeath(monster, client.Entity)
                    || Kernel.GetDistance(client.Entity.X, client.Entity.Y, monster.X, monster.Y) > Constants.pScreenDistance
                    || !OnThisLife(client) || AlreadyDone(client)) return;
                object prior;
                if (countedDeaths.TryGetValue(monster, out prior)) return;
                string name = monster.MonsterInfo.Name.ToLowerInvariant();
                if (monster.MapID == BirdMap && StartsWith(name, "birdman"))
                    AddKill(client, BirdKey, Config.SoulTrialBirdKills, "Birdmen");
                else if (monster.MapID == ApeMap && StartsWith(name, "macaque"))
                    AddKill(client, ApeKey, Config.SoulTrialApeKills, "Macaques");
                else if (monster.MapID == DesertMap && StartsWith(name, "sandmonster"))
                    AddKill(client, SandKey, Config.SoulTrialSandKills, "Sand Monsters");
                else return;
                countedDeaths.Add(monster, new object());
            }
            catch (Exception ex) { Q.Fail(client, "soul-trial-progress", ex); }
        }

        public static void OnReborn(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            lock (client.ItemSyncRoot ?? client)
            lock (client.Variables.SyncRoot)
            {
                client[LifeKey] = 0;
                client[BirdKey] = 0;
                client[ApeKey] = 0;
                client[SandKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAvailable(client, QuestListSystem.SoulTrial);
        }

        public static void SyncQuestList(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            if (AlreadyDone(client))
                QuestListSystem.MarkCompleted(client, QuestListSystem.SoulTrial);
            else if (OnThisLife(client))
            {
                QuestListSystem.MarkAccepted(client, QuestListSystem.SoulTrial);
                PushProgress(client);
            }
            else
                QuestListSystem.MarkAvailable(client, QuestListSystem.SoulTrial);
        }

        private static void PushProgress(GameState client)
        {
            QuestListSystem.MarkProgress(client, QuestListSystem.SoulTrial,
                (uint)client[BirdKey], (uint)client[ApeKey], (uint)client[SandKey], 0);
        }

        private static void ShowGreeting(GameState client, Npcs dialog)
        {
            if (AlreadyDone(client))
            {
                dialog.Text("You already finished the Soul Trial in this life, " + client.Entity.Name + ". Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (OnThisLife(client))
            {
                dialog.Text(ProgressText(client));
                if (HuntComplete(client))
                    dialog.Option("Turn in", 2);
                dialog.Option("Bird Island", 3);
                dialog.Option("Ape Mountain", 4);
                dialog.Option("Desert City", 5);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return;
            }
            dialog.Text("I am Caelan, Soul Warden of Twin City. Each life you may hunt once. Finish before you reborn.");
            dialog.Text("Kill " + Config.SoulTrialBirdKills + " Birdmen on Bird Island, " + Config.SoulTrialApeKills + " Macaques on Ape Mountain, and " + Config.SoulTrialSandKills + " Sand Monsters in the Desert. Return for a level puff and a random bound P7 soul for your class.");
            dialog.Option("Accept the trial", 1);
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
                dialog.Text("You already finished the Soul Trial in this life. Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (OnThisLife(client))
            {
                dialog.Text(ProgressText(client));
                dialog.Option("Bird Island", 3);
                dialog.Option("Ape Mountain", 4);
                dialog.Option("Desert City", 5);
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            lock (client.Variables.SyncRoot)
            {
                client[LifeKey] = (uint)(client.Entity.Reborn + 1);
                client[BirdKey] = 0;
                client[ApeKey] = 0;
                client[SandKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAccepted(client, QuestListSystem.SoulTrial);
            PushProgress(client);
            dialog.Text("The trial is yours. Hunt, then return. I can send you to each map.");
            dialog.Option("Bird Island", 3);
            dialog.Option("Ape Mountain", 4);
            dialog.Option("Desert City", 5);
            dialog.Option("I will go.", 255);
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
                dialog.Text("You already finished the Soul Trial in this life. Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (!OnThisLife(client) || !HuntComplete(client))
            {
                dialog.Text(OnThisLife(client) ? ProgressText(client) : "Accept the trial first.");
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
            bool puff = client.Entity.Level < PuffLevel(client.Entity.Reborn);
            int pots = Config.SoulTrialExpPotions;
            if (!puff)
                pots += Config.SoulTrialPuffPotions;
            if (pots > 0 && !ConquerItemInformation.BaseInformations.ContainsKey(Config.SoulTrialExpPotionId))
            {
                dialog.Text("Exp Potions are missing from this world's catalog.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            int need = 1 + pots;
            if (client.Inventory == null || client.Inventory.Count + need > 40)
            {
                dialog.Text("Clear " + need + " inventory spaces first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            long nextRvp = (long)client.Entity.RevoraPoint + Config.SoulTrialRevoraPoints;
            decimal nextGold = (decimal)client.Entity.Money + Config.SoulTrialGold;
            if (nextRvp > uint.MaxValue || nextGold > Entity.MaximumSilver)
            {
                dialog.Text("Spend some of your saved funds, then return.");
                dialog.Option("Alright.", 255); dialog.Send(); return;
            }
            lock (client.Variables.SyncRoot)
            {
                var progress = PlanLevel(client, puff ? PuffLevel(client.Entity.Reborn) : client.Entity.Level);
                var vars = new VariableVault(); vars.Deserialize(client.Variables.Serialize());
                byte[] savedVariables;
                vars[DoneKey] = (uint)client[DoneKey] | LifeBit(client.Entity.Reborn);
                vars[LifeKey] = 0u; vars[BirdKey] = 0u; vars[ApeKey] = 0u; vars[SandKey] = 0u;
                var rewards = new List<ConquerItem> { Q.New(soul, true) };
                for (int i = 0; i < pots; i++) rewards.Add(Q.New(Config.SoulTrialExpPotionId, false));
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SilverBalance.AssertSaved(c, t, client);
                        if (Q.Execute(c, t, @"UPDATE entities SET revorapoint=@rvp,money=@gold
WHERE uid=@uid AND revorapoint=@oldrvp", "rvp", nextRvp, "gold", nextGold,
                            "uid", (long)client.Entity.UID, "oldrvp", (long)client.Entity.RevoraPoint) != 1)
                            throw new InvalidOperationException("Soul Trial purse changed.");
                        CharacterProgression.Persist(c, t, client, progress);
                        foreach (var item in rewards) Q.Insert(c, t, client.Entity.UID, item);
                        savedVariables=EntityVariableTable.PersistRewardVariables(client,c,t,new[]{DoneKey,LifeKey,BirdKey,ApeKey,SandKey},new DynamicVariable[]{vars[DoneKey],vars[LifeKey],vars[BirdKey],vars[ApeKey],vars[SandKey]});
                        if (Q.Execute(c, t, @"INSERT INTO revora_soul_trial_claims(uid,life,soul_uid,rewards,rvp,silver,target_level,claimed_at)
VALUES(@uid,@life,@soul,@items,@rvp,@gold,@level,@now)", "uid", (long)client.Entity.UID,
                            "life", Math.Min(2, (int)client.Entity.Reborn), "soul", (long)rewards[0].UID,
                            "items", rewards.Select(i => (long)i.UID).ToArray(), "rvp", (long)Config.SoulTrialRevoraPoints,
                            "gold", (long)Config.SoulTrialGold, "level", (int)progress.Values[0], "now", DateTime.UtcNow) != 1)
                            throw new InvalidOperationException("Soul Trial receipt was not saved.");
                        t.Commit();
                    }
                }
                client.Variables.ReplaceContents(savedVariables);
                CharacterProgression.ApplyCommitted(client, progress);
                SilverBalance.ApplyCommitted(client, (ulong)nextGold);
                client.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                Q.Install(client, new uint[0], rewards);
                if (puff)
                {
                    client.CalculateStatBonus(); client.CalculateHPBonus(); client.GemAlgorithm();
                    client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                    client.Entity.Mana = (ushort)Math.Min(client.Entity.MaxMana, Math.Max(client.Entity.Mana, client.Entity.Spirit * 5));
                }
            }
            string soulName = SoulName(soul);
            string puffText = puff
                ? " I raised you to level " + client.Entity.Level + "."
                : " You were already at the puff. I gave extra Exp Potions instead.";
            QuestListSystem.MarkCompleted(client, QuestListSystem.SoulTrial);
            dialog.Text("The trial is complete. A " + soulName + " is in your bag." + puffText);
            dialog.Option("Thank you.", 255);
            dialog.Send();
            client.Send(new Message("Caelan paid the Soul Trial: " + soulName + ", " + Config.SoulTrialGold + " gold, " + Config.SoulTrialRevoraPoints + " Revora Points.", Color.Yellow, Message.TopLeft));
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
            if (!OnThisLife(client) || AlreadyDone(client))
            {
                dialog.Text("Accept the trial first.");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            client.Entity.Teleport(map, x, y);
        }

        private static void AddKill(GameState client, string key, uint need, string label)
        {
            uint count = (uint)client[key];
            if (count >= need)
                return;
            count++;
            client[key] = count;
            EntityVariableTable.Save(client);
            PushProgress(client);
            if (count == need || count % 10 == 0)
                client.Send(new Message("Soul Trial: " + label + " " + count + "/" + need + ".", Color.Yellow, Message.TopLeft));
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

        private static uint LifeBit(byte reborn)
        {
            if (reborn > 2)
                reborn = 2;
            return 1u << reborn;
        }

        private static bool HuntComplete(GameState client)
        {
            return (uint)client[BirdKey] >= Config.SoulTrialBirdKills
                && (uint)client[ApeKey] >= Config.SoulTrialApeKills
                && (uint)client[SandKey] >= Config.SoulTrialSandKills;
        }

        private static string ProgressText(GameState client)
        {
            if (HuntComplete(client))
                return "The hunt is done. Turn in for your puff and a bound class P7 soul.";
            return "Birdmen " + (uint)client[BirdKey] + "/" + Config.SoulTrialBirdKills
                + ". Macaques " + (uint)client[ApeKey] + "/" + Config.SoulTrialApeKills
                + ". Sand Monsters " + (uint)client[SandKey] + "/" + Config.SoulTrialSandKills + ".";
        }

        private static byte PuffLevel(byte reborn)
        {
            byte target;
            if (reborn >= 2)
                target = Config.SoulTrialPuff2;
            else if (reborn == 1)
                target = Config.SoulTrialPuff1;
            else
                target = Config.SoulTrialPuff0;
            if (target > Config.LevelCap)
                return Config.LevelCap;
            return target;
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

        private static bool StartsWith(string value, string prefix)
        {
            return value.Length >= prefix.Length && value.StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}
