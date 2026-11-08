using System;
using System.Linq;
using Revora.Game;
using Emulator.Core;
using System.Drawing;
using System.Threading;
using Revora.Network;
using Revora.Database;
using System.Net.Sockets;
using Revora.Interfaces;
using Revora.Network.Sockets;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Network.Cryptography;
using Revora.Game.ConquerStructures;

namespace Revora.Client
{
    public unsafe class GameState
    {
        #region Runes
        public SafeDictionary<uint, ConquerItem> RunesItem = new SafeDictionary<uint, ConquerItem>();
        #endregion
        public bool Pipe { get; set; }
     //   public CrossClient Cross;
        public int quarantineKill = 0;
        public int quarantineDeath = 0;
        public Time32 XPListStamp;
        #region UnionStructure
        public Game.ProgressBar ProgressBar;
        public byte UnionType
        {
            set
            {
                Entity.CharBuffer._UnionType = value;
                //SendScreen(Entity.SpawnPacket, false);
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
            }
        }
        public Revora.Network.GamePackets.Union.UnionClass Union;
        public uint UnionTitle
        {
            get
            {

                if (Union != null)
                {
                    if (Union.IsKingdom())
                    {
                        return (uint)(Offical + Harem + Guards);
                    }
                }
                return 0;
            }
        }
        uint _UnionID;
        public uint UnionID
        {
            get
            {
                return _UnionID;
            }
            set
            {

                _UnionID = value;
                Entity.CharBuffer._UnionID = value;
                Entity.UpdateDatabase("UnionID", _UnionID);
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
            }
        }
        // Entity materialization must restore both runtime and spawn-packet identity
        // without persistence setters or broadcasts while the character is loading.
        internal void RestoreUnion(Revora.Network.GamePackets.Union.UnionClass union,ushort official,ushort harem,ushort guards,uint exploits) {
            if(Entity==null || Entity.FullyLoaded)throw new InvalidOperationException("Union restoration requires a loading entity.");
            Union=union;_UnionID=union==null?0:union.ID;
            _Offical=official;_Harem=harem;_Guards=guards;_UnionExploits=exploits;
            Entity.CharBuffer._UnionID=_UnionID;
            Entity.CharBuffer.Offical_Harem_Guards=checked((ushort)(official+harem+guards));
        }
        internal void CommitUnionFormation(Revora.Network.GamePackets.Union.UnionClass union) {
            if(!Monitor.IsEntered(ItemSyncRoot) || !Revora.Game.Features.QuestInventory.Player(this,false))throw new InvalidOperationException("Union projection requires its current owner.");
            Union=union;_UnionID=union.ID;_Offical=1;_Harem=0;_Guards=0;
            Entity.CharBuffer._UnionID=union.ID;Entity.CharBuffer.Offical_Harem_Guards=1;
            Entity.CharBuffer._OfficialRank=1;Entity.CharBuffer._UnionType=(byte)(union.IsKingdom()?1:0);Entity.UnionName=union.Name;
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014),false);Send(union.CommittedOverhead(this));
        }
        internal void CommitUnionOffice(ushort official,ushort harem,ushort guards) {
            if(!Monitor.IsEntered(ItemSyncRoot) || !Revora.Game.Features.QuestInventory.Player(this,false) || Union==null)throw new InvalidOperationException("Union office projection requires its current owner.");
            _Offical=official;_Harem=harem;_Guards=guards;Entity.CharBuffer.Offical_Harem_Guards=checked((ushort)(official+harem+guards));
            Entity.CharBuffer._OfficialRank=(byte)(Union.IsLeader(Entity.UID)?1:0);
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014),false);Send(Union.CommittedOverhead(this));
        }
        internal void CommitUnionMembership(Revora.Network.GamePackets.Union.UnionClass union) {
            if(!Monitor.IsEntered(ItemSyncRoot) || !Revora.Game.Features.QuestInventory.Player(this,false))throw new InvalidOperationException("Union membership projection requires its current owner.");
            Union=union;_UnionID=union==null?0:union.ID;_Offical=0;_Harem=0;_Guards=0;
            Entity.CharBuffer._UnionID=_UnionID;Entity.CharBuffer.Offical_Harem_Guards=0;
            Entity.CharBuffer._OfficialRank=0;Entity.CharBuffer._UnionType=(byte)(union!=null && union.IsKingdom()?1:0);Entity.UnionName=union==null?"":union.Name;
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014),false);
            Send((union??new Revora.Network.GamePackets.Union.UnionClass()).CommittedOverhead(this));
            CommitKingdomPresentation();
        }
        internal void CommitGuildAdmission(Revora.Game.ConquerStructures.Society.Guild guild,Revora.Game.ConquerStructures.Society.Guild.Member member) {
            if(!Monitor.IsEntered(ItemSyncRoot) || !Revora.Game.Features.QuestInventory.Player(this,false))throw new InvalidOperationException("Guild admission projection requires its current owner.");
            Guild=guild;AsMember=member;Entity.CharBuffer._GuildID=guild.ID;Entity.CharBuffer._GuildRank=(ushort)member.Rank;
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014),false);guild.SendGuild(this);
        }
        internal void CommitKingdomPresentation() {
            if(!Monitor.IsEntered(ItemSyncRoot) || !Revora.Game.Features.QuestInventory.Player(this,false))throw new InvalidOperationException("Kingdom projection requires its current owner.");
            var icon=new Revora.Network.GamePackets.Union.Union.MsgLeagueOpt(true);
            icon.Action=Revora.Network.GamePackets.Union.Union.Actions.KingdomIcon;icon.dwParam3=Kernel.ServerKingdom;Send(icon.ToArray());
            if(Union==null)return;
            Entity.CharBuffer._UnionType=(byte)(Union.IsKingdom()?1:0);Entity.CharBuffer._OfficialRank=(byte)(Union.IsLeader(Entity.UID)?1:0);
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014),false);Send(Union.CommittedOverhead(this));
        }
        public byte UnionRank
        {
            set
            {

                Entity.CharBuffer._OfficialRank = value;
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
            }
        }
        uint _UnionExploits = 0;
        public uint UnionExploits
        {
            get
            {
                return _UnionExploits;
            }
            set
            {

                _UnionExploits = value;
                Entity.UpdateDatabase("UnionExploits", _UnionExploits);
                byte i = 0;
                if (_UnionExploits >= 200)
                    i += 1;
                if (_UnionExploits >= 500)
                    i += 1;
                if (_UnionExploits >= 1000)
                    i += 1;
                if (_UnionExploits >= 1500)
                    i += 1;
                if (_UnionExploits >= 2100)
                    i += 1;
                if (_UnionExploits >= 2800)
                    i += 1;
                if (_UnionExploits >= 3700)
                    i += 1;
                if (_UnionExploits >= 4700)
                    i += 1;
                if (_UnionExploits >= 6000)
                    i += 1;
                if (_UnionExploits >= 7500)
                    i += 1;
                if (_UnionExploits >= 10000)
                    i += 1;
                if (_UnionExploits >= 15000)
                    i += 1;
                if (_UnionExploits >= 23000)
                    i += 1;
                if (Entity.EntityFlag == EntityFlag.Player)
                {
                    Entity.Update((byte)FlagsUpdate.DataType.MilitaryRank, i, true);
                    Entity.Update((byte)FlagsUpdate.DataType.MilitaryExploits, _UnionExploits, true);
                }
            }
        }
        public static GameState GetClientFromID(uint id)
        {
            foreach (var client in Kernel.GamePool.Values)
            {
                if (client.Entity.UID == id)
                {
                    return client;
                }
            }
            return null;
        }
        public static GameState GetClientFromName(string name)
        {
            foreach (var clientq in Kernel.GamePool.Values)
            {
                if (clientq.Entity.Name == name)
                {
                    return clientq;
                }
            }
            return null;
        }
        ushort _Offical;
        public ushort Offical
        {
            get
            {
                return _Offical;
            }
            set
            {

                _Offical = value;
                Entity.CharBuffer.Offical_Harem_Guards = (ushort)(Offical + Harem + Guards);
                if (Entity.EntityFlag == EntityFlag.Player)
                {
                    Entity.Update((byte)FlagsUpdate.DataType.UnionRank, (ushort)(Offical + Harem + Guards), true);
                }
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
                Entity.UpdateDatabase("Offical", value);
            }
        }
        ushort _Harem;
        public ushort Harem
        {
            get
            {

                return _Harem;
            }
            set
            {

                _Harem = value;
                Entity.CharBuffer.Offical_Harem_Guards = (ushort)(Offical + Harem + Guards);
                if (Entity.EntityFlag == EntityFlag.Player)
                {
                    Entity.Update((byte)FlagsUpdate.DataType.UnionRank, (ushort)(Offical + Harem + Guards), true);
                }
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
                Entity.UpdateDatabase("Harem", value);
            }
        }
        ushort _Guards;
        public ushort Guards
        {
            get
            {

                return _Guards;
            }
            set
            {

                _Guards = value;
                Entity.CharBuffer.Offical_Harem_Guards = (ushort)(Offical + Harem + Guards);
                if (Entity.EntityFlag == EntityFlag.Player)
                {
                    Entity.Update((byte)FlagsUpdate.DataType.UnionRank, (ushort)(Offical + Harem + Guards), true);
                }
                SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
                Entity.UpdateDatabase("Guards", value);
            }
        }
        #endregion
        #region BreakingTouch
        public void BreakTouch(GameState client)
        {
            if (client == null || client.Entity == null || Entity == null) return;
            using (var lease = Revora.Game.Features.TeamArenaCombatLease.Acquire(client.Entity, Entity))
            {
                if (lease != null && !lease.Allowed) return;
                BreakTouchOwned(client);
            }
        }
        private void BreakTouchOwned(GameState client)
        {
            if (!client.Spells.ContainsKey(12400))
                return;

            if (client.Weapons != null)
                if (client.Weapons.Item2 != null)
                    if (client.Weapons.Item2.ID / 1000 != 619)
                        return;

            var spell = SpellTable.GetSpell(client.Spells[12400].ID, client.Spells[12400].Level);
            if (MyMath.Success((double)70))
            {
                if (Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran04))
                {
                    SpellUse suse = new SpellUse(true);
                    suse.Attacker = Entity.UID;
                    suse.SpellID = spell.ID;
                    suse.SpellLevel = spell.Level;

                    var array = Game.Attacking.Handle.PlayerinRange(Entity, Entity).ToArray();
                    foreach (var target in array)
                    {
                        var attacked = target.Entity;
                        if (attacked.UID == client.Entity.UID)
                            continue;
                        if (Game.Attacking.Handle.CanAttack(client.Entity, attacked, spell, true))
                        {
                            var attack = new Attack(true);
                            attack.Attacker = client.Entity.UID;
                            attack.Attacked = attacked.UID;

                            uint damage = Game.Attacking.Calculate.Magic(client.Entity, attacked, spell, ref attack);

                            attack.Damage = damage;
                            suse.Effect = attack.Effect;
                            suse.Effect = attack.Effect;

                            Game.Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell);
                            suse.AddTarget(attacked.UID, damage, attack);
                        }
                    }
                    client.SendScreen(suse, true);

                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran02);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran03);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
                }
                else if (Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran03))
                {
                    Entity.AddFlag((ulong)FlagsUpdate.Flags.lianhuaran04, 20, true);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran03);
                    Entity.lianhuaranPercent = 0.5f;
                }
                else if (Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran02))
                {
                    Entity.AddFlag((ulong)FlagsUpdate.Flags.lianhuaran03, 20, true);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran02);
                    Entity.lianhuaranPercent = 0.4f;
                }
                else if (Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran))
                {
                    Entity.AddFlag((ulong)FlagsUpdate.Flags.lianhuaran02, 20, true);
                    Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran);
                    Entity.lianhuaranPercent = 0.3f;
                }
                else if (!Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran))
                {
                    Entity.AddFlag((ulong)FlagsUpdate.Flags.lianhuaran, 20, true);
                    Entity.lianhuaranPercent = 0.1f;
                }
                Entity.lianhuaranStamp = Time32.Now;
                Entity.lianhuaranLeft = 20;
                Revora.Game.Features.TeamArenaBreakingTouch.Record(client.Entity, Entity);

                client.IncreaseSpellExperience(100, 12400);
            }
        }
        #endregion
        #region TeamQualifier
        public bool endarena = false;
        public bool endteam = false;
        #endregion
        public void ChangeName(GameState client)
        {
            if (client == null || client.Entity == null)
                return;
            string oldName = client.Entity.Name;
            string newName = client.NewName;
            uint uid = client.Entity.UID;
            if (string.IsNullOrEmpty(newName))
                return;
            Console.WriteLine("* Revora - Name change is in progress!");
            TryRenameByUid("apprentice", "MentorName", "MentorID", uid, newName);
            TryRenameByUid("apprentice", "ApprenticeName", "ApprenticeID", uid, newName);
            TryRenameByUid("arena", "EntityName", "EntityID", uid, newName);
            TryRenameByUid("claimitems", "OwnerName", "OwnerUID", uid, newName);
            TryRenameByUid("claimitems", "GainerName", "GainerUID", uid, newName);
            TryRenameByUid("detaineditems", "OwnerName", "OwnerUID", uid, newName);
            TryRenameByUid("detaineditems", "GainerName", "GainerUID", uid, newName);
            TryRenameByUid("enemy", "EnemyName", "EnemyID", uid, newName);
            TryRenameByUid("friends", "FriendName", "FriendID", uid, newName);
            TryRenameByName("guilds", "LeaderName", "LeaderName", oldName, newName);
            TryRenameByName("clans", "LeaderName", "LeaderName", oldName, newName);
            TryRenameByUid("nobility", "EntityName", "EntityUID", uid, newName);
            TryRenameByUid("partners", "PartnerName", "PartnerID", uid, newName);
            TryRenameByUid("chi", "name", "uid", uid, newName);
            TryRenameByUid("teamarena", "EntityName", "EntityID", uid, newName);
            try
            {
                Database.MySqlCommand cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                cmd.Update("entities").Set("name", newName).Set("namechange", "").Where("UID", uid).Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine("[rename] entities " + e.Message);
            }
            try
            {
                if (client.Entity.MyJiang != null)
                {
                    client.Entity.MyJiang.OwnName = newName;
                    Game.JiangHu.JiangHuClients[client.Entity.UID] = client.Entity.MyJiang;
                }
                if (client.Entity.MyFlowers != null)
                    client.Entity.MyFlowers.Name = newName;
                if (Game.ConquerStructures.Nobility.Board.ContainsKey(uid) && Game.ConquerStructures.Nobility.Board[uid] != null)
                    Game.ConquerStructures.Nobility.Board[uid].Name = newName;
                if (Qualifier.ArenaStatistics.ContainsKey(uid) && Qualifier.ArenaStatistics[uid] != null)
                    Qualifier.ArenaStatistics[uid].Name = newName;
                if (client.Entity.GetClan != null)
                {
                    if (client.Entity.GetClan.LeaderName == oldName)
                        client.Entity.GetClan.LeaderName = newName;
                    if (client.Entity.GetClan.Members != null && client.Entity.GetClan.Members.ContainsKey(uid) && client.Entity.GetClan.Members[uid] != null)
                        client.Entity.GetClan.Members[uid].Name = newName;
                }
                if (client.Guild != null)
                {
                    if (client.Guild.LeaderName == oldName)
                        client.Guild.LeaderName = newName;
                    if (client.Guild.Members != null && client.Guild.Members.ContainsKey(uid) && client.Guild.Members[uid] != null)
                        client.Guild.Members[uid].Name = newName;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[rename] memory " + e.Message);
            }
            client.Entity.Name = newName;
            client.Entity.NewName = "";
            client.NewName = "";
            client.Entity.MapID = 1002;
            client.Entity.X = 300;
            client.Entity.Y = 278;
            client.Entity.PreviousMapID = 1002;
            client.Entity.PrevX = 300;
            client.Entity.PrevY = 278;
            try
            {
                Database.EntityTable.SaveEntity(client);
            }
            catch (Exception e)
            {
                Console.WriteLine("[rename] save " + e.Message);
            }
            Console.WriteLine("* Revora - [" + oldName + "] : [" + newName + "]");
            try
            {
                client.Send(new Message("Your name is now " + newName + ". Reconnect. You will enter Twin City.", Color.Yellow, Message.TopLeft));
            }
            catch
            {
            }
            client.OnDisconnect = null;
            client.Disconnect();
        }
        private static void TryRenameByUid(string table, string column, string whereColumn, uint uid, string newName)
        {
            try
            {
                Database.MySqlCommand cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                cmd.Update(table).Set(column, newName).Where(whereColumn, uid).Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine("[rename] " + table + "." + column + " " + e.Message);
            }
        }
        private static void TryRenameByName(string table, string column, string whereColumn, string oldName, string newName)
        {
            try
            {
                Database.MySqlCommand cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                cmd.Update(table).Set(column, newName).Where(whereColumn, oldName).Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine("[rename] " + table + "." + column + " " + e.Message);
            }
        }
        #region AuroraLotus
        public void BlessTouch(GameState client)
        {

            if (!client.Spells.ContainsKey(12390))
                return;
            if (client.Weapons != null)
                if (client.Weapons.Item2 != null)
                    if (client.Weapons.Item2.ID / 1000 != 619)
                        return;

            var spell2 = SpellTable.GetSpell(client.Spells[12390].ID, client.Spells[12390].Level);
            if (Kernel.Rate((double)spell2.Percent))
            {

                var spell = Database.SpellTable.GetSpell(1095, 4);
                Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma,80,true);
                Entity.StigmaStamp = Time32.Now;
                Entity.StigmaIncrease = spell.PowerPercent;
                Entity.StigmaTime = (byte)spell.Duration;
                if (Entity.EntityFlag == EntityFlag.Player)
                    Send(Constants.Stigma(spell.PowerPercent, spell.Duration));

                spell = Database.SpellTable.GetSpell(1090, 4);
                Entity.ShieldTime = 0;
                Entity.ShieldStamp = Time32.Now;
                Entity.MagicShieldStamp = Time32.Now;
                Entity.MagicShieldTime = 0;

                Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicShield,120,true);
                Entity.MagicShieldStamp = Time32.Now;
                Entity.MagicShieldIncrease = 1.1f;//spell.PowerPercent;
                Entity.MagicShieldTime = (byte)spell.Duration;
                if (Entity.EntityFlag == EntityFlag.Player)
                    Send(Constants.Shield(spell.PowerPercent, spell.Duration));

                spell = Database.SpellTable.GetSpell(1085, 4);
                Entity.AccuracyStamp = Time32.Now;
                Entity.StarOfAccuracyStamp = Time32.Now;
                Entity.StarOfAccuracyTime = 0;
                Entity.AccuracyTime = 0;

                Entity.AddFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy,60,true);
                Entity.StarOfAccuracyStamp = Time32.Now;
                Entity.StarOfAccuracyTime = (byte)spell.Duration;
                if (Entity.EntityFlag == EntityFlag.Player)
                    Send(Constants.Accuracy(spell.Duration));

                client.IncreaseSpellExperience(100, 12390);
            }
        }
        #endregion
        public Pet Pet;
        public Quests Quests;
        public SafeDictionary<uint, uint> ClamiedReward = new SafeDictionary<uint, uint>(1000);
        public SafeDictionary<uint, Mailbox.PrizeInfo> Prizes = new SafeDictionary<uint, Mailbox.PrizeInfo>(1000);
        public SafeDictionary<uint, WayofHeroes.StageInfo> Way2Heroes = new SafeDictionary<uint, WayofHeroes.StageInfo>(1000);
        public ConquerItem GetItem(uint UID)
        {
            ConquerItem n_item = null;
            if (this.Inventory.TryGetItem(UID, out n_item))
                return n_item;
            n_item = Equipment.TryGetItem(UID);
            return n_item;
        }
        public Game.InnerPower InnerPower;
        public bool EndTeam;
        public uint PlayRouletteUID = 0, WatchRoulette = 0;
        public uint NinjaPoint = 0, PiratePoint = 0;
        public uint JoinToWar;
        public ConquerItem Clear;
        public bool Effect = false;
        public int ChallengeScore;
        public bool ChallangeScoreStamp;
        public uint testgui;
        public int speedHackSuspiction = 0;
        public bool InArenaMatch = false;
        public ushort EditCount;
        public ushort EditAllowed;
        public bool IsFairy = false;
        public uint FairyType = 0;
        public uint SType = 0;
        public Time32 CpsStamp;
        public uint Vippoints = 0;
        public uint VIPDays;
        public IDisposable[] TimerSubscriptions;
        public object TimerSyncRoot;
        public object ItemSyncRoot = new object();
        public Time32 LastVIPTeleport, LastVIPTeamTeleport;
        public bool AlternateEquipment;
        private ClientWrapper _socket;
        public Database.AccountTable Account;
        public GameCryptography Cryptography;
        public DHKeyExchange.ServerKeyExchange DHKeyExchange;
        public bool Exchange = true;
        public ConcurrentPacketQueue Queue;
        public PacketFilter PacketFilter;
        public Time32 CantAttack = Time32.Now;
        public bool Filtering = false;
        public bool Effect1 = false;
        public bool Effect10 = false;
        public bool Effect8 = false;
        public bool Effect9 = false;
        public bool Effect11 = false;
        public bool Effect4 = false;
        public bool Effect3 = false;
        public bool Effect2 = false;
        
        public bool SpiltStack = false;
        public Action<GameState> OnDisconnect;
        public int apprtnum = 0;
        public bool JustCreated = false;
        public Time32 LastEntitySave;
        public const int HeartbeatSaveSeconds = 60;
        public string NewName = "";
        public uint namechanges
        {
            get { return this["namechanges"]; }
            set
            {
                this["namechanges"] = value;
            }
        }
        #region Network
        public GameState(ClientWrapper socket)
        {
            Fake = socket == null;
            if (Fake) socket = new ClientWrapper() { Alive = true };
            Queue = new ConcurrentPacketQueue();
            PacketFilter = new PacketFilter() { { 10010, 10 }, { 10005, 7 }, { 2064, 4 }, { 2032, 3 }, { 1027, 2 } };
            Attackable = false;
            Action = 0;
            _socket = socket;
            Cryptography = new GameCryptography(System.Text.Encoding.Default.GetBytes(Constants.GameCryptographyKey));
            DHKeyExchange = new Network.GamePackets.DHKeyExchange.ServerKeyExchange();
            ChiPowers = new List<ChiPowerStructure>();
            Retretead_ChiPowers = new List<ChiPowerStructure>();
        }
        public void ReadyToPlay()
        {
            try
            {
                Pet = new Pet(this);
                Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
                Screen = new Game.Screen(this);
                Inventory = new Game.ConquerStructures.Inventory(this);
                Equipment = new Game.ConquerStructures.Equipment(this);
                WarehouseOpen = false;
                WarehouseOpenTries = 0;
                TempPassword = "";
                Warehouses = new SafeDictionary<Game.ConquerStructures.Warehouse.WarehouseID, Game.ConquerStructures.Warehouse>(20);
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.TwinCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.TwinCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.PhoenixCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.PhoenixCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.ApeCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.ApeCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.DesertCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.DesertCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.BirdCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.BirdCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.StoneCity, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.StoneCity));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.Market, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.Market));
                Warehouses.Add(Game.ConquerStructures.Warehouse.WarehouseID.House, new Game.ConquerStructures.Warehouse(this, Game.ConquerStructures.Warehouse.WarehouseID.House));
                // Preserve this legacy persisted account key; new box keys use their full registered ID.
                Warehouses.Add((Game.ConquerStructures.Warehouse.WarehouseID)(ushort)this.Account.EntityID, new Game.ConquerStructures.Warehouse(this, (Game.ConquerStructures.Warehouse.WarehouseID)(ushort)this.Account.EntityID, 200));
                Trade = new Game.ConquerStructures.Trade();
                ArenaStatistic = new QualifierStatistic(true);
                Prayers = new List<GameState>();
                Quests = new Quests(this);
                map = null;
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public void SendHandshake(byte[] buffer)
        {
            if (Fake) return;
            if (!_socket.Alive) return;
            byte[] _buffer = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, _buffer, 0, buffer.Length);
            try
            {
                lock (_socket)
                {
                    if (!_socket.Alive) return;
                    var helloCrypto = new GameCryptography(System.Text.Encoding.Default.GetBytes(Constants.GameCryptographyKey));
                    helloCrypto.Encrypt(_buffer, _buffer.Length);
                    _socket.Send(_buffer);
                }
            }
            catch (Exception)
            {
                _socket.Alive = false;
                Disconnect();
            }
        }
        public void Send(byte[] buffer)
        {
            if (Fake) return;
            if (!_socket.Alive) return;
            if (buffer != null && buffer.Length >= 4
                && BitConverter.ToUInt16(buffer, 2) == PacketID.AutoInvite
                && !AutoInvite.Enabled)
                return;
            ushort length = BitConverter.ToUInt16(buffer, 0);
            if (length >= 1024 && buffer.Length > length)
            {
                //Console.WriteLine(Environment.StackTrace);
                return;
            }
            byte[] _buffer = new byte[buffer.Length];
            if (length == 0)
                Writer.WriteUInt16((ushort)(buffer.Length - 8), 0, buffer);
            Buffer.BlockCopy(buffer, 0, _buffer, 0, buffer.Length);
            Writer.WriteString(Constants.ServerKey, _buffer.Length - 8, _buffer);
            try
            {
                lock (_socket)
                {
                    if (!_socket.Alive) return;
                    lock (Cryptography)
                    {
                        Cryptography.Encrypt(_buffer, _buffer.Length);
                        _socket.Send(_buffer);
                    }
                }
            }
            catch (Exception)
            {
                _socket.Alive = false;
                Disconnect();
            }
        }
        private void EndSend(IAsyncResult res)
        {
            try
            {
                _socket.Socket.EndSend(res);
            }
            catch
            {
                _socket.Alive = false;
                Disconnect();
            }
        }
        internal static Action<GameState, Interfaces.IPacket> BeforePacketSend = null;
        public void Send(Interfaces.IPacket buffer)
        {
            var watch = BeforePacketSend;
            if (watch != null) watch(this, buffer);
            Send(buffer.ToArray());
        }
        public void SendScreenSpawn(Interfaces.IMapObject obj, bool self)
        {
            try
            {
                foreach (Interfaces.IMapObject _obj in Screen.Objects)
                {
                    if (_obj == null) continue;
                    if (_obj.UID != Entity.UID)
                    {
                        if (_obj.MapObjType == Game.MapObjectType.Player)
                        {
                            GameState client = _obj.Owner as GameState;
                            obj.SendSpawn(client, false);
                        }
                    }
                }
                if (self)
                    obj.SendSpawn(this);
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public void RemoveScreenSpawn(Interfaces.IMapObject obj, bool self)
        {
            try
            {
                if (Screen == null) return;
                if (Screen.Objects == null) return;
                foreach (Interfaces.IMapObject _obj in Screen.Objects)
                {
                    if (_obj == null) continue;
                    if (obj == null) continue;
                    if (_obj.UID != Entity.UID)
                    {
                        if (_obj.MapObjType == Game.MapObjectType.Player)
                        {
                            GameState client = _obj.Owner as GameState;
                            client.Screen.Remove(obj);
                        }
                    }
                }
                if (self)
                    Screen.Remove(obj);
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public void SendScreen(byte[] buffer, bool self = true)
        {
            try
            {
                foreach (Interfaces.IMapObject obj in Screen.Objects)
                {
                    if (obj == null) continue;
                    if (obj.UID != Entity.UID)
                    {
                        if (obj.MapObjType == Game.MapObjectType.Player)
                        {
                            GameState client = obj.Owner as GameState;
                            if (WatchingGroup != null && client.WatchingGroup == null)
                                if (Entity.SkillTeamWatchingElitePKMatch != null && client.Entity.SkillTeamWatchingElitePKMatch == null)
                                    continue;
                            client.Send(buffer);
                        }
                    }
                }
                if (self)
                    Send(buffer);
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public void SendScreen(Interfaces.IPacket buffer, bool self = true)
        {
            foreach (Interfaces.IMapObject obj in Screen.Objects)
            {
                if (obj == null) continue;
                if (obj.MapObjType == Game.MapObjectType.Player)
                {
                    GameState client = obj.Owner as GameState;
                    if (client.Entity.UID != Entity.UID)
                        client.Send(buffer);
                }
            }
            if (self)
                Send(buffer);
        }
        public void Disconnect(bool save = true)
        {
          /*  if (Cross != null)
                Cross.Dispose();*/
            if (Fake) return;
            Exception callbackError = null;
            RunUnderSessionLease(delegate { callbackError = DisconnectOwned(); });
            if (callbackError != null) throw callbackError;
        }
        Exception DisconnectOwned()
        {
            if (System.Threading.Interlocked.Exchange(ref disconnectOnce, 1) == 1) return null;
            Revora.Game.Attacking.Handle.InteractionDisconnected(this);
            Exception callbackError = null;
            try
            {
                if (Revora.Game.Features.SessionInstall.IsSuperseded(this))
                {
                    DisposeSupersededSession();
                    return null;
                }
                Revora.Game.Features.MagicDefenderProtection.Cancel(this);
                Revora.Game.Features.WarriorStance.Cancel(this);
                if (Screen != null) Screen.DisposeTimers();
                GeneralData.RemoveTPA(this);
                if (Server.Thread != null) Server.Thread.UnRegister(this);
                try
                {
                    if (Entity != null)
                        Revora.Game.Features.LivingWorldSystem.OnPlayerLogout(Entity.UID);
                    Revora.Game.Features.LastManStandingSystem.OnDisconnect(this);
                    Revora.Game.Features.ExpeditionEvent.OnDisconnect(this);
                    Revora.Game.Features.NobleWarSystem.OnDisconnect(this);
                    Revora.Game.Features.RelicRushSystem.OnDisconnect(this);
                    Revora.Game.Features.WaveDefenseSystem.OnDisconnect(this);
                    Revora.Game.Features.BeastTrialSystem.OnDisconnect(this);
                    Revora.Game.Features.BossRushSystem.OnDisconnect(this);
                    Revora.Game.Features.DuelPitSystem.OnDisconnect(this);
                    Revora.Game.Features.SteedRaceSystem.OnDisconnect(this);
                }
                catch
                {
                }
                try
                {
                    if (OnDisconnect != null) OnDisconnect(this);
                }
                catch (Exception ex)
                {
                    callbackError = ex;
                }
                try
                {
                    if (Entity != null && _socket != null && _socket.Connector != null)
                    {
                        CheckPokerDisconnect();
                        CheckRouletteDisconnect();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[session] disconnect callback failed type=" + ex.GetType().Name);
                }
                try
                {
                    if (_socket != null && _socket.Connector != null)
                        _socket.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[session] socket disconnect failed type=" + ex.GetType().Name);
                }
                try { ShutDown(); }
                catch (Exception ex)
                {
                    Console.WriteLine("[session] shutdown failed type=" + ex.GetType().Name);
                }
            }
            finally
            {
                if (!ShutdownDeferred)
                    Revora.Game.Features.SessionInstall.Retire(this);
            }
            return callbackError;
        }
        void DisposeSupersededSession()
        {
            // Shared world callbacks and saves belong to the current generation.
            // These resources and the presence token belong to this exact object.
            Interlocked.Exchange(ref disconnectOnce, 1);
            Interlocked.Exchange(ref shutdownStarted, 1);
            try
            {
                if (Screen != null) Screen.DisposeTimers();
                if (Pet != null) Pet.DisposeTimers();
                if (Server.Thread != null) Server.Thread.UnRegister(this);
            }
            finally
            {
                try { if (_socket != null && _socket.Connector != null) _socket.Disconnect(); }
                catch (Exception ex) { Console.WriteLine("[session] obsolete socket disconnect failed type=" + ex.GetType().Name); }
                finally
                {
                    if (_socket != null) _socket.Connector = null;
                    ReleaseSessionRegistration(true);
                }
            }
        }
        void RunUnderSessionLease(Action work)
        {
            if (work == null) return;
            uint uid = Revora.Game.Features.SessionInstall.SessionUid(this);
            if (uid == 0)
            {
                work();
                return;
            }
            if (Revora.Game.Features.SessionInstall.Holds(uid))
            {
                work();
                return;
            }
            // Arena retention already holds the character lock. Waiting here would
            // invert session -> owner against a login that holds the lease.
            if (ItemSyncRoot != null && Monitor.IsEntered(ItemSyncRoot))
            {
                if (!Revora.Game.Features.SessionInstall.TryEnter(uid))
                {
                    Revora.Game.Features.SessionInstall.Defer(delegate { RunUnderSessionLease(work); });
                    return;
                }
                try { work(); }
                finally { Revora.Game.Features.SessionInstall.Exit(uid); }
                return;
            }
            Revora.Game.Features.SessionInstall.Enter(uid);
            try { work(); }
            finally { Revora.Game.Features.SessionInstall.Exit(uid); }
        }

        public uint ArmorLook
        {
            get { return this["armorlook"]; }
            set { this["armorlook"] = value; }
        }
        public uint HeadgearLook
        {
            get { return this["headgearlook"]; }
            set { this["headgearlook"] = value; }
        }
        public void CheckPokerDisconnect()
        {
            if (Entity.MyPokerTable != null)
            {
                if (Entity.MyPokerTable.Players.ContainsKey(Entity.UID) && Entity.MyPokerTable.Pot > 1)
                {
                    byte[] P = new byte[10];
                    P[6] = 4; P[9] = 200;
                    Entity.MyPokerTable.NewPlayerMove(P, Entity.UID);
                }
                else
                    Entity.MyPokerTable.RemovePlayer(Entity.UID);
            }
        }
        public void CheckRouletteDisconnect()
        {
            Roulette.Database.Roulettes.RouletteTable Table;
            if (PlayRouletteUID != 0)
            {
                if (Roulette.Database.Roulettes.RoulettesPoll.TryGetValue(PlayRouletteUID, out Table))
                {
                    Table.RemovePlayer(this);
                }
            }
            else if (WatchRoulette != 0)
            {
                if (Roulette.Database.Roulettes.RoulettesPoll.TryGetValue(WatchRoulette, out Table))
                {
                    Table.RemoveWatch(Entity.UID);
                }
            }
        }
        public void HeartbeatSave()
        {
            if (Fake) return;
            if (Entity == null || !Entity.FullyLoaded) return;
            try
            {
                if (Entity.WTitles != null)
                    Entity.WTitles.Update();
                Database.EntityTable.SaveEntity(this);
                Database.SkillTable.SaveSpells(this);
                Database.SkillTable.SaveProficiencies(this);
                Database.JiangHu.SaveJiangHu(this);
                if (Quests != null)
                    Quests.Save();
                if (Prizes != null)
                    Database.MailboxTable.Save(this);
                if (ChiPowers != null && Retretead_ChiPowers != null)
                    Database.ChiTable.Save(this);
                if (Variables != null)
                    Database.EntityVariableTable.Save(this);
                Database.ActivenessTable.Save(this);
                Database.DialyQuestTable.Save(this);
                Database.RewardDataBase.Save(this);
                if (ArenaStatistic != null)
                    Database.ArenaTable.SaveArenaStatistics(ArenaStatistic);
                if (TeamArenaStatistic != null)
                    Database.TeamArenaTable.SaveArenaStatistics(TeamArenaStatistic);
                if (ChampionStats != null)
                    Database.ChampionTable.SaveStats(ChampionStats);
                Database.ConquerItemTable.SaveWornAndBag(this);
                if (NobilityInformation != null && NobilityInformation.Donation > 0)
                    Database.NobilityTable.UpdateNobilityInformation(NobilityInformation);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[heartbeat] " + Entity.Name + " " + ex.Message);
            }
        }

        internal Revora.Game.Features.IArenaShutdownRetention ArenaShutdownRetention;
        internal void ResumeArenaShutdown() { RunUnderSessionLease(ShutDown); }
        private int shutdownStarted;
        int disconnectOnce;
        private volatile bool shutdownDeferred;
        internal bool ShutdownDeferred { get { return shutdownDeferred; } }
        internal bool DisconnectStarted { get { return System.Threading.Volatile.Read(ref disconnectOnce) != 0; } }
        internal bool ShutdownStarted { get { return System.Threading.Volatile.Read(ref shutdownStarted)!=0; } }
        internal bool TryBeginShutdown()
        {
            lock(ItemSyncRoot) {
                if(ShutdownStarted)return false;
                var retention=ArenaShutdownRetention;
                if(retention!=null && retention.ShouldDefer(this)) { shutdownDeferred=true; return false; }
                shutdownDeferred=false;
                return System.Threading.Interlocked.CompareExchange(ref shutdownStarted, 1, 0) == 0;
            }
        }
        internal void ReleaseSessionRegistration(bool releaseDisconnect)
        {
            RunUnderSessionLease(delegate
            {
                try { Database.AccountActivity.Close(this); }
                catch (Exception ex)
                {
                    // Uncertain history must never imply inactivity.
                    Console.WriteLine("[account-presence] close error=" + ex.GetType().Name);
                }
                // Removing by UID alone can evict a newer login after a delayed old-session callback.
                Revora.Game.Features.SessionInstall.DropRegistration(this, releaseDisconnect);
            });
        }
        private void ShutDown()
        {
            if (Revora.Game.Features.SessionInstall.IsSuperseded(this))
            {
                DisposeSupersededSession();
                return;
            }
            if (Socket == null || Socket.Connector == null || !TryBeginShutdown()) return;
            Socket.Connector = null;
            if (this.Entity != null)
            {
                try
                {
                    if (this.Entity.JustCreated && !this.Entity.FullyLoaded) return;
                    Time32 now = Time32.Now;
                    Kernel.DisconnectPool.Add(this.Entity.UID, this);
                    RemoveScreenSpawn(this.Entity, false);
                    Sistemas.Quarantine.Check(this);
                    using (var conn = Database.DataHolder.MySqlConnection)
                    {
                        if (Entity != null && Entity.WTitles != null)
                            Entity.WTitles.Update();
                        conn.Open();
                        Database.EntityTable.UpdateOnlineStatus(this, false, conn);
                        Database.EntityTable.SaveEntity(this, conn);
                        Database.MailboxTable.Save(this);
                        Database.JiangHu.SaveJiangHu(this);
                        if (Quests != null)
                            Quests.Save();
                        //Database.Flowers.SaveFlowers();
                        Database.RewardDataBase.Save(this);
                        Database.DialyQuestTable.Save(this);
                        Database.ActivenessTable.Save(this);
                        Database.EntityTable.UpdateOnlineStatus(this, false, conn);
                        Database.EntityTable.SaveEntity(this, conn);
                        Database.EntityTable.SaveEntity(this);
                        Database.EntityVariableTable.Save(this, conn);
                        Database.SkillTable.SaveProficiencies(this, conn);
                        Database.SkillTable.SaveProficiencies(this);
                        Database.SkillTable.SaveSpells(this, conn);
                        Database.SkillTable.SaveSpells(this);
                        Database.ArenaTable.SaveArenaStatistics(this.ArenaStatistic, conn);
                        Database.TeamArenaTable.SaveArenaStatistics(this.TeamArenaStatistic, conn);
                        Database.ChampionTable.SaveStatistics(this.ChampionStats, conn);
                        Database.ConquerItemTable.SaveWornAndBag(this);
                    }
                    ReleaseSessionRegistration(false);
                    string name = Entity.Name;
                    string NewName = Entity.NewName;
                    if (!string.IsNullOrEmpty(Entity.NewName) && !string.Equals(Entity.Name, Entity.NewName, StringComparison.Ordinal))
                    {
                        Console.WriteLine("Name change in progress.");
                        try
                        {
                            Database.MySqlCommand cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("apprentice").Set("MentorName", NewName).Where("MentorID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("apprentice").Set("ApprenticeName", NewName).Where("ApprenticeID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("arena").Set("EntityName", NewName).Where("EntityID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("claimitems").Set("OwnerName", NewName).Where("OwnerUID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("claimitems").Set("GainerName", NewName).Where("GainerUID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("detaineditems").Set("OwnerName", NewName).Where("OwnerEntity.UID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("detaineditems").Set("GainerName", NewName).Where("GainerUID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("enemy").Set("EnemyName", NewName).Where("EnemyID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("friends").Set("FriendName", NewName).Where("FriendID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("guilds").Set("Name", NewName).Where("Name", Entity.Name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("guilds").Set("LeaderName", NewName).Where("LeaderName", Entity.Name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("nobility").Set("EntityName", NewName).Where("EntityName", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("partners").Set("PartnerName", NewName).Where("PartnerID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("chi").Set("name", NewName).Where("uid", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("teamarena").Set("EntityName", NewName).Where("EntityID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("entities").Set("Name", NewName).Where("UID", Entity.UID).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopArcher", NewName).Where("TopArcher", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopPirate", NewName).Where("TopPirate", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopTrojan", NewName).Where("TopTrojan", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopGuildLeader", NewName).Where("TopGuildLeader", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopNinja", NewName).Where("TopNinja", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopMonk", NewName).Where("TopMonk", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopWarrior", NewName).Where("TopWarrior", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopSpouse", NewName).Where("TopSpouse", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopWaterTaoist", NewName).Where("TopWaterTaoist", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopFireTaoist", NewName).Where("TopFireTaoist", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("MonthlyPkChampion", NewName).Where("MonthlyPkChampion", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("WeeklyPkChampion", NewName).Where("WeeklyPkChampion", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopDeputyLeader", NewName).Where("TopDeputyLeader", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopDeputyLeader2", NewName).Where("TopDeputyLeader2", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopDeputyLeader3", NewName).Where("TopDeputyLeader3", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopDeputyLeader4", NewName).Where("TopDeputyLeader4", name).Execute();

                            cmdupdate = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                            cmdupdate.Update("flags").Set("TopDeputyLeader5", NewName).Where("TopDeputyLeader5", name).Execute();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("[rename] shutdown " + e.Message);
                        }
                    }
                    if (Entity.MyClones.Count > 0)
                    {
                        foreach (var item in Entity.MyClones.Values)
                        {
                            Map.RemoveEntity(item);
                            Data data = new Data(true);
                            data.UID = item.UID;
                            data.ID = Data.RemoveEntity;
                            item.MonsterInfo.SendScreen(data);
                        }
                        Entity.MyClones.Clear();
                    }
                    if (Booth != null)
                        Booth.Remove();

                    if (Companion != null)
                    {
                        Map.RemoveEntity(Companion);
                        Data data = new Data(true);
                        data.UID = Companion.UID;
                        data.ID = Data.RemoveEntity;
                        Companion.MonsterInfo.SendScreen(data);
                    }
                    if (Quests != null)
                        Quests.Save();
                    if (this.Entity.Cloud != null)
                    {
                        this.Entity.Cloud.DisposeCloud(this);
                    }
                    if (Pet != null)
                        Pet.ClearAll();
                    if (QualifierGroup != null)
                        QualifierGroup.End(this);

                    if (TeamQualifierGroup != null)
                        TeamQualifierGroup.CheckEnd(this, true);

                    if (ChampionGroup != null)
                        ChampionGroup.End(this);

                    //Test
                    var Scores = NobiltyPoleWar.AllScores[(ushort)((int)this.Entity.NobilityRank)] = new SafeDictionary<uint, Entity>();
                    if (Scores.ContainsKey(this.Entity.UID))
                        Scores.Remove(this.Entity.UID);
                    //
                    Game.Qualifier.Clear(this);
                    Game.TeamQualifier.Clear(this);
                    Game.Champion.Clear(this);

                    RemoveScreenSpawn(this.Entity, false);

                    #region Friend/Enemy/TradePartner/Apprentice
                    try
                    {
                        Message msg = new Message("Your friend, " + Entity.Name + ", He has left the game!.", System.Drawing.Color.Red, Message.TopLeft);
                        if (Friends == null)
                            Friends = new SafeDictionary<uint, Game.ConquerStructures.Society.Friend>(100);
                        foreach (Game.ConquerStructures.Society.Friend friend in Friends.Values)
                        {
                            if (friend.IsOnline)
                            {
                                var packet = new Network.GamePackets.KnownPersons(true)
                                {
                                    UID = Entity.UID,
                                    Type = Network.GamePackets.KnownPersons.RemovePerson,
                                    Name = Entity.Name,
                                    Online = false
                                };
                                friend.Client.Send(packet);
                                packet.Type = Network.GamePackets.KnownPersons.GetInfo;
                                if (friend != null)
                                {
                                    if (friend.Client != null)
                                    {
                                        friend.Client.Send(packet);
                                        friend.Client.Send(msg);
                                    }
                                }
                            }
                        }
                        if (Enemy == null)
                            Enemy = new SafeDictionary<uint, Game.ConquerStructures.Society.Enemy>(100);
                        foreach (Game.ConquerStructures.Society.Enemy enemy in Enemy.Values)
                        {
                            if (enemy.IsOnline)
                            {
                                var packet = new Network.GamePackets.KnownPersons(true)
                                {
                                    UID = Entity.UID,
                                    Type = Network.GamePackets.KnownPersons.EnemyOffline,
                                    Name = Entity.Name,
                                    Online = false
                                };
                                enemy.Client.Send(packet);
                                packet.Type = Network.GamePackets.KnownPersons.EnemyOffline;
                                if (enemy != null)
                                {
                                    if (enemy.Client != null)
                                    {
                                        enemy.Client.Send(packet);
                                    }
                                }
                            }
                        }
                        Message msg2 = new Message("His couple, " + Entity.Name + ", has left the game!.", System.Drawing.Color.Red, Message.TopLeft);
                        if (Partners != null)
                        {
                            foreach (Game.ConquerStructures.Society.TradePartner partner in Partners.Values)
                            {
                                if (partner.IsOnline)
                                {
                                    var packet = new TradePartner(true)
                                    {
                                        UID = Entity.UID,
                                        Type = TradePartner.BreakPartnership,
                                        Name = Entity.Name,
                                        MinutesLeft = Game.Features.TradePartnerships.RemainingMinutes(partner.ProbationStartedOn),
                                        Online = false
                                    };
                                    partner.Client.Send(packet);
                                    packet.Type = TradePartner.AddPartner;
                                    if (partner != null)
                                    {
                                        if (partner.Client != null)
                                        {
                                            partner.Client.Send(packet);
                                            partner.Client.Send(msg2);
                                        }
                                    }
                                }
                            }
                        }
                        MentorInformation Information = new MentorInformation(true);
                        Information.Mentor_Type = 1;
                        Information.Mentor_ID = Entity.UID;
                        Information.Mentor_Level = Entity.Level;
                        Information.Mentor_Class = Entity.Class;
                        Information.Mentor_PkPoints = Entity.PKPoints;
                        Information.Mentor_Mesh = Entity.Mesh;
                        Information.Mentor_Online = false;
                        Information.String_Count = 3;
                        Information.Mentor_Name = Entity.Name;
                        Information.Mentor_Spouse_Name = Entity.Spouse;
                        foreach (var appr in Apprentices.Values)
                        {
                            if (appr.IsOnline)
                            {
                                Information.Apprentice_ID = appr.ID;
                                Information.Enrole_Date = appr.EnroleDate;
                                Information.Apprentice_Name = appr.Name;
                                appr.Client.Send(Information);
                                appr.Client.ReviewMentor();
                            }
                        }
                        if (Mentor != null)
                        {
                            if (Mentor.IsOnline)
                            {
                                ApprenticeInformation AppInfo = new ApprenticeInformation();
                                AppInfo.Apprentice_ID = Entity.UID;
                                AppInfo.Apprentice_Level = Entity.Level;
                                AppInfo.Apprentice_Name = Entity.Name;
                                AppInfo.Apprentice_Online = false;
                                AppInfo.Apprentice_Spouse_Name = Entity.Spouse;
                                AppInfo.Enrole_date = Mentor.EnroleDate;
                                AppInfo.Mentor_ID = Mentor.Client.Entity.UID;
                                AppInfo.Mentor_Mesh = Mentor.Client.Entity.Mesh;
                                AppInfo.Mentor_Name = Mentor.Client.Entity.Name;
                                AppInfo.Type = 2;
                                Mentor.Client.Send(AppInfo);
                            }
                        }
                    }
                    catch { }
                    #endregion
                    #region Team
                    if (Team != null)
                    {
                        if (Team.TeamLeader)
                        {
                            Network.GamePackets.Team team = new Network.GamePackets.Team();
                            team.UID = Account.EntityID;
                            team.Type = Network.GamePackets.Team.Dismiss;
                            foreach (GameState Teammate in Team.Teammates)
                            {
                                if (Teammate != null)
                                {
                                    if (Teammate.Entity.UID != Account.EntityID)
                                    {
                                        Teammate.Send(team);
                                        Teammate.Team = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Network.GamePackets.Team team = new Network.GamePackets.Team();
                            team.UID = Account.EntityID;
                            team.Type = Network.GamePackets.Team.ExitTeam;
                            foreach (GameState Teammate in Team.Teammates)
                            {
                                if (Teammate != null)
                                {
                                    if (Teammate.Entity.UID != Account.EntityID)
                                    {
                                        Teammate.Send(team);
                                        Teammate.Team.Remove(this);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
                catch (Exception e)
                {
                    Server.SaveException(e);
                }
                finally
                {
                    ReleaseSessionRegistration(true);
                    //Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("(" + this.Entity.Name + " Your IP has come out:[" + this.Account.IP + "]", ConsoleColor.Red);
                    Database.InnerPowerTable.Save();

                }

                Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("[" + Entity.Name + " ] disconnected - Online Players: " + Kernel.GamePool.Count + "", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);

            }

        }
        public ClientWrapper Socket
        {
            get { return _socket; }
        }
        public string IP
        {
            get { return _socket.IP; }
        }
        #endregion
        #region Game
        public Database.ChiTable.ChiData ChiData;
        public List<ChiPowerStructure> ChiPowers;
        public List<ChiPowerStructure> Retretead_ChiPowers;
        private uint chiPoints;
        public uint ChiPoints
        {
            get { lock (ItemSyncRoot) return chiPoints; }
            set { lock (ItemSyncRoot) chiPoints = value; }
        }
        public void AdjustChiPoints(long delta)
        {
            lock (ItemSyncRoot) chiPoints = checked((uint)(chiPoints + delta));
        }

        public SafeDictionary<uint, DetainedItem> ClaimableItem = new SafeDictionary<uint, DetainedItem>(1000), DeatinedItem = new SafeDictionary<uint, DetainedItem>(1000);
        public bool DoSetOffline = true;
        public ushort OnlineTrainingPoints = 0;
        //public ushort TimeDropSlayer = 0;
        public Time32 LastTrainingPointsUp, LastTreasurePoints = Time32.Now.AddMinutes(1);
        public ushort VendingDisguise;
        private uint _BlessTime;
        public uint BlessTime
        {
            get { return _BlessTime; }
            set
            {
                _BlessTime = value;
                if (Entity != null)
                    Entity.UpdateDatabase("BlessTime", value);
            }
        }

        public Game.Entity Companion;
        public List<GameState> Prayers;
        public GameState PrayLead;
        public DateTime ChatBanTime;
        public uint ChatBanLasts;
        public bool ChatBanned;
        #region Screen Color System
        public static DateTime StartDate;
        public static uint ScreenColor = 0;// color and night 
        #region Night Color

        public void Night()
        {
            ScreenColor = 5855577;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Night1()
        {
            ScreenColor = 3358767;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Night2()
        {
            ScreenColor = 97358;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        #endregion
        #region Blue Color
        public void Blue()
        {
            ScreenColor = 69852;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Blue1()
        {
            ScreenColor = 4532453;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Blue2()
        {
            ScreenColor = 684533;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        #endregion
        #region Green Color
        public void Green()
        {
            ScreenColor = 838915;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Green1()
        {
            ScreenColor = 824383;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Green2()
        {
            ScreenColor = 456828;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Green3()
        {
            ScreenColor = 5547633;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        public void Green4()
        {
            ScreenColor = 453450;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        #endregion
        #region Day Color
        public void Day()
        {
            ScreenColor = 0;

            Network.GamePackets.ScreenColor Packet = new Network.GamePackets.ScreenColor(true);
            Packet.UID = this.Entity.UID;
            Packet.ID = 104;
            Packet.dwParam = ScreenColor;
            foreach (GameState pclient in Kernel.GamePool.Values)
            {
                pclient.Send(Packet);
            }
        }
        #endregion

        #endregion  
        public byte JewelarLauKind, JewelarLauGems;
        public uint VirtuePoints;
        private object pkSyncRoot;
        internal object PkSyncRoot {
            get {
                if(pkSyncRoot==null)System.Threading.Interlocked.CompareExchange(ref pkSyncRoot,new object(),null);
                return pkSyncRoot;
            }
        }
        private object virtueSyncRoot;
        internal object VirtueSyncRoot {
            get {
                if(virtueSyncRoot==null)System.Threading.Interlocked.CompareExchange(ref virtueSyncRoot,new object(),null);
                return virtueSyncRoot;
            }
        }
        internal void AddVirtuePoints(uint amount) {
            lock(VirtueSyncRoot)VirtuePoints=(uint)Math.Min(uint.MaxValue,(ulong)VirtuePoints+amount);
        }
        public uint TQPoint;
        public DateTime LastLotteryEntry;
        public byte LotteryEntries;
        public bool InLottery;
        public DateTime OfflineTGEnterTime;
        public bool Mining = false;
        public Time32 MiningStamp;
        public ushort Vigor
        {
            get
            {
                if (Equipment != null)
                    if (!Equipment.Free(12))
                        return Equipment.TryGetItem((byte)12).Vigor;
                return 65535;
            }
            set
            {
                if (!Equipment.Free(12))
                    Equipment.TryGetItem((byte)12).Vigor = value;
            }
        }
        ushort _Maxvigor;
        public ushort MaxVigor
        {
            get { return _Maxvigor; }
            set { _Maxvigor = value; }
        }
        private bool _HeadgearClaim, _NecklaceClaim, _ArmorClaim, _WeaponClaim, _RingClaim, _BootsClaim, _TowerClaim, _FanClaim;
        public bool HeadgearClaim
        {
            get
            {
                return _HeadgearClaim;
            }
            set
            {
                _HeadgearClaim = value;
                if (Entity != null)
                    Entity.UpdateDatabase("HeadgearClaim", value);
            }
        }
        public bool FanClaim
        {
            get
            {
                return _FanClaim;
            }
            set
            {
                _FanClaim = value;
                Entity.UpdateDatabase("FanClaim", value);
            }
        }
        public bool TowerClaim
        {
            get
            {
                return _TowerClaim;
            }
            set
            {
                _TowerClaim = value;
                Entity.UpdateDatabase("TowerClaim", value);
            }
        }
        public bool BootsClaim
        {
            get
            {
                return _BootsClaim;
            }
            set
            {
                _BootsClaim = value;
                Entity.UpdateDatabase("BootsClaim", value);
            }
        }
        public bool RingClaim
        {
            get
            {
                return _RingClaim;
            }
            set
            {
                _RingClaim = value;
                Entity.UpdateDatabase("RingClaim", value);
            }
        }
        public bool WeaponClaim
        {
            get
            {
                return _WeaponClaim;
            }
            set
            {
                _WeaponClaim = value;
                Entity.UpdateDatabase("WeaponClaim", value);
            }
        }
        public bool ArmorClaim
        {
            get
            {
                return _ArmorClaim;
            }
            set
            {
                _ArmorClaim = value;
                Entity.UpdateDatabase("ArmorClaim", value);
            }
        }
        public bool NecklaceClaim
        {
            get
            {
                return _NecklaceClaim;
            }
            set
            {
                _NecklaceClaim = value;
                Entity.UpdateDatabase("NecklaceClaim", value);
            }
        }
        public string PromoteItemNameNeed
        {
            get
            {
                if (Entity.Class % 10 == 0)
                    return " nothing";
                if (Entity.Class % 10 == 1)
                    if (Entity.Class / 10 == 4)
                        return " 5 Euxenite Ores and ";
                    else
                        return " nothing";
                if (Entity.Class % 10 == 2)
                    return " an Emerald and ";
                if (Entity.Class % 10 == 3)
                    return " a Meteor and ";
                if (Entity.Class % 10 == 4)
                    return " a MoonBox and ";
                return " nothing";
            }
        }
        public byte PromoteItemCountNeed
        {
            get
            {
                if (Entity.Class % 10 == 0)
                    return 0;
                if (Entity.Class % 10 == 1)
                    if (Entity.Class / 10 == 4)
                        return 5;
                    else return 0;
                if (Entity.Class % 10 == 2)
                    return 1;
                if (Entity.Class % 10 == 3)
                    return 1;
                if (Entity.Class % 10 == 4)
                    return 1;
                return 0;
            }
        }
        public uint PromoteItemNeed
        {
            get
            {
                if (Entity.Class % 10 == 0)
                    return 0;
                if (Entity.Class % 10 == 1)
                    if (Entity.Class / 10 == 4)
                        return 1072031;
                    else
                        return 0;
                if (Entity.Class % 10 == 2)
                    return 1080001;
                if (Entity.Class % 10 == 3)
                    return 1088001;
                if (Entity.Class % 10 == 4)
                    return 721020;
                return 0;
            }
        }
        public uint PromoteItemGain
        {
            get
            {
                if (Entity.Class % 10 == 0)
                    return 0;
                if (Entity.Class % 10 == 1)
                    if (Entity.Class / 10 == 4)
                        return 500067;
                    else
                        return 0;
                if (Entity.Class % 10 == 2)
                    return 0;
                if (Entity.Class % 10 == 3)
                    return 700031;
                if (Entity.Class % 10 == 4)
                    return 1088000;
                return 0;
            }
        }
        public uint PromoteLevelNeed
        {
            get
            {
                if (Entity.Class % 10 == 0)
                    return 15;
                if (Entity.Class % 10 == 1)
                    return 40;
                if (Entity.Class % 10 == 2)
                    return 70;
                if (Entity.Class % 10 == 3)
                    return 100;
                if (Entity.Class % 10 == 4)
                    return 110;
                return 0;
            }
        }
        public byte SelectedItem, UpdateType;
        public ushort UplevelProficiency;
        public UInt32 GuildJoinTarget = 0;
        public uint OnHoldGuildJoin = 0;
        public uint elitepoints = 0;
        public uint eliterank = 0;
        public bool SentRequest = false;
        public Game.ConquerStructures.Society.Guild Guild;
        public Game.ConquerStructures.Society.Guild.Member AsMember;
        public uint Arsenal_Donation = 0;
        public Game.ConquerStructures.Booth Booth;
        public bool RaceExcitement, RaceDecelerated, RaceGuard, RaceDizzy, RaceFrightened;
        public Time32 RaceExcitementStamp, GuardStamp, DizzyStamp, FrightenStamp, ChaosStamp, ExtraVigorStamp, DecelerateStamp;
        public uint RaceExcitementAmount, RaceExtraVigor;
        public GameCharacterUpdates SpeedChange;
        public void ApplyRacePotion(Enums.RaceItemType type, uint target)
        {
            switch (type)
            {
                case Enums.RaceItemType.FrozenTrap:
                    {
                        if (target != uint.MaxValue)
                        {
                            if (Map.Floor[Entity.X, Entity.Y, MapObjectType.StaticEntity])
                            {
                                StaticEntity item = new StaticEntity((uint)(Entity.X * 1000 + Entity.Y), Entity.X, Entity.Y, (ushort)Map.ID);
                                item.DoFrozenTrap(Entity.UID);
                                Map.AddStaticEntity(item);
                                Kernel.SendSpawn(item);
                            }
                        }
                        else
                        {
                            Entity.FrozenStamp = Time32.Now;
                            Entity.FrozenTime = 5;
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = Entity.UID;
                            update.Add(GameCharacterUpdates.Freeze, 0, 5);
                            SendScreen(update, true);
                            Entity.AddFlag((ulong)FlagsUpdate.Flags.Freeze,10,true);
                        }
                        break;
                    }
                case Enums.RaceItemType.RestorePotion:
                    {
                        Vigor += 2000;
                        if (Vigor > MaxVigor)
                            Vigor = MaxVigor;
                        Send(new Vigor(true) { Amount = Vigor });
                        break;
                    }
                case Enums.RaceItemType.ExcitementPotion:
                    {
                        if (RaceExcitement && RaceExcitementAmount > 50) return;
                        if (RaceDecelerated)
                        {
                            RaceDecelerated = false;
                            var upd = new GameCharacterUpdates(true);
                            upd.UID = Entity.UID;
                            upd.Remove(GameCharacterUpdates.Decelerated);
                            SendScreen(upd, true);
                        }
                        RaceExcitementStamp = Time32.Now;
                        RaceExcitement = true;
                        {
                            var upd = new GameCharacterUpdates(true);
                            upd.UID = Entity.UID;
                            upd.Add(GameCharacterUpdates.Accelerated, 50, 15, 25);
                            SendScreen(upd, true);
                            SpeedChange = upd;
                        }
                        RaceExcitementAmount = 50;
                        Entity.AddFlag((ulong)FlagsUpdate.Flags.OrangeSparkles,10,true);
                        break;
                    }
                case Enums.RaceItemType.SuperExcitementPotion:
                    {
                        if (RaceDecelerated)
                        {
                            RaceDecelerated = false;
                            var upd = new GameCharacterUpdates(true);
                            upd.UID = Entity.UID;
                            upd.Remove(GameCharacterUpdates.Decelerated);
                            SendScreen(upd, true);
                        }
                        RaceExcitementAmount = 200;
                        RaceExcitementStamp = Time32.Now;
                        RaceExcitement = true;
                        this.Entity.AddFlag((ulong)FlagsUpdate.Flags.SpeedIncreased,1,true);
                        {
                            var upd = new GameCharacterUpdates(true);
                            upd.UID = Entity.UID;
                            upd.Add(GameCharacterUpdates.Accelerated, 200, 15, 100);
                            SendScreen(upd, true);
                            SpeedChange = upd;
                        }
                        Entity.AddFlag((ulong)FlagsUpdate.Flags.OrangeSparkles,15,true);
                        break;
                    }
                case Enums.RaceItemType.GuardPotion:
                    {
                        RaceGuard = true;
                        GuardStamp = Time32.Now;
                        Entity.AddFlag((ulong)FlagsUpdate.Flags.GodlyShield,10,true);
                        DizzyStamp = DizzyStamp.AddSeconds(-100);
                        FrightenStamp = FrightenStamp.AddSeconds(-100);
                        var upd = new GameCharacterUpdates(true);
                        upd.UID = Entity.UID;
                        upd.Add(GameCharacterUpdates.DivineShield, 0, (uint)Revora.Game.Features.BloodshedSeaBoosts.CoordinateRaceGuard(this));
                        SendScreen(upd, true);
                        break;
                    }
                case Enums.RaceItemType.DizzyHammer:
                    {
                        Entity Target;
                        if (Screen.TryGetValue(target, out Target))
                        {
                            var Owner = Target.Owner;
                            if (Owner != null)
                            {
                                if (!Owner.RaceGuard && !Owner.RaceFrightened)
                                {
                                    Owner.Entity.ShockStamp = Time32.Now;
                                    Owner.Entity.Shock = 5;
                                    Owner.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stun,5,true);
                                    {
                                        var upd = new GameCharacterUpdates(true);
                                        upd.UID = Owner.Entity.UID;
                                        upd.Add(GameCharacterUpdates.SoulShacle, 0, 5);
                                        Owner.SendScreen(upd, true);
                                    }
                                }
                            }
                        }
                        break;
                    }
                case Enums.RaceItemType.ScreamBomb:
                    {
                        SendScreen(new SpellUse(true)
                        {
                            Attacker = Entity.UID,
                            SpellID = 9989,
                            SpellLevel = 0,
                            X = Entity.X,
                            Y = Entity.Y
                        }.AddTarget(Entity.UID, 0, null), true);
                        foreach (var obj in Screen.SelectWhere<Entity>(MapObjectType.Player,
                            (o) => Kernel.GetDistance(o.X, o.Y, Entity.X, Entity.Y) <= 10))
                        {
                            var Owner = obj.Owner;
                            if (!Owner.RaceGuard && !Owner.RaceDizzy)
                            {
                                Owner.RaceFrightened = true;
                                Owner.FrightenStamp = Time32.Now;
                                Owner.Entity.AddFlag((ulong)FlagsUpdate.Flags.FreezeSmall,20,true);
                                {
                                    var upd = new GameCharacterUpdates(true);
                                    upd.UID = Owner.Entity.UID;
                                    upd.Add(GameCharacterUpdates.Flustered, 0, 20);
                                    Owner.SendScreen(upd, true);
                                }
                            }
                        }
                        break;
                    }
                case Enums.RaceItemType.SpiritPotion:
                    {
                        var upd = new GameCharacterUpdates(true);
                        upd.UID = Entity.UID;
                        upd.Add(GameCharacterUpdates.Sprint, 0, 10);
                        SendScreen(upd, true);
                        ExtraVigorStamp = Time32.Now;
                        RaceExtraVigor = 2000;
                        break;
                    }
                case Enums.RaceItemType.ChaosBomb:
                    {
                        SendScreen(new SpellUse(true)
                        {
                            Attacker = Entity.UID,
                            SpellID = 9989,
                            SpellLevel = 0,
                            X = Entity.X,
                            Y = Entity.Y
                        }.AddTarget(Entity.UID, 0, null), true);
                        foreach (var obj in this.Screen.SelectWhere<Entity>(MapObjectType.Player,
                               (o) => Kernel.GetDistance(o.X, o.Y, Entity.X, Entity.Y) <= 10))
                        {
                            var Owner = obj.Owner;
                            if (!Owner.RaceGuard)
                            {
                                Owner.FrightenStamp = Time32.Now;
                                Owner.DizzyStamp = Owner.DizzyStamp.AddSeconds(-1000);
                                Owner.Entity.AddFlag((ulong)FlagsUpdate.Flags.ChaosCycle,15,true);
                                {
                                    var upd = new GameCharacterUpdates(true);
                                    upd.UID = Owner.Entity.UID;
                                    upd.Add(GameCharacterUpdates.Flustered, 0, 15);
                                    Owner.SendScreen(upd, true);
                                }
                            }
                        }
                        break;
                    }
                case Enums.RaceItemType.SluggishPotion:
                    {
                        SendScreen(new SpellUse(true)
                        {
                            Attacker = Entity.UID,
                            SpellID = 9989,
                            SpellLevel = 0,
                            X = Entity.X,
                            Y = Entity.Y
                        }.AddTarget(Entity.UID, 0, null), true);
                        foreach (var obj in this.Screen.SelectWhere<Entity>(MapObjectType.Player, o => Kernel.GetDistance(o.X, o.Y, Entity.X, Entity.Y) <= 10))
                        {
                            var Owner = obj.Owner;
                            if (!Owner.RaceGuard)
                            {
                                Owner.RaceDecelerated = true;
                                Owner.DecelerateStamp = Time32.Now;
                                if (Owner.RaceExcitement)
                                {
                                    Owner.RaceExcitement = false;

                                    var upd = new GameCharacterUpdates(true);
                                    upd.UID = Owner.Entity.UID;
                                    upd.Remove(GameCharacterUpdates.Accelerated);
                                    Owner.SendScreen(upd, true);
                                }
                                Owner.Entity.AddFlag((ulong)FlagsUpdate.Flags.PurpleSparkles,10,true);
                                {
                                    var upd = new GameCharacterUpdates(true);
                                    upd.UID = Owner.Entity.UID;
                                    unchecked { upd.Add(GameCharacterUpdates.Decelerated, 50, 10, (uint)(0 - 25)); }
                                    Owner.SendScreen(upd, true);
                                    Owner.SpeedChange = upd;
                                }
                            }
                        }
                        break;
                    }
                case Enums.RaceItemType.TransformItem:
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (Potions[i] != null)
                            {
                                if (Potions[i].Type != Enums.RaceItemType.TransformItem)
                                {
                                    Send(new RacePotion(true)
                                    {
                                        Amount = 0,
                                        Location = i + 1,
                                        PotionType = Potions[i].Type
                                    });
                                    Potions[i] = null;
                                }
                            }
                        }
                        {
                            int i = 0;
                            if (Potions[i] == null)
                            {
                                int val = (int)Enums.RaceItemType.TransformItem;
                                while (val == (int)Enums.RaceItemType.TransformItem)
                                    val = Kernel.Random.Next((int)Enums.RaceItemType.ChaosBomb, (int)Enums.RaceItemType.SuperExcitementPotion);
                                Potions[i] = new UsableRacePotion();
                                Potions[i].Count = 1;
                                Potions[i].Type = (Enums.RaceItemType)val;
                                Send(new RacePotion(true)
                                {
                                    Amount = 1,
                                    Location = i + 1,
                                    PotionType = Potions[i].Type
                                });
                            }
                        }
                        break;
                    }
            }
        }
        internal bool AmbiguousMentor;
        public void ReviewMentor() { Revora.Game.Features.BattlePowerSharing.RefreshMentor(this); }
        public void AddBless(uint value)
        {
            lock (ItemSyncRoot ?? this)
            lock (Revora.Game.Features.MentorExperience.Sync)
            {
                if (Entity == null || Entity.PersistenceRequiresReload) return;
                AddBlessCore(value);
            }
        }
        private void AddBlessCore(uint value)
        {
            Revora.Game.Features.BlessingBalance.Grant(this, value);
        }
        public ulong PrizeExperience;
        public ushort PrizeHeavenBlessing;
        public ushort PrizePlusStone;
        public uint MentorApprenticeRequest;
        public object[] OnMessageBoxEventParams;
        public Action<GameState> MessageOK;
        public Action<GameState> MessageCancel;
        public bool JustLoggedOn = true;
        public Time32 ReviveStamp = Time32.Now;
        public bool Attackable;
        public Game.ConquerStructures.NobilityInformation NobilityInformation;
        public Game.Entity Entity;
        public Game.Screen Screen;
        public Time32 LastPing = Time32.Now;
        public static ushort NpcTestType = 0;
        public byte TinterItemSelect = 0;
        public DateTime LastDragonBallUse, LastResetTime;
        public byte Action = 0;
        public bool CheerSent = false;
        public Qualifier.QualifierList.QualifierGroup WatchingGroup;
        public Qualifier.QualifierList.QualifierGroup QualifierGroup;
        public QualifierStatistic ArenaStatistic;
        public Champion.QualifierList.QualifierGroup ChampionGroup;
        public ChampionStatistic ChampionStats;
        public TeamQualifier.QualifierList.QualifierGroup TeamWatchingGroup;
        public TeamQualifier.QualifierList.QualifierGroup TeamQualifierGroup;
        public TeamQualifierStatistic TeamArenaStatistic;
        public ElitePK.FighterStats ElitePKStats;
        public ElitePK.Match ElitePKMatch, WatchingElitePKMatch;
        public KillTournament SelectionKillTournament;
        public uint ArenaPoints
        {
            get { return ArenaStatistic.ArenaPoints; }
            set { ArenaStatistic.ArenaPoints = TeamArenaStatistic.ArenaPoints = value; }
        }
        private byte xpCount;
        public byte XPCount
        {
            get { return xpCount; }
            set
            {
                xpCount = value;
                if (xpCount >= 100) xpCount = 100;
                FlagsUpdate update = new FlagsUpdate(true);
                update.UID = Entity.UID;
                update.Append((byte)FlagsUpdate.DataType.XPCircle, xpCount);
                update.Send(this);
            }
        }
        public Time32 XPCountStamp = Time32.Now;
        #region New acc Reg.
        public string accountname;
        public string accountpass1;
        public string accountpass2;
        public string accountEmail;
        #endregion
        public Game.ConquerStructures.Trade Trade;
        public TradePartner TradePartners;
        public byte ExpBalls = 0;
        public ulong MoneySave = 0;
        //public ulong BankCps = 0;
        public uint ActiveNpc = 0;
        internal object MrMsBallot;
        public bool ForgetPassword = false, FinishForget = false;
        private uint _WarehousePW;
        public uint WarehousePW
        {
            get { return _WarehousePW; }
            set
            {
                _WarehousePW = value;
                if (Entity != null)
                    if (Entity.FullyLoaded)
                        Entity.UpdateDatabase("WarehousePW", value);
            }
        }
        public string TempPassword;
        public bool WarehouseOpen;
        public Time32 CoolStamp;
        public sbyte WarehouseOpenTries;
        public ushort InputLength;
        public Game.ConquerStructures.Society.Mentor Mentor;
        public Game.ConquerStructures.Society.Apprentice AsApprentice;
        public SafeDictionary<ushort, IProf> Proficiencies;
        public SafeDictionary<ushort, ISkill> Spells;
        public SafeDictionary<uint, Game.ConquerStructures.Society.Friend> Friends;
        public SafeDictionary<uint, Game.ConquerStructures.Society.Enemy> Enemy;
        public SafeDictionary<uint, Game.ConquerStructures.Society.TradePartner> Partners;
        public SafeDictionary<uint, Game.ConquerStructures.Society.Apprentice> Apprentices;
        public Inventory Inventory;
        public Equipment Equipment;
        public SafeDictionary<Game.ConquerStructures.Warehouse.WarehouseID, Game.ConquerStructures.Warehouse> Warehouses;
        public Game.ConquerStructures.Team Team;
        public Time32 lastClientJumpTime = Time32.Now;
        public Time32 lastJumpTime = Time32.Now;
        public int LastJumpTime = 0;
        public short lastJumpDistance = 0;
        public bool DoubleExpToday = false;
        public bool TimeDropSlayerToday = false;
        public bool JadeDragonPendantDone = false;
        public bool SupremeJadeDone = false;
        #region Return Plan
         public bool copaslayer = false;
        public bool vip7 = false;
        public bool equipo = false;
        public bool CopaDiaria= false;
        public bool  Frac= false;
        public bool Montura= false;
        public bool Arma= false;
        public bool Menos= false;
        public bool Perfeccion= false;
        #endregion
        private Game.Map map;
        public Game.Map Map
        {
            get
            {
                if (map == null)
                {
                    if (Entity == null) return null;
                    Kernel.Maps.TryGetValue(Entity.MapID, out map);

                    if (map == null)
                    {
                        if (Fake || !Database.MapsTable.MapInformations.ContainsKey(Entity.MapID))
                            return null;
                        return (map = new Game.Map(Entity.MapID, Database.MapsTable.MapInformations[Entity.MapID].BaseID, Database.DMaps.MapPaths[Database.MapsTable.MapInformations[Entity.MapID].BaseID]));
                    }
                }
                else
                {
                    if (map.ID != Entity.MapID)
                    {
                        Kernel.Maps.TryGetValue(Entity.MapID, out map);

                        if (map == null)
                        {
                            if (Fake || !Database.MapsTable.MapInformations.ContainsKey(Entity.MapID))
                                return null;
                            return (map = new Game.Map(Entity.MapID, Database.MapsTable.MapInformations[Entity.MapID].BaseID, Database.DMaps.MapPaths[Database.MapsTable.MapInformations[Entity.MapID].BaseID]));
                        }
                    }
                }
                return map;
            }
        }
        public void BindWorldMap(ushort mapId)
        {
            if (Entity != null)
                Entity.MapID = mapId;
            map = null;
            Kernel.Maps.TryGetValue(mapId, out map);
        }
        public ulong ExpBall
        {
            get
            {
                return Revora.Game.Features.ExperienceMath.Ball(Entity.Level);
            }
        }
        public bool AddProficiency(IProf proficiency)
        {
            if (this.Proficiencies.ContainsKey(proficiency.ID))
            {
                this.Proficiencies[proficiency.ID].Level = proficiency.Level;
                this.Proficiencies[proficiency.ID].Experience = proficiency.Experience;
                proficiency.Send(this);
                return false;
            }
            this.Proficiencies.Add(proficiency.ID, proficiency);
            proficiency.Send(this);
            return true;
        }
        public bool AddSpell(ISkill spell)
        {
            lock (ItemSyncRoot ?? this)
            {
                if (Entity != null && Entity.PersistenceRequiresReload) return false;
                return AddSpellCore(spell);
            }
        }
        private bool AddSpellCore(ISkill spell)
        {
            if (Spells.ContainsKey(spell.ID))
            {
                if (Spells[spell.ID].Level < spell.Level)
                {
                    Spells[spell.ID].Level = spell.Level;
                    Spells[spell.ID].Experience = spell.Experience;
                    if (spell.ID != 3060)
                    {
                        spell.Send(this);
                    }
                }
                return false;
            }
            else
            {
                Spells.Add(spell.ID, spell);
                if (spell.ID != 3060)
                {
                    spell.Send(this);
                }
                return true;
            }
        }
        public bool RemoveSpell(ushort id)
        {
            lock (ItemSyncRoot ?? this)
            {
                if (Entity != null && Entity.PersistenceRequiresReload) return false;
                return RemoveSpellCore(id);
            }
        }
        private bool RemoveSpellCore(ushort id)
        {
            if (Spells != null)
            {
                if (Spells.ContainsKey(id))
                {
                    SkillTable.DeleteSpell(this, id);
                    Spells.Remove(id);
                    Data buffer = new Data(true)
                    {
                        UID = Entity.UID,
                        dwParam = id,
                        ID = 0x6d
                    };
                    Send(buffer);
                    return true;
                }
            }
            return false;
        }
        public bool RemoveSpell(Interfaces.ISkill spell)
        {
            return spell != null && RemoveSpell(spell.ID);
        }
        public bool WentToComplete = false;
        public DateTime LoginAt = DateTime.MinValue;
        public byte SelectedGem = 0;
        public Time32 LastMentorSave = Time32.Now;
        public void IncreaseExperience(ulong experience, bool addMultiple)
        { IncreaseExperience(experience, addMultiple, true); }
        internal void IncreaseExperience(ulong experience, bool addMultiple, bool creditMentor)
        {
            lock (ItemSyncRoot ?? this)
            {
                if (Entity == null || Entity.PersistenceRequiresReload) return;
                IncreaseExperienceCore(experience, addMultiple, creditMentor);
            }
        }
        private void IncreaseExperienceCore(ulong experience, bool addMultiple, bool creditMentor)
        {
            if (Revora.Config.TrainingGroundBlocksExperience && Map.BaseID == 1039) return;
            if (Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)) return;
            if (Entity.Dead) return;
            byte level = Entity.Level;
            var restoration = Revora.Game.Features.ReincarnationRecovery.Snapshot(this);
            if (level >= Revora.Config.LevelCap)
            {
                if (restoration == null || restoration.Recovered) return;
                experience = 0;
            }
            if (addMultiple)
            {
                byte guildLevel = 0;
                if (Guild != null)
                    guildLevel = Guild.Level;
                experience = Revora.Config.ApplyKillExperience(experience, Entity.VIPLevel, Entity.Gems, Entity.DoubleExperienceTime, Entity.HeavenBlessing, Entity.Reborn, guildLevel, Entity.BattlePower);
                if (!Revora.Config.TrainingGroundBlocksExperience && Map.BaseID == 1039 && Revora.Config.TrainingGroundExperienceDivisor > 1)
                    experience /= Revora.Config.TrainingGroundExperienceDivisor;
            }
            var plan = new Revora.Game.Features.ExperienceRewardPlan(
                Revora.Game.Features.CharacterProgression.Capture(this), experience,
                Revora.Config.LevelCap, Revora.Config.AttributePointsPerLevel, restoration, creditMentor);
            ulong autoHunt = Entity.InAutoHunt
                ? checked(Entity.AutoHuntEXP + experience / Math.Max(1u, Revora.Config.AutoHuntExperienceDivisor)) : Entity.AutoHuntEXP;
            bool committed = plan.Restored != null || plan.Steps.Length != 0 && Revora.Game.Features.CharacterProgression.Managed(this);
            var effects = committed ? Revora.Game.Features.ExperienceRewards.Commit(this, plan) : null;
            if (!committed) Revora.Game.Features.MentorExperience.AwardLegacy(this, plan);
            ApplyExperienceReward(plan, effects, autoHunt, committed);
        }
        internal void ApplyExperienceReward(Revora.Game.Features.ExperienceRewardPlan plan,
            Revora.Game.Features.ExperienceRewards.Effects effects, ulong autoHunt, bool committed)
        {
            byte level = Entity.Level;
            try
            {
                if (committed) Revora.Game.Features.ExperienceRewards.ApplyCommitted(this, effects);
                if (Entity.InAutoHunt)
                    Entity.AutoHuntEXP = autoHunt;
                if (plan.DefaultSkill != 0 && !Spells.ContainsKey(plan.DefaultSkill))
                    AddSpell(new Network.GamePackets.Spell(true) { ID = plan.DefaultSkill });
                foreach (var step in plan.Steps)
                {
                    level = step.Level;
                    if (level == 70)
                    {
                        if (ArenaStatistic == null || ArenaStatistic.EntityID == 0)
                        {
                            ArenaStatistic = new Network.GamePackets.QualifierStatistic(true);
                            ArenaStatistic.EntityID = Entity.UID;
                            ArenaStatistic.Name = Entity.Name;
                            ArenaStatistic.Level = Entity.Level;
                            ArenaStatistic.Class = Entity.Class;
                            ArenaStatistic.Model = Entity.Mesh;
                            ArenaPoints = Database.ArenaTable.ArenaPointFill(Entity.Level);
                            ArenaStatistic.LastArenaPointFill = DateTime.Now;
                            Database.ArenaTable.InsertArenaStatistic(this);
                            ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                            Game.Qualifier.ArenaStatistics.Add(Entity.UID, ArenaStatistic);
                        }
                    }
                    if (step.Stats != null)
                    {
                        Entity.Strength = step.Stats.Strength; Entity.Vitality = step.Stats.Vitality;
                        Entity.Agility = step.Stats.Agility; Entity.Spirit = step.Stats.Spirit;
                        CalculateStatBonus();
                        CalculateHPBonus();
                        GemAlgorithm();
                    }
                    Entity.Atributes = step.Attributes;
                }
                bool changedLevel = Entity.Level != plan.After.Values[0];
                if (committed) Revora.Game.Features.CharacterProgression.ApplyCommitted(this, plan.After);
                else if (changedLevel) Entity.Level = (byte)plan.After.Values[0];
                if (changedLevel)
                {
                    if (Team != null)
                    {
                        if (Team.LowestLevelsUID == Entity.UID)
                        {
                            Team.LowestLevel = 0;
                            Team.LowestLevelsUID = 0;
                            Team.SearchForLowest();
                        }
                    }
                    Entity.Hitpoints = Entity.MaxHitpoints;
                    Entity.Mana = Entity.MaxMana;
                    if (Entity.Level > 130 && !committed)
                        Database.EntityTable.UpdateLevel(Entity.Owner);
                    Entity.Update(10, "xp", true);
                }
                if (Entity.Experience != (ulong)plan.After.Values[1])
                    Entity.Experience = (ulong)plan.After.Values[1];
            }
            catch (Exception ex)
            {
                if (committed) Revora.Game.Features.QuestInventory.Fail(this, "experience-projection", ex);
                throw;
            }
        }
        public void IncreaseSpellExperience(uint experience, ushort id)
        {
            lock (ItemSyncRoot ?? this)
            {
                if (Entity == null || Entity.PersistenceRequiresReload) return;
                IncreaseSpellExperienceCore(experience, id);
            }
        }
        private void IncreaseSpellExperienceCore(uint experience, ushort id)
        {
            ISkill skill; SafeDictionary<byte, SpellInformation> levels; SpellInformation information, next;
            if (Spells == null || !Spells.TryGetValue(id, out skill) || skill == null || skill.ID != id
                || !SpellTable.SpellInformations.TryGetValue(id, out levels) || levels == null
                || !levels.TryGetValue(skill.Level, out information) || information == null || information.ID != id
                || information.Level != skill.Level || information.NeedExperience == 0 || Entity.Level < information.NeedLevel
                || skill.Level == byte.MaxValue || !levels.TryGetValue((byte)(skill.Level + 1), out next)
                || next == null || next.ID != id || next.Level != skill.Level + 1) return;
            if (id == 0x50a || id == 0x13a6 || id == 0x1b76) experience = 100;
            uint total = Revora.Game.Features.WeaponSkillMastery.Accumulate(skill.Experience, experience,
                information.NeedExperience, Config.SpellExperienceRate, Config.SpellExperienceRateMultiplier,
                Entity.Gems[6], Map.BaseID == 1039 ? Config.TrainingGroundSpellExperienceDivisor : 1u,
                Entity.VIPLevel > 0 ? Config.VipSpellExperienceMultiplier : 1u);
            if (total >= information.NeedExperience)
            {
                skill.Experience = 0; skill.Level++;
                if (Revora.Game.Features.QuestInventory.Player(this) && Revora.Game.Features.WeaponSkillMastery.Fixed(skill))
                {
                    SkillTable.SaveSpells(this);
                    if (Entity.PersistenceRequiresReload) return;
                }
                Send(Constants.SpellLeveled); skill.Send(this);
            }
            else
            {
                skill.Experience = total;
                var update = new SkillExperience(true); update.AppendSpell(skill.ID, skill.Experience); update.Send(this);
            }
        }
        public void IncreaseProficiencyExperience(uint experience, ushort id)
        {
            if (experience == 0) return;
            IProf prof;
            if (!Proficiencies.TryGetValue(id, out prof))
            {
                prof = new Proficiency(true) { ID = id };
                AddProficiency(prof);
            }
            if (prof.Level >= Revora.Config.ProficiencyLevelCap) return;

            // Keep intermediate awards wider than the stored uint. A high-damage
            // boss hit must not wrap before its rate, gem and VIP bonuses apply.
            decimal remaining = 0;
            for (int level = prof.Level; level < Revora.Config.ProficiencyLevelCap; level++)
                remaining += Database.DataHolder.ProficiencyLevelExperience((byte)level);
            remaining = Math.Max(0, remaining - prof.Experience);
            decimal award = (decimal)experience * Revora.Config.EffectiveProficiencyExperienceRate;
            award += decimal.Truncate(award * Entity.Gems[5] / 100m);
            if (Map.BaseID == 0x40f && Revora.Config.TrainingGroundProficiencyExperienceDivisor > 1)
                award = decimal.Truncate(award / Revora.Config.TrainingGroundProficiencyExperienceDivisor);
            // Saturation is safe after the only reducing operation. Bound the
            // VIP multiplication by the finite EXP required to reach the cap.
            award = Math.Min(award, remaining);
            if (Entity.VIPLevel > 0) award *= Revora.Config.VipProficiencyExperienceMultiplier;
            decimal balance = prof.Experience + Math.Min(award, remaining);
            bool leveled = false;
            while (prof.Level < Revora.Config.ProficiencyLevelCap
                && balance >= Database.DataHolder.ProficiencyLevelExperience(prof.Level))
            {
                balance -= Database.DataHolder.ProficiencyLevelExperience(prof.Level);
                prof.Level++;
                leveled = true;
                Send(Constants.ProficiencyLeveled);
            }
            prof.Experience = prof.Level == Revora.Config.ProficiencyLevelCap ? 0 : checked((uint)balance);
            if (leveled) prof.Send(this);
            else
            {
                SkillExperience update = new SkillExperience(true);
                update.AppendProficiency(prof.ID, prof.Experience, Database.DataHolder.ProficiencyLevelExperience(prof.Level));
                update.Send(this);
            }
        }
        public byte ExtraAtributePoints(byte level, byte mClass)
        { return Revora.Game.Features.RebirthRules.Bonus(level, mClass); }
        public static ISkill LearnableSpell(ushort spellid)
        {
            ISkill spell = new Spell(true);
            spell.ID = spellid;
            return spell;
        }
        public bool Reborn(byte toClass)
        {
            lock (ItemSyncRoot ?? this)
            {
                if (Entity == null || Entity.PersistenceRequiresReload || Entity.Reborn != 1) return false;
                return RebornCore(toClass);
            }
        }
        private bool RebornCore(byte toClass)
        {
            #region Items
            if (Inventory.Count > 37) return false;
            switch (toClass)
            {
                case 11:
                case 21:
                case 51:
                case 61:
                case 71:
                    {
                        Inventory.Add(410077, Game.Enums.ItemEffect.Poison);
                        break;
                    }
                case 41:
                    {
                        Inventory.Add(500057, Game.Enums.ItemEffect.Shield);
                        break;
                    }
                case 132:
                case 142:
                    {
                        if (toClass == 132)
                            Inventory.Add(421077, Game.Enums.ItemEffect.MP);
                        else
                            Inventory.Add(421077, Game.Enums.ItemEffect.HP);
                        break;
                    }
            }
            #region Low level items
            for (byte i = 1; i < 9; i++)
            {
                if (i != 7)
                {
                    ConquerItem item = Equipment.TryGetItem(i);
                    if (item != null && item.ID != 0)
                    {
                        try
                        {
                            //UnloadItemStats(item, false);
                            Database.ConquerItemInformation cii = new Database.ConquerItemInformation(item.ID, item.Plus);
                            item.ID = cii.LowestID(ItemHandler.ItemMinLevel(ItemHandler.ItemPosition(item.ID)));
                            item.Mode = Game.Enums.ItemMode.Update;
                            item.Send(this);
                            LoadItemStats();
                            Database.ConquerItemTable.UpdateItemID(item, this);
                        }
                        catch
                        {
                            Console.WriteLine("Reborn item problem: " + item.ID);
                        }
                    }
                }
            }
            ConquerItem hand = Equipment.TryGetItem(5);
            if (hand != null)
            {
                Equipment.Remove(5);
                CalculateStatBonus();
                CalculateHPBonus();
            }
            hand = Equipment.TryGetItem(25);
            if (hand != null)
            {
                Equipment.Remove(25);
                CalculateStatBonus();
                CalculateHPBonus();
            }
            LoadItemStats();
            SendScreen(Entity.CharBuffer.ToConquerPacket(10014), false);
            #endregion
            #endregion
            if (Entity.Reborn == 0)
            {
                Entity.FirstRebornClass = Entity.Class;
                Entity.FirstRebornLevel = Entity.Level;
                Entity.Atributes = (ushort)(ExtraAtributePoints(Entity.FirstRebornLevel, Entity.FirstRebornClass) + 52);
            }
            else
            {
                Entity.SecondRebornClass = Entity.Class;
                Entity.SecondRebornLevel = Entity.Level;
                Entity.Atributes = (ushort)(ExtraAtributePoints(Entity.FirstRebornLevel, Entity.FirstRebornClass) +
                    ExtraAtributePoints(Entity.SecondRebornLevel, Entity.SecondRebornClass) + 62);
            }
            byte PreviousClass = Entity.Class;
            Entity.Reborn++;
            Revora.Game.Features.SoulTrialSystem.OnReborn(this);
            Revora.Game.Features.PathHuntSystem.OnReborn(this);
            Revora.Game.Features.CourierLoopSystem.OnReborn(this);
            Entity.Class = toClass;
            Entity.Level = 15;
            Entity.Experience = 0;
            Entity.UpdateDatabase("Reborn", Entity.Reborn);
            Entity.UpdateDatabase("FirstRebornClass", Entity.FirstRebornClass);
            Entity.UpdateDatabase("SecondRebornClass", Entity.SecondRebornClass);
            Entity.UpdateDatabase("FirstRebornLevel", Entity.FirstRebornLevel);
            Entity.UpdateDatabase("SecondRebornLevel", Entity.SecondRebornLevel);
            Entity.UpdateDatabase("Atributes", Entity.Atributes);
            try
            {
                Database.EntityTable.SaveEntity(this);
            }
            catch
            {
            }
            #region Spells
            Interfaces.ISkill[] spells = Spells.Values.ToArray();
            foreach (Interfaces.ISkill spell in spells)
            {
                spell.PreviousLevel = spell.Level;
                spell.Level = 0;
                spell.Experience = 0;
                #region KungFuKing
                if (PreviousClass == 85)
                {
                    if (Entity.Class != 81)
                    {
                        switch (spell.ID)
                        {
                            case 12120:
                            case 12130:
                            case 12140:
                            case 12160:
                            case 12170:
                            case 12200:
                            case 12240:
                            case 12350:
                            case 12270:
                            case 12280:
                            case 12290:
                            case 12300:
                            case 12320:
                            case 12330:
                            case 12340:
                                RemoveSpell(spell);
                                break;
                        }
                    }
                }
                #endregion
                #region Pirate

                if (PreviousClass == 75)
                {
                    if (Entity.Class != 71)
                    {
                        switch (spell.ID)
                        {
                            case 11110:
                            case 11040:
                            case 11050:
                            case 11060:
                            case 11100:
                            case 11120:
                            case 11130:
                            case 11030:
                                RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                #region Monk

                if (PreviousClass == 65)
                {
                    if (Entity.Class != 61)
                    {
                        switch (spell.ID)
                        {
                            case 10490:
                            case 10415:
                            case 10381:
                                RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                #region Warrior

                if (PreviousClass == 25)
                {
                    if (Entity.Class != 21)
                    {
                        switch (spell.ID)
                        {
                            case 1025:
                                if (Entity.Class != 21 && Entity.Class != 132)
                                    RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                #region Ninja

                if (toClass != 51)
                {
                    switch (spell.ID)
                    {
                        case 6010:
                        case 6000:
                        case 6011:
                            RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region Trojan

                if (toClass != 11)
                {
                    switch (spell.ID)
                    {
                        case 1115:
                            RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region Archer

                if (toClass != 41)
                {
                    switch (spell.ID)
                    {
                        case 8001:
                        case 8000:
                        case 8003:
                        case 9000:
                        case 8002:
                        case 8030:
                            RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region WaterTaoist

                if (PreviousClass == 135)
                {
                    if (toClass != 132)
                    {
                        switch (spell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1010:
                            case 1125:
                            case 1100:
                            case 8030:
                                RemoveSpell(spell);
                                break;
                            case 1050:
                            case 1175:
                            case 1170:
                                if (toClass != 142)
                                    RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                #region FireTaoist

                if (PreviousClass == 145)
                {
                    if (toClass != 142)
                    {
                        switch (spell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1150:
                            case 1180:
                            case 1120:
                            case 1002:
                            case 1160:
                            case 1165:
                                RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                #region Windwalker
                if (PreviousClass == 165)
                {
                    if (toClass != 161)
                    {
                        switch (spell.ID)
                        {
                            case 12830:
                            case 12840:
                            case 12850:
                            case 12860:
                            case 12870:
                            case 12880:
                            case 12890:
                            case 12930:
                            case 12940:
                            case 12950:
                            case 12960:
                            case 12970:
                            case 12980:
                            case 12990:
                            case 13000:
                            case 13020:
                            case 13030:
                            case 13190:
                            case 13050:
                            case 13060:
                            case 13070:
                            case 13080:
                            case 13090:
                            case 13360:
                                RemoveSpell(spell);
                                break;
                        }
                    }
                }
                #endregion
                if (Spells.ContainsKey(spell.ID))
                    if (spell.ID != (ushort)Game.Enums.SkillIDs.Reflect)
                        spell.Send(this);
            }
            #endregion
            #region Proficiencies
            foreach (Interfaces.IProf proficiency in Proficiencies.Values)
            {
                proficiency.PreviousLevel = proficiency.Level;
                proficiency.Level = 0;
                proficiency.Experience = 0;
                proficiency.Send(this);
            }
            #endregion
            #region Adding earned skills
            if (Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 9876 });
            if (toClass == 51 && PreviousClass == 55 && Entity.Reborn == 1)
                AddSpell(new Spell(true) { ID = 6002 });
            if (toClass == 81 && PreviousClass == 85 && Entity.Reborn == 1)
                AddSpell(new Spell(true) { ID = 12280 });
            if (Entity.FirstRebornClass == 85 && Entity.SecondRebornClass == 85 && Entity.Class == 81 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 12300 });
            if (Entity.FirstRebornClass == 15 && Entity.SecondRebornClass == 15 && Entity.Class == 11 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 10315 });
            if (Entity.FirstRebornClass == 25 && Entity.SecondRebornClass == 25 && Entity.Class == 21 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 10311 });
            if (Entity.FirstRebornClass == 45 && Entity.SecondRebornClass == 45 && Entity.Class == 41 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 10313 });
            if (Entity.FirstRebornClass == 55 && Entity.SecondRebornClass == 55 && Entity.Class == 51 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 6003 });
            if (Entity.FirstRebornClass == 65 && Entity.SecondRebornClass == 65 && Entity.Class == 61 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 10405 });
            if (Entity.FirstRebornClass == 135 && Entity.SecondRebornClass == 135 && Entity.Class == 132 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 30000 });
            if (Entity.FirstRebornClass == 145 && Entity.SecondRebornClass == 145 && Entity.Class == 142 &&
                Entity.Reborn == 2)
                AddSpell(new Spell(true) { ID = 10310 });
            if (Entity.Reborn == 1)
            {
                if (Entity.FirstRebornClass == 75 && Entity.Class == 71)
                {
                    AddSpell(new Spell(true) { ID = 3050 });
                }
                if (Entity.FirstRebornClass == 15 && Entity.Class == 11)
                {
                    AddSpell(new Spell(true) { ID = 3050 });
                }
                else if (Entity.FirstRebornClass == 25 && Entity.Class == 21)
                {
                    AddSpell(new Spell(true) { ID = 3060 });
                }
                else if (Entity.FirstRebornClass == 145 && Entity.Class == 142)
                {
                    AddSpell(new Spell(true) { ID = 3080 });
                }
                else if (Entity.FirstRebornClass == 135 && Entity.Class == 132)
                {
                    AddSpell(new Spell(true) { ID = 3090 });
                }
            }
            if (Entity.Reborn == 2)
            {
                if (Entity.SecondRebornClass == 75 && Entity.Class == 71)
                {
                    AddSpell(new Spell(true) { ID = 3050 });
                }
                if (Entity.SecondRebornClass == 15 && Entity.Class == 11)
                {
                    AddSpell(new Spell(true) { ID = 3050 });
                }
                else if (Entity.SecondRebornClass == 25)
                {
                    AddSpell(new Spell(true) { ID = 3060 });
                }
                else if (Entity.SecondRebornClass == 145 && Entity.Class == 142)
                {
                    AddSpell(new Spell(true) { ID = 3080 });
                }
                else if (Entity.SecondRebornClass == 135 && Entity.Class == 132)
                {
                    AddSpell(new Spell(true) { ID = 3090 });
                }
            }

            #endregion
            Database.DataHolder.GetStats(Entity.Class, Entity.Level, this);
            CalculateStatBonus();
            CalculateHPBonus();
            GemAlgorithm();
            using (var conn = Database.DataHolder.MySqlConnection)
            {
                conn.Open();
                Database.EntityTable.SaveEntity(this, conn);
                Database.SkillTable.SaveSpells(this, conn);
                Database.SkillTable.SaveProficiencies(this, conn);
                Database.SkillTable.SaveSpells(this);
                Database.SkillTable.SaveProficiencies(this);
            }
            Kernel.SendWorldMessage(new Message("" + Entity.Name + " Has obtained " + Entity.Reborn + " rebirths Congratulations!", Message.Center), Server.GamePool);
            return true;
        }

        #region Items
        private int StatHP;
        public void CalculateHPBonus()
        {
            switch (Entity.Class)
            {
                case 11: Entity.MaxHitpoints = (uint)(StatHP * 1.05F); break;
                case 12: Entity.MaxHitpoints = (uint)(StatHP * 1.08F); break;
                case 13: Entity.MaxHitpoints = (uint)(StatHP * 1.10F); break;
                case 14: Entity.MaxHitpoints = (uint)(StatHP * 1.12F); break;
                case 15: Entity.MaxHitpoints = (uint)(StatHP * 1.15F); break;
                default: Entity.MaxHitpoints = (uint)StatHP; break;
            }
            Entity.MaxHitpoints += Entity.ItemHP;
            Entity.MaxHitpoints += Entity.Intensification;
            Entity.Hitpoints = Math.Min(Entity.Hitpoints, Entity.MaxHitpoints);
        }
        public void CalculateHPWrangler()
        {
            foreach (var c in Entity.SubClasses.Classes)
            {
                int lvl = c.Value.Level;
                switch ((Game.ClassID)c.Value.ID)
                {
                    case Game.ClassID.Wrangler:
                        {
                            switch (Entity.SubClassLevel)
                            {
                                case 1:
                                    {
                                        Entity.MaxHitpoints += 100;
                                        break;
                                    }
                                case 2:
                                    {
                                        Entity.MaxHitpoints += 200;
                                        break;
                                    }
                                case 3:
                                    {
                                        Entity.MaxHitpoints += 300;
                                        break;
                                    }
                                case 4:
                                    {
                                        Entity.MaxHitpoints += 400;
                                        break;
                                    }
                                case 5:
                                    {
                                        Entity.MaxHitpoints += 500;
                                        break;
                                    }
                                case 6:
                                    {
                                        Entity.MaxHitpoints += 600;
                                        break;
                                    }
                                case 7:
                                    {
                                        Entity.MaxHitpoints += 800;
                                        break;
                                    }
                                case 8:
                                    {
                                        Entity.MaxHitpoints += 1000;
                                        break;
                                    }
                                case 9:
                                    {
                                        Entity.MaxHitpoints += 1200;
                                        break;
                                    }
                            }
                            break;
                        }
                }
            }
        }
        public void CalculateStatBonus()
        {
            byte ManaBoost = 5;
            const byte HitpointBoost = 24;
            sbyte Class = (sbyte)(Entity.Class / 10);
            if (Class == 13 || Class == 14)
                ManaBoost += (byte)(5 * (Entity.Class - (Class * 10)));
            StatHP = (ushort)((Entity.Strength * 3) +
                                     (Entity.Agility * 3) +
                                     (Entity.Spirit * 3) +
                                     (Entity.Vitality * HitpointBoost));
            Entity.MaxMana = (ushort)((Entity.Spirit * ManaBoost) + Entity.ItemMP);
            Entity.Mana = Math.Min(Entity.Mana, Entity.MaxMana);
        }
        public void SendStatMessage()
        {
            this.ReviewMentor();
            Network.GamePackets.Message Msg = new Network.GamePackets.Message("Your stats have been changed!", System.Drawing.Color.DarkGoldenrod
                , Network.GamePackets.Message.TopLeft);
            Msg.__Message = string.Format(Msg.__Message,
                new object[] { Entity.MinAttack, Entity.MaxAttack, Entity.MagicAttack, Entity.Defence, (Entity.MagicDefence + Entity.MagicDefence), Entity.Dodge, Entity.PhysicalDamageDecrease, Entity.MagicDamageDecrease, Entity.PhysicalDamageIncrease, Entity.MagicDamageIncrease, Entity.Hitpoints, Entity.MaxHitpoints, Entity.Mana, Entity.MaxMana, Entity.BattlePower });
            this.Send(Msg);
        }
        public void doAuraBonuses(ulong type, uint power, int i)
        {
            switch (type)
            {
                case (ulong)FlagsUpdate.Flags.EarthAura:
                    {
                        Entity.EarthResistance += (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.FireAura:
                    {
                        Entity.FireResistance += (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.MetalAura:
                    {
                        Entity.MetalResistance += (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.WoodAura:
                    {
                        Entity.WoodResistance += (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.WaterAura:
                    {
                        Entity.WaterResistance += (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.TyrantAura:
                    {
                        Entity.CriticalStrike += (int)power * i * 100;
                        Entity.SkillCStrike += (int)power * i * 100;
                        if (Entity.CriticalStrike > 120000)
                            Entity.CriticalStrike = 120000;
                        if (Entity.SkillCStrike > 120000)
                            Entity.SkillCStrike = 120000;
                        if (Entity.CriticalStrike < 0)
                            Entity.CriticalStrike = 0;
                        if (Entity.SkillCStrike < 0)
                            Entity.SkillCStrike = 0;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.FendAura:
                    {
                        Entity.Immunity += (int)power * i * 100; break;
                    }
            }
        }
        public void removeAuraBonuses(ulong type, uint power, int i)
        {
            switch (type)
            {
                case (ulong)FlagsUpdate.Flags.EarthAura:
                    {
                        Entity.EarthResistance -= (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.FireAura:
                    {
                        Entity.FireResistance -= (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.MetalAura:
                    {
                        Entity.MetalResistance -= (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.WoodAura:
                    {
                        Entity.WoodResistance -= (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.WaterAura:
                    {
                        Entity.WaterResistance -= (int)power * i;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.TyrantAura:
                    {
                        Entity.CriticalStrike -= (int)power * i * 100;
                        Entity.SkillCStrike -= (int)power * i * 100;
                        if (Entity.CriticalStrike > 120000)
                            Entity.CriticalStrike = 120000;
                        if (Entity.SkillCStrike > 120000)
                            Entity.SkillCStrike = 120000;
                        if (Entity.CriticalStrike < 0)
                            Entity.CriticalStrike = 0;
                        if (Entity.SkillCStrike < 0)
                            Entity.SkillCStrike = 0;
                        break;
                    }
                case (ulong)FlagsUpdate.Flags.FendAura:
                    {
                        Entity.Immunity -= (int)power * i * 100; break;
                    }
            }
        }
        public bool AreStatsLoadable(ConquerItem item)
        {
            if (!AlternateEquipment)
                if (item.Position > 20)
                    return false;
            if (AlternateEquipment)
                if (item.Position < 20)
                    if (!Equipment.Free((byte)(20 + item.Position)))
                        return false;

            int Position = item.Position;
            if (item.Position > 20) Position -= 20;
            if (Position == ConquerItem.LeftWeapon || Position == ConquerItem.RightWeapon)
                return false;

            return true;
        }
        public Tuple<ConquerItem, ConquerItem> ComputeWeapons()
        {
            if (!AlternateEquipment)
            {
                return new Tuple<ConquerItem, ConquerItem>(
                    Equipment.TryGetItem(ConquerItem.RightWeapon),
                    Equipment.TryGetItem(ConquerItem.LeftWeapon));
            }
            else
            {
                if (Equipment.Free(ConquerItem.AlternateRightWeapon))
                {
                    return new Tuple<ConquerItem, ConquerItem>(
                        Equipment.TryGetItem(ConquerItem.RightWeapon),
                        Equipment.TryGetItem(ConquerItem.LeftWeapon));
                }
                else
                {
                    if (Equipment.Free(ConquerItem.RightWeapon))
                    {
                        return new Tuple<ConquerItem, ConquerItem>(
                            Equipment.TryGetItem(ConquerItem.AlternateRightWeapon),
                            Equipment.TryGetItem(ConquerItem.AlternateLeftWeapon));
                    }
                    else
                    {
                        if (!Equipment.Free(ConquerItem.AlternateLeftWeapon))
                        {
                            return new Tuple<ConquerItem, ConquerItem>(
                                Equipment.TryGetItem(ConquerItem.AlternateRightWeapon),
                                Equipment.TryGetItem(ConquerItem.AlternateLeftWeapon));
                        }
                        else
                        {
                            if (Equipment.Free(ConquerItem.LeftWeapon))
                            {
                                return new Tuple<ConquerItem, ConquerItem>(
                                    Equipment.TryGetItem(ConquerItem.AlternateRightWeapon),
                                    null);
                            }
                            else
                            {
                                ConquerItem aRight = Equipment.TryGetItem(ConquerItem.AlternateRightWeapon),
                                             nLeft = Equipment.TryGetItem(ConquerItem.LeftWeapon);
                                if (ItemHandler.IsTwoHand(aRight.ID))
                                {
                                    if (ItemHandler.IsArrow(nLeft.ID))
                                    {
                                        if (ItemHandler.IsBow(aRight.ID))
                                        {
                                            return new Tuple<ConquerItem,
                                                ConquerItem>(aRight, nLeft);
                                        }
                                        else
                                        {
                                            return new Tuple<ConquerItem,
                                                ConquerItem>(aRight, null);
                                        }
                                    }
                                    else
                                    {
                                        if (ItemHandler.IsShield(nLeft.ID))
                                        {
                                            if (!Spells.ContainsKey(10311))//Perseverance
                                            {
                                                Send(new Message("You need to know Perseverance (Pure Warrior skill) to be able to wear 2-handed weapon and shield.", System.Drawing.Color.Red, Message.Talk));
                                                return new Tuple<ConquerItem,
                                                    ConquerItem>(aRight, null);
                                            }
                                            else
                                            {
                                                return new Tuple<ConquerItem,
                                                    ConquerItem>(aRight, nLeft);
                                            }
                                        }
                                        else
                                        {
                                            return new Tuple<ConquerItem,
                                                ConquerItem>(aRight, null);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!ItemHandler.IsTwoHand(nLeft.ID))
                                    {
                                        return new Tuple<ConquerItem,
                                            ConquerItem>(aRight, nLeft);
                                    }
                                    else
                                    {
                                        return new Tuple<ConquerItem,
                                            ConquerItem>(aRight, null);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public int[][] ChampionAllowedStats = new int[][]
        {
            new int[] {1, 0, 0, 0, 0, 0, 0, 30, 0, 0 },
            new int[] {2, 0, 0, 0, 0, 0, 0, 40, 1, 1 },
            new int[] {3, 1, 0, 0, 0, 0, 50, 50, 2, 3 },
            new int[] {4, 3, 1, 1, 0, 0, 100, 60, 5, 4 },
            new int[] {5, 5, 1, 1, 1, 0, 150, 70, 7, 5 },
            new int[] {6, 5, 1, 1, 1, 0, 200, 80, 9, 7 },
            new int[] {12, 7, 2, 2, 1, 1, 255, 100, 12, 9 }
        };
        public ushort Accuracy
        {
            get
            {
                if (Entity.EntityFlag == Game.EntityFlag.Monster) return 0;
                ushort _accuracy = 0;
                foreach (ConquerItem i in Equipment.Objects)
                {
                    if (i == null)
                    {
                        continue;
                    }
                    if (i.Position == Network.GamePackets.ConquerItem.LeftWeapon ||
                        i.Position == Network.GamePackets.ConquerItem.RightWeapon)
                    {
                        Database.ConquerItemInformation dbi = CombatEquipmentInformation(i);
                        if (dbi != null)
                        {
                            _accuracy += dbi.PlusInformation.Agility;
                        }
                    }
                }
                return _accuracy;
            }
        }
        public ushort AgilityItem
        {
            get
            {
                if (Entity.EntityFlag == Game.EntityFlag.Monster) return 0;
                ushort _AgilityItem = 0;
                foreach (ConquerItem i in Equipment.Objects)
                {
                    if (i == null)
                    {
                        continue;
                    }
                    Database.ConquerItemInformation dbi = CombatEquipmentInformation(i);
                    if (dbi != null)
                    {
                        _AgilityItem += dbi.BaseInformation.AgilityItem;
                    }
                }
                return _AgilityItem;
            }
        }
        public ushort MagicDefence
        {
            get
            {
                if (Entity.EntityFlag == Game.EntityFlag.Monster) return 0;
                ushort _MagicDefence = 0;
                foreach (ConquerItem i in Equipment.Objects)
                {
                    if (i == null)
                    {
                        continue;
                    }
                    if (i.Position == Network.GamePackets.ConquerItem.Armor ||
                        i.Position == Network.GamePackets.ConquerItem.Head ||
                        i.Position == Network.GamePackets.ConquerItem.Necklace)
                    {
                        Database.ConquerItemInformation dbi = CombatEquipmentInformation(i);
                        if (dbi != null)
                        {
                            _MagicDefence += dbi.BaseInformation.MagicDefence;
                        }
                    }
                }
                return _MagicDefence;
            }
        }
        private Database.ConquerItemInformation CombatEquipmentInformation(ConquerItem item)
        {
            uint type = DoChampStats ? Game.Features.Tournaments.LocalTeamPkCombat.SkillItemType(item) : item.ID;
            byte plus = DoChampStats ? (byte)Math.Min((int)item.Plus, ChampionLimits[0]) : item.Plus;
            return new Database.ConquerItemInformation(type, plus);
        }
        // Champion grades are1..7; the legacy table has zero-based rows.
        // Invalid stored grades use the most restrictive row until repaired.
        internal int[] ChampionLimits
        {
            get { int grade=ChampionStats==null?1:ChampionStats.Grade;return ChampionAllowedStats[grade>=1 && grade<=7?grade-1:0]; }
        }
        public bool DoChampStats
        {
            get { return ChampionGroup != null; }
        }
        private void CalculateVigor(ConquerItem item, Database.ConquerItemInformation dbi)
        {
            if (!Equipment.Free(12))
            {
                if (!this.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                {
                    this.Vigor = 0;
                    this.MaxVigor = 0;
                    MaxVigor += dbi.PlusInformation.Agility;
                    MaxVigor += 30;
                    if (!Equipment.Free(ConquerItem.SteedCrop))
                    {
                        if (Equipment.Objects[17] != null)
                        {
                            if (Equipment.Objects[17].ID % 10 == 9)
                            {
                                MaxVigor += 1000;
                            }
                            else if (Equipment.Objects[17].ID % 10 == 8)
                            {
                                MaxVigor += 700;
                            }
                            else if (Equipment.Objects[17].ID % 10 == 7)
                            {
                                MaxVigor += 500;
                            }
                            else if (Equipment.Objects[18].ID % 10 == 6)
                            {
                                MaxVigor += 300;
                            }
                            else if (Equipment.Objects[18].ID % 10 == 5)
                            {
                                MaxVigor += 100;
                            }
                        }
                    }
                    Vigor = MaxVigor;
                }
            }
        }

        public void LoadItemStats()
        {
            lock(ItemSyncRoot) LoadItemStatsCore();
        }
        private void ApplySubclassStats()
        {
            #region Subclasses
            bool skill = Game.Features.Tournaments.LocalTeamPkCombat.SkillStats(this);
            foreach (var c in Entity.SubClasses.Classes)
            {
                if(c.Value==null || c.Value.ID!=c.Key || c.Value.Phase<1 || c.Value.Phase>c.Value.Level || c.Value.Level>9)continue;
                int lvl = c.Value.Phase;
                if (DoChampStats && c.Value.ID != (byte)Game.ClassID.Apothecary) lvl = Math.Min(lvl, ChampionLimits[9]);
                if (skill && c.Value.ID != (byte)Game.ClassID.Apothecary) lvl = Math.Min(lvl, 5);
                switch ((Game.ClassID)c.Value.ID)
                {

                    #region ChiMaster
                    case Game.ClassID.ChiMaster:
                        {
                            var result3 = 0;
                            switch (lvl)
                            {
                                case 1: result3 = 100; break;
                                case 2: result3 = 200; break;
                                case 3: result3 = 300; break;
                                case 4: result3 = 400; break;
                                case 5: result3 = 600; break;
                                case 6: result3 = 800; break;
                                case 7: result3 = 1000; break;
                                case 8: result3 = 1200; break;
                                case 9: result3 = 1500; break;
                            }
                            Entity.Immunity += result3;
                            break;
                        }
                    #endregion
                    #region Sage
                    case Game.ClassID.Sage:
                        {
                            var result3 = 0;
                            switch (lvl)
                            {
                                case 1: result3 = 100; break;
                                case 2: result3 = 200; break;
                                case 3: result3 = 300; break;
                                case 4: result3 = 400; break;
                                case 5: result3 = 600; break;
                                case 6: result3 = 800; break;
                                case 7: result3 = 1000; break;
                                case 8: result3 = 1200; break;
                                case 9: result3 = 1500; break;
                            }
                            Entity.Penetration += result3;
                            break;
                        }
                    #endregion
                    #region Apothecary
                    case Game.ClassID.Apothecary:
                        {
                            ushort result3 = 0;
                            switch (lvl)
                            {
                                case 1: result3 = 8; break;
                                case 2: result3 = 16; break;
                                case 3: result3 = 24; break;
                                case 4: result3 = 32; break;
                                case 5: result3 = 40; break;
                                case 6: result3 = 48; break;
                                case 7: result3 = 56; break;
                                case 8: result3 = 64; break;
                                case 9: result3 = 72; break;
                            }
                            Entity.Detoxication += result3;
                            break;
                        }
                    //{
                    //    Player.Detoxication += (ushort)(Player.SubClassLevel * 8);
                    //    break;
                    //}
                    #endregion
                    #region Performer
                    case Game.ClassID.Performer:
                        {
                            uint bonus=(uint)(lvl==9?1000:lvl*100);
                            Entity.BaseMaxAttack += bonus;
                            Entity.BaseMinAttack += bonus;
                            Entity.BaseMagicAttack += bonus;
                            break;
                        }
                    #endregion
                    #region Wrangler
                    case Game.ClassID.Wrangler:
                        {
                            uint result = 0;
                            switch (lvl)
                            {
                                case 1: result = 100; break;
                                case 2: result = 200; break;
                                case 3: result = 300; break;
                                case 4: result = 400; break;
                                case 5: result = 500; break;
                                case 6: result = 600; break;
                                case 7: result = 800; break;
                                case 8: result = 1000; break;
                                case 9: result = 1200; break;
                            }
                            Entity.ItemHP += result;
                            break;
                        }
                    #endregion
                    #region MartialArtist
                    case Game.ClassID.MartialArtist:
                        {
                            var result1 = 0;
                            switch (lvl)
                            {
                                case 1: result1 = 100; break;
                                case 2: result1 = 200; break;
                                case 3: result1 = 300; break;
                                case 4: result1 = 400; break;
                                case 5: result1 = 600; break;
                                case 6: result1 = 800; break;
                                case 7: result1 = 1000; break;
                                case 8: result1 = 1200; break;
                                case 9: result1 = 1500; break;
                            }
                            Entity.CriticalStrike += result1;
                            break;
                        }
                    #endregion
                    #region Warlock
                    case Game.ClassID.Warlock:
                        {
                            var result2 = 0;
                            switch (lvl)
                            {
                                case 1: result2 = 100; break;
                                case 2: result2 = 200; break;
                                case 3: result2 = 300; break;
                                case 4: result2 = 400; break;
                                case 5: result2 = 600; break;
                                case 6: result2 = 800; break;
                                case 7: result2 = 1000; break;
                                case 8: result2 = 1200; break;
                                case 9: result2 = 1500; break;
                            }
                            Entity.SkillCStrike += result2;
                            break;
                        }
                        #endregion
                }
            }
            #endregion
        }

        private bool skillStatsApplied;
        internal bool ValidateSkillEquipment() { return SkillEquipmentIssue()==null; }
        internal string SkillEquipmentIssue() {
            lock(ItemSyncRoot) {
                if(Equipment==null)return "Your equipment is not ready. Speak with me again in a moment.";
                var selected=Equipment.Objects.Where(i=>i!=null && i.Durability!=0 && AreStatsLoadable(i)).ToList();
                var weapons=ComputeWeapons();
                if(weapons!=null && weapons.Item1!=null) {
                    selected.Add(weapons.Item1);
                    if(weapons.Item2!=null && (!weapons.Item1.IsTwoHander() || ItemHandler.IsArrow(weapons.Item2.ID)
                        || ItemHandler.IsHossu(weapons.Item2.ID) || Entity.Class>=20 && Entity.Class<=25))selected.Add(weapons.Item2);
                }
                foreach(var item in selected) {
                    try { Game.Features.Tournaments.LocalTeamPkCombat.SkillItemType(item); }
                    catch(InvalidOperationException) {
                        return item.ID==619301
                            ? "Unequip SoaringSpirit, your Hossu, before entering Skill Team PK. You may keep it in your bag."
                            : "Unequip gear that cannot meet the arena's limits, then speak with me again.";
                    }
                }
                return null;
            }
        }
        private void FinishSkillStats(bool skill,uint previousHp) {
            if(skill || skillStatsApplied)Entity.Hitpoints=Math.Min(previousHp,Entity.MaxHitpoints);
            skillStatsApplied=skill;
        }
        internal void RestoreSkillStats() {
            lock(ItemSyncRoot) {
                bool restore=Entity.InSkillPk || skillStatsApplied;
                Entity.InSkillPk=false;
                if(restore)LoadItemStatsCore();
            }
        }

        private void LoadItemStatsCore()
        {
            bool skill = Game.Features.Tournaments.LocalTeamPkCombat.SkillStats(this);
            if(skill && !ValidateSkillEquipment())throw new InvalidOperationException("Skill PK equipment preflight failed.");
            RuneProgress.RefreshSkills(this);
            uint bStats = Entity.Hitpoints;
            for (int i = 0; i < 30; i++)
                if (Equipment.Objects[i] != null)
                    Equipment.Objects[i].IsWorn = false;
            #region Set Every Variable to Zero
            Entity.CharBuffer.RelicPower = 0;
            Entity.Defence = 0;
            Entity.MagicDefence = 0;
            Entity.MagicDefencePercent = 0;
            Entity.BaseMagicAttack = 0;
            Entity.BaseMagicDefence =
            Entity.BaseMaxAttack =
            Entity.BaseMinAttack =
            Entity.PhysicalDamageDecrease =
            Entity.PhysicalDamageIncrease =
            Entity.MagicDamageDecrease =
            Entity.MagicDamageIncrease = 0;
            Entity.ItemHP = 0;
            Entity.ItemHP =
            Entity.ItemMP =
            Entity.AttackRange = (byte)0;
            Entity.Dodge = 0;
            Entity.MinAttack = 0;
            Entity.MaxAttack = 0;
            Entity.Defence = 0;
            Entity.SuperItemBless = 0;
            Entity.MagicDefence = 0;
            Entity.Dodge = 0;
            Entity.BaseMagicAttack = 0;
            Entity.WoodResistance = 0;
            Entity.FireResistance = 0;
            Entity.WaterResistance = 0;
            Entity.EarthResistance = 0;
            Entity.Breaktrough = 0;
            Entity.CriticalStrike = 0;
            Entity.Immunity = 0;
            Entity.Penetration = 0;
            Entity.Counteraction = 0;
            Entity.Block = 0;
            // _accuracy = 0;
            Entity.Detoxication = 0;
            Entity.Intensification = 0;
            Entity.Penetration = 0;
            Entity.SkillCStrike = 0;
            Entity.MaxAttack = 0;
            Entity.MinAttack = 0;
            Entity.PhysicalDamageDecrease = 0;
            Entity.MagicDamageDecrease = 0;
            Entity.MagicDamageIncrease = 0;
            Entity.PhysicalDamageIncrease = 0;
            Entity.MagicDefencePercent = 0;
            Entity.ItemHP = 0;
            Entity.ItemMP = 0;
            Entity.ItemBless = 0;
            Entity.AttackRange = 1;
            Entity.BaseMinAttack = 0;
            Entity.BaseMaxAttack = 0;
            Entity.BaseMagicDefence = 0;
            Entity.BaseDefence = 0;
            Entity.MagicDamageIncrease = 0;
            Entity.Gems = new UInt16[10];
            Entity.Weight = 0;
            Entity.Accuracy = 0;
            #endregion
            #region Reset Runes values
            RunesSystem.ResetRunesValues(Entity);
            #endregion
            foreach (ConquerItem i in Equipment.Objects)
            {
                if (i == null) continue;
                if (i.Durability == 0) continue;
                if (!AreStatsLoadable(i)) continue;
                loadItemStats(i);
            }

            Weapons = ComputeWeapons();
            if (Weapons == null) Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
            if (Weapons.Item1 != null)
            {
                loadItemStats(Weapons.Item1);
                if (Weapons.Item2 != null)
                {
                    if (!Weapons.Item1.IsTwoHander())
                        loadItemStats(Weapons.Item2);
                    else
                        if (ItemHandler.IsArrow(Weapons.Item2.ID) || (ItemHandler.IsHossu(Weapons.Item2.ID) || (Entity.Class >= 20 && Entity.Class <= 25)))
                        loadItemStats(Weapons.Item2);
                }
            }
            ApplySubclassStats();
            #region Chi [Old]
            /*
            if (ChiPowers.Count > 0)
            {
                uint percentage = 100;
                if (DoChampStats)
                    percentage = (uint)ChampionLimits[7];
                foreach (var chiPower in ChiPowers)
                {
                    foreach (var attribute in chiPower.Attributes)
                    {
                        switch (attribute.Type)
                        {
                            case Game.Enums.ChiAttribute.CriticalStrike:
                                Player.CriticalStrike += (ushort)((ushort)(attribute.Value * 10) * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.Counteraction:
                                Player.Counteraction += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.AddAttack:
                                Player.BaseMinAttack += attribute.Value * percentage / 100;
                                Player.BaseMaxAttack += attribute.Value * percentage / 100;
                                break;
                            case Game.Enums.ChiAttribute.AddMagicAttack:
                                Player.BaseMagicAttack += attribute.Value * percentage / 100;
                                break;
                            case Game.Enums.ChiAttribute.AddMagicDefense:
                                Player.BaseMagicDefence += attribute.Value * percentage / 100;
                                break;
                            case Game.Enums.ChiAttribute.Breakthrough:
                                Player.Breaktrough += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.HPAdd:
                                Player.ItemHP += attribute.Value * percentage / 100;
                                break;
                            case Game.Enums.ChiAttribute.Immunity:
                                Player.Immunity += (ushort)((ushort)(attribute.Value * 10) * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.MagicDamageDecrease:
                                Player.MagicDamageDecrease += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.MagicDamageIncrease:
                                Player.MagicDamageIncrease += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.PhysicalDamageDecrease:
                                Player.PhysicalDamageDecrease += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.PhysicalDamageIncrease:
                                Player.PhysicalDamageIncrease += (ushort)(attribute.Value * percentage / 100);
                                break;
                            case Game.Enums.ChiAttribute.SkillCriticalStrike:
                                Player.SkillCStrike += (int)((ushort)(attribute.Value * 10) * percentage / 100);
                                break;
                        }
                    }
                }
                #region Dragon Ranking
                if (ChiData.DragonPoints == 400)
                {
                    Player.ItemHP += 5000;
                    Player.BaseMagicDefence += 300;
                    Player.PhysicalDamageDecrease += 1000;
                    Player.MagicDamageDecrease += 300;
                }
                if (ChiData.DragonPoints == 396)
                {
                    Player.ItemHP += 2500;
                    Player.BaseMagicDefence += 150;
                    Player.PhysicalDamageDecrease += 500;
                    Player.MagicDamageDecrease += 150;
                }
                #endregion
                #region Phoenix Ranking
                if (ChiData.PhoenixPoints == 400)
                {
                    Player.BaseMinAttack += 3000;
                    Player.BaseMaxAttack += 3000;
                    Player.BaseMagicAttack += 3000;
                    Player.PhysicalDamageIncrease += 1000;
                    Player.MagicDamageIncrease += 300;
                }
                if (ChiData.PhoenixPoints == 396)
                {
                    Player.BaseMinAttack += 1500;
                    Player.BaseMaxAttack += 1500;
                    Player.BaseMagicAttack += 1500;
                    Player.PhysicalDamageIncrease += 500;
                    Player.MagicDamageIncrease += 150;
                }
                #endregion
                #region Tiger Ranking
                if (ChiData.TigerPoints == 400)
                {
                    Player.CriticalStrike += 1500;
                    Player.SkillCStrike += 1500;
                    Player.Immunity += 800;
                }
                if (ChiData.TigerPoints == 396)
                {
                    Player.CriticalStrike += 750;
                    Player.SkillCStrike += 750;
                    Player.Immunity += 400;
                }
                #endregion
                #region Turtle Ranking
                if (ChiData.TurtlePoints == 400)
                {
                    Player.Breaktrough += 150;
                    Player.Counteraction += 150;
                    Player.Immunity += 800;
                }
                if (ChiData.TurtlePoints == 396)
                {
                    Player.Breaktrough += 75;
                    Player.Counteraction += 75;
                    Player.Immunity += 400;
                }
                #endregion
            }
            */
            #endregion
            #region Chi
            uint percentage = DoChampStats ? (uint)ChampionLimits[7] : 100;
            ChiBonuses.Apply(this, percentage);
            #endregion
            #region Perfection
            if (!DoChampStats && Entity != null && Entity.Perfection != null)
            {
                Entity.MinAttack += (uint)Entity.Perfection.PhysicalAttack;
                Entity.MaxAttack += (uint)Entity.Perfection.PhysicalAttack;
                Entity.Defence += (ushort)Entity.Perfection.PhysicalDefense;
                Entity.MagicAttack += (uint)Entity.Perfection.MagicAttack;
                Entity.MagicDefence += (ushort)Entity.Perfection.MagicDefense;
            }
            #endregion
            #region Bloodshed~Sea item's
            if (!DoChampStats)
            {
            if (Entity.IncreaseFinalMDamage)
            {
                Entity.MagicDamageDecrease += 3000;
            }
            if (Entity.IncreaseFinalPDamage)
            {
                Entity.PhysicalDamageDecrease += 3000;
            }
            if (Entity.IncreaseFinalMAttack)
            {
                Entity.MagicDamageIncrease += 3000;
            }
            if (Entity.IncreaseFinalPAttack)
            {
                Entity.PhysicalDamageIncrease += 3000;
            }
            if (Entity.IncreaseAttribute)
            {
                Entity.BaseMaxAttack += 3000;
                Entity.BaseMinAttack += 3000;
                Entity.BaseMagicAttack += 3000;
            }
            if (Entity.IncreaseImunity)
            {
                Entity.Immunity += 150 * 100;
            }
            if (Entity.IncreasePStrike)
            {
                Entity.CriticalStrike += 150 * 100;
                Entity.SkillCStrike += 150 * 100;
            }
            if (Entity.IncreaseAntiBreack)
            {
                Entity.Counteraction += 15 * 100;
            }
            if (Entity.IncreaseBreack)
            {
                Entity.Breaktrough += 15 * 100;
            }
            }
            #endregion
            #region GM Blood Status
            if (!DoChampStats && (int)Account.State == 4)// Jheisson
            {
                Entity.ItemHP += 900000000;
                //Player.BaseMinAttack += 9990000;
                //Player.BaseMaxAttack += 9990000;
                //Player.BaseMagicAttack += 9990000;
                //Player.Immunity += 250000;
                //Player.CriticalStrike += 250000;
                //Player.Breaktrough += 250000;
                //Player.Counteraction += 250000;
                //Player.Defence += 40000;
                //Player.ItemMP += 25000;

            }
            #endregion
            if (Entity.ItemBless > 64)
            {
                Entity.ItemBless = 64;
            }
            #region VIP Blood System
            if (!DoChampStats)
            {
            if (Entity.MapID == 1022)
            {
                Entity.ItemHP += 40000;
                Entity.BaseMinAttack += 50000;
                Entity.BaseMaxAttack += 50000;
            }
            if (Entity.VIPLevel == 7)
            {
                Entity.ItemHP += 21000;
                Entity.BaseMinAttack += 5000;
                Entity.BaseMaxAttack += 5000;
                Entity.BaseMagicAttack += 5000;
                Entity.SkillCStrike += 1000;
                Entity.Defence += 3000;
                Entity.Immunity += 1000;//10%                              
                Entity.CriticalStrike += 1000;
                //Player.Breaktrough += 100;
                //Player.Counteraction += 100;                
                //Player.ItemMP += 1500;


            }
            if (Entity.VIPLevel == 6)
            {
                Entity.ItemHP += 15000;
                Entity.BaseMinAttack += 2000;
                Entity.BaseMaxAttack += 2000;
                Entity.BaseMagicAttack += 2000;
                //Player.BaseMagicAttack += 25er Todo Eso00;
                //Player.Immunity += 100;
                Entity.SkillCStrike += 1000;
                Entity.CriticalStrike += 1000;
                //Player.Breaktrough += 100;
                //Player.Counteraction += 100;
                //Player.Defence += 1000;
                //Player.ItemMP += 1500;

            }
            if (Entity.VIPLevel == 5)
            {
                Entity.ItemHP += 10000;
                Entity.CriticalStrike += 1000;
                Entity.SkillCStrike += 1000;
            }
            if (Entity.VIPLevel == 4)
            {
                Entity.ItemHP += 7000;

            }
            if (Entity.VIPLevel == 3)
            {
                Entity.ItemHP += 5000;
            }
            if (Entity.VIPLevel == 2)
            {
                Entity.ItemHP += 3000;
            }
            if (Entity.VIPLevel == 1)
            {
                Entity.ItemHP += 1000;
            }
            }
            #endregion
            #region Nobility Rank Implementation
            var effectiveNobility = DoChampStats ? NobilityRank.Serf : skill ? (NobilityRank)Math.Min((byte)Entity.NobilityRank, (byte)NobilityRank.Earl) : Entity.NobilityRank;
            #region King Benefits
            if (effectiveNobility == NobilityRank.King)
            {
                //Player.Defence += 1000;
                Entity.ItemMP += 900;
                Entity.CriticalStrike += 200; // 4%
                Entity.SkillCStrike += 200;
                Entity.Immunity += 1500;
                Entity.Breaktrough += 150;
                Entity.Counteraction += 1000;
                Entity.ItemHP += 2900;
                Entity.BaseMaxAttack += 1000;
                Entity.BaseMinAttack += 1000;
                Entity.BaseMagicAttack += 900;
                Entity.BaseMagicDefence += 500;
                Entity.PhysicalDamageIncrease += 1000;
                Entity.PhysicalDamageDecrease += 900;
                Entity.MagicDamageIncrease += 900;
                Entity.MagicDamageDecrease += 900;
            }
            #endregion
            #region Prince Benefits
            if (effectiveNobility == NobilityRank.Prince)
            {

                Entity.ItemMP += 900;
                Entity.CriticalStrike += 200; // 4%
                Entity.SkillCStrike += 200;
                Entity.Immunity += 1500;
                Entity.Breaktrough += 150;
                Entity.Counteraction += 1000;
                Entity.ItemHP += 1900;
                Entity.BaseMaxAttack += 1000;
                Entity.BaseMinAttack += 1000;
                Entity.BaseMagicAttack += 900;
                Entity.BaseMagicDefence += 500;
                Entity.PhysicalDamageIncrease += 1000;
                Entity.PhysicalDamageDecrease += 900;
                Entity.MagicDamageIncrease += 900;
                Entity.MagicDamageDecrease += 900;
            }
            #endregion
            #region Duke Benefits
            if (effectiveNobility == NobilityRank.Duke)
            {

                Entity.ItemMP += 800;
                Entity.CriticalStrike += 150; // 3%
                Entity.SkillCStrike += 150;
                Entity.Immunity += 1000;
                Entity.Breaktrough += 80; // 8%
                Entity.Counteraction += 500;
                Entity.ItemHP += 1800;
                Entity.BaseMaxAttack += 500;
                Entity.BaseMinAttack += 500;
                Entity.BaseMagicAttack += 500;
                Entity.BaseMagicDefence += 500;
                Entity.PhysicalDamageIncrease += 500;
                Entity.PhysicalDamageDecrease += 500;
                Entity.MagicDamageIncrease += 500;
                Entity.MagicDamageDecrease += 500;
            }
            #endregion
            #region Earl Benefits
            if (effectiveNobility == NobilityRank.Earl)
            {

                Entity.ItemMP += 700;
                Entity.CriticalStrike += 200; // 2%
                Entity.SkillCStrike += 200;
                Entity.Immunity += 300;
                Entity.Breaktrough += 30; // 7%
                Entity.Counteraction += 30;
                Entity.ItemHP += 1300;
                Entity.BaseMaxAttack += 300;
                Entity.BaseMinAttack += 300;
                Entity.BaseMagicAttack += 300;
                Entity.BaseMagicDefence += 300;
                Entity.PhysicalDamageIncrease += 300;
                Entity.PhysicalDamageDecrease += 300;
                Entity.MagicDamageIncrease += 300;
                Entity.MagicDamageDecrease += 300;
            }
            #endregion
            #endregion
            if (this.Entity.MyJiang != null)
            {
                this.Entity.MyJiang.CreateStatusAtributes(this.Entity);
            }
            if (Entity.Aura_isActive)
                doAuraBonuses(Entity.Aura_actType, Entity.Aura_actPower, 1);
            if (TeamAura)
                doAuraBonuses(TeamAuraStatusFlag, TeamAuraPower, 1);
            #region Runes
            #region Acalanatha
            if (Entity.Owner.Spells.ContainsKey(14300))
            {
                var ss = Entity.Owner.Spells[14300];
                var Acalanatha = SpellTable.SpellInformations[ss.ID][ss.Level];
                if (Acalanatha != null && Entity.Owner.Spells.ContainsKey(14300))
                    if (Entity.Class / 10 == 6)
                        RunesSystem.HandleRedBlueRunesSkills(null, Entity, null, Acalanatha, Entity.X, Entity.Y);
            }
            #endregion
            #region AllRound
            if (Entity.Owner.Spells.ContainsKey(14350))
            {
                var ss = Entity.Owner.Spells[14350];
                var AllRound = SpellTable.SpellInformations[ss.ID][ss.Level];
                if (AllRound != null && Entity.Owner.Spells.ContainsKey(14350))
                    if (Entity.Class / 10 == 2)
                        RunesSystem.HandleRedBlueRunesSkills(null, Entity, null, AllRound, Entity.X, Entity.Y);
            }
            #endregion
            #region YellowRunes
            RunesSystem.HandleYellowRunes(Entity);
            #endregion
            #endregion
            Entity.Hitpoints = bStats;
            CalculateStatBonus();
            CalculateHPBonus();
            ReviewMentor();
            ReloadBlock();
            GemAlgorithm();
            #region InnerPower Improved Update 6305
            if (this.InnerPower != null)
            {
                this.AddInnerPowerAtributes(InnerPower);
            }
            #endregion
            if (Equipment.Free(4) && Equipment.Free(5))
            {
                Entity.AttackRange += 2;
            }
            FinishSkillStats(skill,bStats);
            if (Revora.Game.Features.CityGuardSystem.RestoreCombatProfile(Entity))
                Entity.Hitpoints = Math.Min(bStats, Entity.MaxHitpoints);
        }
        public void AddInnerPowerAtributes(Game.InnerPower Inner, uint percent = 100)
        {
            if (DoChampStats) return;

            Entity.CriticalStrike += (int)(Inner.CriticalStrike * percent / 100);
            Entity.SkillCStrike += (int)(Inner.SkillCriticalStrike * percent / 100);
            Entity.Immunity += (int)(Inner.Immunity * percent / 100);
            Entity.Counteraction += (ushort)(Inner.Counteraction * percent / 100);
            Entity.Breaktrough += (ushort)(Inner.Breakthrough * percent / 100);
            Entity.MaxHitpoints += Inner.MaxLife * percent / 100;
            Entity.MaxAttack += Inner.AddAttack * percent / 100;
            Entity.MinAttack += Inner.AddAttack * percent / 100;
            Entity.MagicAttack += Inner.AddMagicAttack * percent / 100;
            Entity.MagicDefence += (ushort)(Inner.AddMagicDefense * percent / 100);

            Entity.PhysicalDamageIncrease += (ushort)(Inner.FinalAttack * percent / 100);
            Entity.PhysicalDamageDecrease += (ushort)(Inner.FinalDefense * percent / 100);
            Entity.MagicDamageIncrease += (ushort)(Inner.FinalMagicAttack * percent / 100);
            Entity.MagicDamageDecrease += (ushort)(Inner.FinalMagicDefense * percent / 100);

            Entity.Defence += (ushort)(Inner.Defence * percent / 100);
        }
        private void loadItemStats(ConquerItem item)
        {
            bool skill = Game.Features.Tournaments.LocalTeamPkCombat.SkillStats(this);
            int position = item.Position;
            item.IsWorn = true;
            #region Perfection Stats/jheisson
            if (!DoChampStats)
            {
            if (item.PerfectionLevel > 3 && item.PerfectionLevel < 7)
            {
                Entity.BaseMinAttack += 100 / 12;
                Entity.BaseMaxAttack += 100 / 12;
                Entity.BaseMagicAttack += 300 / 12;
            }
            if (item.PerfectionLevel > 7 && item.PerfectionLevel < 10)
            {
                Entity.BaseMinAttack += 100 / 12;
                Entity.BaseMaxAttack += 100 / 12;
                Entity.BaseDefence += 100 / 12;
                Entity.BaseMagicAttack += 300 / 12;
                Entity.MagicDefence += 100 / 12;
            }
            if (item.PerfectionLevel > 10 && item.PerfectionLevel < 14)
            {
                Entity.BaseMinAttack += 300 / 12;
                Entity.BaseMaxAttack += 300 / 12;
                Entity.BaseDefence += 300 / 12;
                Entity.BaseMagicAttack += 600 / 12;
                Entity.MagicDefence += 150 / 12;
            }
            if (item.PerfectionLevel > 14 && item.PerfectionLevel < 17)
            {
                Entity.BaseMinAttack += 500 / 12;
                Entity.BaseMaxAttack += 500 / 12;
                Entity.BaseDefence += 500 / 12;
                Entity.BaseMagicAttack += 1000 / 12;
                Entity.MagicDefence += 250 / 12;
            }
            if (item.PerfectionLevel > 17 && item.PerfectionLevel < 25)
            {
                Entity.BaseMinAttack += 800 / 12;
                Entity.BaseMaxAttack += 800 / 12;
                Entity.BaseDefence += 1200 / 12;
                Entity.BaseMagicAttack += 1500 / 12;
                Entity.MagicDefence += 500 / 12;
            }
            if (item.PerfectionLevel > 25 && item.PerfectionLevel < 28)
            {
                Entity.BaseMinAttack += 1200 / 12;
                Entity.BaseMaxAttack += 1200 / 12;
                Entity.BaseDefence += 1200 / 12;
                Entity.BaseMagicAttack += 2000 / 12;
                Entity.MagicDefence += 500 / 12;
            }
            if (item.PerfectionLevel > 28 && item.PerfectionLevel < 53)
            {
                Entity.BaseMinAttack += 1600 / 12;
                Entity.BaseMaxAttack += 1600 / 12;
                Entity.BaseDefence += 1600 / 12;
                Entity.BaseMagicAttack += 2500 / 12;
                Entity.MagicDefence += 625 / 12;
            }
            if (item.PerfectionLevel >= 53 && item.PerfectionLevel < 55)
            {
                Entity.BaseMinAttack += 3000 / 12;
                Entity.BaseMaxAttack += 3000 / 12;
                Entity.BaseDefence += 3000 / 12;
                Entity.BaseMagicAttack += 4000 / 12;
                Entity.MagicDefence += 1000 / 12;
            }
            }
            #endregion
            byte plus = DoChampStats ? (byte)Math.Min((int)item.Plus, ChampionLimits[0]) : skill ? (byte)Math.Min((int)item.Plus, 4) : item.Plus;
            uint statType = skill || DoChampStats ? Game.Features.Tournaments.LocalTeamPkCombat.SkillItemType(item) : item.ID;
            Database.ConquerItemInformation dbi = new Database.ConquerItemInformation(statType, plus);
            if (dbi != null)
            {
                #region Give Stats.
                if (DoChampStats && ChampionLimits[5] == 1 || !DoChampStats)
                {
                    #region soul stats
                    
                        if (item.Purification.PurificationLevel != 7)
                        {
                            Database.ConquerItemInformation soulDB = new Database.ConquerItemInformation(item.Purification.PurificationItemID, 0);
                            if (position == ConquerItem.LeftWeapon)
                            {
                                Entity.BaseMinAttack += (uint)(soulDB.BaseInformation.MinAttack);
                                Entity.BaseMaxAttack += (uint)(soulDB.BaseInformation.MaxAttack);
                            }
                            else
                            {
                                Entity.BaseMinAttack += soulDB.BaseInformation.MinAttack;
                                Entity.BaseMaxAttack += soulDB.BaseInformation.MaxAttack;
                            }
                            Entity.ItemHP += soulDB.BaseInformation.ItemHP;
                            Entity.BaseDefence += soulDB.BaseInformation.PhysicalDefence;
                            Entity.MagicDefence += soulDB.BaseInformation.MagicDefence;
                            Entity.Dodge += (byte)soulDB.BaseInformation.Dodge;
                            Entity.Accuracy += soulDB.BaseInformation.Agility;
                            Entity.BaseMagicAttack += soulDB.BaseInformation.MagicAttack;
                            //Player.MetalResistance += soulDB.BaseInformation.MetalResist;
                            Entity.WoodResistance += soulDB.BaseInformation.WoodResist;
                            Entity.FireResistance += soulDB.BaseInformation.FireResist;
                            Entity.WaterResistance += soulDB.BaseInformation.WaterResist;
                            Entity.EarthResistance += soulDB.BaseInformation.EarthResist;
                            Entity.Breaktrough += soulDB.BaseInformation.BreakThrough;
                            Entity.CriticalStrike += soulDB.BaseInformation.CriticalStrike;
                            Entity.Immunity += soulDB.BaseInformation.Immunity;
                            Entity.Penetration += soulDB.BaseInformation.Penetration;
                            Entity.Counteraction += soulDB.BaseInformation.CounterAction;
                            Entity.Block += soulDB.BaseInformation.Block;
                            Entity.Weight += soulDB.BaseInformation.Weight;
                        }
                        else if (item.Purification.PurificationItemID != 0)
                        {
                            Entity.WeaponColor = (uint)Entity.BattlePower;
                            Database.ConquerItemInformation soulDB = new Database.ConquerItemInformation(item.Purification.PurificationItemID, 0);
                            if (position == ConquerItem.LeftWeapon)
                            {
                                Entity.BaseMinAttack += (uint)(soulDB.BaseInformation.MinAttack / 2);
                                Entity.BaseMaxAttack += (uint)(soulDB.BaseInformation.MaxAttack / 2);
                            }
                            else
                            {
                                Entity.BaseMinAttack += soulDB.BaseInformation.MinAttack;
                                Entity.BaseMaxAttack += soulDB.BaseInformation.MaxAttack;
                            }
                            //  Player.BaseMinAttack += soulDB.BaseInformation.MinAttack;
                            //  Player.BaseMaxAttack += soulDB.BaseInformation.MaxAttack;
                            Entity.ItemHP += soulDB.BaseInformation.ItemHP;
                            Entity.BaseDefence += soulDB.BaseInformation.PhysicalDefence;
                            Entity.MagicDefence += soulDB.BaseInformation.MagicDefence;
                            Entity.Dodge += (byte)soulDB.BaseInformation.Dodge;
                            Entity.Accuracy += soulDB.BaseInformation.Agility;
                            Entity.BaseMagicAttack += soulDB.BaseInformation.MagicAttack;
                            //Player.MetalResistance += soulDB.BaseInformation.MetalResist;
                            Entity.WoodResistance += soulDB.BaseInformation.WoodResist;
                            Entity.FireResistance += soulDB.BaseInformation.FireResist;
                            Entity.WaterResistance += soulDB.BaseInformation.WaterResist;
                            Entity.EarthResistance += soulDB.BaseInformation.EarthResist;
                            Entity.Breaktrough += soulDB.BaseInformation.BreakThrough;
                            Entity.CriticalStrike += soulDB.BaseInformation.CriticalStrike;
                            Entity.Immunity += soulDB.BaseInformation.Immunity;
                            Entity.Penetration += soulDB.BaseInformation.Penetration;
                            Entity.Counteraction += soulDB.BaseInformation.CounterAction;
                            Entity.Block += soulDB.BaseInformation.Block;

                            Entity.Weight += soulDB.BaseInformation.Weight;
                        }
                    
                    #endregion
                }
                if (DoChampStats && ChampionLimits[4] == 1 || !DoChampStats)
                {
                    Refinery.RefineryItem refine = null;
                    if (item.RefineItem != 0) refine = item.RefineStats;

                    if (refine != null)
                    {
                        switch (refine.Type)
                        {
                            case Refinery.RefineryItem.RefineryType.Block:
                                Entity.Block += (UInt16)(refine.Percent * 100);
                                break;
                            case Refinery.RefineryItem.RefineryType.BreakThrough:
                                Entity.Breaktrough += (UInt16)((refine.Percent * 10) + 100);
                                break;
                            case Refinery.RefineryItem.RefineryType.Counteraction:
                                Entity.Counteraction += (UInt16)(refine.Percent * 10);
                                break;
                            case Refinery.RefineryItem.RefineryType.Critical:
                                Entity.CriticalStrike += (UInt16)((refine.Percent * 100) + 1000);
                                break;
                            case Refinery.RefineryItem.RefineryType.Detoxication:
                                Entity.Detoxication += (UInt16)(refine.Percent);
                                break;
                            case Refinery.RefineryItem.RefineryType.Immunity:
                                Entity.Immunity += (UInt16)(refine.Percent * 100);
                                break;
                            case Refinery.RefineryItem.RefineryType.Intensification:
                                Entity.Intensification += (UInt16)(refine.Percent);
                                break;
                            case Refinery.RefineryItem.RefineryType.Penetration:
                                Entity.Penetration += (UInt16)(refine.Percent * 100);
                                break;
                            case Refinery.RefineryItem.RefineryType.SCritical:
                                Entity.SkillCStrike += (UInt16)(refine.Percent * 100);
                                break;
                        }
                    }
                }
                if (position == ConquerItem.Relics && !DoChampStats)
                {
                    var relicMaxPower = Relics.RelicsItemsInfo[(Relics.RelicsID)item.ID].Sum(p => p.maxvalue);
                    HandleRelic((int)item.RelicAttrib1);
                    HandleRelic((int)item.RelicAttrib2);
                    HandleRelic((int)item.RelicAttrib3);
                    HandleRelic((int)item.RelicAttrib4);
                    HandleRelic((int)item.RelicAttrib5);
                    Entity.CharBuffer.RelicPower = (uint)(Entity.CharBuffer.RelicPower / (float)relicMaxPower * 100f);
                }
                if (position == ConquerItem.Tower)
                {
                    Entity.PhysicalDamageDecrease += dbi.BaseInformation.PhysicalDefence;
                    Entity.MagicDamageDecrease += dbi.BaseInformation.MagicDefence;
                }
                else
                {
                    if (item.Position != ConquerItem.Steed)
                    {
                        if (skill)
                            Entity.ItemBless += (ushort)Math.Min((int)item.Bless, 3);
                        else if (DoChampStats)
                            Entity.ItemBless += (ushort)Math.Min(item.Bless, ChampionLimits[1]);
                        else
                            Entity.ItemBless += item.Bless;
                    }
                    if (!Revora.Game.Features.GuildCupStats.Apply(Entity,item,dbi.BaseInformation))
                    {
                        Entity.Defence += dbi.BaseInformation.PhysicalDefence;
                        Entity.MagicDefencePercent += dbi.BaseInformation.MagicDefence;
                        Entity.Dodge += (byte)dbi.BaseInformation.Dodge;
                        if (position == ConquerItem.Wing)
                        {
                            Entity.PhysicalDamageDecrease += dbi.BaseInformation.PhysicalDefence;
                            Entity.MagicDamageDecrease += dbi.BaseInformation.MagicDefence;
                        }
                        else
                        {
                            Entity.Defence += dbi.BaseInformation.PhysicalDefence;
                            Entity.MagicDefencePercent += dbi.BaseInformation.MagicDefence;
                            Entity.Dodge += (byte)dbi.BaseInformation.Dodge;

                            if (position != ConquerItem.Fan)
                                Entity.BaseMagicAttack += dbi.BaseInformation.MagicAttack;
                        }
                        Entity.ItemHP += dbi.BaseInformation.ItemHP;
                        Entity.ItemMP += dbi.BaseInformation.ItemMP;

                        if (position == ConquerItem.RightWeapon)
                        {
                            Entity.BaseMagicAttack += (uint)(dbi.BaseInformation.MagicAttack);
                            Entity.AttackRange += dbi.BaseInformation.AttackRange;
                            if (ItemHandler.IsTwoHand(dbi.BaseInformation.ID))
                                Entity.AttackRange += 4;
                            else
                                Entity.AttackRange += 3;
                        }
                        if (position == ConquerItem.LeftWeapon)
                        {
                            Entity.BaseMinAttack += (uint)(dbi.BaseInformation.MinAttack * 0.5F);
                            Entity.BaseMaxAttack += (uint)(dbi.BaseInformation.MaxAttack * 0.5F);
                            Entity.BaseMagicAttack += (uint)(dbi.BaseInformation.MagicAttack * 0.5F);
                        }
                        else if (position == ConquerItem.Fan)
                        {
                            Entity.PhysicalDamageIncrease += dbi.BaseInformation.MinAttack;
                            Entity.MagicDamageIncrease += dbi.BaseInformation.MagicAttack;
                        }
                        else
                        {
                            Entity.BaseMinAttack += dbi.BaseInformation.MinAttack;
                            Entity.BaseMaxAttack += dbi.BaseInformation.MaxAttack;
                        }

                    }

                    if (item.Plus != 0)
                    {
                        if (position == ConquerItem.Wing)
                        {
                            Entity.PhysicalDamageDecrease += dbi.PlusInformation.PhysicalDefence;
                            Entity.MagicDamageDecrease += (ushort)dbi.PlusInformation.MagicDefence;
                        }
                        if (position == ConquerItem.Tower)
                        {
                            Entity.PhysicalDamageDecrease += dbi.PlusInformation.PhysicalDefence;
                            Entity.MagicDamageDecrease += (ushort)dbi.PlusInformation.MagicDefence;
                        }
                        else if (position == ConquerItem.Fan)
                        {
                            Entity.PhysicalDamageIncrease += (ushort)dbi.PlusInformation.MinAttack;
                            Entity.MagicDamageIncrease += (ushort)dbi.PlusInformation.MagicAttack;
                        }
                        else
                        {
                            if (position == ConquerItem.Steed)
                            {
                                CalculateVigor(item, dbi);
                            }
                            Entity.BaseMinAttack += dbi.PlusInformation.MinAttack;
                            Entity.BaseMaxAttack += dbi.PlusInformation.MaxAttack;
                            Entity.MagicAttack += dbi.PlusInformation.MagicAttack;
                            Entity.Defence += dbi.PlusInformation.PhysicalDefence;
                            Entity.MagicDefence += dbi.PlusInformation.MagicDefence;
                            Entity.ItemHP += dbi.PlusInformation.ItemHP;
                            if (item.Position == ConquerItem.Boots)
                                Entity.Dodge += (byte)dbi.PlusInformation.Dodge;
                        }
                    }
                    #region Frack
                    if (position == ConquerItem.Garment)
                    {
                        if (item.ID == 187425)
                        {
                            Entity.Defence += 400;
                            Entity.BaseMagicDefence += 2;
                        }
                        else if (item.ID == 187415)
                        {
                            Entity.Defence += 600;
                            Entity.BaseMagicDefence += 3;
                        }
                        else if (item.ID == 187405)
                        {
                            Entity.Defence += 800;
                            Entity.BaseMagicDefence += 4;
                        }
                        
                        //192695-193055
                              else if (item.ID == 189695 || item.ID == 193055 || item.ID == 192695
                                    || item.ID == 195406 || item.ID == 195015 || item.ID == 189765 
                                    || item.ID == 188755 || item.ID == 195325 || item.ID == 195335
                                    || item.ID == 195295 || item.ID == 195305 || item.ID == 195315                                    
                                    || item.ID == 195345 || item.ID == 195305 || item.ID == 195046
                                    || item.ID == 194885 || item.ID == 194895 || item.ID == 194905
                                    || item.ID == 194915 || item.ID == 194925 || item.ID == 194935
                                    || item.ID == 194945 || item.ID == 194875 || item.ID == 193525
                                    || item.ID == 193625 || item.ID == 188915 || item.ID == 193275
                                    || item.ID == 193445 || item.ID == 193555 || item.ID == 193565
                                    || item.ID == 194385 || item.ID == 194395 || item.ID == 194405
                            )
                        {
                            Entity.CriticalStrike += 400;
                            Entity.Immunity += 400;
                            Entity.SkillCStrike += 1000;
                        }
                        ///183475
                        //else if (item.ID == 183475 || item.ID == 181355 || item.ID == 184325)
                        //{
                        //    Player.Hitpoints += 800;
                        //    Player.Mana += 800;
                        //    Player.CriticalStrike += 200;
                        //    Player.Immunity += 200;
                        //    Player.SkillCStrike += 1000;
                        //}
                    }
                    #endregion
                    #region MountArmor
                    if (position == ConquerItem.SteedArmor)
                    {
                        //Player.CriticalStrike += dbi.BaseInformation.CriticalStrike;
                        //Player.SkillCStrike += dbi.BaseInformation.CriticalStrike;
                        //Player.Immunity += dbi.BaseInformation.Immunity;
                        //Player.MaxHitpoints += dbi.BaseInformation.ItemHP;
                        //Player.MaxMana += dbi.BaseInformation.ItemMP;
                        #region Old
                   if (item.ID == 200221)
                    {
                        Entity.CriticalStrike += 300;
                        Entity.SkillCStrike += 300;
                        Entity.Immunity += 300;
                    }
                    else if (item.ID == 200220)
                    {
                        Entity.CriticalStrike += 200;
                        Entity.SkillCStrike += 200;
                        Entity.Immunity += 200;
                    }
                    else if (item.ID == 200486)
                    {
                        Entity.CriticalStrike += 100;
                        Entity.Immunity += 100;
                    }
                    else if (item.ID == 200485)
                    {
                        Entity.CriticalStrike += 200;
                        Entity.Immunity += 200;
                    }
                    else if (item.ID == 200479)
                    {
                        Entity.CriticalStrike += 100;
                        Entity.SkillCStrike += 100;
                    }
                    else if (item.ID == 200478)
                    {
                        Entity.CriticalStrike += 100;
                        Entity.SkillCStrike += 100;
                        Entity.Immunity += 100;
                    }
                    else if (item.ID == 200477)
                    {
                        Entity.CriticalStrike += 200;
                        Entity.SkillCStrike += 200;
                    }
                   else if (item.ID == 200475 || item.ID == 200621 || item.ID == 200622 || item.ID == 200623 || item.ID == 200624
                       || item.ID == 200626)
                    {
                        Entity.CriticalStrike += 200;
                        Entity.SkillCStrike += 200;
                        Entity.Immunity += 200;
                    }
                        #endregion
                    }
                    #endregion
                    #region Relics [Jheisson]
                    //if (position == ConquerItem.Relics)
                    //{
                    //    if (item.ID == 4100005)
                    //    {
                    //        Entity.Mana += 5000;                           
                    //        Entity.Defence += 1000;
                    //        Entity.MaxAttack += 1000;
                    //        Entity.MinAttack += 1000;
                    //    }
                    //    if (item.ID == 4100004)
                    //    {
                    //        Entity.Mana += 5000;
                    //        Entity.Defence += 1000;
                    //        Entity.MaxAttack += 1000;
                    //        Entity.MinAttack += 1000;
                    //    }
                    //}                    
                    #endregion
                    byte socketone = (byte)item.SocketOne;
                    byte sockettwo = skill ? (byte)0 : (byte)item.SocketTwo;
                    ushort madd = 0, dadd = 0, aatk = 0, matk = 0;
                    if (DoChampStats && ChampionLimits[2] >= 1 || !DoChampStats)
                    {
                        switch (socketone)
                        {
                            case 1: Entity.Gems[0] += 5; break;
                            case 2: Entity.Gems[0] += 10; break;
                            case 3: Entity.Gems[0] += 15; break;

                            case 11: Entity.Gems[1] += 5; break;
                            case 12: Entity.Gems[1] += 10; break;
                            case 13: Entity.Gems[1] += 15; break;

                            case 31: Entity.Gems[3] += 10; break;
                            case 32: Entity.Gems[3] += 15; break;
                            case 33: Entity.Gems[3] += 25; break;

                            case 51: Entity.Gems[5] += 30; break;
                            case 52: Entity.Gems[5] += 50; break;
                            case 53: Entity.Gems[5] += 100; break;

                            case 61: Entity.Gems[6] += 15; break;
                            case 62: Entity.Gems[6] += 30; break;
                            case 63: Entity.Gems[6] += 50; break;

                            case 71: Entity.Gems[7] += 2; break;
                            case 72: Entity.Gems[7] += 4; break;
                            case 73: Entity.Gems[7] += 6; break;

                            case 101: aatk = matk += 100; break;
                            case 102: aatk = matk += 300; break;
                            case 103: aatk = matk += 500; break;

                            case 121: madd = dadd += 100; break;
                            case 122: madd = dadd += 300; break;
                            case 123: madd = dadd += 500; break;
                        }
                    }
                    if (DoChampStats && ChampionLimits[2] >= 2 || !DoChampStats)
                    {
                        switch (sockettwo)
                        {
                            case 1: Entity.Gems[0] += 5; break;
                            case 2: Entity.Gems[0] += 10; break;
                            case 3: Entity.Gems[0] += 15; break;

                            case 11: Entity.Gems[1] += 5; break;
                            case 12: Entity.Gems[1] += 10; break;
                            case 13: Entity.Gems[1] += 15; break;

                            case 31: Entity.Gems[3] += 10; break;
                            case 32: Entity.Gems[3] += 15; break;
                            case 33: Entity.Gems[3] += 25; break;

                            case 51: Entity.Gems[5] += 30; break;
                            case 52: Entity.Gems[5] += 50; break;
                            case 53: Entity.Gems[5] += 100; break;

                            case 61: Entity.Gems[6] += 15; break;
                            case 62: Entity.Gems[6] += 30; break;
                            case 63: Entity.Gems[6] += 50; break;

                            case 71: Entity.Gems[7] += 2; break;
                            case 72: Entity.Gems[7] += 4; break;
                            case 73: Entity.Gems[7] += 6; break;

                            case 101: aatk = matk += 100; break;
                            case 102: aatk = matk += 300; break;
                            case 103: aatk = matk += 500; break;

                            case 121: madd = dadd += 100; break;
                            case 122: madd = dadd += 300; break;
                            case 123: madd = dadd += 500; break;
                        }
                    }
                    Entity.PhysicalDamageDecrease += dadd;
                    Entity.MagicDamageDecrease += madd;
                    Entity.PhysicalDamageIncrease += aatk;
                    Entity.MagicDamageIncrease += matk;
                    if (item.Position != ConquerItem.Steed)
                        if (!DoChampStats)
                            Entity.ItemHP += item.Enchant;
                        else
                            Entity.ItemHP += (uint)Math.Min(item.Enchant, ChampionLimits[6]);
                }
                #endregion
                
            }
        }
        void HandleRelic(int Data)
        {
            if (DoChampStats) return;
            if (Data == 0)
                return;
            var stringdata = Data.ToString("D6");
            var Value = int.Parse(stringdata.Substring(0, 3));
            Entity.CharBuffer.RelicPower += (uint)Value;
            var Type = (Enums.ChiAttribute)int.Parse(stringdata.Substring(4, 2));
            switch (Type)
            {
                case Enums.ChiAttribute.CriticalStrike:
                    Entity.CriticalStrike += (ushort)Value;
                    break;
                case Enums.ChiAttribute.Counteraction:
                    Entity.Counteraction += (ushort)(Value / 10); ;
                    break;
                case Enums.ChiAttribute.AddAttack:
                    Entity.MinAttack += (ushort)Value;
                    Entity.MaxAttack += (ushort)Value;
                    break;
                case Enums.ChiAttribute.AddMagicAttack:
                    Entity.MagicAttack += (ushort)Value;
                    break;
                case Enums.ChiAttribute.AddMagicDefense:
                    Entity.MagicDefence += (ushort)Value;
                    break;
                case Enums.ChiAttribute.Breakthrough:
                    Entity.Breaktrough += (ushort)(Value / 10);
                    break;
                case Enums.ChiAttribute.HPAdd:
                    Entity.ItemHP += (ushort)Value;
                    break;
                case Enums.ChiAttribute.Immunity:
                    Entity.Immunity += (Value);
                    break;
                case Enums.ChiAttribute.MagicDamageDecrease:
                    Entity.MagicDamageDecrease += (ushort)Value;
                    break;
                case Enums.ChiAttribute.MagicDamageIncrease:
                    Entity.MagicDamageIncrease += (ushort)Value;
                    break;
                case Enums.ChiAttribute.PhysicalDamageDecrease:
                    Entity.PhysicalDamageDecrease += (ushort)Value;
                    break;
                case Enums.ChiAttribute.PhysicalDamageIncrease:
                    Entity.PhysicalDamageIncrease += (ushort)Value;
                    break;
                case Enums.ChiAttribute.SkillCriticalStrike:
                    Entity.SkillCStrike += (Value);
                    break;
            }
        }
        public void GemAlgorithm()
        {
            Entity.MinAttack = Entity.BaseMinAttack + Entity.Strength;
            Entity.MaxAttack = Entity.BaseMaxAttack + Entity.Strength;
            Entity.MagicAttack = Entity.BaseMagicAttack;
        }
        public void ReloadBlock()
        {
            Entity.Block = 0;
            foreach (ConquerItem i in Equipment.Objects)
            {
                if (i == null) continue;
                if (i.Durability == 0) continue;
                if (i.Position == ConquerItem.LeftWeaponAccessory || i.Position == ConquerItem.RightWeaponAccessory) continue;
                Refinery.RefineryItem refine = null;
                Database.ConquerItemInformation soulDB = new Database.ConquerItemInformation(i.Purification.PurificationItemID, 0);
                if (i.RefineItem != 0 && (!DoChampStats || ChampionLimits[4] == 1))
                    refine = i.RefineStats;
                if (soulDB != null && (!DoChampStats || ChampionLimits[5] == 1))
                {
                    Entity.Block += soulDB.BaseInformation.Block;
                }
                if (refine != null)
                {
                    switch (refine.Type)
                    {
                        case Refinery.RefineryItem.RefineryType.Block:
                            {
                                Entity.Block += (UInt16)(refine.Percent * 100);
                                break;
                            }
                    }
                }
            }
            if (!Equipment.Free(5))
            {
                ConquerItem left = Equipment.TryGetItem(ConquerItem.LeftWeapon);
                if (left != null && left.ID != 0)
                {
                    if (Entity.BlockShieldCheck)
                    {
                        if (left.GetItemType() == ConquerItem.ItemTypes.ShieldID)
                        {
                            if (Spells.ContainsKey(10470) && Database.SpellTable.SpellInformations.ContainsKey(10470))
                            {
                                var spell = Database.SpellTable.SpellInformations[10470][Spells[10470].Level];
                                Entity.Block += (ushort)(spell.Power * 100);
                            }
                        }
                    }
                }
            }
        }
        #endregion
        #endregion

        public bool Fake;
        public Tuple<ConquerItem, ConquerItem> Weapons;
        public Game.Enums.PKMode PrevPK;
        public int TeamCheerFor;
        public int ArenaState = 0;
        public QuizShow.QuizClient Quiz;
        public uint InteractionEffect;
        public Game.UsableRacePotion[] Potions;
        public bool TeamAura;
        public GameState TeamAuraOwner;
        public ulong TeamAuraStatusFlag;
        public uint TeamAuraPower;
        public VariableVault Variables;
        public uint NpcCpsInput;
        public SlotMachine SlotMachine;
        public bool JustOpenedDetain;
        public Network.GamePackets.Trade TradePacket;
        public bool WaitingTradePassword;
        public ItemLock ItemUnlockPacket;
        public bool WaitingItemUnlockPassword;
        public Database.ConquerItemBaseInformation NewLookArmorInfo;
        public Database.ConquerItemBaseInformation NewLookHeadgearInfo;
        public Database.ConquerItemBaseInformation NewLookWeapon;
        public Database.ConquerItemBaseInformation NewLookWeapon2;
        public Time32 LastAttack, LastMove;
        public bool LoggedIn;
        // Generation captured when this object was published. Later login work
        // compares it with the current epoch; a successor's publication does not
        // rewrite this value.
        internal long SessionGeneration;
        public bool SignedUpForEPK;
        public bool FakeLoaded;
        public int LastTaskRewardID;
        public Time32 FakeQuit;
        public Time32 CTFUpdateStamp;
        public Action<GameState> QCorrect;
        public Action<GameState> QWrong;
        public bool VerifiedChallenge;
        public int VerifyChallengeCount;
        public bool AllowedTreasurePoints;
        public int AllowedTreasurePointsIndex;
        public DynamicVariable this[string variable]
        {
            get { return Variables[variable]; }
            set { Variables[variable] = value; }
        }
        public bool IsWatching()
        {
            return WatchingGroup != null || TeamWatchingGroup != null;
        }
        public bool InQualifier()
        {
            return QualifierGroup != null || TeamQualifierGroup != null;
        }
        public bool InTeamQualifier()
        {
            return TeamQualifierGroup != null;
        }
        public Time32 ImportTime()
        {
            if (QualifierGroup != null)
                return QualifierGroup.CreateTime;
            else
                return TeamQualifierGroup.ImportTime;
        }
        public void UpdateQualifier(GameState client, GameState target, uint damage)
        {
            if (ChampionGroup != null)
            {
                ChampionGroup.UpdateDamage(client, damage);
            }
            else if (ElitePKMatch != null)
            {
                ElitePKStats.Points += damage;
                ElitePKMatch.Update();
            }
            else if (QualifierGroup != null)
            {
                QualifierGroup.UpdateDamage(client, damage);
            }
            else if (TeamQualifierGroup != null)
            {
                if (client == null)
                    TeamQualifierGroup.UpdateDamage(target, damage, true);
                else TeamQualifierGroup.UpdateDamage(client, damage);
            }
            #region TeamEliteMatch
            /*
            if (target == null) return;
            if (target.Entity == null) return;
            if (target.Team == null) return;
            if (client.Team == null) return;
            if (target.Player.Hitpoints <= damage)
            {
                if (client.Team.EliteFighterStats != null && target.Team.EliteFighterStats != null && client.Player.MapID == target.Team.EliteMatch.Map.ID)
                {
                    if (client.Team.EliteMatch != null)
                    {
                        if (!target.Team.Alive)
                        {
                            client.Team.EliteFighterStats.Points += (uint)damage;
                            target.Team.EliteMatch.End(target.Team);
                        }
                        else
                        {
                            client.Team.SendMesageTeam(client.Team.EliteMatch.CreateUpdate().ToArray(), 0);
                            target.Team.SendMesageTeam(client.Team.EliteMatch.CreateUpdate().ToArray(), 0);
                        }
                    }
                }
                else
                {
                    if (client.Team.EliteFighterStats != null && target.Team.EliteFighterStats != null && client.Player.MapID == target.Team.EliteMatch.Map.ID)
                    {
                        if (client.Team.EliteMatch != null)
                        {
                            client.Team.EliteFighterStats.Points += (uint)damage;
                            client.Team.SendMesageTeam(client.Team.EliteMatch.CreateUpdate().ToArray(), 0);
                            client.Team.SendMesageTeam(client.Team.EliteMatch.CreateUpdate().ToArray(), 0);
                        }
                    }
                }
            }
             */
            #endregion
        }
        public uint CurrentHonor
        {
            get { return ArenaStatistic.CurrentHonor; }
            set { ArenaStatistic.CurrentHonor = TeamArenaStatistic.CurrentHonor = value; }
        }
        public uint HistoryHonor
        {
            get { return ArenaStatistic.HistoryHonor; }
            set { ArenaStatistic.HistoryHonor = TeamArenaStatistic.HistoryHonor = value; }
        }
        internal void EndQualifier()
        {
            if (ChampionGroup != null)
                ChampionGroup.End(this);

            var solo=QualifierGroup;
            if (solo != null) {
                if(solo.CompletionOwner!=null)
                    Game.Features.ArenaResultCoordinator.EndForPlayer(solo,this,Game.Features.ArenaResultIntents.Cause.Defeat);
                else solo.End(this);
            }

            var team=TeamQualifierGroup;
            if (team != null)team.CheckEnd(this);
        }
        internal void Send(string msg, uint type = Message.System)
        {
            Send(new Message(msg, type));
        }
        public void MessageBox(string text, Action<GameState> msg_ok = null, Action<GameState> msg_cancel = null, uint time = 0)
        {
            if (!AutoInvite.Enabled && msg_ok != null)
                return;
            if (Entity.MapID == 6000 || Entity.MapID == 6001 || Entity.MapID == 6002 ||
                Entity.MapID == 6003 || Entity.MapID == 6004 ||
                Entity.Dead || InQualifier() || Entity.PokerTable > 0 || PlayRouletteUID > 0) return;
            MessageOK = msg_ok;
            MessageCancel = msg_cancel;
            NpcReply msg = new NpcReply(NpcReply.MessageBox, text);
            Send(msg);
            if (time != 0)
                Time(time);
        }
        public bool aisummoned = false;
        public void Time(uint time)
        {
            Send(new Data(true) { UID = Entity.UID, dwParam = time, ID = Data.CountDown });
        }
        public bool Online
        {
            get
            {
                return Socket.Connector != null;
            }
        }
        internal void LoadData(bool loadFake = false)
        {
            if (Quests != null)
                Quests.Load();
            else
            {
                Quests = new Quests(this);
                Quests.Load();
            }
            Revora.Game.Features.QuestListSystem.OnLogin(this);
            Database.ConquerItemTable.LoadItems(this);
            if (!loadFake)
            {
                Database.ClaimItemTable.LoadClaimableItems(this);
                Database.DetainedItemTable.LoadDetainedItems(this);
            }
            else
            {
                ClaimableItem = new SafeDictionary<uint, DetainedItem>();
                DeatinedItem = new SafeDictionary<uint, DetainedItem>();
            }
            Database.SubClassTable.Load(this.Entity);
            if (!loadFake)
            {
                using (var conn = Database.DataHolder.MySqlConnection)
                {
                    conn.Open();
                    Database.SkillTable.LoadProficiencies(this, conn);
                    Database.SkillTable.LoadSpells(this, conn);
                }
                Database.KnownPersons.LoadPartner(this);
                Database.KnownPersons.LoadEnemy(this);
                Database.KnownPersons.LoaderFriends(this);
                Database.KnownPersons.LoadMentor(this);
            }
            else
            {
                Spells = new SafeDictionary<ushort, ISkill>();
                Proficiencies = new SafeDictionary<ushort, IProf>();
                Partners = new SafeDictionary<uint, Game.ConquerStructures.Society.TradePartner>();
                Enemy = new SafeDictionary<uint, Game.ConquerStructures.Society.Enemy>();
                Friends = new SafeDictionary<uint, Game.ConquerStructures.Society.Friend>();
                Apprentices = new SafeDictionary<uint, Game.ConquerStructures.Society.Apprentice>();
            }
            WayofHeroes.Load(this);
            Database.ChiTable.Load(this);
            RuneProgress.RefreshSkills(this);
            foreach (var item in RunesItem.Values)
            {
                Send(item);
            }
            // Rebuild only after all persisted stat inputs have loaded. Boost
            // restoration during LoadEntity must not read uninitialized data.
            LoadItemStats();
        }
        internal static Action<GameState> BeforeSyntheticScreenDispose = null;
        internal void ReleaseSyntheticPresence()
        {
            if (!Fake) return;
            try
            {
                var screenHook = BeforeSyntheticScreenDispose;
                if (screenHook != null) screenHook(this);
                if (Screen != null) Screen.DisposeTimers();
            }
            catch (Exception ex) { Console.WriteLine("[session] fake-screen type=" + ex.GetType().Name); }
            try { if (Pet != null) Pet.DisposeTimers(); }
            catch (Exception ex) { Console.WriteLine("[session] fake-pet type=" + ex.GetType().Name); }
            try { if (Server.Thread != null) Server.Thread.UnRegister(this); }
            catch (Exception ex) { Console.WriteLine("[session] fake-thread type=" + ex.GetType().Name); }
            try { if (_socket != null) _socket.Alive = false; }
            catch (Exception ex) { Console.WriteLine("[session] fake-socket type=" + ex.GetType().Name); }
            if (Screen != null) Screen.Owner = null;
            if (Pet != null) Pet.Owner = null;
            FakeLoaded = false;
        }
        public void FakeLoad2(uint UID, string Name = "")
        {
            RunFakeLoad(UID, delegate { FakeLoad2Owned(UID, Name); });
        }
        void RunFakeLoad(uint uid, Action load)
        {
            if (!Fake || uid == 0) return;
            Revora.Game.Features.SessionInstall.Enter(uid);
            bool installed = false;
            try
            {
                GameState current;
                if (Kernel.GamePool.TryGetValue(uid, out current))
                {
                    installed = object.ReferenceEquals(current, this);
                    return;
                }
                // Initialize while holding the lease, then publish the complete
                // actor. A real login cannot overtake loading or registration.
                load();
                installed = Kernel.GamePool.TryGetValue(uid, out current) && object.ReferenceEquals(current, this);
            }
            finally
            {
                try
                {
                    if (!installed)
                    {
                        Revora.Game.Features.SessionInstall.TryRemoveFake(uid, this);
                        if (Screen != null) Screen.DisposeTimers();
                        if (Pet != null) Pet.DisposeTimers();
                        if (Server.Thread != null) Server.Thread.UnRegister(this);
                    }
                }
                finally { Revora.Game.Features.SessionInstall.Exit(uid); }
            }
        }
        void FakeLoad2Owned(uint UID, string Name)
        {
            if (Name == "")
                Name = "Slayer[" + UID + "]";
            {
                this.Account = new Database.AccountTable(null);
                this.Account.EntityID = UID;
                this.ReadyToPlay();
                this.Entity = new Entity(EntityFlag.Player, false);
                this.Entity.Owner = this;
                this.Entity.Name = Name;
                this.Entity.UID = UID;
                this.Entity.Vitality = 537;
                this.Entity.Face = 37;
                this.Entity.Body = 1005;
                this.Entity.HairStyle = 630;
                this.Entity.Level = 140;
                this.Entity.Class = 15;
                this.Entity.Reborn = 2;
                this.Entity.MaxHitpoints = 20000;
                this.Entity.Hitpoints = this.Entity.MaxHitpoints;
                this.Entity.Mana = 800;

                this.Variables = new VariableVault();
                this.Friends = new SafeDictionary<uint, Game.ConquerStructures.Society.Friend>();
                this.Enemy = new SafeDictionary<uint, Game.ConquerStructures.Society.Enemy>();
                this.ChiData = new ChiTable.ChiData();
                this.ChiPowers = new List<ChiPowerStructure>();


                this.NobilityInformation = new Game.ConquerStructures.NobilityInformation();
                this.NobilityInformation.EntityUID = this.Entity.UID;
                this.NobilityInformation.Name = this.Entity.Name;
                this.NobilityInformation.Donation = 0;
                this.NobilityInformation.Rank = Game.ConquerStructures.NobilityRank.Serf;
                this.NobilityInformation.Position = -1;
                this.NobilityInformation.Gender = 1;
                this.NobilityInformation.Mesh = this.Entity.Mesh;
                if (this.Entity.Body % 10 >= 3)
                    this.NobilityInformation.Gender = 0;

                this.TeamArenaStatistic = new Network.GamePackets.TeamQualifierStatistic(true);
                this.TeamArenaStatistic.EntityID = this.Entity.UID;
                this.TeamArenaStatistic.Name = this.Entity.Name;
                this.TeamArenaStatistic.Level = this.Entity.Level;
                this.TeamArenaStatistic.Class = this.Entity.Class;
                this.TeamArenaStatistic.Model = this.Entity.Mesh;
                this.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.NotSignedUp;

                this.ArenaStatistic = new Network.GamePackets.QualifierStatistic(true);
                this.ArenaStatistic.EntityID = this.Entity.UID;
                this.ArenaStatistic.Name = this.Entity.Name;
                this.ArenaStatistic.Level = this.Entity.Level;
                this.ArenaStatistic.Class = this.Entity.Class;
                this.ArenaStatistic.Model = this.Entity.Mesh;
                this.ArenaPoints = ArenaTable.ArenaPointFill(this.Entity.Level);
                this.ArenaStatistic.LastArenaPointFill = DateTime.Now;
                this.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;

                this.ChampionStats = new Network.GamePackets.ChampionStatistic(true);
                this.ChampionStats.UID = this.Entity.UID;
                this.ChampionStats.Name = this.Entity.Name;
                this.ChampionStats.Level = this.Entity.Level;
                this.ChampionStats.Class = this.Entity.Class;
                this.ChampionStats.Model = this.Entity.Mesh;
                this.ChampionStats.Points = 0;
                this.ChampionStats.LastReset = DateTime.Now;
                this.ChiPowers = new List<ChiPowerStructure>();
                Retretead_ChiPowers = new List<ChiPowerStructure>();
                this.ChiData = new ChiTable.ChiData() { Name = this.Entity.Name, UID = this.Entity.UID, Powers = this.ChiPowers };

                this.Entity.Stamina = 150;

                this.Spells = new SafeDictionary<ushort, Interfaces.ISkill>();
                this.Proficiencies = new SafeDictionary<ushort, Interfaces.IProf>();

                try { GeneralData.LoginMessages(this); }
                catch (Exception ex) { Console.WriteLine("[game] fake-login-messages type=" + ex.GetType().Name); }
                if (Revora.Game.Features.SessionInstall.TryInstallFake(Entity.UID, this))
                {
                    if (Server.Thread != null) Server.Thread.Register(this);
                }
                else
                {
                    if (Screen != null) Screen.DisposeTimers();
                    if (Pet != null) Pet.DisposeTimers();
                }
            }
        }
        public void FakeLoad(uint UID)
        {
            RunFakeLoad(UID, delegate { FakeLoadOwned(UID); });
        }
        void FakeLoadOwned(uint UID)
        {
            this.Account = new Database.AccountTable(null);
            this.Account.EntityID = UID;
            ReadyToPlay();
            if (Database.EntityTable.LoadEntity(this) && this.Entity != null && this.Entity.FullyLoaded)
            {
                VariableVault variables;
                Database.EntityVariableTable.Load(this.Account.EntityID, out variables);
                this.Variables = variables;

                this.LoadData(true);

                if (this.Entity.GuildID != 0)
                    this.Entity.GuildSharedBp = this.Guild.Arsenal.BattlePower;

                this.ReviewMentor();

                GeneralData.LoginMessages(this);

                if (Revora.Game.Features.SessionInstall.TryInstallFake(Entity.UID, this))
                {
                    if (Server.Thread != null) Server.Thread.Register(this);
                    FakeLoaded = true;
                }
                else
                {
                    if (Screen != null) Screen.DisposeTimers();
                    if (Pet != null) Pet.DisposeTimers();
                }
                if (NobilityInformation != null)
                    Entity.NobilityRank = NobilityInformation.Rank;
            }
            else
            {
                Revora.Game.Features.SessionInstall.TryRemoveFake(UID, this);
                if (Screen != null) Screen.DisposeTimers();
                if (Pet != null) Pet.DisposeTimers();
            }
        }
        
        public DateTime LastClientOK { get; set; }
    }
}
