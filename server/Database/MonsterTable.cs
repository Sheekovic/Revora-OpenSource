using System;
using Revora.Game;
using Revora.Database;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class MonsterInformation
    {
        #region Definitions
        public static SafeDictionary<uint, MonsterInformation> MonsterInformations = new SafeDictionary<uint, MonsterInformation>(8000);
        public Game.Entity Owner;
        public Time32 Lastpop;
        public uint ExcludeFromSend = 0;
        private bool LabirinthDrop = false;
        public bool Guard, Reviver;
        public uint ID;
        public ushort Mesh;
        public byte Level;
        public uint Type;
        public string Name;
        public uint Hitpoints;
        public ushort Defence;
        public ushort ViewRange;
        public ushort AttackRange;
        public int RespawnTime;
        public uint MinAttack, MaxAttack;
        public byte AttackType;
        public ushort SpellID;
        public uint InSight;
        public bool Boss;
        public static bool IsSourceBossName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.EndsWith("King", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("King1", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("MonsterKin", StringComparison.OrdinalIgnoreCase))
                return true;
            switch (name)
            {
                case "HorrifyingSpecter":
                case "PirateKing":
                case "Cerberus":
                case "BlackTurtle":
                case "AzureBird":
                case "ImmortalSoldier":
                case "WhiteDragon":
                case "SilverHawk":
                case "SunDevil":
                case "GreatPheasant(Nor.1)":
                case "SlayerSoldier":
                case "FatDemon":
                case "ChaosRipper":
                case "DragonKing":
                case "NemesisTyrant":
                case "GoldOctopus":
                case "IceQueen":
                case "IceKing":
                case "ThunderDragon":
                case "FuriousBear":
                    return true;
            }
            return false;
        }
        public Time32 LastMove;
        public int MoveSpeed;
        public int RunSpeed;
        public int OwnItemID, OwnItemRate;
        public int HPPotionID, MPPotionID;
        public int AttackSpeed;
        public int MinimumSpeed
        {
            get
            {
                int min = 10000000;
                if (min > MoveSpeed)
                    min = MoveSpeed;
                if (min > RunSpeed)
                    min = RunSpeed;
                if (min > AttackSpeed)
                    min = AttackSpeed;
                return min;
            }
        }
        public uint ExtraExperience;
        public uint MinMoneyDropAmount;
        public uint MaxMoneyDropAmount;
        public ushort BoundX, BoundY;
        public ushort BoundCX, BoundCY;
        public const int ReviverID = 9879;
        #endregion
        public static void Load()
        {
            using (var command = new MySqlCommand(MySqlCommandType.SELECT))
            {
                command.Select("monsterinfos");
                using (var reader = command.CreateReader())
                {
                    while (reader.Read())
                    {
                    
                        MonsterInformation mf = new MonsterInformation();
                        mf.ID = reader.ReadUInt32("id");
                        mf.Name = reader.ReadString("name");
                        mf.Type = reader.ReadUInt32("type");
                        mf.Mesh = reader.ReadUInt16("lookface");
                        mf.Level = reader.ReadByte("level");
                        mf.Hitpoints = reader.ReadUInt32("life");
                        IniFile IniFile = new IniFile(Constants.MonstersPath);
                        if (IniFile.ReadString(mf.Name, "MaxLife") != "")
                        {
                            if (uint.Parse(IniFile.ReadString(mf.Name, "MaxLife")) != 0)
                            {
                                mf.Hitpoints = uint.Parse(IniFile.ReadString(mf.Name, "MaxLife"));
                                byte boss = byte.Parse(IniFile.ReadString(mf.Name, "Boss"));
                                if (boss == 0)
                                    mf.Boss = false;
                                else mf.Boss = true;
                            }
                        }
                        if (!mf.Boss && IsSourceBossName(mf.Name))
                            mf.Boss = true;
                        // Repeatable Chi/Jiang hunts share the 1.0.21 client boss profile.
                        // Override the legacy 500-million-HP catalog entries.
                        if (mf.ID == 7902 || mf.ID == 2648) {
                            mf.Hitpoints = 5000000;
                            mf.Boss = true;
                        }
                        mf.Guard = mf.Name.Contains("Guard1");
                        mf.Reviver = mf.ID == ReviverID;
                        mf.ViewRange = reader.ReadUInt16("view_range");
                        mf.AttackRange = reader.ReadUInt16("attack_range");
                        mf.Defence = reader.ReadUInt16("defence");
                        mf.AttackType = reader.ReadByte("attack_user");
                        mf.MinAttack = reader.ReadUInt32("attack_min");
                        mf.MaxAttack = reader.ReadUInt32("attack_max");
                        if (mf.Name != null && mf.Name.StartsWith("Birdman", StringComparison.OrdinalIgnoreCase)
                            && Revora.Config.BirdmanAttackPercent > 0 && Revora.Config.BirdmanAttackPercent < 100)
                        {
                            mf.MinAttack = Math.Max(1, mf.MinAttack * Revora.Config.BirdmanAttackPercent / 100);
                            mf.MaxAttack = Math.Max(1, mf.MaxAttack * Revora.Config.BirdmanAttackPercent / 100);
                        }
                        mf.SpellID = reader.ReadUInt16("magic_type");
                        mf.MoveSpeed = reader.ReadInt32("move_speed");
                        mf.RunSpeed = reader.ReadInt32("run_speed");
                        mf.OwnItemID = reader.ReadInt32("ownitem");
                        mf.OwnItemRate = reader.ReadInt32("ownitemrate");
                        mf.AttackSpeed = reader.ReadInt32("attack_speed");
                        mf.ExtraExperience = reader.ReadUInt32("extra_exp");
                        uint MoneyDropAmount = reader.ReadUInt16("level");
                        if (MoneyDropAmount != 0)
                        {
                            mf.MaxMoneyDropAmount = MoneyDropAmount * 100;
                            if (mf.MaxMoneyDropAmount != 0)
                                mf.MinMoneyDropAmount = 1;
                        }
                        if (mf.MoveSpeed <= 500)
                            mf.MoveSpeed += 500;
                        if (mf.AttackSpeed <= 500)
                            mf.AttackSpeed += 500;
                        if (!mf.Guard && !mf.Reviver && mf.ViewRange < 16)
                            mf.ViewRange = 16;
                        if (mf.AttackRange < 1)
                            mf.AttackRange = 1;
                        Revora.Game.Features.PowerArenaEvent.ConfigureGuardian(mf);
                        MonsterInformations.Add(mf.ID, mf);
                        byte lvl = mf.Level;
                        if (mf.Name == "Slinger" || mf.Name == "GoldGhost" || 
                            mf.Name == "GoldenOctopus" || mf.Name == "AgileRat" || 
                            mf.Name == "Bladeling" || mf.Name == "BlueBird" || 
                            mf.Name == "BlueFiend" || mf.Name == "MinotaurL120")
                        {
                            mf.LabirinthDrop = true;
                            lvl = 20;
                        }
                    }
                }
            }
       
        }
        public MonsterInformation Copy()
        {
            MonsterInformation mf = new MonsterInformation();
            mf.ID = this.ID;
            mf.Name = this.Name;
            mf.Mesh = this.Mesh;
            mf.Level = this.Level;
            mf.Hitpoints = this.Hitpoints;
            mf.ViewRange = this.ViewRange;
            mf.AttackRange = this.AttackRange;
            mf.AttackType = this.AttackType;
            mf.MinAttack = this.MinAttack;
            mf.MaxAttack = this.MaxAttack;
            mf.SpellID = this.SpellID;
            mf.MoveSpeed = this.MoveSpeed;
            mf.RunSpeed = this.RunSpeed;
            mf.AttackSpeed = this.AttackSpeed;
            mf.BoundX = this.BoundX;
            mf.BoundY = this.BoundY;
            mf.BoundCX = this.BoundCX;
            mf.BoundCY = this.BoundCY;
            mf.RespawnTime = this.RespawnTime;
            mf.ExtraExperience = this.ExtraExperience;
            mf.MaxMoneyDropAmount = this.MaxMoneyDropAmount;
            mf.MinMoneyDropAmount = this.MinMoneyDropAmount;
            mf.OwnItemID = this.OwnItemID;
            mf.HPPotionID = this.HPPotionID;
            mf.MPPotionID = this.MPPotionID;
            mf.OwnItemRate = this.OwnItemRate;
            mf.LabirinthDrop = this.LabirinthDrop;
            mf.Boss = this.Boss;
            mf.Guard = this.Guard;
            mf.Defence = this.Defence;
            mf.Reviver = this.Reviver;
            return mf;
        }
        public void Drop(Game.Entity killer)
        {
            if (Game.Features.AncientDevilBossWorld.IsEncounter(Owner)) return;
            if (Game.Features.EliminationRescueWorld.IsEncounter(Owner)) return;
            if (Game.Features.EliminationDenWorld.IsEncounter(Owner)) return;
            if (Game.Features.AncientDevilWorld.IsEncounter(Owner)) return;
            if (Game.Features.AbyssTrialWorld.IsEncounter(Owner)) return;
            if (Game.Features.TeratoEncounter.IsEncounter(Owner)) return;
            if (Game.Features.ExorcismWorld.IsEncounter(Owner)) return;
            if (Game.Features.WarriorEpicCombat.IsEncounter(Owner)) return;
            if (Game.Features.TrojanEpicCombat.IsEncounter(Owner)) return;
            if (Game.Features.SwordMasterEncounter.IsEncounter(Owner)) return;
            if (Game.Features.PurpleBansheeEncounter.IsEncounter(Owner)) return;
            if (Game.Features.SnowBansheeEncounter.IsEncounter(Owner)) return;
            if (Game.Features.DragonSlayerWorld.IsEncounter(Owner)) return;
            if (Game.Features.DailyWorldBosses.IsDaily(Owner)) return;
            if (Game.Features.DisCityWorld.IsEncounter(Owner)) return;
            if (Game.Features.ExpeditionEvent.IsEncounter(Owner)) return;
            Game.Map potencyMap;
            if (Owner != null && Owner.MapID == Game.Features.PotencyHunt.MapId
                && Kernel.Maps.TryGetValue(Owner.MapID, out potencyMap) && potencyMap.PotencyPopulation != null
                && potencyMap.PotencyPopulation.Drop(Owner, killer)) return;
            if (Revora.Game.Features.LivingWorldSystem.IsAdventurerName(Name))
                return;
            if (Revora.Game.Features.WaveDefenseSystem.IsBanner(Owner))
                return;
            if (Owner != null && Revora.Game.Features.BeastTrialSystem.InEvent(Owner))
                return;
            if (Owner != null && Revora.Game.Features.BossRushSystem.InEvent(Owner))
                return;
            if (killer != null && (Revora.Game.Features.CityGuardSystem.IsCityGuard(killer)
                || Revora.Game.Features.LivingWorldSystem.IsAdventurer(killer)))
                return;
            if (Name == "ArenaGuard" && killer != null && killer.Owner != null)
            {
                Revora.Game.Features.CityGuardSystem.ReleaseArenaVictory(killer.Owner);
                return;
            }
            if (killer != null && killer.Owner != null && Revora.Game.Features.ExpeditionEvent.TryCollectKill(killer.Owner, this))
                return;
            if (Game.Features.DeityLandRelics.TryDrop(Owner, killer)) return;
            Game.Features.MonsterEquipmentDrops.TryDrop(Owner, killer);
            Game.Features.GrottoKeyDrops.TryDrop(Owner, killer);
            Game.Features.CleanWaterDrops.TryDrop(Owner, killer);
            Game.Features.MeteorDrops.TryDrop(Owner, killer);
            Game.Features.DailyHuntDrops.TryDrop(Owner, killer);
            Game.Features.NinjaFlowerHunt.TryDrop(Owner, killer);
            Game.Features.EliminationPills.Collect(Owner, killer);
            #region Taoist epic mission
            if (Name == "Task Epic Taoist")
            {
                byte times = (byte)Kernel.Random.Next(1, 3);
                byte ref_times = (byte)Kernel.Random.Next(1, 6);
                for (byte i = 0; i < times; i++)
                {
                    uint Uid = 0;
                    byte type = (byte)Kernel.Random.Next(1, 6);
                    switch (type)
                    {
                        #region IDs
                        case 1:
                            Uid = 722734;
                            break;
                        case 2:
                            Uid = 729780;
                            break;
                        case 3:
                            Uid = 1072040;
                            break;
                        #endregion

                    }

                    if (Uid != 0)
                    {
                        killer.Owner.Inventory.Add(Uid, 0, 1);
                        return;
                    }
                }
            }
            #endregion
            #region Epic Mission of Trojan
            #region FuriousDevastatorFire (First Stage monsters in TrojanEpicMission)
            if (Name == "FuriousFlameDevastator")
            {
                if (Kernel.Rate(80, 100))
                {
                    if (killer.Name.Contains("Guard1"))
                    {
                        return;
                    }

                    uint ItemID = 3003336;
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                    killer.Owner.Send(new Network.GamePackets.Message("Wow! " + Name + " You killed Furious Devastator Fire and receive Fragment Solar!", System.Drawing.Color.Blue, 2005));
                    return;
                }
            }
            #endregion
            #region Awake Devastator Fire (Second Stage monsters in TrojanEpicMission)
            if (Name == "AwakeFlameDevastator")
            {
                if (Kernel.Rate(80, 100))
                {
                    if (killer.Name.Contains("Guard1"))
                    {
                        return;
                    }

                    uint ItemID = 3003338;
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                    killer.Owner.Send(new Network.GamePackets.Message("Wow! " + Name + "You killed Furious Devastator Fire and receive Essence Solar!", System.Drawing.Color.Blue, 2005));
                    return;
                }
            }
            #endregion
            #region EvilMonkMisery (Third Stage monsters in TrojanEpicMission)
            if (Name == "EvilMonkMisery")
            {
                if (Kernel.Rate(80, 100))
                {
                    if (killer.Name.Contains("Guard1"))
                    {
                        return;
                    }

                    uint ItemID = 3003335;
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                    killer.Owner.Send(new Network.GamePackets.Message("Wow! " + Name + " You killed EvilMonkMisery and you receive KnifeSolarPecio!", System.Drawing.Color.Blue, 2005));
                    return;
                }
            }
            #endregion
            #region LootingGhost (Fourth Stage monsters in TrojanEpicMission)
            if (Name == "GhostReaver")
            {
                if (Kernel.Rate(80, 100))
                {
                    if (killer.Name.Contains("Guard1"))
                    {
                        return;
                    }

                    uint ItemID = 3003340;
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                    killer.Owner.Send(new Network.GamePackets.Message("Wow! " + Name + "You killed GhostPlayer and receive SolarBlade!", System.Drawing.Color.Blue, 2005));
                    return;
                }
            }
            #endregion
            #endregion
            #region Universal CPs
            if (killer.EntityFlag == Game.EntityFlag.Player
                && Revora.Config.KillConquerPointsEnabled
                && killer.VIPLevel >= Revora.Config.KillConquerPointsMinVip)
            {
                if (killer.Name.Contains("Guard1"))
                {
                    return;
                }
                uint cps = Revora.Config.KillConquerPointsAlways;
                if (Kernel.Rate(Revora.Config.KillConquerPointsChance5))
                    cps += Revora.Config.KillConquerPointsAmount5;
                if (Kernel.Rate(Revora.Config.KillConquerPointsChance50))
                    cps += Revora.Config.KillConquerPointsAmount50;
                if (Kernel.Rate(Revora.Config.KillConquerPointsChance250))
                    cps += Revora.Config.KillConquerPointsAmount250;
                if (cps > 0)
                {
                    killer.AddConquerPoints(cps);
                    killer.Owner.Send(new Network.GamePackets.Message("You received " + cps + " CPs from " + Name + ".", System.Drawing.Color.Red, 2005));
                }
            }
            #endregion
            #region VIPs 1 - 5
            if (Revora.Config.VipKillGoldEnabled && killer.EntityFlag == Game.EntityFlag.Player)
                #region VIP5 normal
                if (killer.VIPLevel <= 5  && Kernel.Rate(40))
                {
                    if (killer.Name.Contains("Guard1"))
                    {
                        return;
                    }

                    int min1 = 1;
                    int max1 = 500000;
                    int normal1 = Kernel.Random.Next(min1, max1);
                    int minn1 = 1;
                    int maxx1 = 1000000;
                    int normall1 = Kernel.Random.Next(minn1, maxx1);
                    if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50)
                    {
                        //killer.ConquerPoints += (PremioEventos.VIPcps);
                        killer.AdjustMoney((decimal)((uint)normal1));
                        killer.Owner.Send(new Network.GamePackets.Message("[VIP5 or less] - You Killed " + Owner.Name + " And you won " + normal1 + " Gold ", System.Drawing.Color.DarkBlue, 255));

                    }
                    if (Kernel.GamePool.Count >= 51 )
                    {
                        //killer.ConquerPoints += (PremioEventos.VIPcps);
                        killer.AdjustMoney((decimal)((uint)normall1));
                        killer.Owner.Send(new Network.GamePackets.Message("[VIP5 or less] DROPx2- By: [" + Kernel.GamePool.Count + "] Online Accounts - You Killed " + Owner.Name + "And you won" + normal1 + " Gold ", System.Drawing.Color.DarkBlue, 255));

                    }

                }
#endregion
                
            #endregion
            #region VIP TimerSlayerdrop

            if (killer.EntityFlag == Game.EntityFlag.Player)
                #region VIP5 normal + 1 hour of slayer
                if (killer.VIPLevel <= 5 && killer.TimeDropSlayer > 0)
            {
                if (killer.Name.Contains("Guard1"))
                {
                    return;
                }

                int min1 = 1;
                int max1 = 5;
                int normal1 = Kernel.Random.Next(min1, max1);
                int minn1 = 1;
                int maxx1 = 10;
                int normall1 = Kernel.Random.Next(minn1, maxx1);
                if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50)
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normal1);
                    killer.Owner.Send(new Message("[DropSlayer] - You Killed" + Owner.Name + "And Earned" + normal1 + " Revora Point", System.Drawing.Color.Red, Message.Whisper));
                }
                if (Kernel.GamePool.Count >= 51)
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normall1);
                    killer.Owner.Send(new Message("[DropSlayer] DROPx2- By: [" + Kernel.GamePool.Count + "] Online Accounts - You Killed" + Owner.Name + "And Earned" + normall1 + " Revora Point", System.Drawing.Color.Red, Message.Whisper));
                }

            }
            #endregion
            #region Normal VIP6 + 1 hour of slayer
            if (killer.VIPLevel == 6 && killer.TimeDropSlayer > 0)
            {
                if (killer.Name.Contains("Guard1"))
                {
                    return;
                }

                int min1 = 1;
                int max1 = 10;
                int normal1 = Kernel.Random.Next(min1, max1);
                int minn1 = 1;
                int maxx1 = 15;
                int normall1 = Kernel.Random.Next(minn1, maxx1);
                if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50)
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normal1);
                    killer.Owner.Send(new Message("[DropSlayer Hour] - You Killed" + Owner.Name + " and you won " + normal1 + " Revora Point", System.Drawing.Color.Red, Message.Whisper));
                }
                if (Kernel.GamePool.Count >= 51)
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normall1);
                    killer.Owner.Send(new Message("[DropSlayer Time] DROPx2- By: [" + Kernel.GamePool.Count + "] Online Accounts - You Killed" + Owner.Name + "And Earned" + normall1 + " Revora Point ", System.Drawing.Color.Red, Message.Whisper));
                }

            }
            #endregion
                #region VIP7 normal + 1 hour of slayer
            if (killer.VIPLevel == 7 && killer.TimeDropSlayer > 0)
            {
                if (killer.Name.Contains("Guard1"))
                {
                    return;
                }

                int min1 = 1;
                int max1 = 15;
                int normal1 = Kernel.Random.Next(min1, max1);
                int minn1 = 1;
                int maxx1 = 20;
                int normall1 = Kernel.Random.Next(minn1, maxx1);
                if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50)
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normal1);

                    killer.Owner.Send(new Message("[DropSlayer Time] - You Killed" + Owner.Name + " And you won " + normal1 + " Revora Point ", System.Drawing.Color.Red, Message.Whisper));

                }
                if (Kernel.GamePool.Count >= 51)
                {
                   
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AddRevoraPoints((uint)normall1);
                    killer.Owner.Send(new Message("[DropSlayer Time] DROPx2- By: [" + Kernel.GamePool.Count + "] Online Accounts - You Killed " + Owner.Name + " And you won " + normall1 + " Revora Point ", System.Drawing.Color.Red, Message.Whisper));

                }

            }
            #endregion
            #endregion

            #region VIPs 6-7 Drops
            if (Revora.Config.VipKillGoldEnabled && killer.EntityFlag == Game.EntityFlag.Player)
                if (killer.Name.Contains("Guard1"))
                {
                    return;
                }
            if (Revora.Config.VipKillGoldEnabled && killer.VIPLevel == 6)
            {
                int VIPmin1 = 100000;
                int VIPmax1 = 600000;
                int VIPnormal1 = Kernel.Random.Next(VIPmin1, VIPmax1);
                int VIPmin2 = 100000;
                int VIPmax2 = 1200000;
                int VIPnormal2 = Kernel.Random.Next(VIPmin2, VIPmax2);
                int VIPmin3 = 100000;
                int VIPmax3 = 1800000;
                int VIPnormal3 = Kernel.Random.Next(VIPmin3, VIPmax3);
                if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50 && Kernel.Rate(40))
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AdjustMoney((decimal)((uint)VIPnormal1));
                    killer.Owner.Send(new Network.GamePackets.Message("You're [VIP6] Normal- You Killed" + Owner.Name + " And you won" + VIPnormal1 + " Gold ", System.Drawing.Color.DarkBlue, 255));
                                                       
                }
                if (Kernel.GamePool.Count >= 51 && Kernel.GamePool.Count <= 99 && Kernel.Rate(40))
                {
                    killer.AdjustMoney((decimal)((uint)VIPnormal2));
                    killer.Owner.Send(new Network.GamePackets.Message("You are [VIP 6] DROP x2- By: [" + Kernel.GamePool.Count + "] Online Accounts, - You Killed" + Owner.Name + " And you won " + VIPnormal2 + " Gold ", System.Drawing.Color.DarkBlue, 255));
                }
                if (Kernel.GamePool.Count >= 100 && Kernel.Rate(40))
                {
                    killer.AdjustMoney((decimal)((uint)VIPnormal3));
                    killer.Owner.Send(new Network.GamePackets.Message("You are [VIP6] DROPx3- By: [" + Kernel.GamePool.Count + "] Online Accounts, - You Killed " + Owner.Name + " And you won " + VIPnormal3 + " Gold ", System.Drawing.Color.DarkBlue, 255));
                }
            

            }
            if (Revora.Config.VipKillGoldEnabled && killer.VIPLevel == 7)
            {
                int VIP7min1 = 500000;
                int VIP7max1 = 1800000;
                int VIP7normal1 = Kernel.Random.Next(VIP7min1, VIP7max1);
                int VIP7min2 = 500000;
                int VIP7max2 = 2400000;
                int VIP7normal2 = Kernel.Random.Next(VIP7min2, VIP7max2);
                int VIP7min3 = 500000;
                int VIP7max3 = 3000000;
                int VIP7normal3 = Kernel.Random.Next(VIP7min3, VIP7max3);
                if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 50 && Kernel.Rate(40))
                {
                    //killer.ConquerPoints += (PremioEventos.VIPcps);
                    killer.AdjustMoney((decimal)((uint)VIP7normal1));
                    killer.Owner.Send(new Network.GamePackets.Message("You're [VIP7] Normal- You Killed " + Owner.Name + " And you won" + VIP7normal1 + " Gold ", System.Drawing.Color.DarkBlue, 255));

                }
                if (Kernel.GamePool.Count >= 51 && Kernel.GamePool.Count <= 99 && Kernel.Rate(40))
                {
                    killer.AdjustMoney((decimal)((uint)VIP7normal2));
                    killer.Owner.Send(new Network.GamePackets.Message("You are [VIP 7] DROP x2- For the: [" + Kernel.GamePool.Count + "] Online Accounts, - You Killed " + Owner.Name + " And you won " + VIP7normal2 + " Gold ", System.Drawing.Color.DarkBlue, 255));
                }
                if (Kernel.GamePool.Count >= 100 && Kernel.Rate(40))
                {
                    killer.AdjustMoney((decimal)((uint)VIP7normal3));
                    killer.Owner.Send(new Network.GamePackets.Message("You are [VIP 7] DROP x3- For the: [" + Kernel.GamePool.Count + "] Online Accounts, - You Killed " + Owner.Name + "And you won " + VIP7normal3 + " Gold ", System.Drawing.Color.DarkBlue, 255));
                }


            }
            #endregion
            #region Crazy Hour
            int min12 = 1;
            int max12 = 3500000;
            DateTime Now64 = DateTime.Now;
            int normal12 = Kernel.Random.Next(min12, max12);           

            if (killer.Name.Contains("Guard1"))
            {
                return;
            }
            if (Revora.Config.CrazyHourEnabled && ((Now64.Hour >= 6 && Now64.Hour < 7) || (Now64.Hour >= 12 && Now64.Hour < 13) || (Now64.Hour >= 18 && Now64.Hour < 19) || (Now64.Hour >= 23 && Now64.Hour < 24)))
            {
                
                    if (Kernel.GamePool.Count >= 1 && Kernel.GamePool.Count <= 30 && Kernel.Rate(10))
                    {
                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(1);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 1 Revora Pointr  ", System.Drawing.Color.Red, Message.Whisper));
 
                    }
                    if (Kernel.GamePool.Count >= 31 && Kernel.GamePool.Count <= 40 && Kernel.Rate(15))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(2);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 2 Revora Point  ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 41 && Kernel.GamePool.Count <= 50 && Kernel.Rate(20))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(3);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 3 Revora Point  ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 51 && Kernel.GamePool.Count <= 60 && Kernel.Rate(25))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(4);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + "Gold + 4 Revora Point   ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 61 && Kernel.GamePool.Count <= 70 && Kernel.Rate(30))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(5);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 5 Revora Point  ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 71 && Kernel.GamePool.Count <= 80 && Kernel.Rate(35))
                    {
                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(6);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 6 Revora Point  ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 81 && Kernel.GamePool.Count <= 99 && Kernel.Rate(40))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(7);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + "Gold + 7 Revora Point ", System.Drawing.Color.Red, Message.Whisper));
                    }
                    if (Kernel.GamePool.Count >= 100 && Kernel.Rate(50))
                    {

                        killer.AdjustMoney((decimal)((uint)normal12));
                        killer.AddRevoraPoints(10);
                        killer.Owner.Send(new Message("There are " + Kernel.GamePool.Count + " Connected Players, [RevoraHour], " + normal12 + " Gold + 10 Revora Point  ", System.Drawing.Color.Red, Message.Whisper));
                    }

                
            }
            #endregion
            #region CPs war drop
            int min13 = 500000;
            int max13 = 1000000;
            int normal13 = Kernel.Random.Next(min13, max13);

            if (killer.Name.Contains("Guard1"))
            {
                return;
            }
            if (Revora.Config.GoldWarKillGoldEnabled && Kernel.Rate(100) && Owner.MapID == 2072)
            {
                killer.AdjustMoney((decimal)((uint)normal13));
                killer.Owner.Send(new Message("[GoldWar] you kill " + Name + "and you receive  " + normal13 + " gold [Monday only]", System.Drawing.Color.Azure, Message.TopLeft));

            }
            #endregion
            #region drop disabled
            /*
            #region 20 Players Online
            if (killer.Name.Contains("Guard1"))
            {
                return;
            }
            if (Kernel.GamePool.Count >= 20 && Kernel.GamePool.Count <= 29)
            {
                killer.AddConquerPoints(500);
                killer.Owner.Send(new Network.GamePackets.Message("You received 500 extra CPs for having " + Kernel.GamePool.Count + " players online.", System.Drawing.Color.Yellow, 2005));
            }
            #endregion
            #region 30 Players Online
            else if (Kernel.GamePool.Count >= 30 && Kernel.GamePool.Count <= 39)
            {
                killer.AddConquerPoints(1000);
                killer.Owner.Send(new Network.GamePackets.Message("You received 1000 extra CPs for having " + Kernel.GamePool.Count + " players online.", System.Drawing.Color.Yellow, 2005));
            }
            #endregion
            #region 40 Players Online
            else if (Kernel.GamePool.Count >= 40 && Kernel.GamePool.Count <= 49)
            {
                killer.AddConquerPoints(1500);
                killer.Owner.Send(new Network.GamePackets.Message("You received 1500 extra CPs for having " + Kernel.GamePool.Count + " players online.", System.Drawing.Color.Yellow, 2005));
            }
            #endregion
            #region 50 Players Online
            else if (Kernel.GamePool.Count >= 50 && Kernel.GamePool.Count <= 59)
            {
                killer.AddConquerPoints(2000);
                killer.Owner.Send(new Network.GamePackets.Message("You received 2000 extra CPs for having " + Kernel.GamePool.Count + " players online.", System.Drawing.Color.Yellow, 2005));
            }
            #endregion
            #region 60 Players Online
            else
            {
                if (Kernel.GamePool.Count >= 60)
                {
                    killer.AddConquerPoints(2500);
                    killer.Owner.Send(new Network.GamePackets.Message("You received 2500 extra CPs for having " + Kernel.GamePool.Count + " players online.", System.Drawing.Color.Yellow, 2005));
                }
            }
            #endregion */
            #endregion
            #region No Drob Monster
            if (killer.Name.Contains("Guard1")) return;
            if (Owner.Name.Contains("Guard1")) return;
            if (Owner.Name.Contains("King Ninja's")) return;
            if (Owner.Name.Contains("King Assessin's")) return;
            if (Owner.Name.Contains("King MonK's")) return;
            if (Owner.Name.Contains("King Pirate's")) return;
            if (Owner.Name.Contains("King Warrior's")) return;
            if (Owner.Name.Contains("King Trojan's")) return;
            #endregion
            #region Big Monsters [Bosses]
            #region Storm Wings
            if (Name == "SkyRoc")
            {//Chavarro
                uint ItemID = 0;
                byte type1 = 2;
                for (int i = 0; i < 2; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3006392;// some IDs can be changed
                            break;
                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                return;
            }

            #endregion
            #region SkyDevil auto-hunt [VIP 5]
            if (Name == "SkyDevil")
            {//Chavarro
                uint ItemID = 0;
                byte type1 = 2;
                for (int i = 0; i < 1; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 711899;// some IDs can be changed711899
                            break;
                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                return;
            }

            #endregion
            #region DrunkWatchman
            if (Name == "DrunkWatchman")
            {
                uint Uid = 0;
                byte type = 30;
                killer.AddConquerPoints(Revora.Config.DrunkWatchmanCp);
                for (int i = 0; i < 1; i++)
                {
                    type = (byte)Kernel.Random.Next(1, 9);
                    switch (type)
                    {
                        case 1:
                            Uid = 3004245;
                            break;
                        case 2:
                            Uid = 3004244;
                            break;
                        case 3:
                            Uid = 3004257;
                            break;
                        case 4:
                            Uid = 3004243;
                            break;
                        case 5:
                            Uid = 3004244;
                            break;
                        case 6:
                            Uid = 3004181;
                            break;
                        case 7:
                            Uid = 3004243;
                            break;
                        case 8:
                            Uid = 3004257;
                            break;
                        case 9:
                            Uid = 3006319;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(Uid, 0, 1);
                            if (killer.VIPLevel <= 4)
                            {
                                killer.VIPLevel += 1;
                                Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + "Has killed" + Name + " and get VIP- [Revora]!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                                Network.GamePackets._String str = new _String(true);
                                str.UID = killer.UID;
                                str.TextsCount = 1;
                                str.Type = _String.Effect;
                                str.Texts.Add("cortege");
                                killer.Owner.SendScreen(str, true);
                            }
                            else
                            {
                                killer.Owner.Send(new Network.GamePackets.Message("You have killed DrunkWatchman but you do not receive VIP", System.Drawing.Color.DarkBlue, 255));

                            }
                        }
                    }
                }
            }
            #endregion          

            #region SnowBansheeSoul
            if (Name == "SnowBansheeSoul" || Name == "PurpleBanshee")
            {
                uint ItemID = 0;
                byte type1 = 22;
                for (int i = 0; i < 2; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 9);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3004245;
                            break;
                        case 2:
                            ItemID = 3004244;
                            break;
                        case 3:
                            ItemID = 723713;
                            break;
                        case 4:
                            ItemID = 3004243;
                            break;
                        case 5:
                            ItemID = 3004244;
                            break;
                        case 6:
                            ItemID = 723713;
                            break;
                        case 7:
                            ItemID = 3004243;
                            break;
                        case 8:
                            ItemID = 3004257;
                            break;
                        case 9:
                            ItemID = 3006319;
                            break;

                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                        Kernel.SpawnBansheeSoulKeep = false;
                    }
                }
                if (Owner.MonsterInfo.ID == 4212)
                {
                    if (Owner.MapID == 4390)
                    {
                        Kernel.Maps[4390].Npcs.Remove(1658);
                        NpcSpawn Npc = new NpcSpawn();
                        Npc.UID = 1657;
                        Npc.Mesh = 16530;
                        Npc.X = 437;
                        Npc.Y = 327;
                        Npc.MapID = 4390;
                        Npc.Type = Enums.NpcType.None;
                        Kernel.Maps[4390].Npcs.Add(1657, Npc);
                        Npc.SendSpawn(killer.Owner);

                        killer.Owner.SendScreen(new Data(true)
                        {
                            UID = 1657,
                            ID = Data.SpawnEffect
                        }, true);
                        killer.Owner.SendScreen(new Data(true)
                        {
                            UID = 1658,
                            ID = Data.RemoveEntity
                        }, true);
                    }
                }
                foreach (var Ownerr in Server.GamePool)
                {
                    if (Ownerr.Entity.MapID == 4390)
                    {
                        foreach (Interfaces.INpc npc in Kernel.Maps[4390].Npcs.Values)
                        {
                            if (npc != null)
                            {
                                if (npc.UID == 1657)
                                {
                                    if (Kernel.GetDistance(npc.X, npc.Y, Ownerr.Entity.X, Ownerr.Entity.Y) < 16)
                                    {
                                        npc.SendSpawn(Ownerr, false);
                                    }
                                }
                            }
                        }
                    }
                }
                return;
            }
            #endregion
            #region SuperBoss
            //if ( Name == "FatDemon"|| Name == "ChaosRipper"|| Name == "DragonKing"||Name == "NemesisTyrant" ||Name == "GoldOctopus" || Name == "IceQueen")
            //{
            //    uint Uid = 0;
            //    byte type = 30;                
            //    for (int i = 0; i < 1; i++)
            //    {
            //        type = (byte)Kernel.Random.Next(1, 13);
            //        switch (type)
            //        {
            //            case 1:
            //                Uid = 3004243;
            //                break;
            //            case 2:
            //                Uid = 3004244;
            //                break;
            //            case 3:
            //                Uid = 3004245;
            //                break;
            //            case 4:
            //                Uid = 3009002;
            //                break;
            //            case 5:
            //                Uid = 3004243;
            //                break;
            //            case 6:
            //                Uid = 3004244;
            //                break;
            //            case 7:
            //                Uid = 3004245;
            //                break;
            //            case 8:
            //                Uid = 3009002;
            //                break;
            //            case 9:
            //                Uid = 3004243;
            //                break;
            //            case 10:
            //                Uid = 3004244;
            //                break;
            //            case 12:
            //                Uid = 3004245;
            //                break;
            //            case 13:
            //                Uid = 3009002;
            //                break;
            //        }
            //        if (Uid != 0)
            //        {
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (killer.VIPLevel == 7)
            //            {
            //                killer.ConquerPoints += 15000000;
            //                killer.Owner.Inventory.Add(Uid, 0, 1);
            //                killer.RevoraPoint += 350;
            //                Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " has killed " + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " and 15.000.000 CPs + 350 [Slayer] points!", System.Drawing.Color.White, Message.Center), Server.GamePool);
            //                Network.GamePackets._String str = new _String(true);
            //                str.UID = killer.UID;
            //                str.TextsCount = 1;
            //                str.Type = _String.Effect;
            //                str.Texts.Add("cortege");
            //                killer.Owner.SendScreen(str, true);
            //            }
            //            if (killer.VIPLevel == 6)
            //            {
            //                killer.ConquerPoints += 10000000;
            //                killer.Owner.Inventory.Add(Uid, 0, 1);
            //                killer.RevoraPoint += 250;
            //                Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " has killed " + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " and 10.000.000 CPs + 250 [Slayer] points!", System.Drawing.Color.White, Message.Center), Server.GamePool);
            //                Network.GamePackets._String str = new _String(true);
            //                str.UID = killer.UID;
            //                str.TextsCount = 1;
            //                str.Type = _String.Effect;
            //                str.Texts.Add("cortege");
            //                killer.Owner.SendScreen(str, true);
            //            }
            //            if (killer.VIPLevel <= 5)

            //            {
            //                killer.ConquerPoints += 5000000;
            //                killer.Owner.Inventory.Add(Uid, 0, 1);
            //                killer.RevoraPoint += 150;
            //                Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " has killed " + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " and 5.000.000 CPs + 150 [Slayer] points!", System.Drawing.Color.White, Message.Center), Server.GamePool);
            //                Network.GamePackets._String str = new _String(true);
            //                str.UID = killer.UID;
            //                str.TextsCount = 1;
            //                str.Type = _String.Effect;
            //                str.Texts.Add("cortege");
            //                killer.Owner.SendScreen(str, true);
            //            }
            //        }
                    
            //    }
            //}
            #endregion                       
            #region MagmaDemon
            if (Name == "MagmaDemon")
            {
                uint ItemID = 0;
                byte type1 = 40;
                for (int i = 0; i < 5; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 40);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 724352;
                            break;
                        case 2:
                            ItemID = 724357;
                            break;
                        case 3:
                            ItemID = 724362;
                            break;
                        case 4:
                            ItemID = 724367;
                            break;
                        case 5:
                            ItemID = 724372;
                            break;
                        case 6:
                            ItemID = 724377;
                            break;
                        case 7:
                            ItemID = 724384;
                            break;
                        case 8:
                            ItemID = 724389;
                            break;
                        case 9:
                            ItemID = 724394;
                            break;
                        case 10:
                            ItemID = 724404;
                            break;
                        case 11:
                            ItemID = 724409;
                            break;
                        case 12:
                            ItemID = 724414;
                            break;
                        case 13:
                            ItemID = 724419;
                            break;
                        case 14:
                            ItemID = 724424;
                            break;
                        case 15:
                            ItemID = 724429;
                            break;
                        case 16:
                            ItemID = 724434;
                            break;
                        case 17:
                            ItemID = 724439;
                            break;
                        case 18:
                            ItemID = 724444;
                            break;
                        case 19:
                            ItemID = 724453;
                            break;
                        case 20:
                            ItemID = 724458;
                            break;
                        case 21:
                            ItemID = 724463;
                            break;
                        case 22:
                            ItemID = 724477;
                            break;
                        case 23:
                            ItemID = 724482;
                            break;
                        case 24:
                            ItemID = 724487;
                            break;
                        case 25:
                            ItemID = 724492;
                            break;
                        case 26:
                            ItemID = 724497;
                            break;
                        case 27:
                            ItemID = 724502;
                            break;
                        case 28:
                            ItemID = 350011;
                            break;
                        case 29:
                            ItemID = 350012;
                            break;
                        case 30:
                            ItemID = 350014;
                            break;
                        case 31:
                            ItemID = 350015;
                            break;
                        case 32:
                            ItemID = 350016;
                            break;
                        case 33:
                            ItemID = 350017;
                            break;
                        case 34:
                            ItemID = 350018;
                            break;
                        case 35:
                            ItemID = 350019;
                            break;
                        case 36:
                            ItemID = 350020;
                            break;
                        case 37:
                            ItemID = 360021;
                            break;
                        case 38:
                            ItemID = 360022;
                            break;
                        case 39:
                            ItemID = 360023;
                            break;
                        case 40:
                            ItemID = 360024;
                            break;
                        case 41:
                            ItemID = 360025;
                            break;
                        case 42:
                            ItemID = 360026;
                            break;
                        case 43:
                            ItemID = 360027;
                            break;
                        case 44:
                            ItemID = 360028;
                            break;
                        case 45:
                            ItemID = 360029;
                            break;
                        case 46:
                            ItemID = 360030;
                            break;
                        case 47:
                            ItemID = 360031;
                            break;
                        case 48:
                            ItemID = 360032;
                            break;
                        case 49:
                            ItemID = 360033;
                            break;
                        case 50:
                            ItemID = 360034;
                            break;
                        case 51:
                            ItemID = 360035;
                            break;
                        case 52:
                            ItemID = 360036;
                            break;
                        case 53:
                            ItemID = 360037;
                            break;
                        case 54:
                            ItemID = 360038;
                            break;
                        case 55:
                            ItemID = 360039;
                            break;
                        case 56:
                            ItemID = 360040;
                            break;
                        case 57:
                            ItemID = 380011;
                            break;
                        case 58:
                            ItemID = 380012;
                            break;
                        case 59:
                            ItemID = 380013;
                            break;
                        case 60:
                            ItemID = 380014;
                            break;
                        case 61:
                            ItemID = 380015;
                            break;
                        case 62:
                            ItemID = 370011;
                            break;
                        case 63:
                            ItemID = 370012;
                            break;
                        case 64:
                            ItemID = 370013;
                            break;
                        case 65:
                            ItemID = 370014;
                            break;
                        case 66:
                            ItemID = 370015;
                            break;
                    }

                    if (ItemID != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(ItemID, 0, 1);
                            killer.AddConquerPoints(Revora.Config.NamedMonsterKillCp);
                            killer.AddRevoraPoints(Revora.Config.NamedMonsterKillRevoraPoints);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " and gets" + Database.ConquerItemInformation.BaseInformations[ItemID].Name + " and 1000,000 Cps + 10 Points [Slayer]! ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion            
            #region Drop of SuperBoss Cps + Slayer points
            if (Name == "FatDemon" || Name == "ChaosRipper" || Name == "DragonKing"
                || Name == "NemesisTyrant" || Name == "GoldOctopus" || Name == "IceQueen"
                || Name == "IceKing" || Name == "ThunderDragon" || Name == "FuriousBear")
            {
                //int minBossVIP5 = 500000;
                //int maxBossVIP5 = 30000000;// max CPs
                int minOroBossVIP5 = 10000000;
                int maxOroBossVIP5 = 100000000; // max gold
                //int minSlayerVIP5 = 100;
                //int maxSlayerVIP5 = 1000;

                //int minBossVIP6 = 10000000;
                //int maxBossVIP6 = 40000000;
                int minOroBossVIP6 = 20000000;
                int maxOroBossVIP6 = 300000000;
                //int minSlayerVIP6 = 300;
                //int maxSlayerVIP6 = 2000;


                //int minBossVIP7 = 15000000;
                //int maxBossVIP7 = 50000000;
                int minOroBossVIP7 = 30000000;
                int maxOroBossVIP7 = 500000000;
                //int minSlayerVIP7 = 1000;
                //int maxSlayerVIP7 = 5000;

                //int CpsBossVIP5 = Kernel.Random.Next(minBossVIP5, maxBossVIP5);
                int OroBossVIP5 = Kernel.Random.Next(minOroBossVIP5, maxOroBossVIP5);
                //int SlayerBossVIP5 = Kernel.Random.Next(minSlayerVIP5, maxSlayerVIP5);

                //int CpsBossVIP6 = Kernel.Random.Next(minBossVIP6, maxBossVIP6);
                int OroBossVIP6 = Kernel.Random.Next(minOroBossVIP6, maxOroBossVIP6);
                //int SlayerBossVIP6 = Kernel.Random.Next(minSlayerVIP6, maxSlayerVIP6);

                //int CpsBossVIP7 = Kernel.Random.Next(minBossVIP7, maxBossVIP7);
                int OroBossVIP7 = Kernel.Random.Next(minOroBossVIP7, maxOroBossVIP7);
                //int SlayerBossVIP7 = Kernel.Random.Next(minSlayerVIP7, maxSlayerVIP7);

                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 4; i++)
                {
                    type = (byte)Kernel.Random.Next(1, 3);
                    switch (type)
                    {
                        case 1:
                            Uid = 3004243; //HolySoulPieceE7
                            break;
                        case 2:
                            Uid = 3004244; //EquipmentSoulPieceE7
                            break;
                        case 3:
                            Uid = 3004245;// RefinedSacredPiece
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (killer.VIPLevel == 7)
                        {
                            killer.AdjustMoney((decimal)((uint)OroBossVIP7));
                            //killer.ConquerPoints += (uint)CpsBossVIP7;
                            killer.Owner.Inventory.Add(Uid, 0, 1);
                            //killer.RevoraPoint += (uint)SlayerBossVIP7;
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + "and gets" + OroBossVIP7 + " Gold  ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                        if (killer.VIPLevel == 6)
                        {
                            killer.AdjustMoney((decimal)((uint)OroBossVIP6));
                            //killer.ConquerPoints += (uint)CpsBossVIP6;
                            killer.Owner.Inventory.Add(Uid, 0, 1);
                            //killer.RevoraPoint += (uint)SlayerBossVIP6;
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " and gets" + OroBossVIP6 + " Gold  ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                        if (killer.VIPLevel <= 5)
                        {
                            killer.AdjustMoney((decimal)((uint)OroBossVIP5));
                            //killer.ConquerPoints += (uint)CpsBossVIP5;
                            killer.Owner.Inventory.Add(Uid, 0, 1);
                            //killer.RevoraPoint += (uint)SlayerBossVIP5;
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " and gets" + OroBossVIP5 + " Gold  ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }


                    }
                }
            }
            #endregion
            #region Team SuperBoss Drop
            if (Name == "FatDemon" || Name == "ChaosRipper" || Name == "DragonKing"
                 || Name == "NemesisTyrant" || Name == "GoldOctopus" || Name == "IceQueen"
                 || Name == "IceKing" || Name == "ThunderDragon" || Name == "FuriousBear")
            {//Chavarro
                int minSlayerVIP5 = 100;
                int maxSlayerVIP5 = 1000;
                int minSlayerVIP6 = 100;
                int maxSlayerVIP6 = 2000;
                int minSlayerVIP7 = 100;
                int maxSlayerVIP7 = 5000;
                int SlayerBossVIP5 = Kernel.Random.Next(minSlayerVIP5, maxSlayerVIP5);
                int SlayerBossVIP6 = Kernel.Random.Next(minSlayerVIP6, maxSlayerVIP6);
                int SlayerBossVIP7 = Kernel.Random.Next(minSlayerVIP7, maxSlayerVIP7);
                
                if (killer.VIPLevel == 7)
                {
                    
                    killer.AddRevoraPoints((uint)SlayerBossVIP7);
                    Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " has killed " + Name + " and dropped fabulous prizes on the ground + " + SlayerBossVIP7 + " [Slayer] points!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                    Network.GamePackets._String str = new _String(true);
                    str.UID = killer.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("cortege");
                    killer.Owner.SendScreen(str, true);
                    #region Item VIP7
                    uint ItemID = 0;
                    byte type1 = 2;
                    for (int i = 0; i < 15; i++)// 4 drop count 
                    {
                        type1 = (byte)Kernel.Random.Next(1, 17);
                        switch (type1)
                        {
                            case 1:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 2:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 3:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 4:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 5:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 6:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 7:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 8:
                                ItemID = 3004245;// RefinedSacredPiece
                                break;
                            case 9:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 10:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 11:
                                ItemID = 3009002; //SplendidStarStone
                                break;
                            case 12:
                                ItemID = 723721;// Lvl 9 Wallet
                                break;
                            case 13:
                                ItemID = 729912; // 20kk Cps
                                break;
                            case 14:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 15:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 16:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 17:
                                ItemID = 3306370; //yellow rune fragments
                                break;
                            case 18:
                                ItemID = 729912; // 20kk Cps
                                break;
                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = ItemID;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = ItemID;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                        }
                    }
#endregion
                }
                if (killer.VIPLevel == 6)
                {
                    
                    killer.AddRevoraPoints((uint)SlayerBossVIP6);
                    Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " and Boot Fabulous Ground Awards + " + SlayerBossVIP6 + " Points [Slayer]!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                    Network.GamePackets._String str = new _String(true);
                    str.UID = killer.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("cortege");
                    killer.Owner.SendScreen(str, true);
                    #region Item VIP 6
                    uint ItemID = 0;
                    byte type1 = 2;
                    for (int i = 0; i < 10; i++)// 4 drop count 
                    {
                        type1 = (byte)Kernel.Random.Next(1, 17);
                        switch (type1)
                        {
                            case 1:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 2:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 3:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 4:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 5:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 6:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 7:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 8:
                                ItemID = 3004245;// RefinedSacredPiece
                                break;
                            case 9:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 10:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 11:
                                ItemID = 3009001; //SplendidStarStone
                                break;
                            case 12:
                                ItemID = 723721;// Lvl 9 Wallet
                                break;
                            case 13:
                                ItemID = 729912; // 20kk Cps
                                break;
                            case 14:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 15:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 16:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 17:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = ItemID;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = ItemID;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                        }
                    }
                    #endregion
                }
                if (killer.VIPLevel <= 5)
                {
                    killer.AddRevoraPoints((uint)SlayerBossVIP5);
                    Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + "Has killed " + Name + " and Boot Fabulous Ground Awards + " + SlayerBossVIP5 + " Points [Slayer]!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                    Network.GamePackets._String str = new _String(true);
                    str.UID = killer.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("cortege");
                    killer.Owner.SendScreen(str, true);
                    #region Item VIP 5
                    uint ItemID = 0;
                    byte type1 = 2;
                    for (int i = 0; i < 5; i++)// 4 drop count 
                    {
                        type1 = (byte)Kernel.Random.Next(1, 17);
                        switch (type1)
                        {
                            case 1:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 2:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 3:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 4:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 5:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 6:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 7:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 8:
                                ItemID = 3004245;// RefinedSacredPiece
                                break;
                            case 9:
                                ItemID = 3009002; //RadiantStarStone
                                break;
                            case 10:
                                ItemID = 3004246; //HolyStone
                                break;
                            case 11:
                                ItemID = 3009001; //SplendidStarStone
                                break;
                            case 12:
                                ItemID = 723721;// Lvl 9 Wallet
                                break;
                            case 13:
                                ItemID = 729912; // 20kk Cps
                                break;
                            case 14:
                                ItemID = 3004243; //HolySoulPieceE7
                                break;
                            case 15:
                                ItemID = 3004244; //EquipmentSoulPieceE7
                                break;
                            case 16:
                                ItemID = 3004245;// RefinedSacredPiece 3004246
                                break;
                            case 17:
                                ItemID = 3009001; //RadiantStarStone
                                break;
                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = ItemID;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = ItemID;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                        }
                    }
                    #endregion
                }
                return;
            }

            #endregion
            #region Devil of Soul
            if (Name == "SunDevil")
            {//Chavarro
                uint Uid = 0;
                byte type1 = 2;
                killer.AddConquerPoints(Revora.Config.SoulDevilCp);
                killer.AdjustMoney((decimal)(Revora.Config.SoulDevilGold));
                killer.AddRevoraPoints(Revora.Config.SoulDevilRevoraPoints);
                for (int i = 0; i < 8; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 15);
                    switch (type1)
                    {
                        case 1:
                            Uid = 3004247;
                            break;
                        case 2:
                            Uid = 3004248;
                            break;
                        case 3:
                            Uid = 3004249;
                            break;
                        case 4:
                            Uid = 720121;
                            break;
                        case 5:
                            Uid = 3009003;
                            break;
                        case 6:
                            Uid = 3004247;
                            break;
                        case 7:
                            Uid = 3004248;
                            break;
                        case 8:
                            Uid = 3004249;
                            break;
                        case 9:
                            Uid = 720121;
                            break;
                        case 10:
                            Uid = 3009003;
                            break;
                        case 11:
                            Uid = 3004247;
                            break;
                        case 12:
                            Uid = 3004248;
                            break;
                        case 13:
                            Uid = 3004249;
                            break;
                        case 14:
                            Uid = 720121;
                            break;
                        case 15:
                            Uid = 3009003;
                            break;
                    }
                     var infos = Database.ConquerItemInformation.BaseInformations[Uid];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = Uid;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = Uid;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                        floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                        Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " Prize + 200,000,000 Cps + 500,000,000 Gold Collect the Fabulous Prizes", System.Drawing.Color.White, Message.Center), Server.GamePool);

                    }
                }
                return;
            }
            #endregion            
            
            #region GM Show
            // WhiteDragon leftover 100,000,000 CP purse is cut.
            #endregion
            #endregion
            #region SuperBoss of cities
            // Leftover 5,000,000 CP / 15,000,000 gold purse is cut. These bosses
            // take the normal kill-CP and drop path. Cup-quest tokens still grant.
            if (Name == "HorrifyingSpecter" || Name == "PirateKing"
                || Name == "Cerberus" || Name == "BlackTurtle"
                || Name == "AzureBird" || Name == "ImmortalSoldier")
            {
                if (killer.TareaCopa == 2 && killer.MapID == 1002)
                    killer.Owner.Inventory.Add(3300131, 0, 1);
                if (killer.TareaCopa == 3 && killer.MapID == 1011)
                    killer.Owner.Inventory.Add(3300132, 0, 1);
                if (killer.TareaCopa == 4 && killer.MapID == 1020)
                    killer.Owner.Inventory.Add(3300133, 0, 1);
                if (killer.TareaCopa == 5 && killer.MapID == 1000)
                    killer.Owner.Inventory.Add(3300134, 0, 1);
                if (killer.TareaCopa == 6 && killer.MapID == 1015)
                    killer.Owner.Inventory.Add(3300135, 0, 1);
            }
            #endregion
            //
            #region GreatPheasant(Nor.1)
            // Leftover 2,000,000,000 CP purse is cut.
            #endregion

            #region Slayer soldier
            if (Name == "SlayerSoldier")
                Kernel.SoldadoSlayer1 = false;
            #endregion

            // Easter Holy Week
            #region Easter dragon quest
            #region dragon
            if (killer.MapID == 1002)
            //{//Chavarro
            //    uint Uid = 0;
            //    byte type1 = 2;
                
            //    if (Kernel.Rate(5))
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            type1 = (byte)Kernel.Random.Next(1, 2);
            //            switch (type1)
            //            {
            //                case 1:
            //                    Uid = 710060;//paquete santo
            //                    break;
            //                case 2:
            //                    Uid = 710060;// billetera
            //                    break;
            //            }
            //            var infos = Database.ConquerItemInformation.BaseInformations[Uid];
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (Map.SelectCoordonates(ref X, ref Y))
            //            {
            //                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                floorItem.Item.ID = Uid;
            //                floorItem.Item.Plus = floorItem.Item.Plus;
            //                floorItem.Item.MaximDurability = infos.Durability;
            //                floorItem.Item.Durability = infos.Durability;
            //                floorItem.Item.MobDropped = true;
            //                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                floorItem.ItemID = Uid;
            //                floorItem.MapID = Owner.MapID;
            //                floorItem.MapObjType = Game.MapObjectType.Item;
            //                floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
            //                floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
            //                floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                floorItem.OnFloor = Time32.Now;
            //                floorItem.ItemColor = floorItem.Item.Color;
            //                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                while (Map.Npcs.ContainsKey(floorItem.UID))
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                Map.AddFloorItem(floorItem);
            //                SendScreenSpawn(floorItem);
                            
            //            }
            //        }
            //    }
            //    return;
            //}
            #endregion
            #region Phoenix
            //if (killer.MapID == 1011)
            //{//Chavarro
            //    uint Uid = 0;
            //    byte type1 = 2;

            //    if (Kernel.Rate(5))
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            type1 = (byte)Kernel.Random.Next(1, 2);
            //            switch (type1)
            //            {
            //                case 1:
            //                    Uid = 710061;//paquete santo
            //                    break;
            //                case 2:
            //                    Uid = 710061;// billetera
            //                    break;
            //            }
            //            var infos = Database.ConquerItemInformation.BaseInformations[Uid];
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (Map.SelectCoordonates(ref X, ref Y))
            //            {
            //                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                floorItem.Item.ID = Uid;
            //                floorItem.Item.Plus = floorItem.Item.Plus;
            //                floorItem.Item.MaximDurability = infos.Durability;
            //                floorItem.Item.Durability = infos.Durability;
            //                floorItem.Item.MobDropped = true;
            //                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                floorItem.ItemID = Uid;
            //                floorItem.MapID = Owner.MapID;
            //                floorItem.MapObjType = Game.MapObjectType.Item;
            //                floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
            //                floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
            //                floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                floorItem.OnFloor = Time32.Now;
            //                floorItem.ItemColor = floorItem.Item.Color;
            //                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                while (Map.Npcs.ContainsKey(floorItem.UID))
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                Map.AddFloorItem(floorItem);
            //                SendScreenSpawn(floorItem);

            //            }
            //        }
            //    }
            //    return;
            //}
            #endregion
            #region Tiger
            //if (killer.MapID == 1020)
            //{//Chavarro
            //    uint Uid = 0;
            //    byte type1 = 2;

            //    if (Kernel.Rate(5))
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            type1 = (byte)Kernel.Random.Next(1, 2);
            //            switch (type1)
            //            {
            //                case 1:
            //                    Uid = 710062;//paquete santo
            //                    break;
            //                case 2:
            //                    Uid = 710062;// billetera
            //                    break;
            //            }
            //            var infos = Database.ConquerItemInformation.BaseInformations[Uid];
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (Map.SelectCoordonates(ref X, ref Y))
            //            {
            //                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                floorItem.Item.ID = Uid;
            //                floorItem.Item.Plus = floorItem.Item.Plus;
            //                floorItem.Item.MaximDurability = infos.Durability;
            //                floorItem.Item.Durability = infos.Durability;
            //                floorItem.Item.MobDropped = true;
            //                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                floorItem.ItemID = Uid;
            //                floorItem.MapID = Owner.MapID;
            //                floorItem.MapObjType = Game.MapObjectType.Item;
            //                floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
            //                floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
            //                floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                floorItem.OnFloor = Time32.Now;
            //                floorItem.ItemColor = floorItem.Item.Color;
            //                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                while (Map.Npcs.ContainsKey(floorItem.UID))
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                Map.AddFloorItem(floorItem);
            //                SendScreenSpawn(floorItem);

            //            }
            //        }
            //    }
            //    return;
            //}
            #endregion
            #region cloud
            //if (killer.MapID == 1000)
            //{//Chavarro
            //    uint Uid = 0;
            //    byte type1 = 2;

            //    if (Kernel.Rate(5))
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            type1 = (byte)Kernel.Random.Next(1, 2);
            //            switch (type1)
            //            {
            //                case 1:
            //                    Uid = 710063;//paquete santo
            //                    break;
            //                case 2:
            //                    Uid = 710063;// billetera
            //                    break;
            //            }
            //            var infos = Database.ConquerItemInformation.BaseInformations[Uid];
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (Map.SelectCoordonates(ref X, ref Y))
            //            {
            //                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                floorItem.Item.ID = Uid;
            //                floorItem.Item.Plus = floorItem.Item.Plus;
            //                floorItem.Item.MaximDurability = infos.Durability;
            //                floorItem.Item.Durability = infos.Durability;
            //                floorItem.Item.MobDropped = true;
            //                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                floorItem.ItemID = Uid;
            //                floorItem.MapID = Owner.MapID;
            //                floorItem.MapObjType = Game.MapObjectType.Item;
            //                floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
            //                floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
            //                floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                floorItem.OnFloor = Time32.Now;
            //                floorItem.ItemColor = floorItem.Item.Color;
            //                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                while (Map.Npcs.ContainsKey(floorItem.UID))
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                Map.AddFloorItem(floorItem);
            //                SendScreenSpawn(floorItem);

            //            }
            //        }
            //    }
            //    return;
            //}
            #endregion
            #region bird
            //if (killer.MapID == 1015)
            //{//Chavarro
            //    uint Uid = 0;
            //    byte type1 = 2;

            //    if (Kernel.Rate(5))
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            type1 = (byte)Kernel.Random.Next(1, 2);
            //            switch (type1)
            //            {
            //                case 1:
            //                    Uid = 710064;//paquete santo
            //                    break;
            //                case 2:
            //                    Uid = 710064;// billetera
            //                    break;
            //            }
            //            var infos = Database.ConquerItemInformation.BaseInformations[Uid];
            //            ushort X = Owner.X, Y = Owner.Y;
            //            Game.Map Map = Kernel.Maps[Owner.MapID];
            //            if (Map.SelectCoordonates(ref X, ref Y))
            //            {
            //                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                floorItem.Item.ID = Uid;
            //                floorItem.Item.Plus = floorItem.Item.Plus;
            //                floorItem.Item.MaximDurability = infos.Durability;
            //                floorItem.Item.Durability = infos.Durability;
            //                floorItem.Item.MobDropped = true;
            //                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                floorItem.ItemID = Uid;
            //                floorItem.MapID = Owner.MapID;
            //                floorItem.MapObjType = Game.MapObjectType.Item;
            //                floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
            //                floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
            //                floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                floorItem.OnFloor = Time32.Now;
            //                floorItem.ItemColor = floorItem.Item.Color;
            //                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                while (Map.Npcs.ContainsKey(floorItem.UID))
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                Map.AddFloorItem(floorItem);
            //                SendScreenSpawn(floorItem);

            //            }
            //        }
            //    }
            //    return;
            //}
            #endregion
            #endregion
            //saqueo
            #region SuperBoss Loot 100%
                #region [Looting Show]
                if (Name == "CursedChief")
            {//Chavarro
                uint ItemID = 0;
                byte type1 = 2;
                for (int i = 0; i < 1; i++)// 2 drop count 
                {
                    type1 = (byte)Kernel.Random.Next(1, 9);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3004243; //HolySoulPieceE7
                            break;
                        case 2:
                            ItemID = 3004244; //EquipmentSoulPieceE7
                            break;
                        case 3:
                            ItemID = 3004245;// RefinedSacredPiece
                            break;
                        case 4:
                            ItemID = 3009002; //RadiantStarStone
                            break;
                        case 5:
                            ItemID = 3004243; //HolySoulPieceE7
                            break;
                        case 6:
                            ItemID = 3004244; //EquipmentSoulPieceE7
                            break;
                        case 7:
                            ItemID = 3004245;// RefinedSacredPiece
                            break;
                        case 8:
                            ItemID = 3009001; //RadiantStarStone
                            break;
                        case 9:
                            ItemID = 720121; //SplendidStarStone
                            break;

                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (DateTime.Now.Hour == 14 && DateTime.Now.Minute >= 00 && DateTime.Now.Minute <= 30)
                    {
                        if (Kernel.Rate(15))// higher rate drops faster 
                        {
                            
                            killer.AdjustMoney((decimal)(500000));
                            killer.AddRevoraPoints(100);
                            killer.Owner.Send(new Network.GamePackets.Message("[PACKAGE] You Killed" + Owner.Name + " Collect 500,000 Money, + 100 Revora Points", System.Drawing.Color.DarkBlue, 255));

                            if (Map.SelectCoordonates(ref X, ref Y))
                            {
                                Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                                floorItem.Item = new Network.GamePackets.ConquerItem(true);
                                floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                                floorItem.Item.ID = ItemID;
                                floorItem.Item.Plus = floorItem.Item.Plus;
                                floorItem.Item.MaximDurability = infos.Durability;
                                floorItem.Item.Durability = infos.Durability;
                                floorItem.Item.MobDropped = true;
                                floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                                floorItem.ItemID = ItemID;
                                floorItem.MapID = Owner.MapID;
                                floorItem.MapObjType = Game.MapObjectType.Item;
                                floorItem.X = X;
                                floorItem.Y = Y;
                                floorItem.Type = Network.GamePackets.FloorItem.Drop;
                                floorItem.OnFloor = Time32.Now;
                                floorItem.ItemColor = floorItem.Item.Color;
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                                while (Map.Npcs.ContainsKey(floorItem.UID))
                                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                                Map.AddFloorItem(floorItem);
                                SendScreenSpawn(floorItem);
                                // Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " has killed " + Name + " and drops " + Database.ConquerItemInformation.BaseInformations[ItemID].Name + " and 2.000.000  CPs + 5.000.000 Gold + 100 [Slayer] points!", System.Drawing.Color.White, Message.Center), Server.GamePool);

                            }
                        }
                    }
                    else
                    {
                        killer.Owner.Send(new Network.GamePackets.Message("[PUNCH FINISH] You Killed " + Owner.Name + " BUT IT DOES NOTHING BECAUSE TIME IS ALREADY OVER", System.Drawing.Color.DarkBlue, 255));
                    
                    }
                }
                return;
            }

            #endregion

            #endregion
            ////
            #region B-O-Y-A-C-A quest
            //if (Name == "Birdman" || Name == "BirdmanKing" || Name == "BirdmanL88" || Name == "BirdmanAide" || Name == "HawKing" || Name == "2093")
            //{//Chavarro
            //    uint ItemID = 0;
            //    byte type1 = 2;
            //    for (int i = 0; i < 1; i++)// 2 drop count 
            //    {
            //        type1 = (byte)Kernel.Random.Next(1, 9);
            //        switch (type1)
            //        {
            //            case 1:
            //                ItemID = 3006098; //HolySoulPieceE7
            //                break;
            //            case 2:
            //                ItemID = 3006099; //EquipmentSoulPieceE7
            //                break;
            //            case 3:
            //                ItemID = 3006100;// RefinedSacredPiece
            //                break;
            //            case 4:
            //                ItemID = 3006101; //RadiantStarStone
            //                break;
            //            case 5:
            //                ItemID = 3006102; //HolySoulPieceE7
            //                break;
            //            case 6:
            //                ItemID = 3006098; //HolySoulPieceE7
            //                break;
            //            case 7:
            //                ItemID = 3006099; //EquipmentSoulPieceE7
            //                break;
            //            case 8:
            //                ItemID = 3006100;// RefinedSacredPiece
            //                break;
            //            case 9:
            //                ItemID = 3006101; //RadiantStarStone
            //                break;



            //        }
            //        var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
            //        ushort X = Owner.X, Y = Owner.Y;
            //        Game.Map Map = Kernel.Maps[Owner.MapID];
            //        if (DateTime.Now.Day == 16 || DateTime.Now.Day == 17 || DateTime.Now.Day == 18 || DateTime.Now.Day == 19)
            //        {
            //            if (Kernel.Rate(20))// higher rate drops faster 
            //            {
            //                if (Map.SelectCoordonates(ref X, ref Y))
            //                {
            //                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
            //                    floorItem.Item = new Network.GamePackets.ConquerItem(true);
            //                    floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
            //                    floorItem.Item.ID = ItemID;
            //                    floorItem.Item.Plus = floorItem.Item.Plus;
            //                    floorItem.Item.MaximDurability = infos.Durability;
            //                    floorItem.Item.Durability = infos.Durability;
            //                    floorItem.Item.MobDropped = true;
            //                    floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
            //                    floorItem.ItemID = ItemID;
            //                    floorItem.MapID = Owner.MapID;
            //                    floorItem.MapObjType = Game.MapObjectType.Item;
            //                    floorItem.X = X;
            //                    floorItem.Y = Y;
            //                    floorItem.Type = Network.GamePackets.FloorItem.Drop;
            //                    floorItem.OnFloor = Time32.Now;
            //                    floorItem.ItemColor = floorItem.Item.Color;
            //                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                    while (Map.Npcs.ContainsKey(floorItem.UID))
            //                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
            //                    Map.AddFloorItem(floorItem);
            //                    SendScreenSpawn(floorItem);

            //                }
            //            }
            //        }


            //    }
            //    return;
            //}

            #endregion

            ////// SLAYER CUP QUEST
            #region CUP stage
            if (killer.MapID == 1819)
            {
                if (Name == "TombRaider")
                {
                    if (killer.JadeDragonPendant != 30 )
                    {
                        if (Kernel.Rate(30, 1000))
                        {
                            if (killer.Owner.Inventory.Count != 40)
                            {
                                Random Nr = new Random();
                                int R = Nr.Next(0, 3);
                                if (R == 1)
                                {
                                    killer.Owner.Inventory.Add(3200541, 0, 1);
                                    killer.JadeDragonPendant++;
                                }
                                if (R == 2)
                                {
                                    killer.Owner.Inventory.Add(3200541, 0, 1);
                                    killer.JadeDragonPendant++;
                                }
                            }
                            else
                            {
                                killer.Owner.Send("You have the Inventory Full Take out things ..");
                            }
                            return;
                        }
                    }
                }
                if (Name == "TombRobbingLeader")
                {
                    if ( killer.SupremeJade != 10)
                    {
                        if (Kernel.Rate(30, 1000))
                        {
                            if (killer.Owner.Inventory.Count != 40)
                            {
                                Random Nr = new Random();
                                int R = Nr.Next(0, 3);
                                if (R == 1)
                                {
                                    killer.Owner.Inventory.Add(3200542, 0, 1);
                                    killer.SupremeJade++;
                                }
                                if (R == 2)
                                {
                                    killer.Owner.Inventory.Add(3200542, 0, 1);
                                    killer.SupremeJade++;
                                }
                            }
                            else
                            {
                                killer.Owner.Send("You have the Inventory Full Take out things ..");
                            }
                            return;
                        }
                    }
                }
            }
            #endregion

             #region Secound Quest
            if (!Game.Features.AbyssTrialWorld.SuppressLegacy(Owner, killer)) {
            #region Demons
            #region ValeDemon
            if (Name == "ValeDemon")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "ValeDemon")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 25);
                switch (ban)
                {
                    case 1: b = 722739; break;

                    case 2: b = 722738; break;

                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;

                    case 6: b = 722733; break;

                    case 7: b = 722734; break;

                    case 8: b = 722735; break;

                    case 9: b = 722736; break;



                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region MudDemon
            if (Name == "MudDemon")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "MudDemon")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;

                    case 2: b = 722738; break;

                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;

                    case 6: b = 722733; break;

                    case 7: b = 722734; break;

                    case 8: b = 722735; break;

                    case 9: b = 722736; break;



                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region AbyssDemon
            if (Name == "AbyssDemon")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "AbyssDemon")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region DemonLord
            if (Name == "DemonLord")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest6, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = 722727;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = 722727;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
            }
            if (Name == "DemonLord")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Serpent
            #region SingingSerpent
            if (Name == "SingingSerpent")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "SingingSerpent")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region RoaringSerpent
            if (Name == "RoaringSerpent")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "RoaringSerpent")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region CryingSerpent
            if (Name == "CryingSerpent")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "CryingSerpent")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region SerpentSpirit
            if (Name == "SerpentSpirit")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 9);
                    switch (ban)
                    {
                        case 1: b = 722739; break;
                        case 2: b = 722738; break;
                        case 3: b = 722737; break;
                        case 4: b = 0; break;
                        case 5: b = 722732; break;
                        case 6: b = 722733; break;
                        case 7: b = 722734; break;
                        case 8: b = 722735; break;
                        case 9: b = 722736; break;
                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest6, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = 722727;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = 722727;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
            }
            if (Name == "SerpentSpirit")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Fiend
            #region BlueFiend
            if (Name == "BlueFiend")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "BlueFiend")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region RedFiend
            if (Name == "RedFiend")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "RedFiend")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region WhiteFiend
            if (Name == "WhiteFiend")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "WhiteFiend")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region FiendLord
            if (Name == "FiendLord")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest6, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = 722727;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = 722727;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
            }
            if (Name == "FiendLord")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Ape
            #region WindApe
            if (Name == "WindApe")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "WindApe")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region RainApe
            if (Name == "RainApe")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "RainApe")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region LightningApe
            if (Name == "LightningApe")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "LightningApe")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region FuryApe
            if (Name == "FuryApe")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "FuryApe")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Hades
            #region WoodHades
            if (Name == "WoodHades")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "WoodHades")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region WaterHades
            if (Name == "WaterHades")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "WaterHades")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region EarthHades
            if (Name == "EarthHades")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "EarthHades")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region HadesLord
            if (Name == "HadesLord")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest6, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = 722727;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = 722727;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
            }
            if (Name == "HadesLord")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Bat
            #region FuryBat
            if (Name == "FuryBat")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "FuryBat")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region CruelBat
            if (Name == "CruelBat")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "CruelBat")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region ViciousBat
            if (Name == "ViciousBat")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "ViciousBat")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region DemonBat
            if (Name == "DemonBat")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "DemonBat")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Skeleton
            #region BoneSkeleton
            if (Name == "BoneSkeleton")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "BoneSkeleton")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region LameSkeleton
            if (Name == "LameSkeleton")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "LameSkeleton")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region CarrionSkeleton
            if (Name == "CarrionSkeleton")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "CarrionSkeleton")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region FurySkeleton
            if (Name == "FurySkeleton")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 29);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "FurySkeleton")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #endregion
            #region Beast
            #region AngryBeast
            if (Name == "AngryBeast")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 30);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 1;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "AngryBeast")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region SensitiveBeast
            if (Name == "SensitiveBeast")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 30);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 2;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "SensitiveBeast")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region CrazyBeast
            if (Name == "CrazyBeast")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 70);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 4;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "CrazyBeast")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion
            #region FearlessBeast
            if (Name == "FearlessBeast")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint b = 0;
                    uint ban = (uint)Kernel.Random.Next(1, 6);
                    switch (ban)
                    {
                        case 1: b = 722723; break;
                        case 2: b = 0; break;
                        case 3: b = 722724; break;
                        case 4: b = 0; break;
                        case 5: b = 0; break;
                        case 6: b = 722725; break;
                        case 7: b = 0; break;
                        case 8: b = 722732; break;
                        case 9: b = 0; break;
                        case 10: b = 0; break;
                        case 11: b = 0; break;
                        case 12: b = 722733; break;
                        case 13: b = 0; break;
                        case 14: b = 0; break;
                        case 15: b = 722734; break;
                        case 16: b = 0; break;
                        case 17: b = 0; break;
                        case 18: b = 722735; break;
                        case 19: b = 0; break;
                        case 20: b = 0; break;
                        case 21: b = 722736; break;
                        case 22: b = 0; break;
                        case 23: b = 0; break;
                        case 24: b = 722737; break;
                        case 25: b = 0; break;
                        case 26: b = 0; break;
                        case 27: b = 722738; break;
                        case 28: b = 0; break;
                        case 29: b = 0; break;
                        case 30: b = 722739; break;

                    }
                    ushort X = Owner.X, Y = Owner.Y;
                    Map Map = Kernel.Maps[Owner.MapID];
                    if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                    {
                        FloorItem floorItem = new FloorItem(true);
                        floorItem.Item = new ConquerItem(true);
                        floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = b;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = FloorItem.FloorValueType.Item;
                        floorItem.ItemID = b;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                        //floorItem.Owner = killer.Owner;
                        floorItem.Type = FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 40001)
                {
                    killer.SecoundQuestPoint += 500;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            if (Name == "FearlessBeast")
            {

                uint b = 0;
                uint ban = (uint)Kernel.Random.Next(1, 9);
                switch (ban)
                {
                    case 1: b = 722739; break;
                    case 2: b = 722738; break;
                    case 3: b = 722737; break;
                    case 4: b = 0; break;
                    case 5: b = 722732; break;
                    case 6: b = 722733; break;
                    case 7: b = 722734; break;
                    case 8: b = 722735; break;
                    case 9: b = 722736; break;
                }
                ushort X = Owner.X, Y = Owner.Y;
                Map Map = Kernel.Maps[Owner.MapID];
                if (b!=0 && (!(b==722732 || b==722736) || !Game.Features.NinjaFlowerHunt.Owns(Owner,killer)) && Map.SelectCoordonates(ref X, ref Y))
                {
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.Item = new ConquerItem(true);
                    floorItem.Item.Color = (Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = b;
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                    floorItem.ItemID = b;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Owner = killer.Owner; floorItem.OwnerUID = killer.UID; floorItem.Y = Y;
                    //floorItem.Owner = killer.Owner;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }

            }
            #endregion

            #endregion
            #region Theodore third stage
            if (Name == "Theodore" || Name == "Wayne" || Name == "Victoria" || Name == "Daphne" || Name == "Timothy" || Name == "Philip" || Name == "Peter" || Name == "Andrew")
            {

                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest3, MsgTaskStatus.QuestData.QuestStatus.Accepted) && killer.SecoundQuestPoint < 75)
                {
                    killer.SecoundQuestPoint++;
                    killer.Owner.Quests.IncreaseQuestKills(QuestID.Secound_Quest3, killer.SecoundQuestPoint);
                }
            }
            #endregion
            #region HillSpirit ///722722
            if (Name == "HillSpirit")
            {//Chavarro
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint Uid = 0;
                    byte type1 = 2;

                    for (int i = 0; i < 1; i++)
                    {
                        type1 = (byte)Kernel.Random.Next(1, 7);
                        switch (type1)
                        {
                            case 1:
                                Uid = 722723;//paquete santo
                                break;
                            case 2:
                                Uid = 722724;// billetera
                                break;
                            case 3:
                                Uid = 722725;//paquete lvl 6
                                break;                            
                            case 4:
                                Uid = 722722;
                                break;
                            case 5:
                                Uid = 722723;//paquete santo
                                break;
                            case 6:
                                Uid = 722724;// billetera
                                break;
                            case 7:
                                Uid = 722725;//paquete lvl 6
                                break;
                                

                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[Uid];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = Uid;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = Uid;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " is doing the third stage of the CopaSlayer", System.Drawing.Color.White, Message.Center), Server.GamePool);

                        }
                    }
                    return;
                }
                else
                {
                    Revora.Kernel.SendWorldMessage(new Message("Accept the task first.", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                }
            }
            #endregion            
            #region SwiftDevil ///722726
            if (Name == "SwiftDevil")
            {//Chavarro
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint Uid = 0;
                    byte type1 = 2;

                    for (int i = 0; i < 1; i++)
                    {
                        type1 = (byte)Kernel.Random.Next(1, 7);
                        switch (type1)
                        {
                            case 1:
                                Uid = 722723;//paquete santo
                                break;
                            case 2:
                                Uid = 722724;// billetera
                                break;
                            case 3:
                                Uid = 722725;//paquete lvl 6
                                break;
                            case 4:
                                Uid = 722726;
                                break;
                            case 5:
                                Uid = 722723;//paquete santo
                                break;
                            case 6:
                                Uid = 722724;// billetera
                                break;
                            case 7:
                                Uid = 722725;//paquete lvl 6
                                break;

                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[Uid];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = Uid;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = Uid;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " is doing the third stage of the CopaSlayer", System.Drawing.Color.White, Message.Center), Server.GamePool);

                        }
                    }
                    return;
                }
                else
                {
                    Revora.Kernel.SendWorldMessage(new Message("Accept the task first.", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                }
            }
            #endregion            
            #region  Banshee///722729
            if (Name == "Banshee")
            {//Chavarro
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint Uid = 0;
                    byte type1 = 2;

                    for (int i = 0; i < 1; i++)
                    {
                        type1 = (byte)Kernel.Random.Next(1, 7);
                        switch (type1)
                        {
                            case 1:
                                Uid = 722723;//paquete santo
                                break;
                            case 2:
                                Uid = 722724;// billetera
                                break;
                            case 3:
                                Uid = 722725;//paquete lvl 6
                                break;
                            case 4:
                                Uid = 722729;
                                break;
                            case 5:
                                Uid = 722723;//paquete santo
                                break;
                            case 6:
                                Uid = 722724;// billetera
                                break;
                            case 7:
                                Uid = 722725;//paquete lvl 6
                                break;

                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[Uid];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = Uid;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = Uid;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " is doing the third stage of the CopaSlayer", System.Drawing.Color.White, Message.Center), Server.GamePool);

                        }
                    }
                    return;
                }
                else
                {
                    Revora.Kernel.SendWorldMessage(new Message("Accept the task first.", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                }
            }
            #endregion            
            #region  CleansingDevil///722731
            if (Name == "CleansingDevil")
            {//Chavarro
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    uint Uid = 0;
                    byte type1 = 2;

                    for (int i = 0; i < 1; i++)
                    {
                        type1 = (byte)Kernel.Random.Next(1, 2);
                        switch (type1)
                        {
                            case 1:
                                Uid = 722731;//paquete santo
                                break;
                            case 2:
                                Uid = 722731;// billetera
                                break;
                           

                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[Uid];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = Uid;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = Uid;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = (ushort)Kernel.Random.Next(X - 2, X + 2);
                            floorItem.Y = (ushort)Kernel.Random.Next(Y - 2, Y + 2);
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);

                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed " + Name + " is doing the third stage of the CopaSlayer", System.Drawing.Color.White, Message.Center), Server.GamePool);

                        }
                    }
                    return;
                }
                else
                {
                    Revora.Kernel.SendWorldMessage(new Message("Accept the quest first.", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                }
            }
            #endregion            
            
            
            #region Satan
            if (Name == "Satan")
            {
                if (killer.Owner.Quests.CheckQuest(QuestID.Secound_Quest6, MsgTaskStatus.QuestData.QuestStatus.Accepted))
                {
                    killer.Owner.Quests.FinishQuest(QuestID.Secound_Quest6);
                    killer.SecoundQuestDone = true;
                    killer.Teleport(1036, 200, 200);
                    MsgName str = new MsgName(true);
                    str.UID = killer.UID;
                    str.TextsCount = 1;
                    str.Action = MsgName.Mode.Effect;
                    str.Texts.Add("end_task");
                    killer.Owner.SendScreen(str, true);
                    Revora.Kernel.SendWorldMessage(new Message("Congratulations! " + killer.Name + " Has Finished Secound Quest .", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                }
            }

            #endregion
            #endregion
            /////
            }
            #region DorbItem
            byte morepercent = 0;
            //   byte lessrate = 0;
            if (!Revora.Game.Features.LabyrinthQuest.SuppressLegacyQuestDrop(Owner) && !Game.Features.DailyHuntDrops.OwnsEmeraldDrop(Owner) && Kernel.Rate(Revora.Config.AdjustedItemDropRate(OwnItemRate + morepercent)) && OwnItemID != 0)
            {
                var infos = Database.ConquerItemInformation.BaseInformations[(uint)OwnItemID];
                ushort X = Owner.X, Y = Owner.Y;
                Game.Map Map = Kernel.Maps[Owner.MapID];
                if (Map.SelectCoordonates(ref X, ref Y))
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.Item = new Network.GamePackets.ConquerItem(true);
                    floorItem.Item.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                    floorItem.Item.ID = (uint)OwnItemID;
                    floorItem.Item.MaximDurability = infos.Durability;
                    if (!ItemHandler.IsEquipment(OwnItemID) && infos.ConquerPointsWorth == 0)
                    {
                        floorItem.Item.StackSize = 1;
                        floorItem.Item.MaxStackSize = infos.StackSize;
                    }
                    floorItem.Item.MobDropped = true;
                    floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                    floorItem.ItemID = (uint)OwnItemID;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = Game.MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Y = Y;
                    floorItem.Owner = killer.Owner;
                    floorItem.Type = Network.GamePackets.FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.ItemColor = floorItem.Item.Color;
                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }
            }
            #endregion
            #region DropMoney
            if (Revora.Config.MoneyDropEnabled && Kernel.Rate((int)Revora.Config.MoneyDropRate))
            {
                uint minGold = MinMoneyDropAmount;
                uint maxGold = MaxMoneyDropAmount;
                if (maxGold < minGold)
                    maxGold = minGold;
                uint amount = minGold;
                if (maxGold > minGold)
                    amount = (uint)Kernel.Random.Next((int)minGold, (int)maxGold + 1);
                amount *= Revora.Config.MoneyDropMultiple;
                if (amount > 0)
                {
                    uint ItemID = ItemHandler.MoneyItemID(amount);
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Money;
                        floorItem.Value = amount;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Owner = killer.Owner;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
            }
            /*
            if (Kernel.Rate(Constants.MoneyDropRate - lessrate + morepercent))
            {
                if (killer.MapID == 2102) return;
                uint amount = (uint)Kernel.Random.Next((int)MinMoneyDropAmount, (int)MaxMoneyDropAmount);
                amount *= Constants.MoneyDropMultiple;
                if (amount > 1000000)
                    amount = 6000000;
                if (amount == 0) return;
                uint ItemID = ItemHandler.MoneyItemID(amount);
                ushort X = Owner.X, Y = Owner.Y;
                Game.Map Map = Kernel.Maps[Owner.MapID];
                if (Map.SelectCoordonates(ref X, ref Y))
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Money;
                    floorItem.Value = amount;
                    floorItem.ItemID = ItemID;
                    floorItem.MapID = Owner.MapID;
                    floorItem.MapObjType = Game.MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Y = Y;
                    floorItem.Type = Network.GamePackets.FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    SendScreenSpawn(floorItem);
                }
            }*/
            #endregion
            #region DropMoney[Official]
            /*if (Kernel.Rate(Constants.MoneyDropRate))
            {
                if (Kernel.Rate(50))
                {
                    uint amount = (uint)Kernel.Random.Next(Level * 2, Level * 6);
                    if (Level >= 0 && Level <= 5)
                        amount = (uint)Kernel.Random.Next(1, 100);
                    if (amount > 1000000)
                        amount = 6000000;
                    //uint ItemID = Network.PacketHandler.MoneyItemID(amount);
                    uint ItemID = ItemHandler.MoneyItemID(amount);
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Money;
                        floorItem.Value = amount;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.OwnerUID = killer.UID;
                        floorItem.Owner = killer.Owner;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                else
                {
                    int r = Kernel.Random.Next(1, 5);
                    for (int x = 0; x < r; x++)
                    {
                        uint amount = (uint)Kernel.Random.Next(Level * 2, Level * 6);
                        if (Level >= 0 && Level <= 5)
                            amount = (uint)Kernel.Random.Next(1, 100);
                        if (amount > 1000000)
                            amount = 6000000;
                        //uint ItemID = Network.PacketHandler.MoneyItemID(amount);
                        uint ItemID = ItemHandler.MoneyItemID(amount);
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Money;
                            floorItem.Value = amount;
                            floorItem.ItemID = ItemID;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = X;
                            floorItem.Y = Y;
                            floorItem.Owner = killer.Owner;
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))
                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            Map.AddFloorItem(floorItem);
                            SendScreenSpawn(floorItem);
                        }
                    }
                }
            }*/
            #endregion
            #region DemonBox
            if (killer.EntityFlag == Game.EntityFlag.Player || killer.Companion)
            {
                uint cps = 0;
                if (Name == "Demon")
                {
                    if (Kernel.Rate(30, 100)) cps += 10;
                    else if (Kernel.Rate(40, 40)) cps += 10;
                    else if (Kernel.Rate(70, 60)) cps += 0;
                    else if (Kernel.Rate(80, 80)) cps += 11;
                    else cps += 10;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
                if (Name == "AncientDemon")
                {
                    if (Kernel.Rate(30, 1000)) cps += 50;
                    else if (Kernel.Rate(40, 1000)) cps += 50;
                    else if (Kernel.Rate(70, 1000)) cps += 0;
                    else if (Kernel.Rate(80, 1000)) cps += 51;
                    else cps += 50;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
                if (Name == "FloodDemon")
                {
                    if (Kernel.Rate(30, 1000)) cps += 100;
                    else if (Kernel.Rate(40, 1000)) cps += 10;
                    else if (Kernel.Rate(70, 1000)) cps += 0;
                    else if (Kernel.Rate(80, 1000)) cps += 101;
                    else cps += 100;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
                if (Name == "HeavenDemon")
                {
                    if (Kernel.Rate(30, 1000)) cps += 500;
                    else if (Kernel.Rate(40, 1000)) cps += 500;
                    else if (Kernel.Rate(70, 1000)) cps += 0;
                    else if (Kernel.Rate(80, 1000)) cps += 501;
                    else cps += 500;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
                if (Name == "ChaosDemon")
                {
                    if (Kernel.Rate(30, 1000)) cps += 1000;
                    else if (Kernel.Rate(40, 1000)) cps += 1000;
                    else if (Kernel.Rate(70, 1000)) cps += 0;
                    else if (Kernel.Rate(80, 1000)) cps += 1001;
                    else cps += 1000;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
                if (Name == "ScaredDemon")
                {
                    if (Kernel.Rate(30, 1000)) cps += 1200;
                    else if (Kernel.Rate(40, 1000)) cps += 1200;
                    else if (Kernel.Rate(70, 1000)) cps += 0;
                    else if (Kernel.Rate(80, 1000)) cps += 1201;
                    else cps += 1200;
                    killer.Owner.Entity.AddConquerPoints(cps);
                }
            }
            #endregion
            #region Chi monster
            if (ID == 7902 || Name == "MostroDeChi" || Name == "Chi Beast")
            {
                uint ItemID = 3000060;
                for (int i = 0; i < 10; i++)
                {
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                        floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                return;
            }

            #endregion
            #region Jiang monster
            if (ID == 2648 || Name == "MostroDeJian" || Name == "Jiang Hu Beast")
            {
                uint ItemID = 3000061;
                for (int i = 0; i < 20; i++)
                {
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                        floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);
                    }
                }
                return;
            }

            #endregion
            #region Monsters [Perfection system] quest
            if (Name == "CoinsStealer")
            {
                Random randoms = new Random();
                int r = randoms.Next(1, 30);
                if (r == 5)
                {
                    killer.Owner.Inventory.AddTime(711609);
                    killer.Owner.Send(new Message("Congratulations! You have earned 1 GoldCoin", Message.System));
                }
                else if (r == 10)
                {
                    killer.Owner.Inventory.AddTime(711610);
                    killer.Owner.Send(new Message("Congratulations! You have obtained 1 SilverCoin", Message.System));
                }
                else if (r == 15)
                {
                    killer.Owner.Inventory.AddTime(711611);
                    killer.Owner.Send(new Message("Congratulations! You have earned 1 CopperCoin ", Message.System));
                }
                return;
            }
            if ((Kernel.Rate(20)) &&
                  (Name == "Naga")  || (Name == "Tentadora")
                || (Name == "Centinela")
              )
            {
                uint ItemID = 0;
                byte type1 = 20;
                for (int i = 0; i < 1; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 5);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3009001;//unique perfection stone
                            break;
                        case 2:
                            ItemID = 3009001;
                            break;
                        case 3:
                            ItemID = 3009001;//cestas
                            break;
                        case 4:
                            ItemID = 3009001;//oro
                            break;
                        case 5:
                            ItemID = 3009001;//piedra antigua
                            break;
                        
                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                }

            }
            if (Name == "Naga")
            {
                killer.DisKO += 1;
                killer.Owner.Send(new Message("Congratulations! You have killed the Perfection Task monster and you collect a soul. You carry [" + killer.DisKO + "]  Souls", System.Drawing.Color.Red, Message.TopLeft));
                return;
            }
            if (Name == "Tentadora")
            {
                killer.DisKO += 1;
                killer.Owner.Send(new Message("Congratulations! You have killed the Perfection Task monster and you collect a soul. You carry [" + killer.DisKO + "]  Souls", System.Drawing.Color.Red, Message.TopLeft));
                return;
            }
            if (Name == "Centinela")
            {
                killer.DisKO += 1;
                killer.Owner.Send(new Message("Congratulations! You have killed the Perfection Task monster and you collect a soul. You carry [" + killer.DisKO + "]  Souls", System.Drawing.Color.Red, Message.TopLeft));
                return;
            }
            if (Name == "TrollInfiernal")
            {
                killer.DisKO += 1;
                killer.Owner.Send(new Message("Congratulations! You have killed the Perfection Task monster and you collect a soul. You carry [" + killer.DisKO + "]  Souls", System.Drawing.Color.Red, Message.TopLeft));
                return;
            }
            if ((Kernel.Rate(20)) && Name == "TrollInfiernal")
            {
                uint ItemID = 0;
                byte type1 = 20;
                for (int i = 0; i < 1; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 5);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3009001;//unique perfection stone
                            break;
                        case 2:
                            ItemID = 3009001;
                            break;
                        case 3:
                            ItemID = 3009001;//cestas
                            break;
                        case 4:
                            ItemID = 3009001;//oro
                            break;
                        case 5:
                            ItemID = 3009001;//piedra antigua
                            break;
                        



                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                }

            }

            ///Perfection monster///
            #region UltimatePluto
            #region Pluto ground items
            if (Name == "PlutoDerecho" || Name == "PlutoIzquierdo" || Name == "PlutoCentro")
            {//Chavarro
                uint ItemID = 0;
                byte type1 = 2;
                for (int i = 0; i < 10; i++)// 4 drop count 
                {
                    type1 = (byte)Kernel.Random.Next(1, 8);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3009003; //HolySoulPieceE7
                            break;
                        case 2:
                            ItemID = 3009002; //EquipmentSoulPieceE7
                            break;
                        case 3:
                            ItemID = 3009001;// RefinedSacredPiece
                            break;
                        case 4:
                            ItemID = 3009002; //RadiantStarStone
                            break;
                        case 5:
                            ItemID = 3009003; //HolySoulPieceE7
                            break;
                        case 6:
                            ItemID = 3009001; //EquipmentSoulPieceE7
                            break;
                        case 7:
                            ItemID = 3009002;// RefinedSacredPiece
                            break;
                        case 8:
                            ItemID = 3009003; //RadiantStarStone
                            break;
                        case 9:
                            ItemID = 3009002; //SplendidStarStone
                            break;
                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = (ushort)Kernel.Random.Next(X - 10, X + 10);
                        floorItem.Y = (ushort)Kernel.Random.Next(Y - 10, Y + 10);
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        SendScreenSpawn(floorItem);

                    }
                }
                return;
            }

            #endregion
            #region Plutos drop
            if (Name == "PlutoDerecho" || Name == "PlutoIzquierdo" || Name == "PlutoCentro")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 6; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3009003;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3009003, 0, 1);
                            killer.Owner.Inventory.Add(3009002, 0, 1);
                            killer.AddRevoraPoints(50000);
                            Game.Entity.name = new object[] { "[PerfectionBoss]" + killer.Name + " Has killed " + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " + 50.000 Revora Points " };
                            Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "Perfection", System.Drawing.Color.Red, 2500), Server.GamePool);
                              
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #endregion

            #endregion
            #region DemonsLotto [New Year's Event]
            if (Name == "LotteryDemons")
            {
                uint ItemID = 0;
                byte type1 = 20;
                for (int i = 0; i < 1; i++)
                {
                    if (Kernel.Rate(30, 500))
                    {
                        type1 = (byte)Kernel.Random.Next(1, 20);
                        switch (type1)
                        {
                            case 1:
                                ItemID = 728710;
                                break;
                            case 2:
                                ItemID = 728711;
                                break;
                            case 3:
                                ItemID = 728712;
                                break;
                            case 4:
                                ItemID = 728713;
                                break;
                            case 5:
                                ItemID = 728714;
                                break;
                            case 6:
                                ItemID = 728715;
                                break;
                            case 7:
                                ItemID = 728716;
                                break;
                            case 8:
                                ItemID = 728717;
                                break;
                            case 9:
                                ItemID = 728718;
                                break;
                            case 10:
                                ItemID = 728719;
                                break;
                            case 11:
                                ItemID = 728710;
                                break;
                            case 12:
                                ItemID = 728711;
                                break;
                            case 13:
                                ItemID = 728712;
                                break;
                            case 14:
                                ItemID = 728713;
                                break;
                            case 15:
                                ItemID = 728714;
                                break;
                            case 16:
                                ItemID = 728715;
                                break;
                            case 17:
                                ItemID = 728716;
                                break;
                            case 18:
                                ItemID = 728717;
                                break;
                            case 19:
                                ItemID = 728718;
                                break;
                            case 20:
                                ItemID = 728719;
                                break;

                        }
                        var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        if (Map.SelectCoordonates(ref X, ref Y))
                        {
                            Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                            floorItem.Item = new Network.GamePackets.ConquerItem(true);
                            floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                            floorItem.Item.ID = ItemID;
                            floorItem.Item.Plus = floorItem.Item.Plus;
                            floorItem.Item.MaximDurability = infos.Durability;
                            floorItem.Item.Durability = infos.Durability;
                            floorItem.Item.MobDropped = true;
                            floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                            floorItem.ItemID = ItemID;
                            floorItem.MapID = Owner.MapID;
                            floorItem.MapObjType = Game.MapObjectType.Item;
                            floorItem.X = X;
                            floorItem.Y = Y;
                            floorItem.Type = Network.GamePackets.FloorItem.Drop;
                            floorItem.OnFloor = Time32.Now;
                            floorItem.ItemColor = floorItem.Item.Color;
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                            while (Map.Npcs.ContainsKey(floorItem.UID))

                                floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                            Map.AddFloorItem(floorItem);

                            SendScreenSpawn(floorItem);
                        }
                    }
                }
               
            }
            #endregion
            #region MinotaurL120[Dragon balls]
            if (Name == "MinotaurL120" && Kernel.Rate(30))
            {
                uint ItemID = 0;
                byte type1 = 20;
                for (int i = 0; i < 1; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 14);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 3000502;
                            break;
                        case 2:
                            ItemID = 3000503;
                            break;
                        case 3:
                            ItemID = 3000504;
                            break;
                        case 4:
                            ItemID = 3000505;
                            break;
                        case 5:
                            ItemID = 3000506;
                            break;
                        case 6:
                            ItemID = 3000507;
                            break;
                        case 7:
                            ItemID = 3000508;
                            break;
                        case 8:
                            ItemID = 3000502;
                            break;
                        case 9:
                            ItemID = 3000503;
                            break;
                        case 10:
                            ItemID = 3000504;
                            break;
                        case 11:
                            ItemID = 3000505;
                            break;
                        case 12:
                            ItemID = 3000506;
                            break;
                        case 13:
                            ItemID = 3000507;
                            break;
                        case 14:
                            ItemID = 3000508;
                            break;


                    }
                    var infos = Database.ConquerItemInformation.BaseInformations[ItemID];
                    ushort X = Owner.X, Y = Owner.Y;
                    Game.Map Map = Kernel.Maps[Owner.MapID];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.Item = new Network.GamePackets.ConquerItem(true);
                        floorItem.Item.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                        floorItem.Item.ID = ItemID;
                        floorItem.Item.Plus = floorItem.Item.Plus;
                        floorItem.Item.MaximDurability = infos.Durability;
                        floorItem.Item.Durability = infos.Durability;
                        floorItem.Item.MobDropped = true;
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.Item;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = Owner.MapID;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.ItemColor = floorItem.Item.Color;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))

                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;

                        Map.AddFloorItem(floorItem);

                        SendScreenSpawn(floorItem);
                    }
                }

            }
            #endregion

            #region InnerPower monsters [Bosses]
            #region MingDemon
            if (Name == "MingDemon")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3005365;
                            Uid = 3005366;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3005365, 0, 1);
                            killer.Owner.Inventory.Add(3005366, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region DarkKingGhost
            if (Name == "DarkKingGhost")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3005395;
                            Uid = 3005396;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3005395, 0, 1);
                            killer.Owner.Inventory.Add(3005396, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region AlienBeast
            if (Name == "AlienBeast")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3007113;
                            Uid = 3007114;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3007113, 0, 1);
                            killer.Owner.Inventory.Add(3007114, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region ImmortalWarrior
            if (Name == "ImmortalWarrior")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3005397;
                            Uid = 3005398;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3005397, 0, 1);
                            killer.Owner.Inventory.Add(3005398, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region DoubleAxeBeast
            if (Name == "DoubleAxeBeast")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3007115;
                            Uid = 3007116;
                            Uid = 3007117;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3007115, 0, 1);
                            killer.Owner.Inventory.Add(3007116, 0, 1);
                            killer.Owner.Inventory.Add(3007117, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region DarkSnakeKing
            if (Name == "DarkSerpentKing")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3005399;
                            Uid = 3005400;
                            Uid = 3005401;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3005399, 0, 1);
                            killer.Owner.Inventory.Add(3005400, 0, 1);
                            killer.Owner.Inventory.Add(3005401, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion
            #region FastDemon
            if (Name == "SwiftDemon")
            {
                uint Uid = 0;
                byte type = 1;
                for (int i = 0; i < 1; i++)
                {
                    switch (type)
                    {
                        case 1:
                            Uid = 3002723;
                            break;
                    }

                    if (Uid != 0)
                    {
                        ushort X = Owner.X, Y = Owner.Y;
                        Game.Map Map = Kernel.Maps[Owner.MapID];
                        {
                            killer.Owner.Inventory.Add(3002723, 0, 1);
                            //killer.Owner.Inventory.Add(3007231, 0, 1);
                            //killer.Owner.Inventory.Add(3007232, 0, 1);
                            Kernel.SendWorldMessage(new Network.GamePackets.Message("Congratulations! " + killer.Name + " Has killed" + Name + " and gets " + Database.ConquerItemInformation.BaseInformations[Uid].Name + " ", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            Network.GamePackets._String str = new _String(true);
                            str.UID = killer.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("cortege");
                            killer.Owner.SendScreen(str, true);
                        }
                    }
                }
            }
            #endregion

            #endregion

        }
        public void SendScreen(byte[] buffer)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client != null)
                {
                    if (client.Entity != null)
                    {
                        if (client.Entity.UID != ExcludeFromSend)
                        {
                            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, Owner.X, Owner.Y) > 18)
                            {
                                continue;
                            }
                            client.Send(buffer);
                        }
                    }
                }
            }
        }
        public static bool ChanceSuccess(double Chance)
        {
            int num = Kernel.Random.Next(10000000);
            double num2 = (double)num / 10000000.0 * 100.0;
            return Chance >= num2;
        }
        public void SendScreen(Interfaces.IPacket buffer)
        {
            SendScreen(buffer.ToArray());
        }
        public void SendScreenSpawn(Interfaces.IMapObject _object)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client != null)
                {
                    if (client.Entity != null)
                    {
                        if (client.Entity.UID != ExcludeFromSend)
                        {
                            if (client.Map.ID == Owner.MapID)
                            {
                                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, Owner.X, Owner.Y) > 25)
                                {
                                    continue;
                                }
                                _object.SendSpawn(client, false);
                            }
                        }
                    }
                }
            }
        }

    }
}
