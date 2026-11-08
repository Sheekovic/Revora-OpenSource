using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class ArenaBotSnapshot
    {
        internal static Entity CreateBody(GameState owner,uint uid)
        {
            return new Entity(EntityFlag.Player,false){Owner=owner,UID=uid,
                Name=ArenaBotStore.Names[uid-ArenaBotStore.FirstUid],Spouse="None",ClanName="Arena"};
        }
        internal static byte ChooseClass(byte source,int choice)
        {
            return ChooseFreshClass(source,choice,new byte[0]);
        }
        internal static byte ChooseFreshClass(byte source,int choice,IEnumerable<byte> recent)
        {
            byte[] eligible=new byte[]{15,25,45,55,65,75,85,145,165}.Where(c=>c/10!=source/10).ToArray();
            var history=recent.ToArray();
            var excluded=history.Skip(Math.Max(0,history.Length-eligible.Length+1)).ToArray();
            var fresh=eligible.Where(c=>!excluded.Contains(c)).ToArray();
            return fresh[(int)((uint)choice%(uint)fresh.Length)];
        }
        internal static uint EquipmentFamily(byte profession,ushort position)
        {
            if(profession/10==4 && (position==4 || position==5))return 613;
            uint[] ids=RevoraStarterPack.GearIds(profession);
            if(ids==null)throw new InvalidOperationException("Unknown Arena class.");
            switch(position) {
                case 1:return ids[0]/1000;
                case 2:return ids[2]/1000;
                case 3:return ids[1]/1000;
                case 4:return ids[5]/1000;
                case 5:return ids.Length>6?ids[6]/1000:0;
                case 6:return ids[3]/1000;
                default:return 0;
            }
        }
        internal sealed class StrengthTier
        {
            internal int Plus=12,Perfection=51,Soul=7,Bless=7,Sockets=2,ChiFloor=90,JiangFloor=5;
        }
        internal static StrengthTier MeasureTier(GameState source)
        {
            var positions=new byte[]{1,2,3,4,5,6,8,10,11,12,18,19};
            var gear=source.Equipment.GetItems().Where(i=>i!=null && i.IsWorn && !ItemHandler.IsArrow(i.ID)
                && positions.Contains((byte)(i.Position>=21 && i.Position<=29?i.Position-20:i.Position))
                && (ReferenceEquals(i,source.Weapons.Item1) || ReferenceEquals(i,source.Weapons.Item2) || source.AreStatsLoadable(i))).ToArray();
            var tier=new StrengthTier();
            if(gear.Length==0)return new StrengthTier{Plus=0,Perfection=0,Soul=0,Bless=0,Sockets=0,ChiFloor=50,JiangFloor=1};
            tier.Plus=(int)Math.Min(12,Math.Ceiling(gear.Average(i=>(double)i.Plus)));
            tier.Perfection=(int)Math.Min(54,Math.Round(gear.Average(i=>(double)i.PerfectionLevel)));
            tier.Bless=(int)Math.Min(7,Math.Round(gear.Average(i=>(double)i.Bless)));
            var socketable=gear.Where(i=>i.Position!=12 && i.Position!=18).ToArray();
            tier.Sockets=socketable.Length==0?0:(int)Math.Round(socketable.Average(i=>(i.SocketOne!=Enums.Gem.NoSocket?1:0)+(i.SocketTwo!=Enums.Gem.NoSocket?1:0)));
            var souls=gear.Where(i=>i.Purification.PurificationLevel>0).ToArray();
            tier.Soul=souls.Length==0?0:(int)Math.Min(7,Math.Ceiling(souls.Average(i=>(double)i.Purification.PurificationLevel)));
            tier.ChiFloor=source.ChiPowers==null || source.ChiPowers.Count==0?50:
                Math.Max(50,Math.Min(95,(int)(source.ChiPowers.Average(c=>c.Points)/4/5)*5-5));
            var stars=source.Entity.MyJiang==null?new Game.JiangHu.JiangStages.Star[0]:source.Entity.MyJiang.Stagers
                .Where(st=>st.Activate).SelectMany(st=>st.Stars).Where(st=>st.Activate).ToArray();
            tier.JiangFloor=stars.Length==0?1:Math.Max(1,Math.Min(5,(int)Math.Round(stars.Average(st=>(double)st.Level))-1));
            return tier;
        }
        internal static void BuildProfile(GameState p,byte profession,byte level,int seed)
        { BuildTieredProfile(p,profession,level,seed,new StrengthTier()); }
        // Aggregate tiers only: item identities, stat rolls and character combat scalars are never copied.
        internal static void BuildTieredProfile(GameState p,byte profession,byte level,int seed,StrengthTier tier)
        {
            var rng=new Random(seed);var e=p.Entity;bool magic=profession==145;
            e.Level=level;e.Class=profession;e.Reborn=(byte)(level>=110?2:0);
            e.FirstRebornClass=e.SecondRebornClass=e.Reborn>0?profession:(byte)0;
            e.NobilityRank=ConquerStructures.NobilityRank.Serf;
            e.Body=1006;e.Face=37;e.HairStyle=(ushort)(410+rng.Next(3));
            int points=level*3+(e.Reborn>0?150:0);
            e.Strength=(ushort)(magic?Math.Min(40,points/10):Math.Min(176,points/3));
            e.Agility=(ushort)(profession==45?Math.Min(256,points/2):Math.Min(65,points/6));
            e.Spirit=(ushort)(magic?Math.Min(220,points/2):Math.Min(40,points/10));
            e.Vitality=(ushort)Math.Max(1,points-e.Strength-e.Agility-e.Spirit);
            byte[] positions={1,2,3,4,5,6,8,10,11,12,18,19};
            foreach(byte position in positions) {
                if(level<100 && (position==10 || position==11))continue;
                uint family=position==8?160u:position==10?201u:position==11?202u:position==12?300u:position==18?203u:position==19?204u:EquipmentFamily(profession,position);
                if(family==0)continue;
                bool arrow=family==1050;
                var choices=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==family
                    && (arrow?ItemHandler.IsArrow(i.ID):position==12 || i.ID%10==9) && i.Level<=level).OrderByDescending(i=>i.Level).ThenBy(i=>i.ID).Take(tier.Plus>=11?1:3).ToArray();
                if(choices.Length==0)throw new InvalidOperationException("Arena class catalog has no equipment family "+family);
                var info=choices[rng.Next(choices.Length)];
                var item=new ConquerItem(true){ID=info.ID,UID=checked(0x70000000u+(e.UID-ArenaBotStore.FirstUid)*32+position),
                    Position=position,IsWorn=true,Durability=info.Durability,MaximDurability=info.Durability};
                if(!arrow) {
                    item.Plus=(byte)rng.Next(tier.Plus>=11?tier.Plus:Math.Max(0,tier.Plus-1),Math.Min(12,tier.Plus+1)+1);
                    item.Bless=(byte)(tier.Bless>=6?7:tier.Bless>=4?5:tier.Bless>=2?3:tier.Bless);
                    item.PerfectionLevel=(uint)rng.Next(Math.Max(0,tier.Perfection-3),Math.Min(54,tier.Perfection+3)+1);
                    var gem=position==10?Enums.Gem.SuperThunderGem:position==11?Enums.Gem.SuperGloryGem:
                        magic?Enums.Gem.SuperPhoenixGem:Enums.Gem.SuperDragonGem;
                    if(position!=12 && position!=18) {
                        item.SocketOne=tier.Sockets>=1?gem:Enums.Gem.NoSocket;
                        item.SocketTwo=tier.Sockets>=2?gem:Enums.Gem.NoSocket;
                    }
                    uint soulTier=(uint)tier.Soul;
                    var souls=ConquerItemInformation.BaseInformations.Values.Where(i=>soulTier>0 && i.ID>=800000 && i.ID<900000
                        && i.PurificationLevel==soulTier && ArtifactApplication.Fits(item.ID,i.ID)).OrderBy(i=>i.ID).ToArray();
                    if(souls.Length>0)item.Purification=new ItemAdding.Purification_{Available=true,ItemUID=item.UID,
                        PurificationItemID=souls[rng.Next(souls.Length)].ID,PurificationLevel=soulTier};
                }
                if(!p.Equipment.Add(item,Enums.ItemUse.None))throw new InvalidOperationException("Arena equipment collision.");
                p.Equipment.UpdateItemview(item);
            }
            p.Weapons=p.ComputeWeapons();
            foreach(var weapon in new[]{p.Weapons.Item1,p.Weapons.Item2}.Where(i=>i!=null && !ItemHandler.IsArrow(i.ID))) {
                ushort type=(ushort)(weapon.ID/1000);
                if(!p.Proficiencies.ContainsKey(type))p.Proficiencies.Add(type,new Proficiency(true){ID=type,Level=20});
            }
            p.ChiPowers=new List<ConquerStructures.ChiPowerStructure>();
            if(level>=110)for(int power=1;power<=4;power++) {
                var chi=new ConquerStructures.ChiPowerStructure((Enums.ChiPowerType)power);
                var types=new[]{Enums.ChiAttribute.HPAdd,magic?Enums.ChiAttribute.AddMagicAttack:Enums.ChiAttribute.AddAttack,
                    Enums.ChiAttribute.Immunity,Enums.ChiAttribute.Breakthrough,Enums.ChiAttribute.Counteraction,
                    magic?Enums.ChiAttribute.SkillCriticalStrike:Enums.ChiAttribute.CriticalStrike};
                for(int n=types.Length-1;n>2;n--){int pick=rng.Next(2,n+1);var swap=types[n];types[n]=types[pick];types[pick]=swap;}
                for(int n=0;n<4;n++)chi.Attributes[n]=new ConquerStructures.ChiAttribute(types[n],(ushort)(Enums.ChiMaxValues(types[n])*rng.Next(tier.ChiFloor,100)/100));
                chi.CalculatePoints();p.ChiPowers.Add(chi);
            }
            p.ChiData=new ChiTable.ChiData{UID=e.UID,Name=e.Name,Powers=p.ChiPowers};
            if(level>=110) {
                // UID zero constructor is deliberately detached: no persistence or ranking registration.
                var jiang=new Game.JiangHu(0){UID=e.UID,OwnName=e.Name,JiangName="Arena Disciple",Level=level,Stage=9,Star=9,
                    Talent=(byte)rng.Next(1,6),FreeTimeTodeyUsed=10,SaveBlocked=true,OnJiangHu=false};
                for(byte stage=0;stage<9;stage++) {
                    var allowed=Database.JiangHu.CultivateStatus[(byte)(stage+1)];
                    var rolls=Database.JiangHu.Attributes.Where(a=>allowed.Contains(a.Value.Type) && a.Value.Level>=tier.JiangFloor && a.Value.Level<=Math.Min(6,tier.JiangFloor+1)
                        && a.Value.Type!=(magic?2:3) && a.Value.Type!=(magic?6:7)).OrderBy(a=>a.Key).ToArray();
                    if(rolls.Length==0)throw new InvalidOperationException("Missing Arena Jiang stage.");
                    jiang.Stagers[stage].Activate=true;
                    var groups=rolls.GroupBy(a=>a.Value.Type).ToArray();
                    var chosen=groups[rng.Next(groups.Length)].ToArray();
                    // Coherent stage alignment, rather than nine unrelated weak rolls.
                    foreach(var star in jiang.Stagers[stage].Stars){var roll=chosen[rng.Next(chosen.Length)];star.Activate=true;star.UID=roll.Key;
                        star.Level=roll.Value.Level;star.Typ=(Game.JiangHu.JiangStages.AtributesType)roll.Value.Type;}
                }
                e.MyJiang=jiang;
            }
            LearnClassSkills(p);
            p.LoadItemStats();
            // Proven genuine-6711 male world model; older Body=1003 models do not draw.
            e.CharBuffer._Mesh=371006;
            foreach(var item in p.Equipment.GetItems())p.Equipment.UpdateItemview(item);
        }
        private static void LearnClassSkills(GameState p)
        {
            var profile=ArenaBotPolicy.For(p.Entity.Class,p.Entity.Assassin(),true);
            foreach(ushort id in profile.Attacks.Concat(profile.Defenses).Concat(profile.Recovery).Distinct()) {
                if(!SpellTable.SpellInformations.ContainsKey(id))continue;
                var spell=SpellTable.SpellInformations[id].Values.Where(i=>i.NeedLevel<=p.Entity.Level && i.NeedXP!=1)
                    .OrderByDescending(i=>i.Level).FirstOrDefault();
                if(spell==null)continue;
                p.Spells.Add(id,new Spell(true){ID=id,Level=spell.Level,Available=true});
            }
        }
        internal static GameState Create(uint uid,GameState source,byte profession)
        {
            if(!System.Threading.Monitor.IsEntered(source.ItemSyncRoot))throw new InvalidOperationException("Snapshot needs owner lock.");
            var p=new GameState(null){Account=new AccountTable(null)};
            try
            {
            p.Account.EntityID=uid;p.ReadyToPlay();
            if(p.Screen==null || p.Inventory==null || p.Equipment==null)throw new InvalidOperationException("Arena bot initialization failed.");
            p.Entity=CreateBody(p,uid);
            p.Variables=new VariableVault();
            p.Friends=new SafeDictionary<uint,ConquerStructures.Society.Friend>();
            p.Enemy=new SafeDictionary<uint,ConquerStructures.Society.Enemy>();
            p.Spells=new SafeDictionary<ushort,ISkill>();p.Proficiencies=new SafeDictionary<ushort,IProf>();
            p.NobilityInformation=new ConquerStructures.NobilityInformation{EntityUID=uid,Name=p.Entity.Name,Rank=ConquerStructures.NobilityRank.Serf};
            BuildTieredProfile(p,profession,source.Entity.Level,Kernel.Random.Next(),MeasureTier(source));
            p.ArenaStatistic=ArenaTable.ReadExisting(uid);
            p.TeamArenaStatistic=TeamArenaTable.ReadExisting(uid);
            if(p.ArenaStatistic==null || p.TeamArenaStatistic==null)throw new InvalidOperationException("Arena bot statistics missing.");
            p.ChampionStats=new ChampionStatistic(true){UID=uid,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Model=p.Entity.Mesh};
            p.Entity.PKMode=Enums.PKMode.PK;p.Entity.MapObjType=MapObjectType.Player;
            p.BindWorldMap(700);p.Entity.X=50;p.Entity.Y=50;
            p.Entity.Hitpoints=p.Entity.MaxHitpoints;p.Entity.Mana=p.Entity.MaxMana;p.Entity.Stamina=100;
            p.Entity.FullyLoaded=true;p.Entity.SendUpdates=true;p.Entity.HealthSynchronizationRequired=true;
            p.Action=2;p.Attackable=true;
            return p;
            }
            catch
            {
                if(p.Screen!=null)p.Screen.DisposeTimers();
                if(p.Pet!=null)p.Pet.DisposeTimers();
                throw;
            }
        }
    }
}
