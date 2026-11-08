using System;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static readonly ushort[] MasteryFamilies={0,420,410,460,540,580,560,490,561,510,530,430,450,480,481,440,500,610,601,421,900,611,612,511,613,614,616,617,620,622,624,626,671,670};
    static readonly int[] MasteryAchievements={10321,10322,10323,10324,10325,10326,10327,10328,10329,10330,10331,10332,10401,10402,10403,10404,10405,10406,10407,10408,11132,11210,11211,11212,11213,11214,11215,11221,11222,11223,11302,11303,11322,11323};
    static void MasteryAwards() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);
        uint rate=Config.ProficiencyExperienceRate,mult=Config.ProficiencyExperienceRateMultiplier,vip=Config.VipProficiencyExperienceMultiplier,div=Config.TrainingGroundProficiencyExperienceDivisor;
        try {
            Config.ProficiencyExperienceRate=3;Config.ProficiencyExperienceRateMultiplier=1;p.Entity.VIPLevel=0;
            p.IncreaseProficiencyExperience(100,410);Require(p.Proficiencies[410].Level==1 && p.Proficiencies[410].Experience==300);
            p.IncreaseProficiencyExperience(0,622);Require(!p.Proficiencies.ContainsKey(622));
            p.BindWorldMap(1039);p.Entity.Gems[5]=50;p.Entity.VIPLevel=1;Config.VipProficiencyExperienceMultiplier=3;Config.TrainingGroundProficiencyExperienceDivisor=2;
            p.IncreaseProficiencyExperience(7,421);Require(p.Proficiencies[421].Level==1 && p.Proficiencies[421].Experience==45);
            p.BindWorldMap(1002);p.Entity.Gems[5]=0;p.Entity.VIPLevel=0;Config.ProficiencyExperienceRate=2;
            p.AddProficiency(new Proficiency(true){ID=601,Level=19});p.IncreaseProficiencyExperience(2147483648u,601);
            Require(p.Proficiencies[601].Level==20 && p.Proficiencies[601].Experience==0);
            Config.ProficiencyExperienceRate=1;p.AddProficiency(new Proficiency(true){ID=610,Level=19,Experience=2000000000});p.IncreaseProficiencyExperience(3000000000,610);
            Require(p.Proficiencies[610].Level==20 && p.Proficiencies[610].Experience==0);
            p.Entity.VIPLevel=1;Config.VipProficiencyExperienceMultiplier=2;p.AddProficiency(new Proficiency(true){ID=611,Level=19});p.IncreaseProficiencyExperience(2147483648u,611);
            Require(p.Proficiencies[611].Level==20 && p.Proficiencies[611].Experience==0);
            p.Entity.VIPLevel=0;p.Entity.Gems[5]=100;p.AddProficiency(new Proficiency(true){ID=612,Level=17});p.IncreaseProficiencyExperience(2147483648u,612);
            Require(p.Proficiencies[612].Level==19 && p.Proficiencies[612].Experience==1589693858u);
            p.Entity.VIPLevel=1;p.Entity.Gems[5]=ushort.MaxValue;Config.ProficiencyExperienceRate=uint.MaxValue;Config.VipProficiencyExperienceMultiplier=uint.MaxValue;
            p.AddProficiency(new Proficiency(true){ID=613,Level=1});p.IncreaseProficiencyExperience(uint.MaxValue,613);
            Require(p.Proficiencies[613].Level==20 && p.Proficiencies[613].Experience==0);
            p.IncreaseProficiencyExperience(uint.MaxValue,613);Require(p.Proficiencies[613].Experience==0);
            p.Entity.VIPLevel=0;p.Entity.Gems[5]=0;Config.ProficiencyExperienceRate=1;p.Proficiencies.Clear();
            foreach(ushort family in MasteryFamilies) {
                p.AddProficiency(new Proficiency(true){ID=family,Level=11,Experience=94999999});
                p.IncreaseProficiencyExperience(1,family);Require(p.Proficiencies[family].Level==12 && p.Proficiencies[family].Experience==0);
            }
            SkillTable.SaveProficiencies(p);
            Require((long)Sql("SELECT count(*) FROM profs WHERE entityid=@uid AND level=12","uid",(long)p.Entity.UID)==34);
            // Exercise the actual rite before any explicit achievement refresh.
            p.Entity.Level=130;p.Entity.Class=15;
            var npc=Kernel.Maps[1002].Npcs[59558];p.Entity.X=(ushort)(npc.X-1);p.Entity.Y=npc.Y;
            Require(p.Inventory.Add(721259,0,1));Require(EntityTable.SaveEntity(p));
            foreach(int id in MasteryAchievements)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==0);
            foreach(byte option in new byte[]{1,200,210,212}) {
                p.ActiveNpc=59558;Npcs.GetDialog(new NpcRequest(true){NpcID=59558,OptionID=option},p);
            }
            Sql("CREATE FUNCTION mastery_rebirth_reject() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'; CREATE TRIGGER mastery_rebirth_reject BEFORE INSERT ON revora_first_rebirths FOR EACH ROW EXECUTE FUNCTION mastery_rebirth_reject()");
            try {p.ActiveNpc=59558;Npcs.GetDialog(new NpcRequest(true){NpcID=59558,OptionID=214},p);}
            finally {Sql("DROP TRIGGER mastery_rebirth_reject ON revora_first_rebirths; DROP FUNCTION mastery_rebirth_reject()");}
            Require(p.Entity.Reborn==0 && p.Inventory.Contains(721259,1));
            Require((long)Sql("SELECT count(*) FROM profs WHERE entityid=@uid AND level=12","uid",(long)p.Entity.UID)==34);
            foreach(int id in MasteryAchievements)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==0);
            nextUid=FirstUid;p=Player(Kernel.Maps[1002],true);SkillTable.LoadProficiencies(p);
            foreach(byte option in new byte[]{1,200,210,212,214}) {
                p.ActiveNpc=59558;Npcs.GetDialog(new NpcRequest(true){NpcID=59558,OptionID=option},p);
            }
            Require(p.Entity.Reborn==1 && p.Entity.Level==15);
            Require(MasteryFamilies.All(id=>p.Proficiencies[id].Level==0 && p.Proficiencies[id].PreviousLevel==12));
            foreach(int id in MasteryAchievements)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
            MasteryLaterRite(false);
            MasteryLaterRite(true);
            System.IO.File.WriteAllText("mastery-rites.snapshot",MasteryRiteSnapshot());
            System.Console.WriteLine("[mastery-process] mastery-awards-passed");
        } finally {Config.ProficiencyExperienceRate=rate;Config.ProficiencyExperienceRateMultiplier=mult;Config.VipProficiencyExperienceMultiplier=vip;Config.TrainingGroundProficiencyExperienceDivisor=div;}
        Server.CommandsAI("@close");throw new InvalidOperationException("Mastery awards did not close.");
    }
    static void MasteryNpc(Revora.Client.GameState p,params byte[] options) {
        foreach(byte option in options){p.ActiveNpc=59558;Npcs.GetDialog(new NpcRequest(true){NpcID=59558,OptionID=option},p);}
    }
    static void MasteryPaidRequest(Revora.Client.GameState p) {
        var packet=new byte[21];Revora.Network.Writer.WriteUInt16(13,0,packet);Revora.Network.Writer.WriteUInt16(1066,2,packet);
        packet[8]=21;packet[12]=3;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,13,packet);
        Reincarnation.Process(packet,p);
    }
    static void MasteryEvidence(Revora.Client.GameState p,long expected) {
        foreach(int id in MasteryAchievements)
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==expected);
    }
    static void MasteryLaterRite(bool paid) {
        uint uid=FirstUid+(paid?2u:1u);nextUid=uid;Player(Kernel.Maps[1002]);nextUid=uid;
        var p=Player(Kernel.Maps[1002],true);
        var npc=Kernel.Maps[1002].Npcs[59558];p.Entity.X=(ushort)(npc.X-1);p.Entity.Y=npc.Y;
        p.Entity.Level=120;p.Entity.Class=15;Require(p.Inventory.Add(721259,0,1));Require(EntityTable.SaveEntity(p));
        MasteryNpc(p,1,200,210,212,214);Require(p.Entity.Reborn==1 && p.Entity.Level==15);
        p.Entity.Level=120;p.Entity.Class=15;Require(p.Inventory.Add(723701,0,1));Require(EntityTable.SaveEntity(p));
        if(paid) {
            MasteryNpc(p,2,170,180);Require(p.Entity.Reborn==2 && p.Entity.Level==15);
            p.Entity.Atributes=checked((ushort)(p.Entity.Atributes+95*Config.AttributePointsPerLevel));
            p.Entity.Level=110;p.Entity.ConquerPoints=50000;Require(p.Inventory.Add(711083,0,1));Require(EntityTable.SaveEntity(p));
        }
        p.Entity.VIPLevel=0;Config.VipProficiencyExperienceMultiplier=1;
        // Previous-level-only records must not manufacture mastery evidence.
        foreach(ushort family in MasteryFamilies)p.AddProficiency(new Proficiency(true){ID=family,Level=0,PreviousLevel=12});
        Require(((int[])Call(Feature("AchievementCatalog"),"ObserveMasteries",p)).Length==0);
        foreach(ushort family in MasteryFamilies) {
            p.Proficiencies[family].Level=11;p.Proficiencies[family].Experience=94999999;
            p.IncreaseProficiencyExperience(1,family);
            if(p.Proficiencies[family].Level!=12)System.Console.WriteLine("[mastery-process] family="+family+" level="+p.Proficiencies[family].Level+" xp="+p.Proficiencies[family].Experience+" vip="+p.Entity.VIPLevel);
            Require(p.Proficiencies[family].Level==12);
        }
        SkillTable.SaveProficiencies(p);MasteryEvidence(p,0);
        uint payment=paid?711083u:723701u;uint beforeCp=p.Entity.ConquerPoints;
        string table=paid?"revora_reincarnations":"revora_second_rebirths";
        if(paid)MasteryNpc(p,3,5);else MasteryNpc(p,2,170);
        Sql("CREATE FUNCTION mastery_later_reject() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'; CREATE TRIGGER mastery_later_reject BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION mastery_later_reject()");
        try {if(paid)MasteryPaidRequest(p);else MasteryNpc(p,180);}
        finally {Sql("DROP TRIGGER mastery_later_reject ON "+table+"; DROP FUNCTION mastery_later_reject()");}
        Require(p.Entity.Level==(paid?110:120) && p.Inventory.Contains(payment,1) && p.Entity.ConquerPoints==beforeCp);
        Require((long)Sql("SELECT conquerpoints FROM entities WHERE uid=@uid","uid",(long)uid)==beforeCp);
        Require((long)Sql("SELECT count(*) FROM "+table+" WHERE uid=@uid","uid",(long)uid)==0);
        Require((long)Sql("SELECT count(*) FROM profs WHERE entityid=@uid AND level=12","uid",(long)uid)==34);MasteryEvidence(p,0);
        nextUid=uid;p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);
        if(paid){MasteryNpc(p,3,5);MasteryPaidRequest(p);MasteryPaidRequest(p);}else{MasteryNpc(p,2,170,180,180);}
        Require(p.Entity.Reborn==2 && p.Entity.Level==15 && !p.Inventory.Contains(payment,1));
        Require(p.Entity.ConquerPoints==beforeCp-(paid?Config.SerethReincarnationCp:0));
        Require((long)Sql("SELECT count(*) FROM "+table+" WHERE uid=@uid","uid",(long)uid)==1);
        Require(MasteryFamilies.All(id=>p.Proficiencies[id].Level==0 && p.Proficiencies[id].PreviousLevel==12));MasteryEvidence(p,1);
        System.Console.WriteLine("[mastery-process] "+(paid?"paid-reincarnation":"second-rebirth")+" rollback, retry and duplicate refusal passed");
    }
    static string MasteryRiteSnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,entityid,id,bound,position,stacksize FROM items WHERE entityid BETWEEN @first AND @last) r),
