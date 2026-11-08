using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime ChampionFixtureSignupTime(uint uid) {
        var previous=Sql("SELECT max(event_day) FROM revora_champion_entry_players WHERE uid=@uid","uid",(long)uid);
        var day=previous==DBNull.Value || previous==null?new DateTime(2026,10,5):Convert.ToDateTime(previous).Date.AddDays(1);
        while(day.DayOfWeek!=DayOfWeek.Monday)day=day.AddDays(1);
        return day.AddHours(20).AddMinutes(55);
    }
    static bool championCapsFixture;
    static void ChampionCapsProcess(){
        championCapsFixture=true;const uint uid=4294913701;
        if((bool)Sql("SELECT to_regclass('revora_champion_caps_probe') IS NULL")){
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0);nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);Sql("CREATE TABLE revora_champion_caps_probe(checked_at timestamptz)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);p.LoadItemStats();other.LoadItemStats();WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var engine=typeof(Revora.Game.Champion.QualifyEngine);
        var registrationClock=engine.GetField("RegistrationClock",Hidden);
        var oldClock=registrationClock.GetValue(null);
        var monday=ChampionFixtureSignupTime(uid);
        try {
            foreach(var time in new[]{monday.AddSeconds(-1),monday.AddMinutes(5),monday.AddDays(5),monday.AddDays(6)}) {
                registrationClock.SetValue(null,new Func<DateTime>(()=>time));
                Revora.Game.Champion.QualifyEngine.DoSignup(p);Require(!p.ChampionStats.SignedUp && !Revora.Game.Champion.PlayerList.ContainsKey(uid));
            }
            registrationClock.SetValue(null,new Func<DateTime>(()=>monday));
            Require((bool)Call(engine,"CanRegister",p));
            byte level=p.Entity.Level;p.Entity.Level=69;Require(!(bool)Call(engine,"CanRegister",p));p.Entity.Level=level;
            p.Fake=true;Require(!(bool)Call(engine,"CanRegister",p));p.Fake=false;
            Revora.Game.Champion.WaitingPlayerList.TryAdd(uid,other);Revora.Game.Champion.QualifyEngine.DoSignup(p);
            Require(!p.ChampionStats.SignedUp && ReferenceEquals(Revora.Game.Champion.WaitingPlayerList[uid],other));
            GameState removed;Revora.Game.Champion.WaitingPlayerList.TryRemove(uid,out removed);
            Revora.Game.Champion.PlayerList.TryAdd(uid,other);Revora.Game.Champion.QualifyEngine.DoSignup(p);
            Require(!p.ChampionStats.SignedUp && ReferenceEquals(Revora.Game.Champion.PlayerList[uid],other));
            Revora.Game.Champion.PlayerList.TryRemove(uid,out removed);
            System.Threading.Tasks.Parallel.For(0,8,i=>Revora.Game.Champion.QualifyEngine.DoSignup(p));
            Require(p.ChampionStats.SignedUp && ReferenceEquals(Revora.Game.Champion.PlayerList[uid],p));
            Require(p.ArenaState==Revora.Game.Champion.FindMatch);
            var position=p.Entity.MapID+":"+p.Entity.X+":"+p.Entity.Y;
            uint points=p.ChampionStats.Points, otherPoints=other.ChampionStats.Points;
            Revora.Game.Champion.QualifyEngine.DoQuit(p);
            Require(!p.ChampionStats.SignedUp && !Revora.Game.Champion.PlayerList.ContainsKey(uid));
            Require(position==p.Entity.MapID+":"+p.Entity.X+":"+p.Entity.Y && p.ChampionStats.Points==points);
            Revora.Game.Champion.QualifyEngine.DoQuit(p); // repeated cancellation is inert
            Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
            Require(p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);
            other.ChampionStats.Grade=p.ChampionStats.Grade;
            p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            registrationClock.SetValue(null,new Func<DateTime>(()=>monday.AddMinutes(5)));
            Revora.Game.Champion.EngagePlayers();
            Require(ReferenceEquals(p.ChampionStats.Opponent,other) && ReferenceEquals(other.ChampionStats.Opponent,p));
            Require(Revora.Game.Champion.WaitingPlayerList.Count==2);
            Revora.Game.Champion.QualifyEngine.DoAccept(p);Require(p.ChampionStats.AcceptBox && p.ArenaState==Revora.Game.Champion.WaitForOther);
            Revora.Game.Champion.QualifyEngine.DoGiveUp(other);
            Require(p.ChampionStats.Opponent==null && other.ChampionStats.Opponent==null && p.ChampionGroup==null && other.ChampionGroup==null);
            Require(p.ChampionStats.SignedUp && !other.ChampionStats.SignedUp && Revora.Game.Champion.WaitingPlayerList.Count==0);
            Require(p.ChampionStats.Points==points && other.ChampionStats.Points==otherPoints);
            Revora.Game.Champion.QualifyEngine.DoSignup(other);Require(!other.ChampionStats.SignedUp);
            Revora.Game.Champion.Clear(p);registrationClock.SetValue(null,new Func<DateTime>(()=>monday));
            Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
            registrationClock.SetValue(null,new Func<DateTime>(()=>monday.AddMinutes(10)));
            p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
            p.ChampionStats.AcceptBoxShow=other.ChampionStats.AcceptBoxShow=Time32.Now.AddSeconds(-61);
            Revora.Game.Champion.VerifyAwaitingPeople();
            Require(Revora.Game.Champion.WaitingPlayerList.Count==0 && p.ChampionGroup==null && other.ChampionGroup==null);
            Require(p.ChampionStats.Points==points && other.ChampionStats.Points==otherPoints);
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
            Revora.Game.Champion.PlayerList.TryAdd(uid,other);Revora.Game.Champion.Clear(p);
            Require(ReferenceEquals(Revora.Game.Champion.PlayerList[uid],other));
            Revora.Game.Champion.PlayerList.TryRemove(uid,out removed);
            var unrelated=(Revora.Game.Qualifier.QualifierList.QualifierGroup)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.Qualifier.QualifierList.QualifierGroup));
            p.QualifierGroup=unrelated;p.ArenaState=Revora.Game.Qualifier.Fight;
            Revora.Game.Champion.Clear(p);
            Require(ReferenceEquals(p.QualifierGroup,unrelated) && p.ArenaState==Revora.Game.Qualifier.Fight);
            p.QualifierGroup=null;p.ArenaState=Revora.Game.Champion.None;
            p.ChampionStats.AcceptBox=false;Revora.Game.Champion.QualifyEngine.DoAccept(p);Require(!p.ChampionStats.AcceptBox);
            Revora.Game.Champion.Win(null,p);Require(p.ChampionStats.Points==points);

        } finally {
            registrationClock.SetValue(null,oldClock);GameState removed;
            Revora.Game.Champion.PlayerList.TryRemove(uid,out removed);Revora.Game.Champion.WaitingPlayerList.TryRemove(uid,out removed);
            Revora.Game.Champion.Clear(other);
            p.ChampionStats.SignedUp=false;p.ArenaState=Revora.Game.Champion.None;
        }
        ChampionEntryChecks(p,other);
        var boots=p.Equipment.TryGetItem(8);
        if(boots==null){uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==9 && i.Level>=15).OrderBy(i=>i.Level).First().ID;
            ushort durability=ConquerItemInformation.BaseInformations[type].Durability;
            boots=new ConquerItem(true){ID=type,Bound=true,StackSize=1,Enchant=255,Durability=durability,MaximDurability=durability};
            Require(p.Inventory.Add(boots,Revora.Game.Enums.ItemUse.CreateAndAdd));RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(8).UID==boots.UID);
        }
        foreach(var id in new byte[]{(byte)Revora.Game.ClassID.Apothecary,(byte)Revora.Game.ClassID.ChiMaster})
            if(!p.Entity.SubClasses.Classes.ContainsKey(id))p.Entity.SubClasses.Classes.Add(id,new Revora.Game.SubClass{ID=id,Level=9,Phase=9});
        p.LoadItemStats();Require(p.Entity.Detoxication==72 && p.Entity.Immunity>=1500);int normalImmunity=p.Entity.Immunity;
        uint normalHp=p.Entity.ItemHP;int normalBp=p.Entity.BattlePower;int noble=(int)p.Entity.NobilityRank;Require(normalHp>=255);
        var group=new Revora.Game.Champion.QualifierList.QualifierGroup(p,other);byte oldGrade=p.ChampionStats.Grade;
        // A synthetic group activates the existing combat-stat path only. This is
        // not signup, admission, combat or a milestone earning fixture.
        try{
            p.ChampionGroup=group;int[] enchants={0,0,50,100,150,200,255};int[] nobles={0,1,2,5,7,9,12};int[] immunity={0,100,300,400,600,1000,1500};
            for(byte grade=1;grade<=7;grade++){
                p.ChampionStats.Grade=grade;p.LoadItemStats();
                Require(p.Entity.ItemHP==normalHp-255+enchants[grade-1]);
                Require(p.Entity.Detoxication==72 && p.Entity.Immunity==normalImmunity-1500+immunity[grade-1]);
                Require(p.Entity.BattlePower==normalBp-noble+Math.Min(noble,nobles[grade-1]));Require(!PirateFenced(p));
            }
            foreach(byte invalid in new byte[]{0,255}){p.ChampionStats.Grade=invalid;p.LoadItemStats();Require(p.Entity.ItemHP==normalHp-255 && p.Entity.BattlePower==normalBp-noble && p.Entity.Detoxication==72 && p.Entity.Immunity==normalImmunity-1500);}
        }finally{p.ChampionGroup=null;p.ChampionStats.Grade=oldGrade;Revora.Game.Champion.QualifierList.QualifierGroup ignored;Revora.Game.Champion.QualifierList.Groups.TryRemove(group.ID,out ignored);p.LoadItemStats();}
        Require(p.Entity.ItemHP==normalHp && p.Entity.BattlePower==normalBp);
        string before=NobilityClaimSnapshot(p);NobilityClaimPacket(p,505);Require(before==NobilityClaimSnapshot(p));
        Sql("INSERT INTO revora_champion_caps_probe VALUES(@now)","now",DateTime.UtcNow);
        System.Console.WriteLine("[career-process] career-champion-caps-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion caps did not close.");
    }
    static bool championEquipmentFixture;
    static void ChampionEquipmentProcess() {
        championEquipmentFixture=true;const uint uid=4294915201;
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=140,class=15,strength=500,agility=500 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        SkillTable.LoadProficiencies(p);
        if(!p.Proficiencies.ContainsKey(410))p.AddProficiency(new Proficiency(true){ID=410,Level=20});
        ChampionEquipmentChecks(p,other,160,8,120);
        ChampionEquipmentChecks(p,other,410,4,120);
        ChampionEquipmentChecks(p,other,117,1,112);
        ChampionEquipmentChecks(p,other,120,2,120);
        ChampionSupplementalStats(p,other);
        ChampionModernStats(p,other);
        System.Console.WriteLine("[career-process] career-champion-equipment-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion equipment did not close.");
    }
    static void ChampionModernStats(GameState p,GameState other) {
        var body=p.Entity;var original=p.Equipment.TryGetItem(8);var originalInner=p.InnerPower;var originalJiang=body.MyJiang;byte oldGrade=p.ChampionStats.Grade;
        var boots=p.Inventory.Objects.First(item=>item.ID/1000==160 && item.Plus==12);var originalPerfection=boots.PerfectionLevel;
        var group=new Revora.Game.Champion.QualifierList.QualifierGroup(p,other);
        Revora.Game.InnerPower zeroInner;bool hadZero=Revora.Game.InnerPower.InnerPowerPolle.TryGetValue(0,out zeroInner);
        var inner=new Revora.Game.InnerPower("Champion stat fixture",0);
        var innerStats=typeof(Revora.Game.InnerPower).GetProperties().Where(prop=>prop.PropertyType==typeof(uint) && prop.GetSetMethod(true)!=null).ToArray();
        foreach(var prop in innerStats)prop.GetSetMethod(true).Invoke(inner,new object[]{1000u});
        var jiang=new Revora.Game.JiangHu(0);
        var attribute=Revora.Database.JiangHu.Attributes.Where(pair=>pair.Value.Type==(byte)Revora.Game.JiangHu.JiangStages.AtributesType.PDefense && pair.Value.Power>0).OrderByDescending(pair=>pair.Value.Power).First();
        jiang.Stagers[0].Activate=true;jiang.Stagers[0].Stars[0]=new Revora.Game.JiangHu.JiangStages.Star{Activate=true,UID=attribute.Key,Level=attribute.Value.Level,Typ=Revora.Game.JiangHu.JiangStages.AtributesType.PDefense};
        var relic=typeof(GameState).GetMethod("HandleRelic",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(relic!=null);
        Action clear=()=>{p.InnerPower=null;body.MyJiang=null;boots.PerfectionLevel=0;};
        string stored=Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@uid","uid",(long)boots.UID));
        try {
            if(original!=null)RetainedUpgradeUsage(p,original.UID,8,ItemUsage.Mode.UnequipItem);
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(8).UID==boots.UID);
            for(byte grade=1;grade<=7;grade++) {
                p.ChampionStats.Grade=grade;
                foreach(string source in new[]{"InnerPower","JiangHu","Perfection","Relic"}) {
                    clear();p.ChampionGroup=group;p.LoadItemStats();string baseline=ChampionSupplementalVector(p);
                    Action supply=()=>{if(source=="InnerPower")p.InnerPower=inner;else if(source=="JiangHu")body.MyJiang=jiang;else if(source=="Perfection")boots.PerfectionLevel=54;};
                    supply();p.LoadItemStats();if(source=="Relic")relic.Invoke(p,new object[]{100006});
                    Require(ChampionSupplementalVector(p)==baseline);
                    // Direct refresh APIs cannot put suppressed stats back into an active match.
                    if(source=="InnerPower")p.AddInnerPowerAtributes(inner);
                    if(source=="JiangHu"){jiang.IncreaseStatus(body,Revora.Game.JiangHu.JiangStages.AtributesType.PDefense,1000);jiang.DecreaseStatus(body,Revora.Game.JiangHu.JiangStages.AtributesType.PDefense,1000);}
                    Require(ChampionSupplementalVector(p)==baseline);
                    clear();p.ChampionGroup=null;p.LoadItemStats();string outside=ChampionSupplementalVector(p);
                    supply();p.LoadItemStats();if(source=="Relic")relic.Invoke(p,new object[]{100006});
                    Require(ChampionSupplementalVector(p)!=outside);
                    p.ChampionGroup=group;p.LoadItemStats();Require(ChampionSupplementalVector(p)==baseline);
                    System.Console.WriteLine("[champion-modern] grade="+grade+" source="+source+" suppressed inside and restored outside");
                }
            }
            boots.PerfectionLevel=54;int tpl=p.Equipment.GetTPL();Require(tpl>0);
            PerfectionManager.RefineEffect savedEffect;bool hadEffect=PerfectionManager.ItemsRefineEffects.TryGetValue(tpl,out savedEffect);
            try {
                // Deterministic fixture effect proves that the production proc boundary is gated.
                PerfectionManager.ItemsRefineEffects[tpl]=new PerfectionManager.RefineEffect{LuckyStrike=100};
                uint damage=1000;p.ChampionGroup=group;PerfectionManager.DoEffects(body,other.Entity,ref damage);Require(damage==1000);
                p.ChampionGroup=null;PerfectionManager.DoEffects(body,other.Entity,ref damage);Require(damage!=1000);
                System.Console.WriteLine("[champion-modern] Perfection proc suppressed in Champion and active outside");
            } finally {if(hadEffect)PerfectionManager.ItemsRefineEffects[tpl]=savedEffect;else PerfectionManager.ItemsRefineEffects.Remove(tpl);}
            Require(innerStats.All(prop=>(uint)prop.GetValue(inner,null)==1000) && jiang.Stagers[0].Stars[0].UID==attribute.Key);
        } finally {
            p.ChampionGroup=null;p.ChampionStats.Grade=oldGrade;p.InnerPower=originalInner;body.MyJiang=originalJiang;boots.PerfectionLevel=originalPerfection;
            if(p.Equipment.TryGetItem(8)!=null)RetainedUpgradeUsage(p,p.Equipment.TryGetItem(8).UID,8,ItemUsage.Mode.UnequipItem);
            if(original!=null)RetainedUpgradeUsage(p,original.UID,8,ItemUsage.Mode.EquipItem);
            Revora.Game.Champion.QualifierList.QualifierGroup ignored;Revora.Game.Champion.QualifierList.Groups.TryRemove(group.ID,out ignored);
            if(!hadZero){Revora.Game.InnerPower removed;Revora.Game.InnerPower.InnerPowerPolle.TryRemove(0,out removed);}
            p.LoadItemStats();
        }
        Require(stored==Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@uid","uid",(long)boots.UID)));
    }
    static string ChampionSupplementalVector(GameState p) {
        return ChampionEquipmentVector(p)+":"+string.Join(":",new object[]{p.Entity.MaxHitpoints,p.Entity.MaxMana,p.Entity.MinAttack,p.Entity.MaxAttack,p.Entity.MagicAttack,p.Entity.BaseMagicAttack,p.Entity.BaseMagicDefence,p.Entity.ItemMP,p.Entity.CriticalStrike,p.Entity.SkillCStrike,p.Entity.Immunity,p.Entity.Breaktrough,p.Entity.Counteraction,p.Entity.PhysicalDamageIncrease,p.Entity.PhysicalDamageDecrease,p.Entity.MagicDamageIncrease,p.Entity.MagicDamageDecrease});
    }
    static void ChampionSupplementalStats(GameState p,GameState other) {
        var body=p.Entity;var oldRank=body.NobilityRank;byte oldGrade=p.ChampionStats.Grade;
        var vip=typeof(Revora.Game.Entity).GetField("_vipLevel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(vip!=null);var oldVip=vip.GetValue(body);
        var flags=new[]{"IncreaseFinalMDamage","IncreaseFinalPDamage","IncreaseFinalMAttack","IncreaseFinalPAttack","IncreaseImunity","IncreaseAntiBreack","IncreasePStrike","IncreaseBreack","IncreaseAttribute"}.Select(name=>typeof(Revora.Game.Entity).GetField(name)).ToArray();Require(flags.All(field=>field!=null));
        var oldFlags=flags.Select(field=>(bool)field.GetValue(body)).ToArray();
        var group=new Revora.Game.Champion.QualifierList.QualifierGroup(p,other);
        string stored=Convert.ToString(Sql("SELECT row_to_json(e)::text FROM entities e WHERE uid=@uid","uid",(long)body.UID));
        try {
            for(byte grade=1;grade<=7;grade++) {
                p.ChampionStats.Grade=grade;p.ChampionGroup=group;vip.SetValue(body,(byte)0);body.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;foreach(var flag in flags)flag.SetValue(body,false);p.LoadItemStats();
                string baseline=ChampionSupplementalVector(p);uint hp=body.MaxHitpoints;
                vip.SetValue(body,(byte)7);body.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.King;foreach(var flag in flags)flag.SetValue(body,true);p.LoadItemStats();
                Require(ChampionSupplementalVector(p)==baseline && body.VIPLevel==7 && body.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.King && flags.All(flag=>(bool)flag.GetValue(body)));
                p.ChampionGroup=null;p.LoadItemStats();Require(body.MaxHitpoints>hp && ChampionSupplementalVector(p)!=baseline);
                p.ChampionGroup=group;p.LoadItemStats();Require(ChampionSupplementalVector(p)==baseline);
                System.Console.WriteLine("[champion-supplemental] grade="+grade+" VIP/nobility/boost stats excluded; sources retained and restored outside arena");
            }
        } finally {
            p.ChampionGroup=null;p.ChampionStats.Grade=oldGrade;vip.SetValue(body,oldVip);body.NobilityRank=oldRank;
            for(int i=0;i<flags.Length;i++)flags[i].SetValue(body,oldFlags[i]);p.LoadItemStats();
            Revora.Game.Champion.QualifierList.QualifierGroup ignored;Revora.Game.Champion.QualifierList.Groups.TryRemove(group.ID,out ignored);
        }
        Require(stored==Convert.ToString(Sql("SELECT row_to_json(e)::text FROM entities e WHERE uid=@uid","uid",(long)body.UID)));
    }
    static string ChampionEquipmentVector(GameState p) {
        return string.Join(":",new object[]{p.Entity.BaseMinAttack,p.Entity.BaseMaxAttack,p.Entity.ItemHP,
            p.Entity.Defence,p.Entity.BaseDefence,p.Entity.MagicDefence,p.Entity.MagicDefencePercent,
            p.Entity.Dodge,p.Entity.Accuracy,p.Accuracy,p.AgilityItem,p.MagicDefence});
    }
    static void ChampionEquipmentChecks(GameState p,GameState other,uint family,byte slot,byte capLevel) {
        var catalog=ConquerItemInformation.BaseInformations;
        uint highType=catalog.Values.Where(i=>i.ID/1000==family && i.ID%10==9 && i.Level>capLevel && i.Level<=140 && i.Class==0 && i.Gender==0).OrderByDescending(i=>i.Level).First().ID;
        uint cappedType=catalog.Values.Where(i=>i.ID/1000==family && i.ID%10==9 && i.Level<=capLevel && i.Class==0 && i.Gender==0).OrderByDescending(i=>i.Level).First().ID;
        var original=p.Equipment.TryGetItem(slot);byte level=p.Entity.Level,oldGrade=p.ChampionStats.Grade;
        var group=new Revora.Game.Champion.QualifierList.QualifierGroup(p,other);
        var vip=typeof(Revora.Game.Entity).GetField("_vipLevel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var oldVip=vip.GetValue(p.Entity);var oldRank=p.Entity.NobilityRank;
        Func<uint,byte,byte,ConquerItem> gear=(type,plus,sockets)=>{
            var found=p.Inventory.Objects.FirstOrDefault(i=>i.ID==type && i.Plus==plus && i.Enchant==0 && (byte)i.SocketOne==(sockets>=1?13:0) && (byte)i.SocketTwo==(sockets>=2?13:0));
            if(found!=null)return found;
            var item=new ConquerItem(true){ID=type,Plus=plus,Bound=true,StackSize=1,SocketOne=(Revora.Game.Enums.Gem)(sockets>=1?13:0),SocketTwo=(Revora.Game.Enums.Gem)(sockets>=2?13:0),Durability=catalog[type].Durability,MaximDurability=catalog[type].Durability};
            Require(p.Inventory.Add(item,Revora.Game.Enums.ItemUse.CreateAndAdd));return item;
        };
        Action<ConquerItem> equip=item=>{
            var current=p.Equipment.TryGetItem(slot);if(current!=null)RetainedUpgradeUsage(p,current.UID,slot,ItemUsage.Mode.UnequipItem);
            if(item!=null){RetainedUpgradeUsage(p,item.UID,slot,ItemUsage.Mode.EquipItem);if(p.Equipment.TryGetItem(slot)==null)System.Console.WriteLine("Champion fixture equip failed: slot="+slot+" type="+item.ID+" stats="+ItemHandler.EquipPassStatsReq(catalog[item.ID],p)+" class="+ItemHandler.EquipPassJobReq(catalog[item.ID],p)+" level="+p.Entity.Level+" str="+p.Entity.Strength+" agility="+p.Entity.Agility);Require(p.Equipment.TryGetItem(slot).UID==item.UID);}
        };
        try {
            vip.SetValue(p.Entity,(byte)0);p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;
            p.Entity.Level=140;var high=gear(highType,12,2);
            string stored=Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@uid","uid",(long)high.UID));
            int[] plus={1,2,3,4,5,6,12},sockets={0,0,0,1,1,1,2},nobles={0,1,2,5,7,9,12};
            for(byte grade=1;grade<=7;grade++) {
                p.ChampionGroup=null;equip(gear(cappedType,(byte)plus[grade-1],(byte)sockets[grade-1]));p.LoadItemStats();
                string expected=ChampionEquipmentVector(p);int normalBp=p.Entity.BattlePower,noble=(int)p.Entity.NobilityRank;
                equip(high);p.ChampionStats.Grade=grade;p.ChampionGroup=group;p.LoadItemStats();
                string actual=ChampionEquipmentVector(p);
                System.Console.WriteLine("[champion-equipment] slot="+slot+" grade="+grade+" expected="+expected+" actual="+actual);
                Require(actual==expected);Require(p.Entity.BattlePower==normalBp-noble+Math.Min(noble,nobles[grade-1]));
                Require(high.ID==highType && high.Plus==12 && (byte)high.SocketOne==13 && (byte)high.SocketTwo==13);
            }
            // Exercise combat's block refresh directly with catalog effects. These
            // temporary effect fields are restored, not claimed as attachment tests.
            if(slot==8) {
            var refine=Kernel.DatabaseRefinery.Values.First(i=>i.Type==Refinery.RefineryItem.RefineryType.Block);
            uint soul=catalog.Values.First(i=>i.Block>0).ID;
            uint previousRefine=high.RefineItem,previousSoul=high.Purification.PurificationItemID;
            var refineryState=typeof(ConquerItem).GetField("RefineryState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            object previousState=refineryState.GetValue(high);
            try {
                refineryState.SetValue(high,null);high.RefineItem=refine.Identifier;high.Purification.PurificationItemID=soul;
                for(byte grade=1;grade<=7;grade++) {
                    p.ChampionStats.Grade=grade;p.ReloadBlock();
                    Require(p.Entity.Block==(grade>=5?refine.Percent*100:0)+(grade==7?catalog[soul].Block:0));
                }
            } finally {high.RefineItem=previousRefine;high.Purification.PurificationItemID=previousSoul;refineryState.SetValue(high,previousState);}
            }
            p.ChampionGroup=null;equip(original);
            string after=Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@uid","uid",(long)high.UID));
            if(stored!=after)System.Console.WriteLine("[champion-equipment] item-before="+stored+" item-after="+after);
            Require(stored==after);
        } finally {
            p.ChampionGroup=null;p.ChampionStats.Grade=oldGrade;p.Entity.Level=level;vip.SetValue(p.Entity,oldVip);p.Entity.NobilityRank=oldRank;
            if(p.Equipment.TryGetItem(slot)!=original)equip(original);
            Revora.Game.Champion.QualifierList.QualifierGroup ignored;Revora.Game.Champion.QualifierList.Groups.TryRemove(group.ID,out ignored);p.LoadItemStats();
        }
    }
    static void ChampionEntryChecks(GameState p,GameState other) {
        var engine=typeof(Revora.Game.Champion.QualifyEngine);
        var clock=engine.GetField("RegistrationClock",Hidden);var previous=clock.GetValue(null);
        var groupType=typeof(Revora.Game.Champion.QualifierList.QualifierGroup);
        var probe=groupType.GetField("EntryProbe",Hidden);
        uint points=p.ChampionStats.Points,otherPoints=other.ChampionStats.Points;
        var mode=p.Entity.PKMode;var otherMode=other.Entity.PKMode;
        var map=p.Map;var otherMap=other.Map;ushort x=p.Entity.X,y=p.Entity.Y,ox=other.Entity.X,oy=other.Entity.Y;
        var registration=ChampionFixtureSignupTime(p.Entity.UID);int round=0;
        Action pair=()=>{
            clock.SetValue(null,new Func<DateTime>(()=>registration));
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
            Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
            p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            round++;clock.SetValue(null,new Func<DateTime>(()=>registration.AddMinutes(round*5)));
            Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
            Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);
        };
        try {
            clock.SetValue(null,new Func<DateTime>(()=>registration));
            pair();Revora.Game.Champion.VerifyAwaitingPeople();
            var group=p.ChampionGroup;Require(group!=null && ReferenceEquals(other.ChampionGroup,group));
            Require((bool)groupType.GetProperty("OwnsImportedMap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(group,null));
            var assigned=p.Map;Require(assigned.ID!=700 && assigned.BaseID==700 && ReferenceEquals(other.Map,assigned));
            group.Import();Require(ReferenceEquals(p.Map,assigned) && ReferenceEquals(other.Map,assigned));
            group.UpdateDamage(p,50);group.End(p);group.Export();
            points=p.ChampionStats.Points;otherPoints=other.ChampionStats.Points;
            Require(p.ChampionGroup==null && other.ChampionGroup==null && !Kernel.Maps.ContainsKey(assigned.ID));
            Require(ReferenceEquals(p.Map,map) && ReferenceEquals(other.Map,otherMap) && p.Entity.X==x && p.Entity.Y==y && other.Entity.X==ox && other.Entity.Y==oy);
            Require(p.Entity.PKMode==mode && other.Entity.PKMode==otherMode);
            group.Export();Require(ReferenceEquals(p.Map,map) && p.Entity.X==x && p.Entity.Y==y);
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
            // Use the real pairing path, then fail midway through its real transfer.
            pair();int mapsBefore=Kernel.Maps.Count;
            probe.SetValue(null,new Action<string>(stage=>{if(stage=="first-arrived")throw new InvalidOperationException("fixture partial entry");}));
            Revora.Game.Champion.VerifyAwaitingPeople();
            Require(p.ChampionGroup==null && other.ChampionGroup==null && Kernel.Maps.Count==mapsBefore);
            Require(ReferenceEquals(p.Map,map) && ReferenceEquals(other.Map,otherMap) && p.Entity.X==x && p.Entity.Y==y && other.Entity.X==ox && other.Entity.Y==oy);
            Require(p.Entity.PKMode==mode && other.Entity.PKMode==otherMode && !PirateFenced(p) && !PirateFenced(other));
            Require(p.ChampionStats.Points==points && other.ChampionStats.Points==otherPoints);
            probe.SetValue(null,null);pair();
            probe.SetValue(null,new Action<string>(stage=>{if(stage=="both-arrived") {p.Entity.Teleport(map.ID,x,y);other.Entity.Teleport(otherMap.ID,ox,oy);}}));
            Revora.Game.Champion.VerifyAwaitingPeople();
            Require(p.ChampionGroup==null && other.ChampionGroup==null && Kernel.Maps.Count==mapsBefore);
            Require(ReferenceEquals(p.Map,map) && ReferenceEquals(other.Map,otherMap));
            Require(p.ChampionStats.Points==points && other.ChampionStats.Points==otherPoints);
            probe.SetValue(null,null);pair();
            probe.SetValue(null,new Action<string>(stage=>{if(stage=="first-arrived")Revora.Game.Champion.QualifyEngine.DoQuit(p);}));
            Revora.Game.Champion.VerifyAwaitingPeople();
            Require(p.ChampionGroup==null && other.ChampionGroup==null && Kernel.Maps.Count==mapsBefore);
            Require(p.ChampionStats.Points==points && other.ChampionStats.Points==otherPoints && !PirateFenced(p) && !PirateFenced(other));
        } finally {
            probe.SetValue(null,null);clock.SetValue(null,previous);
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
        }
    }

    static void ChampionEntryRecovery(string phase) {
        championCapsFixture=true;nextUid=4294913701;
        var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);
        Sql("CREATE TABLE IF NOT EXISTS revora_champion_recovery_probe(run uuid PRIMARY KEY,confirmed boolean NOT NULL,recorded_at timestamptz NOT NULL)");
        if(phase.EndsWith("reload")) {
            var run=(Guid)Sql("SELECT run FROM revora_champion_recovery_probe ORDER BY recorded_at DESC LIMIT 1");
            Require((string)Sql("SELECT state FROM revora_champion_entries WHERE id=@run","run",run)=="interrupted");
            Require((bool)Sql("SELECT (e.entered_at IS NOT NULL)=p.confirmed FROM revora_champion_entries e JOIN revora_champion_recovery_probe p ON p.run=e.id WHERE e.id=@run","run",run));
            Require((long)Sql(@"SELECT count(*) FROM revora_champion_entry_players p JOIN entities e ON e.uid=p.uid
WHERE p.run=@run AND p.released_at IS NOT NULL AND e.mapid=p.return_map AND e.x=p.return_x AND e.y=p.return_y","run",run)==2);
            Require(!p.ChampionStats.SignedUp && !other.ChampionStats.SignedUp && p.ChampionGroup==null && other.ChampionGroup==null);
            WayofHeroes.Load(p);string before=NobilityClaimSnapshot(p);NobilityClaimPacket(p,505);Require(before==NobilityClaimSnapshot(p));
            System.Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion recovery did not close.");
        }
        p.LoadItemStats();other.LoadItemStats();
        var engine=typeof(Revora.Game.Champion.QualifyEngine);var local=ChampionFixtureSignupTime(p.Entity.UID);engine.GetField("RegistrationClock",Hidden).SetValue(null,new Func<DateTime>(()=>local));
        Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
        Require(p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);
        other.ChampionStats.Grade=p.ChampionStats.Grade;p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
        local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);
        Action crash=()=>{
            var group=p.ChampionGroup;Require(group!=null);
            var run=(Guid)group.GetType().GetField("EntryKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(group);
            bool confirmed=phase.EndsWith("confirmed");
            Require((string)Sql("SELECT state FROM revora_champion_entries WHERE id=@run","run",run)==(confirmed?"entered":"reserved"));
            Require((long)Sql("SELECT count(*) FROM revora_champion_entry_players WHERE run=@run AND released_at IS NULL","run",run)==2);
            Sql("INSERT INTO revora_champion_recovery_probe VALUES(@run,@confirmed,@now)","run",run,"confirmed",confirmed,"now",DateTime.UtcNow);
            System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);
        };
        if(phase.EndsWith("partial"))typeof(Revora.Game.Champion.QualifierList.QualifierGroup).GetField("EntryProbe",Hidden).SetValue(null,new Action<string>(stage=>{if(stage=="first-arrived")crash();}));
        Revora.Game.Champion.VerifyAwaitingPeople();crash();throw new InvalidOperationException("Champion crash fixture did not exit.");
    }

    static bool championBalanceFixture;
    static void ChampionBalanceProcess(bool reload) {
        championBalanceFixture=true;const uint uid=4294913801;nextUid=uid;
        if(!reload && (bool)Sql("SELECT to_regclass('revora_champion_balance_probe') IS NULL")) {
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=0 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_champion_balance_probe(uid bigint PRIMARY KEY,points bigint,matches integer,streak integer)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
        if(!reload) {
            Require((long)Sql("SELECT count(*) FROM revora_champion_balance_probe")==0);
            // Explicit supplied balance, not an event reward.
            p.ChampionStats.Points=47;p.ChampionStats.TotalMatches=9;p.ChampionStats.WinStreak=2;p.ChampionStats.LastReset=DateTime.Now;
            ChampionTable.SaveStats(p.ChampionStats);
            Require((long)Sql("SELECT points::bigint FROM championarena WHERE entityid=@uid","uid",(long)uid)==47);
            Require(Convert.ToInt32(Sql("SELECT totalmatches FROM championarena WHERE entityid=@uid","uid",(long)uid))==9);
            var stale=p.ChampionStats;
            Sql("UPDATE championarena SET points=57,revora_revision=revora_revision+1 WHERE entityid=@uid","uid",(long)uid);
            stale.Points=0;bool refused=false;try{ChampionTable.SaveStats(stale);}catch(InvalidOperationException){refused=true;}
            Require(refused && PirateFenced(p));
            nextUid=uid;p=Player(Kernel.Maps[1002],true);Require(p.ChampionStats.Points==57 && !PirateFenced(p) && !ReferenceEquals(stale,p.ChampionStats));
            refused=false;try{ChampionTable.SaveStats(stale);}catch(InvalidOperationException){refused=true;}
            Require(refused && !PirateFenced(p) && p.ChampionStats.Points==57);
            ChampionTable.SaveStats(p.ChampionStats);
            Sql("INSERT INTO revora_champion_balance_probe VALUES(@uid,57,9,2)","uid",(long)uid);
        }
        Require(p.ChampionStats.Points==57 && p.ChampionStats.TotalMatches==9 && p.ChampionStats.WinStreak==2);
        Require((long)Sql("SELECT points::bigint FROM championarena WHERE entityid=@uid","uid",(long)uid)==57);
        System.Console.WriteLine("[career-process] career-champion-balance-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion balance did not close.");
    }

    static bool championShopFixture;
    static void ChampionShopBuy(GameState p,uint type) {
        var frame=new ItemUsage(true){UID=6003,dwParam=type,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);ItemUsage.Process(frame,p);
    }
    static string ChampionShopSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('points',c.points,'total',c.totalpoints,
'items',(SELECT json_agg(r ORDER BY r.uid) FROM (SELECT uid,id,plus,plusprogress,bound,position,stacksize FROM items WHERE entityid=@uid) r),
'purchases',(SELECT json_agg(b ORDER BY b.id) FROM revora_champion_purchases b WHERE b.uid=@uid))::text FROM championarena c WHERE c.entityid=@uid","uid",(long)p.Entity.UID));
    }
    static void ChampionShopProcess(bool reload) {
        championShopFixture=true;const uint first=4294913901;
        if((bool)Sql("SELECT to_regclass('revora_champion_shop_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3)==0);
            nextUid=first;for(int i=0;i<4;i++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3);
            Sql("CREATE TABLE revora_champion_shop_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        var type=Feature("ChampionPurchases");var clock=type.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);
        var beforeCommit=type.GetField("BeforeCommit",Hidden);var beforeInstall=type.GetField("BeforeInstall",Hidden);
        DateTime now=DateTime.UtcNow;clock.SetValue(null,new Func<DateTime>(()=>now));
        try {
            for(uint uid=first;uid<first+4;uid++) {
                nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
                Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
                var saved=Sql("SELECT snapshot FROM revora_champion_shop_probe WHERE uid=@uid","uid",(long)uid);
                if(saved==null || saved==DBNull.Value) {
                    Require(!reload && (p.Inventory.Count==0 || (uid==first && p.Inventory.Count==1 && (long)Sql("SELECT count(*) FROM revora_champion_purchases WHERE uid=@uid","uid",(long)uid)==0)));
                    p.ChampionStats.Points=uid==first+3?0u:500u;ChampionTable.SaveStats(p.ChampionStats);
                    if(uid==first) {
                        uint bootsType=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==9 && i.Level>=15).OrderBy(i=>i.Level).First().ID;
                        var boots=p.Inventory.Objects.FirstOrDefault(i=>i.ID/1000==160)??RefineryClaimGive(p,bootsType);
                        foreach(uint item in new uint[]{730001,730002,730003}) {
                            now=now.AddSeconds(2);uint before=p.ChampionStats.Points;
                            Require((bool)Call(type,"Ready",p));
                            System.Threading.Tasks.Parallel.For(0,8,_=>ChampionShopBuy(p,item));
                            System.Console.WriteLine("[champion-shop-probe] type="+item+" items="+p.Inventory.Count+" points="+p.ChampionStats.Points+" action="+p.Action);
                            var stone=p.Inventory.Objects.Single(i=>i.ID==item);Require(stone.Bound && stone.Plus==item-730000 && stone.StackSize==1);
                            Require(p.ChampionStats.Points==before-ChampionShop.Shop.Items[item].Price);
                            string once=ChampionShopSnapshot(p);ChampionShopBuy(p,item);Require(once==ChampionShopSnapshot(p));
                            CompositionClaimCompose(p,boots,stone);
                        }
                        Require(p.ChampionStats.Points==330 && boots.Plus>0 && !PirateFenced(p));
                        string snapshot=ChampionShopSnapshot(p);ChampionShopBuy(p,3001046);Require(snapshot==ChampionShopSnapshot(p));
                    } else if(uid==first+1) {
                        string before=ChampionShopSnapshot(p);
                        beforeCommit.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture rollback");}));ChampionShopBuy(p,730001);beforeCommit.SetValue(null,null);
                        Require(PirateFenced(p) && before==ChampionShopSnapshot(p));
                        nextUid=uid;p=Player(Kernel.Maps[1002],true);now=now.AddSeconds(2);ChampionShopBuy(p,730001);
                        Require(!PirateFenced(p) && p.ChampionStats.Points==490 && p.Inventory.Objects.Single().Plus==1);
                    } else if(uid==first+2) {
                        var stale=p.ChampionStats;
                        beforeInstall.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture committed install");}));ChampionShopBuy(p,730001);beforeInstall.SetValue(null,null);
                        Require(PirateFenced(p));bool refused=false;try{ChampionTable.SaveStats(stale);}catch(InvalidOperationException){refused=true;}Require(refused);
                        nextUid=uid;p=Player(Kernel.Maps[1002],true);
                        Require(!PirateFenced(p) && p.ChampionStats.Points==490 && p.Inventory.Objects.Single().Plus==1);
                        string once=ChampionShopSnapshot(p);ChampionShopBuy(p,730001);Require(once==ChampionShopSnapshot(p));
                    } else {
                        string before=ChampionShopSnapshot(p);ChampionShopBuy(p,730001);ChampionShopBuy(p,3001065);Require(before==ChampionShopSnapshot(p));
                    }
                    Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_champion_shop_probe VALUES(@uid,@snapshot)","uid",(long)uid,"snapshot",ChampionShopSnapshot(p));
                    saved=ChampionShopSnapshot(p);
                }
                Require(Convert.ToString(saved)==ChampionShopSnapshot(p) && !PirateFenced(p));
            }
        }finally{clock.SetValue(null,oldClock);beforeCommit.SetValue(null,null);beforeInstall.SetValue(null,null);}
        Require((long)Sql("SELECT count(*) FROM revora_champion_purchases WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3)==5);
        System.Console.WriteLine("[career-process] career-champion-shop-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion shop did not close.");
    }

    static bool championSteedFixture;
    static string ChampionSteedSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'steeds',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,plus,plusprogress,bound,effect,socketprogress,nextsteedcolor,position FROM items WHERE entityid=@uid AND id=300000) i),
'openings',(SELECT json_agg(o ORDER BY id) FROM revora_champion_steed_openings o WHERE uid=@uid),
'breeding',(SELECT json_agg(b ORDER BY id) FROM revora_steed_breedings b WHERE uid=@uid))::text","base",ChampionShopSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void ChampionSteedProcess(bool reload) {
        championSteedFixture=true;const uint first=4294914001;
        if((bool)Sql("SELECT to_regclass('revora_champion_steed_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2)==0);
            nextUid=first;for(int i=0;i<3;i++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=120,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2);
            Sql("CREATE TABLE revora_champion_steed_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        var type=Feature("ChampionSteedPacks");var beforeCommit=type.GetField("BeforeCommit",Hidden);var beforeInstall=type.GetField("BeforeInstall",Hidden);
        try {
            for(uint uid=first;uid<=first+2;uid++) {
                nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
                object saved=Sql("SELECT snapshot FROM revora_champion_steed_probe WHERE uid=@uid","uid",(long)uid);
                if(saved==null || saved==DBNull.Value) {
                    Require(!reload);
                    bool completed=uid==first && (long)Sql("SELECT count(*) FROM revora_champion_steed_openings WHERE uid=@uid","uid",(long)uid)==6
                        && (long)Sql("SELECT count(*) FROM revora_steed_breedings WHERE uid=@uid","uid",(long)uid)==1;
                    if(!completed){Require(p.Inventory.Count==0);p.ChampionStats.Points=1000;ChampionTable.SaveStats(p.ChampionStats);}
                    if(uid==first) {
                        if(!completed) {
                        var steeds=new System.Collections.Generic.List<ConquerItem>();uint[] colors={0xff9600,0x00ff96,0x9600ff};
                        for(uint id=3001060;id<=3001065;id++) {
                            uint before=p.ChampionStats.Points;ChampionShopBuy(p,id);var pack=p.Inventory.Objects.Single(i=>i.ID==id);
                            Require(pack.Bound && p.ChampionStats.Points==before-ChampionShop.Shop.Items[id].Price);
                            uint[] previous=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint packUid=pack.UID;
                            System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem));
                            var steed=p.Inventory.Objects.Single(i=>!previous.Contains(i.UID));Require(!p.Inventory.ContainsUID(packUid));
                            Require(steed.ID==300000 && steed.Bound && steed.Plus==(id<3001063?1:3) && steed.SocketProgress==colors[(id-3001060)%3]);
                            Require(((uint)steed.NextGreen|((uint)steed.NextBlue<<8)|((uint)steed.NextRed<<16))==steed.SocketProgress);
                            string once=ChampionSteedSnapshot(p);RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(once==ChampionSteedSnapshot(p));steeds.Add(steed);
                        }
                        Require(p.ChampionStats.Points==415 && steeds.Count==6);
                        var main=steeds[0];var minor=steeds[3];RetainedUpgradeUsage(p,main.UID,ConquerItem.Steed,ItemUsage.Mode.EquipItem);
                        Require(p.Equipment.TryGetItem(ConquerItem.Steed).UID==main.UID);
                        Revora.Network.PacketHandler.HandlePacket(RetainedBreedFrame(main.UID,minor.UID),p);
                        Require(!p.Inventory.ContainsUID(minor.UID) && main.Plus==3 && main.PlusProgress==10 && main.SocketProgress==0xff9600 && main.Bound && !PirateFenced(p));
                        }
                        var retained=p.Equipment.TryGetItem(ConquerItem.Steed);
                        Require(retained!=null && retained.Plus==3 && retained.PlusProgress==10 && retained.Bound && retained.SocketProgress==0xff9600 && p.ChampionStats.Points==415);
                        Require((long)Sql(@"SELECT count(*) FROM revora_champion_steed_openings WHERE uid=@uid
AND plus=CASE WHEN pack_type<3001063 THEN 1 ELSE 3 END
AND color=CASE (pack_type-3001060)%3 WHEN 0 THEN 16750080 WHEN 1 THEN 65430 ELSE 9830655 END","uid",(long)uid)==6);
                    } else {
                        uint id=uid==first+1?3001060u:3001063u;ChampionShopBuy(p,id);var pack=p.Inventory.Objects.Single();uint packUid=pack.UID;
                        var hook=uid==first+1?beforeCommit:beforeInstall;string before=ChampionSteedSnapshot(p);
                        hook.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture steed pack failure");}));RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);hook.SetValue(null,null);
                        Require(PirateFenced(p));if(uid==first+1)Require(before==ChampionSteedSnapshot(p));
                        nextUid=uid;p=Player(Kernel.Maps[1002],true);
                        if(uid==first+1)RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);
                        var steed=p.Inventory.Objects.Single();Require(steed.ID==300000 && steed.Bound && steed.Plus==(uid==first+1?1:3) && !PirateFenced(p));
                        string once=ChampionSteedSnapshot(p);RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(once==ChampionSteedSnapshot(p));
                    }
                    Require(EntityTable.SaveEntity(p));saved=ChampionSteedSnapshot(p);Sql("INSERT INTO revora_champion_steed_probe VALUES(@uid,@snapshot)","uid",(long)uid,"snapshot",saved);
                }
                Require(Convert.ToString(saved)==ChampionSteedSnapshot(p) && !PirateFenced(p));
            }
        }finally{beforeCommit.SetValue(null,null);beforeInstall.SetValue(null,null);}
        Require((long)Sql("SELECT count(*) FROM revora_champion_steed_openings WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2)==8);
        System.Console.WriteLine("[career-process] career-champion-steed-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion steed did not close.");
    }

    static bool attributeBonusFixture;
    static long[] BonusBase(byte job, byte level) {
        var stats=Call(typeof(DataHolder),"StatsFor",job,level);
        return new[]{"Strength","Agility","Vitality","Spirit"}.Select(n=>Convert.ToInt64(stats.GetType().GetField(n,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).GetValue(stats))).ToArray();
    }
    static long[] BonusValues(object profile) { return (long[])profile.GetType().GetField("Values",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(profile); }
    static object BonusProfile(long[] values) {return Activator.CreateInstance(Feature("CharacterProgression").GetNestedType("Profile",System.Reflection.BindingFlags.NonPublic),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{values},null);}
    static void AttributeBonusProcess(bool reload) {
        attributeBonusFixture=true;const uint uid=4294914101;var progression=Feature("CharacterProgression");
        if(!reload && (bool)Sql("SELECT to_regclass('revora_attribute_bonus_probe') IS NULL")) {
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            var stats=BonusBase(15,80);
            // Supplied points exercise preservation; this is not a fruit-use claim.
            Sql("UPDATE entities SET level=80,class=15,reborn=0,experience=0,strength=@s,agility=@a,vitality=@v,spirit=@p,atributes=0,revora_attribute_bonus=3 WHERE uid=@uid",
                "s",stats[0]+3,"a",stats[1],"v",stats[2],"p",stats[3],"uid",(long)uid);
            Sql("CREATE TABLE revora_attribute_bonus_probe(uid bigint PRIMARY KEY,profile bigint[] NOT NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
        lock(p.ItemSyncRoot) {
            var captured=Call(progression,"Capture",p);Require(BonusValues(captured)[13]==3);
            if(!reload) {
                Require((long)Sql("SELECT count(*) FROM revora_attribute_bonus_probe")==0);
                ulong exp=DataHolder.LevelExperience(80)+DataHolder.LevelExperience(81);
                var plan=Activator.CreateInstance(Feature("ExperienceRewardPlan"),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{captured,exp,(byte)140,(ushort)3},null);
                var after=plan.GetType().GetField("After",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(plan);
                var av=BonusValues(after);Require(av[0]==82 && av[6]==3 && av[13]==3);
                var s82=BonusBase(15,82);Require(av.Skip(2).Take(4).SequenceEqual(s82));
                // Multi-level automatic allocation must not credit the same bonus twice.
                var first=Call(Feature("RebirthRules"),"Profile",(byte)15,(byte)130,(byte)11);
                var preserved=Call(progression,"PreserveEarnedAttributes",first,captured);
                Require(BonusValues(preserved)[6]==BonusValues(first)[6]+3 && BonusValues(preserved)[13]==3);
                bool refused=false;try{Call(progression,"PreserveEarnedAttributes",preserved,captured);}catch{refused=true;}Require(refused);
                var second=Call(Feature("SecondRebirthRules"),"Profile",(byte)15,(byte)130,(byte)15,(byte)130,(byte)11);
                var secondBonus=Call(progression,"PreserveEarnedAttributes",second,captured);
                Require(BonusValues(secondBonus).Skip(2).Take(5).Sum()==BonusValues(second).Skip(2).Take(5).Sum()+3);
                var pv=(long[])BonusValues(secondBonus).Clone();pv[0]=130;pv[6]+=115*3;pv[7]=15;
                var paid=Call(Feature("ReincarnationRules"),"Profile",BonusProfile(pv),(byte)11,(ushort)3);
                Require(BonusValues(paid).Skip(2).Take(5).Sum()==BonusValues(secondBonus).Skip(2).Take(5).Sum() && BonusValues(paid)[13]==3);
                foreach(long invalid in new long[]{-1,901}){var bad=(long[])pv.Clone();bad[13]=invalid;refused=false;try{BonusProfile(bad);}catch{refused=true;}Require(refused);}
                // Rollback must retain both ordinary attributes and their bonus counter.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Call(progression,"Persist",c,t,p,after);t.Rollback();}}
                Require(Convert.ToInt32(Sql("SELECT level FROM entities WHERE uid=@uid","uid",(long)uid))==80);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Call(progression,"Persist",c,t,p,after);t.Commit();}}
                Call(progression,"ApplyCommitted",p,after);Require(EntityTable.SaveEntity(p));
                Sql("INSERT INTO revora_attribute_bonus_probe(uid,profile) VALUES(@uid,@v)","uid",(long)uid,"v",av);
            }
            var expected=(long[])Sql("SELECT profile FROM revora_attribute_bonus_probe WHERE uid=@uid","uid",(long)uid);
            Require(BonusValues(Call(progression,"Capture",p)).SequenceEqual(expected));Require(EntityTable.SaveEntity(p));
            Require(Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==3);
        }
        System.Console.WriteLine("[career-process] career-attribute-bonus-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Attribute fixture did not close.");
    }

    static bool mysteryFruitFixture;
    static string MysteryFruitSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'p',(SELECT row_to_json(e) FROM (SELECT level,experience,strength,agility,vitality,spirit,atributes,revora_attribute_bonus FROM entities WHERE uid=@uid) e),
'i',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,stacksize,bound,position FROM items WHERE entityid=@uid) i),
'r',(SELECT json_agg(r ORDER BY id) FROM revora_mystery_fruit_uses r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));
    }
    static void MysteryFruitProcess(bool reload) {
        mysteryFruitFixture=true;const uint first=4294914201;
        if((bool)Sql("SELECT to_regclass('revora_mystery_fruit_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3)==0);
            nextUid=first;for(int j=0;j<4;j++)Player(Kernel.Maps[1002]);var stats=BonusBase(15,80);
            Sql("UPDATE entities SET level=80,class=15,reborn=0,experience=0,strength=@s,agility=@a,vitality=@v,spirit=@p,atributes=0 WHERE uid BETWEEN @first AND @last",
                "s",stats[0],"a",stats[1],"v",stats[2],"p",stats[3],"first",(long)first,"last",(long)first+3);
            Sql("CREATE TABLE revora_mystery_fruit_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        var type=Feature("MysteryFruit");var roll=type.GetField("Roll",Hidden);object original=roll.GetValue(null);
        var commit=type.GetField("BeforeCommit",Hidden);var install=type.GetField("BeforeInstall",Hidden);
        try{for(uint uid=first;uid<=first+3;uid++) {
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
            object saved=Sql("SELECT snapshot FROM revora_mystery_fruit_probe WHERE uid=@uid","uid",(long)uid);
            if(saved==null || saved==DBNull.Value) {
                Require(!reload && p.Inventory.Count==0);roll.SetValue(null,new Func<int,int>(n=>0));
                if(uid==first) {
                    var dew=RefineryClaimGive(p,3001045);string before=MysteryFruitSnapshot(p);RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);Require(MysteryFruitSnapshot(p)==before);
                    for(int n=1;n<=3;n++) {
                        int count=n;roll.SetValue(null,new Func<int,int>(limit=>limit==3?count-1:count));var fruit=RefineryClaimGive(p,3001044);uint item=fruit.UID;
                        RetainedUpgradeUsage(p,item,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(item) && p.Inventory.ContainsUID(dew.UID) && !PirateFenced(p));
                        before=MysteryFruitSnapshot(p);RetainedUpgradeUsage(p,item,0,ItemUsage.Mode.EquipItem);Require(MysteryFruitSnapshot(p)==before);
                    }
                    Require(Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==6);
                    RefineryClaimGive(p,3001044);RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(dew.UID));
                    Require(Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==9);
                    var stack=RefineryClaimGive(p,3001044);stack.StackSize=3;Sql("UPDATE items SET stacksize=3 WHERE uid=@item","item",(long)stack.UID);
                    roll.SetValue(null,new Func<int,int>(n=>0));System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,stack.UID,0,ItemUsage.Mode.EquipItem));
                    Require(stack.StackSize==2 && Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==10);
                    // The remaining legacy default-stat entry point preserves the earned budget.
                    long total=p.Entity.Strength+p.Entity.Agility+p.Entity.Vitality+p.Entity.Spirit+p.Entity.Atributes;
                    DataHolder.GetStats(p.Entity.Class,p.Entity.Level,p);Require(p.Entity.Strength+p.Entity.Agility+p.Entity.Vitality+p.Entity.Spirit+p.Entity.Atributes==total && p.Entity.Atributes==10);
                } else if(uid<=first+2) {
                    var fruit=RefineryClaimGive(p,3001044);var dew=RefineryClaimGive(p,3001045);uint item=dew.UID;string before=MysteryFruitSnapshot(p);
                    var hook=uid==first+1?commit:install;hook.SetValue(null,new Action(()=>{throw new InvalidOperationException("injected-fruit");}));
                    RetainedUpgradeUsage(p,item,0,ItemUsage.Mode.EquipItem);hook.SetValue(null,null);Require(PirateFenced(p));
                    if(uid==first+1)Require(MysteryFruitSnapshot(p)==before);
                    Require(!EntityTable.SaveEntity(p));nextUid=uid;p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!PirateFenced(p));
                    RetainedUpgradeUsage(p,item,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(fruit.UID) && !p.Inventory.ContainsUID(item));
                    Require((long)Sql("SELECT count(*) FROM revora_mystery_fruit_uses WHERE uid=@uid","uid",(long)uid)==1);
                } else {
                    p.Entity.Strength=898;p.Entity.Agility=0;p.Entity.Vitality=0;p.Entity.Spirit=0;p.Entity.Atributes=0;Require(EntityTable.SaveEntity(p));
                    var fruit=RefineryClaimGive(p,3001044);var dew=RefineryClaimGive(p,3001045);string before=MysteryFruitSnapshot(p);
                    RetainedUpgradeUsage(p,fruit.UID,0,ItemUsage.Mode.EquipItem);RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);
                    Require(MysteryFruitSnapshot(p)==before && !PirateFenced(p));
                }
                Require(EntityTable.SaveEntity(p));saved=MysteryFruitSnapshot(p);Sql("INSERT INTO revora_mystery_fruit_probe VALUES(@uid,@s)","uid",(long)uid,"s",saved);
            }
            Require(MysteryFruitSnapshot(p)==Convert.ToString(saved));Require(EntityTable.SaveEntity(p));Require(MysteryFruitSnapshot(p)==Convert.ToString(saved));
        }}finally{roll.SetValue(null,original);commit.SetValue(null,null);install.SetValue(null,null);}
        System.Console.WriteLine("[career-process] career-mystery-fruit-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fruit fixture did not close.");
    }

    static bool fruitCapFixture;
    static void FruitCapProcess(bool reload) {
        fruitCapFixture=true;const uint first=4294914301;
        if((bool)Sql("SELECT to_regclass('revora_fruit_cap_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3)==0);
            nextUid=first;for(int j=0;j<4;j++)Player(Kernel.Maps[1002]);var stats=BonusBase(15,80);
            Sql("UPDATE entities SET level=80,class=15,reborn=0,experience=0,strength=@s,agility=@a,vitality=@v,spirit=@p,atributes=0 WHERE uid BETWEEN @first AND @last",
                "s",stats[0],"a",stats[1],"v",stats[2],"p",stats[3],"first",(long)first,"last",(long)first+3);
            Sql("UPDATE entities SET level=15,class=11,reborn=2,firstrebornclass=15,secondrebornclass=15,firstrebornlevel=130,secondrebornlevel=130,strength=525,agility=0,vitality=0,spirit=0,atributes=0,revora_attribute_bonus=333 WHERE uid=@uid","uid",(long)first);
            Sql("UPDATE entities SET strength=strength+331,revora_attribute_bonus=331 WHERE uid=@uid","uid",(long)first+1);
            Sql("UPDATE entities SET strength=strength+330,revora_attribute_bonus=330 WHERE uid=@uid","uid",(long)first+2);
            Sql("CREATE TABLE revora_fruit_cap_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        var type=Feature("MysteryFruit");var roll=type.GetField("Roll",Hidden);object original=roll.GetValue(null);
        try{roll.SetValue(null,new Func<int,int>(n=>n==3?2:0));for(uint uid=first;uid<=first+3;uid++) {
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
            object saved=Sql("SELECT snapshot FROM revora_fruit_cap_probe WHERE uid=@uid","uid",(long)uid);
            if(saved==null || saved==DBNull.Value) {
                Require(!reload && p.Inventory.Count==0);var fruit=RefineryClaimGive(p,3001044);var dew=RefineryClaimGive(p,3001045);
                string before=MysteryFruitSnapshot(p);
                if(uid<=first+1) {
                    lock(p.ItemSyncRoot){var profile=Call(Feature("CharacterProgression"),"Capture",p);Require(Convert.ToInt64(Call(type,"FutureAttributeTotal",profile))==(uid==first?900:898));}
                    RetainedUpgradeUsage(p,fruit.UID,0,ItemUsage.Mode.EquipItem);RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);
                    Require(MysteryFruitSnapshot(p)==before && !PirateFenced(p));
                } else if(uid==first+2) {
                    RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(dew.UID) && !p.Inventory.ContainsUID(fruit.UID));
                    Require(Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==333);
                    fruit=RefineryClaimGive(p,3001044);before=MysteryFruitSnapshot(p);RetainedUpgradeUsage(p,fruit.UID,0,ItemUsage.Mode.EquipItem);Require(MysteryFruitSnapshot(p)==before);
                    lock(p.ItemSyncRoot){
                        var profile=Call(Feature("CharacterProgression"),"Capture",p);
                        var rite=Call(Feature("RebirthRules"),"Profile",(byte)15,(byte)130,(byte)11);
                        var after=Call(Feature("CharacterProgression"),"PreserveEarnedAttributes",rite,profile);
                        Require(Convert.ToInt64(Call(type,"FutureAttributeTotal",after))==900);
                        var second=Call(Feature("SecondRebirthRules"),"Profile",(byte)15,(byte)130,(byte)15,(byte)130,(byte)11);
                        after=Call(Feature("CharacterProgression"),"PreserveEarnedAttributes",second,profile);Require(Convert.ToInt64(Call(type,"FutureAttributeTotal",after))==900);
                        var v=(long[])BonusValues(after).Clone();v[0]=130;v[6]+=115*3;v[7]=15;
                        after=Call(Feature("ReincarnationRules"),"Profile",BonusProfile(v),(byte)11,(ushort)3);Require(Convert.ToInt64(Call(type,"FutureAttributeTotal",after))==900);
                    }
                } else {
                    var frame=new ItemUsage(true){UID=fruit.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
                    foreach(int index in new[]{0,2,20,frame.Length-1}){var bad=(byte[])frame.Clone();bad[index]^=1;Require((bool)Call(type,"TryHandle",p,bad));Require(MysteryFruitSnapshot(p)==before && !PirateFenced(p));}
                    // A different saved profile must abort consumption and fence the stale cache.
                    Sql("UPDATE entities SET strength=strength+1 WHERE uid=@uid","uid",(long)uid);before=MysteryFruitSnapshot(p);
                    RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);Require(PirateFenced(p) && MysteryFruitSnapshot(p)==before && !EntityTable.SaveEntity(p));
                    nextUid=uid;p=Player(Kernel.Maps[1002],true);Require(!PirateFenced(p) && p.Inventory.ContainsUID(fruit.UID) && p.Inventory.ContainsUID(dew.UID));
                }
                Require(EntityTable.SaveEntity(p));saved=MysteryFruitSnapshot(p);Sql("INSERT INTO revora_fruit_cap_probe VALUES(@uid,@s)","uid",(long)uid,"s",saved);
            }
            Require(MysteryFruitSnapshot(p)==Convert.ToString(saved));Require(EntityTable.SaveEntity(p));Require(MysteryFruitSnapshot(p)==Convert.ToString(saved));
        }}finally{roll.SetValue(null,original);}
        System.Console.WriteLine("[career-process] career-fruit-cap-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fruit cap fixture did not close.");
    }

    static bool meditationFixture;
    static string MeditationSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,'fruit',@fruit::json,
'chi',(SELECT points FROM chi WHERE uid=@uid),'study',(SELECT studypoints FROM entities WHERE uid=@uid),
'openings',(SELECT json_agg(o ORDER BY id) FROM revora_meditation_openings o WHERE uid=@uid))::text",
            "base",ChampionShopSnapshot(p),"fruit",MysteryFruitSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void MeditationProcess(bool reload) {
        meditationFixture=true;const uint first=4294914401;
        if((bool)Sql("SELECT to_regclass('revora_meditation_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+3)==0);
            nextUid=first;for(int j=0;j<4;j++)Player(Kernel.Maps[1002]);var stats=BonusBase(15,120);
            Sql("UPDATE entities SET level=120,class=15,reborn=0,experience=0,strength=@s,agility=@a,vitality=@v,spirit=@p,atributes=0 WHERE uid BETWEEN @first AND @last",
                "s",stats[0],"a",stats[1],"v",stats[2],"p",stats[3],"first",(long)first,"last",(long)first+3);
            Sql("CREATE TABLE revora_meditation_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        var type=Feature("MeditationPacks");var roll=type.GetField("Roll",Hidden);object original=roll.GetValue(null);
        var commit=type.GetField("BeforeCommit",Hidden);var install=type.GetField("BeforeInstall",Hidden);
        var buyClock=Feature("ChampionPurchases").GetField("Clock",Hidden);object originalClock=buyClock.GetValue(null);DateTime now=DateTime.UtcNow;
        try{buyClock.SetValue(null,new Func<DateTime>(()=>now));for(uint uid=first;uid<=first+3;uid++) {
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
            object saved=Sql("SELECT snapshot FROM revora_meditation_probe WHERE uid=@uid","uid",(long)uid);
            if(saved==null || saved==DBNull.Value) {
                Require(!reload && p.Inventory.Count==0);p.ChampionStats.Points=3200;ChampionTable.SaveStats(p.ChampionStats);
                if(uid==first) {
                    uint originalChi=p.ChiPoints;int originalStudy=p.Entity.SubClasses.StudyPoints;
                    for(int n=0;n<3;n++) {
                        int outcome=n;roll.SetValue(null,new Func<int,int>(_=>outcome));now=now.AddSeconds(2);ChampionShopBuy(p,3001046);
                        var pack=p.Inventory.Objects.Single(x=>x.ID==3001046);Require(pack.Bound && p.ChampionStats.Points==3200-(n+1)*800);
                        System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem));Require(!p.Inventory.ContainsUID(pack.UID) && !PirateFenced(p));
                        if(n==0){var fruit=p.Inventory.Objects.Single(x=>x.ID==3001044);Require(fruit.Bound);RetainedUpgradeUsage(p,fruit.UID,0,ItemUsage.Mode.EquipItem);Require(!p.Inventory.ContainsUID(fruit.UID) && !PirateFenced(p));}
                        string before=MeditationSnapshot(p);RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(MeditationSnapshot(p)==before);
                    }
                    Require(p.ChiPoints==originalChi+1000 && p.Entity.SubClasses.StudyPoints==originalStudy+3000 && p.ChampionStats.Points==800);
                    Require((long)Sql("SELECT count(*) FROM revora_meditation_openings WHERE uid=@uid","uid",(long)uid)==3);
                    Require((long)Sql("SELECT count(*) FROM revora_mystery_fruit_uses WHERE uid=@uid","uid",(long)uid)==1);
                } else if(uid<=first+2) {
                    int outcome=uid==first+1?1:2;roll.SetValue(null,new Func<int,int>(_=>outcome));now=now.AddSeconds(2);ChampionShopBuy(p,3001046);
                    var pack=p.Inventory.Objects.Single(x=>x.ID==3001046);string before=MeditationSnapshot(p);uint chi=p.ChiPoints;int study=p.Entity.SubClasses.StudyPoints;
                    var hook=uid==first+1?commit:install;hook.SetValue(null,new Action(()=>{throw new InvalidOperationException("injected-meditation");}));
                    RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);hook.SetValue(null,null);Require(PirateFenced(p) && !EntityTable.SaveEntity(p));
                    if(uid==first+1)Require(MeditationSnapshot(p)==before);
                    nextUid=uid;p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);p.LoadItemStats();RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);
                    Require(!p.Inventory.ContainsUID(pack.UID) && p.ChampionStats.Points==2400 && !PirateFenced(p));
                    Require(p.ChiPoints==chi+(outcome==1?1000u:0u) && p.Entity.SubClasses.StudyPoints==study+(outcome==2?3000:0));
                    Require((long)Sql("SELECT count(*) FROM revora_meditation_openings WHERE uid=@uid","uid",(long)uid)==1);
                } else {
                    var pack=RefineryClaimGive(p,3001046);string before=MeditationSnapshot(p);
                    var frame=new ItemUsage(true){UID=pack.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
                    foreach(int index in new[]{0,2,20,frame.Length-1}){var bad=(byte[])frame.Clone();bad[index]^=1;Require((bool)Call(type,"TryHandle",p,bad));Require(MeditationSnapshot(p)==before && !PirateFenced(p));}
                    p.Entity.SubClasses.StudyPoints=ushort.MaxValue;Require(EntityTable.SaveEntity(p));before=MeditationSnapshot(p);
                    RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);now=now.AddSeconds(2);ChampionShopBuy(p,3001046);Require(MeditationSnapshot(p)==before && !PirateFenced(p));
                    p.Entity.SubClasses.StudyPoints=0;Require(EntityTable.SaveEntity(p));Sql("UPDATE chi SET points=4294967295 WHERE uid=@uid","uid",(long)uid);
                    nextUid=uid;p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);p.LoadItemStats();before=MeditationSnapshot(p);RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(MeditationSnapshot(p)==before && !PirateFenced(p));
                    Require((long)Sql("SELECT count(*) FROM revora_meditation_openings WHERE uid=@uid","uid",(long)uid)==0);
                }
                Require(EntityTable.SaveEntity(p));saved=MeditationSnapshot(p);Sql("INSERT INTO revora_meditation_probe VALUES(@uid,@s)","uid",(long)uid,"s",saved);
            }
            Require(MeditationSnapshot(p)==Convert.ToString(saved));Require(EntityTable.SaveEntity(p));Require(MeditationSnapshot(p)==Convert.ToString(saved));
        }}finally{roll.SetValue(null,original);buyClock.SetValue(null,originalClock);commit.SetValue(null,null);install.SetValue(null,null);}
        System.Console.WriteLine("[career-process] career-meditation-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Meditation fixture did not close.");
    }

    static bool fruitLifeFixture;
    static string FruitLifeSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('rewards',@rewards::json,
'life',(SELECT row_to_json(e) FROM (SELECT level,experience,class,reborn,firstrebornclass,firstrebornlevel,secondrebornclass,secondrebornlevel FROM entities WHERE uid=@uid) e),
'first',(SELECT row_to_json(r) FROM revora_first_rebirths r WHERE uid=@uid),
'second',(SELECT row_to_json(r) FROM revora_second_rebirths r WHERE uid=@uid),
'paid',(SELECT json_agg(r ORDER BY id) FROM revora_reincarnations r WHERE uid=@uid))::text","uid",(long)p.Entity.UID,"rewards",MeditationSnapshot(p)));
    }
    static long FruitLifeTotal(GameState p){return (long)p.Entity.Strength+p.Entity.Agility+p.Entity.Vitality+p.Entity.Spirit+p.Entity.Atributes;}
    static void FruitLifeGrow(GameState p) {
        ulong xp=0;for(byte level=p.Entity.Level;level<130;level++)xp=checked(xp+DataHolder.LevelExperience(level));
        // Supply XP to the real progression/recovery service; no hunting claim.
        p.IncreaseExperience(xp,false);Require(p.Entity.Level==130 && !PirateFenced(p));
    }
    static void FruitLifeProcess(string stage) {
        fruitLifeFixture=true;const uint uid=4294914501;
        if((bool)Sql("SELECT to_regclass('revora_fruit_life_probe') IS NULL")) {
            Require(stage=="first" && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);var stats=BonusBase(15,120);
            Sql("UPDATE entities SET level=130,class=15,reborn=0,experience=0,strength=@s,agility=@a,vitality=@v,spirit=@p,atributes=30 WHERE uid=@uid",
                "s",stats[0],"a",stats[1],"v",stats[2],"p",stats[3],"uid",(long)uid);
            Sql("CREATE TABLE revora_fruit_life_probe(uid bigint PRIMARY KEY,step integer NOT NULL,snapshot text);INSERT INTO revora_fruit_life_probe(uid,step) VALUES(@uid,0)","uid",(long)uid);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        int step=Convert.ToInt32(Sql("SELECT step FROM revora_fruit_life_probe WHERE uid=@uid","uid",(long)uid));
        var prior=Sql("SELECT snapshot FROM revora_fruit_life_probe WHERE uid=@uid","uid",(long)uid);if(prior!=DBNull.Value && prior!=null)Require(FruitLifeSnapshot(p)==Convert.ToString(prior));
        if(stage=="first") {
            Require(step==0);
            if((long)Sql("SELECT count(*) FROM revora_mystery_fruit_uses WHERE uid=@uid","uid",(long)uid)==0) {
                if(!p.Inventory.Contains(3001044,1) && !p.Inventory.Contains(3001046,1)) {p.ChampionStats.Points=800;ChampionTable.SaveStats(p.ChampionStats);ChampionShopBuy(p,3001046);}
                var roll=Feature("MeditationPacks").GetField("Roll",Hidden);object old=roll.GetValue(null);
                try{roll.SetValue(null,new Func<int,int>(_=>0));var pack=p.Inventory.Objects.SingleOrDefault(x=>x.ID==3001046);if(pack!=null)RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);}finally{roll.SetValue(null,old);}
                var dew=RefineryClaimGive(p,3001045);RetainedUpgradeUsage(p,dew.UID,0,ItemUsage.Mode.EquipItem);
                Require(p.ChampionStats.Points==0 && Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==3);
            }
            if(p.Entity.Reborn==0){if(!p.Inventory.Contains(721259,1))RefineryClaimGive(p,721259);foreach(byte option in new byte[]{1,200,210,212,214})HeartTalk(p,59558,option);}
            Require(!PirateFenced(p) && p.Entity.Reborn==1 && p.Entity.Level==15 && FruitLifeTotal(p)==130);
            string after=FruitLifeSnapshot(p);HeartTalk(p,59558,214);Require(FruitLifeSnapshot(p)==after);step=1;
        } else if(stage=="second") {
            Require(step==1);FruitLifeGrow(p);Require(FruitLifeTotal(p)==475);
            // Promotion eligibility is supplied; the second-life NPC transaction is real.
            p.Entity.Class=15;Require(EntityTable.SaveEntity(p));RefineryClaimGive(p,723701);
            foreach(byte option in new byte[]{2,170,180})HeartTalk(p,59558,option);
            Require(!PirateFenced(p) && p.Entity.Reborn==2 && p.Entity.Level==15 && FruitLifeTotal(p)==195);
            string after=FruitLifeSnapshot(p);HeartTalk(p,59558,180);Require(FruitLifeSnapshot(p)==after);step=2;
        } else if(stage=="paid") {
            Require(step==2);FruitLifeGrow(p);Require(FruitLifeTotal(p)==540);
            p.Entity.Class=15;p.Entity.ConquerPoints=checked(Config.SerethReincarnationCp+2000);Require(EntityTable.SaveEntity(p));
            foreach(byte option in new byte[]{3,6,7})HeartTalk(p,59558,option);Require(p.Inventory.Contains(711083,1));
            HeartTalk(p,59558,3);HeartTalk(p,59558,5);CareerPaidRequest(p,41);
            Require(!PirateFenced(p) && p.Entity.Class==41 && p.Entity.Level==15 && p.Entity.Reborn==2 && FruitLifeTotal(p)==195 && p.Entity.ConquerPoints==0);
            string after=FruitLifeSnapshot(p);CareerPaidRequest(p,41);Require(FruitLifeSnapshot(p)==after);step=3;
        } else {Require(stage=="reload" && step==3);Require(FruitLifeTotal(p)==195 && p.Entity.Class==41 && p.Entity.Level==15);}
        Require(Convert.ToInt32(Sql("SELECT revora_attribute_bonus FROM entities WHERE uid=@uid","uid",(long)uid))==3 && EntityTable.SaveEntity(p));
        Require((long)Sql("SELECT count(*) FROM revora_mystery_fruit_uses WHERE uid=@uid","uid",(long)uid)==1 && (long)Sql("SELECT count(*) FROM revora_meditation_openings WHERE uid=@uid","uid",(long)uid)==1);
        if(stage!="reload")Sql("UPDATE revora_fruit_life_probe SET step=@step,snapshot=@s WHERE uid=@uid","step",step,"s",FruitLifeSnapshot(p),"uid",(long)uid);
        else Require(FruitLifeSnapshot(p)==Convert.ToString(prior));
        System.Console.WriteLine("[career-process] career-fruit-life-"+stage+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fruit life fixture did not close.");
    }

    static bool championOutcomeFixture;
    static void ChampionOutcomeProcess(bool reload) {
        championOutcomeFixture=true;const uint first=4294915001;
        if((bool)Sql("SELECT to_regclass('revora_champion_outcome_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0);
            nextUid=first;for(int n=0;n<6;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,class=15,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5);
            Sql("CREATE TABLE revora_champion_outcome_probe(uid bigint PRIMARY KEY,run uuid NOT NULL,expected bigint NOT NULL,ending boolean NOT NULL)");
        }
        nextUid=first;var players=Enumerable.Range(0,6).Select(n=>Player(Kernel.Maps[1002],true)).ToArray();
        foreach(var p in players){p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(reload) {
            foreach(var p in players) {
                uint expected=Convert.ToUInt32(Sql("SELECT expected FROM revora_champion_outcome_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
                Require(p.ChampionStats.Points==expected && p.ChampionStats.TotalPoints==expected);
                Require(p.ChampionGroup==null && p.Map.ID==1002 && !p.ChampionStats.SignedUp);
                Require((long)Sql("SELECT count(*) FROM revora_champion_result_awards WHERE uid=@uid","uid",(long)p.Entity.UID)==(expected==0?0:1));
                Require((string)Sql("SELECT e.state FROM revora_champion_entries e JOIN revora_champion_outcome_probe p ON p.run=e.id WHERE p.uid=@uid","uid",(long)p.Entity.UID)=="interrupted");
                Require((long)Sql("SELECT count(*) FROM revora_champion_entry_players WHERE uid=@uid AND released_at IS NULL","uid",(long)p.Entity.UID)==0);
                string before=ChampionResultSnapshot(p.Entity.UID);Revora.Game.Champion.Win(p,players[0]);Require(before==ChampionResultSnapshot(p.Entity.UID));
            }
            Require((long)Sql(@"SELECT count(*) FROM revora_champion_outcomes o JOIN revora_champion_outcome_probe p ON p.run=o.run
WHERE p.ending AND NOT EXISTS(SELECT 1 FROM revora_champion_results r WHERE r.run=o.run)")==0);
            System.Console.WriteLine("[career-process] career-champion-outcome-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion outcome reload did not close.");
        }
        Require((long)Sql("SELECT count(*) FROM revora_champion_outcome_probe")==0);
        var clock=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var old=clock.GetValue(null);
        DateTime local=ChampionFixtureSignupTime(first);
        try {
            clock.SetValue(null,new Func<DateTime>(()=>local));
            for(int pair=0;pair<3;pair++) {
                var p=players[pair*2];var other=players[pair*2+1];local=ChampionFixtureSignupTime(p.Entity.UID);
                Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
                p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);
                Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
                Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);Revora.Game.Champion.VerifyAwaitingPeople();
                var group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
                var run=(Guid)group.GetType().GetField("EntryKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(group);
                if(pair==0)group.End(other);else if(pair==1)group.End();
                Require((long)Sql("SELECT count(*) FROM revora_champion_outcomes WHERE run=@run","run",run)==(pair<2?1:0));
                Require((long)Sql("SELECT count(*) FROM revora_champion_results WHERE run=@run","run",run)==0);
                foreach(var actor in new[]{p,other}) {
                    Require(actor.ChampionStats.Points==0 && actor.ChampionStats.TotalMatches==0);
                    if(pair<2) {
                        string snapshot=ChampionResultSnapshot(actor.Entity.UID);ChampionTable.SaveStats(actor.ChampionStats);
                        Require(snapshot==ChampionResultSnapshot(actor.Entity.UID));
                    }
                    long expected=pair==0?(ReferenceEquals(actor,p)?10:6):pair==1?6:0;
                    Sql("INSERT INTO revora_champion_outcome_probe VALUES(@uid,@run,@expected,@ending)","uid",(long)actor.Entity.UID,"run",run,"expected",expected,"ending",pair<2);
                }
            }
            System.Console.WriteLine("[career-process] career-champion-outcome-crash-passed");System.Console.Out.Flush();Environment.Exit(0);
        }finally{clock.SetValue(null,old);}
        throw new InvalidOperationException("Champion outcome crash fixture did not exit.");
    }
    static bool championDisconnectFixture;
    static void ChampionDisconnectProcess(bool reload) {
        championDisconnectFixture=true;const uint first=4294914901;
        if((bool)Sql("SELECT to_regclass('revora_champion_disconnect_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0);
            nextUid=first;for(int n=0;n<6;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,class=15,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5);
            Sql("CREATE TABLE revora_champion_disconnect_probe(uid bigint PRIMARY KEY,points bigint NOT NULL)");
        }
        nextUid=first;var players=Enumerable.Range(0,6).Select(n=>Player(Kernel.Maps[1002],true)).ToArray();
        foreach(var p in players){p.LoadItemStats();Call(Feature("SessionInstall"),"Install",p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(reload)foreach(var p in players) {
            Require(p.ChampionStats.Points==Convert.ToUInt32(Sql("SELECT points FROM revora_champion_disconnect_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
            Require(!p.ChampionStats.SignedUp && p.ChampionGroup==null && p.Map.ID==1002);
        } else {
            Require((long)Sql("SELECT count(*) FROM revora_champion_disconnect_probe")==0);
            var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var begin=typeof(GameState).GetMethod("TryBeginShutdown",instance);
            var retention=typeof(GameState).GetField("ArenaShutdownRetention",instance);
            var disconnected=typeof(GameState).GetField("disconnectOnce",instance);
            var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
            var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
            var resume=typeof(Revora.Game.Champion).GetField("ResumeShutdown",Hidden);var oldResume=resume.GetValue(null);
            var before=typeof(ChampionTable).GetField("ResultBeforeCommit",Hidden);
            var entry=typeof(Revora.Game.Champion.QualifierList.QualifierGroup).GetField("EntryProbe",Hidden);
            DateTime local=ChampionFixtureSignupTime(first);Time32 now=Time32.Now;int resumed=0;
            Action<GameState> request=p=>{disconnected.SetValue(p,1);Require(!(bool)begin.Invoke(p,null));Require(retention.GetValue(p)!=null);};
            try {
                signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
                // Use the production defer/reconnect barriers; replace only final socket teardown in this offline fixture.
                resume.SetValue(null,new Action<GameState>(p=>{
                    Require(p.ChampionGroup==null && p.Map.ID==1002 && (bool)begin.Invoke(p,null));
                    Require((long)Sql("SELECT count(*) FROM revora_champion_result_awards WHERE uid=@uid","uid",(long)p.Entity.UID)==(p.Entity.UID>=first+4?0:1));
                    Call(Feature("SessionInstall"),"Retire",p);GameState ignored;Kernel.DisconnectPool.TryRemove(p.Entity.UID,out ignored);resumed++;
                }));
                for(int pair=0;pair<3;pair++) {
                    var p=players[pair*2];var other=players[pair*2+1];local=ChampionFixtureSignupTime(p.Entity.UID);
                    Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
                    p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);
                    Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
                    Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);
                    if(pair==2)entry.SetValue(null,new Action<string>(stage=>{if(stage=="first-arrived")request(p);}));
                    Revora.Game.Champion.VerifyAwaitingPeople();entry.SetValue(null,null);
                    if(pair==2) {
                        Require(p.ChampionGroup==null && other.ChampionGroup==null);Call(typeof(Revora.Game.Champion),"TickShutdowns");
                        Require(resumed==4 && p.ChampionStats.Points==0 && other.ChampionStats.Points==0);continue;
                    }
                    var group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
                    if(pair==1){group.End(other);request(p);request(other);}
                    else {
                        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                            var worker=System.Threading.Tasks.Task.Run(()=>{lock(other.ItemSyncRoot){held.Set();release.Wait();}});
                            try {
                                Require(held.Wait(5000));request(p);now=now.AddSeconds(6);Revora.Game.Champion.CheckGroups();now=now.AddSeconds(5);Revora.Game.Champion.CheckGroups();
                                Require(ReferenceEquals(p.ChampionGroup,group) && !Kernel.DisconnectPool.IsEmpty);
                            }finally{release.Set();Require(worker.Wait(5000));}
                        }
                        var successor=new GameState(null);successor.Entity=new Revora.Game.Entity(Revora.Game.EntityFlag.Player,false);successor.Entity.UID=p.Entity.UID;
                        Call(Feature("SessionInstall"),"Replace",p.Entity.UID,successor,(Action)null);Require(ReferenceEquals(Kernel.GamePool[p.Entity.UID],p));
                        before.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture unavailable settlement");}));Revora.Game.Champion.CheckGroups();before.SetValue(null,null);
                        Require(p.ChampionStats.Points==0 && other.ChampionStats.Points==0 && ReferenceEquals(p.ChampionGroup,group));
                    }
                    now=now.AddSeconds(6);Revora.Game.Champion.CheckGroups();
                    Require(p.ChampionGroup==null && other.ChampionGroup==null && retention.GetValue(p)==null && retention.GetValue(other)==null);
                    Require(ReferenceEquals(group.Winner,pair==0?other:p));
                    Require(p.ChampionStats.Points==(pair==0?6:10) && other.ChampionStats.Points==(pair==0?10:6));
                    Require(!Kernel.GamePool.ContainsKey(p.Entity.UID));if(pair==1)Require(!Kernel.GamePool.ContainsKey(other.Entity.UID));
                    Revora.Game.Champion.Clear(other);
                    if(pair==0) {nextUid=p.Entity.UID;var fresh=Player(Kernel.Maps[1002],true);Call(Feature("SessionInstall"),"Install",fresh);Require(fresh.ChampionStats.Points==6 && fresh.Map.ID==1002 && !PirateFenced(fresh));players[0]=fresh;}
                }
                Require(!(bool)typeof(Revora.Game.Champion).GetProperty("HasRetainedShutdown",Hidden).GetValue(null,null));
                foreach(var p in players)Sql("INSERT INTO revora_champion_disconnect_probe VALUES(@uid,@points)","uid",(long)p.Entity.UID,"points",(long)p.ChampionStats.Points);
            } finally {signup.SetValue(null,oldSignup);timer.SetValue(null,oldTimer);resume.SetValue(null,oldResume);before.SetValue(null,null);entry.SetValue(null,null);foreach(var p in players)Revora.Game.Champion.Clear(p);}
        }
        System.Console.WriteLine("[career-process] career-champion-disconnect-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion disconnect fixture did not close.");
    }
    static bool championRoundsFixture;
    static void ChampionRoundsProcess(bool reload) {
        championRoundsFixture=true;const uint first=4294914801;
        if((bool)Sql("SELECT to_regclass('revora_champion_round_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0);
            nextUid=first;for(int n=0;n<6;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,class=15,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5);
            Sql("CREATE TABLE revora_champion_round_probe(uid bigint PRIMARY KEY,points bigint NOT NULL)");
        }
        nextUid=first;var players=Enumerable.Range(0,6).Select(n=>Player(Kernel.Maps[1002],true)).ToArray();
        foreach(var p in players){p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(reload) {
            foreach(var p in players) {
                Require(p.ChampionStats.Points==Convert.ToUInt32(Sql("SELECT points FROM revora_champion_round_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
                Require(!p.ChampionStats.SignedUp && p.ChampionGroup==null && p.Map.ID==1002);
            }
            Require((long)Sql("SELECT count(*) FROM revora_champion_entry_players WHERE uid IN(@a,@b) AND event_day IS NOT NULL AND released_at IS NOT NULL","a",(long)first,"b",(long)first+1)==10);
        } else {
            Require((long)Sql("SELECT count(*) FROM revora_champion_round_probe")==0);
            var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
            var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
            DateTime registration=ChampionFixtureSignupTime(first),local=registration;Time32 now=Time32.Now;
            Action<GameState,GameState> sign=(p,q)=>{local=registration;Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(q);Require(p.ChampionStats.SignedUp && q.ChampionStats.SignedUp);};
            Action<GameState,GameState> invite=(p,q)=>{p.ChampionStats.SignUpTime=q.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,q));};
            Action<GameState,GameState> accept=(p,q)=>{Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(q);Revora.Game.Champion.VerifyAwaitingPeople();Require(p.ChampionGroup!=null && ReferenceEquals(p.ChampionGroup,q.ChampionGroup));};
            try {
                signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
                for(int round=1;round<=5;round++) {
                    DateTime start=registration.Date.AddHours(21).AddMinutes((round-1)*5);
                    Require((int)Call(typeof(Revora.Game.Champion),"InvitationRound",start)==round);
                    Require((int)Call(typeof(Revora.Game.Champion),"InvitationRound",start.AddSeconds(59))==round);
                    Require((int)Call(typeof(Revora.Game.Champion),"InvitationRound",start.AddMinutes(1))==0);
                    Require((int)Call(typeof(Revora.Game.Champion),"InvitationRound",start.AddDays(5))==0);
                }
                var winner=players[0];var loser=players[1];sign(winner,loser);
                Revora.Game.Champion.EngagePlayers();Require(winner.ChampionStats.Opponent==null);
                for(int round=1;round<=5;round++) {
                    local=registration.Date.AddHours(21).AddMinutes((round-1)*5);invite(winner,loser);accept(winner,loser);
                    var group=winner.ChampionGroup;now=now.AddSeconds(10);group.UpdateDamage(winner,100);group.UpdateDamage(loser,50);
                    now=now.AddSeconds(180);Revora.Game.Champion.CheckGroups();Require(group.Done && ReferenceEquals(group.Winner,winner));
                    now=now.AddSeconds(5);local=local.AddMinutes(3).AddSeconds(15);Revora.Game.Champion.CheckGroups();
                    Require(winner.ChampionGroup==null && loser.ChampionGroup==null && winner.ChampionStats.TotalMatches==round && loser.ChampionStats.TotalMatches==round);
                    Require(winner.ChampionStats.SignedUp==(round<5) && loser.ChampionStats.SignedUp==(round<5));
                    // Even if the wall clock moves back within this round, another invitation is refused.
                    local=local.AddMinutes(-3).AddSeconds(-15);Revora.Game.Champion.EngagePlayers();Require(winner.ChampionStats.Opponent==null);
                    now=now.AddSeconds(105);
                }
                Require(winner.ChampionStats.Points==80 && loser.ChampionStats.Points==30);
                local=registration.Date.AddHours(21).AddMinutes(25);Revora.Game.Champion.EngagePlayers();Revora.Game.Champion.Reset();Revora.Game.Champion.Reset();
                Require(winner.ChampionStats.TotalMatches==5 && winner.ChampionStats.TodayPoints==80);
                try{Sql("UPDATE revora_champion_entry_players SET round=1 WHERE uid=@uid AND round=2","uid",(long)first);throw new Exception("Round uniqueness was not enforced.");}
                catch(Npgsql.PostgresException ex){Require(ex.SqlState=="23505");}
                var quitter=players[2];var peer=players[3];sign(quitter,peer);local=registration.AddMinutes(5);invite(quitter,peer);accept(quitter,peer);
                var abandoned=quitter.ChampionGroup;Revora.Game.Champion.QualifyEngine.DoQuit(quitter);abandoned.Export();
                Require(!quitter.ChampionStats.SignedUp && peer.ChampionStats.SignedUp && quitter.ChampionStats.Points==6 && peer.ChampionStats.Points==10);
                local=registration.AddMinutes(10);Revora.Game.Champion.EngagePlayers();Require(peer.ChampionStats.Opponent==null);
                Revora.Game.Champion.Clear(peer);
                var late=players[4];var latePeer=players[5];sign(late,latePeer);local=registration.AddMinutes(5);invite(late,latePeer);
                local=local.AddMinutes(1);Revora.Game.Champion.QualifyEngine.DoAccept(late);Revora.Game.Champion.QualifyEngine.DoAccept(latePeer);Revora.Game.Champion.VerifyAwaitingPeople();
                Require(late.ChampionGroup==null && latePeer.ChampionGroup==null && Revora.Game.Champion.WaitingPlayerList.Count==0);
                Require(late.ChampionStats.Points==0 && latePeer.ChampionStats.Points==0);
                local=registration.Date.AddHours(21).AddMinutes(21);Revora.Game.Champion.EngagePlayers();Require(Revora.Game.Champion.PlayerList.Count==0);
                foreach(var p in players)Sql("INSERT INTO revora_champion_round_probe VALUES(@uid,@points)","uid",(long)p.Entity.UID,"points",(long)p.ChampionStats.Points);
            } finally {signup.SetValue(null,oldSignup);timer.SetValue(null,oldTimer);foreach(var p in players)Revora.Game.Champion.Clear(p);}
        }
        System.Console.WriteLine("[career-process] career-champion-rounds-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion round fixture did not close.");
    }
    static bool championResultFixture;
    static string ChampionResultSnapshot(uint uid) {
        return Convert.ToString(Sql(@"SELECT json_build_object('points',points,'today',todaypoints,'total',totalpoints,
'matches',totalmatches,'streak',winstreak,'revision',revora_revision,
'awards',(SELECT json_agg(a ORDER BY a.run) FROM revora_champion_result_awards a WHERE a.uid=c.entityid))::text
FROM championarena c WHERE entityid=@uid","uid",(long)uid));
    }
    static void ChampionResultProcess(bool reload,bool outcomeEdition=false) {
        championResultFixture=true;uint first=outcomeEdition?4294915101u:4294914701u;
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0) {
            Require(!reload);
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0);
            nextUid=first;for(int n=0;n<6;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,class=15,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5);
            Sql("CREATE TABLE IF NOT EXISTS revora_champion_result_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        nextUid=first;var players=Enumerable.Range(0,6).Select(n=>Player(Kernel.Maps[1002],true)).ToArray();
        foreach(var p in players){p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(reload) {
            foreach(var p in players) {
                Require(ChampionResultSnapshot(p.Entity.UID)==Convert.ToString(Sql("SELECT snapshot FROM revora_champion_result_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
                Require(p.ChampionStats.Points==Convert.ToUInt32(Sql("SELECT points FROM championarena WHERE entityid=@uid","uid",(long)p.Entity.UID)));
                Require(!p.ChampionStats.SignedUp && p.Map.ID==1002);
                ChampionTable.SaveStats(p.ChampionStats);
            }
        } else {
            Require((long)Sql("SELECT count(*) FROM revora_champion_result_probe WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+5)==0);
            var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
            var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
            var before=typeof(ChampionTable).GetField("ResultBeforeCommit",Hidden);var after=typeof(ChampionTable).GetField("ResultBeforeInstall",Hidden);
            Time32 now=Time32.Now;DateTime local=ChampionFixtureSignupTime(first);
            try {
                signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
                for(int pair=0;pair<3;pair++) {
                    local=ChampionFixtureSignupTime(first+(uint)(pair*2));
                    var p=players[pair*2];var other=players[pair*2+1];
                    if(pair==0)foreach(var actor in new[]{p,other}) {
                        actor.ChampionStats.Points=304390;actor.ChampionStats.Grade=7;
                        actor.ChampionStats.TodayPoints=actor.ChampionStats.TotalPoints=uint.MaxValue-1;
                        actor.ChampionStats.WinStreak=actor.ChampionStats.TotalMatches=255;ChampionTable.SaveStats(actor.ChampionStats);
                    }
                    Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
                    p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
                    local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
                    Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);Revora.Game.Champion.VerifyAwaitingPeople();
                    var group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));ushort map=p.Map.ID;
                    now=now.AddSeconds(10);
                    if(pair==0) {
                        group.UpdateDamage(p,uint.MaxValue);group.UpdateDamage(p,100);group.UpdateDamage(other,uint.MaxValue);group.UpdateDamage(other,99);
                        group.End();Require(ReferenceEquals(group.Winner,p));
                    } else if(pair==1) {group.UpdateDamage(p,50);group.UpdateDamage(other,50);group.End();Require(group.Winner==null && group.Loser==null);}
                    else group.End(other);
                    string pBefore=ChampionResultSnapshot(p.Entity.UID),otherBefore=ChampionResultSnapshot(other.Entity.UID);
                    if(pair==1) {
                        before.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture rollback");}));group.Export();before.SetValue(null,null);
                        Require(ReferenceEquals(p.ChampionGroup,group) && Kernel.Maps.ContainsKey(map));
                        Require(ChampionResultSnapshot(p.Entity.UID)==pBefore && ChampionResultSnapshot(other.Entity.UID)==otherBefore);
                    }
                    if(pair==2)after.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture lost projection");}));
                    group.Export();after.SetValue(null,null);
                    Require(p.ChampionGroup==null && other.ChampionGroup==null && !Kernel.Maps.ContainsKey(map));
                    Require((long)Sql("SELECT count(*) FROM revora_champion_result_awards WHERE uid IN(@a,@b)","a",(long)p.Entity.UID,"b",(long)other.Entity.UID)==2);
                    if(pair==0) {
                        Require(p.ChampionStats.Points==304400 && other.ChampionStats.Points==304400 && p.ChampionStats.WinStreak==255 && other.ChampionStats.WinStreak==0);
                        Require(p.ChampionStats.TotalMatches==255 && other.ChampionStats.TotalMatches==255 && p.ChampionStats.TotalPoints==uint.MaxValue);
                    } else if(pair==1)Require(p.ChampionStats.Points==6 && other.ChampionStats.Points==6 && p.ChampionStats.WinStreak==0 && other.ChampionStats.WinStreak==0);
                    else {
                        Require(PirateFenced(p) && PirateFenced(other));
                        Require(Convert.ToUInt32(Sql("SELECT points FROM championarena WHERE entityid=@uid","uid",(long)p.Entity.UID))==10);
                        Require(Convert.ToUInt32(Sql("SELECT points FROM championarena WHERE entityid=@uid","uid",(long)other.Entity.UID))==6);
                        try{ChampionTable.SaveStats(p.ChampionStats);throw new Exception("Stale save unexpectedly succeeded.");}catch(InvalidOperationException){}
                    }
                    Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
                    string pAfter=ChampionResultSnapshot(p.Entity.UID),otherAfter=ChampionResultSnapshot(other.Entity.UID);
                    System.Threading.Tasks.Parallel.For(0,8,n=>group.Export());Revora.Game.Champion.Win(p,other);
                    Require(pAfter==ChampionResultSnapshot(p.Entity.UID) && otherAfter==ChampionResultSnapshot(other.Entity.UID));
                    foreach(var actor in new[]{p,other})Sql("INSERT INTO revora_champion_result_probe VALUES(@uid,@snapshot)","uid",(long)actor.Entity.UID,"snapshot",ChampionResultSnapshot(actor.Entity.UID));
                }
            } finally {timer.SetValue(null,oldTimer);signup.SetValue(null,oldSignup);before.SetValue(null,null);after.SetValue(null,null);}
        }
        System.Console.WriteLine("[career-process] "+(outcomeEdition?"career-champion-outcome-result-":"career-champion-result-")+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion result fixture did not close.");
    }
    static bool championCombatFixture;
    static void ChampionDirectReload() {
        championDirectFixture=true;
        Require((long)Sql("SELECT count(*) FROM revora_champion_combat_probe WHERE id=2 AND passed")==1);
        nextUid=4294915301;
        for(int i=0;i<2;i++) {
            var p=Player(Kernel.Maps[1002],true);long uid=p.Entity.UID;
            uint paid=Convert.ToUInt32(Sql("SELECT sum(award) FROM revora_champion_result_awards WHERE uid=@uid","uid",uid));
            long matches=(long)Sql("SELECT count(*) FROM revora_champion_result_awards WHERE uid=@uid","uid",uid);
            Require(paid>0 && matches>0 && p.ChampionStats.Points==paid && p.ChampionStats.TodayPoints==paid && p.ChampionStats.TotalPoints==paid);
            Require(p.ChampionStats.TotalMatches==(byte)matches && p.ChampionGroup==null && p.Map.ID==1002 && !PirateFenced(p));
            Require((long)Sql("SELECT count(*) FROM revora_champion_entry_players WHERE uid=@uid AND released_at IS NULL","uid",uid)==0);
        }
        System.Console.WriteLine("[career-process] career-champion-direct-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion direct reload did not close.");
    }
    static bool championControlsFixture;
    static byte[] ChampionNpcFrame(uint npc,byte option,byte interact=0) {
        var frame=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,frame);Revora.Network.Writer.WriteUInt16(2032,2,frame);
        Revora.Network.Writer.WriteUInt32(npc,8,frame);frame[14]=option;frame[15]=interact;
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,frame);return frame;
    }
    static void ChampionChoice(GameState p,uint npc,byte option) {
        RetainedBoxOpen(p,npc);Revora.Network.PacketHandler.HandlePacket(ChampionNpcFrame(npc,option),p);
    }
    static void ChampionControlsProcess(bool reload) {
        championControlsFixture=true;const uint uid=4294915401,registrar=703001,steward=703002;
        Sql("CREATE TABLE IF NOT EXISTS revora_champion_controls_probe(uid bigint PRIMARY KEY,points bigint NOT NULL,matches integer NOT NULL)");
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);
        foreach(var player in new[]{p,other}) {
            player.LoadItemStats();Require(!player.Fake && player.Account.State==AccountTable.AccountState.Player && !PirateFenced(player));
            if(reload) {
                Require(player.ChampionStats.Points==Convert.ToUInt32(Sql("SELECT points FROM revora_champion_controls_probe WHERE uid=@uid","uid",(long)player.Entity.UID)));
                Require(player.ChampionStats.TotalMatches==Convert.ToByte(Sql("SELECT matches FROM revora_champion_controls_probe WHERE uid=@uid","uid",(long)player.Entity.UID)));
                Require(player.ChampionGroup==null && player.Map.ID==1002 && !player.ChampionStats.SignedUp);
                Require((long)Sql("SELECT count(*) FROM revora_champion_entry_players WHERE uid=@uid AND released_at IS NULL","uid",(long)player.Entity.UID)==0);
            }
        }
        Require(Kernel.Maps[1002].Npcs.ContainsKey(registrar));
        Call(typeof(Revora.Game.Champion),"PlaceRegistrar");
        Require(Kernel.Maps[1002].Npcs.Values.Count(n=>n.UID==registrar)==1);
        if(!reload) {
            Require((long)Sql("SELECT count(*) FROM revora_champion_controls_probe")==0);
            var clock=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var previousClock=clock.GetValue(null);
            var local=ChampionFixtureSignupTime(uid);bool oldInvites=AutoInvite.Enabled;
            uint before=p.ChampionStats.Points,otherBefore=other.ChampionStats.Points;
            Revora.Game.Champion.QualifierList.QualifierGroup group=null;
            try {
                clock.SetValue(null,new Func<DateTime>(()=>local));AutoInvite.Enabled=true;
                Revora.Game.Champion.Handle(p,null);Revora.Game.Champion.Handle(p,new byte[]{0});
                foreach(ushort id in new ushort[]{2600,2601,2602,2603,2604}) {
                    var disabled=new byte[12];Revora.Network.Writer.WriteUInt16(4,0,disabled);Revora.Network.Writer.WriteUInt16(id,2,disabled);
                    Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,4,disabled);Revora.Network.PacketHandler.HandlePacket(disabled,p);
                }
                Require(!p.ChampionStats.SignedUp);
                local=local.AddMinutes(-1);ChampionChoice(p,registrar,1);Require(!p.ChampionStats.SignedUp);local=local.AddMinutes(1);
                RetainedBoxOpen(p,registrar);
                foreach(var bad in new[]{new byte[20],ChampionNpcFrame(registrar,1)}) {
                    if(bad.Length==20){Revora.Network.Writer.WriteUInt16(12,0,bad);Revora.Network.Writer.WriteUInt16(2032,2,bad);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,12,bad);}
                    else if(bad[17]==0){bad[17]=121;}
                    Revora.Network.PacketHandler.HandlePacket(bad,p);Require(!p.ChampionStats.SignedUp);
                }
                p.Entity.X=400;p.Entity.Y=400;Revora.Network.PacketHandler.HandlePacket(ChampionNpcFrame(registrar,1),p);Require(!p.ChampionStats.SignedUp);
                ChampionChoice(p,registrar,1);ChampionChoice(other,registrar,1);Require(p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);
                var signupStamp=p.ChampionStats.SignUpTime;ChampionChoice(p,registrar,1);Require(p.ChampionStats.SignUpTime.Value==signupStamp.Value);
                ChampionChoice(p,registrar,4);Require(!p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);ChampionChoice(p,registrar,1);
                p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();
                Require(ReferenceEquals(p.ChampionStats.Opponent,other) && p.MessageOK!=null && p.MessageCancel!=null);
                var staleYes=p.MessageOK;var staleNo=p.MessageCancel;
                // Generic message-box acceptance traverses the real2032 dispatcher.
                Revora.Network.PacketHandler.HandlePacket(ChampionNpcFrame(registrar,255,NpcReply.MessageBox),p);Require(p.ChampionStats.AcceptBox);
                ChampionChoice(other,registrar,3);Require(p.ChampionGroup==null && other.ChampionGroup==null && p.ChampionStats.Points==before && other.ChampionStats.Points==otherBefore);
                Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);local=local.Date.AddDays(7).AddHours(20).AddMinutes(55);AutoInvite.Enabled=false;
                p.MessageOK=p.MessageCancel=other.MessageOK=other.MessageCancel=null;
                ChampionChoice(p,registrar,1);ChampionChoice(other,registrar,1);
                p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();
                Require(p.MessageOK==null && other.MessageOK==null && !p.ChampionStats.AcceptBox);
                staleYes(p);staleNo(p);Require(!p.ChampionStats.AcceptBox && p.ChampionStats.SignedUp && ReferenceEquals(p.ChampionStats.Opponent,other));
                p.Entity.X=400;p.Entity.Y=400;Revora.Network.PacketHandler.HandlePacket(ChampionNpcFrame(registrar,2),p);Require(!p.ChampionStats.AcceptBox);
                ChampionChoice(p,registrar,2);ChampionChoice(other,registrar,2);Revora.Game.Champion.VerifyAwaitingPeople();
                group=p.ChampionGroup;Require(group!=null && ReferenceEquals(other.ChampionGroup,group) && p.Map.Npcs.ContainsKey(steward));
                p.ActiveNpc=steward;Revora.Network.PacketHandler.HandlePacket(ChampionNpcFrame(steward,4),p);Require(!group.Done);
                ChampionChoice(p,steward,4);Require(group.Done && ReferenceEquals(group.Winner,other));group.Export();
                Require(p.ChampionGroup==null && other.ChampionGroup==null && p.ChampionStats.Points==before+6 && other.ChampionStats.Points==otherBefore+10);
                Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
                foreach(var player in new[]{p,other})Sql("INSERT INTO revora_champion_controls_probe VALUES(@uid,@points,@matches)","uid",(long)player.Entity.UID,"points",(long)player.ChampionStats.Points,"matches",(int)player.ChampionStats.TotalMatches);
            } finally {
                AutoInvite.Enabled=oldInvites;clock.SetValue(null,previousClock);
                if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}
                Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
            }
        }
        System.Console.WriteLine("[career-process] career-champion-controls-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion controls did not close.");
    }
    static bool championLethalFixture;
    static string ChampionLethalSnapshot(GameState p,bool valuables) {
        string result=ChampionResultSnapshot(p.Entity.UID);
        if(valuables)result+="|"+Convert.ToString(Sql(@"SELECT json_build_object('gold',money,'cp',conquerpoints,
'items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid=@uid))::text FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID));
        return result;
    }
    static void ChampionLethalProcess(bool reload,bool valuables=false) {
        championLethalFixture=true;uint uid=valuables?4294915601u:4294915501u;
        Sql("CREATE TABLE IF NOT EXISTS revora_champion_lethal_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            if(valuables)Sql("UPDATE entities SET money=123456,conquerpoints=30000 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);
        foreach(var player in new[]{p,other}) {
            ChiTable.Load(player);player.LoadItemStats();Require(!player.Fake && player.Account.State==AccountTable.AccountState.Player && !PirateFenced(player));
            if(reload) {
                Require(ChampionLethalSnapshot(player,valuables)==Convert.ToString(Sql("SELECT snapshot FROM revora_champion_lethal_probe WHERE uid=@uid","uid",(long)player.Entity.UID)));
                Require(player.ChampionGroup==null && player.Map.ID==1002 && !player.Entity.Dead);
                Require(player.ChampionStats.Points==Convert.ToUInt32(Sql("SELECT points FROM championarena WHERE entityid=@uid","uid",(long)player.Entity.UID)));
            }
        }
        if(!reload) {
            Require((long)Sql("SELECT count(*) FROM revora_champion_lethal_probe WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0);
            if(valuables)foreach(var player in new[]{p,other}) {
                foreach(uint type in new uint[]{1088000,1088001})if(!player.Inventory.Objects.Any(i=>i.ID==type)) {
                    var catalog=ConquerItemInformation.BaseInformations[type];
                    Require(player.Inventory.Add(new ConquerItem(true){ID=type,Bound=false,StackSize=1,Durability=catalog.Durability,MaximDurability=catalog.Durability},Revora.Game.Enums.ItemUse.CreateAndAdd));
                }
                Require(player.Entity.Money==123456 && player.Entity.ConquerPoints==30000 && player.Inventory.Objects.Count(i=>!i.Bound)>=2);
            }
            var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
            var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
            DateTime local=ChampionFixtureSignupTime(uid);Time32 now=Time32.Now;
            Revora.Game.Champion.QualifierList.QualifierGroup group=null;
            try {
                signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
                ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);
                p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();
                ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Revora.Game.Champion.VerifyAwaitingPeople();
                group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
                p.Attackable=other.Attackable=true;now=now.AddSeconds(10);
                Require(Revora.Game.Attacking.Handle.CanAttack(p.Entity,other.Entity,null,true));
                uint before=p.ChampionStats.Points,otherBefore=other.ChampionStats.Points,hp=other.Entity.Hitpoints;
                ushort pk=p.Entity.PKPoints;ulong gold=p.Entity.Money,otherGold=other.Entity.Money;
                uint cp=p.Entity.ConquerPoints,otherCp=other.Entity.ConquerPoints;
                string items=Convert.ToString(Sql("SELECT json_agg(i ORDER BY uid)::text FROM items i WHERE entityid IN(@a,@b)","a",(long)uid,"b",(long)uid+1));
                uint damage=hp+100;
                var attack=new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=other.Entity.UID,X=other.Entity.X,Y=other.Entity.Y};
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,other.Entity,attack,ref damage,null);
                Require(other.Entity.Dead && group.Done && ReferenceEquals(group.Winner,p) && ReferenceEquals(group.Loser,other));
                Require((ReferenceEquals(group.Player1,p)?group.Player1Damage:group.Player2Damage)==hp);
                ChampionRejectedHit(p.Entity,other.Entity);
                Require(p.Entity.PKPoints==pk && p.Entity.Money==gold && other.Entity.Money==otherGold && p.Entity.ConquerPoints==cp && other.Entity.ConquerPoints==otherCp);
                Require(items==Convert.ToString(Sql("SELECT json_agg(i ORDER BY uid)::text FROM items i WHERE entityid IN(@a,@b)","a",(long)uid,"b",(long)uid+1)));
                group.Export();Require(p.ChampionGroup==null && other.ChampionGroup==null && !other.Entity.Dead && p.Map.ID==1002 && other.Map.ID==1002);
                Require(p.ChampionStats.Points==before+10 && other.ChampionStats.Points==otherBefore+6);
                string paid=ChampionResultSnapshot(p.Entity.UID),otherPaid=ChampionResultSnapshot(other.Entity.UID);
                group.Export();Require(paid==ChampionResultSnapshot(p.Entity.UID) && otherPaid==ChampionResultSnapshot(other.Entity.UID));
                foreach(var player in new[]{p,other}) {
                    Require(EntityTable.SaveEntity(player));
                    Sql("INSERT INTO revora_champion_lethal_probe VALUES(@uid,@snapshot)","uid",(long)player.Entity.UID,"snapshot",ChampionLethalSnapshot(player,valuables));
                }
            } finally {
                timer.SetValue(null,oldTimer);signup.SetValue(null,oldSignup);
                if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}
                Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
            }
        }
        System.Console.WriteLine("[career-process] "+(valuables?"career-champion-lethal-valuables-":"career-champion-lethal-")+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion lethal did not close.");
    }
    static bool xpTransformFixture;
    static void XpTransformProcess(bool reload) {
        xpTransformFixture=true;const uint uid=4294916501;
        Sql("CREATE TABLE IF NOT EXISTS revora_xp_transform_probe(uid bigint PRIMARY KEY,receipts text NOT NULL)");
        bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
        Require(exists || !reload);nextUid=uid;var p=Player(Kernel.Maps[1002],exists);SkillTable.LoadSpells(p);p.LoadItemStats();
        Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT coalesce(json_agg(r ORDER BY life)::text,'[]') FROM revora_xp_kills r WHERE uid=@uid","uid",(long)uid));
        Func<ushort> active=()=> (ushort)Call(Feature("XpTransformAttribution"),"Active",p.Entity);
        Action kill=()=>{
            var info=new MonsterInformation{ID=1,Name="XPTransform",Level=1,Hitpoints=10};
            var m=new Revora.Game.Entity(Revora.Game.EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPTransform",MaxHitpoints=10,MapObjType=Revora.Game.MapObjectType.Monster,IsDropped=true};
            info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);
            p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=1000;
            var attack=new Attack(true){AttackType=Attack.Melee,Attacker=uid,Attacked=m.UID};
            uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,m,ref attack);Require(damage>=10);
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,attack,ref damage,null);System.Console.WriteLine("[xp-transform-hit] damage="+damage+" hp="+m.Hitpoints+" dead="+m.Dead+" fenced="+PirateFenced(p));Require(m.Dead && !PirateFenced(p));
            Call(Feature("MonsterKoCounter"),"AcceptedDeath",m,p.Entity);p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);
        };
        if((long)Sql("SELECT count(*) FROM revora_xp_transform_probe WHERE uid=@uid","uid",(long)uid)==0) {
            Require(!reload);long before=count();Require(before<=17);ushort[] ids={1270,1280,1360};
            for(int i=0;i<ids.Length;i++) {
                ushort id=ids[i];p.Entity.ClearFlags();p.Entity.TransformationID=0;
                p.Spells[id]=new Spell(true){ID=id,Level=0,Available=true};
                p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);
                p.Spells[id].ColdTime=Time32.Now.AddSeconds(-10);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=uid,Attacked=uid,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
                System.Console.WriteLine("[xp-transform] spell="+id+" form="+p.Entity.TransformationID+" source="+active());
                Require(p.Entity.Transformed && active()==id);kill();Require(count()==before+i+1);
                var clock=Feature("XpTransformAttribution").GetField("Clock",Hidden);var previous=clock.GetValue(null);
                try {var later=p.Entity.TransformationStamp.AddSeconds(p.Entity.TransformationTime+1);clock.SetValue(null,new Func<Time32>(()=>later));Require(active()==0);kill();Require(count()==before+i+1);}
                finally{clock.SetValue(null,previous);}
                ushort form=p.Entity.TransformationID;p.Entity.TransformationID=form;Require(active()==0);kill();Require(count()==before+i+1);
                p.Entity.Untransform();Require(active()==0 && !p.Entity.Transformed);kill();Require(count()==before+i+1);
            }
            Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_xp_transform_probe VALUES(@uid,@receipts)","uid",(long)uid,"receipts",snapshot());
        }
        Require(active()==0 && snapshot()==Convert.ToString(Sql("SELECT receipts FROM revora_xp_transform_probe WHERE uid=@uid","uid",(long)uid)));
        foreach(int id in new[]{1270,1280,1360})Require((long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid AND spell=@spell","uid",(long)uid,"spell",id)>0);
        System.Console.WriteLine("[career-process] career-xp-transform-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP transform did not close.");
    }
    static bool xpEffectsFixture;
    static void XpEffectsProcess(bool reload) {
        xpEffectsFixture=true;const uint uid=4294916401;
        Sql("CREATE TABLE IF NOT EXISTS revora_xp_effects_v2_probe(uid bigint PRIMARY KEY,receipts text NOT NULL)");
        bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
        Require(exists || !reload);nextUid=uid;var p=Player(Kernel.Maps[1002],exists);SkillTable.LoadSpells(p);
        Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT coalesce(json_agg(r ORDER BY life)::text,'[]') FROM revora_xp_kills r WHERE uid=@uid","uid",(long)uid));
        Func<ushort> active=()=> (ushort)Call(Feature("XpEffectAttribution"),"Active",p.Entity);
        Action kill=()=>{
            var info=new MonsterInformation{ID=1,Name="XPEffect",Level=1,Hitpoints=10};
            var m=new Revora.Game.Entity(Revora.Game.EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPEffect",MaxHitpoints=10,MapObjType=Revora.Game.MapObjectType.Monster,IsDropped=true};
            info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);
            p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=1000;
            var attack=new Attack(true){AttackType=Attack.Melee,Attacker=uid,Attacked=m.UID};
            uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,m,ref attack);Require(damage>=10);
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,attack,ref damage,null);Require(m.Dead && !PirateFenced(p));
            Call(Feature("MonsterKoCounter"),"AcceptedDeath",m,p.Entity);p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);
        };
        if((long)Sql("SELECT count(*) FROM revora_xp_effects_v2_probe WHERE uid=@uid","uid",(long)uid)==0) {
            Require(!reload);long before=count();Require(before<=16);
            var flags=new[]{FlagsUpdate.Flags.Cyclone,FlagsUpdate.Flags.Superman,FlagsUpdate.Flags.FatalStrike,FlagsUpdate.Flags.ShurikenVortex};
            ushort[] ids={1110,1025,6011,6010};
            for(int i=0;i<ids.Length;i++) {
                ushort id=ids[i];ulong flag=(ulong)flags[i];p.Entity.ClearFlags();Require(active()==0);
                p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);kill();Require(count()==before+i);
                p.Entity.AddFlag(flag,60,true);Require(active()==0);kill();Require(count()==before+i);p.Entity.RemoveFlag(flag);
                p.Spells[id]=new Spell(true){ID=id,Level=0,Available=true};
                var definition=SpellTable.GetSpell(id,0);
                if(definition.OnlyWithThisWeaponSubtype.Count>0) {p.Entity.Class=55;p.Entity.Level=120;p.Entity.Strength=2000;p.Entity.Agility=2000;}
                if(definition.OnlyWithThisWeaponSubtype.Count>0 && p.Equipment.TryGetItem(ConquerItem.RightWeapon)==null) {
                    var type=ConquerItemInformation.BaseInformations.Where(kv=>definition.OnlyWithThisWeaponSubtype.Contains((ushort)(kv.Key/1000)) && ItemHandler.EquipPassStatsReq(kv.Value,p) && ItemHandler.EquipPassJobReq(kv.Value,p)).OrderBy(kv=>kv.Key).First().Key;
                    var weapon=p.Inventory.Objects.FirstOrDefault(item=>item.ID==type) ?? BattlePowerGear(p,type,0,0);
                    RetainedUpgradeUsage(p,weapon.UID,ConquerItem.RightWeapon,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(ConquerItem.RightWeapon)!=null);
                }
                p.LoadItemStats();p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);
                p.Spells[id].ColdTime=Time32.Now.AddSeconds(-10);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=uid,Attacked=uid,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
                System.Console.WriteLine("[xp-effects] spell="+id+" flag="+p.Entity.ContainsFlag(flag)+" source="+active());Require(p.Entity.ContainsFlag(flag) && active()==id);kill();Require(count()==before+i+1);
                var clock=Feature("XpEffectAttribution").GetField("Clock",Hidden);var previous=clock.GetValue(null);
                try {
                    var extended=DateTime.Now.AddSeconds(definition.Duration+10);p.Entity.UpdateXP(flag,120,true);
                    clock.SetValue(null,new Func<DateTime>(()=>extended));Require(active()==id);
                    p.Entity.UpdateXP(flag,120,false);Require(active()==0);
                    var later=DateTime.Now.AddDays(2);clock.SetValue(null,new Func<DateTime>(()=>later));Require(active()==0);kill();Require(count()==before+i+1);
                }
                finally{clock.SetValue(null,previous);}
                p.Entity.RemoveFlag(flag);p.Entity.AddFlag(flag,60,true);Require(active()==0);kill();Require(count()==before+i+1);
            }
            p.Entity.ClearFlags();Require(active()==0 && EntityTable.SaveEntity(p));
            Sql("INSERT INTO revora_xp_effects_v2_probe VALUES(@uid,@receipts)","uid",(long)uid,"receipts",snapshot());
        }
        Require(active()==0 && snapshot()==Convert.ToString(Sql("SELECT receipts FROM revora_xp_effects_v2_probe WHERE uid=@uid","uid",(long)uid)));
        foreach(int id in new[]{1110,1025,6011,6010})Require((long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid AND spell=@spell","uid",(long)uid,"spell",id)>0);
        System.Console.WriteLine("[career-process] career-xp-effects-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP effects did not close.");
    }
    static bool xpRecoveryFixture;
    static void XpRecoveryProcess(bool reload) {
        xpRecoveryFixture=true;const uint uid=4294916301;
        Sql("CREATE TABLE IF NOT EXISTS revora_xp_recovery_probe(uid bigint PRIMARY KEY,receipts text NOT NULL)");
        bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
        Require(exists || !reload);nextUid=uid;var p=Player(Kernel.Maps[1002],exists);var other=Player(Kernel.Maps[1002],exists);SkillTable.LoadSpells(p);
        p.Spells[1010]=new Spell(true){ID=1010,Level=0,Available=true};var spell=SpellTable.GetSpell(1010,0);
        Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT coalesce(json_agg(r ORDER BY life)::text,'[]') FROM revora_xp_kills r WHERE uid=@uid","uid",(long)uid));
        Func<Revora.Game.Entity> monster=()=>{
            var info=new MonsterInformation{ID=1,Name="XPRecovery",Level=1,Hitpoints=10};
            var m=new Revora.Game.Entity(Revora.Game.EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPRecovery",MaxHitpoints=10,MapObjType=Revora.Game.MapObjectType.Monster,IsDropped=true};
            info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);return m;
        };
        Action<Revora.Game.Entity> cast=m=>{
            p.Entity.MagicAttack=100000;p.Entity.Mana=10000;p.Entity.Stamina=150;
            p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[1010].ColdTime=Time32.Now.AddSeconds(-10);
            p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
            new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=1010,Attacker=uid,Attacked=m.UID,X=m.X,Y=m.Y},p.Entity,m);
        };
        Action<GameState,Revora.Game.Entity> ordinary=(owner,m)=>{uint damage=100;Revora.Game.Attacking.Handle.ReceiveAttack(owner.Entity,m,new Attack(true){AttackType=Attack.Melee},ref damage,null);};
        Action<Revora.Game.Entity> remove=m=>{p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);};
        if((long)Sql("SELECT count(*) FROM revora_xp_recovery_probe WHERE uid=@uid","uid",(long)uid)==0) {
            Require(!reload);long before=count();Require(before<=17);
            var m=monster();
            Sql("CREATE OR REPLACE FUNCTION xp_recovery_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Sql("CREATE TRIGGER xp_recovery_suppress BEFORE INSERT ON revora_xp_kills FOR EACH ROW WHEN (NEW.uid=4294916301) EXECUTE FUNCTION xp_recovery_suppress()");
            try{cast(m);System.Console.WriteLine("[xp-recovery] rollback dead="+m.Dead+" hp="+m.Hitpoints+" count="+count()+" before="+before+" fenced="+PirateFenced(p));Require(!m.Dead && m.Hitpoints==10 && count()==before && !PirateFenced(p));}
            finally{Sql("DROP TRIGGER xp_recovery_suppress ON revora_xp_kills;DROP FUNCTION xp_recovery_suppress()");}
            cast(m);Require(m.Dead && count()==before+1);remove(m);
            m=monster();var hook=Feature("XpKillLedger").GetField("AfterCommit",Hidden);var previous=hook.GetValue(null);
            try{hook.SetValue(null,new Action(()=>{throw new InvalidOperationException("fixture XP postcommit interruption");}));cast(m);}
            finally{hook.SetValue(null,previous);}
            Require(!m.Dead && m.Hitpoints==10 && count()==before+2 && !PirateFenced(p));
            string committed=snapshot();ordinary(other,m);Require(!m.Dead && snapshot()==committed);
            ordinary(p,m);Require(m.Dead && snapshot()==committed);ordinary(p,m);Require(snapshot()==committed);remove(m);
            m=monster();
            Sql("CREATE FUNCTION xp_recovery_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Sql("CREATE TRIGGER xp_recovery_suppress BEFORE INSERT ON revora_xp_kills FOR EACH ROW WHEN (NEW.uid=4294916301) EXECUTE FUNCTION xp_recovery_suppress()");
            try{cast(m);Require(!m.Dead && snapshot()==committed);}
            finally{Sql("DROP TRIGGER xp_recovery_suppress ON revora_xp_kills;DROP FUNCTION xp_recovery_suppress()");}
            ordinary(p,m);Require(m.Dead && snapshot()==committed);remove(m);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(other));
            Sql("INSERT INTO revora_xp_recovery_probe VALUES(@uid,@receipts)","uid",(long)uid,"receipts",snapshot());
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT receipts FROM revora_xp_recovery_probe WHERE uid=@uid","uid",(long)uid)));
        Require((long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid+1)==0);
        System.Console.WriteLine("[career-process] career-xp-recovery-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP recovery did not close.");
    }
    static bool xpKillsFixture;
    static void XpKillsProcess(bool reload) {
        xpKillsFixture=true;const uint uid=4294916201;
        bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
        Require(exists || !reload);nextUid=uid;var p=Player(Kernel.Maps[1002],exists);SkillTable.LoadSpells(p);
        p.Spells[1010]=new Spell(true){ID=1010,Level=0,Available=true};
        var spell=SpellTable.GetSpell(1010,0);Require(spell!=null && spell.NeedXP==1);
        Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
        Func<Revora.Game.Entity> monster=()=>{
            var info=new MonsterInformation{ID=1,Name="XPProbe",Level=1,Hitpoints=10};
            var m=new Revora.Game.Entity(Revora.Game.EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPProbe",MaxHitpoints=10,MapObjType=Revora.Game.MapObjectType.Monster,IsDropped=true};
            info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);return m;
        };
        if(!reload) {
            var ordinary=monster();uint damage=100;long before=count();
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,ordinary,new Attack(true){AttackType=Attack.Magic},ref damage,spell);
            Require(ordinary.Dead && count()==before);
            Call(Feature("MonsterKoCounter"),"AcceptedDeath",ordinary,p.Entity);Require(count()==before);
            p.Screen.Remove(ordinary);p.Map.Entities.Remove(ordinary.UID);
            bool capChecked=false;
            while(count()<20 || !capChecked) {
                var m=monster();long n=count();if(n==20)capChecked=true;
                p.Entity.MagicAttack=100000;p.Entity.Mana=10000;p.Entity.Stamina=150;
                p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[1010].ColdTime=Time32.Now.AddSeconds(-10);
                p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                var attack=new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=1010,Attacker=uid,Attacked=m.UID,X=m.X,Y=m.Y};
                new Revora.Game.Attacking.Handle(attack,p.Entity,m);
                Require(m.Dead && !PirateFenced(p));Require(count()==Math.Min(20,n+1));
                Call(Feature("MonsterKoCounter"),"AcceptedDeath",m,p.Entity);Require(count()==Math.Min(20,n+1));
                p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);
            }
            Require(EntityTable.SaveEntity(p));
        }
        Require(count()==20 && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid AND spell=1010 AND family=1 AND map=1002","uid",(long)uid)==20);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=103","uid",(long)uid)==0);
        System.Console.WriteLine("[career-process] career-xp-kills-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP kills did not close.");
    }
    static bool xpAttributionFixture;
    static void XpAttributionProcess() {
        xpAttributionFixture=true;const uint uid=4294916101;
        bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
        nextUid=uid;var p=Player(Kernel.Maps[1002],exists);SkillTable.LoadSpells(p);
        var spell=SpellTable.GetSpell(1010,0);Require(spell!=null && spell.NeedXP==1);
        p.Spells[1010]=new Spell(true){ID=1010,Level=0,Available=true};
        Func<bool> direct=()=> (bool)Call(Feature("XpCastAttribution"),"Direct",p.Entity,spell);
        Action ready=()=>p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
        Action prepare=()=>Revora.Game.Attacking.Handle.PrepareSpell(spell,p);
        Require(!direct());ready();prepare();Require(!direct());
        using((IDisposable)Call(Feature("XpCastAttribution"),"Begin",p.Entity)) {
            Require(!direct());prepare();Require(!direct());
            ready();Require(Revora.Game.Attacking.Handle.CanUseSpell(spell,p));prepare();Require(direct());
            Require(!p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList));
            Require(!(bool)Call(Feature("XpCastAttribution"),"Direct",p.Entity,new SpellInformation{ID=spell.ID,Level=spell.Level,NeedXP=1}));
            System.Threading.Tasks.Task.Run(()=>Require(!direct())).GetAwaiter().GetResult();
            using((IDisposable)Call(Feature("XpCastAttribution"),"Begin",p.Entity)) { Require(!direct()); }
            Require(direct());
            ready();prepare();Require(direct());
            p.Entity.MapID=1000;p.Entity.MapID=1002;Require(!direct());
        }
        Require(!direct());
        try {using((IDisposable)Call(Feature("XpCastAttribution"),"Begin",p.Entity)){ready();prepare();Require(direct());throw new InvalidOperationException("fixture XP scope unwind");}}
        catch(InvalidOperationException ex){Require(ex.Message=="fixture XP scope unwind");}
        Require(!direct());
        using((IDisposable)Call(Feature("XpCastAttribution"),"Begin",p.Entity)) {
            p.Spells.Remove(1010);ready();prepare();Require(!direct());
            p.Spells[1010]=new Spell(true){ID=1010,Level=0,Available=true};ready();prepare();Require(direct());
            var ordinary=SpellTable.GetSpell(1000,0);Require(ordinary!=null && ordinary.NeedXP==0);
            Revora.Game.Attacking.Handle.PrepareSpell(ordinary,p);Require(!direct());
        }
        Require(!PirateFenced(p));System.Console.WriteLine("[career-process] career-xp-attribution-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("XP attribution did not close.");
    }
    static bool championClaimFixture;
    static string ChampionClaimSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,'champion',@champion::json,
'progress',(SELECT json_build_array(level,experience,reborn,expballs,enlightenpoints) FROM entities WHERE uid=@uid),
'ball_day',(SELECT row_to_json(d) FROM revora_exp_ball_days d WHERE uid=@uid))::text",
            "base",NobilityClaimSnapshot(p),"champion",ChampionResultSnapshot(p.Entity.UID),"uid",(long)p.Entity.UID));
    }
    static void ChampionClaimProcess(bool reload) {
        championClaimFixture=true;const uint uid=4294916001;
        Sql("CREATE TABLE IF NOT EXISTS revora_champion_claim_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL);CREATE TABLE IF NOT EXISTS revora_champion_claim_checks(id integer PRIMARY KEY,negatives boolean NOT NULL)");
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+2)==0) {
            Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=120,reborn=1,experience=0,expballs=0,enlightenpoints=0 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+2);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);var outsider=Player(Kernel.Maps[1002],true);
        foreach(var actor in new[]{p,other,outsider}){SkillTable.LoadSpells(actor);ChiTable.Load(actor);WayofHeroes.Load(actor);actor.LoadItemStats();Require(!actor.Fake && actor.Account.State==AccountTable.AccountState.Player && !PirateFenced(actor));}
        Action<GameState> denied=actor=>{string before=ChampionClaimSnapshot(actor);NobilityClaimPacket(actor,505);Require(before==ChampionClaimSnapshot(actor) && !PirateFenced(actor));};
        if((long)Sql("SELECT count(*) FROM revora_champion_claim_probe")==0) {
            Require(!reload);var engine=typeof(Revora.Game.Champion.QualifyEngine);var clock=engine.GetField("RegistrationClock",Hidden);var oldClock=clock.GetValue(null);
            var entry=typeof(Revora.Game.Champion.QualifierList.QualifierGroup).GetField("EntryProbe",Hidden);var oldEntry=entry.GetValue(null);
            DateTime local=ChampionFixtureSignupTime(uid);Revora.Game.Champion.QualifierList.QualifierGroup group=null;
            try {
                clock.SetValue(null,new Func<DateTime>(()=>local));
                Action signup=()=>{ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);Require(p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);};
                Action offer=()=>{p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));};
                Action accept=()=>{ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Revora.Game.Champion.VerifyAwaitingPeople();};
                if((long)Sql("SELECT count(*) FROM revora_champion_claim_checks WHERE id=1 AND negatives")==0) {
                    denied(p);denied(other);denied(outsider);
                    byte saved=outsider.ChampionStats.TotalMatches;outsider.ChampionStats.TotalMatches=255;denied(outsider);outsider.ChampionStats.TotalMatches=saved;
                    signup();denied(p);offer();ChampionChoice(other,703001,3);denied(p);denied(other);Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
                    local=local.Date.AddDays(7).AddHours(20).AddMinutes(55);signup();offer();
                    entry.SetValue(null,new Action<string>(stage=>{if(stage=="reserved")denied(p);if(stage=="first-arrived"){denied(p);denied(other);throw new InvalidOperationException("fixture interrupted paired entry");}}));
                    accept();entry.SetValue(null,oldEntry);Require(p.ChampionGroup==null && other.ChampionGroup==null);denied(p);denied(other);
                    Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
                    Sql("INSERT INTO revora_champion_claim_checks VALUES(1,true)");System.Console.WriteLine("[champion-claim] signup, refusal, partial transfer and forged runtime progress refused");
                }
                bool entered=(bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_champion_entries e JOIN revora_champion_entry_players a ON a.run=e.id WHERE a.uid=@uid AND e.entered_at IS NOT NULL AND e.state IN('entered','returned','interrupted'))","uid",(long)uid);
                if(!entered) {
                    local=ChampionFixtureSignupTime(uid);signup();offer();accept();group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
                    Require((long)Sql("SELECT count(*) FROM revora_champion_entries e JOIN revora_champion_entry_players a ON a.run=e.id WHERE a.uid=@uid AND e.entered_at IS NOT NULL AND e.state='entered'","uid",(long)uid)==1);
                    group.End(other);group.Export();Require(p.ChampionGroup==null && other.ChampionGroup==null);
                }
                denied(outsider);
                foreach(var actor in new[]{p,other}) {
                    if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=505","uid",(long)actor.Entity.UID)==0) {
                        actor.Trade.InTrade=true;denied(actor);actor.Trade.InTrade=false;
                        System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(actor,505));
                    }
                    Require(!PirateFenced(actor) && actor.Way2Heroes[5].SubStages[505].Done && actor.Way2Heroes[5].SubStages[505].points==1);
                    var reward=actor.Inventory.Objects.SingleOrDefault(item=>item.ID==723700);
                    if(reward!=null) {
                        Require(reward.Bound && reward.StackSize==1);ulong experience=actor.Entity.Experience,award=actor.ExpBall;byte level=actor.Entity.Level;
                        SoulClaimUse(actor,reward);Require(actor.Entity.Level==level && actor.Entity.Experience==experience+award && actor.ExpBalls==1);
                    }
                    Require(actor.Inventory.Count==0 && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=505 AND prize_type=723700 AND prize_count=1 AND NOT legacy","uid",(long)actor.Entity.UID)==1);
                    Require(EntityTable.SaveEntity(actor));string snapshot=ChampionClaimSnapshot(actor);NobilityClaimPacket(actor,505);Require(snapshot==ChampionClaimSnapshot(actor));
                    Sql("INSERT INTO revora_champion_claim_probe VALUES(@uid,@snapshot)","uid",(long)actor.Entity.UID,"snapshot",snapshot);
                }
            } finally {entry.SetValue(null,oldEntry);clock.SetValue(null,oldClock);if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);}
        }
        Require((long)Sql("SELECT count(*) FROM revora_champion_claim_probe")==2);
        foreach(var actor in new[]{p,other}) {
            Require(actor.Entity.MapID==1002 && actor.Inventory.Count==0 && actor.Way2Heroes[5].SubStages[505].Done && !PirateFenced(actor));
            string snapshot=ChampionClaimSnapshot(actor);Require(snapshot==Convert.ToString(Sql("SELECT snapshot FROM revora_champion_claim_probe WHERE uid=@uid","uid",(long)actor.Entity.UID)));
            NobilityClaimPacket(actor,505);Require(snapshot==ChampionClaimSnapshot(actor));
        }
        denied(outsider);System.Console.WriteLine("[career-process] career-champion-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion claim did not close.");
    }
    static bool championRuneFixture;
    static void ChampionRuneProcess() {
        championRuneFixture=true;const uint uid=4294915901;
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=75,level=140,reborn=2 WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
        var selected=new System.Collections.Generic.List<ConquerItem>();
        var canUse=typeof(RunesSystem).Assembly.GetType("Revora.Network.GamePackets.RuneProgress").GetMethod("CanUse",Hidden,null,new[]{typeof(GameState),typeof(uint)},null);Require(canUse!=null);
        for(uint family=401;family<=403;family++) {
            uint runeType=ConquerItemInformation.BaseInformations.Keys.Where(id=>id/10000==family && (bool)canUse.Invoke(null,new object[]{p,id})).OrderByDescending(id=>id%100).ThenBy(id=>id).First();
            var rune=p.RunesItem.Values.FirstOrDefault(item=>item.ID==runeType) ?? p.Inventory.Objects.FirstOrDefault(item=>item.ID==runeType);
            if(rune==null){rune=new ConquerItem(true){ID=runeType,Bound=true,StackSize=1,Durability=1,MaximDurability=1};Require(p.Inventory.Add(rune,Revora.Game.Enums.ItemUse.CreateAndAdd));}
            if(rune.Position==0)RetainedUpgradeUsage(p,rune.UID,0,(ItemUsage.Mode)61);
            byte slot=(byte)(family==401?101:family==402?102:103);if(rune.Position!=slot)RetainedUpgradeUsage(p,rune.UID,slot,(ItemUsage.Mode)57);
            Require(rune.Position==slot && p.RunesItem.ContainsKey(rune.UID) && !PirateFenced(p));selected.Add(rune);
        }
        p.LoadItemStats();other.LoadItemStats();
        var ids=selected.Select(item=>(ushort)(ConquerItemInformation.BaseInformations[item.ID].subtype/100)).ToArray();Require(ids.All(id=>p.Spells.ContainsKey(id)) && p.Entity.TortoiseBreaker>0);
        if(!p.Spells.ContainsKey(11110))p.AddSpell(new Spell(true){ID=11110,Level=SpellTable.SpellInformations[11110].Keys.Min()});SkillTable.SaveSpells(p);Require(!PirateFenced(p));
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT coalesce(json_agg(row_to_json(i) ORDER BY uid)::text,'[]') FROM items i WHERE entityid=@uid AND position BETWEEN 101 AND 108","uid",(long)uid));string before=snapshot();
        var flags=new[]{FlagsUpdate.Flags.Rampage,FlagsUpdate.Flags.Infinity,FlagsUpdate.Flags.Absolution,FlagsUpdate.Flags.IronShield,FlagsUpdate.Flags.Slayer,FlagsUpdate.Flags.Sacrifice,FlagsUpdate.Flags.FrostArrows};
        foreach(var flag in flags)p.Entity.AddFlag((ulong)flag,60,true);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,60,true);
        p.Entity.InfinityDamage=100;p.Entity.FineRainTime=60;p.Entity.IronShieldTime=60;
        var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
        var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
        DateTime local=ChampionFixtureSignupTime(uid);Time32 now=Time32.Now;Revora.Game.Champion.QualifierList.QualifierGroup group=null;
        try {
            signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
            ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Revora.Game.Champion.VerifyAwaitingPeople();
            group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));now=now.AddSeconds(10);
            Require(ids.All(id=>!p.Spells.ContainsKey(id)) && p.Spells.ContainsKey(11110) && snapshot()==before);
            Require(flags.All(flag=>!p.Entity.ContainsFlag((ulong)flag)) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly));
            Require(p.Entity.TortoiseBreaker==0 && p.Entity.InfinityDamage==0 && p.Entity.FineRainTime==0 && p.Entity.IronShieldTime==0);
            var runeSpell=SpellTable.SpellInformations[ids[0]].Values.First();
            Require(!Revora.Game.Attacking.Handle.CanUseSpell(runeSpell,p));
            foreach(ushort id in RunesSystem.RunesSpells){var info=SpellTable.SpellInformations[id].Values.First();RunesSystem.HandleRedBlueRunesSkills(null,p.Entity,other.Entity,info,p.Entity.X,p.Entity.Y);}
            Require(flags.All(flag=>!p.Entity.ContainsFlag((ulong)flag)));
            var runeItem=selected[0];RetainedUpgradeUsage(p,runeItem.UID,0,(ItemUsage.Mode)58);RetainedUpgradeUsage(p,runeItem.UID,101,(ItemUsage.Mode)57);Require(snapshot()==before && ids.All(id=>!p.Spells.ContainsKey(id)));
            SkillTable.SaveSpells(p);Require(!PirateFenced(p));
            Require((long)Sql("SELECT count(*) FROM skills WHERE entityid=@uid AND id=11110","uid",(long)uid)==1);
            // A stale queued rune trap remains harmless even if an in-memory capability is forged.
            if(!p.Spells.ContainsKey(14250))p.Spells.Add(14250,new Spell(true){ID=14250,Level=SpellTable.SpellInformations[14250].Keys.Min()});
            uint hp=other.Entity.Hitpoints;var trap=new FloorItem(true){UID=FloorItem.FloorUID.Next,ItemID=FloorItem.TideTrap,MapID=p.Map.ID,Type=FloorItem.Effect,Owner=p,OwnerUID=uid,X=60,Y=60,X2=58,Y2=58,TideTrapOnPlayer=123};p.Map.AddFloorItem(trap);
            Revora.Game.Attacking.Handle.TideTrap(trap);Require(other.Entity.Hitpoints==hp && !p.Map.FloorItems.ContainsKey(trap.UID));p.LoadItemStats();
            group.End(other);group.Export();Require(p.ChampionGroup==null && p.Entity.TortoiseBreaker>0 && ids.All(id=>p.Spells.ContainsKey(id)) && snapshot()==before && !PirateFenced(p));
            SkillTable.SaveSpells(p);Require(!PirateFenced(p));
            Require((long)Sql("SELECT count(*) FROM skills WHERE entityid=@uid AND id=11110","uid",(long)uid)==1);
        } finally {timer.SetValue(null,oldTimer);signup.SetValue(null,oldSignup);if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);}
        System.Console.WriteLine("[career-process] career-champion-runes-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion runes did not close.");
    }
    static bool championTrapFixture;
    static void ChampionTrapProcess() {
        championTrapFixture=true;const uint uid=4294915701;
        Sql("CREATE TABLE IF NOT EXISTS revora_champion_trap_probe(passed boolean NOT NULL)");
        Require((long)Sql("SELECT count(*) FROM revora_champion_trap_probe")==0);
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);
        p.LoadItemStats();other.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player);
        Func<ushort,FloorItem> trap=map=>{
            var item=new FloorItem(true){UID=FloorItem.FloorUID.Next,ItemID=FloorItem.TideTrap,MapID=map,Type=FloorItem.Effect,
                Owner=p,OwnerUID=p.Entity.UID,X=60,Y=60,X2=58,Y2=58,TideTrapOnPlayer=123,OnFloor=Time32.Now};
            Kernel.Maps[map].AddFloorItem(item);return item;
        };
        var fromTown=trap(1002);
        var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
        var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
        DateTime local=ChampionFixtureSignupTime(uid);Time32 now=Time32.Now;
        Revora.Game.Champion.QualifierList.QualifierGroup group=null;
        try {
            signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
            ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Revora.Game.Champion.VerifyAwaitingPeople();
            group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
            // Supply the trap spell capability; rune acquisition is tested elsewhere.
            byte level=SpellTable.SpellInformations[14250].Keys.Min();p.Spells.Add(14250,new Spell(true){ID=14250,Level=level});
            other.Entity.X=60;other.Entity.Y=60;p.Attackable=other.Attackable=true;
            typeof(Revora.Game.Screen).GetField("_objects",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p.Screen,new Revora.Interfaces.IMapObject[]{other.Entity});
            uint hp=other.Entity.Hitpoints;
            var early=trap(p.Map.ID);Revora.Game.Attacking.Handle.TideTrap(early);Require(other.Entity.Hitpoints==hp && !p.Map.FloorItems.ContainsKey(early.UID));
            now=now.AddSeconds(10);Require(Revora.Game.Attacking.Handle.CanAttack(p.Entity,other.Entity,null,true));
            Revora.Game.Attacking.Handle.TideTrap(fromTown);Require(other.Entity.Hitpoints==hp && !Kernel.Maps[1002].FloorItems.ContainsKey(fromTown.UID));
            var missing=trap(p.Map.ID);p.Map.RemoveFloorItem(missing);Revora.Game.Attacking.Handle.TideTrap(missing);Require(other.Entity.Hitpoints==hp);
            var forged=trap(p.Map.ID);forged.OwnerUID=other.Entity.UID;Revora.Game.Attacking.Handle.TideTrap(forged);Require(other.Entity.Hitpoints==hp);p.Map.RemoveFloorItem(forged);
            var active=trap(p.Map.ID);
            System.Threading.Tasks.Parallel.Invoke(()=>Revora.Game.Attacking.Handle.TideTrap(active),()=>Revora.Game.Attacking.Handle.TideTrap(active));
            uint score=ReferenceEquals(group.Player1,p)?group.Player1Damage:group.Player2Damage;
            Require(other.Entity.Hitpoints==hp && score==0 && active.TideTrapOnPlayer==123 && !p.Map.FloorItems.ContainsKey(active.UID));
            Revora.Game.Attacking.Handle.TideTrap(active);Require(other.Entity.Hitpoints==hp);
            group.End(other);var ended=trap(p.Map.ID);Revora.Game.Attacking.Handle.TideTrap(ended);Require(other.Entity.Hitpoints==hp && !p.Map.FloorItems.ContainsKey(ended.UID));
            group.Export();Require(p.ChampionGroup==null && other.ChampionGroup==null && !PirateFenced(p) && !PirateFenced(other));
            Sql("INSERT INTO revora_champion_trap_probe VALUES(true)");
        } finally {
            timer.SetValue(null,oldTimer);signup.SetValue(null,oldSignup);
            if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);Kernel.Maps[1002].RemoveFloorItem(fromTown);
        }
        System.Console.WriteLine("[career-process] career-champion-trap-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion trap did not close.");
    }
    static bool championBladeFixture;
    static void ChampionBladeProcess() {
        championBladeFixture=true;const uint uid=4294915801;
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+1)==0) {
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=75 WHERE uid=@uid","uid",(long)uid);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);p.LoadItemStats();other.LoadItemStats();
        var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);var oldSignup=signup.GetValue(null);
        var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);var oldTimer=timer.GetValue(null);
        var twilightClock=typeof(Revora.Game.Attacking.Handle).GetField("TwilightClock",Hidden);var oldTwilight=twilightClock.GetValue(null);
        byte twilightLevel=SpellTable.SpellInformations[12070].Keys.Min();p.Spells.Add(12070,new Spell(true){ID=12070,Level=twilightLevel});
        var twilightSpell=SpellTable.SpellInformations[12070][twilightLevel];
        Func<Action> twilight=()=> (Action)Call(typeof(Revora.Game.Attacking.Handle),"CreateTwilightPulse",p.Entity,twilightSpell,(ushort)44,(ushort)40);
        var town=p.Map;var oldTownPulse=twilight();Require(oldTownPulse!=null);
        DateTime local=ChampionFixtureSignupTime(uid);Time32 now=Time32.Now;Time32 pulseTime=now;
        twilightClock.SetValue(null,new Func<Time32>(()=>pulseTime));
        Revora.Game.Champion.QualifierList.QualifierGroup group=null;
        try {
            signup.SetValue(null,new Func<DateTime>(()=>local));timer.SetValue(null,new Func<Time32>(()=>now));
            ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);
            local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Revora.Game.Champion.VerifyAwaitingPeople();
            group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
            int preparingFloors=p.Map.FloorItems.Count;var preparing=twilight();preparing();Require(p.Map.FloorItems.Count==preparingFloors);
            now=now.AddSeconds(10);preparing();Require(p.Map.FloorItems.Count==preparingFloors);
            int townFloors=town.FloorItems.Count;oldTownPulse();Require(town.FloorItems.Count==townFloors && p.Map.FloorItems.Count==preparingFloors);
            p.Attackable=other.Attackable=true;
            byte level=SpellTable.SpellInformations[11110].Keys.Min();p.Spells.Add(11110,new Spell(true){ID=11110,Level=level});
            var bladeSpell=SpellTable.SpellInformations[11110][level];
            System.Console.WriteLine("[champion-blade] weapon-types="+string.Join(",",bladeSpell.OnlyWithThisWeaponSubtype)+" xp="+bladeSpell.NeedXP+" distance="+bladeSpell.Distance+" automatic="+SpellTable.cheakrspell.ContainsKey(11110));
            if(bladeSpell.OnlyWithThisWeaponSubtype.Count>0 && p.Equipment.TryGetItem(ConquerItem.RightWeapon)==null) {
                var weaponType=ConquerItemInformation.BaseInformations.Where(kv=>bladeSpell.OnlyWithThisWeaponSubtype.Contains((ushort)(kv.Key/1000)) && ItemHandler.EquipPassStatsReq(kv.Value,p) && ItemHandler.EquipPassJobReq(kv.Value,p)).OrderBy(kv=>kv.Key).First().Key;
                var weapon=p.Inventory.Objects.FirstOrDefault(item=>item.ID==weaponType) ?? BattlePowerGear(p,weaponType,0,0);
                RetainedUpgradeUsage(p,weapon.UID,ConquerItem.RightWeapon,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(ConquerItem.RightWeapon)!=null);
            }
            p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=1000;
            p.Entity.CriticalStrike=0;other.Entity.Defence=0;other.Entity.Dodge=0;other.Entity.Block=0;other.Entity.MaxHitpoints=100000;
            p.Entity.MagicAttack=1000;
            uint baseline=0;uint[] formulaBaseline=null;
            var ranks=new[]{Revora.Game.ConquerStructures.NobilityRank.Knight,Revora.Game.ConquerStructures.NobilityRank.Serf,Revora.Game.ConquerStructures.NobilityRank.Baron,Revora.Game.ConquerStructures.NobilityRank.Earl,Revora.Game.ConquerStructures.NobilityRank.Duke,Revora.Game.ConquerStructures.NobilityRank.Prince,Revora.Game.ConquerStructures.NobilityRank.King};
            foreach(var attackerRank in ranks)
            foreach(var rank in ranks) {
                p.Entity.NobilityRank=attackerRank;
                p.Entity.X=40;p.Entity.Y=40;other.Entity.X=42;other.Entity.Y=40;other.Entity.NobilityRank=rank;other.Entity.Hitpoints=100000;p.Screen.Add(other.Entity);
                p.Spells[11110].ColdTime=Time32.Now.AddSeconds(-10);p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Entity.Stamina=150;p.Entity.Mana=10000;
                var attack=new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=11110,Attacker=p.Entity.UID,Attacked=other.Entity.UID,X=44,Y=40};
                Revora.Game.Entity visible;Require(p.Screen.TryGetValue(other.Entity.UID,out visible) && ReferenceEquals(visible,other.Entity));
                var formulaPacket=new Attack(true){AttackType=Attack.Magic};
                var formulas=new[]{Revora.Game.Attacking.Calculate.Melee(p.Entity,other.Entity,bladeSpell,ref formulaPacket),Revora.Game.Attacking.Calculate.Rango(p.Entity,other.Entity,bladeSpell,ref formulaPacket),Revora.Game.Attacking.Calculate.Magic(p.Entity,other.Entity,bladeSpell,ref formulaPacket),Revora.Game.Attacking.Calculate.Magico(p.Entity,other.Entity,bladeSpell,ref formulaPacket)};
                Require(formulas.All(value=>value>0));
                if(formulaBaseline==null)formulaBaseline=formulas;else Require(formulaBaseline.SequenceEqual(formulas));
                new Revora.Game.Attacking.Handle(attack,p.Entity,other.Entity);
                uint hit=100000-other.Entity.Hitpoints;
                System.Console.WriteLine("[champion-blade] attacker="+attackerRank+" target="+rank+" damage="+hit+" formulas="+string.Join(",",formulas));
                Require(!group.Done && !other.Entity.Dead && hit>0 && hit<10000);
                if(baseline==0)baseline=hit;else Require(hit==baseline);
            }
            p.Entity.X=40;p.Entity.Y=40;other.Entity.Hitpoints=100000;
            var arena=p.Map;var path=new Revora.Game.Attacking.InLineAlgorithm(40,44,40,40,(byte)twilightSpell.Distance).lcoords.ToArray();Require(path.Length>0);
            Action placeTarget=()=>{other.Entity.X=(ushort)path[Math.Min(path.Length-1,path.Length/3)].X;other.Entity.Y=(ushort)path[Math.Min(path.Length-1,path.Length/3)].Y;p.Screen.Add(other.Entity);};
            placeTarget();var cast=twilight();Require(cast!=null);int floors=arena.FloorItems.Count;uint hp=other.Entity.Hitpoints;
            System.Threading.Tasks.Parallel.Invoke(cast,cast);
            uint firstHit=hp-other.Entity.Hitpoints;Require(firstHit>0 && arena.FloorItems.Count==floors+1);
            hp=other.Entity.Hitpoints;cast();pulseTime=pulseTime.AddMilliseconds(399);cast();Require(other.Entity.Hitpoints==hp && arena.FloorItems.Count==floors+1);
            pulseTime=pulseTime.AddMilliseconds(1);
            other.Entity.X=(ushort)path[Math.Min(path.Length-1,2*(path.Length/3))].X;other.Entity.Y=(ushort)path[Math.Min(path.Length-1,2*(path.Length/3))].Y;
            cast();Require(hp-other.Entity.Hitpoints==firstHit && arena.FloorItems.Count==floors+2);
            hp=other.Entity.Hitpoints;pulseTime=pulseTime.AddMilliseconds(400);
            other.Entity.X=(ushort)path[Math.Min(path.Length-1,3*(path.Length/3))].X;other.Entity.Y=(ushort)path[Math.Min(path.Length-1,3*(path.Length/3))].Y;
            cast();Require(hp-other.Entity.Hitpoints==firstHit && arena.FloorItems.Count==floors+3);
            hp=other.Entity.Hitpoints;pulseTime=pulseTime.AddMilliseconds(400);cast();Require(other.Entity.Hitpoints==hp && arena.FloorItems.Count==floors+3);
            var returned=twilight();p.Entity.MapID=1002;p.Entity.MapID=arena.ID;returned();Require(other.Entity.Hitpoints==hp && arena.FloorItems.Count==floors+3);
            var ended=twilight();group.End(other);ended();Require(other.Entity.Hitpoints==hp && arena.FloorItems.Count==floors+3);
            group.Export();int returnFloors=p.Map.FloorItems.Count;hp=other.Entity.Hitpoints;ended();Require(other.Entity.Hitpoints==hp && p.Map.FloorItems.Count==returnFloors);
            Require(!PirateFenced(p) && !PirateFenced(other));
            System.Console.WriteLine("[champion-twilight] three spaced pulses, duplicate, preparation, map-return and completed-match checks passed; hit="+firstHit);
        } finally {
            twilightClock.SetValue(null,oldTwilight);timer.SetValue(null,oldTimer);signup.SetValue(null,oldSignup);
            if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}
            Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);
        }
        System.Console.WriteLine("[career-process] career-champion-blade-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion blade did not close.");
    }
    static bool championDirectFixture;
    static void ChampionRejectedHit(Revora.Game.Entity attacker,Revora.Game.Entity target) {
        uint before=target.Hitpoints,damage=10;
        Revora.Game.Attacking.Handle.ReceiveAttack(attacker,target,new Attack(true){AttackType=Attack.Melee},ref damage,null);
        Require(damage==0 && target.Hitpoints==before);
    }
    static void ChampionCombatProcess(bool directEdition=false) {
        championCombatFixture=true;championDirectFixture=directEdition;uint first=directEdition?4294915301u:4294914601u;int probeId=directEdition?2:1;
        if((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2)==0) {
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2)==0);
            nextUid=first;for(int j=0;j<3;j++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=120,class=15,reborn=0 WHERE uid BETWEEN @a AND @b","a",(long)first,"b",(long)first+2);
            Sql("CREATE TABLE IF NOT EXISTS revora_champion_combat_probe(id integer PRIMARY KEY,passed boolean NOT NULL)");
        }
        Require((long)Sql("SELECT count(*) FROM revora_champion_combat_probe WHERE id=@id","id",probeId)==0);
        nextUid=first;var p=Player(Kernel.Maps[1002],true);var other=Player(Kernel.Maps[1002],true);var outsider=Player(Kernel.Maps[1002],true);
        foreach(var actor in new[]{p,other,outsider}){ChiTable.Load(actor);actor.LoadItemStats();actor.Attackable=true;Require(!PirateFenced(actor) && !actor.Fake && actor.Account.State==AccountTable.AccountState.Player);}
        var timer=typeof(Revora.Game.Champion).GetField("CombatClock",Hidden);object original=timer.GetValue(null);
        var signup=typeof(Revora.Game.Champion.QualifyEngine).GetField("RegistrationClock",Hidden);object oldSignup=signup.GetValue(null);
        DateTime local=ChampionFixtureSignupTime(first);
        Time32 start=new Time32(int.MaxValue-5000),now=start;
        Revora.Game.Champion.QualifierList.QualifierGroup group=null;
        try {
            timer.SetValue(null,new Func<Time32>(()=>now));signup.SetValue(null,new Func<DateTime>(()=>local));
            Revora.Game.Champion.QualifyEngine.DoSignup(p);Revora.Game.Champion.QualifyEngine.DoSignup(other);
            p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Revora.Game.Champion.EngagePlayers();
            Require(ReferenceEquals(p.ChampionStats.Opponent,other));Revora.Game.Champion.QualifyEngine.DoAccept(p);Revora.Game.Champion.QualifyEngine.DoAccept(other);Revora.Game.Champion.VerifyAwaitingPeople();
            group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));var map=p.Map;
            if(!ReferenceEquals(group.Player1,p)){var swap=p;p=other;other=swap;}
            uint beforePoints=p.ChampionStats.Points,otherBeforePoints=other.ChampionStats.Points;
            // The separate five-second teleport grace has expired before combat starts.
            p.Attackable=other.Attackable=true;
            Require(map.BaseID==700 && map.ID!=700 && ReferenceEquals(map,other.Map));
            Func<bool> attack=()=>Revora.Game.Attacking.Handle.CanAttack(p.Entity,other.Entity,null,true);
            Require(!attack());if(directEdition)ChampionRejectedHit(p.Entity,other.Entity);group.UpdateDamage(p,50);Require(group.Player1Damage==0 && group.Player2Damage==0);
            now=new Time32((long)start.Value+9999);Require(!attack());
            now=new Time32((long)start.Value+10000);Require(attack() && Revora.Game.Attacking.Handle.CanAttack(other.Entity,p.Entity,null,false));
            if(directEdition) {
                ChampionRejectedHit(outsider.Entity,p.Entity);ChampionRejectedHit(p.Entity,outsider.Entity);
                var pet=new Revora.Game.Entity(Revora.Game.EntityFlag.Monster,false){Owner=p,UID=4294915399,MaxHitpoints=100};
                typeof(Revora.Game.Entity).GetField("_hitpoints",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(pet,100u);
                pet.MapID=p.Entity.MapID;
                Require(!Revora.Game.Attacking.Handle.CanAttack(pet,other.Entity,null,true));
                Require(!Revora.Game.Attacking.Handle.CanAttack(other.Entity,pet,null,true));
                ChampionRejectedHit(pet,other.Entity);ChampionRejectedHit(other.Entity,pet);
                using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                    var worker=System.Threading.Tasks.Task.Run(()=>{lock(other.ItemSyncRoot){held.Set();release.Wait();}});
                    try {Require(held.Wait(5000));ChampionRejectedHit(p.Entity,other.Entity);} finally {release.Set();Require(worker.Wait(5000));}
                }
            }
            group.UpdateDamage(p,100);group.UpdateDamage(other,75);Require(group.Player1Damage==100 && group.Player2Damage==75);
            outsider.BindWorldMap(map.ID);outsider.Entity.PKMode=Revora.Game.Enums.PKMode.PK;
            Require(!Revora.Game.Attacking.Handle.CanAttack(outsider.Entity,p.Entity,null,true) && !Revora.Game.Attacking.Handle.CanAttack(p.Entity,outsider.Entity,null,true));
            outsider.ChampionGroup=group;Require(!Revora.Game.Attacking.Handle.CanAttack(outsider.Entity,p.Entity,null,true));group.UpdateDamage(outsider,50);outsider.ChampionGroup=null;
            Require(group.Player1Damage==100 && group.Player2Damage==75);
            other.ChampionGroup=null;Require(!attack());other.ChampionGroup=group;
            Kernel.GamePool[p.Entity.UID]=outsider;try{Require(!attack());group.UpdateDamage(p,50);Require(group.Player1Damage==100);}finally{Kernel.GamePool[p.Entity.UID]=p;}
            other.BindWorldMap(1002);Require(!attack());other.BindWorldMap(map.ID);Require(attack());
            // Both items would otherwise have capacity, but cannot change match stats.
            lock(p.ItemSyncRoot)Require((bool)Call(Feature("MeditationPacks"),"Capacity",p));
            var fruit=RefineryClaimGive(p,3001044);var pack=RefineryClaimGive(p,3001046);string snapshot=MeditationSnapshot(p);
            RetainedUpgradeUsage(p,fruit.UID,0,ItemUsage.Mode.EquipItem);RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(MeditationSnapshot(p)==snapshot && !PirateFenced(p));
            now=new Time32((long)start.Value+189999);Require(attack());Revora.Game.Champion.CheckGroups();Require(!group.Done);
            uint expectedScore=100;
            if(directEdition) {
                uint hp=other.Entity.Hitpoints,damage=10;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,other.Entity,new Attack(true){AttackType=Attack.Melee},ref damage,null);
                Require(damage>0 && other.Entity.Hitpoints==hp-damage && group.Player1Damage==100+damage);expectedScore=group.Player1Damage;
            }
            now=new Time32((long)start.Value+190000);Require(!attack());if(directEdition)ChampionRejectedHit(p.Entity,other.Entity);group.UpdateDamage(p,50);Require(group.Player1Damage==expectedScore);Revora.Game.Champion.CheckGroups();Require(group.Done && ReferenceEquals(group.Winner,p));
            // Cleanup now requires the paired durable result.
            outsider.BindWorldMap(1002);group.Export();Require(p.ChampionGroup==null && other.ChampionGroup==null && !Kernel.Maps.ContainsKey(map.ID));
            Require(p.ChampionStats.Points==beforePoints+10 && other.ChampionStats.Points==otherBeforePoints+6);
            Sql("INSERT INTO revora_champion_combat_probe VALUES(@id,true)","id",probeId);
        }finally{timer.SetValue(null,original);signup.SetValue(null,oldSignup);outsider.ChampionGroup=null;outsider.BindWorldMap(1002);if(group!=null && p.ChampionGroup!=null){group.End(p);group.Export();}Revora.Game.Champion.Clear(p);Revora.Game.Champion.Clear(other);}
        System.Console.WriteLine(directEdition?"[career-process] career-champion-direct-passed":"[career-process] career-champion-combat-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Champion combat fixture did not close.");
    }

}
