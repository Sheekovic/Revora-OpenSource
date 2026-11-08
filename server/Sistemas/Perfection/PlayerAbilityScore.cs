using Revora.Client;
using ProtoBuf;
using System;
using System.IO;
using System.Linq;

namespace Revora.Network.GamePackets
{
    public class CMsgUserAbilityScore
    {
        private byte[] _packet;
        public CMsgUserAbilityScore() { }
        public CMsgUserAbilityScore(byte[] payload)
        {
            _packet = payload;
        }
        public CMsgUserAbilityScore(GameState client)
        {
            Data = Calculate(PrestigeProfile.FromClient(client));
            _packet = FinalizeProtoBuf(Data);
        }
        public CMsgUserAbilityScore(UserAbilityScoreProto data)
        {
            Data = data;
            _packet = FinalizeProtoBuf(data);
        }
        public static UserAbilityScoreProto Calculate(PrestigeProfile profile)
        {
            UserAbilityScoreProto abilitiesScore;
            abilitiesScore = new UserAbilityScoreProto();
            abilitiesScore.Id = profile.UID;
            abilitiesScore.Level = profile.Level;
            var abilitesSort = System.Enum.GetValues(typeof(AbilitiesSort));
            abilitiesScore.Abilities = new Ability[abilitesSort.Length];

            foreach (AbilitiesSort abilitySort in abilitesSort)
            {
                abilitiesScore.Abilities[abilitySort.Value() - 1] = new Ability();
                abilitiesScore.Abilities[abilitySort.Value() - 1].Sort = abilitySort;

                switch (abilitySort)
                {
                    case AbilitiesSort.RedRune:
                    case AbilitiesSort.BlueRune:
                    case AbilitiesSort.YellowRune:
                        uint runeType=(uint)abilitySort+380;
                        abilitiesScore.Abilities[abilitySort.Value()-1].Value=RuneProgress.LegalRunes(profile.Runes,profile.Class,profile.Level,profile.Reborn)
                            .Where(r=>r.ID/10000==runeType && RuneProgress.Equipped(r))
                            .GroupBy(r=>r.Position).Sum(slot=>slot.Max(RuneProgress.Prestige));
                        break;
                    case AbilitiesSort.Jiang:
                        if (profile.Jiang == null) break;
                        var score = (int)profile.Jiang.Inner_Strength;
                        if (score <= 0) break;
                        var abilityScore = PerfectionManager.GetAbilityScore(abilitySort, score);
                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value = (int)(((decimal)score / 100) * abilityScore.Score);
                        break;
                    case AbilitiesSort.Chi:
                        if (profile.Chi == null)
                            break;
                        var chiScores = new int[] { profile.Chi.DragonPoints, profile.Chi.PhoenixPoints, profile.Chi.TigerPoints, profile.Chi.TurtlePoints };
                        foreach (var chiScore in chiScores.Where(v => v > 0))
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, chiScore);
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value += (int)(((decimal)chiScore / 100) * abilityScore.Score);
                        }
                        break;
                    case AbilitiesSort.InnerPower:
                        var innerScores = new System.Collections.Generic.List<int>();
                        if (profile.InnerPower == null)
                            break;
                        foreach (var stage in profile.InnerPower.Stages)
                        {
                            if (stage != null && stage.UnLocked && stage.Score > 0)
                                innerScores.Add((int)stage.Score);
                        }
                        if (innerScores.Count > 0)
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, innerScores.Sum());
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value = (int)(((decimal)innerScores.Sum() / 100) * abilityScore.Score);
                        }
                        break;
                    case AbilitiesSort.Enchant:
                        foreach (var item in profile.Items)
                        {
                            if (item.Enchant > 0)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, item.Enchant);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += (int)(((decimal)item.Enchant / 100) * abilityScore.Score);
                            }
                        }
                        break;
                    case AbilitiesSort.Level:
                        var val = profile.Level > 140 ? 140 : profile.Level;
                        abilityScore = PerfectionManager.GetAbilityScore(abilitySort, val);
                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value = (int)(((decimal)val / 100) * abilityScore.Score);
                        break;
                    case AbilitiesSort.PerfectionLevel:
                        foreach (var item in profile.Items)
                        {
                            if (item.PerfectionLevel > 0)
                            {
                                val = (int)(item.PerfectionLevel >= 28 ? 27 : item.PerfectionLevel);
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, val);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += (int)(((decimal)val / 100) * abilityScore.Score);
                                if (item.PerfectionLevel >= 28)
                                {
                                    var index = 28;
                                    while (index <= item.PerfectionLevel)
                                    {
                                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value += 100;
                                        index++;
                                    }
                                }
                            }
                        }
                        break;
                    case AbilitiesSort.Attributes:
                        var attributes = profile.Attributes;
                        if (attributes > 0 && attributes < 539)
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, attributes);
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value = (int)(((decimal)attributes / 100) * abilityScore.Score);
                        }
                        break;
                    case AbilitiesSort.Rebirth:
                        if (profile.Reborn == 0) break;
                        abilityScore = PerfectionManager.GetAbilityScore(abilitySort, profile.Reborn);
                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value = abilityScore.Score;
                        break;
                    case AbilitiesSort.Quality:
                        foreach (var item in profile.Items)
                        {
                            if (item.Position != ConquerItem.SteedArmor && item.Position != ConquerItem.Garment
                                && item.Position != ConquerItem.Bottle && item.Position != ConquerItem.RightWeaponAccessory &&
                                item.Position != ConquerItem.LeftWeaponAccessory && item.Position != ConquerItem.AlternateGarment
                                && item.Position != ConquerItem.AlternateBottle)
                            {
                                if (item.ID % 10 > 0)
                                {
                                    abilityScore = PerfectionManager.GetAbilityScore(abilitySort, (int)(item.ID % 10));
                                    abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                                }
                            }
                        }
                        break;
                    case AbilitiesSort.Sockets:
                        foreach (var item in profile.Items)
                        {
                            var count = item.SocketsCount();
                            if (count != 0)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, count);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                            }
                        }
                        break;
                    case AbilitiesSort.GemQuality:
                        foreach (var item in profile.Items)
                        {
                            if (item.SocketsCount() > 0)
                            {
                                var gemsQuality = item.GemsQuality();
                                if (gemsQuality.Length > 0)
                                {
                                    foreach (var gemQuality in gemsQuality)
                                    {
                                        abilityScore = PerfectionManager.GetAbilityScore(abilitySort, gemQuality);
                                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                                    }
                                }
                            }
                        }
                        break;
                    case AbilitiesSort.Plus:
                        foreach (var item in profile.Items)
                        {
                            if (item.Plus > 0)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, item.Plus);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                            }
                        }
                        break;
                    case AbilitiesSort.SoulPhase:
                        foreach (var item in profile.Items)
                        {
                            if (item.Purification.PurificationLevel > 0)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, (int)item.Purification.PurificationLevel);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                            }
                        }
                        break;
                    case AbilitiesSort.MaterialPhase:
                        foreach (var item in profile.Items)
                        {
                            if (item.RefineItem > 0)
                            {
                                var refine = Kernel.DatabaseRefinery[item.RefineItem];
                                if (refine.Level == 0) continue;
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, refine.Level);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                            }
                        }
                        break;
                    case AbilitiesSort.Protection:
                        foreach (var item in profile.Items)
                        {
                            if (item.Bless > 0 && item.Bless < 8)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, item.Bless);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += abilityScore.Score;
                            }
                        }
                        break;
                    case AbilitiesSort.SubClass:
                        var subClasses = profile.SubclassCount;
                        if (subClasses > 0)
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, subClasses);
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value = abilityScore.Score;
                        }
                        break;
                    case AbilitiesSort.Garments:
                        if (profile.Wardrobe == null) break;
                        var garmentsCount = profile.Wardrobe.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Garment).Count();
                        if (garmentsCount > 0)
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, garmentsCount);
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value = abilityScore.Score;
                        }
                        break;
                    case AbilitiesSort.MountArmors:
                        if (profile.Wardrobe == null) break;
                        var mountsOverlayCount = profile.Wardrobe.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.SteedArmor).Count();
                        if (mountsOverlayCount > 0)
                        {
                            abilityScore = PerfectionManager.GetAbilityScore(abilitySort, mountsOverlayCount);
                            abilitiesScore.Abilities[abilitySort.Value() - 1].Value = abilityScore.Score;
                        }
                        break;
                    case AbilitiesSort.NobilityRank:
                        var rankType = (int)profile.NobilityRank;
                        if (rankType == 0) break;
                        abilityScore = PerfectionManager.GetAbilityScore(abilitySort, rankType);
                        abilitiesScore.Abilities[abilitySort.Value() - 1].Value = abilityScore.Score;
                        break;
                    case AbilitiesSort.ItemLevel:
                        foreach (var item in profile.Items)
                        {
                            var dbInfo = Database.ConquerItemInformation.BaseInformations[item.ID];
                            if (dbInfo.Level > 0)
                            {
                                abilityScore = PerfectionManager.GetAbilityScore(abilitySort, dbInfo.Level);
                                abilitiesScore.Abilities[abilitySort.Value() - 1].Value += (int)(((decimal)dbInfo.Level / 100) * abilityScore.Score);
                            }
                        }
                        break;
                }
            }
            return abilitiesScore;
        }
        public static int CalculatePerfectionChiPoints(Client.GameState client)
        {
            if (client.ChiData == null)
                return 0;

            int Point = 0;
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 1) break;
                Point += ((client.ChiData.Powers[0].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[0].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 2) break;
                Point += ((client.ChiData.Powers[1].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[1].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 3) break;
                Point += ((client.ChiData.Powers[2].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[2].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 4) break;
                Point += ((client.ChiData.Powers[3].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[3].Attributes[i].Type)) * 100) * 60;
            }
            return Point;
        }
        public static int CalculatePerfectionJiangPoints(Client.GameState client)
        {
            int Points = 0;
            if (client.Entity.MyJiang != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (client.Entity.MyJiang.Stagers[i] == null || !client.Entity.MyJiang.Stagers[i].Activate) break;
                    foreach (var star in client.Entity.MyJiang.Stagers[i].Stars)
                    {
                        if (star.Activate)
                            Points += star.Level * 100;
                    }
                }
            }
            return Points;
        }
        public bool Read()
        {
            if (_packet == null || _packet.Length < 14 || _packet.Length > 256
                || BitConverter.ToUInt16(_packet, 0) != _packet.Length - 8
                || BitConverter.ToUInt16(_packet, 2) != 3253) return false;
            try
            {
                using (var stream = new MemoryStream(_packet, 4, _packet.Length - 12))
                    Data = Serializer.Deserialize<UserAbilityScoreProto>(stream);
                return Data != null && Data.Type == UserAbilityScoreProto.Action.RequestAbility
                    && Data.Id != 0 && (Data.Abilities == null || Data.Abilities.Length <= 23);
            }
            catch (ProtoException) { return false; }
            catch (IOException) { return false; }
            catch (OverflowException) { return false; }
        }
        public Revora.PerfectionManager.MsgUserAbilityScoreProto Info;
        public void Handle(GameState client)
      {
          try
          {
          if (Data == null || client == null) return;
          if (!PrestigeRanking.AllowQuery(client)) return;
          switch (Data.Type)
          {
              case UserAbilityScoreProto.Action.RequestAbility:
                  var all = PrestigeRanking.Snapshot(client);
                  var viewed = all.FirstOrDefault(e => e.Profile.UID == Data.Id);
                  if (viewed != null) client.Send(new CMsgUserAbilityScore(viewed.Abilities));
                  break;
                  }
          }
          catch (Exception ex)
          {
              Console.WriteLine("[game] ability-score " + ex.Message);
          }
      }
     /*   public void Handle(Client.GameState client)
        {
            switch (Info.ActionId)
            {
                case 1://Observ
                    {
                        if (Kernel.GamePool.ContainsKey(Info.UID))
                        {
                            SendScore(client, client);
                            SendScore(Kernel.GamePool[Info.UID], client);
                        }
                        else
                        {
                            client.Send(Kernel.FinalizeProtoBuf(new OverDrive.PerfectionManager.MsgUserAbilityScoreProto()
                            {
                                ActionId = 1,
                                UID = 0
                            }, 3253));
                        }
                        break;
                    }
            }
        }*/
        public void SendScore(Client.GameState client, Client.GameState Observer)
        {
            var packet = new Revora.PerfectionManager.MsgUserAbilityScoreProto();
            packet.ActionId = 0;
            packet.UID = client.Entity.UID;
            packet.Level = client.Entity.Level;
            packet.Scores = new Revora.PerfectionManager.Scores[23];
            for (int i = 0; i < packet.Scores.Length; i++)
            {
                byte Type = (byte)(i + 1);
                packet.Scores[i] = new Revora.PerfectionManager.Scores();
                packet.Scores[i].Type = Type;
                packet.Scores[i].Score = GetScoreValue(client, Type);
            }


            var proto = Kernel.FinalizeProtoBuf(packet, 3253);
            Observer.Send(proto);
        }
        public uint GetScoreValue(Client.GameState client, uint Type)
        {
            var score = Calculate(PrestigeProfile.FromClient(client)).Abilities.FirstOrDefault(a => (uint)a.Sort == Type);
            return score == null ? 0u : checked((uint)score.Value);
        }
        public static uint CalculateSubClassPoints(Client.GameState client)
        {
            uint Points = 0;
            return Points;
        }
    
        public UserAbilityScoreProto Data;
        private static byte[] FinalizeProtoBuf(UserAbilityScoreProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3253, 2, pkt);
                return pkt;
            }
        }
        public static implicit operator byte[](CMsgUserAbilityScore p)
        {
            return p._packet;
        }
    }
    [ProtoContract]
    public class UserAbilityScoreProto
    {
        public enum Action
        {
            ReponseWithAbility,
            RequestAbility
        }
        [ProtoMember(1, IsRequired = true)]
        public Action Type;
        [ProtoMember(2, IsRequired = true)]
        public uint Id;
        [ProtoMember(3, IsRequired = true)]
        public int Level;
        [ProtoMember(4, IsRequired = true)]
        public Ability[] Abilities;
    }
    [ProtoContract]
    public class Ability
    {
        [ProtoMember(1, IsRequired = true)]
        public AbilitiesSort Sort;
        [ProtoMember(2, IsRequired = true)]
        public int Value;
    }
}
