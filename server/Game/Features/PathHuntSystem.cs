using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Q = Revora.Game.Features.QuestInventory;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class PathHuntSystem
    {
        public const uint NpcId = 900000016;
        public const string NpcName = "Rowan";
        public const ushort TwinCityMap = 1002;
        public const ushort ClaimX = 319;
        public const ushort ClaimY = 299;
        public const ushort Mesh = 50050;
        public const ushort PheasantX = 520;
        public const ushort PheasantY = 490;
        public const ushort RobinX = 320;
        public const ushort RobinY = 700;
        public const ushort DoveX = 450;
        public const ushort DoveY = 630;
        public const ushort ApparitionX = 200;
        public const ushort ApparitionY = 650;

        public const string RewardKey = "path-hunt-valuable";
        public const string DoneKey = "path-hunt";
        public const string LifeKey = "path-hunt-life";
        public const string PheasantKey = "path-hunt-pheasant";
        public const string RobinKey = "path-hunt-robin";
        public const string DoveKey = "path-hunt-dove";
        public const string ApparitionKey = "path-hunt-apparition";

        private static readonly ConditionalWeakTable<Entity, object> countedDeaths = new ConditionalWeakTable<Entity, object>();
        internal static void InitializeRewards()
        {
            using (var c=DataHolder.MySqlConnection) {c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_path_hunt_claims(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),valuable boolean NOT NULL,
soul_uid bigint UNIQUE,rewards bigint[] NOT NULL,rvp bigint NOT NULL CHECK(rvp>=0),silver bigint NOT NULL CHECK(silver>=0),
target_level smallint NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,life),
CHECK((valuable AND soul_uid IS NOT NULL) OR (NOT valuable AND soul_uid IS NULL)))");}
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
            if(ForgottenRealms.HandleNpc(client,request))return true;
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            lock (client.ItemSyncRoot ?? client) try
            {
                if(!Q.Near(client,NpcId,TwinCityMap))return true;
                if(PlanetWeekend.HandleNpc(client,request))return true;
                if(client.Variables==null)return true;
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
                        TryTeleport(client, dialog, TwinCityMap, PheasantX, PheasantY);
                        return true;
                    case 4:
                        TryTeleport(client, dialog, TwinCityMap, RobinX, RobinY);
                        return true;
                    case 5:
                        TryTeleport(client, dialog, TwinCityMap, DoveX, DoveY);
                        return true;
                    case 6:
                        TryTeleport(client, dialog, TwinCityMap, ApparitionX, ApparitionY);
                        return true;
                }
                return true;
            }
            catch(Exception ex){Q.Fail(client,"path-hunt",ex);return true;}
        }

        internal static void Revived(Entity monster) {ForgottenRealms.Revived(monster);if(monster!=null && !monster.Dead)lock(monster)countedDeaths.Remove(monster);}
        public static void OnMonsterKill(GameState client, Entity monster)
        {
            ForgottenRealms.OnDeath(client,monster);
            if(!Q.Player(client) || client.Variables==null || monster==null)return;
            lock(client.ItemSyncRoot)lock(monster)try {
                if(!DailySoulBall.WildernessDeath(monster,client.Entity) || monster.MapID!=TwinCityMap
                    || Kernel.GetDistance(client.Entity.X,client.Entity.Y,monster.X,monster.Y)>Constants.pScreenDistance
                    || !OnThisLife(client) || AlreadyDone(client))return;
                object prior;if(countedDeaths.TryGetValue(monster,out prior))return;
                string name=monster.MonsterInfo.Name.ToLowerInvariant();
                if(name=="pheasant")AddKill(client,PheasantKey,Config.PathHuntPheasantKills,"Pheasants");
                else if(name=="robin")AddKill(client,RobinKey,Config.PathHuntRobinKills,"Robins");
                else if(name=="turtledove")AddKill(client,DoveKey,Config.PathHuntDoveKills,"Turtledoves");
                else if(name=="apparition")AddKill(client,ApparitionKey,Config.PathHuntApparitionKills,"Apparitions");
                else return;
                countedDeaths.Add(monster,new object());
            }catch(Exception ex){Q.Fail(client,"path-hunt-progress",ex);}
        }

        private static void RestorePaidLife(GameState client)
        {
            if(AlreadyDone(client))return;
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var q=Q.Command(c,null,
                "SELECT EXISTS(SELECT 1 FROM revora_path_hunt_claims WHERE uid=@uid AND life=@life)",
                "uid",(long)client.Entity.UID,"life",Math.Min(2,(int)client.Entity.Reborn)))if(!Equals(q.ExecuteScalar(),true))return;}
            lock(client.Variables.SyncRoot) {
                client[DoneKey]=(uint)client[DoneKey]|LifeBit(client.Entity.Reborn);
                client[LifeKey]=0u;client[RewardKey]=0u;client[PheasantKey]=0u;client[RobinKey]=0u;client[DoveKey]=0u;client[ApparitionKey]=0u;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkCompleted(client,QuestListSystem.PathHunt);
        }

        public static void OnReborn(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            lock(client.ItemSyncRoot ?? client)
            lock(client.Variables.SyncRoot)
            {
                client[RewardKey]=0u;
                client[LifeKey] = 0;
                client[PheasantKey] = 0;
                client[RobinKey] = 0;
                client[DoveKey] = 0;
                client[ApparitionKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAvailable(client, QuestListSystem.PathHunt);
        }

        public static void SyncQuestList(GameState client)
        {
            if (client == null || client.Variables == null)
                return;
            if (AlreadyDone(client))
                QuestListSystem.MarkCompleted(client, QuestListSystem.PathHunt);
            else if (OnThisLife(client))
            {
                QuestListSystem.MarkAccepted(client, QuestListSystem.PathHunt);
                PushProgress(client);
            }
            else
                QuestListSystem.MarkAvailable(client, QuestListSystem.PathHunt);
        }

        private static void PushProgress(GameState client)
        {
            QuestListSystem.MarkProgress(client, QuestListSystem.PathHunt,
                (uint)client[PheasantKey], (uint)client[RobinKey], (uint)client[DoveKey], (uint)client[ApparitionKey]);
        }

        private static void ShowGreeting(GameState client, Npcs dialog)
        {
            PlanetWeekend.Offer(dialog);
            ForgottenRealms.Offer(dialog);
            if (AlreadyDone(client))
            {
                dialog.Text("You finished the Path Hunt in this life, " + client.Entity.Name + ". Caelan, four tiles south, has the Soul Trial. Reborn, then return.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (OnThisLife(client))
            {
                dialog.Text(ProgressText(client));
                if (HuntComplete(client))
                    dialog.Option("Turn in", 2);
                HuntTravelOptions(dialog);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return;
            }
            dialog.Text("I am Rowan. Twin City's flocks will raise a new warrior. Hunt " + Config.PathHuntPheasantKills + " Pheasants, " + Config.PathHuntRobinKills + " Robins, " + Config.PathHuntDoveKills + " Turtledoves, and " + Config.PathHuntApparitionKills + " Apparitions, then return. I can send you to each flock.");
            dialog.Option("Accept the hunt", 1);
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
                dialog.Text("You finished the Path Hunt in this life. Caelan, four tiles south, has the Soul Trial.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (OnThisLife(client))
            {
                dialog.Text(ProgressText(client));
                HuntTravelOptions(dialog);
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            lock(client.Variables.SyncRoot)
            {
                client[RewardKey]=1u;
                client[LifeKey] = (uint)(client.Entity.Reborn + 1);
                client[PheasantKey] = 0;
                client[RobinKey] = 0;
                client[DoveKey] = 0;
                client[ApparitionKey] = 0;
                EntityVariableTable.Save(client);
            }
            QuestListSystem.MarkAccepted(client, QuestListSystem.PathHunt);
            PushProgress(client);
            dialog.Text("The hunt is yours. A bound P7 soul awaits your return. I can send you to each flock.");
            HuntTravelOptions(dialog);
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
                dialog.Text("You finished the Path Hunt in this life. Caelan, four tiles south, has the Soul Trial.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (!OnThisLife(client) || !HuntComplete(client))
            {
                dialog.Text(OnThisLife(client) ? ProgressText(client) : "Accept the hunt first.");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            bool puff = client.Entity.Level < PuffLevel(client.Entity.Reborn);
            int pots = Config.PathHuntExpPotions;
            if (!puff)
                pots += Config.PathHuntPuffPotions;
            if (pots > 0 && !ConquerItemInformation.BaseInformations.ContainsKey(Config.PathHuntExpPotionId))
            {
                dialog.Text("Exp Potions are missing from this world's catalog.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            bool valuable=(uint)client[RewardKey]==1;
            int slots=pots+(valuable?1:0);
            if (client.Inventory == null || client.Inventory.Count + slots > 40)
            {
                dialog.Text("Clear " + slots + " inventory spaces first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            long nextRvp=(long)client.Entity.RevoraPoint+Config.PathHuntRevoraPoints;
            decimal nextGold=(decimal)client.Entity.Money+Config.PathHuntGold;
            if(nextRvp>uint.MaxValue || nextGold>Entity.MaximumSilver) {
                dialog.Text("Spend some of your saved funds, then return.");dialog.Option("Alright.",255);dialog.Send();return;
            }
            lock(client.Variables.SyncRoot) {
                var profile=PlanLevel(client,puff?PuffLevel(client.Entity.Reborn):client.Entity.Level);
                var vars=new VariableVault();vars.Deserialize(client.Variables.Serialize());
                byte[] savedVariables;
                vars[DoneKey]=(uint)client[DoneKey]|LifeBit(client.Entity.Reborn);
                vars[LifeKey]=0u;vars[RewardKey]=0u;vars[PheasantKey]=0u;vars[RobinKey]=0u;vars[DoveKey]=0u;vars[ApparitionKey]=0u;
                var rewards=new List<ConquerItem>();if(valuable)rewards.Add(QuestSoulReward.Create());
                for(int i=0;i<pots;i++)rewards.Add(Q.New(Config.PathHuntExpPotionId,false));
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    SilverBalance.AssertSaved(c,t,client);
                    if(Q.Execute(c,t,"UPDATE entities SET revorapoint=@rvp,money=@gold WHERE uid=@uid AND revorapoint=@before",
                        "rvp",nextRvp,"gold",nextGold,"uid",(long)client.Entity.UID,"before",(long)client.Entity.RevoraPoint)!=1)
                        throw new InvalidOperationException("Path Hunt purse changed.");
                    CharacterProgression.Persist(c,t,client,profile);
                    foreach(var item in rewards)Q.Insert(c,t,client.Entity.UID,item);
                    savedVariables=EntityVariableTable.PersistRewardVariables(client,c,t,new[]{DoneKey,LifeKey,RewardKey,PheasantKey,RobinKey,DoveKey,ApparitionKey},new DynamicVariable[]{vars[DoneKey],vars[LifeKey],vars[RewardKey],vars[PheasantKey],vars[RobinKey],vars[DoveKey],vars[ApparitionKey]});
                    if(Q.Execute(c,t,@"INSERT INTO revora_path_hunt_claims(uid,life,valuable,soul_uid,rewards,rvp,silver,target_level,claimed_at)
VALUES(@uid,@life,@valuable,CASE WHEN @valuable THEN @soul ELSE NULL END,@items,@rvp,@gold,@level,@now)",
                        "uid",(long)client.Entity.UID,"life",Math.Min(2,(int)client.Entity.Reborn),"valuable",valuable,
                        "soul",valuable?(long)rewards[0].UID:0L,"items",rewards.Select(i=>(long)i.UID).ToArray(),
                        "rvp",(long)Config.PathHuntRevoraPoints,"gold",(long)Config.PathHuntGold,"level",(int)profile.Values[0],"now",DateTime.UtcNow)!=1)
                        throw new InvalidOperationException("Path Hunt receipt was not saved.");
                    t.Commit();
                }}
                client.Variables.ReplaceContents(savedVariables);CharacterProgression.ApplyCommitted(client,profile);
                SilverBalance.ApplyCommitted(client,(ulong)nextGold);client.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                Q.Install(client,new uint[0],rewards);
                if(puff) {
                    client.CalculateStatBonus();client.CalculateHPBonus();client.GemAlgorithm();client.Entity.Hitpoints=client.Entity.MaxHitpoints;
                    client.Entity.Mana=(ushort)Math.Min(client.Entity.MaxMana,Math.Max(client.Entity.Mana,client.Entity.Spirit*5));
                }
            }
            string puffText = puff
                ? " I raised you to level " + client.Entity.Level + "."
                : " You were already at the puff. I gave extra Exp Potions instead.";
            QuestListSystem.MarkCompleted(client, QuestListSystem.PathHunt);
            dialog.Text("The Path Hunt is complete." + (valuable?" Your bound P7 soul is in your bag.":"") + puffText + " Cerys, four tiles north, pays the courier purse. Caelan, four tiles south, has the Soul Trial.");
            dialog.Option("Thank you.", 255);
            dialog.Send();
            client.Send(new Message("Rowan paid the Path Hunt: " + Config.PathHuntGold + " gold, " + Config.PathHuntRevoraPoints + " Revora Points.", Color.Yellow, Message.TopLeft));
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
                dialog.Text("Accept the hunt first.");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            client.Entity.Teleport(map, x, y);
        }

        private static void HuntTravelOptions(Npcs dialog)
        {
            dialog.Option("Pheasants", 3);
            dialog.Option("Robins", 4);
            dialog.Option("Turtledoves", 5);
            dialog.Option("Apparitions", 6);
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
            if (count == need || count % 5 == 0)
                client.Send(new Message("Path Hunt: " + label + " " + count + "/" + need + ".", Color.Yellow, Message.TopLeft));
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
            return (uint)client[PheasantKey] >= Config.PathHuntPheasantKills
                && (uint)client[RobinKey] >= Config.PathHuntRobinKills
                && (uint)client[DoveKey] >= Config.PathHuntDoveKills
                && (uint)client[ApparitionKey] >= Config.PathHuntApparitionKills;
        }

        private static string ProgressText(GameState client)
        {
            if (HuntComplete(client))
                return "The hunt is done. Turn in for your puff.";
            return "Pheasants " + (uint)client[PheasantKey] + "/" + Config.PathHuntPheasantKills
                + ". Robins " + (uint)client[RobinKey] + "/" + Config.PathHuntRobinKills
                + ". Turtledoves " + (uint)client[DoveKey] + "/" + Config.PathHuntDoveKills
                + ". Apparitions " + (uint)client[ApparitionKey] + "/" + Config.PathHuntApparitionKills + ".";
        }

        private static byte PuffLevel(byte reborn)
        {
            byte target;
            if (reborn >= 2)
                target = Config.PathHuntPuff2;
            else if (reborn == 1)
                target = Config.PathHuntPuff1;
            else
                target = Config.PathHuntPuff0;
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


    }
}
