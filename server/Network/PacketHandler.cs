using System;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Drawing;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Network
{
    public unsafe static class PacketHandler
    {
        public static void WorldMessage(string message)
        {
            Message msg = new Message(message, System.Drawing.Color.MediumBlue, Message.Center);
            foreach (Client.GameState pClient in Server.GamePool)
                pClient.Send(msg);
        }
        public static readonly DateTime UnixEpoch = new DateTime();
        public static uint UnixTimestamp
        {
            get { return (uint)(DateTime.UtcNow - UnixEpoch).TotalSeconds; }
        }
        #region String PerfectionSystem
        public static string ReadString(byte[] data, ushort position, ushort count)
        {
            return Server.Encoding.GetString(data, position, count);
        }
        #endregion
        public static ulong ClientSeal = BitConverter.ToUInt64(Encoding.Default.GetBytes("TQClient"), 0);
        public static void HandlePacket(byte[] packet, Client.GameState client)
        {
            NpcReply reply;
            if (packet == null || packet.Length < 12) return;
            if (client == null) return;
            ushort Length = BitConverter.ToUInt16(packet, 0);
            ushort ID = BitConverter.ToUInt16(packet, 2);
            if (client.Filtering)
                if (client.PacketFilter.Filter(ID)) return;
            if (packet.Length < Length + 8 || ClientSeal != BitConverter.ToUInt64(packet, Length))
            {
                Console.WriteLine("[game] seal-fail id=" + ID + " len=" + Length + " buf=" + packet.Length);
                client.Disconnect();
                return;
            }
            if (client.Action == 1 && ID != PacketID.LoginPacket)
                Console.WriteLine("[game] pkt id=" + ID + " len=" + Length + " buf=" + packet.Length);
            switch (ID)
            {
                case 2030:
                    Revora.Game.Features.HouseFurniture.Place(client, packet);
                    break;
                #region Faces/Hair
                case 3510:
                    {
                        FaceAndHair.Handle(client, packet);
                        break;
                    }
                #endregion
                #region MsgExchangeShopRespond
                case PacketID.MsgExchangueShop:
                    {
                        new MsgExchangeShopRespond(packet).Handle(client);
                        break;
                    }
                #endregion
                #region MsgGlobalLottery
                case PacketID.MsgGlobalLottery:
                    {
                        var pkt = new MsgGlobalLottery();
                        var myPacket = new byte[packet.Length - 8];
                        for (int i = 0; i < myPacket.Length; i++)
                        {
                            myPacket[i] = packet[i];
                        }
                        if (pkt.Read(myPacket))
                            pkt.Handle(client);
                        break;
                    }
                #endregion
                #region MsgGlobalLotteryRankList
                case PacketID.MsgGlobalLotteryRankList:
                    {
                        var pkt = new MsgGlobalLotteryRankList();
                        var myPacket = new byte[packet.Length - 8];
                        for (int i = 0; i < myPacket.Length; i++)
                        {
                            myPacket[i] = packet[i];
                        }
                        if (pkt.Read(myPacket))
                            pkt.Handle(client);
                        break;
                    }
                #endregion
                #region Clone Attack
                case PacketID.CloneAttack:
                    {
                        if(Length<9 || Length>64 || packet.Length!=Length+8 || packet[8]<1 || packet[8]>2 || 9+packet[8]*4>Length
                            || !Game.Features.QuestInventory.Player(client))return;
                        lock(client.ItemSyncRoot) {
                        if(!Game.Features.QuestInventory.Player(client) || !client.Spells.ContainsKey(12090))return;
                        var attackd = BitConverter.ReadUint(packet, 4);
                        var clonecount = packet[8];
                        var requested=new HashSet<uint>();
                        for(int index=0;index<clonecount;index++) {
                            uint uid=BitConverter.ReadUint(packet,9+index*4);Entity clone,current;
                            if(!requested.Add(uid) || !client.Entity.MyClones.TryGetValue(uid,out clone) || clone==null || clone.UID!=uid
                                || !clone.IAmShadow || clone.ShadowID!=client.Entity.UID || !ReferenceEquals(clone.Owner,client)
                                || clone.MapID!=client.Entity.MapID || !(client.Map.Entities.TryGetValue(uid,out current) && ReferenceEquals(current,clone)
                                || client.Map.Companions.TryGetValue(uid,out current) && ReferenceEquals(current,clone)))return;
                        }
                        for (int i = 0; i < clonecount; i++)
                        {
                            var attackr = BitConverter.ReadUint(packet, 9 + (i * 4));
                            if (client.Entity.MyClones.Count < clonecount)
                                return;
                            var attack = new Attack(true);
                            if(client.Entity.AttackPacket!=null)attack.Deserialize((byte[])client.Entity.AttackPacket.ToArray().Clone());
                            attack.Attacker = attackr;
                            attack.Attacked = attackd;

                            if (client.Entity.MyClones.Count == 0)
                                return;
                            if (attackd == client.Entity.UID || client.Entity.MyClones.ContainsKey(attackd))
                                return;
                            Entity Clone = null;
                            if (client.Entity.MyClones.TryGetValue(attackr, out Clone))
                            {
                                Entity registered;
                                if(Clone==null || !Clone.IAmShadow || Clone.ShadowID!=client.Entity.UID || !ReferenceEquals(Clone.Owner,client)
                                    || Clone.MapID!=client.Entity.MapID || !(client.Map.Entities.TryGetValue(Clone.UID,out registered) && ReferenceEquals(registered,Clone)
                                    || client.Map.Companions.TryGetValue(Clone.UID,out registered) && ReferenceEquals(registered,Clone)))return;
                                Entity attacked = null;
                                SobNpcSpawn attackedsob = null;
                                if (client.Screen.TryGetValue(attackd, out attacked))
                                {
                                    uint damage = 0;
                                    if (attack.AttackType == Attack.Melee)
                                    {
                                        if (Game.Attacking.Handle.CanAttack(client.Entity, attacked, null, true))
                                        {
                                            damage = (uint)Game.Attacking.Calculate.Melee(client.Entity, attacked, ref attack);
                                            Game.Attacking.Handle.ReceiveAttack(Clone, attacked, attack, ref damage, null);
                                            attack.Damage = damage;
                                            client.SendScreen(attack);
                                        }
                                    }
                                }
                                else if (client.Screen.TryGetSob(attackd, out attackedsob))
                                {
                                    uint damage = 0;
                                    if (attack.AttackType == Attack.Melee)
                                    {
                                        if (Game.Attacking.Handle.CanAttack(client.Entity, attackedsob, null))
                                        {
                                            damage = (uint)Game.Attacking.Calculate.Melee(client.Entity, attackedsob, ref attack);
                                            Game.Attacking.Handle.ReceiveAttack(Clone, attackedsob, attack, damage, null);
                                            attack.Damage = damage;
                                            client.SendScreen(attack);
                                        }
                                    }
                                }
                            }
                        }
                        }
                        break;
                    }
                #endregion
                #region Union
                case PacketID.MsgLeagueOpt:
                    {
                        Revora.Network.GamePackets.Union.Union.Prosses(packet, client);
                        break;
                    }
                case PacketID.UnionJoin:
                    {
                        byte[] pac = new byte[23 + 8];
                        Writer.WriteUInt32(23, 0, pac);
                        Writer.WriteUInt32(2622, 2, pac);
                        Writer.WriteUInt32(8, 4, pac);
                        Writer.WriteUInt32(3, 6, pac);
                        Writer.WriteUInt32(3, 14, pac);
                        client.Send(pac);
                        break;
                    }
                case PacketID.UnionQuit:
                    {
                        byte[] pac = new byte[23 + 8];
                        Writer.WriteUInt32(23, 0, pac);
                        Writer.WriteUInt32(2622, 2, pac);
                        Writer.WriteUInt32(8, 4, pac);
                        Writer.WriteUInt32(3, 14, pac);
                        client.Send(pac);
                        break;
                    }
                #endregion
                #region Advertise 2225 | 2227
                case 2226:
                    {
                        Revora.GamePackets.Advertise.Top8.Clear();
                        //Revora.GamePackets.Advertise.LoadTop8();
                        byte PageNumber = packet[4];
                        Revora.Network.Advertise2.Execute(client, packet);
                        break;
                    }
                #region advetise screen
                case 2227:
                    {
                        switch (packet[4])
                        {
                            case 1:
                                {
                                    uint Id = BitConverter.ToUInt32(packet, 8);
                                    if (!Revora.Game.Features.QuestInventory.Player(client, false)) break;
                                    lock (client.ItemSyncRoot)
                                    {
                                        Game.ConquerStructures.Society.Guild guild;
                                        if (Revora.Game.Features.QuestInventory.Player(client, false)
                                            && client.Guild == null && client.AsMember == null && client.Entity.GuildID == 0
                                            && Kernel.Guilds.TryGetValue(Id, out guild)
                                            && !guild.BlackList.Contains(client.Entity.UID)
                                            && Guild_Advertise.PassJoinRequirements(client, guild))
                                        {
                                            guild.AddMember(client);

                                        }

                                    }
                                    break;
                                }
                            case 2:
                                {

                                    byte[] recruit = new byte[288];
                                    Writer.Write(280, 0, recruit);
                                    Writer.Write(2225, 2, recruit);
                                    client.Send(recruit);
                                    break;
                                }

                        }
                        break;
                    }
                #endregion
                case 2225:
                    // The legacy branch charged for a bulletin without publishing
                    // an advertisement. Keep this unverified purchase unavailable.
                    if (Game.Features.QuestInventory.Player(client,false))
                        client.Send("Guild advertisements are not available yet.");
                    return;
                #endregion Advertise
                #region Perfection
                case 3257:
                    {
                        uint action, target;
                        if (!PrestigePackets.TryReadRequest(packet, out action, out target)) break;
                        if (!PrestigeRanking.AllowQuery(client)) break;
                        if (action == 0) PrestigeRanking.SendWall(client);
                        else if (target != 0)
                        {
                            if (Kernel.GamePool.ContainsKey(target))
                                GeneralData.ObserveEquipment(new Data(true) { ID = Data.ObserveEquipment, UID = client.Entity.UID, dwParam = target }, client);
                            else client.Send(PrestigePackets.Unavailable());
                        }
                        break;
                    }
                case 3256:
                    {
                        var myPacket = new CMsgEquipRefineRank(packet);
                        if (myPacket.Read())
                            myPacket.Handle(client);
                    }
                    break;
                case 3253:
                    {
                        var myPacket = new CMsgUserAbilityScore(packet);
                        if (myPacket.Read())
                            myPacket.Handle(client);
                    }
                    break;
                case 3255:
                    {
                        var myPacket = new CMsgItemRefineRecord(packet);
                        if (myPacket.Read())
                            myPacket.Handle(client);
                    }
                    break;
                case 3251:
                    {
                        var myPacket = new CMsgItemRefineOpt(packet);
                        if (myPacket.Read())
                            myPacket.Handle(client);
                    }
                    break;
                #endregion
                #region Auction House
                case 1320:
                    {
                        Auction.ProcesoInglesCompletoSubasta(packet, client);
                        break;
                    }
                case 1321:
                    {
                        MsgAuctionItem.Process(packet, client);
                        break;
                    }
                case 1322:
                    {
                        Auction.ProcessAuctionInfo(packet, client);
                        break;
                    }
                #endregion
                #region InnerPower
                case 2610:
                    {
                        if (Length < 13 || Length > 64 || packet.Length != Length + 8) return;
                        byte Action = packet[4];
                        uint dwparam = BitConverter.ToUInt32(packet, 5);
                        uint dwparam2 = BitConverter.ToUInt32(packet, 9);

                        if (client.InnerPower == null) break;
                        switch (Action)
                        {

                            case 5:
                                {
                                    Client.GameState user;
                                    if (Kernel.GamePool.TryGetValue(dwparam, out user)
                                        && user != null && user.Entity != null && user.InnerPower != null)
                                    {
                                        user.InnerPower.AddPotency(user, 0);
                                        client.Send(new Network.GamePackets.InnerPowerGui(user.InnerPower.GetNeiGongs()).ToArray());
                                        var stages = user.InnerPower.Stages.Where(p => p.Complete);
                                        if (stages.Count() > 0)
                                            client.Send(new Network.GamePackets.InnerPowerStage(InnerPowerStage.ActionID.UpdateStage, user.Entity.UID, stages.Last()).ToArray());
                                    }
                                    break;
                                }
                            case 0:
                                {

                                    Client.GameState user;
                                    if (Kernel.GamePool.TryGetValue(dwparam, out user)
                                        && user != null && user.Entity != null && user.InnerPower != null
                                        && user.InnerPower.Stages != null && dwparam2 > 0
                                        && dwparam2 - 1 < user.InnerPower.Stages.Length)
                                    {
                                        client.Send(new Network.GamePackets.InnerPowerStage(InnerPowerStage.ActionID.UpdateStage, user.Entity.UID, user.InnerPower.Stages[dwparam2 - 1]).ToArray());
                                        client.Send(new Network.GamePackets.InnerPowerStage(InnerPowerStage.ActionID.UpdateScore, user.Entity.UID, user.InnerPower.Stages[dwparam2 - 1]).ToArray());
                                    }
                                    break;
                                }
                            case 4:
                            case 3:
                                Game.Features.InnerPowerProgress.Process(client, Action, dwparam);
                                break;
                        }
                        break;
                    }

                #endregion
                #region WayOfHeroes
                case PacketID.WayofHeroes:
                    {
                        WayofHeroes.Process(packet, client);
                        break;
                    }
                #endregion
                #region Activeness
                case PacketID.ActivenessGift:
                    {
                        ActivenessGift.Process(packet, client);
                        break;
                    }
                #endregion
                #region ConfirmMap
                case PacketID.ConfirmMap:
                    {
                        ConfirmMap.Process(client);
                        break;
                    }
                #endregion
                #region MsgSignIn
                case PacketID.MsgSignIn: // DailyFix By ThroneCO
                    {
                        MsgSignIn.Handle(client, packet);
                        break;
                    }
                #endregion
                #region Roulette
                case PacketID.RouletteAction:
                    {
                        Roulette.MsgRouletteAction.Poroces(client, packet);
                        break;
                    }
                case PacketID.RouletteCheck:
                    {
                        Roulette.MsgRouletteCheck.Poroces(client, packet);
                        break;
                    }
                case PacketID.RouletteSignUp:
                    {
                        Roulette.MsgRouletteSignUp.Poroces(client, packet);
                        break;
                    }
                #endregion
                #region SkillSoul
                case PacketID.SkillSoul:
                    {
                        Spell.Poroces(packet, client);
                        break;
                    }
                #endregion
                #region JiangHu
                case PacketID.JiangHu:
                    {
                        Game.JiangHu.JiangHus(packet, client);
                        break;
                    }
                case PacketID.JiangHuStudy:
                    {
                        Game.JiangHu.ExtraStudy(packet, client);
                        break;
                    }
                case PacketID.JiangHuRanking:
                    {
                        Game.JiangHu.JiangRanking(packet, client);
                        break;
                    }
                case PacketID.JiangHuSettings:
                    {
                        Game.JiangHu.JiangSettings(packet, client);
                        break;
                    }
                #endregion
                #region ClanCityWar
                case PacketID.ClanCityWar:
                    {
                        ClanWar.Process(packet, client);
                        break;
                    }
                #endregion
                #region ThrillingSpooks
                case PacketID.ThrillingSpooks:
                    {
                        // 2400 has no verified native entry layout. Entry awaits the owned NPC encounter route.
                        break;
                    }
                #endregion
                #region AutoHunt
                case PacketID.AutoHunt:
                    {
                        AutoHunt.Process(packet, client);
                        break;
                    }
                #endregion
                #region CurrentFlags
                case PacketID.CurrentFlag:
                    {
                        CurrentFlags.Process(packet, client);
                        break;
                    }
                #endregion
                #region OneArmedBandit
                case PacketID.OneArmedBandit:
                    {
                        SlotMachineRequest.Process(packet, client);
                        break;
                    }
                #endregion
                #region GuildAdvertise 2225-2226-2227
                /* case PacketID.AdvertiseCommend:
                    {
                        Guild_AdvertiseCommends.Process(packet, client);
                        break;
                    }
                case PacketID.AdvertiseRank:
                    {
                        client.Send(packet);
                        Guild_Advertise.Process(client, packet[4]);
                        break;
                    }
                case PacketID.AdvertiseRecruit:
                    {
                        Guild_AdvertiseRecruit.Process(packet, client);
                        break;
                    }*/
                #endregion
                #region EnitityCreate
                case PacketID.EnitityCreate:
                    {
                        EnitityCreate.Process(packet, client);
                        break;
                    }
                #endregion
                #region Message
                case PacketID.Message:
                    {
                        Message.Process(packet, client);
                        break;
                    }
                #endregion
                #region ItemUsage
                case PacketID.ItemUsage:
                    {
                        ItemUsage.Process(packet, client);
                        break;
                    }
                #endregion
                #region String
                case PacketID.String:
                    {
                        _String.Process(packet, client);
                        break;
                    }
                #endregion
                #region KnownPersons
                case PacketID.KnownPersons:
                    {
                        KnownPersons.Process(packet, client);
                        break;
                    }
                #endregion
                #region Attack
                case PacketID.Attack:
                    {
                        Attack.Process(packet, client);
                        break;
                    }
                #endregion
                #region WardRobe
                case 3300:
                    {
                        var pkt = new CoatStorage();

                        var myPacket = new byte[packet.Length - 8];

                        for (int i = 0; i < myPacket.Length; i++)
                        {
                            myPacket[i] = packet[i];
                        }

                        if (pkt.Read(myPacket))
                        {
                            pkt.Handle(client);
                        }
                        break;
                    }
                case 3301:
                    {
                        var pkt = new TitleStorage();

                        var myPacket = new byte[packet.Length - 8];

                        for (int i = 0; i < myPacket.Length; i++)
                        {
                            myPacket[i] = packet[i];
                        }

                        if (pkt.Read(myPacket))
                        {
                            pkt.Handle(client);
                        }
                        break;
                    }
                #endregion
                #region Team
                case PacketID.Team:
                    {
                        Team.Process(packet, client);
                        break;
                    }
                #endregion
                #region Atributes
                case PacketID.Atributes:
                    {
                        Game.Features.CharacterAttributes.Allocate(client, packet);
                        break;
                    }
                #endregion
                #region Socketing
                case PacketID.Socketing:
                    {
                        EmbedSocket.Process(packet, client);
                        break;
                    }
                #endregion
                #region WindowStats
                case PacketID.WindowStats:
                    {
                        uint UID = BitConverter.ToUInt32(packet, 8);
                        Client.GameState Client;
                        if (Kernel.GamePool.TryGetValue(UID, out Client))
                        {
                            WindowStats Stats = new WindowStats(Client);
                            client.Send(Stats.ToArray());
                        }
                        break;
                    }
                #endregion
                #region LoginPacket
                case PacketID.LoginPacket:
                    {
                        Console.WriteLine("[game] 1052 action=" + client.Action + " token=" + BitConverter.ToUInt32(packet, 4));
                        if (client.LoggedIn || client.Action == 2)
                            break;
                        if (client.Action == 1)
                        {
                            Connect connect = new Connect();
                            connect.Deserialize(packet);
                            GeneralData.AppendConnect(connect, client);
                        }
                        else
                            client.Disconnect();
                        break;
                    }
                #endregion
                #region Trade
                case PacketID.Trade:
                    {
                        Trade.Process(packet, client);
                        break;
                    }
                #endregion
                #region Lottery
                case PacketID.Lottery:
                    {
                        Game.Lottery.Process(packet, client);
                        break;
                    }
                #endregion
                #region FloorItem
                case PacketID.FloorItem:
                    {
                        FloorItem.Process(packet, client);
                        break;
                    }
                #endregion
                #region Warehouses
                case PacketID.Warehouses:
                    {
                        Warehouse.Process(packet, client);
                        break;
                    }
                #endregion
                #region GuildCommand
                case PacketID.GuildCommand:
                    {
                        GuildCommand.Process(packet, client);
                        break;
                    }
                #endregion
                #region Poker
                case PacketID.PokerPlayerInfo:
                case PacketID.PokerPlayerMove:
                case PacketID.PokerLeaveTable:
                case PacketID.PokerJoinTable:
                    {
                        new Game.HandlePoker(packet, client);
                        break;
                    }
                case PacketID.MsgTexasExInteractive:
                case PacketID.MsgTexasExMatchFieldList:
                case PacketID.MsgTexasExChampionshipList:
                case PacketID.MsgTexasPersonalInfo:
                case PacketID.MsgTexasExChampionshipRank:
                case PacketID.MsgTexasExMyChampionshipRank:
                case PacketID.MsgTexasChampionshipTableChip:
                    break;
                #endregion
                #region Enlight
                case PacketID.Enlight:
                    {
                        Enlight.Process(packet, client);
                        break;
                    }
                #endregion
                #region MsgTaskStatus
                //case PacketID.MsgTaskStatus:
                //    {
                //        QuestPacket quest = new QuestPacket(false);
                //        quest.Deserialize(packet);
                //        switch (quest.Action)
                //        {
                //            case QuestPacket.QuestAction.Begin:
                //                {
                //                    /* if (client.Quests.AllowAccept())
                //                         client.Quests.Accept((QuestID)quest[0].UID, 0);
                //                     client.Send(quest);*/
                //                    break;
                //                }
                //            case QuestPacket.QuestAction.List:
                //                {
                //                    client.Quests.SendFullGUI();
                //                    break;
                //                }
                //        }
                //        break;
                //    }
                #endregion
                #region MsgTask
                case PacketID.MsgTaskDetailInfo:
                    {
                        MsgTaskDetailInfo quest = new MsgTaskDetailInfo(true);
                        quest.Deserialize(packet);
                        break;
                    }
                case PacketID.MsgTaskStatus:
                    {
                        MsgTaskStatus.Process(packet, client);
                        break;
                    }
                #endregion

                #region QuizShow
                case PacketID.QuizShow:
                    {
                        OpenQuiz.Process(packet, client);
                        break;
                    }
                #endregion
                #region NpcDialog
                case PacketID.NpcInit:
                    {
                        if (Revora.Game.Features.HouseFurniture.Remove(client, packet)) return;
                        if (Revora.Game.Features.HouseFurniture.OpenBox(client, packet)) return;
                        if (client.Action != 2)
                            return;
                        CMsgNpc req = packet.ConvertToProto<CMsgNpc>();

                        #region warehouse and infos and Guild ExpelMember
                        Data data = new Data(true);
                        if (ID == 2031)
                            client.ActiveNpc = (uint)req.UID;
                        if (client.Account.State == Database.AccountTable.AccountState.GameMaster || client.Account.State == Database.AccountTable.AccountState.Administrador)
                            client.Send(new Message("NpcID[" + client.ActiveNpc + "]", Color.Red, Message.TopLeft));
                        if (req.UID == 12)
                        {
                            if (client.WarehousePW != 0)
                            {
                                if (!client.WarehouseOpen)
                                {
                                    data = new Data(true);
                                    data.ID = Data.OpenWindow;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = 568;
                                    data.wParam1 = client.Entity.X;
                                    data.wParam2 = client.Entity.Y;
                                    client.Send(data);
                                    break;
                                }
                            }
                            if (client.Entity.VIPLevel > 0)
                            {
                                if (!client.Entity.Dead)
                                {
                                    //Data data = new Data(true);
                                    data.ID = Data.OpenWindow;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = Data.WindowCommands.VIPWarehouse;
                                    data.wParam1 = client.Entity.X;
                                    data.wParam2 = client.Entity.Y;
                                    client.Send(data);
                                }
                                break;
                            }
                        }
                        Interfaces.INpc npc = null;
                        #endregion

                        if (client.Map.Npcs.TryGetValue(client.ActiveNpc, out npc))
                        {
                            NpcRequest req2 = new NpcRequest(true);
                            req2.NpcID = client.ActiveNpc;
                            if (req.OptionID <= 255)
                                req2.OptionID = (byte)req.OptionID;
                            if (client.ActiveNpc != 8798 && client.ActiveNpc != 8308 &&
                                Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17) return;
                            if (req.OptionID == 255 || (req.OptionID == 0 && ID == 2032)) return;
                            if (client.Entity.Dead) return;
                            Npcs.GetDialog(req2, client);

                        }


                        break;
                    }
                case PacketID.NpcDialog:
                    {
                        if (client.Action != 2) return;
                        if (Length < 20 || packet.Length != Length + 8 || packet[17] > Length - 18) return;
                        // Item menus have no world NPC; validate before routing their owned offer.
                        if (client.ActiveNpc == Revora.Game.Features.P7SoulChoicePack.ItemId)
                        {
                            if (Length < 20 || Length > 120 || packet.Length != Length + 8
                                || packet[17] > Length - 18
                                || BitConverter.ToUInt64(packet, packet.Length - 8) != ClientSeal) return;
                            var choice = new NpcRequest(false);
                            choice.Deserialize(packet);
                            Revora.Game.Features.P7SoulChoicePack.HandleNpc(client, choice);
                            return;
                        }
                        if ((client.ActiveNpc == Game.Champion.RegistrarNpc || client.ActiveNpc == Game.Champion.StewardNpc)
                            && (Length < 20 || Length > 120 || packet.Length != Length + 8 || packet[17] > Length - 18
                                || BitConverter.ToUInt64(packet, packet.Length - 8) != ClientSeal)) return;
                        if ((client.ActiveNpc == 30156 || client.ActiveNpc == 720842 || client.ActiveNpc == 3004259) && !Revora.Game.Features.HouseVisits.Frame(packet)) return;
                        if ((Revora.Game.Features.AncientDevilQuest.Handles(client.ActiveNpc) || Revora.Game.Features.MonkEpicQuest.Handles(client.ActiveNpc) || Revora.Game.Features.TaoistEpicQuest.Handles(client.ActiveNpc) || client.ActiveNpc == 10000 || Revora.Game.Features.NinjaEpicQuest.Handles(client.ActiveNpc) || client.ActiveNpc == 4720 || client.ActiveNpc == 4271 || client.ActiveNpc == 10001 || client.ActiveNpc == 400 || Revora.Game.Features.AbyssTrialQuest.Handles(client.ActiveNpc) || Revora.Game.Features.AbyssTrialJournal.Selected(client) || client.ActiveNpc == Revora.Game.Features.HeavenlyStoneCrafting.Sereth || client.ActiveNpc == Revora.Game.Features.ExplorationWorld.GardenKeeper || client.ActiveNpc == Revora.Game.Features.ElitePkEvent.Envoy || client.ActiveNpc == Revora.Game.Features.DailyPriceQuest.ListItem || client.ActiveNpc == Revora.Game.Features.DailyQuestEnvoySystem.NpcId || client.ActiveNpc == 12333 || client.ActiveNpc == Revora.Game.Features.ExorcismWorld.Earth || client.ActiveNpc == Revora.Game.Features.AnniversaryQuest.Npc || Revora.Game.Features.ReleaseSoulsQuest.Handles(client.ActiveNpc))
                            && (Length < 20 || Length > 120 || packet.Length != Length + 8 || packet[17] > Length - 18)) return;
                        NpcRequest req = new NpcRequest(false);
                        if ((client.ActiveNpc == 10022 || Revora.Game.Features.WarriorEpicQuest.Handles(client.ActiveNpc) || Revora.Game.Features.TrojanEpicQuest.Handles(client.ActiveNpc)) &&
                            (Length < 20 || Length > 120 || packet.Length != Length + 8 || packet[17] > Length - 18)) return;
                        req.Deserialize(packet);
                        // Close before eligibility/menu handlers can recreate the dialogue.
                        if (req.IsDialogueClose) { client.ActiveNpc = 0; return; }
                        if (Revora.Game.Features.AncientDevilQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.HouseVisits.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.WarriorEpicQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.TrojanEpicQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.TrojanEpicTraining.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.NinjaEpicQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.MonkEpicQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.TaoistEpicQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.NinjaTraining.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.MonkTraining.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.ClanDisbanding.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.WarriorTraining.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.AssassinTraining.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.PaidReincarnation.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.SecondRebirth.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.FirstRebirth.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.AbyssTrialQuest.HandleNpc(client, req)) return;
                        if (Revora.Game.Features.ReleaseSoulsQuest.HandleNpc(client, req)) return;
                        if (client.ActiveNpc == Revora.Game.Features.ExplorationWorld.GardenKeeper && Revora.Game.Features.MagnoliaQuest.HandleNpc(client, req)) return;
                        if (client.ActiveNpc == Revora.Game.Features.ElitePkEvent.Envoy) { if (Revora.Game.Features.ElitePkEvent.Current != null) Revora.Game.Features.ElitePkEvent.Current.HandleNpc(client, req); return; }
                        if (client.ActiveNpc == Revora.Game.Features.AnniversaryQuest.Npc && Revora.Game.Features.AnniversaryQuest.HandleNpc(client, req)) return;
                        if (client.ActiveNpc == 12333 && Revora.Game.Features.ExorcismQuest.HandleNpc(client, req)) return;
                        if (client.ActiveNpc == Revora.Game.Features.ExorcismWorld.Earth && Revora.Game.Features.ExorcismWorld.HandleNpc(client, req)) return;
                        if (req.InteractType == 102)
                        {
                            if (Length < 20 || Length > 120 || packet.Length != Length + 8 || packet[17] > Length - 18) return;
                            var quest = req.NpcID - QuestInfo.ActionBase;
                            if (Revora.Game.Features.TrojanEpicJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TrojanEpicJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.NewcomerIntroduction.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.NewcomerIntroduction.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.TomboyQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TomboyQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.DesertTreasureQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertTreasureQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.DefenseTalismanQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DefenseTalismanQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.AttackTalismanQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.AttackTalismanQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.TempestWingsQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TempestWingsQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.DesertExpeditionQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertExpeditionQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.FamilyFeudQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.FamilyFeudQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.HeroWingsQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.HeroWingsQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.DesertBloodQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertBloodQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.GhostCityQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.GhostCityQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.MadManQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MadManQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.SoulGeneralQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.SoulGeneralQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.FranticMonkeysQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.FranticMonkeysQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.MysticRingQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MysticRingQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.ArtisanFeatherQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.ArtisanFeatherQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.SpunGoldQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.SpunGoldQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.ConnectorQuest.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.ConnectorQuest.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.PowerBookJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.PowerBookJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.WarriorEpicJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.WarriorEpicJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.MonkEpicJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MonkEpicJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.PirateEpicJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.PirateEpicJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.TaoistEpicJournal.Protect(quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TaoistEpicJournal.Refresh(client);
                                return;
                            }
                            if (Revora.Game.Features.AbyssTrialJournal.Protect(client, quest)) {
                                if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.AbyssTrialJournal.Refresh(client);
                                return;
                            }
                            client.Quests.QuitQuest((QuestID)quest);
                        }
                        #region WarehouseOpen
                        if (req.InteractType == NpcReply.MessageBox)
                        {
                            if (client.Entity.ActivePOPUP == 99995)
                            {
                                client.Entity.ActivePOPUP = 0;
                                break;
                            }
                            if (req.OptionID == 255 && client.MessageOK != null)
                                client.MessageOK.Invoke(client);
                            else
                                if (client.MessageCancel != null)
                                client.MessageCancel.Invoke(client);

                            client.MessageOK = null;
                            client.MessageCancel = null;
                        }
                        else
                        {
                            Data data = new Data(true);
                            if (ID == 2031)
                                client.ActiveNpc = req.NpcID;
                            if (req.NpcID == 12)
                            {
                                if (client.WarehousePW != 0)
                                {
                                    if (!client.WarehouseOpen)
                                    {
                                        data = new Data(true);
                                        data.ID = Data.OpenWindow;
                                        data.UID = client.Entity.UID;
                                        data.TimeStamp = Time32.Now;
                                        data.dwParam = 568;
                                        data.wParam1 = client.Entity.X;
                                        data.wParam2 = client.Entity.Y;
                                        client.Send(data);
                                        break;
                                    }
                                }
                                else
                                {
                                    client.Send(new Message("To protect your items that are stored in warehouse, you should set a Secondary password at Settings.", System.Drawing.Color.Red, Message.TopLeft));
                                }
                                if (client.Entity.VIPLevel > 0)
                                {
                                    data = new Data(true);
                                    data.ID = Data.OpenWindow;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = Data.WindowCommands.VIPWarehouse;
                                    data.wParam1 = client.Entity.X;
                                    data.wParam2 = client.Entity.Y;
                                    client.Send(data);
                                    break;
                                }
                            }
                            #endregion
                            #region HouseBox
                            if (client.Entity.MapID == (ushort)client.Entity.UID)
                            {
                                if (client.Map.Npcs.ContainsKey(req.NpcID))
                                {
                                    if ((client.Map.Npcs[req.NpcID].Mesh / 10) == 820)
                                    {
                                        data = new Data(true);
                                        data.ID = Data.OpenWindow;
                                        data.UID = client.Entity.UID;
                                        data.TimeStamp = Time32.Now;
                                        data.dwParam = Data.WindowCommands.Warehouse;
                                        data.wParam1 = client.Entity.X;
                                        data.wParam2 = client.Entity.Y;
                                        client.Send(data);
                                        break;
                                    }
                                }
                            }
                            #endregion
                            #region ExpelMember
                            Interfaces.INpc npc = null;
                            if (req.InteractType == 102)
                            {
                                if (client.Guild != null)
                                {
                                    if (client.AsMember.Rank == Game.Enums.GuildMemberRank.GuildLeader)
                                    {
                                        client.Guild.ExpelMember(req.Input, false);
                                    }
                                }
                                return;
                            }
                            #endregion
                            #region Item ID
                            Npcs dialog = new Npcs(client);

                            if (client.ActiveNpc >= 728525 && client.ActiveNpc <= 728527)
                            {
                                if (client.Inventory.Contains(client.ActiveNpc, 1))
                                {
                                    if (req.OptionID >= 1 && req.OptionID <= 3)
                                    {
                                        uint color = 255 | 150 << 16;
                                        if (req.OptionID == 2)
                                            color = 150 << 8 | 255 << 16;
                                        else if (req.OptionID == 3)
                                            color = 150 | 255 << 8;
                                        byte plus = 1;
                                        if (client.ActiveNpc % 10 == 6) plus = 3;
                                        else if (client.ActiveNpc % 10 == 7) plus = 6;
                                        client.Inventory.Remove(client.ActiveNpc, 1);
                                        ConquerItem _item = new ConquerItem(true);
                                        _item.ID = 300000;
                                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                                        _item.Plus = plus;
                                        _item.Effect = Game.Enums.ItemEffect.Horse;
                                        _item.SocketProgress = color;
                                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                                    }
                                }
                                return;
                            }
                            if (client.ActiveNpc == 720842)
                            {
                                Revora.Game.Features.SwordMasterEncounter.Confirm(client, req);
                                return;
                            }
                            if (client.ActiveNpc == 3004259)
                            {
                                Revora.Game.Features.PurpleBansheeEncounter.Confirm(client, req);
                                return;
                            }
                            if (client.ActiveNpc == Revora.Game.Features.DailyPriceQuest.ListItem)
                            {
                                Revora.Game.Features.DailyPriceQuest.Travel(client, req);
                                return;
                            }
                            if (client.ActiveNpc == 3004247)
                            {
                                if (client.Inventory.Contains(client.ActiveNpc, 1))
                                {
                                    if (req.OptionID >= 1 && req.OptionID <= 13)
                                    {
                                        uint IDs = 800020;
                                        if (req.OptionID == 2)
                                            IDs = 800111;
                                        else if (req.OptionID == 3)
                                            IDs = 800215;
                                        else if (req.OptionID == 4)
                                            IDs = 800422;
                                        else if (req.OptionID == 5)
                                            IDs = 800917;
                                        else if (req.OptionID == 6)
                                            IDs = 800618;
                                        else if (req.OptionID == 7)
                                            IDs = 800142;
                                        else if (req.OptionID == 8)
                                            IDs = 800255;
                                        else if (req.OptionID == 9)
                                            IDs = 800725;
                                        else if (req.OptionID == 10)
                                            IDs = 800810;
                                        else if (req.OptionID == 11)
                                            IDs = 800811;
                                        else if (req.OptionID == 12)
                                            IDs = 800522;
                                        else if (req.OptionID == 13)
                                            IDs = 801004;
                                        ConquerItem _item = new ConquerItem(true);
                                        _item.ID = IDs;
                                        _item.Durability = _item.MaximDurability = Revora.Database.ConquerItemInformation.BaseInformations[IDs].Durability;
                                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                                        client.Inventory.Remove(client.ActiveNpc, 1);
                                    }
                                }
                                return;
                            }
                            if (client.ActiveNpc == 3004464)
                            {
                                if (client.Inventory.Contains(client.ActiveNpc, 1))
                                {
                                    if (req.OptionID >= 1 && req.OptionID <= 3)
                                    {
                                        uint IDs = 616439;
                                        if (req.OptionID == 2)
                                            IDs = 800111;
                                        else if (req.OptionID == 3)
                                            IDs = 800215;
                                        ConquerItem _item = new ConquerItem(true);
                                        _item.ID = IDs;
                                        _item.Durability = _item.MaximDurability = Revora.Database.ConquerItemInformation.BaseInformations[IDs].Durability;
                                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                                        client.Inventory.Remove(client.ActiveNpc, 1);
                                    }
                                }
                                return;
                            }
                            //////////////

                            //////////////
                            if (client.ActiveNpc == 3004248)
                            {
                                if (client.Inventory.Contains(client.ActiveNpc, 1))
                                {
                                    if (req.OptionID >= 1 && req.OptionID <= 16)
                                    {
                                        uint IDs = 823058;
                                        if (req.OptionID == 2)
                                            IDs = 824018;
                                        else if (req.OptionID == 3)
                                            IDs = 822071;
                                        else if (req.OptionID == 4)
                                            IDs = 821033;
                                        else if (req.OptionID == 5)
                                            IDs = 820073;
                                        else if (req.OptionID == 6)
                                            IDs = 821034;
                                        else if (req.OptionID == 7)
                                            IDs = 823060;
                                        else if (req.OptionID == 8)
                                            IDs = 822072;
                                        else if (req.OptionID == 9)
                                            IDs = 824020;
                                        else if (req.OptionID == 10)
                                            IDs = 820076;
                                        else if (req.OptionID == 11)
                                            IDs = 820074;
                                        else if (req.OptionID == 12)
                                            IDs = 820075;
                                        else if (req.OptionID == 13)
                                            IDs = 824019;
                                        else if (req.OptionID == 14)
                                            IDs = 823059;
                                        else if (req.OptionID == 15)
                                            IDs = 823061;
                                        else if (req.OptionID == 16)
                                            IDs = 823062;
                                        ConquerItem _item = new ConquerItem(true);
                                        _item.ID = IDs;
                                        _item.Durability = _item.MaximDurability = Revora.Database.ConquerItemInformation.BaseInformations[IDs].Durability;
                                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                                        client.Inventory.Remove(client.ActiveNpc, 1);
                                    }
                                }
                                return;
                            }
                            if (client.ActiveNpc == 3004249)// 3004282 refinery lvl 7 10 critical
                            {
                                if (client.Inventory.Contains(client.ActiveNpc, 1))
                                {
                                    if (req.OptionID >= 1 && req.OptionID <= 28)
                                    {
                                        uint IDs = 3004136;
                                        if (req.OptionID == 2)
                                            IDs = 3004139;
                                        else if (req.OptionID == 3)
                                            IDs = 3004142;
                                        else if (req.OptionID == 4)
                                            IDs = 3004144;
                                        else if (req.OptionID == 5)
                                            IDs = 3004146;
                                        else if (req.OptionID == 6)
                                            IDs = 3004149;
                                        else if (req.OptionID == 7)
                                            IDs = 3004154;
                                        else if (req.OptionID == 8)
                                            IDs = 3004155;
                                        else if (req.OptionID == 9)
                                            IDs = 3004157;
                                        else if (req.OptionID == 10)
                                            IDs = 3004158;
                                        else if (req.OptionID == 11)
                                            IDs = 3004160;
                                        else if (req.OptionID == 12)
                                            IDs = 3004163;
                                        else if (req.OptionID == 13)
                                            IDs = 3004164;
                                        else if (req.OptionID == 14)
                                            IDs = 3004281;
                                        else if (req.OptionID == 15)
                                            IDs = 3004283;
                                        else if (req.OptionID == 16)
                                            IDs = 3004284;
                                        else if (req.OptionID == 17)
                                            IDs = 3004285;
                                        else if (req.OptionID == 18)
                                            IDs = 3004286;
                                        else if (req.OptionID == 19)
                                            IDs = 3004287;
                                        else if (req.OptionID == 20)
                                            IDs = 3004288;
                                        else if (req.OptionID == 21)
                                            IDs = 3004289;
                                        else if (req.OptionID == 22)
                                            IDs = 3004290;
                                        else if (req.OptionID == 23)
                                            IDs = 3004291;
                                        else if (req.OptionID == 24)
                                            IDs = 3004292;
                                        else if (req.OptionID == 25)
                                            IDs = 3004293;
                                        else if (req.OptionID == 26)
                                            IDs = 3004294;
                                        else if (req.OptionID == 27)
                                            IDs = 3004295;
                                        ConquerItem _item = new ConquerItem(true);
                                        _item.ID = IDs;
                                        _item.Durability = _item.MaximDurability = Revora.Database.ConquerItemInformation.BaseInformations[IDs].Durability;
                                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                                        client.Inventory.Remove(client.ActiveNpc, 1);
                                    }
                                }
                                return;
                            }
                            #endregion
                            #region Npc Dialog
                            //Npcs dialog = new Npcs(client);

                            if (client.ActiveNpc >= 9994 && client.ActiveNpc <= 9997)
                            {
                                if (req.OptionID == 255 || (req.OptionID == 0 && ID == 2032)) return;
                                switch (req.OptionID)
                                {
                                    case 0:
                                        {
                                            dialog.Text("Would~you~like~to~go~back~to~Guild~Area~in~Twin~City?~I~will~teleport~our~members~there~free~and~charge~nonmembers~500.");
                                            dialog.Option("Please~teleport~me~there.", 1);
                                            dialog.Option("Just~passing~by.", 255);
                                            dialog.Avatar(3);
                                            dialog.Send();
                                            break;
                                        }
                                    case 1:
                                        {
                                            if (client.Entity.Money >= 500)
                                            {
                                                client.Entity.AdjustMoney(-(decimal)(500));
                                                client.Entity.Teleport(1038, 348, 339);
                                                client.ActiveNpc = 0;
                                            }
                                            else
                                            {
                                                dialog.Text("You~do~not~have~enough~money~to~use~Guild~Teleporter.");
                                                dialog.Option("I~see.", 255);
                                                dialog.Avatar(3);
                                                dialog.Send();
                                            }
                                            break;
                                        }
                                }
                                return;
                            }
                            if (client.Map.Npcs.TryGetValue(client.ActiveNpc, out npc))
                            {
                                if (client.ActiveNpc != 8798 && client.ActiveNpc != 8308 &&
                                    Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17) return;
                                if (req.OptionID == 255 || (req.OptionID == 0 && ID == 2032)) return;
                                if (client.Entity.Dead) return;
                                req.NpcID = client.ActiveNpc;
                                Npcs.GetDialog(req, client);
                            }
                            if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                                client.Send(new Message("Npc ID [" + req.NpcID + "]", Color.Red, Message.TopLeft));
                            break;
                            #endregion
                        }
                        break;
                    }
                #endregion
                #region Arsenal
                case PacketID.ArsenalInscribedPage:
                    {
                        if (packet[16] == 0) packet[16] = 8;
                        client.Send(ArsenalPacket.ArsenalInscribedPage(client, packet, (Game.Features.Arsenal_ID)packet[16]));
                        break;
                    }
                case PacketID.ArsenalPackets:
                    {
                        ArsenalPacket.ArsenalPackets(packet, client);
                        break;
                    }
                case PacketID.FastInscribeItem:
                    {
                        ArsenalPacket.FastInscribeItem(packet, client);
                        break;
                    }
                #endregion
                #region Compose
                case PacketID.Compose:
                    {
                        Compose compose = new Compose(false);
                        compose.Deserialize(packet);
                        ItemHandler.ComposePlus(compose, client);
                        break;
                    }
                #endregion
                #region OfflineTG
                case PacketID.OfflineTG:
                    {
                        OfflineTGRequest.Process(packet, client);
                        break;
                    }
                #endregion
                #region TradePartner
                case PacketID.TradePartner:
                    {
                        TradePartner.Process(packet, client);
                        break;
                    }
                #endregion
                #region ItemLock
                case PacketID.ItemLock:
                    {
                        ItemLock.Process(packet, client);
                        break;
                    }
                #endregion
                #region Broadcast
                case PacketID.Broadcast:
                    {
                        Broadcast.Process(packet, client);
                        break;
                    }
                #endregion
                #region Nobility
                case PacketID.Nobility:
                    {
                        NobilityInfo nobility = new NobilityInfo(false);
                        nobility.Deserialize(packet);
                        Game.ConquerStructures.Nobility.Handle(nobility, client);
                        if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                        {
                            Data data = new Data(true);
                            data.ID = Data.OpenCustom;
                            data.UID = client.Entity.UID;
                            data.TimeStamp = Time32.Now;
                            data.dwParam = 3291;
                            client.Send(data);
                        }
                        break;
                    }
                #endregion
                #region RacePotion
                case PacketID.RacePotion:
                    {
                        RacePotion.Process(packet, client);
                        break;
                    }
                #endregion
                #region MessageBoard
                case PacketID.MessageBoard:
                    {
                        MessageBoard.Process(client, packet);
                        return;
                    }
                #endregion
                #region TopGuild
                case PacketID.TopGuild:
                    {
                        if (client.Guild != null && client.AsMember != null)
                        {
                            Game.ConquerStructures.Society.Guild.Process(packet, client);
                        }
                        break;
                    }
                #endregion
                #region GuildDoantion
                case PacketID.GuildDoantion:
                    {
                        Game.ConquerStructures.Society.GuildDoantion.Process(packet, client);
                        break;
                    }
                #endregion
                #region FairyFlower
                case PacketID.FairyFlower:
                    {
                        FairySpawn.Process(packet, client);
                        break;
                    }
                #endregion
                #region Flowers
                case PacketID.SendFlower:
                    {
                        Game.Features.Flowers.SendFlower(client, packet);
                        return;
                    }
                case PacketID.FlowersPacket:
                    {
                        Game.Features.Flowers.FlowersPacket(packet, client);
                        break;
                    }
                //case 1150:
                //    {
                //        if (packet[4] == 0)
                //        {
                //            new Revora.Game.Features.Flowers.FlowerSystem(packet, client);
                //        }
                //        if (packet[4] == 1)
                //        {
                //            new ElmistRo.Game.Features.Kisses.KissSystem(packet, client);
                //        }
                //        break;
                //    }
                //case 1151:
                //    {
                //        GenericRanking ranking = new GenericRanking(false, 0);
                //        ranking.Deserialize(packet);
                //        ElmistRo.Database.FlowerSystemTable.Load();
                //        AddFlowers(client, packet);
                //        switch (ranking.Mode)
                //        {
                //            #region Ranking
                //            case GenericRanking.Ranking:
                //                {
                //                    uint uid = BitConverter.ToUInt32(packet, 8);
                //                    ushort pagenumber = BitConverter.ToUInt16(packet, 14);
                //                    GamePackets.GenericRanking ranking1 = new GamePackets.GenericRanking(true, 10);
                //                    ranking1.Mode = 1;
                //                    ranking1.Page = pagenumber;
                //                    ranking1.RankingType = uid;
                //                    switch (uid)
                //                    {
                //                        #region Flower
                //                        case 30000002:
                //                        case 30000102:
                //                        case 30000202:
                //                        case 30000302:
                //                            {
                //                                int subtype = packet[4];

                //                                if (subtype == 1)
                //                                {
                //                                    var F = client.Entity.Flowers;
                //                                    {
                //                                        int rankss = 1;
                //                                        ElmistRo.Game.Features.Flowers.Flowers.Redrosse = (from entry in ElmistRo.Database.FlowerSystemTable.FlowerList
                //                                                                                           where entry.RedRoses > 0
                //                                                                                           orderby entry.RedRoses descending
                //                                                                                           select new ElmistRo.Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = rankss++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                                            .ToList();
                //                                        foreach (var bl in ElmistRo.Game.Features.Flowers.Flowers.Redrosse)
                //                                        {
                //                                            if (bl.name == F.name)
                //                                            {
                //                                                if (bl.rank <= 100)
                //                                                {
                //                                                    F.redroesstoday3 = true;
                //                                                }
                //                                                else
                //                                                    F.redroesstoday3 = false;
                //                                            }
                //                                        }
                //                                    }
                //                                    {
                //                                        int ranks = 1;
                //                                        ElmistRo.Game.Features.Flowers.Flowers.Orchides = (from entry in ElmistRo.Database.FlowerSystemTable.FlowerList
                //                                                                                           where entry.Orchads > 0
                //                                                                                           orderby entry.Orchads descending
                //                                                                                           select new ElmistRo.Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                                           .ToList();
                //                                        foreach (var bl in ElmistRo.Game.Features.Flowers.Flowers.Orchides)
                //                                        {
                //                                            if (bl.name == F.name)
                //                                            {
                //                                                if (bl.rank <= 100)
                //                                                {
                //                                                    F.orchadstoday3 = true;
                //                                                }
                //                                                else
                //                                                    F.orchadstoday3 = false;
                //                                            }
                //                                        }
                //                                    }
                //                                    {
                //                                        int ranks3 = 1;
                //                                        ElmistRo.Game.Features.Flowers.Flowers.Tuplise = (from entry in ElmistRo.Database.FlowerSystemTable.FlowerList
                //                                                                                          where entry.Tulips > 0
                //                                                                                          orderby entry.Tulips descending
                //                                                                                          select new ElmistRo.Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks3++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                                           .ToList();
                //                                        foreach (var bl in ElmistRo.Game.Features.Flowers.Flowers.Lilise)
                //                                        {
                //                                            if (bl.name == F.name)
                //                                            {
                //                                                if (bl.rank <= 100)
                //                                                {
                //                                                    F.liliestoday3 = true;
                //                                                }
                //                                                else
                //                                                    F.liliestoday3 = false;
                //                                            }
                //                                        }
                //                                    }
                //                                    {
                //                                        int ranks4 = 1;
                //                                        ElmistRo.Game.Features.Flowers.Flowers.Lilise = (from entry in ElmistRo.Database.FlowerSystemTable.FlowerList
                //                                                                                         where entry.Lilies > 0
                //                                                                                         orderby entry.Lilies descending
                //                                                                                         select new ElmistRo.Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks4++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                                        .ToList();
                //                                        foreach (var bl in ElmistRo.Game.Features.Flowers.Flowers.Tuplise)
                //                                        {
                //                                            if (bl.name == F.name)
                //                                            {
                //                                                if (bl.rank <= 100)
                //                                                {
                //                                                    F.tulpistoday3 = true;
                //                                                }
                //                                                else
                //                                                    F.tulpistoday3 = false;
                //                                            }
                //                                        }
                //                                    }
                //                                    if (F.tulpistoday3 || F.redroesstoday3 || F.orchadstoday3 || F.liliestoday3)
                //                                    {
                //                                        client.Send(Game.ConquerStructures.Society.FlowerRank.BuildPacketRankFlower(client, uid, pagenumber));
                //                                    }
                //                                }
                //                                break;
                //                            }
                //                        #endregion
                //                        #region Chi
                //                        case GamePackets.GenericRanking.DragonChi:
                //                            {
                //                                ShowGenericRanking(ranking1, client);
                //                                break;
                //                            }
                //                        case GamePackets.GenericRanking.TigerChi:
                //                            {
                //                                ShowGenericRanking(ranking1, client);
                //                                break;
                //                            }
                //                        case GamePackets.GenericRanking.PhoenixChi:
                //                            {
                //                                ShowGenericRanking(ranking1, client);
                //                                break;
                //                            }
                //                        case GamePackets.GenericRanking.TurtleChi:
                //                            {
                //                                ShowGenericRanking(ranking1, client);
                //                                break;
                //                            }
                //                        #endregion
                //                        #region InnerPower
                //                        case GamePackets.GenericRanking.InnerPower:
                //                            {
                //                                ushort page = ranking.Page;
                //                                Game.InnerPower[] list = null;
                //                                Func<Game.InnerPower, uint> select = null;


                //                                list = Game.InnerPower.InnerPowerRank.GetRankingList();
                //                                select = (inner) => { return inner.TotalScore; };


                //                                uint count = (uint)Math.Min(list.Length - page * 10, 10);
                //                                var nRanking = new GenericRanking(true, count);
                //                                nRanking.Mode = GenericRanking.Ranking;
                //                                nRanking.RankingType = ranking.RankingType;
                //                                nRanking.Page = page;
                //                                nRanking.RegisteredCount = (ushort)Math.Min(list.Length * 100, 100);
                //                                int rank = (int)page * 10;
                //                                for (int i = rank; i < rank + count; i++)
                //                                {
                //                                    var current = list[i];
                //                                    nRanking.Append((uint)(i + 1), (uint)select(current), current.UID, current.Name);
                //                                }
                //                                client.Send(nRanking);
                //                                break;
                //                            }
                //                        #endregion
                //                        #region Perfection
                //                        case 80000001:
                //                        case 80000002:
                //                        case 80000003:
                //                        case 80000004:
                //                        case 80000005:
                //                        case 80000006:
                //                        case 80000007:
                //                        case 80000008:
                //                        case 80000009:
                //                        case 80000010:
                //                            {
                //                                ushort page = ranking.Page;
                //                                byte realyclass = 0;
                //                                if (ranking.RankingType % 10 == 1) realyclass = 15;
                //                                if (ranking.RankingType % 10 == 2) realyclass = 25;
                //                                if (ranking.RankingType % 10 == 3) realyclass = 45;
                //                                if (ranking.RankingType % 10 == 4) realyclass = 55;
                //                                if (ranking.RankingType % 10 == 5) realyclass = 65;
                //                                if (ranking.RankingType % 10 == 6) realyclass = 75;
                //                                if (ranking.RankingType % 10 == 7) realyclass = 85;
                //                                if (ranking.RankingType % 10 == 8) realyclass = 135;
                //                                if (ranking.RankingType % 10 == 9) realyclass = 145;
                //                                if (ranking.RankingType % 10 == 10) realyclass = 165;
                //                                var array = MsgUserAbilityScore.RankingList.Where(i => i.Class >= (realyclass - 5) && i.Class <= realyclass).OrderByDescending(i => i.TotalPerfectionScore).ThenByDescending(i => i.Level).ThenBy(i => i.UID).ToArray();
                //                                uint count = (uint)Math.Min(array.Length - page * 10, 10);
                //                                var nRanking = new GenericRanking(true, count);
                //                                nRanking.Mode = Network.GamePackets.GenericRanking.Ranking;
                //                                nRanking.RankingType = ranking.RankingType;
                //                                nRanking.Page = page;
                //                                nRanking.RegisteredCount = (ushort)Math.Min(array.Length * 100, 100);
                //                                int rank = (int)page * 10;
                //                                for (int i = rank; i < rank + count; i++)
                //                                {
                //                                    var current = array[i];
                //                                    nRanking.Append2((uint)(i + 1), (uint)(current.TotalPerfectionScore), current.UID, current.Name, current.Level, current.Class, current.Mesh);
                //                                }
                //                                client.Send(nRanking);
                //                                break;
                //                            }
                //                        case 80000000:
                //                            {
                //                                var array = MsgUserAbilityScore.RankingList.OrderByDescending(i => i.TotalPerfectionScore).ThenByDescending(i => i.Level).ThenBy(i => i.UID).ToArray();

                //                                var nRanking = new GenericRanking(true);
                //                                nRanking.RegisteredCount = (ushort)Math.Min(array.Length, 100);
                //                                nRanking.Mode = 1;
                //                                nRanking.RankingType = ranking.RankingType;
                //                                nRanking.Page = ranking.Page;
                //                                //  nRanking.Count = (ushort)Math.Min(array.Length - (ranking.Page * 10), 10);
                //                                uint sss = (ushort)Math.Min(array.Length - (ranking.Page * 10), 10);

                //                                int rank = (int)nRanking.Page * 10;

                //                                for (int i = rank; i < rank + sss; i++)
                //                                {
                //                                    var current = array[i]; if (current == null) break;
                //                                    nRanking.Append2((uint)(i + 1), current.TotalPerfectionScore, current.UID, current.Name, current.Level, current.Class, current.Mesh);
                //                                }
                //                                client.Send(nRanking);
                //                                break;
                //                            }
                //                        #endregion
                //                        default: { Console.WriteLine("[1151-Ranking] UnKnown = " + uid); } break;
                //                    }
                //                    break;
                //                }
                //            #endregion
                //            #region QueryCount
                //            case GenericRanking.QueryCount:
                //                {
                //                    #region Perfection
                //                    if (ranking.RankingType == 80000000)
                //                    {
                //                        var array = MsgUserAbilityScore.RankingList.OrderByDescending(i => i.TotalPerfectionScore).ThenByDescending(i => i.Level).ThenBy(i => i.UID).ToArray();
                //                        for (int i = 0; i < array.Length; i++)
                //                        {
                //                            if (array[i].UID == client.Entity.UID)
                //                            {
                //                                Network.GamePackets.GenericRanking PacketRank = new Network.GamePackets.GenericRanking(true);
                //                                PacketRank.Mode = Network.GamePackets.GenericRanking.QueryCount;
                //                                PacketRank.RankingType = ranking.RankingType;
                //                                PacketRank.Append2((uint)(i + 1), array[i].TotalPerfectionScore, array[i].UID, array[i].Name, array[i].Level, array[i].Class, array[i].Mesh);
                //                                client.Send(PacketRank.ToArray());
                //                                break;
                //                            }
                //                        }
                //                        break;
                //                    }
                //                    if (ranking.RankingType >= 80000000 && ranking.RankingType <= 80000010)
                //                    {
                //                        byte realyclass = 0;
                //                        if (ranking.RankingType % 10 == 1) realyclass = 15;
                //                        if (ranking.RankingType % 10 == 2) realyclass = 25;
                //                        if (ranking.RankingType % 10 == 3) realyclass = 45;
                //                        if (ranking.RankingType % 10 == 4) realyclass = 55;
                //                        if (ranking.RankingType % 10 == 5) realyclass = 65;
                //                        if (ranking.RankingType % 10 == 6) realyclass = 75;
                //                        if (ranking.RankingType % 10 == 7) realyclass = 85;
                //                        if (ranking.RankingType % 10 == 8) realyclass = 135;
                //                        if (ranking.RankingType % 10 == 9) realyclass = 145;
                //                        if (ranking.RankingType % 10 == 10) realyclass = 165;
                //                        var array = MsgUserAbilityScore.RankingList.Where(i => i.Class >= (realyclass - 5) && i.Class <= realyclass).OrderByDescending(i => i.TotalPerfectionScore).ThenByDescending(i => i.Level).ThenBy(i => i.UID).ToArray();
                //                        for (int i = 0; i < array.Length; i++)
                //                        {
                //                            if (array[i].UID == client.Entity.UID)
                //                            {
                //                                Network.GamePackets.GenericRanking PacketRank = new Network.GamePackets.GenericRanking(true);
                //                                PacketRank.Mode = Network.GamePackets.GenericRanking.QueryCount;
                //                                PacketRank.RankingType = ranking.RankingType;
                //                                PacketRank.Append2((uint)(i + 1), array[i].TotalPerfectionScore, array[i].UID, array[i].Name, array[i].Level, array[i].Class, array[i].Mesh);
                //                                client.Send(PacketRank.ToArray());
                //                                break;
                //                            }

                //                        }
                //                        break;
                //                    }
                //                    #endregion
                //                    break;
                //                }
                //            #endregion
                //            #region Prestigee
                //            case 6:
                //                {
                //                    PrestigeRank.Load();
                //                    #region Best
                //                    GenericRanking Rank = new GenericRanking(true);
                //                    Rank.Mode = 6;
                //                    Rank.Count = 10;
                //                    var toper = PrestigeRank.Topers.Values.OrderByDescending(e => e.Prestige).ThenByDescending(e => e.Level).ThenBy(e => e.UID).FirstOrDefault();
                //                    Rank.Append3(1, toper.Prestige, toper.UID, toper.Name, toper.Level, toper.Class, toper.Mesh, true);
                //                    foreach (var typ in PrestigeRank.Topers.Values)
                //                    {
                //                        Rank.Append3(1, typ.Prestige, typ.UID, typ.Name, typ.Level, typ.Class, typ.Mesh);
                //                    }
                //                    client.Send(Rank);
                //                    #endregion
                //                    #region All
                //                    var index = new byte[11] { 0, 15, 25, 45, 55, 65, 75, 85, 135, 145, 165 };
                //                    Game.Entity Trojan = null;
                //                    Game.Entity Warrior = null;
                //                    Game.Entity Archer = null;
                //                    Game.Entity Ninja = null;
                //                    Game.Entity Monk = null;
                //                    Game.Entity Pirate = null;
                //                    Game.Entity Dragon = null;
                //                    Game.Entity Water = null;
                //                    Game.Entity Fire = null;
                //                    Game.Entity Windwalker = null;
                //                    for (int i = 0; i < 11; i++)
                //                    {
                //                        if (i == 1)
                //                            Trojan = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 2)
                //                            Warrior = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 3)
                //                            Archer = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 4)
                //                            Ninja = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 5)
                //                            Monk = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 6)
                //                            Pirate = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 7)
                //                            Dragon = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 8)
                //                            Water = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 9)
                //                            Fire = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                        if (i == 10)
                //                            Windwalker = MsgUserAbilityScore.RankingList.Where(x => x.Class >= (index[i] - 5) && x.Class <= index[i]).OrderByDescending(x => x.TotalPerfectionScore).ThenByDescending(x => x.Level).ThenBy(x => x.UID).FirstOrDefault();
                //                    }
                //                    Entity[] array = new Entity[10] { Trojan, Warrior, Archer, Ninja, Monk, Pirate, Dragon, Water, Fire, Windwalker };
                //                    byte count = 0;
                //                    if (Trojan != null) count++;
                //                    if (Warrior != null) count++;
                //                    if (Fire != null) count++;
                //                    if (Water != null) count++;
                //                    if (Ninja != null) count++;
                //                    if (Monk != null) count++;
                //                    if (Pirate != null) count++;
                //                    if (Dragon != null) count++;
                //                    if (Archer != null) count++;
                //                    if (Windwalker != null) count++;
                //                    var nRanking = new GenericRanking(true);
                //                    nRanking.Mode = 6;
                //                    nRanking.Page = ranking.Page;
                //                    nRanking.Count = count;
                //                    for (byte z = 0; z < array.Length; z++)
                //                    {
                //                        var Entity = array[z];
                //                        if (Entity == null) continue;
                //                        nRanking.Append2((uint)(z + 1), Entity.TotalPerfectionScore, Entity.UID, Entity.Name, Entity.Level, Entity.Class, Entity.Mesh);
                //                    }
                //                    client.Send(nRanking);
                //                    #endregion
                //                    break;
                //                }
                //            #endregion
                //                Database.FlowerSystemTable.Load();
                //                int subtypes = packet[4];
                //                AddFlowers(client, packet);
                //                uint uids = BitConverter.ToUInt32(packet, 8);
                //                ushort pagenumber2 = BitConverter.ToUInt16(packet, 14);

                //                if (subtypes == 1)
                //                {

                //                    int rankss = 1;
                //                    Game.Features.Flowers.Flowers.Redrosse = (from entry in Database.FlowerSystemTable.FlowerList
                //                                                              where entry.RedRoses > 0
                //                                                              orderby entry.RedRoses descending
                //                                                              select new Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = rankss++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                         .ToList();
                //                    int ranks = 1;
                //                    Game.Features.Flowers.Flowers.Orchides = (from entry in Database.FlowerSystemTable.FlowerList
                //                                                              where entry.Orchads > 0
                //                                                              orderby entry.Orchads descending
                //                                                              select new Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                       .ToList();
                //                    int ranks3 = 1;
                //                    Game.Features.Flowers.Flowers.Tuplise = (from entry in Database.FlowerSystemTable.FlowerList
                //                                                             where entry.Tulips > 0
                //                                                             orderby entry.Tulips descending
                //                                                             select new Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks3++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                       .ToList();
                //                    int ranks4 = 1;
                //                    Game.Features.Flowers.Flowers.Lilise = (from entry in Database.FlowerSystemTable.FlowerList
                //                                                            where entry.Lilies > 0
                //                                                            orderby entry.Lilies descending
                //                                                            select new Game.Features.Flowers.Flowers.ListFlowerRank() { name = entry.name, redrosse = entry.RedRoses, body = entry.Body, rank = ranks4++, orchides = entry.Orchads, lilise = entry.Lilies, tuplise = entry.Tulips })
                //                                    .ToList();
                //                    var F = client.Entity.Flowers;
                //                    foreach (var bl in Game.Features.Flowers.Flowers.Redrosse)
                //                    {
                //                        if (bl.name == F.name)
                //                        {
                //                            if (bl.rank <= 100)
                //                            {
                //                                F.redroesstoday3 = true;
                //                            }
                //                            else
                //                                F.redroesstoday3 = false;
                //                        }
                //                    }
                //                    foreach (var bl in Game.Features.Flowers.Flowers.Orchides)
                //                    {
                //                        if (bl.name == F.name)
                //                        {
                //                            if (bl.rank <= 100)
                //                            {
                //                                F.orchadstoday3 = true;
                //                            }
                //                            else
                //                                F.orchadstoday3 = false;
                //                        }
                //                    }
                //                    foreach (var bl in Game.Features.Flowers.Flowers.Lilise)
                //                    {
                //                        if (bl.name == F.name)
                //                        {
                //                            if (bl.rank <= 100)
                //                            {
                //                                F.liliestoday3 = true;
                //                            }
                //                            else
                //                                F.liliestoday3 = false;
                //                        }
                //                    }
                //                    foreach (var bl in Game.Features.Flowers.Flowers.Tuplise)
                //                    {
                //                        if (bl.name == F.name)
                //                        {
                //                            if (bl.rank <= 100)
                //                            {
                //                                F.tulpistoday3 = true;
                //                            }
                //                            else
                //                                F.tulpistoday3 = false;
                //                        }
                //                    }
                //                    if (F.tulpistoday3 || F.redroesstoday3 || F.orchadstoday3 || F.liliestoday3)
                //                    {
                //                        client.Send(Game.ConquerStructures.Society.FlowerRank.BuildPacketRankFlower(client, uids, pagenumber2));
                //                    }
                //                }
                //            default: { Console.WriteLine("[1151-F] UnKnown = " + ranking.Mode); } break;
                //        }
                //        break;
                //    }
                #endregion
                #region VIPTeleport
                case PacketID.VIPTeleport:
                    {
                        VIPTeleport.Process(packet, client);
                        break;
                    }
                #endregion
                #region Mentor prize
                case PacketID.MentorPrize:
                    {
                        MentorPrize.Process(packet, client);
                        break;
                    }
                #endregion
                #region MentorApprentice
                case PacketID.MentorApprentice:
                    {
                        MentorApprentice.MentorApprentices(packet, client);
                        break;
                    }
                case PacketID.MentorInformation:
                    {
                        MentorApprentice.MentorInformation(packet, client);
                        break;
                    }
                #endregion
                #region MentorPremio
                case PacketID.MentorPremio:
                    {
                        MentorPremio.Process(packet, client);
                        break;
                    }
                #endregion
                #region GuildMember
                case PacketID.GuildMember:
                    {
                        ushort Page = BitConverter.ToUInt16(packet, 8);
                        if (client.Guild != null)
                        {
                            if (client.AsMember != null)
                                client.Guild.SendMembers(client, Page);
                        }
                        break;
                    }
                #endregion

                #region Arena
                case PacketID.ArenaRank:
                    {
                        ushort PageIndex = BitConverter.ToUInt16(packet, 6);
                        Game.Qualifier.Statistics.ShowRankingPage(packet[4], PageIndex, client);
                        break;
                    }
                case PacketID.ArenaRequest:
                    {
                        ushort PageIndex = BitConverter.ToUInt16(packet, 4);
                        Game.Qualifier.QualifyEngine.RequestGroupList(client, PageIndex);
                        break;
                    }
                case PacketID.ArenaDialog:
                    {
                        uint DialogID = BitConverter.ToUInt32(packet, 4);
                        uint ButtonID = BitConverter.ToUInt32(packet, 8);
                        switch (DialogID)
                        {
                            case 4:
                                {
                                    switch (ButtonID)
                                    {
                                        case 0:
                                            {
                                                Game.Qualifier.QualifyEngine.DoQuit(client);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case 0: Game.Qualifier.QualifyEngine.DoSignup(client); client.Send(packet); break;
                            case 1: Game.Qualifier.QualifyEngine.DoQuit(client); client.Send(packet); break;
                            case 3:
                                {
                                    switch (ButtonID)
                                    {
                                        case 2: Game.Qualifier.QualifyEngine.DoGiveUp(client); break;
                                        case 1: Game.Qualifier.QualifyEngine.DoAccept(client); break;
                                    }
                                    break;
                                }
                            case 5:
                                {
                                    Revora.Game.Features.ArenaPointPurchases.Buy(client);
                                    break;
                                }
                            case 11:
                                {
                                    switch (ButtonID)
                                    {
                                        case 0: Game.Qualifier.QualifyEngine.DoSignup(client); break;
                                    }
                                    break;
                                }

                        }
                        break;
                    }
                case PacketID.ArenaShowWiners:
                    {
                        Game.Qualifier.Statistics.ShowWiners(client);
                        break;
                    }
                case PacketID.ArenaStatistic:
                    {
                        client.ArenaStatistic.Send(client);
                        break;
                    }
                case PacketID.ArenaWatch:
                    {
                        if (client.Entity.MapID == 601 || client.Entity.MapID == 3846) return;
                        if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;
                        ushort Type = BitConverter.ToUInt16(packet, 4);
                        uint Fighter = BitConverter.ToUInt32(packet, 10);
                        if (Type == 0)
                        {
                            if (!Constants.PKFreeMaps.Contains(client.Entity.MapID))
                            {
                                if (Kernel.GamePool.ContainsKey(Fighter))
                                {
                                    Client.GameState Client = Kernel.GamePool[Fighter];
                                    if (Client.InQualifier())
                                    {
                                        if (Client.QualifierGroup != null)
                                        {
                                            if (!Client.QualifierGroup.Done)
                                            {
                                                Client.QualifierGroup.BeginWatching(client);
                                            }
                                        }
                                        else if (Client.TeamQualifierGroup != null)
                                        {
                                            if (!Client.TeamQualifierGroup.Done)
                                            {
                                                Client.TeamQualifierGroup.BeginWatching(client);
                                            }
                                        }
                                    }
                                    else if (Client.Team != null)
                                    {
                                        if (Client.Team.EliteMatch != null)
                                        {
                                            if (Client.Team.EliteMatch.OnGoing)
                                            {
                                                Client.Team.EliteMatch.BeginWatch(client);
                                            }
                                        }
                                    }
                                    else if (Client.ElitePKMatch != null)
                                    {
                                        if (Client.ElitePKMatch.OnGoing)
                                        {
                                            Client.ElitePKMatch.BeginWatch(client);
                                        }
                                    }
                                }
                            }
                        }
                        else if (Type == 1)
                        {
                            if (client.WatchingElitePKMatch != null)
                            {
                                client.WatchingElitePKMatch.LeaveWatch(client);
                            }
                            else if (client.WatchingGroup != null)
                            {
                                Qualifier.QualifyEngine.DoLeave(client);
                            }
                            else if (client.TeamWatchingGroup != null)
                            {
                                TeamQualifier.QualifyEngine.DoLeave(client);
                            }
                            else if (client.Entity.SkillTeamWatchingElitePKMatch != null)
                            {
                                client.Entity.SkillTeamWatchingElitePKMatch.LeaveWatch(client);
                            }
                        }
                        else if (Type == 4)
                        {
                            uint fighter = BitConverter.ToUInt32(packet, 10);
                            if (client.WatchingElitePKMatch != null)
                            {
                                client.WatchingElitePKMatch.Cheer(client, fighter);
                            }
                            else if (client.WatchingGroup != null)
                            {
                                Qualifier.QualifyEngine.DoCheer(client, fighter);
                            }
                            else if (client.TeamWatchingGroup != null)
                            {
                                TeamQualifier.QualifyEngine.DoCheer(client, fighter);
                            }
                            else if (client.Entity.SkillTeamWatchingElitePKMatch != null)
                            {
                                client.Entity.SkillTeamWatchingElitePKMatch.Cheer(client, fighter);
                            }
                        }
                        break;
                    }
                #endregion
                #region TeamArena
                case PacketID.TeamArenaRank:
                    {
                        ushort PageIndex = BitConverter.ToUInt16(packet, 6);
                        Game.TeamQualifier.Statistics.ShowRankingPage(packet[4], packet[4], client);
                        break;
                    }
                case PacketID.TeamArenaRequest:
                    {
                        ushort PageIndex = BitConverter.ToUInt16(packet, 4);
                        Game.TeamQualifier.QualifyEngine.RequestGroupList(client, PageIndex);
                        break;
                    }
                case PacketID.TeamArenaDialog:
                    {
                        uint DialogID = BitConverter.ToUInt32(packet, 4);
                        uint ButtonID = BitConverter.ToUInt32(packet, 8);
                        switch (DialogID)
                        {
                            case 4:
                                {
                                    switch (ButtonID)
                                    {
                                        case 0:
                                            {
                                                Game.TeamQualifier.QualifyEngine.DoQuit(client);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case 0: Game.TeamQualifier.QualifyEngine.DoSignup(client); client.Send(packet); break;
                            case 1: Game.TeamQualifier.QualifyEngine.DoQuit(client); client.Send(packet); break;
                            case 3:
                                {
                                    switch (ButtonID)
                                    {
                                        case 2: Game.TeamQualifier.QualifyEngine.DoGiveUp(client); break;
                                        case 1: Game.TeamQualifier.QualifyEngine.DoAccept(client); break;
                                    }
                                    break;
                                }
                            case 5:
                                {
                                    Revora.Game.Features.ArenaPointPurchases.Buy(client);
                                    break;
                                }
                            case 11:
                                {
                                    switch (ButtonID)
                                    {
                                        case 0: Game.TeamQualifier.QualifyEngine.DoSignup(client); break;
                                    }
                                    break;
                                }

                        }
                        break;
                    }
                case PacketID.TeamArenaShowWiners:
                    {
                        Game.TeamQualifier.Statistics.ShowWiners(client);
                        break;
                    }
                case PacketID.TeamArenaStatistic:
                    {
                        client.TeamArenaStatistic.Send(client);
                        break;
                    }
                case PacketID.InTeamArena:
                    {
                        if (client.Entity.MapID == 601) return;
                        if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;
                        ushort Type = BitConverter.ToUInt16(packet, 4);
                        uint Fighter = BitConverter.ToUInt32(packet, 8);
                        if (Type == 0)
                        {
                            if (Kernel.GamePool.ContainsKey(Fighter))
                            {
                                Client.GameState Client = Kernel.GamePool[Fighter];
                                if (Client.InTeamQualifier())
                                {
                                    Game.TeamQualifier.QualifierList.QualifierParticipants particiaptants = new Game.TeamQualifier.QualifierList.QualifierParticipants(Client, Game.TeamQualifier.QualifierList.QualifierParticipants.KindOfParticipants.Neutral);
                                    client.Send(particiaptants.Build(client.Entity.UID));
                                }
                            }
                        }
                        break;
                    }
                #endregion
                #region ChampionArena
                // Unconfirmed legacy control/stat request layouts remain disabled.
                // Revora's restored event is operated through ordinary NPC dialogue.
                case PacketID.ChampionArenaRank:
                case PacketID.ChampionArenaStats:
                case PacketID.ChampionArenaShowTop:
                case PacketID.ChampionArenaShowTop2:
                case PacketID.ChampionArenaHandle:
                    break;
                #endregion
                #region ElitePk
                case PacketID.ElitePkRank:
                case PacketID.ElitePKBrackets:
                case PacketID.ElitePKWager:
                case PacketID.ElitePKWagersList:
                    // Native tournament UI and wagering are not enabled for the restored edition.
                    break;
                case PacketID.ElitePKTitle:
                    {
                        if (packet.Length < 12 || !TitleRequirements.AllowRequest(client)) break;
                        TitleRequirements.Reconcile(client, false);
                        try
                        {
                            TitlePacket tpacket = new TitlePacket(false);
                            tpacket.Deserialize(packet);
                            switch (tpacket.Type)
                            {
                                case TitlePacket.Types.List:
                                    {
                                        tpacket.Title = client.Entity.MyTitle;
                                        if (client.Entity.Titles.Count > 0)
                                        {
                                            tpacket.Count = (Byte)client.Entity.Titles.Count();
                                            foreach (var title in client.Entity.Titles)
                                                tpacket.Add((Byte)title.Key);
                                        }
                                        client.Send(tpacket);
                                        break;
                                    }
                                case TitlePacket.Types.Switch:
                                    {
                                        if (client.Entity.Titles.Keys.Contains(tpacket.Title) || tpacket.Title == TitlePacket.Titles.None)
                                        {
                                            client.Entity.MyTitle = tpacket.Title;
                                            client.Send(tpacket);
                                            client.Entity.Teleport(client.Entity.MapID, client.Entity.X, client.Entity.Y);
                                        }
                                        break;
                                    }
                            }
                        }
                        catch (Exception x)
                        {
                            Console.WriteLine(x.ToString());
                        }
                        break;
                    }
                #endregion
                #region TeamPk
                case PacketID.TeamPkBrackets:
                    {
                        TeamElitePkBrackets.TeamPk(packet, client);
                        break;
                    }
                case PacketID.TeamPkTournament:
                    {
                        client.Send(packet);
                        Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments[packet[8]].Rankings(client);
                        break;
                    }
                case PacketID.TeamPkFighterStats:
                    {
                        string TeamName = GeneralData.ReadString(packet, 12, 32);
                        byte Mode = packet[4];
                        if (client.Team != null)
                        {
                            if (client.Team.EliteFighterStats != null)
                            {
                                client.Team.EliteFighterStats.Name = TeamName;

                                client.Send(packet);
                            }
                        }
                        break;
                    }
                #endregion
                #region SkillTeamPk
                case PacketID.SkillTeamPkBrackets:
                    {
                        TeamElitePkBrackets.SkillTeamPk(packet, client);
                        break;
                    }
                case PacketID.SkillTeamPkTournament:
                    {
                        client.Send(packet);
                        Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Tournaments[packet[8]].Rankings(client);
                        break;
                    }
                case PacketID.SkillTeamPkFighterStats:
                    {
                        string TeamName = GeneralData.ReadString(packet, 12, 32);
                        byte Mode = packet[4];
                        if (client.Team != null)
                        {
                            if (client.Team.EliteFighterStats != null)
                            {
                                client.Team.EliteFighterStats.Name = TeamName;

                                client.Send(packet);
                            }
                        }
                        break;
                    }
                #endregion
                #region CaptureTheFlag
                //case PacketID.CaptureTheFlag:
                //    {
                //        CaptureTheFlag.Process(packet, client);
                //        break;
                //    }
                //case PacketID.CrossServer:
                //    {
                //        CrossServer.Process(packet, client);
                //        break;
                //    }
                #endregion
                #region RuedaDeSuerte
                case PacketID.RuedaDeSuerte: // If you do not have MsgTypes, change this to PacketID; if you have neither, use case 2811:
                    {
                        RuedaDeSuerte.Proceso(packet, client);
                        break;
                    }
                #endregion
                #region Chi
                case 2533:
                    {
                        if (Length < 16) return;
                        Chi chi = new Chi(false);
                        chi.Deserialize(packet);
                        switch (chi.Type)
                        {
                            case Chi.Unlock:
                                GeneralData.UnlockChi(chi, client);
                                break;
                            case Chi.QueryInfo:
                                GeneralData.QueryChi(chi, client);
                                break;
                            case Chi.Study:
                                GeneralData.HandleChiStudy(chi, client);
                                break;
                            case Chi.BuyStrength:
                                GeneralData.BuyStrengthChi(chi, client);
                                break;
                        }
                        break;
                    }
                #endregion
                #region GroundMovement
                case PacketID.GroundMovement:
                    {
                        GroundMovement.Process(packet, client);
                        break;
                    }
                #endregion
                #region Reincarnation
                case PacketID.Reincarnation:
                    {
                        Reincarnation.Process(packet, client);
                        break;
                    }
                #endregion
                #region PurifyItem
                case PacketID.PurifyItem:
                    {
                        Purification.Process(packet, client);
                        break;
                    }
                #endregion
                #region Clan
                case PacketID.Clan:
                    {
                        Clan.Process(packet, client);
                        break;
                    }
                #endregion
                #region SubClass
                case PacketID.SubClass:
                    {
                        GamePackets.SendSubClass.Process(packet, client);
                        break;
                    }
                #endregion
                #region ChangeName (2080)
                case 0x820:
                    Network.GamePackets.NameChange change = new Network.GamePackets.NameChange(false);
                    change.Deserialize(packet);
                    if (change.Action == Network.GamePackets.NameChange.NameChangeAction.Request)
                    {
                        string name = Encoding.Default.GetString(packet, 10, 0x10).TrimEnd(new char[1]);
                        packet[6] = (byte)client.namechanges;
                        packet[8] = (byte)(30 - client.namechanges);
                        change.Send(client);
                        client.Send(change);
                        if ((name != "") && (name != ""))
                        {
                            if (!Npcs.InvalidCharacters(name) || !Npcs.InvalidCharacters3(name) || name.Contains("[") || name.Contains("]")
                                || name.Contains(")") || name.Contains("(") || name.Contains("GM") || name.Contains("ADMI")
                                || name.Contains("PM") || name.Contains("ayudante") || name.Contains("chavarro") || name.Contains("ADM")
                                || name.Contains("ADMI") || name.Contains(" ") || name.Contains("_") || name.Contains("Ayud")
                                )
                            {
                                reply = new NpcReply(6, "Invalid characters! You cannot use this name!");
                                reply.OptionID = 0xff;
                                client.Send(reply.ToArray());
                                return;
                            }
                            Database.MySqlReader reader = new Database.MySqlReader(new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("entities").Where("name", name));
                            if (reader.Read())
                            {
                                packet[4] = 2;
                                change.Send(client);
                                client.Send(change);
                                return;
                            }
                            if (client.Entity.ConquerPoints >= Revora.Config.NameChangeCp)
                            {
                                client.Entity.TrySpendConquerPoints((uint)(Revora.Config.NameChangeCp));
                                //    Database.MySqlCommand command3 = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE);
                                //   command3.Update("entities").Set("namechange", name).Where("UID", (long)client.Player.UID).Execute();                               
                                client.NewName = name;
                                // Console.WriteLine("(" + client.Player.Name + ") changed name to: (" + name + ")");
                                Server.CambiarNombreRegistro(client.Entity.Name, " Cambio Su Nombre A " + name + " Fecha " + System.DateTime.Now.ToString());
                                Kernel.SendWorldMessage(new Network.GamePackets.Message(client.Entity.Name + " has changed their name to " + name + ". It will take effect after maintenance.", System.Drawing.Color.Red, 0x7d0), Server.GamePool);
                                client.namechanges += 1;
                                packet[4] = 1;
                                change.Send(client);
                                client.Send(change);
                                reply = new NpcReply(6, "Your new name is: " + name + ". You will receive it after the server restarts.")
                                {
                                    OptionID = 0xff
                                };
                                client.Send(reply.ToArray());
                                client.ChangeName(client);

                            }
                            else
                            {
                                reply = new NpcReply(6, "Return when you have 500.000.000 CPs.")
                                {
                                    OptionID = 0xff
                                };
                                client.Send(reply.ToArray());
                            }
                        }
                    }
                    return;
                #endregion ChangeName (2080)

                #region ItemAdding
                case PacketID.ItemAdding:
                    {
                        ItemAddingStabilization.Process(packet, client);
                        break;
                    }
                #endregion
                #region Data
                case PacketID.GeneralData:
                    {
                        Data.Process(packet, client);
                        break;
                    }
                #endregion
                #region TimePacket
                case PacketID.TimePacket:
                    {
                        ServerTime.Process(packet, client);
                        break;
                    }
                #endregion
                #region PkExplorer
                //case PacketID.PkExplorer:
                //    {
                //        PkExploits.Process(packet, client);
                //        break;
                //    }
                #endregion
                #region SecondaryPassword
                case PacketID.SecondaryPassword:
                    {
                        SecondaryPassword.Process(packet, client);
                        break;
                    }
                #endregion
                #region AutoInvite
                case PacketID.AutoInvite:
                    {
                        AutoInvite.Process(packet, client);
                        break;
                    }
                #endregion
                #region MemoryAgate
                case PacketID.MemoryAgate:
                    {
                        MemoryAgate.Process(packet, client);
                        break;
                    }
                #endregion
                #region Achievement
                case PacketID.Achievement:
                    {
                        Achievement.Process(packet, client);
                        break;
                    }
                #endregion
                #region RewardEvent
                case PacketID.RewardEvent:
                    {
                        RewardEvent.Process(packet, client);
                        break;
                    }
                #endregion
                #region Mailbox
                case PacketID.MailboxCheck:
                    {
                        if (packet.Length < 20 || packet.Length > 1024 || System.BitConverter.ToUInt16(packet, 0) != packet.Length - 8) break;
                        uint page = BitConverter.ReadUint(packet, 8);
                        Mailbox inbox = new Mailbox();
                        inbox.Check(client, page);
                        inbox.Send(client);
                        break;
                    }
                case PacketID.MailboxPage:
                    {
                        Mailbox.Process(packet, client);
                        break;
                    }
                #endregion
                #region ReloadScreen
                case PacketID.ReloadScreen:
                    {
                        client.SendScreen(packet, true);
                        client.Screen.Reload(null);
                        break;
                    }
                #endregion
                default:
                    if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                        client.Send(new Message("Unknown type: " + ID + " with length " + packet.Length, Message.Talk)); break;
            }
        }

        public static void PrintPacket(byte[] packet)
        {
            ushort Length = BitConverter.ToUInt16(packet, 0);
            ushort ID = BitConverter.ToUInt16(packet, 2);
            System.Console.Write("Packet ID : [{0}] , Lenght : [{1}] \n\n", ID, Length);
            foreach (byte D in packet)
            {
                System.Console.Write((Convert.ToString(D, 16)).PadLeft(2, '0') + " ");
            }
            System.Console.Write("\n\n");
        }

   
    }
}
