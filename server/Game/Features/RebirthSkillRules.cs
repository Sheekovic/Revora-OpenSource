using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class RebirthSkillRules
    {
        // Class arts are distinct from weapon proficiencies, dances and common weapon skills.
        private static readonly Dictionary<byte, ushort[]> schools = new Dictionary<byte, ushort[]> {
            { 1, new ushort[] { 1110,1115,1190,1270,5100,3050,10315,11960,11970,11980,11990 } },
            { 2, new ushort[] { 1015,1020,1025,1040,1051,1320,3060,10311,10470,11160,11200,12660,12670,12680,12690,12700,12770 } },
            { 4, new ushort[] { 8000,8001,8002,8003,8030,9000,10313,11590,11600,11610,11620,11650,11660,11670 } },
            { 5, new ushort[] { 6000,6001,6002,6003,6004,6010,6011,11170,11180,11230,12070,12080,12090,12110 } },
            { 6, new ushort[] { 10381,10390,10395,10400,10405,10410,10415,10420,10421,10422,10423,10424,10425,10430,10490,12550,12560,12570 } },
            { 7, new ushort[] { 11000,11005,11030,11040,11050,11060,11070,11100,11110,11120,11130,11140,14680,14710,14720 } },
            { 8, new ushort[] { 12120,12130,12140,12160,12170,12200,12210,12220,12230,12240,12270,12280,12290,12300,12320,12330,12340,12350 } },
            { 13, new ushort[] { 1050,1055,1075,1085,1090,1095,1100,1170,1175,1280,1350,3090,30000,10309,12370,12380,12390,12400 } },
            { 14, new ushort[] { 1000,1001,1002,1010,1120,1125,1150,1160,1165,1180,3080,5001,10309,10310,13362,13363,13364 } },
            { 16, new ushort[] { 12830,12840,12850,12860,12870,12880,12890,12930,12940,12950,12960,12970,12980,12990,13000,13020,13030,13050,13060,13070,13080,13090,13190,13360 } }
        };
        private static readonly HashSet<ushort> classArts = new HashSet<ushort>(schools.Values.SelectMany(s => s));
        private static IEnumerable<ushort> Inherited(byte oldSchool, byte nextSchool) {
            switch (oldSchool) {
                case 1: return nextSchool == 2 ? new ushort[] { 1110,1190,1270,5100 }
                    : nextSchool == 4 || nextSchool == 13 || nextSchool == 14 ? new ushort[] { 1110,1190,1270 } : new ushort[] { 1110,1190 };
                case 2: return nextSchool == 16 ? new ushort[] { 3060 } : nextSchool == 13 ? new ushort[] { 1025,1020,1040 }
                    : nextSchool == 4 || nextSchool == 14 ? new ushort[] { 1020,1040 } : new ushort[] { 1015,1040,1320 };
                case 5: return new ushort[] { 6001 };
                case 6: return new ushort[] { 10400 };
                case 7: return new ushort[] { 11070 };
                case 8: return new ushort[] { 12280 };
                case 13: return nextSchool == 14 ? new ushort[] { 1050,1175,1075,1055 }
                    : nextSchool == 4 ? new ushort[] { 1075,1090,1095 }
                    : nextSchool == 6 ? new ushort[] { 1085,1090,1095,1280,1350 }
                    : nextSchool == 16 ? new ushort[] { 1085,1090,1095,1350 } : new ushort[] { 1085,1090,1095 };
                case 14: return nextSchool == 13 ? new ushort[] { 1120 } : new ushort[] { 1000,1001 };
                case 16: return new ushort[] { 12830 };
                default: return new ushort[0];
            }
        }
        // Ninja expansion and Trojan Epic guides require these arts to be relearned.
        internal static bool LostAtRebirth(ushort id) {return id==12070 || id==12090 || id==12110 || id==11980 || id==11990;}
        internal static void ClearNinjaModes(GameState p) {
            foreach(var flag in new[]{FlagsUpdate.Flags.FatalStrike,FlagsUpdate.Flags.ShurikenVortex})
                if(p.Entity.ContainsFlag((ulong)flag))p.Entity.RemoveFlag((ulong)flag);
            if(p.Entity.CounterKillSwitch) {
                p.Entity.CounterKillSwitch=false;
                p.Send(new Attack(true){Attacker=p.Entity.UID,Attacked=p.Entity.UID,AttackType=Attack.CounterKillSwitch,Damage=0,X=p.Entity.X,Y=p.Entity.Y});
            }
            foreach(var clone in p.Entity.MyClones.Values.ToArray()) {
                if(clone==null || !clone.IAmShadow || clone.ShadowID!=p.Entity.UID || !ReferenceEquals(clone.Owner,p))continue;
                Map map;Entity current,companion;
                if(Kernel.Maps.TryGetValue(clone.MapID,out map)
                    && (!map.Entities.TryGetValue(clone.UID,out current) || ReferenceEquals(current,clone))
                    && (!map.Companions.TryGetValue(clone.UID,out companion) || ReferenceEquals(companion,clone)))map.RemoveEntity(clone);
                p.SendScreen(new Data(true){UID=clone.UID,ID=Data.RemoveEntity},true);p.RemoveScreenSpawn(clone,true);
            }
            p.Entity.MyClones.Clear();
        }
        internal static void ClearPirateModes(GameState p) {
            if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce))p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ImmortalForce);
            foreach(var map in Kernel.Maps.Values)foreach(var item in map.FloorItems.Values.ToArray()) {
                if(item.ItemID!=FloorItem.SeaBurial || !ReferenceEquals(item.Owner,p) || item.OwnerUID!=p.Entity.UID || item.MapID!=map.ID)continue;
                lock(item){FloorItem current;
                    if(!map.FloorItems.TryGetValue(item.UID,out current) || !ReferenceEquals(item,current))continue;
                    item.Type=FloorItem.RemoveEffect;map.RemoveFloorItem(item);p.RemoveScreenSpawn(item,true);
                }
            }
        }
        internal static bool MonkEffect(FloorItem item){return item!=null && (item.ItemID==FloorItem.InfernalEcho || item.ItemID==FloorItem.ActOfTarget || item.ItemID==FloorItem.ActOfTarget2);}
        internal static void ClearMonkModes(GameState p){
            p.Entity.WrathoftheEmperor=false;p.Entity.WrathoftheEmperorStamp=DateTime.MinValue;
            if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion))p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Oblivion);
            p.Entity.OblivionTime=0;
            if(p.Entity.Aura_isActive){p.Entity.RemoveFlag(p.Entity.Aura_actType);p.doAuraBonuses(p.Entity.Aura_actType,p.Entity.Aura_actPower,-1);}
            p.Entity.Aura_isActive=false;p.Entity.Aura_actType=0;p.Entity.Aura_actPower=0;
            foreach(var map in Kernel.Maps.Values)foreach(var item in map.FloorItems.Values.ToArray()){
                if(!MonkEffect(item) || !ReferenceEquals(item.Owner,p) || item.OwnerUID!=p.Entity.UID || item.MapID!=map.ID)continue;
                lock(item){FloorItem current;
                    if(!map.FloorItems.TryGetValue(item.UID,out current) || !ReferenceEquals(item,current))continue;
                    item.Type=FloorItem.RemoveEffect;map.RemoveFloorItem(item);p.RemoveScreenSpawn(item,true);
                }
            }
        }
        internal static void ClearAssassinModes(GameState p) {
            foreach (var flag in new[] { FlagsUpdate.Flags.PathOfShadow, FlagsUpdate.Flags.BladeFlurry, FlagsUpdate.Flags.KineticSpark })
                if (p.Entity.ContainsFlag((ulong)flag)) p.Entity.RemoveFlag((ulong)flag);
            p.Entity.BladeFlurryTime = 0; p.Entity.SpiritFocus = false; p.Entity.SpiritFocusPercent = 0;
        }
        internal static ushort[] SchoolArts(byte school) { return (ushort[])schools[school].Clone(); }
        internal static ushort[] KnownArts() { return classArts.ToArray(); }
        // Backfire is explicitly excluded from recovering its former training after rebirth.
        internal static byte PreviousAfterRebirth(ISkill skill) { return skill.ID == 12680 ? (byte)0 : Math.Max(skill.Level,skill.PreviousLevel); }
        internal static Spell[] Plan(GameState p, byte nextClass) {
            byte oldSchool = (byte)(p.Entity.Class / 10), nextSchool = (byte)(nextClass / 10);
            var retained = new HashSet<ushort>(schools[nextSchool]);
            retained.Add(12080); // Preserve an already learned Super Twofold Blade; never grant it here.
            if (oldSchool == nextSchool) retained.UnionWith(schools[oldSchool]);
            else retained.UnionWith(Inherited(oldSchool, nextSchool));
            var skills = p.Spells.Values.Where(s => !LostAtRebirth(s.ID) && !SkillTable.DerivedRuneSkill(s.ID) && (!classArts.Contains(s.ID) || retained.Contains(s.ID)))
                .Select(s => new Spell(true) { ID = s.ID, PreviousLevel = PreviousAfterRebirth(s), Level = 0,
                    Experience = 0, Souls = s.Souls, LevelHu2 = s.LevelHu2, Available = s.Available }).ToList();
            ushort bonus = oldSchool != nextSchool ? (ushort)0 : oldSchool == 1 ? (ushort)3050 : oldSchool == 2 ? (ushort)3060
                : oldSchool == 4 ? (ushort)5000 : oldSchool == 5 ? (ushort)6002 : oldSchool == 8 ? (ushort)12280 : oldSchool == 13 ? (ushort)3090 : oldSchool == 14 ? (ushort)3080 : (ushort)0;
            if (oldSchool == 4 && nextSchool != 4 && !skills.Any(s => s.ID == 5002)) skills.Add(new Spell(true) { ID = 5002 });
            if (bonus != 0 && !skills.Any(s => s.ID == bonus)) skills.Add(new Spell(true) { ID = bonus });
            return skills.ToArray();
        }
    }
}
