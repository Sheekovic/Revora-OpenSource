// * Programador C++ Chavarro
// * Copyright © 2014 - 2016
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.IO;
using Revora.Client;
using Revora.Network;
using Revora.Database;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Revora.Interfaces;

namespace Revora.Game
{
    public class JiangHu : IJiangHu
    {
        public static ConcurrentDictionary<uint, IJiangHu> JiangHuClients = new ConcurrentDictionary<uint, IJiangHu>();
        public System.Random Random = new System.Random();
        public byte JiangSetting = 0;
        public DateTime RemoveJiangMod;
        public object sync = new object();
        internal string SavedData;
        internal bool HasSavedData, SaveBlocked;
        public DateTime TimerStamp;
        public enum AttackFlag
        {
            None = 0,
            NotHitFriends = 1,
            NotHitClanMembers = 2,
            NotHitGuildMembers = 4,
            NotHitAlliedGuild = 8,
            NoHitAlliesClan = 16
        }
        public JiangHu(uint m_UID)
        {
            Talent = 1;
            Stage = 1;
            Star = 1;
            FreeCourse = 10000;
            FreeTimeTodey = 10;
            MyOldStar = null;
            UID = m_UID;
            DateTime time = new DateTime();
            CountDownEnd = time;
            StartCountDwon = new DateTime();
            TimerStamp = new DateTime();
            RemoveJiangMod = new DateTime();
            Stagers = new JiangStages[9];
            for (byte i = 0; i < 9; i = (byte)(i + 1))
            {
                Stagers[i] = new JiangStages();
            }
            if (!(JiangHuClients.ContainsKey(UID) || (UID == 0)))
            {
                JiangHuClients.TryAdd(UID, this);
                using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
                {
                    cmd.Select("JiangHu").Where("uid", UID);
                    using (MySqlReader rdr = new MySqlReader(cmd))
                    {
                        if (!rdr.Read())
                        {
                            using (var command = new MySqlCommand(MySqlCommandType.INSERT))
                            {
                                command.Insert("JiangHu").Insert("uid", UID);
                                command.Execute();
                            }
                            rdr.Close();
                        }
                    }
                }
            }
            if (UID != 0) Database.JiangHu.CaptureBaseline(this);
            OnJiangHu = true;
            TimerStamp = DateTime.Now;
        }
        public static bool AllowNameCaracters(string Name)
        {
            if (Name.Contains('['))
                return false;
            if (Name.Contains(']'))
                return false;
            if (Name.Contains("GM"))
                return false;
            if (Name.Contains("PM"))
                return false;
            if (Name.Contains("!") || Name.Contains("@") || Name.Contains("#"))
                return false;
            if (Name.Contains("$") || Name.Contains("%") || Name.Contains("^"))
                return false;
            if (Name.Contains("&") || Name.Contains("*") || Name.Contains("/") || Name.Contains("|"))
                return false;
            return true;
        }
        private void RecalculateInnerStrength()
        {
            Inner_Strength = 0;
            foreach (JiangStages stages in Stagers)
            {
                if (stages.Activate)
                {
                    stages.Aligments = new List<JiangStages.Aligment>();
                    for (int i = 0; i < stages.Stars.Length; i++)
                    {
                        var star = stages.Stars[i];
                        if (star == null) continue;
                        if (star.UID == 0 || !star.Activate) continue;
                        var alignment = new JiangStages.Aligment { Stars = new List<JiangStages.Star> { star } };
                        int next = i + 1;
                        while (next < stages.Stars.Length)
                        {
                            var adjacent = stages.Stars[next];
                            if (adjacent == null || adjacent.UID == 0 || !adjacent.Activate || adjacent.Typ != star.Typ)
                                break;
                            alignment.Stars.Add(adjacent);
                            next++;
                        }
                        if (alignment.count >= 2) stages.Aligments.Add(alignment);
                        uint score = 0;
                        foreach (var member in alignment.Stars) score += Database.JiangHu.GetStatusPoints(member.Level);
                        Inner_Strength += score + score * (uint)(GetPercentALignment(alignment.count) * 10) / 100;

                        i = next - 1;
                    }
                }
            }
        }
        public void CreateStatusAtributes(Entity client)
        {
            RecalculateInnerStrength();
            if (client.Owner != null && client.Owner.ChampionGroup != null) return;
            foreach (JiangStages stages in Stagers)
            {
                if (stages.Activate)
                {
                    foreach (Revora.Game.JiangHu.JiangStages.Star star in stages.Stars)
                    {
                        if (star != null && star.Activate && (star.UID != 0))
                        {
                            Revora.Database.JiangHu.Atribut atribut = Database.JiangHu.Attributes[star.UID];
                            IncreaseStatus(client, (JiangStages.AtributesType)atribut.Type, atribut.Power);
                        }
                    }
                    foreach (var aligment in stages.Aligments)
                    {
                        foreach (var star in aligment.Stars)
                        {
                            Revora.Database.JiangHu.Atribut atribut = Database.JiangHu.Attributes[star.UID];
                            ushort power = (ushort)Math.Floor((double)(atribut.Power * aligment.getpower() / 100));
                            IncreaseStatus(client, (JiangStages.AtributesType)atribut.Type, power);
                        }
                    }
                }
            }
        }
        internal byte GetPercentALignment(int count)
        {
            if (count < 2) return 0;
            if (count < 8)
                return (byte)(count - 1);
            if (count == 8)
                return (byte)count;
            else
                return (byte)(count + 1);
        }
        public void CreateTime()
        {
            StartCountDwon = DateTime.Now;
            CountDownEnd = DateTime.Now.AddMinutes((double)Revora.Database.JiangHu.GetMinutesOnTalent(Talent));
        }
        public void DecreaseStatus(Entity client, JiangStages.AtributesType status, ushort Power)
        {
            if (client.Owner != null && client.Owner.ChampionGroup != null) return;
            switch (status)
            {
                case JiangStages.AtributesType.MaxLife:
                    client.MaxHitpoints -= Power;
                    break;

                case JiangStages.AtributesType.PAttack:
                    client.MaxAttack -= Power;
                    client.MinAttack -= Power;
                    break;

                case JiangStages.AtributesType.MAttack:
                    client.MagicAttack -= Power;
                    break;

                case JiangStages.AtributesType.PDefense:
                    client.Defence = (ushort)(client.Defence - Power);
                    break;

                case JiangStages.AtributesType.Mdefense:
                    client.MagicDefence = (ushort)(client.MagicDefence - Power);
                    break;

                case JiangStages.AtributesType.FinalAttack:
                    client.PhysicalDamageIncrease = (ushort)(client.PhysicalDamageIncrease - Power);
                    break;

                case JiangStages.AtributesType.FinalMagicAttack:
                    client.MagicDamageIncrease = (ushort)(client.MagicDamageIncrease - Power);
                    break;

                case JiangStages.AtributesType.FinalDefense:
                    client.PhysicalDamageDecrease = (ushort)(client.PhysicalDamageDecrease - Power);
                    break;

                case JiangStages.AtributesType.FinalMagicDefense:
                    client.MagicDamageDecrease = (ushort)(client.MagicDamageDecrease - Power);
                    break;

                case JiangStages.AtributesType.CriticalStrike:
                    client.CriticalStrike -= Power;
                    break;

                case JiangStages.AtributesType.SkillCriticalStrike:
                    client.SkillCStrike -= Power;
                    break;

                case JiangStages.AtributesType.Immunity:
                    client.Immunity -= Power;
                    break;

                case JiangStages.AtributesType.Breakthrough:
                    client.Breaktrough = (ushort)(client.Breaktrough - Power);
                    break;

                case JiangStages.AtributesType.Counteraction:
                    client.Counteraction = (ushort)(client.Counteraction - Power);
                    break;

                case JiangStages.AtributesType.MaxMana:
                    client.MaxMana = (ushort)(client.MaxMana - Power);
                    break;
            }
        }
        public bool GetChange(uint value)
        {
            return ((Kernel.Random.Next() % 100) < value);
        }
        public void GetReward(GameState client)
        {
            do FreeCourse++; while (FreeCourse % 1000 != 0);
            SendInfo(client, Network.GamePackets.JiangHu.UpdateTime, FreeCourse.ToString(), Time.ToString());
            CreateTime();
        }
        public void GetRoll(GameState client, byte mStar, byte mStage, bool Restore = false, byte higher = 0)
        {
            if (client == null || client.ItemSyncRoot == null) return;
            lock (client.ItemSyncRoot) {
                if (client.Entity == null || client.Entity.PersistenceRequiresReload || !ReferenceEquals(client.Entity.MyJiang, this)) return;
                GetRollCore(client, mStar, mStage, Restore, higher);
            }
        }
        private void GetRollCore(GameState client, byte mStar, byte mStage, bool Restore, byte higher)
        {
            if (mStar < 1 || mStar > 9 || mStage < 1 || mStage > 9)
                return;
            try
            {
                JiangStages stages = Stagers[mStage - 1];
                if (stages.Activate)
                {
                    JiangHu.JiangStages.Star star = stages.Stars[mStar - 1];
                    if (Restore)
                    {
                        if (MyOldStar == null || MyOldStar.Star == null ||
                            MyOldStar.Stage != mStage || MyOldStar.PositionStar != mStar)
                            return;
                        if (star.UID != 0)
                            Inner_Strength = (uint)(Inner_Strength - Database.JiangHu.GetStatusPoints(star.Level));
                        stages.Stars[mStar - 1] = MyOldStar.Star;
                        star = MyOldStar.Star;
                        if (star.UID != 0)
                            Inner_Strength = (uint)(Inner_Strength + Database.JiangHu.GetStatusPoints(star.Level));
                        MyOldStar = null;
                        client.Send(new JiangHuUpdate { Atribute = star.UID, FreeCourse = FreeCourse, Stage = mStage, Star = mStar, FreeTimeTodeyUsed = (byte)FreeTimeTodeyUsed, RoundBuyPoints = RoundBuyPoints }.ToArray());
                        client.LoadItemStats();
                        JiangHuRanking.UpdateRank(this);
                        SendStatusMode(client);
                        return;
                    }
                    if (star.UID != 0)
                    {
                        Inner_Strength = (uint)(Inner_Strength - Database.JiangHu.GetStatusPoints(star.Level));
                    }
                    OldStar star2 = new OldStar
                    {
                        Stage = mStage,
                        PositionStar = mStar,
                        Star = new JiangStages.Star
                        {
                            Activate = star.Activate,
                            Level = star.Level,
                            Typ = star.Typ,
                            UID = star.UID
                        }
                    };
                    MyOldStar = star2;
                    var level = star.Level;
                    star.Level = GetStatusLevel();
                    if (level == 0)
                        level = star.Level;

                    if (higher == 1)
                        if (Kernel.Rate(20 / level))
                            star.Level = (byte)Random.Next(level, 5);
                    if (higher == 2)
                        if (Kernel.Rate(50 / level))
                            star.Level = (byte)Random.Next(4, 6);
                    star.Typ = (JiangStages.AtributesType)Random.Next(1, 16);
                    do
                    {
                        star.Typ = (JiangStages.AtributesType)Random.Next(1, 16);
                        Kernel.SendWorldMessage(new Message(client.Entity.Name + " increased Score Level to " + Inner_Strength + "!", System.Drawing.Color.Red, Message.JiangHu));
                    }
                    while (!Database.JiangHu.CultivateStatus[mStage].Contains((byte)star.Typ));
                    star.UID = ValueToRoll(star.Typ, star.Level);
                    if (!star.Activate)
                    {
                        Star = (byte)(Star + 1);
                        star.Activate = true;
                    }
                    client.Send(new JiangHuUpdate { Atribute = star.UID, FreeCourse = FreeCourse, Stage = mStage, Star = mStar, FreeTimeTodeyUsed = (byte)FreeTimeTodeyUsed, RoundBuyPoints = RoundBuyPoints }.ToArray());
                    Inner_Strength = (uint)(Inner_Strength + Database.JiangHu.GetStatusPoints(star.Level));
                    if ((mStage < 9) && !((mStar != 9) || Stagers[mStage].Activate))
                    {
                        Stage = (byte)(Stage + 1);
                        Stagers[mStage].Activate = true;
                        SendInfo(client, 12, new string[] { Stage.ToString() });
                    }
                    client.LoadItemStats();
                    JiangHuRanking.UpdateRank(this);
                    client.Entity.MyJiang.SendStatusMode(client);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        internal void PrepareRestoration(OldStar previous)
        {
            var value = previous.Star;
            Stagers[previous.Stage - 1].Stars[previous.PositionStar - 1] = new JiangStages.Star {
                Activate = value.Activate, UID = value.UID, Typ = value.Typ, Level = value.Level
            };
            MyOldStar = null;
            RecalculateInnerStrength();
        }
        internal void PrepareTrainingRoll(byte starNumber, byte stageNumber, bool strengthened = false)
        {
            var stage = Stagers[stageNumber - 1];
            var star = stage.Stars[starNumber - 1];
            MyOldStar = new OldStar { Stage = stageNumber, PositionStar = starNumber,
                Star = new JiangStages.Star { Activate = star.Activate, UID = star.UID, Typ = star.Typ, Level = star.Level } };
            byte level = GetStatusLevel();
            if (strengthened && level < 6) {
                int draw = Features.JiangTraining.PillRoll(100);
                if (draw < 0 || draw >= 100) throw new InvalidOperationException("Jiang pill roll outside range.");
                if (draw < 10) level = 6;
            }
            var allowed = Database.JiangHu.CultivateStatus[stageNumber];
            if (allowed.Count == 0) throw new InvalidOperationException("Jiang training catalog missing.");
            byte type = allowed[Random.Next(allowed.Count)];
            ushort attribute = Database.JiangHu.ValueToRoll(type, level);
            if (!Database.JiangHu.Attributes.ContainsKey(attribute)) throw new InvalidOperationException("Jiang attribute missing.");
            star.Level = level; star.Typ = (JiangStages.AtributesType)type; star.UID = attribute;
            if (!star.Activate) { Star++; star.Activate = true; }
            if (stageNumber < 9 && starNumber == 9 && !Stagers[stageNumber].Activate) {
                Stage++; Stagers[stageNumber].Activate = true;
            }
            RecalculateInnerStrength();
        }
        public byte GetStatusLevel()
        {
            byte num = (byte)Random.Next(1, Random.Next(1, 15));
            if (num > 6)
            {
                num = 6;
            }
            return num;
        }
        public byte GetValueLevel(ushort val)
        {
            return (byte)((val - ((ushort)(val % 256))) / 256);
        }
        public JiangStages.AtributesType GetValueType(uint val)
        {
            return (JiangStages.AtributesType)(val % 256);
        }
        public void IncreaseStatus(Entity client, JiangStages.AtributesType status, ushort Power)
        {
            if (client.Owner != null && client.Owner.ChampionGroup != null) return;
            switch (status)
            {
                case JiangStages.AtributesType.MaxLife:
                    client.ItemHP += Power;
                    break;

                case JiangStages.AtributesType.PAttack:
                    client.BaseMaxAttack += Power;
                    client.BaseMinAttack += Power;
                    break;

                case JiangStages.AtributesType.MAttack:
                    client.BaseMagicAttack += Power;
                    break;

                case JiangStages.AtributesType.PDefense:
                    client.Defence = (ushort)(client.Defence + Power);
                    break;

                case JiangStages.AtributesType.Mdefense:
                    client.MagicDefence = (ushort)(client.MagicDefence + Power);
                    break;

                case JiangStages.AtributesType.FinalAttack:
                    client.PhysicalDamageIncrease = (ushort)(client.PhysicalDamageIncrease + Power);
                    break;

                case JiangStages.AtributesType.FinalMagicAttack:
                    client.MagicDamageIncrease = (ushort)(client.MagicDamageIncrease + Power);
                    break;

                case JiangStages.AtributesType.FinalDefense:
                    client.PhysicalDamageDecrease = (ushort)(client.PhysicalDamageDecrease + Power);
                    break;

                case JiangStages.AtributesType.FinalMagicDefense:
                    client.MagicDamageDecrease = (ushort)(client.MagicDamageDecrease + Power);
                    break;

                case JiangStages.AtributesType.CriticalStrike:
                    client.CriticalStrike += Power;
                    break;

                case JiangStages.AtributesType.SkillCriticalStrike:
                    client.SkillCStrike += Power;
                    break;

                case JiangStages.AtributesType.Immunity:
                    client.Immunity += Power;
                    break;

                case JiangStages.AtributesType.Breakthrough:
                    client.Breaktrough = (ushort)(client.Breaktrough + Power);
                    break;

                case JiangStages.AtributesType.Counteraction:
                    client.Counteraction = (ushort)(client.Counteraction + Power);
                    break;

                case JiangStages.AtributesType.MaxMana:
                    client.ItemMP = (ushort)(client.ItemMP + Power);
                    break;
            }
        }
        public bool InTwinCastle(Entity location)
        {
            ushort x = location.X;
            ushort y = location.Y;
            if (location.MapID != 1002)
                if (x <= 357 && x >= 274 && y <= 277 && y >= 110)
                    return true;
            if (x >= 335 && x <= 370 && y <= 235 && y >= 232)
                return true;
            if (x >= 281 && x <= 350 && y >= 268 && y <= 322)
                return true;
            if (x >= 247 && x <= 291 && y >= 200 && y <= 243)
                return true;
            return false;
        }
        public void Load(string Line)
        {
            try
            {
                if (((Line != null) && (Line != "")) && Line.Contains<char>('#'))
                {
                    string[] strArray = Line.Split(new char[] { '#' });
                    UID = uint.Parse(strArray[0]);
                    OwnName = strArray[1];
                    JiangName = strArray[2];
                    Level = byte.Parse(strArray[3]);
                    Talent = byte.Parse(strArray[4]);
                    Stage = byte.Parse(strArray[5]);
                    Star = byte.Parse(strArray[6]);
                    FreeTimeTodeyUsed = uint.Parse(strArray[7]);
                    OnJiangHu = byte.Parse(strArray[8]) == 0;
                    FreeCourse = uint.Parse(strArray[9]);
                    StartCountDwon = DateTime.Now;
                    TimerStamp = DateTime.Now;
                    CountDownEnd = DateTime.Now.AddSeconds((double)uint.Parse(strArray[10]));
                    RoundBuyPoints = ushort.Parse(strArray[11]);
                    ushort index = 12;
                    foreach (JiangStages stages in Stagers)
                    {
                        stages.Activate = byte.Parse(strArray[index]) == 1;
                        index = (ushort)(index + 1);
                        foreach (Game.JiangHu.JiangStages.Star star in stages.Stars)
                        {
                            star.Activate = byte.Parse(strArray[index]) == 1;
                            index = (ushort)(index + 1);
                            star.UID = ushort.Parse(strArray[index]);
                            index = (ushort)(index + 1);
                            if (star.Activate)
                            {
                                star.Typ = GetValueType(star.UID);
                                star.Level = GetValueLevel(star.UID);
                            }
                        }
                    }
                    RecalculateInnerStrength();
                    SavedData = Line; HasSavedData = true; SaveBlocked = false;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        public void OnloginClient(GameState client)
        {
            SendStatus(client, client);
            SendStatusMode(client);
            TimerStamp = DateTime.Now;
        }
        public void ResetDay(GameState client)
        {
            RoundBuyPoints = 0;
            FreeTimeTodeyUsed = 0;
            SendStatus(client, client);
        }
        public bool SameCourseStage(uint first, uint last)
        {
            if (first > 100000)
            {
                return (first.ToString()[2] == last.ToString()[2]);
            }
            return (first.ToString()[1] == last.ToString()[1]);
        }
        public void SendInfo(GameState client, byte mode = 1, params string[] data)
        {
            lock (sync)
            {
                try
                {
                    Revora.Network.GamePackets.JiangHu hu = new Revora.Network.GamePackets.JiangHu
                    {
                        Mode = mode,
                        Texts = new List<string>(data)
                    };
                    hu.CreateArray();
                    hu.Send(client);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.ToString());
                }
            }
        }
        public void SendStatus(GameState client, GameState Attacked)
        {
            try
            {
                var dictionary = Stagers.Where(p => p.Activate);
                JiangHuStatus stat = new JiangHuStatus((byte)dictionary.Count());
                stat.Name = JiangName;
                stat.FreeTimeTodey = 10;
                stat.Talent = Talent;
                stat.Stage = Stage;
                stat.RoundBuyPoints = RoundBuyPoints;
                stat.FreeTimeTodeyUsed = (byte)FreeTimeTodeyUsed;
                stat.StudyPoints = Attacked.Entity.SubClasses.StudyPoints;
                if (client.Entity.UID != Attacked.Entity.UID)
                    stat.Timer = 13751297;
                else
                    stat.Timer = 15500800;
                stat.Apprend(dictionary.ToArray());
                client.Send(stat.ToArray());
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
        }
        public void SendStatusMode(GameState client)
        {
            if (client.Entity.MyJiang != null)
            {
                if (Talent < 1)
                {
                    Talent = 1;
                }
                else if (Talent > 5)
                {
                    Talent = 5;
                }
                client.Entity.TalentStaus = Talent;
                client.Entity.JiangActive = OnJiangHu;
                SendInfo(client, 7, new string[] { client.Entity.UID.ToString(), client.Entity.TalentStaus.ToString(), OnJiangHu ? "1" : "2" });
                SendInfo(client.Entity.Owner, 5, client.ToString(), Talent.ToString());
                client.Entity.Owner.SendScreen(client.Entity.CharBuffer.ToConquerPacket(10014), false);
                if (OnJiangHu)
                {
                    RemoveJiangMod = DateTime.Now;
                }
            }
            else return;
        }
        public void TheadTime(GameState client)
        {
            if (client == null || client.ItemSyncRoot == null) return;
            lock (client.ItemSyncRoot) {
                if (client.Entity == null || client.Entity.PersistenceRequiresReload || !ReferenceEquals(client.Entity.MyJiang, this)) return;
                TheadTimeCore(client);
            }
        }
        private void TheadTimeCore(GameState client)
        {
            try
            {
                if (((client != null) && (client.Entity != null)) && (client.Entity.FullyLoaded && (DateTime.Now > TimerStamp.AddMinutes(10))))
                {
                    if (((client.Entity.PKMode != Game.Enums.PKMode.Jiang) && OnJiangHu) && (DateTime.Now >= RemoveJiangMod.AddMinutes(10)))
                    {
                        OnJiangHu = false;
                        SendStatusMode(client);
                        client.Send(new Message("You have quitted the Jiang Hu temporarily!", Message.System));
                    }
                    if (client.Entity.PKMode == Enums.PKMode.Jiang)
                    {
                        OnJiangHu = true;
                        RemoveJiangMod = DateTime.Now;
                    }
                    if ((FreeCourse < 10000000) && (FreeTimeTodeyUsed < 10))
                    {
                        if (InTwinCastle(client.Entity))
                        {
                            StartCountDwon = StartCountDwon.AddMinutes(Database.JiangHu.GetMinutesInCastle(Talent));
                            FreeCourse += Database.JiangHu.GetFreeCourseInCastle(Talent);
                        }
                        else
                        {
                            FreeCourse += Database.JiangHu.GetFreeCourse(Talent);
                            StartCountDwon = StartCountDwon.AddMinutes(1.0);
                        }
                        if (StartCountDwon > CountDownEnd)
                        {
                            GetReward(client);
                        }
                        SendInfo(client, Network.GamePackets.JiangHu.UpdateTime, FreeCourse.ToString(), Time.ToString());
                        if ((FreeCourse % 10000) == 0)
                            GetReward(client);
                    }
                    else
                        FreeCourse = 10000000;
                    TimerStamp = DateTime.Now;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        public override string ToString()
        {
            if (StartCountDwon.Ticks > CountDownEnd.Ticks)
                CreateTime();
            uint SecoundesLeft = (uint)((CountDownEnd.Ticks - StartCountDwon.Ticks) / 10000000);
            if (OwnName != null && OwnName.Contains('#'))
                OwnName = OwnName.Replace("#", "");
            if (JiangName.Contains('#'))
                JiangName = JiangName.Replace("#", "");

            StringBuilder build = new StringBuilder();
            build.Append(UID + "#" + OwnName + "#" + JiangName + "#" + Level + "#" +
                Talent + "#" + Stage + "#" + Star + "#" + FreeTimeTodeyUsed + "#" +
                (byte)(OnJiangHu ? 0 : 1) + "#" + FreeCourse + "#" + SecoundesLeft + "#" + RoundBuyPoints + "#");
            foreach (var obj in Stagers)
                build.Append(obj.ToString());

            return build.ToString();
        }
        public void UpdateStundyPoints(GameState client, ushort amount)
        {
            client.Entity.SubClasses.StudyPoints = (ushort)(client.Entity.SubClasses.StudyPoints + amount);
            client.Send(new SubClassShowFull(0) { ID = 8, Study = client.Entity.SubClasses.StudyPoints, StudyReceive = amount }.ToArray());
            _String str = new _String(true)
            {
                Type = 10,
                UID = client.Entity.UID
            };
            str.Texts.Add("zf2-e300");
            client.SendScreen(str.ToArray(), true);
        }
        public ushort ValueToRoll(JiangStages.AtributesType status, byte level)
        {
            return (ushort)(((ushort)status) + (level * 256));
        }

        public DateTime CountDownEnd
        {
            get;
            set;
        }
        public string JiangName
        {
            get;
            set;
        }
        public uint FreeCourse
        {
            get;
            set;
        }
        public uint FreeTimeTodey
        {
            get;
            set;
        }
        public uint FreeTimeTodeyUsed
        {
            get;
            set;
        }
        public uint Inner_Strength
        {
            get;
            set;
        }
        public byte Level
        {
            get;
            set;
        }
        public OldStar MyOldStar
        {
            get;
            set;
        }
        public bool OnJiangHu
        {
            get;
            set;
        }
        public string OwnName
        {
            get;
            set;
        }
        public byte Rank
        {
            get;
            set;
        }
        public ushort RoundBuyPoints
        {
            get;
            set;
        }
        public byte Stage
        {
            get;
            set;
        }
        public JiangStages[] Stagers
        {
            get;
            set;
        }
        public byte Star
        {
            get;
            set;
        }
        public DateTime StartCountDwon
        {
            get;
            set;
        }
        public byte Talent
        {
            get;
            set;
        }
        public uint Time
        {
            get
            {
                DateTime time = new DateTime(1970, 1, 1);
                TimeSpan span = (TimeSpan)(CountDownEnd - time.ToLocalTime());
                return (uint)span.TotalSeconds;
            }
        }
        public uint UID
        {
            get;
            set;
        }

        public static class JiangHuRanking
        {
            private static object SyncRoot = new object();
            private static ConcurrentDictionary<uint, IJiangHu> TopRank = new ConcurrentDictionary<uint, IJiangHu>();
            public static IJiangHu[] TopRank100 = null;
            private static void CalculateRanks()
            {
                foreach (IJiangHu hu in TopRank.Values)
                {
                    hu.Rank = 0;
                }
                IOrderedEnumerable<IJiangHu> enumerable = from jiang in TopRank.Values.ToArray<IJiangHu>() orderby jiang.Inner_Strength descending select jiang;
                List<IJiangHu> list = new List<IJiangHu>();
                byte num = 1;
                foreach (IJiangHu hu in enumerable)
                {
                    if (num == 101)
                    {
                        break;
                    }
                    hu.Rank = num;
                    list.Add(hu);
                    num = (byte)(num + 1);
                }
                TopRank100 = list.ToArray();
                TopRank = new ConcurrentDictionary<uint, IJiangHu>();
                foreach (IJiangHu hu in list)
                {
                    TopRank.TryAdd(hu.UID, hu);
                }
                list = null;
            }
            public static void UpdateRank(IJiangHu jiang)
            {
                lock (SyncRoot)
                {
                    if (!TopRank.ContainsKey(jiang.UID))
                    {
                        TopRank.TryAdd(jiang.UID, jiang);
                    }
                    CalculateRanks();
                }
            }
        }
        public class JiangStages
        {
            public struct Aligment
            {
                public List<Star> Stars;
                public int count
                {
                    get { return Stars.Count; }
                }
                public ushort getpower()
                {
                    return Database.JiangHu.Aligmentpower[count];
                }
            }
            public List<Aligment> Aligments = new List<Aligment>();
            public bool Activate = false;
            public Star[] Stars = new Star[9];
            public JiangStages()
            {
                Stars = new Star[9];
                for (byte x = 0; x < 9; x++)
                    Stars[x] = new Star();
            }
            public bool ContainAtribut(AtributesType typ)
            {
                foreach (var atr in Stars)
                    if (atr.Typ == typ)
                        return true;
                return false;
            }
            public override string ToString()
            {
                StringBuilder build = new StringBuilder();
                build.Append((byte)(Activate ? 1 : 0) + "#");
                foreach (var obj in Stars)
                    build.Append(obj.ToString());
                return build.ToString();
            }
            public enum AtributesType
            {
                None,
                MaxLife,
                PAttack,
                MAttack,
                PDefense,
                Mdefense,
                FinalAttack,
                FinalMagicAttack,
                FinalDefense,
                FinalMagicDefense,
                CriticalStrike,
                SkillCriticalStrike,
                Immunity,
                Breakthrough,
                Counteraction,
                MaxMana
            }
            public class Star
            {
                public bool Activate = false;
                public byte Level;
                public Game.JiangHu.JiangStages.AtributesType Typ;
                public ushort UID;
                public override string ToString()
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append(string.Concat(new object[] { Activate ? ((byte)1) : ((byte)0), "#", UID, "#" }));
                    return builder.ToString();
                }
            }
        }
        public class OldStar
        {
            public byte PositionStar;
            public byte Stage;
            public Game.JiangHu.JiangStages.Star Star;
        }
        public class GetNewStar
        {
            public byte Stage;
            public byte PositionStar;
            public JiangStages.Star Star;
        }
        public GetNewStar MyNewStart = null;
        public void ApplayNewStar(Client.GameState client)
        {
            if (MyNewStart == null) return;
            JiangStages n_stage = Stagers[MyNewStart.Stage - 1];
            if (!n_stage.Activate) return;
            JiangStages.Star n_star = n_stage.Stars[MyNewStart.PositionStar - 1];
            if (!n_star.Activate)
            {
                Star++;
                n_star.Activate = true;
            }
            n_star.Level = MyNewStart.Star.Level;
            n_star.Typ = MyNewStart.Star.Typ;
            n_star.UID = MyNewStart.Star.UID;
            client.LoadItemStats();
            if (MyNewStart.Stage < 9)
            {
                if (MyNewStart.PositionStar == 9 && !Stagers[MyNewStart.Stage].Activate)
                {
                    Stage++;
                    Stagers[MyNewStart.Stage].Activate = true;
                    SendInfo(client, Network.GamePackets.JiangHu.OpenStage, Stage.ToString());
                }
            }
            MyNewStart = null;
        }
        public static void JiangHus(byte[] packet, GameState client)
        {
            if (packet == null || packet.Length < 5 || client == null || client.Entity == null) return;
            switch (packet[4])
            {
                case 1:
                    {
                        if (packet.Length < 7) return;
                        byte length = packet[6];
                        int body = BitConverter.ToUInt16(packet, 0);
                        if (length < 1 || length > 16 || body < 7 + length || packet.Length < body + 8) return;
                        Features.JiangTraining.Create(client, GeneralData.ReadString(packet, 7, length));
                        break;
                    }
                case 18:
                    {
                        byte len = packet[6];
                        string newname = GeneralData.ReadString(packet, 7, len);
                        if (len > 16)
                        {
                            client.Entity.SendSysMesage("Name length must be 16 char or less");
                        }
                        else if (len < 4)
                        {
                            client.Entity.SendSysMesage("Name length must be 4 char or more");
                        }
                        else
                        {
                            if (client.Entity.ConquerPoints >= 810)
                            {

                                if (Revora.Game.JiangHu.AllowNameCaracters(newname))
                                {
                                    client.Entity.MyJiang.JiangName = newname;
                                    Game.JiangHu.JiangHuClients[client.Entity.UID].JiangName = newname;
                                    client.Entity.MyJiang.SendStatus(client, client);
                                    client.Entity.TrySpendConquerPoints((uint)(810));
                                }
                            }
                            else
                            {
                                client.Entity.SendSysMesage("you don't have enough cps!");
                            }
                        }
                        break;
                    }
                case 9:
                    {
                        string Name = GeneralData.ReadString(packet, 7, packet[6]);
                        uint UID = 0;
                        if (uint.TryParse(Name, out UID))
                        {
                            Client.GameState pClient;
                            if (Kernel.GamePool.TryGetValue(UID, out pClient))
                            {
                                if (pClient.Entity.MyJiang != null)
                                {
                                    pClient.Entity.MyJiang.SendStatus(client, pClient);
                                }
                                else
                                {
                                    JiangHu.ObserveJiangHu(client);
                                }
                            }
                        }
                        else
                        {
                            JiangHuStatus stat = new JiangHuStatus(0);
                            stat.Name = Name;
                            client.Send(stat.ToArray());
                        }
                        break;
                    }
                case 10:
                    {
                        Features.JiangTraining.Restore(client);
                        break;
                    }
                case 11:
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            client.Entity.MyJiang.ApplayNewStar(client);
                        }
                        break;
                    }
                case 13:
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            client.Entity.MyJiang.SendInfo(client, Network.GamePackets.JiangHu.UpdateTime, client.Entity.MyJiang.FreeCourse.ToString(), client.Entity.MyJiang.Time.ToString());
                        }
                        break;
                    }
                default: Console.WriteLine("[JiangHu] Unknown Action " + packet[4]); break;
            }
        }
        public static uint GetTrainingCost(ushort completedTrainings)
        {
            return (uint)Math.Min((ulong)Config.JiangHuTrainingMaxCp,
                ((ulong)completedTrainings + 1) * Config.JiangHuTrainingStepCp);
        }

        public static void ExtraStudy(byte[] packet, GameState client)
        {
            if (packet == null || packet.Length < 12) return;
            Features.JiangTraining.Train(client, packet[9], packet[10], packet[11]);
        }
        public static void JiangRanking(byte[] packet, GameState client)
        {
            if (Game.JiangHu.JiangHuRanking.TopRank100 == null) return;
            ushort page = BitConverter.ToUInt16(packet, 4);
            if (page > 10 || page == 0) return;
            page -= 1;
            int offset = page * 10;
            int count = Math.Min(10, Game.JiangHu.JiangHuRanking.TopRank100.Length);
            JiangHuRank jiang_rank = new JiangHuRank((byte)count);
            jiang_rank.Page = (byte)(page + 1);
            jiang_rank.RegisteredCount = (byte)Math.Min(100, Game.JiangHu.JiangHuRanking.TopRank100.Length);
            if (client.Entity.MyJiang != null)
                jiang_rank.MyRank = client.Entity.MyJiang.Rank;
            for (byte x = 0; x < count; x++)
            {
                if (x + offset >= Game.JiangHu.JiangHuRanking.TopRank100.Length) break;
                var obj = Game.JiangHu.JiangHuRanking.TopRank100[offset + x];
                jiang_rank.Appren(obj.Rank, obj.Inner_Strength, obj.Level, obj.OwnName, obj.JiangName);
            }
            client.Send(jiang_rank.ToArray());
        }
        public static void JiangSettings(byte[] packet, GameState client)
        {
            Game.JiangHu.AttackFlag Flag = (Game.JiangHu.AttackFlag)packet[4];
            client.Entity.AttackJiang = Flag;
        }
        public unsafe static void ObserveJiangHu(GameState client)
        {
            byte[] Packet = new byte[16];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.JiangHu;
                *((byte*)(Pack + 4)) = 8;//Type
                *((byte*)(Pack + 5)) = 1;
                *((byte*)(Pack + 6)) = 1;
                *((byte*)(Pack + 7)) = 51;
                client.Send(Packet);
            }
        }
        public unsafe static void SendJiangHu(GameState client)
        {
            if (client.Entity.MyJiang != null)
            {
                client.Entity.MyJiang.OnJiangHu = false;
                client.Entity.MyJiang.SendStatusMode(client);
                client.SendScreen(client.Entity.CharBuffer.ToConquerPacket(10014), true);
            }
            else if (client.Entity.Reborn == 2)
            {
                byte[] Packet = new byte[14];
                fixed (byte* Pack = Packet)
                {
                    *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                    *((ushort*)(Pack + 2)) = PacketID.JiangHu;
                    *((byte*)(Pack + 4)) = 0;//Type
                    client.Send(Packet);
                }
            }
        }

        public void GetKill(Client.GameState attacker, Game.JiangHu attacked)
        {
            if ((attacked != null) && this.GetChange(0x23))
            {
                this.Talent = (byte)Math.Min(5, this.Talent + 1);
                attacker.Entity.JiangTalent1 = this.Talent;
                this.SendInfo(attacker, 5, new string[] { attacker.Entity.UID.ToString(), this.Talent.ToString() });
            }
        }
    }
}
