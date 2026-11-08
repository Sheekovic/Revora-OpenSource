using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Game.Features.Tournaments;
using Console=System.Console;
internal static class SkillChiStatsTests {
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Type Chi=typeof(Entity).Assembly.GetType("Revora.ChiBonuses",true);
    static int checks;
    static T Empty<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;}
    static void Zero(GameState p){var e=p.Entity;e.ItemHP=e.BaseMinAttack=e.BaseMaxAttack=e.BaseMagicAttack=e.BaseMagicDefence=0;e.CriticalStrike=e.SkillCStrike=e.Immunity=0;e.Breaktrough=e.Counteraction=0;e.PhysicalDamageIncrease=e.MagicDamageIncrease=e.PhysicalDamageDecrease=e.MagicDamageDecrease=0;}
    static long Read(Entity e,Enums.ChiAttribute type){switch(type){
        case Enums.ChiAttribute.CriticalStrike:return e.CriticalStrike;
        case Enums.ChiAttribute.SkillCriticalStrike:return e.SkillCStrike;
        case Enums.ChiAttribute.Immunity:return e.Immunity;
        case Enums.ChiAttribute.Breakthrough:return e.Breaktrough;
        case Enums.ChiAttribute.Counteraction:return e.Counteraction;
        case Enums.ChiAttribute.HPAdd:return e.ItemHP;
        case Enums.ChiAttribute.AddAttack:return e.BaseMinAttack;
        case Enums.ChiAttribute.AddMagicAttack:return e.BaseMagicAttack;
        case Enums.ChiAttribute.AddMagicDefense:return e.BaseMagicDefence;
        case Enums.ChiAttribute.PhysicalDamageIncrease:return e.PhysicalDamageIncrease;
        case Enums.ChiAttribute.MagicDamageIncrease:return e.MagicDamageIncrease;
        case Enums.ChiAttribute.PhysicalDamageDecrease:return e.PhysicalDamageDecrease;
        case Enums.ChiAttribute.MagicDamageDecrease:return e.MagicDamageDecrease;
        default:throw new InvalidOperationException("Unknown fixture Chi type.");}}
    static long Apply(GameState p,Enums.ChiAttribute type,ushort value,uint percentage=100){
        p.ChiPowers[0].Attributes=new[]{new ChiAttribute(type,value)};Zero(p);
        Chi.GetMethod("Apply",Hidden).Invoke(null,new object[]{p,percentage});
        Check(p.ChiPowers[0].Attributes[0].Value==value,"calculation never mutates saved Chi: "+type);
        if(type==Enums.ChiAttribute.AddAttack)Check(p.Entity.BaseMinAttack==p.Entity.BaseMaxAttack,"both attack endpoints receive the same Chi cap");
        return Read(p.Entity,type);
    }
    static void SubclassChecks(GameState p){
        var apply=typeof(GameState).GetMethod("ApplySubclassStats",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(ClassID id in Enum.GetValues(typeof(ClassID))){
            var entry=new SubClass{ID=(byte)id,Level=9,Phase=9};p.Entity.SubClasses.Classes.Clear();p.Entity.SubClasses.Classes[entry.ID]=entry;
            Func<long> read=()=>{switch(id){
                case ClassID.MartialArtist:return p.Entity.CriticalStrike;
                case ClassID.Warlock:return p.Entity.SkillCStrike;
                case ClassID.ChiMaster:return p.Entity.Immunity;
                case ClassID.Sage:return p.Entity.Penetration;
                case ClassID.Apothecary:return p.Entity.Detoxication;
                case ClassID.Performer:return p.Entity.BaseMinAttack;
                default:return p.Entity.ItemHP;
            }};
            Action run=()=>{Zero(p);p.Entity.Penetration=0;p.Entity.Detoxication=0;apply.Invoke(p,null);};
            long normal=id==ClassID.Apothecary?72:id==ClassID.Performer?1000:id==ClassID.Wrangler?1200:1500;
            long capped=id==ClassID.Apothecary?72:id==ClassID.Performer||id==ClassID.Wrangler?500:600;
            p.Entity.InSkillPk=false;run();Check(read()==normal,"normal subclass phase9: "+id);
            p.Entity.InSkillPk=true;run();Check(read()==capped,"owned Skill subclass cap/Apothecary exception: "+id);
            if(id==ClassID.Performer)Check(p.Entity.BaseMaxAttack==500&&p.Entity.BaseMagicAttack==500,"Performer caps all three attack contributions");
            Check(entry.Phase==9&&entry.Level==9,"saved subclass phase and level unchanged: "+id);
            p.Entity.InSkillPk=false;run();Check(read()==normal,"leaving restores original subclass contribution: "+id);
            entry.Phase=3;p.Entity.InSkillPk=true;run();Check(read()==(id==ClassID.Apothecary?24:300),"lower subclass phase remains unchanged: "+id);
            entry.Phase=10;run();Check(read()==0,"invalid subclass phase is rejected: "+id);
        }
        p.Entity.InSkillPk=false;
    }
    static void BattlePowerChecks(GameState p){
        p.Equipment=new Equipment(p);p.Weapons=new Tuple<Revora.Network.GamePackets.ConquerItem,Revora.Network.GamePackets.ConquerItem>(null,null);
        var item=new Revora.Network.GamePackets.ConquerItem(true){ID=130009,Position=3,Plus=12,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13,IsWorn=true};
        var slots=new Revora.Network.GamePackets.ConquerItem[40];slots[2]=item;
        typeof(Equipment).GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p.Equipment,slots);
        p.Entity.NobilityRank=NobilityRank.King;
        typeof(Entity).GetField("_GuildSharedBp",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p.Entity,15u);
        int baseline=p.Entity.Level+p.Entity.Reborn*5;
        p.Entity.InSkillPk=false;Check(p.Entity.BattlePower==baseline+47,"normal BP includes both sockets, plus12, King and guild");
        p.Entity.InSkillPk=true;Check(p.Entity.BattlePower==baseline+15,"Skill BP uses plus4, first socket, Earl and no guild");
        Check(p.Entity.NMBattlePower==baseline+47,"normal source remains unchanged for outside recipients");
        Check(item.Plus==12&&(byte)item.SocketTwo==13&&p.Entity.NobilityRank==NobilityRank.King&&p.Entity.GuildSharedBp==15,"BP projection does not mutate gear, rank or guild state");
        p.Entity.NobilityRank=NobilityRank.Baron;item.Plus=2;item.SocketOne=0;
        Check(p.Entity.BattlePower==baseline+9,"lower plus/rank retained and second socket alone contributes nothing");
        item.ID=560009;item.Position=4;item.Plus=12;item.SocketOne=(Enums.Gem)13;slots[2]=null;p.Weapons=new Tuple<Revora.Network.GamePackets.ConquerItem,Revora.Network.GamePackets.ConquerItem>(item,null);
        Check(p.Entity.BattlePower==baseline+23,"two handed weapon retains BP multiplier with capped contribution");
        var left=new Revora.Network.GamePackets.ConquerItem(true){ID=410009,Plus=12,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13};
        p.Weapons=new Tuple<Revora.Network.GamePackets.ConquerItem,Revora.Network.GamePackets.ConquerItem>(item,left);
        Check(p.Entity.BattlePower==baseline+23,"two equipped weapons each receive one capped contribution");
        p.Entity.InSkillPk=false;Check(p.Entity.BattlePower==baseline+58,"leaving restores full weapon and guild BP");
        var peer=new GameState(null);peer.Account=new AccountTable(null);peer.Account.State=AccountTable.AccountState.Player;
        peer.Entity=new Entity(EntityFlag.Player,false);peer.Entity.Owner=peer;peer.Entity.SendUpdates=false;peer.Entity.UID=920002;peer.Entity.Hitpoints=100;
        peer.Inventory=new Inventory(peer);peer.Equipment=new Equipment(peer);peer.ItemSyncRoot=new object();peer.Action=2;peer.Fake=false;peer.BindWorldMap(p.Map.ID);peer.Entity.FullyLoaded=true;Kernel.GamePool[peer.Entity.UID]=peer;
        peer.Weapons=new Tuple<Revora.Network.GamePackets.ConquerItem,Revora.Network.GamePackets.ConquerItem>(null,null);
        typeof(Entity).GetField("independentBattlePower",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(peer.Entity,200u);
        p.Mentor=new Revora.Game.ConquerStructures.Society.Mentor{ID=peer.Entity.UID,EnroleDate=1};
        p.AsApprentice=new Revora.Game.ConquerStructures.Society.Apprentice{ID=p.Entity.UID,EnroleDate=1};
        peer.Apprentices=new SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Apprentice>();peer.Apprentices[p.Entity.UID]=p.AsApprentice;
        int mentorNormal=p.Entity.BattlePower;Check(mentorNormal>baseline+58,"canonical mentor actually contributes outside Skill scope");
        p.Entity.InSkillPk=true;Check(p.Entity.BattlePower==baseline+23,"canonical mentor share is excluded from Skill total");
        p.Entity.InSkillPk=false;Check(p.Entity.BattlePower==mentorNormal,"mentor contribution returns after leaving");p.Mentor=null;p.AsApprentice=null;
        var clan=new Clan(p.Entity.UID,p.Entity.UID,"SkillFixture","fighter");Kernel.Clans[clan.ID]=clan;
        p.Entity.ClanId=clan.ID;peer.Entity.ClanId=clan.ID;peer.Team=p.Team;
        p.Team.Members[peer.Entity.UID]=new Team.Member{entry=peer};clan.Members[p.Entity.UID]=new ClanMember{Identifier=p.Entity.UID};clan.Members[peer.Entity.UID]=new ClanMember{Identifier=peer.Entity.UID};
        int clanNormal=p.Entity.BattlePower;Check(clanNormal>baseline+58,"canonical clan peer actually contributes outside Skill scope");
        p.Entity.InSkillPk=true;Check(p.Entity.BattlePower==baseline+23,"canonical clan share is excluded from Skill total");
        p.Entity.InSkillPk=false;Check(p.Entity.BattlePower==clanNormal,"clan contribution returns after leaving");

    }
    static void EquipmentChecks(GameState p){
        ConquerItemInformation.BaseInformations=new SafeDictionary<uint,ConquerItemBaseInformation>();
        ConquerItemInformation.PlusInformations=new SafeDictionary<uint,SafeDictionary<byte,ConquerItemPlusInformation>>();
        const uint id=130009;
        ConquerItemInformation.BaseInformations[id]=new ConquerItemBaseInformation{ID=id,MinAttack=10,MaxAttack=20,PhysicalDefence=30,ItemHP=40};
        var tiers=new SafeDictionary<byte,ConquerItemPlusInformation>();
        foreach(byte tier in new byte[]{2,4,12})tiers[tier]=new ConquerItemPlusInformation{ID=id,Plus=tier,MinAttack=(uint)(tier*10),MaxAttack=(uint)(tier*20),PhysicalDefence=(ushort)(tier*30),ItemHP=(ushort)(tier*40)};
        ConquerItemInformation.PlusInformations[id-id%10]=tiers;
        var item=new Revora.Network.GamePackets.ConquerItem(true){ID=id,Position=3,Plus=12,Bless=7,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13};
        var apply=typeof(GameState).GetMethod("loadItemStats",BindingFlags.Instance|BindingFlags.NonPublic);
        Action run=()=>{Zero(p);p.Entity.ItemBless=0;p.Entity.Defence=0;p.Entity.Gems=new ushort[10];apply.Invoke(p,new object[]{item});};
        p.Entity.InSkillPk=false;run();Check(p.Entity.BaseMinAttack==130&&p.Entity.BaseMaxAttack==260&&p.Entity.Defence==420&&p.Entity.ItemHP==520,"ordinary item contributions use full catalog plus12");
        Check(p.Entity.ItemBless==7&&p.Entity.Gems[1]==30,"ordinary blessing and both gems contribute");
        p.Entity.InSkillPk=true;run();Check(p.Entity.BaseMinAttack==50&&p.Entity.BaseMaxAttack==100&&p.Entity.Defence==180&&p.Entity.ItemHP==200,"Skill actual stat loader selects plus4 catalog contributions");
        Check(p.Entity.ItemBless==3&&p.Entity.Gems[1]==15,"Skill caps blessing and retains only first gem");
        Check(item.ID==id&&item.Plus==12&&item.Bless==7&&(byte)item.SocketOne==13&&(byte)item.SocketTwo==13,"effective equipment caps preserve all source values");
        item.Plus=2;item.Bless=1;item.SocketOne=0;run();Check(p.Entity.BaseMinAttack==30&&p.Entity.ItemBless==1&&p.Entity.Gems[1]==0,"lower item values stay unchanged and second gem cannot replace missing first");
        item.Plus=12;item.Bless=7;item.SocketOne=(Enums.Gem)13;p.Entity.InSkillPk=false;run();Check(p.Entity.BaseMinAttack==130&&p.Entity.ItemBless==7&&p.Entity.Gems[1]==30,"normal item contributions return outside Skill scope");
        ConquerItemInformation.BaseInformations[id].Level=140;
        ConquerItemInformation.BaseInformations[130019]=new ConquerItemBaseInformation{ID=130019,Level=120,MinAttack=2,MaxAttack=4,PhysicalDefence=6,ItemHP=8};
        var cappedPlus=new SafeDictionary<byte,ConquerItemPlusInformation>();cappedPlus[4]=new ConquerItemPlusInformation{MinAttack=7,MaxAttack=9,PhysicalDefence=11,ItemHP=13};ConquerItemInformation.PlusInformations[130010]=cappedPlus;
        p.Entity.InSkillPk=true;run();Check(p.Entity.BaseMinAttack==9&&p.Entity.BaseMaxAttack==13&&p.Entity.Defence==23&&p.Entity.ItemHP==21,"actual loader combines capped-level base and capped-level plus4 row");
        Check(item.ID==id&&item.Plus==12,"level projection never replaces owned item identity or plus");
        p.Entity.InSkillPk=false;run();Check(p.Entity.BaseMinAttack==130&&p.Entity.ItemHP==520,"level projection restores original base and plus outside scope");

    }
    static void EquipmentLevels(string catalogPath){
        var resolver=typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkCombat").GetMethod("SkillItemType",Hidden);
        Func<uint,ushort,uint> project=(id,slot)=>(uint)resolver.Invoke(null,new object[]{new Revora.Network.GamePackets.ConquerItem(true){ID=id,Position=checked((byte)slot)}});
        uint[] high={117309,120269,121269,130309,150269,151269,152279,160249,410439,900309};
        uint[] low={117099,120229,121229,130109,150229,151229,152239,160229,410239,900109};
        ushort[] slots={1,2,2,3,6,6,6,8,4,5};
        var wanted=new HashSet<uint>(high.Concat(low));var families=new HashSet<uint>(high.Select(x=>x/1000));var catalog=new SafeDictionary<uint,ConquerItemBaseInformation>();
        foreach(string line in System.IO.File.ReadLines(catalogPath)){
            int end=line.IndexOf("@@",StringComparison.Ordinal);uint id;
            if(end<1||!uint.TryParse(line.Substring(0,end),out id)||!families.Contains(id/1000))continue;
            var row=new ConquerItemBaseInformation();row.Parse(line);catalog[id]=row;
        }
        Check(wanted.All(id=>catalog.ContainsKey(id)),"all requested installed catalog rows parsed");ConquerItemInformation.BaseInformations=catalog;
        for(int i=0;i<high.Length;i++){
            Check(project(high[i],slots[i])==low[i],"installed family/quality cap: "+high[i]);
            Check(project(high[i],(ushort)(slots[i]+20))==low[i],"alternate slot cap: "+high[i]);
            Check(project(low[i],slots[i])==low[i],"at-cap item retained: "+low[i]);
        }
        Check(project(999999,9)==999999,"unlisted decorative slot is not projected");
        foreach(uint id in catalog.Keys.Where(id=>id/1000==117&&id!=high[0]).ToArray())catalog.Remove(id);bool missing=false;try{project(high[0],1);}catch(TargetInvocationException e){missing=e.InnerException is InvalidOperationException;}
        Check(missing,"missing compatible catalog row rejects uncapped fallback");
        catalog[117098]=new ConquerItemBaseInformation{ID=117098,Level=112};missing=false;try{project(high[0],1);}catch(TargetInvocationException e){missing=e.InnerException is InvalidOperationException;}
        Check(missing,"different quality cannot substitute for missing capped entry");
        catalog[117099]=new ConquerItemBaseInformation{ID=117099,Level=112,Class=99};missing=false;try{project(high[0],1);}catch(TargetInvocationException e){missing=e.InnerException is InvalidOperationException;}
        Check(missing,"different class cannot substitute for missing capped entry");
    }
    static void PreflightAndHp(GameState p){
        p.Entity.Name="SkillFixture";
        foreach(var member in p.Team.Players)((Revora.Network.Sockets.ClientWrapper)typeof(GameState).GetField("_socket",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(member)).Alive=false;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var validate=typeof(GameState).GetMethod("ValidateSkillEquipment",flags);
        var finish=typeof(GameState).GetMethod("FinishSkillStats",flags);
        var item=new Revora.Network.GamePackets.ConquerItem(true){ID=130999,Position=3,Durability=100};
        p.Equipment=new Equipment(p);p.Equipment.Objects[2]=item;p.Weapons=new Tuple<Revora.Network.GamePackets.ConquerItem,Revora.Network.GamePackets.ConquerItem>(null,null);
        Check(!(bool)validate.Invoke(p,null),"equipped missing catalog row fails preflight");
        p.Entity.InSkillPk=true;p.Entity.BaseMinAttack=987;p.Entity.Hitpoints=100;
        bool failed=false;try{p.LoadItemStats();}catch(InvalidOperationException){failed=true;}
        Check(failed&&p.Entity.BaseMinAttack==987&&p.Entity.Hitpoints==100&&!item.IsWorn,"preflight refuses recalculation before resetting stats or worn flags");
        item.Position=23;p.Equipment.Objects[2]=null;p.Equipment.Objects[22]=item;
        Check((bool)validate.Invoke(p,null),"inactive alternate equipment cannot block preflight");
        p.Equipment.Objects[22]=null;item.Position=4;p.Equipment.Objects[3]=item;
        Check(!(bool)validate.Invoke(p,null),"selected weapon is checked even when cached Weapons is empty");
        p.Equipment.Objects[3]=null;
        p.Entity.MaxHitpoints=60;finish.Invoke(p,new object[]{true,100u});Check(p.Entity.Hitpoints==60,"capped recalculation clamps HP to effective maximum");
        finish.Invoke(p,new object[]{true,25u});Check(p.Entity.Hitpoints==25,"recalculation never heals damage");
        p.Entity.MaxHitpoints=100;finish.Invoke(p,new object[]{false,25u});Check(p.Entity.Hitpoints==25,"normal profile restoration retains current HP");
        p.Entity.Hitpoints=90;finish.Invoke(p,new object[]{false,10u});Check(p.Entity.Hitpoints==90,"unrelated normal recalculation is unchanged");
        finish.Invoke(p,new object[]{true,0u});Check(p.Entity.Hitpoints==0,"stat recalculation cannot revive a dead fighter");p.Entity.InSkillPk=false;
    }
    static void FullProfile(GameState p){
        p.Entity.FullyLoaded=false;typeof(Entity).GetField("_level",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p.Entity,(byte)140);p.Entity.Class=15;p.Entity.Strength=100;p.Entity.Vitality=100;p.Entity.Agility=20;p.Entity.Spirit=20;p.Entity.FullyLoaded=true;
        p.Spells=new SafeDictionary<ushort,Revora.Interfaces.ISkill>();
        p.Entity.SubClasses.Classes.Clear();p.Entity.SubClasses.Classes[9]=new SubClass{ID=9,Level=9,Phase=9};
        p.ChiPowers[0].Attributes=new[]{new ChiAttribute(Enums.ChiAttribute.HPAdd,3500)};
        p.Equipment=new Equipment(p);var item=new Revora.Network.GamePackets.ConquerItem(true){ID=130309,Position=3,Durability=100,Plus=12,Bless=7,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13};p.Equipment.Objects[2]=item;
        p.Entity.InSkillPk=false;p.Entity.NobilityRank=NobilityRank.King;p.Entity.Hitpoints=100;p.LoadItemStats();
        uint normalHp=p.Entity.MaxHitpoints,normalAttack=p.Entity.MinAttack;ushort normalBless=p.Entity.ItemBless;
        p.Entity.Hitpoints=normalHp;p.Entity.InSkillPk=true;p.LoadItemStats();
        uint skillKingHp=p.Entity.MaxHitpoints,skillKingAttack=p.Entity.MinAttack;
        Check(skillKingHp<normalHp&&p.Entity.Hitpoints==skillKingHp&&p.Entity.ItemBless==3,"complete Skill recalculation lowers max HP and clamps current HP with equipment caps");
        p.Entity.NobilityRank=NobilityRank.Earl;p.LoadItemStats();
        Check(p.Entity.MaxHitpoints==skillKingHp&&p.Entity.MinAttack==skillKingAttack,"Skill King and Earl use identical capped nobility combat bonuses");
        Func<string> profile=()=>string.Join(",",new object[]{p.Entity.MaxHitpoints,p.Entity.MaxMana,p.Entity.MinAttack,p.Entity.MaxAttack,p.Entity.MagicAttack,p.Entity.Defence,p.Entity.BaseMagicDefence,p.Entity.CriticalStrike,p.Entity.SkillCStrike,p.Entity.Immunity,p.Entity.Breaktrough,p.Entity.Counteraction,p.Entity.PhysicalDamageIncrease,p.Entity.MagicDamageIncrease,p.Entity.PhysicalDamageDecrease,p.Entity.MagicDamageDecrease,p.Entity.BattlePower});
        string earlProfile=profile();
        foreach(var rank in new[]{NobilityRank.Duke,NobilityRank.Prince,NobilityRank.King}){
            p.Entity.NobilityRank=rank;p.LoadItemStats();Check(profile()==earlProfile,"all Skill nobility combat contributions equal Earl: "+rank);
        }
        p.Entity.NobilityRank=NobilityRank.King;p.Entity.Hitpoints=100;
        typeof(GameState).GetMethod("RestoreSkillStats",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        Check(!p.Entity.InSkillPk&&p.Entity.MaxHitpoints==normalHp&&p.Entity.MinAttack==normalAttack&&p.Entity.ItemBless==normalBless&&p.Entity.Hitpoints==100,"whole-character explicit return restores normal profile without healing");
        p.LoadItemStats();Check(p.Entity.MaxHitpoints==normalHp&&p.Entity.MinAttack==normalAttack,"repeated normal recalculation does not accumulate bonuses");
        string ordinaryProfile=profile();
        for(int cycle=0;cycle<3;cycle++){
            p.Entity.InSkillPk=true;p.LoadItemStats();Check(profile()==earlProfile&&p.Entity.Hitpoints==100,"repeated Skill entry remains stable without healing");
            typeof(GameState).GetMethod("RestoreSkillStats",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
            Check(profile()==ordinaryProfile&&p.Entity.Hitpoints==100,"repeated normal return restores the complete profile");
        }

        Check(item.ID==130309&&item.Plus==12&&item.Bless==7&&(byte)item.SocketTwo==13&&p.Entity.SubClasses.Classes[9].Phase==9&&p.ChiPowers[0].Attributes[0].Value==3500,"complete transition preserves gear subclass and Chi source state");
    }
    static void AllEquipmentLevels(string catalogPath){
        var normalize=typeof(ConquerItemInformation).GetMethod("NormalizeItemtype",Hidden);
        var lines=(List<string>)normalize.Invoke(null,new object[]{System.IO.File.ReadAllLines(catalogPath)});
        string sub=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(catalogPath),"ItemtypeSub.txt");if(System.IO.File.Exists(sub))lines.AddRange(System.IO.File.ReadAllLines(sub));
        var catalog=new SafeDictionary<uint,ConquerItemBaseInformation>();int excluded=0;
        foreach(string line in lines){
            int end=line.IndexOf("@@",StringComparison.Ordinal);uint id;if(end<1||!uint.TryParse(line.Substring(0,end),out id))continue;
            int slot=(int)Revora.Network.GamePackets.ItemHandler.GetPositionFromID(id);if(!(slot>=1&&slot<=6||slot==8))continue;
            var row=new ConquerItemBaseInformation();try{row.Parse(line);}catch(FormatException){excluded++;continue;}catch(OverflowException){excluded++;continue;}
            if(!catalog.ContainsKey(id))catalog[id]=row;
        }
        ConquerItemInformation.BaseInformations=catalog;var project=typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkCombat").GetMethod("SkillItemType",Hidden);
        var missing=new List<uint>();int projected=0;
        foreach(var row in catalog.Values){
            byte slot=(byte)Revora.Network.GamePackets.ItemHandler.GetPositionFromID(row.ID);uint family=row.ID/1000;
            int limit=family==117?112:family==121||family==150?116:family==151||family==152?117:120;
            try {
                uint id=(uint)project.Invoke(null,new object[]{new Revora.Network.GamePackets.ConquerItem(true){ID=row.ID,Position=slot}});var effective=catalog[id];
                if(row.Level<=limit){if(id!=row.ID)throw new InvalidOperationException("Under-cap item was replaced: "+row.ID);}
                else {projected++;if(effective.Level>limit||id/1000!=family||id%10!=row.ID%10||effective.Class!=row.Class||effective.Gender!=row.Gender)throw new InvalidOperationException("Incompatible equipment projection: "+row.ID);}
            }catch(TargetInvocationException e){if(!(e.InnerException is InvalidOperationException))throw;missing.Add(row.ID);}
        }
        Console.WriteLine("Installed equipment audit: rows="+catalog.Count+" families="+catalog.Values.Select(row=>row.ID/1000).Distinct().Count()+" projected="+projected+" parse_excluded="+excluded+" missing="+missing.Count);
        if(missing.Count>0)Console.WriteLine("Missing capped rows: "+string.Join(",",missing.Take(30)));
        Check(missing.Count==1&&missing[0]==619301,"installed catalog has exactly one known refused projection: Hossu 619301; all other equipment resolves");
    }
    static int Main(string[] args){
        var map=Empty<Map>();map.ID=9200;map.BaseID=700;Kernel.Maps[map.ID]=map;
        var p=new GameState(null);p.Account=new AccountTable(null);p.Account.State=AccountTable.AccountState.Player;
        p.Entity=new Entity(EntityFlag.Player,false);p.Entity.Owner=p;p.Entity.SendUpdates=false;p.Entity.UID=920001;p.Entity.Hitpoints=100;
        p.Inventory=new Inventory(p);p.ItemSyncRoot=new object();p.Action=2;p.Fake=false;p.BindWorldMap(map.ID);p.Entity.FullyLoaded=true;Kernel.GamePool[p.Entity.UID]=p;
        p.ChiPowers=new List<ChiPowerStructure>{new ChiPowerStructure(Enums.ChiPowerType.Dragon)};p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};
        var team=Empty<Team>();team.UID=41;team.Lider=p;team.Members=new ConcurrentDictionary<uint,Team.Member>();team.Members[p.Entity.UID]=new Team.Member{entry=p,Lider=true};p.Team=team;
        team.EliteFighterStats=new TeamElitePk.FighterStats(p.Entity.UID,"fixture",0,team);team.EliteFighterStats.Flag=TeamElitePk.FighterStats.StatusFlag.Fighting;
        var match=Empty<TeamElitePk.Match>();match.ID=100000;match.T_ID=TeamElitePk.GamePackets.SkillElitePkBrackets;match.Map=map;match.Inside=true;match.MatchStats=new[]{team.EliteFighterStats};team.EliteMatch=match;
        var bracket=Empty<TeamElitePk>();bracket.Alive=true;bracket.Teams=new ConcurrentDictionary<uint,Team>();bracket.Teams[team.UID]=team;bracket.Matches=new ConcurrentDictionary<uint,TeamElitePk.Match>();bracket.Matches[match.ID]=match;
        TeamElitePk.SkillTeamTournament.Tournaments=new[]{bracket};
        foreach(Enums.ChiAttribute type in Enum.GetValues(typeof(Enums.ChiAttribute)))if(type!=Enums.ChiAttribute.None){
            ushort max=checked((ushort)Enums.ChiMaxValues(type)),low=checked((ushort)(max*40/100));int factor=(int)type<=3?10:1;
            p.Entity.InSkillPk=false;Check(Apply(p,type,max)==max*factor,"ordinary maximum remains unchanged: "+type);
            p.Entity.InSkillPk=true;Check(Apply(p,type,max)==max*60/100*factor,"owned Skill maximum is capped: "+type);
            Check(Apply(p,type,low)==low*factor,"below-cap Chi is not reduced: "+type);
            Check(Apply(p,type,max,50)==max*60/100*factor/2,"existing percentage is applied after the cap: "+type);
        }
        Action<Action,Action,string> outside=(set,reset,name)=>{set();Check(Apply(p,Enums.ChiAttribute.HPAdd,3500)==3500,name);reset();};
        outside(()=>p.Fake=true,()=>p.Fake=false,"fake actor cannot acquire Skill stats");
        outside(()=>Kernel.GamePool.Remove(p.Entity.UID),()=>Kernel.GamePool[p.Entity.UID]=p,"uninstalled session cannot acquire Skill stats");
        outside(()=>match.T_ID=TeamElitePk.GamePackets.TeamElitePkBrackets,()=>match.T_ID=TeamElitePk.GamePackets.SkillElitePkBrackets,"regular Team PK is not normalized");
        outside(()=>match.Done=true,()=>match.Done=false,"completed match returns normal contribution");
        outside(()=>match.Inside=false,()=>match.Inside=true,"unimported match returns normal contribution");
        outside(()=>bracket.Alive=false,()=>bracket.Alive=true,"inactive bracket returns normal contribution");
        outside(()=>bracket.Matches.Clear(),()=>bracket.Matches[match.ID]=match,"unowned match identity is refused");
        outside(()=>bracket.Teams[team.UID]=Empty<Team>(),()=>bracket.Teams[team.UID]=team,"same UID cannot replace the registered team");
        outside(()=>team.Members.Clear(),()=>team.Members[p.Entity.UID]=new Team.Member{entry=p,Lider=true},"removed member cannot retain Skill stats");
        outside(()=>team.EliteFighterStats.Flag=TeamElitePk.FighterStats.StatusFlag.Waiting,()=>team.EliteFighterStats.Flag=TeamElitePk.FighterStats.StatusFlag.Fighting,"waiting member is not normalized");
        outside(()=>match.Map=Empty<Map>(),()=>match.Map=map,"another map object cannot confer arena ownership");
        outside(()=>match.ID=0,()=>match.ID=100000,"negative derived bracket is refused");
        outside(()=>match.ID=500000,()=>match.ID=100000,"absent derived bracket is refused");
        Check(Apply(p,Enums.ChiAttribute.HPAdd,3500)==2100,"restored canonical match restores only the effective cap");
        p.Entity.InSkillPk=false;Check(Apply(p,Enums.ChiAttribute.HPAdd,3500)==3500,"leaving Skill scope recovers the original contribution");
        SubclassChecks(p);
        BattlePowerChecks(p);
        EquipmentChecks(p);
        if(args.Length!=1)throw new InvalidOperationException("Pass the private installed item catalog path.");
        EquipmentLevels(args[0]);
        PreflightAndHp(p);
        FullProfile(p);
        AllEquipmentLevels(args[0]);
        p.Equipment=new Equipment(p);p.Entity.FullyLoaded=false;p.Entity.Class=135;p.Entity.FullyLoaded=true;
        var hossu=new Revora.Network.GamePackets.ConquerItem(true){UID=98701,ID=619301,Position=5,Durability=100,Plus=12};
        p.Equipment.Objects[3]=new Revora.Network.GamePackets.ConquerItem(true){ID=421309,Position=4,Durability=100};
        p.Equipment.Objects[4]=hossu;
        var issue=typeof(GameState).GetMethod("SkillEquipmentIssue",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(((string)issue.Invoke(p,null)).Contains("Unequip SoaringSpirit"),"unsupported equipped Hossu gives the approved actionable explanation");
        Check(hossu.ID==619301&&hossu.Plus==12&&hossu.Position==5&&p.Equipment.Objects[4]==hossu,"refusal does not alter or remove owned Hossu");
        p.Equipment.Objects[4]=null;
        Check(issue.Invoke(p,null)==null,"unequipping unsupported Hossu restores admission eligibility");
        Console.WriteLine("Skill combat scope/contribution checks passed: "+checks);return 0;
    }
}