'characters',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,level,class,reborn,conquerpoints FROM entities WHERE uid BETWEEN @first AND @last) r),
'profs',(SELECT json_agg(r ORDER BY entityid,id) FROM (SELECT entityid,id,level,previouslevel,experience FROM profs WHERE entityid BETWEEN @first AND @last) r))::text",
            "first",(long)FirstUid,"last",(long)(FirstUid+2)));
    }
    static void MasteryReload() {
        Require(System.IO.File.ReadAllText("mastery-rites.snapshot")==MasteryRiteSnapshot());
        for(uint index=0;index<3;index++) {
            nextUid=FirstUid+index;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);
            Require(MasteryFamilies.All(id=>p.Proficiencies.ContainsKey(id) && p.Proficiencies[id].Level==0 && p.Proficiencies[id].PreviousLevel==12 && p.Proficiencies[id].Experience==0));
            Require(p.Entity.Reborn==(index==0?1:2));
            p.Entity.MyAchievement.Send();p.Entity.MyAchievement.Send();MasteryEvidence(p,1);
            if(index==0)MasteryNpc(p,214);else if(index==1)MasteryNpc(p,180);else MasteryPaidRequest(p);
        }
        Require(System.IO.File.ReadAllText("mastery-rites.snapshot")==MasteryRiteSnapshot());
        System.Console.WriteLine("[mastery-process] mastery-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Mastery reload did not close.");
    }
}
