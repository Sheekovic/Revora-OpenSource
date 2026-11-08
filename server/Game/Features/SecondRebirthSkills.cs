using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class SecondRebirthSkills
    {
        private static readonly Dictionary<int,ushort[]> matrix=Load();
        private static readonly Dictionary<byte,ushort> pure=new Dictionary<byte,ushort> {
            {1,10315},{2,10311},{4,10313},{5,6003},{6,10405},{7,11040},{8,12300},{13,30000},{14,10310}
        };
        private static readonly HashSet<ushort> classified=new HashSet<ushort>(RebirthSkillRules.KnownArts()
            .Concat(matrix.Values.SelectMany(s=>s)).Concat(new ushort[]{5000,5002,1005,1195}));
        private static Dictionary<int,ushort[]> Load() {
            var result=new Dictionary<int,ushort[]>();
            using(var stream=typeof(SecondRebirthSkills).Assembly.GetManifestResourceStream("Revora.SecondRebirthSkills.csv"))
            using(var reader=new StreamReader(stream)) {
                reader.ReadLine();string line;
                while((line=reader.ReadLine())!=null) {
                    var f=line.Split(',');if(f.Length!=5)throw new InvalidOperationException("Second-life skill row is invalid.");
                    int a=int.Parse(f[0],CultureInfo.InvariantCulture),b=int.Parse(f[1],CultureInfo.InvariantCulture),c=int.Parse(f[2],CultureInfo.InvariantCulture);
                    result.Add(a*10000+b*100+c,f[3].Length==0?new ushort[0]:f[3].Split(';').Select(s=>ushort.Parse(s,CultureInfo.InvariantCulture)).ToArray());
                }
            }
            if(result.Count!=729)throw new InvalidOperationException("Second-life skill matrix is incomplete.");return result;
        }
        private static IEnumerable<ushort> WindInherited(byte old,byte next) {
            // Explicit composition policy for triples omitted from the older
            // matrix; this permits retention, not unearned cross-class grants.
            if(old==16)return new ushort[]{12830};
            if(next==16) {
                switch(old) {
                    case 1:return new ushort[]{1110,1190};case 2:return new ushort[]{3060};case 4:return new ushort[]{5002};
                    case 5:return new ushort[]{6001};case 6:return new ushort[]{10400};case 7:return new ushort[]{11070};case 8:return new ushort[]{12280};
                    case 13:return new ushort[]{1005,1085,1090,1095,1195,1350};case 14:return new ushort[]{1000,1001,1005,1195};
                }
            }
            switch(old) {
                case 1:return next==2?new ushort[]{1110,1190,1270,5100}:new ushort[]{1110,1190,1270};
                case 2:return next==13?new ushort[]{1025,1020,1040,3060}:next==4 || next==14?new ushort[]{1020,1040,3060}:new ushort[]{1015,1040,1320,3060};
                case 4:return new ushort[]{5002};case 5:return new ushort[]{6001};case 6:return new ushort[]{10400};case 7:return new ushort[]{11070};case 8:return new ushort[]{12280};
                case 13:return new ushort[]{1005,1085,1090,1095,1195};case 14:return next==13?new ushort[]{1120}:new ushort[]{1000,1001,1005,1195};
            }
            return new ushort[0];
        }
        internal static ushort[] Retained(byte first,byte second,byte third) {
            var allowed=new HashSet<ushort>(RebirthSkillRules.SchoolArts(third));
            if(third==13 || third==14)allowed.UnionWith(new ushort[]{1005,1195});
            if(first==16 || second==16 || third==16) {allowed.UnionWith(WindInherited(first,third));allowed.UnionWith(WindInherited(second,third));}
            else allowed.UnionWith(matrix[first*10000+second*100+third]);
            foreach(var art in pure)if(first!=art.Key || second!=art.Key || third!=art.Key)allowed.Remove(art.Value);
            if(!(second==5 && third==5))allowed.Remove(6002);
            // Kraken's Revenge belongs to another school followed by two Pirate lives.
            // Its existing trainer handles learning; the rite only retains an earned art.
            if(first==7 || second!=7 || third!=7)allowed.Remove(11100);
            allowed.Add(12080); // Explicit Ninja expansion retention exception, learned arts only.
            allowed.RemoveWhere(RebirthSkillRules.LostAtRebirth);
            return allowed.OrderBy(id=>id).ToArray();
        }
        internal static Spell[] Plan(GameState p,byte nextClass) {
            return PlanHistory(p,p.Entity.FirstRebornClass,p.Entity.Class,nextClass);
        }
        internal static Spell[] PlanHistory(GameState p,byte firstClass,byte currentClass,byte nextClass) {
            byte first=(byte)(firstClass/10),second=(byte)(currentClass/10),third=(byte)(nextClass/10);
            var allowed=new HashSet<ushort>(Retained(first,second,third));
            var skills=p.Spells.Values.Where(s=>!SkillTable.DerivedRuneSkill(s.ID) && (!classified.Contains(s.ID) || allowed.Contains(s.ID)))
                .Select(s=>new Spell(true){ID=s.ID,Level=0,PreviousLevel=RebirthSkillRules.PreviousAfterRebirth(s),Experience=0,Souls=s.Souls,LevelHu2=s.LevelHu2,Available=s.Available}).ToList();
            Action<ushort> grant=id=>{
                if(skills.Any(s=>s.ID==id))return;
                // A granted art may have been filtered from the retention matrix.
                // Preserve its existing row and learned level when granting it again.
                var old=p.Spells.Values.SingleOrDefault(s=>s.ID==id);
                skills.Add(old==null?new Spell(true){ID=id}:new Spell(true){ID=id,Level=0,
                    PreviousLevel=RebirthSkillRules.PreviousAfterRebirth(old),Experience=0,Souls=old.Souls,LevelHu2=old.LevelHu2,Available=old.Available});
            };grant(9876);
            ushort bonus=second!=third?(ushort)0:second==1?(ushort)3050:second==2?(ushort)3060:second==4?(ushort)5000:
                second==5?(ushort)6002:second==8?(ushort)12280:second==13?(ushort)3090:second==14?(ushort)3080:(ushort)0;
            if(bonus!=0)grant(bonus);
            if(second==4 && third!=4 && allowed.Contains(5002))grant(5002);
            if(allowed.Contains(3060) && second==2)grant(3060);
            ushort art;if(first==second && second==third && pure.TryGetValue(third,out art))grant(art);
            return skills.ToArray();
        }
    }
}
