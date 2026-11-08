using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type TitleRules=typeof(Entity).Assembly.GetType("Revora.TitleRequirements");
    static HashSet<int> EligibleTitles(GameState p) { return (HashSet<int>)Call(TitleRules,null,"Eligible",p); }
    static Dictionary<int,bool> OwnedTitles(GameState p) { return (Dictionary<int,bool>)Call(typeof(WardrobeTitles),p.Entity.WTitles,"Records"); }
    static void RefreshTitles(GameState p) { Call(TitleRules,null,"Reconcile",p,false); }
    static void Collection(GameState p,params uint[] ids)
    {
        p.Entity.StorageItems=new Dictionary<uint,ConquerItem>();
        foreach(uint id in ids) { uint uid=ConquerItem.ItemUID.Next;p.Entity.StorageItems.Add(uid,new ConquerItem(true){UID=uid,ID=id,InWardrobe=true}); }
    }
    static TitleStorage TitleRequest(int key,TitleStorage.Action action)
    {
        return new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=action,Value=new TitleValue{Type=key/10000,Id=key%10000}}};
    }
    static void VerifyTitles(Map map)
    {
        ConquerItem.ItemUID=new Counter(3200000);
        Query("ALTER TABLE entities ADD COLUMN viplevel integer NOT NULL DEFAULT 0");
        Query("CREATE TABLE jianghu (LIKE public.jianghu INCLUDING ALL)");
        var p=RunePlayer(map);p.Entity.FullyLoaded=true;p.Entity.VIPLevel=7;
        Collection(p);
        Check(p.Account.State==AccountTable.AccountState.Player,"title fixtures use an ordinary non-GM player");
        new TitleStorage().Login(p);
        Check(OwnedTitles(p).Count==0 && p.Entity.EquippedWing==0,"VIP7 alone gives no titles or Emperor wings");
        Check(!StorageManager.Wing<bool>(999,1000) && !StorageManager.Title<bool>(999,1),"unknown types cannot fall back to another catalog type");
        foreach(uint score in new uint[]{399999,400000,599999,600000}) {
            var earned=new HashSet<int>();Call(TitleRules,null,"AddPrestige",earned,score);
            Check(earned.SetEquals(score<400000?new int[0]:score<600000?new[]{20272027}:new[]{20272027,20262026}),"prestige boundary awards correct 6711 titles at "+score);
        }
        var gearSlots=new byte[]{1,2,3,4,5,6,8,10,11,12,18,19};
        foreach(int total in new[]{215,216,323,324,647,648}) {
            Array.Clear(p.Equipment.Objects,0,p.Equipment.Objects.Length);
            int left=total;
            foreach(byte slot in gearSlots) {
                uint level=(uint)Math.Min(54,left);left-=(int)level;
                p.Equipment.Objects[slot-1]=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=130009,Position=slot,PerfectionLevel=level};
            }
            var earned=EligibleTitles(p);
            Check(earned.Contains(60086008)==(total>=216) && earned.Contains(60076007)==(total>=324) && earned.Contains(20392039)==(total>=648),"active equipment perfection threshold "+total);
        }
        Array.Clear(p.Equipment.Objects,0,p.Equipment.Objects.Length);
        p.Union=(Revora.Network.GamePackets.Union.UnionClass)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Network.GamePackets.Union.UnionClass));
        p.Union.ID=44;p.Union.LeaderUID=p.Entity.UID;Kernel.ServerKingdom=45;
        Check(!EligibleTitles(p).Contains(40011000),"ordinary union leader is not the Emperor");
        Kernel.ServerKingdom=44;
        Check(!EligibleTitles(p).Contains(40011000) && !EligibleTitles(p).Contains(11000),"a seeded kingdom flag cannot replace a finalized competitive result");
        p.Union.LeaderUID=p.Entity.UID+1;
        Check(!EligibleTitles(p).Contains(40011000),"a kingdom member cannot wear Emperor wings");p.Union=null;Kernel.ServerKingdom=0;
        p.Guild=new Revora.Game.ConquerStructures.Society.Guild(p.Entity.Name){ID=55};
        p.Guild.Leader=new Revora.Game.ConquerStructures.Society.Guild.Member(55){ID=p.Entity.UID};
        Call(TitleRules,null,"RecordCtfWinner",55u);
        Check(EligibleTitles(p).Contains(20020003) && !EligibleTitles(p).Contains(60020021),"local CTF winner earns Victor without cross-server prizes");
        StorageManager.Load();Check(EligibleTitles(p).Contains(20020003),"CTF champion guild persists across storage reload");
        p.Guild.Leader.ID++;
        Check(!EligibleTitles(p).Contains(20020003),"leader replacement removes prior leader eligibility");
        p.Guild.Leader.ID=p.Entity.UID;Call(TitleRules,null,"RecordCtfWinner",56u);
        Check(!EligibleTitles(p).Contains(20020003),"next champion guild revokes previous guild title");p.Guild=null;
        foreach(var pair in new[]{Tuple.Create(2026,2026,500),Tuple.Create(2013,14,50),Tuple.Create(6007,6007,0),Tuple.Create(9,1,500)})
            Check(StorageManager.GetTitlePoints((short)pair.Item1,(short)pair.Item2)==pair.Item3,"6711 title points: "+pair.Item1);
        for(int i=0;i<4;i++)p.ChiPowers.Add(new ChiPowerStructure{Points=400});
        p.ChiPowers[3].Points=399;
        Check(!EligibleTitles(p).Contains(20180001),"1599 Chi does not unlock Grandmaster");
        p.ChiPowers[3].Points=400;RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20180001) && p.Entity.WTitles.Points==500,"1600 Chi unlocks Grandmaster and 500 points");
        Check(Scalar("SELECT points FROM titles WHERE id=@uid",p.Entity.UID)==500,"earned title points persist in the database");
        TitleRequest(20180001,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20180001,"an ordinary eligible owner can equip Grandmaster");
        TitleRequest(40011000,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedWing==0,"forged Emperor wing equip is rejected");
        p.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(p);
        Check(OwnedTitles(p).Count==1 && p.Entity.WTitles.Points==500 && p.Entity.EquippedTitle==20180001,"database reload restores one earned title and equipped selection");
        Parallel.For(0,12,i=>new TitleStorage().Login(p));
        Check(OwnedTitles(p).Count==1 && p.Entity.WTitles.Points==500,"concurrent login reconciliation cannot duplicate titles or points");
        p.ChiPowers[3].Points=399;RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20180001) && p.Entity.EquippedTitle==0 && p.Entity.WTitles.Points==0,"losing Chi eligibility clears ownership, selection and points");
        p.Entity.MyJiang=new Revora.Game.JiangHu(p.Entity.UID){Inner_Strength=80999};
        Check(!EligibleTitles(p).Contains(20040005),"80999 Jiang Hu does not unlock Talent");
        p.Entity.MyJiang.Inner_Strength=81000;
        Check(EligibleTitles(p).Contains(20040005),"81000 Jiang Hu is representable and unlocks Talent");
        var rank=new JiangHuRank(1); // Compile against the actual uint score writer below via reflection-free packet API.
        rank.Appren(1,81000,140,"Tester","Jiang");
        Check(System.BitConverter.ToUInt32(rank.ToArray(),9)==81000,"Jiang Hu rank packet retains the complete 81000 score");
        Revora.Database.JiangHu.LoadStatus();
        var perfectStar=Revora.Database.JiangHu.Attributes.First(a=>a.Value.Level==6);
        foreach(var stage in p.Entity.MyJiang.Stagers) {
            stage.Activate=true;
            foreach(var star in stage.Stars) {star.Activate=true;star.UID=perfectStar.Key;star.Level=6;star.Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)perfectStar.Value.Type;}
        }
        p.Entity.MyJiang.CreateStatusAtributes(p.Entity);
        Check(p.Entity.MyJiang.Inner_Strength==81000 && EligibleTitles(p).Contains(20040005),"81 real maximum stars with full alignment calculate 81000 without overflow");
        p.Entity.MyJiang=null;p.ChiPowers.Clear();
        p.Entity.MyAchievement=new Achievement(p.Entity);
        var knownIds=(int[])Call(CatalogType,null,"Ids");
        using(var connection=DataHolder.MySqlConnection) {
            connection.Open();using(var transaction=connection.BeginTransaction()) {
                foreach(int id in knownIds.Take(319))Call(EvidenceType,null,"Complete",connection,transaction,p.Entity.UID,id,"fixture","title-boundary",1);
                transaction.Commit();
            }
        }
        EvidenceRefresh(p);
        Check(p.Entity.MyAchievement.CompletedCount==319 && !EligibleTitles(p).Contains(20010002),"319 verified achievements do not unlock Rising Star");
        RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20010002),"319 boundary does not create Rising Star ownership");
        CompleteEvidence(p.Entity.UID,knownIds[319],"title-boundary");EvidenceRefresh(p);
        Check(p.Entity.MyAchievement.CompletedCount==320 && EligibleTitles(p).Contains(20010002),"320 distinct verified achievements unlock Rising Star");
        RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20010002) && p.Entity.WTitles.Points==300,"Rising Star reconciliation grants ownership and300 title points");
        TitleRequest(20010002,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20010002,"ordinary eligible character equips Rising Star");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.WTitles=null;
        StorageManager.Load();new TitleStorage().Login(p);
        Check(p.Entity.MyAchievement.CompletedCount==320 && OwnedTitles(p).ContainsKey(20010002)
            && p.Entity.WTitles.Points==300 && p.Entity.EquippedTitle==20010002,"saved achievement and title reload retain Rising Star and selection");
        Parallel.For(0,12,i=>RefreshTitles(p));
        Check(p.Entity.WTitles.Points==300 && Scalar("SELECT points FROM titles WHERE id=@uid",p.Entity.UID)==300,"concurrent Rising Star reconciliation cannot multiply points");
        Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid","uid",(long)p.Entity.UID);
        p.Entity.MyAchievement=new Achievement(p.Entity);RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20010002) && p.Entity.EquippedTitle==0,"isolated fixture eligibility removal clears Rising Star selection");
        p.Entity.MyAchievement=null;

        var garments=StorageItem.Garments.Values.Where(i=>i.Stars>=4).Take(5).Select(i=>i.ID).ToArray();
        var mounts=StorageItem.Mounts.Values.Where(i=>i.Stars>=4).Take(5).Select(i=>i.ID).ToArray();
        Collection(p,Enumerable.Repeat(garments[0],5).ToArray());
        Check(!EligibleTitles(p).Contains(20050006),"five copies of one garment are not five designs");
        Collection(p,garments.Take(4).ToArray());Check(!EligibleTitles(p).Contains(20050006),"four qualifying garments remain below the threshold");
        Collection(p,garments);Check(EligibleTitles(p).Contains(20050006) && StorageItem.AmountStarGarments(p,4)==5,"five four-star garment designs unlock Fashionist");
        Collection(p,mounts);Check(EligibleTitles(p).Contains(20060007) && StorageItem.AmountStarMounts(p,4)==5,"five four-star mount designs unlock Swift Chaser");
        uint garment5=StorageItem.Garments.Values.First(i=>i.Stars>=5).ID,mount5=StorageItem.Mounts.Values.First(i=>i.Stars>=5).ID;
        Collection(p,garment5,mount5);RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(60030022) && OwnedTitles(p).ContainsKey(60040023),"five-star collections unlock Starlight and Moonlight wings");
        TitleRequest(60030022,TitleStorage.Action.Equip).Handle(p);TitleRequest(60040023,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedWing==60040023 && OwnedTitles(p).Count(r=>r.Value)==1,"equipping a second wing unequips the first durably");
        foreach(var item in p.Entity.StorageItems.Values){item.Days=1;item.DayStamp=DateTime.Now.AddDays(-2);}
        RefreshTitles(p);Check(p.Entity.EquippedWing==0 && !OwnedTitles(p).ContainsKey(60040023),"expired collections revoke wings and equipped visuals");
        Collection(p,Enumerable.Range(200553,8).Select(i=>(uint)i).ToArray());
        Check(new[]{20130014,20140015,20150016,20160017}.All(EligibleTitles(p).Contains),"eight permanent monkey designs unlock all four rider titles");
        p.Entity.StorageItems.Values.First(i=>i.ID==200559).Days=1;
        Check(!EligibleTitles(p).Contains(20140015) && !EligibleTitles(p).Contains(20160017),"temporary Solar Monkey cannot satisfy permanent collections");
        Collection(p,200595,200596,194695,194705,194675,194685);
        Check(new[]{20502050,20512051,20562056}.All(EligibleTitles(p).Contains),"exact fox and basketball collections unlock their titles");
        Collection(p,193735,193745,193755,193765,193815,193825,200563);
        Check(new[]{20322032,20332033}.All(EligibleTitles(p).Contains),"six Euro designs and Euro mount unlock both collection titles");
        Collection(p,194875,200613,200614);p.Entity.Body=2001;
        Check(EligibleTitles(p).Contains(20642064) && !EligibleTitles(p).Contains(20652065),"romance collection selects the female title");
        p.Entity.Body=1003;Check(EligibleTitles(p).Contains(20652065) && !EligibleTitles(p).Contains(20642064),"romance collection selects the male title");
        Collection(p);
        p.Entity.FullyLoaded=false;p.Entity.Reborn=2;p.Entity.Class=15;p.Entity.FullyLoaded=true;
        RuneItem(p,4010001,101);RuneItem(p,4020127,102);RuneItem(p,4030109,103);RuneItem(p,4030209,104);RuneItem(p,4030308,105);
        Check(!EligibleTitles(p).Contains(90001),"four max-level runes are insufficient");
        p.RunesItem.Values.First(r=>r.Position==105).ID=4030309;
        Check(EligibleTitles(p).Contains(90001),"five equipped max-level runes unlock Chosen One");
        p.RunesItem.Values.First(r=>r.Position==105).Position=125;
        Check(!EligibleTitles(p).Contains(90001),"splitting rune sets cannot bypass the five equipped rune requirement");
        p.RunesItem.Clear();
        p.Entity.Titles[TitlePacket.Titles.ElitePKChamption_Low]=DateTime.Now.AddDays(7);
        Check(!EligibleTitles(p).Contains(60010020),"lower Elite PK brackets do not receive level-130 champion wings");
        p.Entity.Titles[TitlePacket.Titles.ElitePKChamption_High]=DateTime.Now.AddDays(7);
        Check(!EligibleTitles(p).Contains(60010020),"unverified legacy champion flag cannot unlock Infernal wings");
        p.Entity.Titles[TitlePacket.Titles.ElitePKChamption_High]=DateTime.Now.AddSeconds(-1);
        Check(!EligibleTitles(p).Contains(60010020),"expired champion status cannot retain Infernal wings");
        p.Entity.Titles.Clear();

        byte[] legacy;
        using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory)) {
            writer.Write((byte)4);for(int i=0;i<260;i++){writer.Write((short)6007);writer.Write((short)6007);writer.Write(true);}legacy=memory.ToArray();
        }
        var old=RunePlayer(map);old.Entity.FullyLoaded=true;Collection(old);
        Query("INSERT INTO titles(id,points,data) VALUES(@id,999999,@data)","id",(long)old.Entity.UID,"data",legacy);
        StorageManager.Load();new TitleStorage().Login(old);
        Check(OwnedTitles(old).Count==0 && old.Entity.WTitles.Points==0,"wrapped legacy counts and unearned VIP grants are removed");
        Check(Scalar("SELECT octet_length(data) FROM revora_title_backup_v1 WHERE id=@uid",old.Entity.UID)==legacy.Length,"original legacy blob survives correction in the rollback table");
        Check(Scalar("SELECT points FROM revora_title_backup_v1 WHERE id=@uid",old.Entity.UID)==999999,"original point total is backed up once");
        old.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(old);
        Check(OwnedTitles(old).Count==0 && Scalar("SELECT points FROM revora_title_backup_v1 WHERE id=@uid",old.Entity.UID)==999999,"relogin preserves correction and immutable original backup");
        var packet=TitleRequest(20180001,TitleStorage.Action.Equip);byte[] frame=packet.FinalizeProtoBuf(packet.Info);
        Check(new TitleStorage().Read(frame.Take(frame.Length-8).ToArray()),"3301 reader accepts framed protobuf payload from the retained writer");
        for(int length=0;length<frame.Length-8;length++)Check(!new TitleStorage().Read(frame.Take(length).ToArray()),"truncated title request rejected: "+length);
        frame[4]=255;Check(!new TitleStorage().Read(frame.Take(frame.Length-8).ToArray()),"malformed protobuf rejected without mutation");
        Check(!new TitleStorage().Read(new byte[257]),"oversized title request rejected");
        VerifyHonoredHero(map);
        byte[] saved=(byte[])old.Entity.WTitles.Data.Clone();
        Query("ALTER TABLE titles ADD CONSTRAINT reject_fixture_title_points CHECK(points=0)");
        for(int i=0;i<4;i++)old.ChiPowers.Add(new ChiPowerStructure{Points=400});
        bool failed=false;try{RefreshTitles(old);}catch(Npgsql.PostgresException){failed=true;}
        Check(failed && old.Entity.WTitles.Data.SequenceEqual(saved) && old.Entity.WTitles.Points==0,"failed database update does not grant an in-memory title");
        Check(Scalar("SELECT points FROM titles WHERE id=@uid",old.Entity.UID)==0,"failed award leaves durable points unchanged");
        Query("ALTER TABLE titles DROP CONSTRAINT reject_fixture_title_points");
        VerifyHomecomingTitles(map);
        VerifyEarthKnightTitles(map);
        VerifyHomecomingReturnFlow(map);
        VerifyFashionSnapshot(map);
        VerifyFashionCalendar();
        VerifyMrMs2016(map);
        VerifyMrMs2017Donations(map);
        VerifyHeartStore(map);
        VerifyOriginCtf(map);
        VerifyOriginElitePk(map);
        VerifyOriginTeamPk(map);
        VerifyOriginBind(map);
        VerifyPlanetWeekend();
    }
    static void VerifyHonoredHero(Map map)
    {
        var p=ProgressPlayer(map,"");uint uid=p.Entity.UID;
        RefreshTitles(p);int points=p.Entity.WTitles.Points;
        Check(StorageManager.Title<int>(2057,2057)==20572057 && StorageManager.GetTitlePoints(2057,2057)==30,
            "Honored Hero catalog identity and points match retained 6711 mapping");
        Check(!OwnedTitles(p).ContainsKey(20572057),"ordinary player without final receipts does not earn Honored Hero");
        string[] receipts={
            "INSERT INTO revora_soul_trial_claims VALUES(@uid,0,@uid,ARRAY[@uid]::bigint[],0,0,120,now())",
            "INSERT INTO revora_courier_claims VALUES(@uid,0,@uid,0,0,0,0,0,0,120,now())",
            "INSERT INTO revora_path_hunt_claims VALUES(@uid,0,false,NULL,ARRAY[]::bigint[],0,0,120,now())",
            "INSERT INTO revora_first_rebirths(uid,old_class,old_level,old_experience,new_class,body,stone_uid,bound,reward_item,blessed_item) VALUES(@uid,15,130,0,11,1003,@uid,false,0,0)",
            "INSERT INTO revora_second_rebirths(uid,first_class,first_level,old_class,old_level,old_experience,new_class,token_uid,bound,reward_item) VALUES(@uid,15,130,15,130,0,11,@uid,false,0)"
        };
        for(int i=0;i<4;i++) {
            Query(receipts[i],"uid",(long)uid);EvidenceRefresh(p);RefreshTitles(p);
            Check(!OwnedTitles(p).ContainsKey(20572057),"incomplete career cannot earn Honored Hero after route "+(i+1));
        }
        Query("INSERT INTO revora_soul_trial_claims SELECT uid,1,soul_uid+10000000,ARRAY[soul_uid+10000000],rvp,silver,target_level,claimed_at FROM revora_soul_trial_claims WHERE uid=@uid AND life=0","uid",(long)uid);
        EvidenceRefresh(p);RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20572057),"repeating a completed route does not replace second rebirth");
        Query(receipts[4],"uid",(long)uid);
        Query("UPDATE revora_second_rebirths SET created_at=now()+interval '1 day' WHERE uid=@uid","uid",(long)uid);
        EvidenceRefresh(p);RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20572057),"future completion receipt does not qualify early");
        Query("UPDATE revora_second_rebirths SET created_at=now()-interval '1 minute' WHERE uid=@uid","uid",(long)uid);
        Query("CREATE FUNCTION reject_hero() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_hero BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='honored-hero-revora-v1') EXECUTE FUNCTION reject_hero()");
        bool failed=false;
        try{EvidenceRefresh(p);}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER reject_hero ON revora_event_awards; DROP FUNCTION reject_hero()");}
        Check(failed && !OwnedTitles(p).ContainsKey(20572057) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid",uid)==0,
            "suppressed career award leaves no durable or in-memory title");
        Parallel.For(0,6,i=>Call(EvidenceType,null,"Read",uid,"",false,new int[0],DateTime.UtcNow));
        EvidenceRefresh(p);RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20572057) && p.Entity.WTitles.Points==points+30,
            "five durable final routes award permanent Honored Hero to an ordinary player");
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='honored-hero-revora-v1' AND source_key='career-v1' AND title_key=20572057 AND expires_at IS NULL",uid)==1,
            "concurrent recovery stores exactly one permanent career award");
        new TitleStorage().Login(p);TitleRequest(20572057,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20572057,"earned Honored Hero can be equipped through ordinary wardrobe request");
        p.Entity.WTitles=null;p.Entity.MyAchievement=new Achievement(p.Entity);
        StorageManager.Load();new TitleStorage().Login(p);
        Check(OwnedTitles(p).ContainsKey(20572057) && p.Entity.EquippedTitle==20572057 && p.Entity.WTitles.Points==points+30,
            "career ownership points and equipped selection survive database reload");
        Query("UPDATE revora_event_awards SET revoked=true WHERE uid=@uid AND source='honored-hero-revora-v1'","uid",(long)uid);
        EvidenceRefresh(p);RefreshTitles(p);
        Check(!OwnedTitles(p).ContainsKey(20572057) && p.Entity.EquippedTitle==0,
            "career recovery does not override explicit award revocation");
    }

    static void VerifyHomecomingTitles(Map map)
    {
        var p=ProgressPlayer(map,"");uint uid=p.Entity.UID;
        string name="return-fixture-"+uid;
        DateTime returned=DateTime.UtcNow.AddDays(-29);
        returned=new DateTime(returned.Ticks-returned.Ticks%10,DateTimeKind.Utc);
        string profile=@"{""version"":1,""level"":120,""reborn"":0,""vip_level"":7,""vip_days"":0,""last_reset"":0,""timezone"":""UTC""}";
        Query("INSERT INTO revora_account_activity VALUES(@name,@first,@returned,2)","name",name,"first",new DateTimeOffset(returned.AddDays(-40)),"returned",new DateTimeOffset(returned));
        Query(@"INSERT INTO revora_account_returns(account_name,auth_number,character_uid,inactive_since,returned_at,character_snapshot,expires_at,title_key)
VALUES(@name,2,@uid,@first,@returned,CAST(@profile AS jsonb),@expiry,20312031)","name",name,"uid",(long)uid,"first",new DateTimeOffset(returned.AddDays(-31)),"returned",new DateTimeOffset(returned),"profile",profile,"expiry",new DateTimeOffset(returned.AddDays(30)));
        RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20312031),"ordinary returnee receives saved Paladin tier despite current profile differing");
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1' AND expires_at-earned_at=interval '720 hours'",uid)==1,
            "late return grant preserves original thirty-day term");
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND award_recorded",uid)==1,
            "wardrobe grant atomically acknowledges return receipt");
        Parallel.For(0,6,i=>Call(EvidenceType,null,"Read",uid,"",false,new int[0],DateTime.UtcNow));
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1'",uid)==1,
            "replayed return refresh does not duplicate title grants");
        new TitleStorage().Login(p);TitleRequest(20312031,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20312031,"ordinary returnee can equip Paladin");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(p);
        Check(OwnedTitles(p).ContainsKey(20312031) && p.Entity.EquippedTitle==20312031,"return title and equipped selection survive database reload");
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>returned.AddDays(30)));
        try {new TitleStorage().Login(p);Check(!OwnedTitles(p).ContainsKey(20312031) && p.Entity.EquippedTitle==0,"return title expires at original boundary without claim extension");}
        finally{TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        var failure=ProgressPlayer(map,"");uint other=failure.Entity.UID;
        Query(@"INSERT INTO revora_account_returns(account_name,auth_number,character_uid,inactive_since,returned_at,character_snapshot,expires_at,title_key)
VALUES(@name,3,@uid,@first,@returned,CAST(@profile AS jsonb),@expiry,20312031)","name",name,"uid",(long)other,"first",new DateTimeOffset(returned.AddDays(-31)),"returned",new DateTimeOffset(returned),"profile",profile,"expiry",new DateTimeOffset(returned.AddDays(30)));
        Query("CREATE FUNCTION reject_return_grant() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_return_grant BEFORE UPDATE ON revora_account_returns FOR EACH ROW EXECUTE FUNCTION reject_return_grant()");
        bool failed=false;
        try{EvidenceRefresh(failure);}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER reject_return_grant ON revora_account_returns; DROP FUNCTION reject_return_grant()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1'",other)==0 &&
            Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND NOT award_recorded",other)==1,
            "failed receipt acknowledgement rolls back return award and preserves retry");
        Query("CREATE FUNCTION suppress_return_grant() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_return_grant BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='homecoming-revora-v1') EXECUTE FUNCTION suppress_return_grant()");
        failed=false;
        try{EvidenceRefresh(failure);}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER suppress_return_grant ON revora_event_awards; DROP FUNCTION suppress_return_grant()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND NOT award_recorded",other)==1,
            "suppressed return grant cannot consume qualifying receipt");
        Parallel.For(0,6,i=>Call(EvidenceType,null,"Read",other,"",false,new int[0],DateTime.UtcNow));
        EvidenceRefresh(failure);RefreshTitles(failure);
        Check(OwnedTitles(failure).ContainsKey(20312031) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1'",other)==1,
            "concurrent retry materializes one saved return award");
        var unknown=ProgressPlayer(map,"");uint unknownUid=unknown.Entity.UID;
        Query(@"INSERT INTO revora_account_returns(account_name,auth_number,character_uid,inactive_since,returned_at,expires_at,title_key)
VALUES(@name,4,@uid,@first,@returned,@expiry,20312031)","name",name,"uid",(long)unknownUid,"first",new DateTimeOffset(returned.AddDays(-31)),"returned",new DateTimeOffset(returned),"expiry",new DateTimeOffset(returned.AddDays(30)));
        EvidenceRefresh(unknown);RefreshTitles(unknown);
        Check(!OwnedTitles(unknown).ContainsKey(20312031),"legacy return without contemporaneous snapshot cannot claim title");
        Query("UPDATE revora_account_returns SET character_snapshot=CAST(@profile AS jsonb),inactive_since=now()-interval '62 days',returned_at=now()-interval '31 days',expires_at=now()-interval '1 day' WHERE character_uid=@uid","uid",(long)unknownUid,"profile",profile);
        EvidenceRefresh(unknown);RefreshTitles(unknown);
        Check(!OwnedTitles(unknown).ContainsKey(20312031) && Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND NOT award_recorded",unknownUid)==1,
            "expired unclaimed return cannot restart a title term");

    }

    static void VerifyEarthKnightTitles(Map map)
    {
        var p=ProgressPlayer(map,"");uint uid=p.Entity.UID;
        string name="earth-return-"+uid;
        DateTime returned=DateTime.UtcNow.AddDays(-29);
        returned=new DateTime(returned.Ticks-returned.Ticks%10,DateTimeKind.Utc);
        string profile=@"{""version"":1,""level"":120,""reborn"":0,""vip_level"":0,""vip_days"":0,""last_reset"":0,""timezone"":""UTC""}";
        Query("INSERT INTO revora_account_activity VALUES(@name,@first,@returned,2)","name",name,"first",new DateTimeOffset(returned.AddDays(-40)),"returned",new DateTimeOffset(returned));
        Query(@"INSERT INTO revora_account_returns(account_name,auth_number,character_uid,inactive_since,returned_at,character_snapshot,expires_at,title_key)
VALUES(@name,2,@uid,@first,@returned,CAST(@profile AS jsonb),@expiry,20282028)","name",name,"uid",(long)uid,"first",new DateTimeOffset(returned.AddDays(-31)),"returned",new DateTimeOffset(returned),"profile",profile,"expiry",new DateTimeOffset(returned.AddDays(30)));
        RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20282028) && !OwnedTitles(p).ContainsKey(20302030),
            "frozen raw VIP 0 grants Earth Knight without the VIP-4 login floor");
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1' AND title_key=20282028 AND expires_at-earned_at=interval '720 hours'",uid)==1,
            "Earth Knight grant preserves original thirty-day term");
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND award_recorded AND title_key=20282028",uid)==1,
            "Earth Knight wardrobe grant atomically acknowledges the return receipt");
        Parallel.For(0,6,i=>Call(EvidenceType,null,"Read",uid,"",false,new int[0],DateTime.UtcNow));
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1'",uid)==1,
            "replayed Earth Knight refresh does not duplicate the title grant");
        new TitleStorage().Login(p);TitleRequest(20282028,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20282028,"ordinary returnee can equip Earth Knight");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(p);
        Check(OwnedTitles(p).ContainsKey(20282028) && p.Entity.EquippedTitle==20282028,
            "Earth Knight ownership and equipped selection survive database reload");
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>returned.AddDays(30)));
        try {new TitleStorage().Login(p);Check(!OwnedTitles(p).ContainsKey(20282028) && p.Entity.EquippedTitle==0,
            "Earth Knight expires at the original boundary without claim extension");}
        finally{TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }

    static void VerifyHomecomingReturnFlow(Map map)
    {
        // Exercise the post-proof authentication hook through actual wardrobe persistence.
        // Time passage is simulated in the activity tables; no return/award is seeded.
        Query(@"CREATE TABLE accounts(username text PRIMARY KEY,password text NOT NULL,ip text DEFAULT '',
entityid bigint DEFAULT 0,lastcheck bigint DEFAULT 0,state smallint DEFAULT 2,email text DEFAULT '',idreferido bigint DEFAULT 0);
ALTER TABLE entities ADD COLUMN IF NOT EXISTS level integer;
ALTER TABLE entities ADD COLUMN IF NOT EXISTS reborn integer;
ALTER TABLE entities ADD COLUMN IF NOT EXISTS viplevel integer;
ALTER TABLE entities ADD COLUMN IF NOT EXISTS vipdays text;
ALTER TABLE entities ADD COLUMN IF NOT EXISTS vipleveldays integer;
ALTER TABLE entities ADD COLUMN IF NOT EXISTS lastresettime bigint;");
        var p=ProgressPlayer(map,"");uint uid=p.Entity.UID;string name="flow-return-"+uid;
        Query("INSERT INTO accounts(username,password,entityid) VALUES(@name,'!synthetic-unusable!',@uid)","name",name,"uid",(long)uid);
        Query("UPDATE entities SET level=120,reborn=0,viplevel=7,vipdays='0',vipleveldays=0,lastresettime=0 WHERE uid=@uid","uid",(long)uid);
        p.Account=new AccountTable(name);
        Check(p.Account.exists && p.Account.State==AccountTable.AccountState.Player && p.Account.EntityID==uid,
            "Homecoming integration uses an ordinary stored account and matching character");
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
        Call(activity,null,"Authenticated",p.Account);
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",uid)==0,
            "newly observed account cannot fabricate thirty days of absence");
        Query("UPDATE revora_account_activity SET first_auth=now()-interval '40 days',last_auth=now()-interval '31 days' WHERE account_name=@name","name",name);
        Guid session=Guid.NewGuid();Call(activity,null,"SessionOpened",p.Account,session);
        Call(activity,null,"Authenticated",p.Account);
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",uid)==0,
            "real presence producer blocks a return even with old authentication history");
        Call(activity,null,"SessionClosed",session);
        Query("UPDATE revora_account_activity SET last_auth=now()-interval '31 days' WHERE account_name=@name","name",name);
        Call(activity,null,"Authenticated",p.Account);
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",uid)==0,
            "real session closure restarts the inactivity interval");
        Query("UPDATE revora_account_sessions SET opened_at=now()-interval '40 days',closed_at=now()-interval '31 days' WHERE character_uid=@uid","uid",(long)uid);
        Query("UPDATE revora_account_activity SET last_auth=now()-interval '31 days' WHERE account_name=@name","name",name);
        Parallel.For(0,4,i=>Call(activity,null,"Authenticated",new AccountTable(name.ToUpperInvariant())));
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND title_key=20312031 AND NOT award_recorded",uid)==1,
            "concurrent qualifying authentication creates one real Paladin return snapshot");
        Query("UPDATE entities SET level=119,reborn=0,viplevel=4 WHERE uid=@uid","uid",(long)uid);
        p.Entity.Level=119;p.Entity.Reborn=0;p.Entity.VIPLevel=4;
        EvidenceRefresh(p);RefreshTitles(p);
        Check(OwnedTitles(p).ContainsKey(20312031) && !OwnedTitles(p).ContainsKey(20302030),
            "production return snapshot grants its saved tier despite later VIP and level changes");
        Check(Scalar(@"SELECT count(*) FROM revora_event_awards a JOIN revora_account_returns r ON r.character_uid=a.uid
WHERE a.uid=@uid AND a.source='homecoming-revora-v1' AND a.earned_at=r.returned_at AND a.expires_at=r.expires_at AND r.award_recorded",uid)==1,
            "wardrobe producer preserves authentication timestamp expiry and atomic receipt acknowledgement");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(p);
        TitleRequest(20312031,TitleStorage.Action.Equip).Handle(p);
        Check(OwnedTitles(p).ContainsKey(20312031) && p.Entity.EquippedTitle==20312031,
            "integrated return survives database reload and ordinary equip");
        DateTime expiry;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT expires_at FROM revora_account_returns WHERE character_uid=@uid",c)) {
            q.Parameters.AddWithValue("uid",(long)uid);using(var r=q.ExecuteReader()){r.Read();expiry=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
        }}
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>expiry.AddTicks(-10)));
        try {
            new TitleStorage().Login(p);Check(OwnedTitles(p).ContainsKey(20312031),"integrated return remains active immediately before its UTC expiry");
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>expiry));
            new TitleStorage().Login(p);Check(!OwnedTitles(p).ContainsKey(20312031) && p.Entity.EquippedTitle==0,
                "integrated return expires and unequips exactly at its saved UTC deadline");
        } finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        var recovered=ProgressPlayer(map,"");uint recoveredUid=recovered.Entity.UID;string recoveredName="flow-recover-"+recoveredUid;
        Query("INSERT INTO accounts(username,password,entityid) VALUES(@name,'!synthetic-unusable!',@uid)","name",recoveredName,"uid",(long)recoveredUid);
        Query("UPDATE entities SET level=120,reborn=0,viplevel=7,vipdays='0',vipleveldays=0,lastresettime=0 WHERE uid=@uid","uid",(long)recoveredUid);
        recovered.Account=new AccountTable(recoveredName);
        Call(activity,null,"Authenticated",recovered.Account);
        Query("UPDATE revora_account_activity SET first_auth=now()-interval '40 days',last_auth=now()-interval '31 days' WHERE account_name=@name","name",recoveredName);
        Guid leftover=Guid.NewGuid();Call(activity,null,"SessionOpened",recovered.Account,leftover);
        Query("UPDATE revora_account_sessions SET owner='44444444-4444-4444-4444-444444444444',opened_at=now()-interval '40 days' WHERE session_id=@session","session",leftover);
        Call(activity,null,"Authenticated",recovered.Account);
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",recoveredUid)==0,
            "previous-process presence still blocks Homecoming until startup recovery");
        Call(activity,null,"Initialize");
        Check(Scalar("SELECT count(*) FROM revora_account_sessions WHERE character_uid=@uid AND closed_at IS NOT NULL",recoveredUid)==1,
            "startup recovery closes the previous-process Homecoming session");
        Query("UPDATE revora_account_sessions SET opened_at=now()-interval '40 days',closed_at=now()-interval '31 days' WHERE session_id=@session","session",leftover);
        Query("UPDATE revora_account_activity SET first_auth=now()-interval '40 days',last_auth=now()-interval '31 days' WHERE account_name=lower(@name)","name",recoveredName);
        Check(Scalar("SELECT count(*) FROM revora_account_sessions WHERE character_uid=@uid AND closed_at<=now()-interval '30 days' AND closed_at IS NOT NULL",recoveredUid)==1,
            "aged recovered closure is old enough to resume inactivity");
        Call(activity,null,"Authenticated",new AccountTable(recoveredName));
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND title_key=20312031 AND character_snapshot IS NOT NULL AND NOT award_recorded AND returned_at<=now() AND expires_at>now()",recoveredUid)==1,
            "recovered previous-process session creates a Paladin return after thirty days");
        DateTime granted;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT returned_at FROM revora_account_returns WHERE character_uid=@uid",c)) {
            q.Parameters.AddWithValue("uid",(long)recoveredUid);using(var r=q.ExecuteReader()){r.Read();granted=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
        }}
        Call(EvidenceType,null,"Read",recoveredUid,"",false,new int[0],granted.AddSeconds(1));
        EvidenceRefresh(recovered);
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>granted.AddSeconds(1)));
        try {new TitleStorage().Login(recovered);RefreshTitles(recovered);}
        finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid AND award_recorded",recoveredUid)==1,
            "wardrobe producer acknowledges the recovered Paladin return");
        Check(OwnedTitles(recovered).ContainsKey(20312031),
            "recovered Paladin return is owned after ordinary wardrobe refresh");
        var earth=ProgressPlayer(map,"");uint earthUid=earth.Entity.UID;string earthName="flow-earth-"+earthUid;
        Query("INSERT INTO accounts(username,password,entityid) VALUES(@name,'!synthetic-unusable!',@uid)","name",earthName,"uid",(long)earthUid);
        Query("UPDATE entities SET level=120,reborn=0,viplevel=0,vipdays='0',vipleveldays=0,lastresettime=0 WHERE uid=@uid","uid",(long)earthUid);
        earth.Account=new AccountTable(earthName);
        Call(activity,null,"Authenticated",earth.Account);
        Query("UPDATE revora_account_activity SET first_auth=now()-interval '40 days',last_auth=now()-interval '31 days' WHERE account_name=@name","name",earthName);
        Query("UPDATE entities SET viplevel=0 WHERE uid=@uid","uid",(long)earthUid);
        Call(activity,null,"Authenticated",earth.Account);
        Check(Convert.ToInt64(Scalar("SELECT title_key FROM revora_account_returns WHERE character_uid=@uid AND character_snapshot->>'vip_level'='0'",earthUid))==20282028,
            "qualifying authentication of frozen raw VIP 0 stores Earth Knight");
        DateTime earthGranted;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT returned_at FROM revora_account_returns WHERE character_uid=@uid AND title_key=20282028",c)) {
            q.Parameters.AddWithValue("uid",(long)earthUid);using(var r=q.ExecuteReader()){r.Read();earthGranted=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
        }}
        Call(EvidenceType,null,"Read",earthUid,"",false,new int[0],earthGranted.AddSeconds(1));
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>earthGranted.AddSeconds(1)));
        try {new TitleStorage().Login(earth);RefreshTitles(earth);}
        finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        Check(OwnedTitles(earth).ContainsKey(20282028) && !OwnedTitles(earth).ContainsKey(20302030),
            "ordinary VIP-0 returner receives Earth Knight through wardrobe refresh");
        Call(activity,null,"Relinquish");
    }

    static void VerifyFashionSnapshot(Map map)
    {
        var fashion=typeof(Entity).Assembly.GetType("Revora.Game.Features.FashionCompetition");
        Call(fashion,null,"Initialize");
        var p=ProgressPlayer(map,"");var tied=ProgressPlayer(map,"");var third=ProgressPlayer(map,"");var empty=ProgressPlayer(map,"");
        uint garment=StorageItem.Garments.Values.First(x=>x.Stars==5 && ConquerItemInformation.BaseInformations.ContainsKey(x.ID)).ID;
        uint mount=StorageItem.Mounts.Values.First(x=>x.Stars==5 && ConquerItemInformation.BaseInformations.ContainsKey(x.ID)).ID;
        Action<GameState,uint,int> owned=(player,id,days)=>Query("INSERT INTO items(uid,id,entityid,days) VALUES(@item,@id,@uid,@days)",
            "item",(long)ConquerItem.ItemUID.Next,"id",(long)id,"uid",(long)player.Entity.UID,"days",days);
        owned(p,garment,0);owned(p,garment,0);owned(p,mount,0);
        owned(tied,garment,0);owned(tied,mount,0);owned(third,garment,0);
        owned(empty,garment,7);owned(empty,720027,0);
        var close=new DateTime(2030,2,1,8,0,0,DateTimeKind.Utc); // Pacific midnight after January.
        fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>close));
        try {
            bool early=false;fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>close.AddTicks(-1)));
            try{Call(fashion,null,"Freeze",close);}catch(InvalidOperationException){early=true;}
            Check(early && Scalar("SELECT count(*) FROM revora_fashion_seasons",0)==0,"Fashion cannot snapshot a season before close");
            fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>close));
            Query("CREATE FUNCTION reject_fashion_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_fashion_result BEFORE INSERT ON revora_fashion_results FOR EACH ROW EXECUTE FUNCTION reject_fashion_result()");
            bool failed=false;
            try{Call(fashion,null,"Freeze",close);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER reject_fashion_result ON revora_fashion_results; DROP FUNCTION reject_fashion_result()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_fashion_seasons",0)==0 && Scalar("SELECT count(*) FROM revora_fashion_designs",0)==0,
                "failed Fashion ranking rolls back season and ownership snapshot");
            int freezes=0;Parallel.For(0,4,i=>{if((bool)Call(fashion,null,"Freeze",close))System.Threading.Interlocked.Increment(ref freezes);});
            Check(freezes==1,"concurrent season finalization freezes ownership exactly once");
            Check(Scalar("SELECT score FROM revora_fashion_results WHERE season='2030-01' AND uid=@uid",p.Entity.UID)==10 &&
                Scalar("SELECT count(*) FROM revora_fashion_designs WHERE season='2030-01' AND uid=@uid",p.Entity.UID)==2,
                "permanent garment and mount designs score once despite duplicate item instances");
            Check(Scalar("SELECT placement FROM revora_fashion_results WHERE season='2030-01' AND uid=@uid",p.Entity.UID)==1 &&
                Scalar("SELECT placement FROM revora_fashion_results WHERE season='2030-01' AND uid=@uid",tied.Entity.UID)==1 &&
                Scalar("SELECT placement FROM revora_fashion_results WHERE season='2030-01' AND uid=@uid",third.Entity.UID)==3,
                "Fashion equal scores share first and skip second placement");
            Check(Scalar("SELECT count(*) FROM revora_fashion_results WHERE uid=@uid",empty.Entity.UID)==0,
                "temporary and noncatalog items cannot create a Fashion entrant");
            Query("UPDATE items SET entityid=@next WHERE entityid=@uid","uid",(long)p.Entity.UID,"next",(long)empty.Entity.UID);
            fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>close.AddDays(2)));
            Call(fashion,null,"Initialize");
            Check(!(bool)Call(fashion,null,"Freeze",close) && Scalar("SELECT score FROM revora_fashion_results WHERE season='2030-01' AND uid=@uid",p.Entity.UID)==10 &&
                Scalar("SELECT count(*) FROM revora_fashion_results WHERE uid=@uid",empty.Entity.UID)==0,
                "season reload and later ownership transfers cannot rewrite frozen results");
            var missed=new DateTime(2030,3,1,8,0,0,DateTimeKind.Utc);
            fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>missed.AddMinutes(2)));
            bool late=false;try{Call(fashion,null,"Freeze",missed);}catch(InvalidOperationException){late=true;}
            Check(late && Scalar("SELECT count(*) FROM revora_fashion_seasons WHERE season='2030-02'",0)==0,
                "missed month close cannot fabricate historical cosmetic ownership");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE source LIKE 'fashion%'",0)==0,
                "snapshot capture alone does not publish Fashion awards");
            VerifyFashionAwards(map,p,tied,third,empty,close,fashion);
        } finally {fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }

    static void VerifyFashionAwards(Map map,GameState winner,GameState tied,GameState third,GameState transferred,DateTime close,Type fashion)
    {
        DateTime now=close.AddDays(2);
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Query("CREATE FUNCTION suppress_fashion_award() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_fashion_award BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='fashion-revora-v1') EXECUTE FUNCTION suppress_fashion_award()");
            bool failed=false;
            try{new TitleStorage().Login(winner);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER suppress_fashion_award ON revora_event_awards; DROP FUNCTION suppress_fashion_award()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='fashion-revora-v1'",winner.Entity.UID)==0 &&
                Scalar("SELECT count(*) FROM revora_fashion_results WHERE uid=@uid AND NOT award_recorded",winner.Entity.UID)==1,
                "suppressed Fashion award preserves the pending winner without granting ownership");
            Query("CREATE FUNCTION reject_fashion_ack() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_fashion_ack BEFORE UPDATE ON revora_fashion_results FOR EACH ROW EXECUTE FUNCTION reject_fashion_ack()");
            failed=false;
            try{new TitleStorage().Login(winner);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER reject_fashion_ack ON revora_fashion_results; DROP FUNCTION reject_fashion_ack()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='fashion-revora-v1'",winner.Entity.UID)==0,
                "failed Fashion acknowledgement rolls back its title grant");
            int notices=0;
            Parallel.For(0,6,i=>{
                var snapshot=Call(EvidenceType,null,"Read",winner.Entity.UID,"",false,new int[0],now);
                System.Threading.Interlocked.Add(ref notices,((IEnumerable<string>)snapshot.GetType().GetField("Notices",Instance).GetValue(snapshot)).Count());
            });
            Check(notices==1,"concurrent Fashion recovery yields one post-commit award notice");
            var replay=Call(EvidenceType,null,"Read",winner.Entity.UID,"",false,new int[0],now);
            Check(!((IEnumerable<string>)replay.GetType().GetField("Notices",Instance).GetValue(replay)).Any(),
                "Fashion award notice is not repeated on later login reads");
            new TitleStorage().Login(winner);new TitleStorage().Login(tied);new TitleStorage().Login(third);new TitleStorage().Login(transferred);
            Check(OwnedTitles(winner).ContainsKey(20522052) && OwnedTitles(tied).ContainsKey(20522052) &&
                winner.Entity.WTitles.Points==300 && tied.Entity.WTitles.Points==300,
                "tied first-place ordinary collectors both receive Fashion Icon and catalog points");
            Check(OwnedTitles(third).ContainsKey(20542054) && third.Entity.WTitles.Points==100 &&
                Scalar("SELECT count(*) FROM revora_event_awards WHERE source='fashion-revora-v1' AND title_key=20532053",0)==0,
                "skipped second placement produces no Idol while third earns Fashion Master");
            Check(!OwnedTitles(transferred).ContainsKey(20522052),"post-close cosmetic transfer cannot steal a frozen Fashion award");
            Check(Scalar(@"SELECT count(*) FROM revora_event_awards a JOIN revora_fashion_seasons s ON s.season=a.source_key
WHERE a.uid=@uid AND a.source='fashion-revora-v1' AND a.earned_at=s.closed_at AND a.expires_at=a.earned_at+interval '720 hours'",winner.Entity.UID)==1,
                "concurrent delayed Fashion claim keeps one award with original season expiry");
            TitleRequest(20522052,TitleStorage.Action.Equip).Handle(winner);
            winner.Entity.MyAchievement=new Achievement(winner.Entity);winner.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(winner);
            Check(OwnedTitles(winner).ContainsKey(20522052) && winner.Entity.EquippedTitle==20522052 && winner.Entity.WTitles.Points==300,
                "Fashion ownership points and ordinary equipped selection survive database reload");
            now=close.AddDays(30);new TitleStorage().Login(winner);
            Check(!OwnedTitles(winner).ContainsKey(20522052) && winner.Entity.EquippedTitle==0 && winner.Entity.WTitles.Points==0,
                "Fashion title expires and unequips at thirty days from close");
            uint extra=StorageItem.Garments.Values.First(x=>x.Stars==4 && ConquerItemInformation.BaseInformations.ContainsKey(x.ID)).ID;
            Query("INSERT INTO items(uid,id,entityid,days) VALUES(@item,@id,@uid,0)","item",(long)ConquerItem.ItemUID.Next,"id",(long)extra,"uid",(long)tied.Entity.UID);
            DateTime nextClose=new DateTime(2030,3,1,8,0,0,DateTimeKind.Utc);
            fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>nextClose));
            Check((bool)Call(fashion,null,"Freeze",nextClose),"next Fashion season captures changed collections independently");
            now=nextClose.AddDays(1);new TitleStorage().Login(transferred);
            Check(OwnedTitles(transferred).ContainsKey(20532053) && transferred.Entity.WTitles.Points==200 &&
                Scalar("SELECT placement FROM revora_fashion_results WHERE uid=@uid AND season='2030-02'",transferred.Entity.UID)==2,
                "ordinary second-place collector earns Fashion Idol in the next season");
            VerifyFashionGuide(map,transferred,fashion);
            now=nextClose.AddDays(30);new TitleStorage().Login(tied);
            Check(!OwnedTitles(tied).ContainsKey(20522052) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='fashion-revora-v1' AND source_key='2030-02'",tied.Entity.UID)==0,
                "expired unclaimed Fashion season cannot start a fresh title term");
        } finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }

    static void VerifyFashionCalendar()
    {
        var fashion=typeof(Entity).Assembly.GetType("Revora.Game.Features.FashionCompetition");
        DateTime now=new DateTime(2031,1,15,12,0,0,DateTimeKind.Utc);
        fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Query("DELETE FROM revora_fashion_calendar");Call(fashion,null,"Initialize");
            Check(Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-02-01T08:00:00Z'::timestamptz",0)==1,
                "first Fashion startup schedules the next Pacific month without inventing prior seasons");
            Check(!(bool)Call(fashion,null,"ProcessDue"),"Fashion calendar does not finalize before its saved close");
            now=new DateTime(2031,2,1,8,0,0,DateTimeKind.Utc);
            int advanced=0;Parallel.For(0,4,i=>{if((bool)Call(fashion,null,"ProcessDue"))System.Threading.Interlocked.Increment(ref advanced);});
            Check(advanced==1 && Scalar("SELECT count(*) FROM revora_fashion_seasons WHERE season='2031-01'",0)==1 &&
                Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-03-01T08:00:00Z'::timestamptz",0)==1,
                "concurrent coordinators finalize one due month and advance one durable cursor");
            now=new DateTime(2031,3,1,8,0,0,DateTimeKind.Utc);
            Query("CREATE FUNCTION reject_fashion_calendar() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_fashion_calendar BEFORE UPDATE ON revora_fashion_calendar FOR EACH ROW EXECUTE FUNCTION reject_fashion_calendar()");
            bool failed=false;
            try{Call(fashion,null,"ProcessDue");}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER reject_fashion_calendar ON revora_fashion_calendar; DROP FUNCTION reject_fashion_calendar()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_fashion_seasons WHERE season='2031-02'",0)==1 &&
                Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-03-01T08:00:00Z'::timestamptz",0)==1,
                "calendar write failure preserves committed Fashion snapshot and retry cursor");
            now=now.AddHours(2);
            VerifyFashionRestartProcess(now);
            Call(fashion,null,"Initialize");
            Check(!(bool)Call(fashion,null,"ProcessDue") && Scalar("SELECT count(*) FROM revora_fashion_missed WHERE season='2031-02'",0)==0,
                "parent observes fresh-process recovery without advancing or recreating the season again");
            Check(Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-04-01T07:00:00Z'::timestamptz",0)==1,
                "Fashion month cursor follows Pacific daylight saving change");
            now=new DateTime(2031,5,2,12,0,0,DateTimeKind.Utc);
            Call(fashion,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_fashion_missed WHERE season IN ('2031-03','2031-04')",0)==2 &&
                Scalar("SELECT count(*) FROM revora_fashion_seasons WHERE season IN ('2031-03','2031-04')",0)==0,
                "event tick records missed closes without fabricating ownership or rankings");
            Check(Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-06-01T07:00:00Z'::timestamptz",0)==1,
                "event tick catches up missed months to the next future Pacific close");
            Call(fashion,null,"Initialize");Call(fashion,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_fashion_missed WHERE season IN ('2031-03','2031-04')",0)==2,
                "repeated startup and tick preserve immutable missed-close records");
            long before=Scalar("SELECT count(*) FROM revora_fashion_missed",0);
            now=new DateTime(2033,1,2,12,0,0,DateTimeKind.Utc);Call(fashion,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_fashion_missed",0)==before+12 &&
                Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2032-06-01T07:00:00Z'::timestamptz",0)==1,
                "Fashion catch-up limits one event tick to twelve missed months");
            Call(fashion,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_fashion_missed",0)==before+12,"Fashion repeated ticks respect the ten-second polling limit");
            now=now.AddSeconds(11);Call(fashion,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_fashion_missed",0)==before+20 &&
                Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2033-02-01T08:00:00Z'::timestamptz",0)==1,
                "Fashion catch-up resumes remaining months on the next bounded poll");

        } finally {fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }

    static void VerifyFashionGuide(Map map,GameState player,Type fashion)
    {
        // Confirm the existing guide is present in the installed world catalog; no new actor is spawned.
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly FROM public.npcs WHERE id=6002",c))
        using(var r=q.ExecuteReader()) {
            Check(r.Read(),"existing garment merchant is present in installed world catalog");
            System.Console.WriteLine("Fashion guide catalog location: "+r.GetValue(0)+" "+r.GetValue(1)+","+r.GetValue(2));
        }}
        bool had=map.Npcs.ContainsKey(6002);var saved=had?map.Npcs[6002]:null;uint active=player.ActiveNpc;
        ushort x=player.Entity.X,y=player.Entity.Y;
        try {
            map.Npcs[6002]=new NpcSpawn{UID=6002,MapID=map.ID,X=x,Y=y};player.ActiveNpc=6002;
            string rules=(string)Call(fashion,null,"Page",player,(byte)238);
            string board=(string)Call(fashion,null,"Page",player,(byte)239);
            Check(rules.Contains("Each design counts once") && rules.Contains("30 days") && rules.Length<300,
                "ordinary nearby player can read brief Fashion rules");
            Check(board.Contains("2030-02") && board.Contains("Your place: #2 with 10 stars."),
                "Fashion standings show the frozen season and that player's own place");
            Check((bool)Call(fashion,null,"HandleNpc",player,new NpcRequest(true){NpcID=6002,OptionID=239}),
                "ordinary garment merchant handler serves Fashion standings");
            map.Npcs[6002].X=(ushort)(x+10);
            Check(Call(fashion,null,"Page",player,(byte)239)==null,"remote Fashion dialogue cannot read through the NPC service");
            map.Npcs[6002].X=x;player.ActiveNpc=6003;
            Check(Call(fashion,null,"Page",player,(byte)239)==null,"wrong active NPC cannot serve Fashion standings");
            player.ActiveNpc=6002;player.Fake=true;
            Check(Call(fashion,null,"Page",player,(byte)239)==null,"synthetic actors cannot use the ordinary Fashion guide");
            player.Fake=false;
            Check(Call(fashion,null,"Page",player,(byte)240)==null,"unsupported Fashion dialogue option is ignored");
        }finally{player.Fake=false;player.ActiveNpc=active;if(had)map.Npcs[6002]=saved;else map.Npcs.Remove(6002);}
    }

    static void VerifyFashionRestartProcess(DateTime now)
    {
        Call(typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity"),null,"Relinquish");
        uint winner=checked((uint)Scalar("SELECT uid FROM revora_fashion_results WHERE season='2031-02' AND placement=1 ORDER BY uid LIMIT 1",0));
        var start=new System.Diagnostics.ProcessStartInfo {
            FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--fashion-restart-check",
            WorkingDirectory=Environment.CurrentDirectory,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var child=System.Diagnostics.Process.Start(start)) {
            var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
            // Protected process pipe only: never persist or print the fixture connection string.
            child.StandardInput.WriteLine(DataHolder.ConnectionString);
            child.StandardInput.WriteLine(now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            child.StandardInput.WriteLine(winner.ToString(System.Globalization.CultureInfo.InvariantCulture));child.StandardInput.Close();
            if(!child.WaitForExit(20000)){child.Kill();child.WaitForExit();throw new Exception("Fashion restart child timed out.");}
            string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
            Check(child.ExitCode==0 && error.Length==0 && result.Contains("Fashion restart checks passed: 5"),
                "fresh process recovers committed Fashion results cursor and grants without parent caches");
        }
    }
    static int FashionRestartChild()
    {
        try {
            string connection=System.Console.ReadLine();if(string.IsNullOrEmpty(connection) || connection.Length>8192)return 2;
            var builder=new Npgsql.NpgsqlConnectionStringBuilder(connection);
            if(builder.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=builder.ConnectionString;
            DateTime now=new DateTime(long.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture),DateTimeKind.Utc);
            uint winner=uint.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            var fashion=typeof(Entity).Assembly.GetType("Revora.Game.Features.FashionCompetition");
            Check(Kernel.GamePool.Count==0 && !(bool)fashion.GetField("initialized",Hidden).GetValue(null) && StorageItem.Garments.Count==0 && StorageItem.Mounts.Count==0,
                "Fashion child starts with no players catalog or coordinator memory");
            fashion.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Call(EvidenceType,null,"Initialize");
            Check((bool)Call(fashion,null,"ProcessDue") &&
                Scalar("SELECT count(*) FROM revora_fashion_calendar WHERE next_close='2031-04-01T07:00:00Z'::timestamptz",0)==1 &&
                Scalar("SELECT count(*) FROM revora_fashion_missed WHERE season='2031-02'",0)==0,
                "new process recovers committed month and advances cursor outside capture window");
            var snapshot=Call(EvidenceType,null,"Read",winner,"",false,new int[0],now);
            Check(Scalar(@"SELECT count(*) FROM revora_event_awards a JOIN revora_fashion_seasons s ON s.season=a.source_key
WHERE a.uid=@uid AND a.source='fashion-revora-v1' AND a.source_key='2031-02' AND a.title_key=20522052
AND a.earned_at=s.closed_at AND a.expires_at=s.closed_at+interval '720 hours'",winner)==1 &&
                Scalar("SELECT count(*) FROM revora_fashion_results WHERE uid=@uid AND season='2031-02' AND award_recorded",winner)==1 &&
                ((IEnumerable<string>)snapshot.GetType().GetField("Notices",Instance).GetValue(snapshot)).Any(),
                "new process grants saved winner with original expiry and committed notice");
            snapshot=Call(EvidenceType,null,"Read",winner,"",false,new int[0],now);
            Check(!((IEnumerable<string>)snapshot.GetType().GetField("Notices",Instance).GetValue(snapshot)).Any() &&
                Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='fashion-revora-v1' AND source_key='2031-02'",winner)==1,
                "new-process repeated reads preserve one grant without duplicate notices");
            Call(fashion,null,"Initialize");
            Check(!(bool)Call(fashion,null,"ProcessDue") && StorageItem.Garments.Count==0 && StorageItem.Mounts.Count==0,
                "restart recovery never needs to recalculate saved scores from current catalog or ownership");
            System.Console.WriteLine("Fashion restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine("Fashion restart failed: "+ex.GetType().Name);return 1;}
    }

    static void VerifyPlanetWeekend()
    {
        var planet=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetWeekend");
        Func<DateTime,object> current=utc=>Call(planet,null,"Current",utc);
        Func<object,string,object> field=(window,name)=>window.GetType().GetField(name,Instance).GetValue(window);
        DateTime start=new DateTime(2030,3,9,8,0,0,DateTimeKind.Utc);
        var spring=current(start);
        Check(current(start.AddTicks(-1))==null,"Planet Friday night cannot count as a weekend completion");
        Check((string)field(spring,"Key")=="20300309" && (DateTime)field(spring,"StartUtc")==start,
            "Planet opens at Saturday midnight Pacific with a stable weekend identity");
        DateTime end=(DateTime)field(spring,"EndUtc");
        Check(end==new DateTime(2030,3,11,7,0,0,DateTimeKind.Utc) && (end-start).TotalHours==47,
            "spring Planet weekend spans the real 47-hour daylight-saving interval");
        Check((string)field(current(end.AddTicks(-1)),"Key")=="20300309" && current(end)==null,
            "Sunday shares Saturday identity and Monday close is exclusive");
        Check((bool)Call(spring.GetType(),spring,"Contains",start) && !(bool)Call(spring.GetType(),spring,"Contains",end),
            "Planet event credit uses inclusive start and exclusive end");
        var fall=current(new DateTime(2030,11,2,7,0,0,DateTimeKind.Utc));
        Check(((DateTime)field(fall,"EndUtc")-(DateTime)field(fall,"StartUtc")).TotalHours==49,
            "autumn Planet weekend spans both repeated local hours");
        var one=current(new DateTime(2030,11,3,8,30,0,DateTimeKind.Utc));
        var two=current(new DateTime(2030,11,3,9,30,0,DateTimeKind.Utc));
        Check((string)field(one,"Key")=="20301102" && (string)field(two,"Key")=="20301102",
            "repeated Pacific hour cannot create a second weekend identity");
        var crossing=current(new DateTime(2023,1,1,12,0,0,DateTimeKind.Utc));
        Check((string)field(crossing,"Key")=="20221231","Planet year boundary preserves the Saturday weekend identity");
        var keys=Enumerable.Range(0,4).Select(i=>(string)field(current(start.AddDays(i*7)),"Key")).ToArray();
        Check(keys.Distinct().Count()==4,"four successive weekends have four distinct completion identities");
        bool rejected=false;try{current(DateTime.SpecifyKind(start,DateTimeKind.Unspecified));}catch(ArgumentException){rejected=true;}
        Check(rejected,"Planet calendar rejects ambiguous non-UTC timestamps");
        Check(StorageManager.Wing<int>(6009,6009)==60096009,"Planet wing maps to retained 6711 catalog key 60096009");
        foreach(uint id in new uint[]{18,1413}) {
            var monster=MonsterInformation.MonsterInformations[id];
            Check(monster.Hitpoints>0 && monster.MaxAttack>=monster.MinAttack,"Planet proposed monster base is present and has valid combat stats: "+id);
            System.Console.WriteLine("Planet catalog base "+id+": "+monster.Name+" hp="+monster.Hitpoints+" attack="+monster.MinAttack+"-"+monster.MaxAttack);
        }
    }

    static void VerifyHeartStore(Map map)
    {
        var heart=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeartCampaign");Call(heart,null,"Initialize");
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS mapid integer NOT NULL DEFAULT 1002,ADD COLUMN IF NOT EXISTS x integer NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS y integer NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS previousmapid integer NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS previousx integer NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS previousy integer NOT NULL DEFAULT 0");
        var clock=heart.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        DateTime now=new DateTime(2026,3,7,8,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            var p=ProgressPlayer(map,"");var other=ProgressPlayer(map,"");
            Check(Call(heart,null,"Enroll",p,"invalid")==null,"Heart rejects unknown faction");
            var run=Call(heart,null,"Enroll",p,"couples");
            Func<object,Guid> id=r=>(Guid)r.GetType().GetField("Id",Instance).GetValue(r);
            Guid first=id(run);
            Guid encounter=(Guid)Call(heart,null,"BeginEncounter",p,first,12001);
            Check(encounter!=Guid.Empty && (Guid)Call(heart,null,"BeginEncounter",p,first,12002)==Guid.Empty,"Heart owns at most one active encounter per player");
            Check(!(bool)Call(heart,null,"Return",p),"Heart active encounter cannot acknowledge a return");
            Func<Guid,int,Guid> spawn=(rid,slot)=>(Guid)Call(heart,null,"ActorIdentity",p,rid,encounter,slot);
            Check(run!=null && id(Call(heart,null,"Enroll",p,"couples"))==first,"Heart ordinary enrollment resumes the saved run");
            Check(Call(heart,null,"Enroll",p,"singles")==null,"Heart faction cannot change during the weekend");
            Check(!(bool)Call(heart,null,"Deliver",p,first),"Heart incomplete sortie cannot deliver");
            Check(!(bool)Call(heart,null,"Record",p,first,10,spawn(first,10),now),"Heart guard requires ten raiders first");
            Check(!(bool)Call(heart,null,"Record",p,first,0,spawn(first,0),now.AddTicks(-10)),"Heart rejects objective timestamps before sortie admission");
            Check(!(bool)Call(heart,null,"Record",other,first,0,spawn(first,0),now),"Heart foreign character cannot credit an owned run");
            Check(!(bool)Call(heart,null,"Record",p,first,0,Guid.NewGuid(),now),"Heart refuses an unregistered actor identity");
            Guid actor=spawn(first,0);int accepted=0;
            Check(actor!=Guid.Empty && spawn(first,0)==actor,"Heart repeated spawn reservation reuses the saved actor identity");
            Parallel.For(0,6,i=>{if((bool)Call(heart,null,"Record",p,first,0,actor,now))System.Threading.Interlocked.Increment(ref accepted);});
            Check(accepted==1,"Heart concurrent objective replay credits once");
            Check(!(bool)Call(heart,null,"Record",p,first,1,actor,now),"Heart actor cannot credit two objective slots");
            Query("CREATE FUNCTION reject_heart_mask() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_heart_mask BEFORE UPDATE ON revora_heart_runs FOR EACH ROW EXECUTE FUNCTION reject_heart_mask()");
            bool failed=false;Guid retry=spawn(first,1);
            try{Call(heart,null,"Record",p,first,1,retry,now);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER reject_heart_mask ON revora_heart_runs; DROP FUNCTION reject_heart_mask()");}
            Check(failed && (bool)Call(heart,null,"Record",p,first,1,retry,now),"Heart failed progress update rolls back objective receipt for safe retry");
            for(int sortie=1;sortie<=3;sortie++) {
                run=Call(heart,null,"Enroll",p,"couples");Guid current=id(run);
                if(sortie>1)encounter=(Guid)Call(heart,null,"BeginEncounter",p,current,12001);
                bool credited=true;for(int slot=sortie==1?2:0;slot<12;slot++)credited&=(bool)Call(heart,null,"Record",p,current,slot,spawn(current,slot),now);
                Check(credited,"Heart sortie requires ordered raiders guard and cache: "+sortie);
                Check((bool)Call(heart,null,"EndEncounter",p.Entity.UID,encounter),"Heart completed encounter releases its arena reservation: "+sortie);
                Check((bool)Call(heart,null,"Return",p) && p.Entity.MapID==1002 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,"Heart return persists safe town position before acknowledgement: "+sortie);
                int deliveries=0;Parallel.For(0,4,i=>{if((bool)Call(heart,null,"Deliver",p,current))System.Threading.Interlocked.Increment(ref deliveries);});
                Check(deliveries==1,"Heart concurrent delivery commits once: "+sortie);
            }
            Check(Call(heart,null,"Enroll",p,"couples")==null && Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL",p.Entity.UID)==3,"Heart caps each character at three durable deliveries");
            var otherRun=Call(heart,null,"Enroll",other,"singles");
            Guid interrupted=(Guid)Call(heart,null,"BeginEncounter",other,id(otherRun),12001);
            Guid stale=(Guid)Call(heart,null,"ActorIdentity",other,id(otherRun),interrupted,0);
            Check((int)Call(heart,null,"RecoverInterruptedEncounters")==1 && !(bool)Call(heart,null,"Record",other,id(otherRun),0,stale,now),"Heart interrupted encounter closes and its stale actor cannot earn credit");
            Check((Guid)Call(heart,null,"BeginEncounter",other,id(otherRun),12001)==Guid.Empty,"Heart pending safe return blocks another encounter");
            Query("CREATE FUNCTION reject_heart_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_heart_return BEFORE UPDATE ON revora_heart_encounters FOR EACH ROW EXECUTE FUNCTION reject_heart_return()");
            try {
                Check(!(bool)Call(heart,null,"Return",other) && other.Action==0 && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND NOT returned",other.Entity.UID)==1,"Heart suppressed return receipt fences the player and retains recovery work");
            } finally {Query("DROP TRIGGER reject_heart_return ON revora_heart_encounters; DROP FUNCTION reject_heart_return()");}
            // Fixture retry only: the real connection must reload after the fence.
            typeof(Entity).GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(other.Entity,false);other.Action=2;
            Check((bool)Call(heart,null,"Return",other) && (bool)Call(heart,null,"Return",other),"Heart interrupted return is durable and idempotent");
            Guid resumed=(Guid)Call(heart,null,"BeginEncounter",other,id(otherRun),12001);
            Guid resumedActor=(Guid)Call(heart,null,"ActorIdentity",other,id(otherRun),resumed,0);
            Check(resumed!=Guid.Empty && resumedActor!=stale && (bool)Call(heart,null,"Record",other,id(otherRun),0,resumedActor,now),"Heart abandoned sortie resumes with a fresh owned actor");
            Check(!(bool)Call(heart,null,"EndEncounter",p.Entity.UID,resumed),"Heart foreign owner cannot release another encounter");
            Guid expiredActor=(Guid)Call(heart,null,"ActorIdentity",other,id(otherRun),resumed,1);
            now=now.AddMinutes(16);
            Check(!(bool)Call(heart,null,"Record",other,id(otherRun),1,expiredActor,now),"Heart elapsed encounter cannot credit a registered actor");
            Call(heart,null,"EndEncounter",other.Entity.UID,resumed);
            now=new DateTime(2026,3,9,7,0,0,DateTimeKind.Utc);
            Check(!(bool)Call(heart,null,"Record",other,id(otherRun),0,Guid.NewGuid(),now) && Call(heart,null,"Enroll",other,"singles")==null,"Heart Pacific close rejects progress and admission across DST");
            now=new DateTime(2026,3,14,7,0,0,DateTimeKind.Utc);
            Check(Call(heart,null,"Enroll",p,"singles")!=null,"Heart next weekend allows a new faction and quota");
            VerifyHeartWorld(heart);
            VerifyHeartResults(map,heart);
            VerifyHeartNpc(heart);
        } finally {clock.SetValue(null,previous);}
    }

    static void VerifyHeartNpc(Type heart)
    {
        var clock=heart.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        var titleClock=TitleRules.GetField("Clock",Hidden);object previousTitle=titleClock.GetValue(null);
        var town=Kernel.Maps[1002];Revora.Interfaces.INpc oldNpc;town.Npcs.TryGetValue(380,out oldNpc);
        DateTime now=new DateTime(2026,4,4,7,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));titleClock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            var p=PlanetPlayer(town);p.ActiveNpc=380;
            town.Npcs[380]=new NpcSpawn{UID=380,MapID=1002,X=321,Y=299};
            Action home=()=>{p.ActiveNpc=380;p.Entity.X=321;p.Entity.Y=299;Call(heart,null,"Page",p,(byte)160);};
            home();Check(Call(heart,null,"Page",p,(byte)163)==null,"Heart forged faction confirmation is refused");
            Call(heart,null,"Page",p,(byte)161);
            Call(heart,null,"HandleNpc",p,new NpcRequest(true){OptionID=255});
            Check(Call(heart,null,"Page",p,(byte)163)==null,"Heart farewell invalidates faction confirmation");
            home();Call(heart,null,"Page",p,(byte)162);
            Check(((string)Call(heart,null,"Page",p,(byte)163)).Contains("ready"),"Heart ordinary player confirms free faction choice");
            Check(Call(heart,null,"Page",p,(byte)163)==null,"Heart faction confirmation replay is refused");
            p.Entity.X=400;Check(Call(heart,null,"Page",p,(byte)164)==null,"Heart distant commander request is refused");
            for(int sortie=1;sortie<=3;sortie++) {
                home();Call(heart,null,"Page",p,(byte)164);
                Check(p.Entity.MapID!=1002,"Heart NPC admits earned sortie "+sortie);
                foreach(var mob in p.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(p,mob);
                var guardian=p.Map.Entities.Values.Single(e=>e.EntityFlag==EntityFlag.Monster);PlanetCombatDefeat(p,guardian);
                p.ActiveNpc=900000191;p.Entity.X=48;p.Entity.Y=48;
                Check(Call(heart,null,"Page",p,(byte)1)==null,"Heart cache requires its own displayed menu "+sortie);
                Call(heart,null,"Page",p,(byte)0);
                Check(((string)Call(heart,null,"Page",p,(byte)1)).Contains("safe") && p.Entity.MapID==1002,"Heart cache dialogue returns completed sortie "+sortie);
                home();Check(((string)Call(heart,null,"Page",p,(byte)165)).Contains("100 support"),"Heart NPC delivers earned cache "+sortie);
                Check(((string)Call(heart,null,"Page",p,(byte)165)).Contains("no new cache"),"Heart repeated delivery adds no support "+sortie);
            }
            Check(((string)Call(heart,null,"Page",p,(byte)166)).Contains("Singles: 300. Your deliveries: 3/3"),"Heart standings show three earned deliveries");
            Call(heart,null,"Page",p,(byte)164);Check(p.Entity.MapID==1002,"Heart NPC refuses a fourth sortie");
            now=new DateTime(2026,4,6,7,0,0,DateTimeKind.Utc);
            Check((bool)Call(heart,null,"Freeze","20260404"),"Heart fully earned weekend closes");
            home();Call(heart,null,"Page",p,(byte)167);
            Check(p.Entity.ConquerPoints==5000 && p.Entity.RevoraPoint==1000 && OwnedTitles(p).ContainsKey(60186018),"Heart three combat sorties earn NPC currency and wing prizes");
            Call(heart,null,"Page",p,(byte)167);
            Check(p.Entity.ConquerPoints==5000 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",p.Entity.UID)==1,"Heart NPC prize replay cannot duplicate an earned reward");
            new TitleStorage().Login(p);TitleRequest(60186018,TitleStorage.Action.Equip).Handle(p);
            Check(p.Entity.EquippedWing==60186018,"Heart fully earned wing equips after wardrobe reload");
            VerifyHeartRestartProcess(heart,p,now.AddDays(30));
            now=now.AddDays(30);RefreshTitles(p);
            Check(!OwnedTitles(p).ContainsKey(60186018) && p.Entity.EquippedWing!=60186018,"Heart fully earned wing expires at its fixed deadline");
        } finally {
            clock.SetValue(null,previous);titleClock.SetValue(null,previousTitle);
            if(oldNpc!=null)town.Npcs[380]=oldNpc;else town.Npcs.Remove(380);
        }
    }

    static void VerifyHeartResults(Map map,Type heart)
    {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS conquerpoints bigint NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS revorapoint bigint NOT NULL DEFAULT 0");
        var clock=heart.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        try {
            foreach(bool tie in new[]{false,true}) {
                DateTime now=new DateTime(2026,3,tie?28:21,8,0,0,DateTimeKind.Utc),close=now.Date.AddDays(2).AddHours(7);
                clock.SetValue(null,(Func<DateTime>)(()=>now));
                string weekend=tie?"20260328":"20260321";
                var winner=ProgressPlayer(map,"");var rival=ProgressPlayer(map,"");var partial=ProgressPlayer(map,"");var empty=ProgressPlayer(map,"");
                Call(heart,null,"Enroll",empty,"singles");
                foreach(var p in new[]{winner,rival,partial}) {
                    Call(heart,null,"Enroll",p,ReferenceEquals(p,rival)?"singles":"couples");
                    int count=ReferenceEquals(p,partial)?(tie?0:1):3;
                    // Frozen-result fixture only; objective/actor earning is covered separately.
                    for(int ordinal=1;ordinal<=count;ordinal++) {
                        if(ordinal>1)Query("INSERT INTO revora_heart_runs(id,weekend,uid,ordinal,started_at) VALUES(@id,@weekend,@uid,@ordinal,@at)","id",Guid.NewGuid(),"weekend",weekend,"uid",(long)p.Entity.UID,"ordinal",ordinal,"at",new DateTimeOffset(now));
                        Query("UPDATE revora_heart_runs SET mask=4095,delivered_at=@at WHERE uid=@uid AND weekend=@weekend AND ordinal=@ordinal","at",new DateTimeOffset(now),"uid",(long)p.Entity.UID,"weekend",weekend,"ordinal",ordinal);
                    }
                }
                Check(!(bool)Call(heart,null,"Freeze",weekend),"Heart cannot freeze an open weekend");
                now=close;
                Query("CREATE FUNCTION suppress_heart_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_heart_result BEFORE INSERT ON revora_heart_results FOR EACH ROW EXECUTE FUNCTION suppress_heart_result()");
                bool failed=false;try{Call(heart,null,"Freeze",weekend);}catch(InvalidOperationException){failed=true;}
                finally{Query("DROP TRIGGER suppress_heart_result ON revora_heart_results; DROP FUNCTION suppress_heart_result()");}
                Check(failed && Scalar("SELECT count(*) FROM revora_heart_scores s JOIN revora_heart_entries e USING(weekend) WHERE e.uid=@uid",winner.Entity.UID)==0,"Heart suppressed prizes roll back frozen faction totals");
                int closed=0;Parallel.For(0,4,i=>{if((bool)Call(heart,null,"Freeze",weekend))System.Threading.Interlocked.Increment(ref closed);});
                Check(closed==1,"Heart concurrent weekend close commits one snapshot");
                Check(Scalar("SELECT cp FROM revora_heart_results WHERE uid=@uid",winner.Entity.UID)==5000 && Scalar("SELECT rvp FROM revora_heart_results WHERE uid=@uid",winner.Entity.UID)==1000,"Heart qualifying winner has the fixed currency entitlement");
                Check(Scalar("SELECT cp FROM revora_heart_results WHERE uid=@uid",rival.Entity.UID)==(tie?5000:0),"Heart ties reward both factions and losses omit currency: tie="+tie);
                Check(Scalar("SELECT count(*) FROM revora_heart_results WHERE uid=@uid",partial.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_heart_results WHERE uid=@uid",empty.Entity.UID)==0,"Heart partial and empty entrants do not qualify for wings");
                Check(Scalar("SELECT count(*) FROM revora_heart_results r JOIN revora_heart_weekends w USING(weekend) WHERE r.uid=@uid AND r.earned_at=w.ends_at AND r.expires_at=w.ends_at+interval '720 hours' AND NOT r.award_recorded",rival.Entity.UID)==1,"Heart both qualifying factions retain a close-anchored thirty-day wing entitlement");
                now=now.AddDays(2);
                Check(!(bool)Call(heart,null,"Freeze",weekend),"Heart delayed repeat close cannot renew prize expiry");
                VerifyHeartPrizes(heart,weekend,winner,rival,tie,close,now);
            }
        } finally {clock.SetValue(null,previous);}
    }

    static void VerifyHeartPrizes(Type heart,string weekend,GameState winner,GameState rival,bool tie,DateTime close,DateTime now)
    {
        var titleClock=TitleRules.GetField("Clock",Hidden);object previous=titleClock.GetValue(null);
        titleClock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            uint cp=winner.Entity.ConquerPoints,rvp=winner.Entity.RevoraPoint;
            Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)uint.MaxValue,"uid",(long)winner.Entity.UID);
            Call(typeof(Entity),winner.Entity,"ApplyCommittedConquerPoints",uint.MaxValue);
            Check(!(bool)Call(heart,null,"Claim",winner,weekend) && !Fenced(winner) && Scalar("SELECT count(*) FROM revora_heart_results WHERE uid=@uid AND NOT award_recorded",winner.Entity.UID)==1,"Heart currency overflow leaves the complete prize pending without a partial wing");
            Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)cp,"uid",(long)winner.Entity.UID);
            Call(typeof(Entity),winner.Entity,"ApplyCommittedConquerPoints",cp);
            Query("CREATE FUNCTION reject_heart_prize() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.award_recorded THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_heart_prize BEFORE UPDATE ON revora_heart_results FOR EACH ROW EXECUTE FUNCTION reject_heart_prize()");
            try {Check(!(bool)Call(heart,null,"Claim",winner,weekend) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",winner.Entity.UID)==0 && Scalar("SELECT COALESCE(conquerpoints,0) FROM entities WHERE uid=@uid",winner.Entity.UID)==cp,"Heart failed claim acknowledgement rolls back wing and currency together");}
            finally {Query("DROP TRIGGER reject_heart_prize ON revora_heart_results; DROP FUNCTION reject_heart_prize()");}
            typeof(Entity).GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(winner.Entity,false);winner.Action=2;
            int claims=0;Parallel.For(0,4,i=>{if((bool)Call(heart,null,"Claim",winner,weekend))System.Threading.Interlocked.Increment(ref claims);});
            Check(claims==1 && winner.Entity.ConquerPoints==cp+5000 && winner.Entity.RevoraPoint==rvp+1000,"Heart concurrent claim awards live currency once");
            Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",winner.Entity.UID)==cp+5000 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",winner.Entity.UID)==rvp+1000 && OwnedTitles(winner).ContainsKey(60186018),"Heart claim persists balances and immediately reconciles wardrobe ownership");
            if(tie) {
                var hook=heart.GetField("BeforePrizeInstall",Hidden);hook.SetValue(null,(Action<GameState>)(p=>{throw new InvalidOperationException("fixture prize install failure");}));
                try{Check(!(bool)Call(heart,null,"Claim",rival,weekend) && rival.Action==0,"Heart interrupted postcommit installation fences the session");}
                finally{hook.SetValue(null,null);}
                Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",rival.Entity.UID)==5000 && Scalar("SELECT count(*) FROM revora_heart_results WHERE uid=@uid AND award_recorded",rival.Entity.UID)==1 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",rival.Entity.UID)==1,"Heart postcommit interruption retains currency wing and receipt atomically");
                Check(!(bool)Call(heart,null,"Claim",rival,weekend) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",rival.Entity.UID)==5000,"Heart fenced replay cannot double an already committed prize");
            } else {
                Check((bool)Call(heart,null,"Claim",rival,weekend) && rival.Entity.ConquerPoints==0 && rival.Entity.RevoraPoint==0 && OwnedTitles(rival).ContainsKey(60186018),"Heart qualified losing faction receives wings without winner currency");
            }
            new TitleStorage().Login(winner);TitleRequest(60186018,TitleStorage.Action.Equip).Handle(winner);
            Check(winner.Entity.EquippedWing==60186018,"Heart ordinary winner can equip earned wings");
            titleClock.SetValue(null,(Func<DateTime>)(()=>close.AddDays(30)));RefreshTitles(winner);
            Check(!OwnedTitles(winner).ContainsKey(60186018) && winner.Entity.EquippedWing!=60186018,"Heart wing expires and unequips at the original close plus thirty days");
        } finally {titleClock.SetValue(null,previous);}
    }

    static void VerifyHeartWorld(Type heart)
    {
        var world=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeartEncounter");
        Call(world,null,"Initialize");
        var terrain=World(Environment.CurrentDirectory,700);terrain.Path="heart-fixture";
        DMaps.MapPaths[700]="heart-fixture";DMaps.MapPaths[1002]="heart-town-fixture";
        var town=Kernel.Maps[1002];var p=PlanetPlayer(town);
        Call(heart,null,"Enroll",p,"singles");
        Check((bool)Call(world,null,"Enter",p),"Heart ordinary enrolled character enters an owned scene");
        bool refused=false;try{Call(world,null,"Initialize");}catch(InvalidOperationException){refused=true;}
        Check(refused,"Heart startup recovery refuses to abandon live component scenes");
        var arena=p.Map;ushort arenaId=arena.ID;
        var raiders=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).OrderBy(e=>e.MonsterInfo.ID).ToArray();
        Check(arena.ID!=700 && raiders.Length==10 && raiders.All(e=>e.MaxHitpoints==35050),"Heart spawns ten owned raiders without an early guardian");
        p.ActiveNpc=900000191;p.Entity.X=48;p.Entity.Y=48;
        Check(!(bool)Call(world,null,"Collect",p),"Heart cache cannot skip raiders and guardian");
        var foreign=PlanetPlayer(town);uint hp=raiders[0].Hitpoints;raiders[0].Die(foreign.Entity);
        Check(raiders[0].Hitpoints==hp && (bool)Call(world,null,"BlockAttack",foreign.Entity,raiders[0]),"Heart foreign attacker cannot damage or credit owned raiders");
        uint damage=100;var hit=new Attack(true){Attacker=foreign.Entity.UID,Attacked=raiders[0].UID,AttackType=Attack.Melee};
        Revora.Game.Attacking.Handle.ReceiveAttack(foreign.Entity,raiders[0],hit,ref damage,null);
        Check(damage==0 && raiders[0].Hitpoints==hp,"Heart normal damage application rejects a foreign fighter before HP changes");
        Check(!(bool)Call(world,null,"AllowTeleport",foreign,arena.ID,(ushort)48,(ushort)41),"Heart private scene rejects foreign teleport");
        foreach(var mob in raiders){PlanetCombatDefeat(p,mob);mob.Die(p.Entity);}
        var guards=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray();
        Check(guards.Length==1 && guards[0].MonsterInfo.ID==9926210 && guards[0].MaxHitpoints==210300,"Heart guardian appears only after all ten durable raider deaths");
        var guardian=guards[0];PlanetCombatDefeat(p,guardian);
        p.Entity.X=48;p.Entity.Y=48;p.ActiveNpc=900000191;
        Check((bool)Call(world,null,"Collect",p) && p.Entity.MapID==1002,"Heart guarded cache commits and returns its owner safely");
        Check(!Kernel.Maps.ContainsKey(arenaId) && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='complete' AND returned",p.Entity.UID)==1,"Heart completed scene is disposed after durable safe return");
        var run=Call(heart,null,"Resume",p);Guid id=(Guid)run.GetType().GetField("Id",Instance).GetValue(run);
        Check((bool)Call(heart,null,"Deliver",p,id),"Heart actual raider guardian and cache route qualifies for delivery");
        Call(heart,null,"Enroll",p,"singles");Check((bool)Call(world,null,"Enter",p),"Heart next earned sortie opens a fresh scene");
        arenaId=p.Entity.MapID;p.Entity.Teleport(1002,321,299);Call(world,null,"Tick");
        Check(!Kernel.Maps.ContainsKey(arenaId) && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='abandoned' AND returned",p.Entity.UID)==1,"Heart departed owner triggers abandonment return and scene disposal");
        Check((bool)Call(world,null,"Enter",p),"Heart saved unfinished sortie resumes after abandoned scene");
        arenaId=p.Entity.MapID;
        var first=p.Map.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster);p.Entity.X=(ushort)(first.X-1);p.Entity.Y=first.Y;first.Die(p.Entity);
        long mask=Scalar("SELECT mask FROM revora_heart_runs WHERE uid=@uid ORDER BY ordinal DESC LIMIT 1",p.Entity.UID);
        var resumed=PlanetRestartPlayer(p.Map,p.Entity.UID);Call(world,null,"OnLogin",resumed);
        Check(!Fenced(resumed) && resumed.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(arenaId),"Heart canonical replacement session recovers town and removes old scene");
        Check(Scalar("SELECT mask FROM revora_heart_runs WHERE uid=@uid ORDER BY ordinal DESC LIMIT 1",p.Entity.UID)==mask,"Heart reconnect recovery preserves earned objectives without crediting the cache");
        Check((bool)Call(world,null,"Enter",resumed) && resumed.Map.Entities.Values.Count(e=>e.EntityFlag==EntityFlag.Monster)==9,"Heart resumed encounter spawns only nine remaining raiders");
        Call(world,null,"OnLogin",resumed);
        Call(world,null,"OnLogin",resumed);
        Check(!Fenced(resumed) && resumed.Entity.MapID==1002,"Heart repeated login recovery is idempotent");
    }

    static void VerifyMrMs2017Donations(Map map)
    {
        var eventType=typeof(Entity).Assembly.GetType("Revora.Game.Features.MrMs2017");
        Call(eventType,null,"Initialize");
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS body integer NOT NULL DEFAULT 1003");
        string season=(string)Call(eventType,null,"SeasonOf",DateTime.UtcNow);
        var candidate=ProgressPlayer(map,"");candidate.Entity.Body=2001;
        Query("UPDATE entities SET body=2001 WHERE uid=@uid","uid",(long)candidate.Entity.UID);
        var sender=ProgressPlayer(map,"");
        Check((bool)Call(eventType,null,"Register",candidate,season),"2017 ordinary registration persists");
        Check(!(bool)Call(eventType,null,"Register",candidate,season),"2017 duplicate registration keeps first record");
        var flowers=RuneItem(sender,754003,0,3);Guid request=Guid.NewGuid();
        Check((bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,request),"2017 unbound tulip donation succeeds");
        Check(flowers.StackSize==2 && SavedRune(flowers,"stacksize")==2,"2017 consumes one stack unit durably");
        Check(Scalar("SELECT sum(points) FROM revora_mrms2017_donations WHERE sender=@uid",sender.Entity.UID)==6,"2017 tulip bundle earns double support");
        Check(!(bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,request) && flowers.StackSize==2,"2017 replay does not consume or credit again");
        Check(!(bool)Call(eventType,null,"Donate",sender,season,sender.Entity.UID,flowers.UID,Guid.NewGuid()),"2017 self vote rejected");
        flowers.Bound=true;
        Check(!(bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,Guid.NewGuid()),"2017 bound flowers rejected");flowers.Bound=false;
        int wins=0;Guid parallel=Guid.NewGuid();
        Parallel.For(0,4,j=>{if((bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,parallel))System.Threading.Interlocked.Increment(ref wins);});
        Check(wins==1 && flowers.StackSize==1 && SavedRune(flowers,"stacksize")==1,"2017 concurrent identical request consumes once");
        Query("CREATE FUNCTION reject_2017() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_2017 BEFORE INSERT ON revora_mrms2017_donations FOR EACH ROW EXECUTE FUNCTION reject_2017()");
        Check(!(bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,Guid.NewGuid()),"2017 missing donation receipt refuses success");
        Check(SavedRune(flowers,"stacksize")==1 && flowers.StackSize==1,"2017 suppressed receipt rolls back flower consumption");
        Query("DROP TRIGGER reject_2017 ON revora_mrms2017_donations; DROP FUNCTION reject_2017()");
        typeof(Entity).GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(sender.Entity,false);sender.Action=2;
        Check((bool)Call(eventType,null,"Donate",sender,season,candidate.Entity.UID,flowers.UID,Guid.NewGuid()),"2017 rollback allows valid retry");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid",flowers.UID)==0 && !sender.Inventory.ContainsUID(flowers.UID),"2017 final stack removes persisted and live item");
        Check((int)Call(eventType,null,"FlowerPoints",751999u)==999 && (int)Call(eventType,null,"FlowerPoints",754999u)==1998,"2017 flower weights match bundle quantity");
        Check((int)Call(eventType,null,"FlowerPoints",755001u)==0 && (int)Call(eventType,null,"FlowerPoints",754000u)==0,"2017 male gifts and zero flowers rejected");
        var stopped=ProgressPlayer(map,"");var held=RuneItem(stopped,751003,0,1);
        var clock=eventType.GetField("Clock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var oldClock=clock.GetValue(null);
        clock.SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow.AddMonths(1)));
        try { Check(!(bool)Call(eventType,null,"Donate",stopped,season,candidate.Entity.UID,held.UID,Guid.NewGuid()),"2017 stale month refuses donation"); }
        finally {clock.SetValue(null,oldClock);}
        Check(SavedRune(held,"stacksize")==1,"2017 stale month preserves flower");
        var interrupted=ProgressPlayer(map,"");var committed=RuneItem(interrupted,751003,0,1);
        var hook=eventType.GetField("BeforeInstall",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        hook.SetValue(null,new Action<GameState>(p=>{throw new InvalidOperationException("fixture after commit");}));
        try { Check(!(bool)Call(eventType,null,"Donate",interrupted,season,candidate.Entity.UID,committed.UID,Guid.NewGuid()),"2017 interrupted client installation is fenced"); }
        finally {hook.SetValue(null,null);}
        Check(interrupted.Action==0 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",committed.UID)==0 && Scalar("SELECT sum(points) FROM revora_mrms2017_donations WHERE sender=@uid",interrupted.Entity.UID)==3,"2017 postcommit failure keeps durable support and consumption");
        Check(!(bool)Call(eventType,null,"Donate",interrupted,season,candidate.Entity.UID,committed.UID,Guid.NewGuid()),"2017 fenced stale inventory cannot donate twice");
        VerifyMrMs2017Results(map,eventType,season,candidate);
        VerifyMrMs2017Calendar(eventType);
        VerifyMrMs2017Npc(map,eventType);
    }

    static void VerifyMrMs2017Npc(Map map,Type eventType)
    {
        map=Kernel.Maps[1002];
        var clock=eventType.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        Revora.Interfaces.INpc oldNpc;map.Npcs.TryGetValue(380,out oldNpc);
        try {
            clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2050,1,15,12,0,0,DateTimeKind.Utc)));
            var candidate=ProgressPlayer(map,"");candidate.Entity.Body=2001;candidate.ActiveNpc=380;
            Query("UPDATE entities SET body=2001 WHERE uid=@uid","uid",(long)candidate.Entity.UID);
            map.Npcs[380]=new NpcSpawn{UID=380,MapID=map.ID,X=candidate.Entity.X,Y=candidate.Entity.Y};
            Check(Call(eventType,null,"Page",candidate,(byte)241)==null,"2017 forged registration without a menu is refused");
            Check(((string)Call(eventType,null,"Page",candidate,(byte)240)).Contains("monthly"),"2017 ordinary player opens NPC contest");
            Check(((string)Call(eventType,null,"Page",candidate,(byte)241)).Contains("entered"),"2017 ordinary NPC registration succeeds");
            var donor=ProgressPlayer(map,"");donor.ActiveNpc=380;
            map.Npcs[380].X=donor.Entity.X;map.Npcs[380].Y=donor.Entity.Y;
            var flowers=RuneItem(donor,754003,0,2);
            Call(eventType,null,"Page",donor,(byte)240);
            Call(eventType,null,"Page",donor,(byte)244);
            var choices=(List<Tuple<string,byte>>)Call(eventType,null,"LastMenu",donor);
            Check(choices.Count(c=>c.Item2>=180 && c.Item2<=187)==1,"2017 category page selects registered women only");
            Call(eventType,null,"Page",donor,(byte)180);
            Check(((string)Call(eventType,null,"Page",donor,(byte)190)).Contains("6 support"),"2017 confirmation names bundle support before charging");
            Check(SavedRune(flowers,"stacksize")==2,"2017 selecting a flower does not consume it");
            Check(((string)Call(eventType,null,"Page",donor,(byte)247)).Contains("delivered"),"2017 ordinary confirmation delivers flowers");
            Check(Call(eventType,null,"Page",donor,(byte)247)==null && SavedRune(flowers,"stacksize")==1,"2017 replayed confirm cannot spend a second bundle");
            Check(((string)Call(eventType,null,"Page",donor,(byte)242)).Contains(": 6"),"2017 NPC standings reflect committed support");
            Call(eventType,null,"Page",donor,(byte)244);Call(eventType,null,"Page",donor,(byte)180);Call(eventType,null,"Page",donor,(byte)190);
            Call(eventType,null,"HandleNpc",donor,new NpcRequest(true){OptionID=255});
            Check(Call(eventType,null,"Page",donor,(byte)247)==null && SavedRune(flowers,"stacksize")==1,"2017 farewell invalidates a pending gift confirmation");
            Call(eventType,null,"Page",donor,(byte)240);Call(eventType,null,"Page",donor,(byte)244);Call(eventType,null,"Page",donor,(byte)180);Call(eventType,null,"Page",donor,(byte)190);
            clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2050,2,1,8,0,0,DateTimeKind.Utc)));
            Check(((string)Call(eventType,null,"Page",donor,(byte)247)).Contains("new month") && SavedRune(flowers,"stacksize")==1,"2017 old confirmation cannot donate after month rollover");
            map.Npcs[380].X=(ushort)(donor.Entity.X+20);
            Check(Call(eventType,null,"Page",donor,(byte)240)==null,"2017 NPC refuses distant player");
            VerifyMrMs2017Pages(map,eventType,clock);
        } finally {
            clock.SetValue(null,previous);
            if(oldNpc!=null)map.Npcs[380]=oldNpc;else map.Npcs.Remove(380);
        }
    }

    static void VerifyMrMs2017Pages(Map map,Type eventType,System.Reflection.FieldInfo clock)
    {
        clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2051,1,15,12,0,0,DateTimeKind.Utc)));
        var candidates=new List<GameState>();
        for(int i=0;i<10;i++) {
            var p=ProgressPlayer(map,"");p.Entity.Body=2001;
            Query("UPDATE entities SET body=2001 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Check((bool)Call(eventType,null,"Register",p,"2051-01"),"2017 pagination contestant registers");candidates.Add(p);
        }
        var donor=ProgressPlayer(map,"");donor.ActiveNpc=380;
        map.Npcs[380].X=donor.Entity.X;map.Npcs[380].Y=donor.Entity.Y;
        var flowers=new List<ConquerItem>();for(int i=0;i<9;i++)flowers.Add(RuneItem(donor,751003,0,2));
        Call(eventType,null,"Page",donor,(byte)240);Call(eventType,null,"Page",donor,(byte)244);
        var menu=(List<Tuple<string,byte>>)Call(eventType,null,"LastMenu",donor);
        Check(menu.Count(c=>c.Item2>=180 && c.Item2<=187)==8 && menu.Any(c=>c.Item2==245),"2017 first contestant page exposes eight pinned choices and next");
        Call(eventType,null,"Page",donor,(byte)245);
        menu=(List<Tuple<string,byte>>)Call(eventType,null,"LastMenu",donor);
        Check(menu.Count(c=>c.Item2>=180 && c.Item2<=187)==2 && !menu.Any(c=>c.Item2==245),"2017 final contestant page has no phantom next action");
        Check(Call(eventType,null,"Page",donor,(byte)187)==null,"2017 nonexistent displayed candidate index is rejected");
        Call(eventType,null,"Page",donor,(byte)180);
        menu=(List<Tuple<string,byte>>)Call(eventType,null,"LastMenu",donor);
        Check(menu.Count(c=>c.Item2>=190 && c.Item2<=197)==8 && menu.Any(c=>c.Item2==198),"2017 flower page lists eight inventory identities");
        Call(eventType,null,"Page",donor,(byte)198);
        menu=(List<Tuple<string,byte>>)Call(eventType,null,"LastMenu",donor);
        Check(menu.Count(c=>c.Item2>=190 && c.Item2<=197)==1 && menu.Any(c=>c.Item2==199),"2017 last flower page retains previous navigation");
        Call(eventType,null,"Page",donor,(byte)199);Call(eventType,null,"Page",donor,(byte)198);Call(eventType,null,"Page",donor,(byte)190);
        int delivered=0;Parallel.For(0,4,i=>{string text=(string)Call(eventType,null,"Page",donor,(byte)247);if(text!=null && text.Contains("have been delivered"))System.Threading.Interlocked.Increment(ref delivered);});
        Check(delivered==1 && SavedRune(flowers[8],"stacksize")==1 && SavedRune(flowers[0],"stacksize")==2,"2017 concurrent confirmation consumes only the pinned later-page flower");
        Check(Scalar("SELECT sum(points) FROM revora_mrms2017_donations WHERE candidate=@uid",candidates[8].Entity.UID)==3,"2017 later-page selection credits the pinned contestant");
    }

    static void VerifyMrMs2017Calendar(Type eventType)
    {
        var clock=eventType.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        try {
            DateTime boundary=new DateTime(2040,4,1,7,0,0,DateTimeKind.Utc);
            Query("INSERT INTO revora_mrms2017_seasons(season) VALUES('2040-01'),('2040-02'),('2040-03'),('2040-04')");
            clock.SetValue(null,(Func<DateTime>)(()=>boundary.AddTicks(-1)));
            Check((int)Call(eventType,null,"ProcessDue")==2,"2017 downtime closes saved older months only");
            Check(Scalar("SELECT count(*) FROM revora_mrms2017_seasons WHERE season='2040-03' AND closed_at IS NULL",0)==1,"2017 current month remains open before Pacific midnight");
            clock.SetValue(null,(Func<DateTime>)(()=>boundary));
            int closed=0;Parallel.For(0,4,i=>System.Threading.Interlocked.Add(ref closed,(int)Call(eventType,null,"ProcessDue")));
            Check(closed==1,"2017 concurrent calendar pulses close month exactly once at boundary");
            Check((int)Call(eventType,null,"ProcessDue")==0,"2017 repeated calendar pulse creates no new seasons");
            Check(Scalar("SELECT count(*) FROM revora_mrms2017_seasons WHERE season='2040-04' AND closed_at IS NULL",0)==1,"2017 recovery preserves next month registration window");
            Check((DateTime)Call(eventType,null,"CloseOf","2040-03")==boundary,"2017 spring close follows Pacific daylight time");
            Check((DateTime)Call(eventType,null,"CloseOf","2040-11")==new DateTime(2040,12,1,8,0,0,DateTimeKind.Utc),"2017 winter close follows Pacific standard time");
        } finally {clock.SetValue(null,previous);}
    }

    static void VerifyMrMs2017Results(Map map,Type eventType,string season,GameState winner)
    {
        var second=ProgressPlayer(map,"");var third=ProgressPlayer(map,"");var empty=ProgressPlayer(map,"");
        foreach(var p in new[]{second,third,empty}) {
            p.Entity.Body=2001;Query("UPDATE entities SET body=2001 WHERE uid=@uid","uid",(long)p.Entity.UID);
            Check((bool)Call(eventType,null,"Register",p,season),"2017 additional ordinary contestant registers");
        }
        var donor=ProgressPlayer(map,"");
        foreach(var p in new[]{second,third}) {
            var item=RuneItem(donor,751003,0,1);
            Check((bool)Call(eventType,null,"Donate",donor,season,p.Entity.UID,item.UID,Guid.NewGuid()),"2017 tied contestants receive real flower support");
        }
        Query("UPDATE revora_mrms2017_entrants SET registered_at='2026-01-01T00:00:00Z' WHERE uid IN (@a,@b)","a",(long)second.Entity.UID,"b",(long)third.Entity.UID);
        DateTime close=(DateTime)Call(eventType,null,"CloseOf",season);
        var clock=eventType.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        var titleClock=TitleRules.GetField("Clock",Hidden);object previousTitle=titleClock.GetValue(null);
        try {
            clock.SetValue(null,(Func<DateTime>)(()=>close.AddDays(2)));
            titleClock.SetValue(null,(Func<DateTime>)(()=>close.AddDays(2)));
            int freezes=0;Parallel.For(0,4,i=>{if((bool)Call(eventType,null,"Freeze",season))System.Threading.Interlocked.Increment(ref freezes);});
            Check(freezes==1,"2017 concurrent close freezes exactly once");
            Check(Scalar("SELECT placement FROM revora_mrms2017_results WHERE uid=@uid",winner.Entity.UID)==1,"2017 highest flower support wins");
            Check(Scalar("SELECT placement FROM revora_mrms2017_results WHERE uid=@uid",Math.Min(second.Entity.UID,third.Entity.UID))==2,"2017 exact ties use UID after registration time");
            Check(Scalar("SELECT count(*) FROM revora_mrms2017_results WHERE uid=@uid",empty.Entity.UID)==0,"2017 zero support earns no prize");
            Query("CREATE FUNCTION reject_2017_ack() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_2017_ack BEFORE UPDATE ON revora_mrms2017_results FOR EACH ROW EXECUTE FUNCTION reject_2017_ack()");
            bool failed=false;
            try{Call(EvidenceType,null,"Read",winner.Entity.UID,"",false,new int[0],close.AddDays(2));}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER reject_2017_ack ON revora_mrms2017_results; DROP FUNCTION reject_2017_ack()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source LIKE 'mrms-2017-%'",winner.Entity.UID)==0,"2017 failed acknowledgement rolls back every award");
            Parallel.For(0,4,i=>Call(EvidenceType,null,"Read",winner.Entity.UID,"",false,new int[0],close.AddDays(2)));
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source LIKE 'mrms-2017-%'",winner.Entity.UID)==3,"2017 concurrent recovery awards champion exactly once");
            Query("UPDATE revora_mrms2017_results SET award_recorded=false WHERE uid=@uid","uid",(long)winner.Entity.UID);
            Call(EvidenceType,null,"Read",winner.Entity.UID,"",false,new int[0],close.AddDays(2));
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source LIKE 'mrms-2017-%'",winner.Entity.UID)==3,"2017 existing exact awards recover their acknowledgement without duplicates");
            new TitleStorage().Login(winner);new TitleStorage().Login(second);new TitleStorage().Login(third);
            Check(OwnedTitles(winner).ContainsKey(20442044) && OwnedTitles(winner).ContainsKey(20402040) && OwnedTitles(winner).ContainsKey(60126012),"2017 ordinary champion receives both titles and Romance wings");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='mrms-2017-wing-1-v1' AND expires_at IS NULL",winner.Entity.UID)==1,"2017 first place wings are permanent");
            Check(Scalar("SELECT count(*) FROM revora_event_awards a JOIN revora_mrms2017_seasons s ON a.source_key LIKE s.season || ':%' WHERE a.uid=@uid AND a.source='mrms-2017-local-v1' AND a.expires_at=s.closed_at+interval '360 days'",second.Entity.UID)==1,"2017 delayed delivery expires from season close");
            string later=(string)Call(eventType,null,"SeasonOf",close.AddDays(2));
            DateTime laterClose=(DateTime)Call(eventType,null,"CloseOf",later);
            Query("INSERT INTO revora_mrms2017_seasons(season,closed_at) VALUES(@season,@close)","season",later,"close",new DateTimeOffset(laterClose));
            Query("INSERT INTO revora_mrms2017_entrants(season,uid,contest,registered_at) VALUES(@season,@uid,'ms',@at)","season",later,"uid",(long)winner.Entity.UID,"at",close);
            Query("INSERT INTO revora_mrms2017_results(season,uid,contest,placement,support) VALUES(@season,@uid,'ms',3,1)","season",later,"uid",(long)winner.Entity.UID);
            titleClock.SetValue(null,(Func<DateTime>)(()=>laterClose.AddDays(1)));
            new TitleStorage().Login(winner);
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND title_key=60126012 AND source LIKE 'mrms-2017-%'",winner.Entity.UID)==2,"2017 repeat placement retains independent permanent and timed receipts");
            titleClock.SetValue(null,(Func<DateTime>)(()=>close.AddDays(366)));
            RefreshTitles(winner);
            Check(OwnedTitles(winner).ContainsKey(60126012) && !OwnedTitles(winner).ContainsKey(20402040),"2017 permanent wings survive title expiration");
            TitleRequest(60126012,TitleStorage.Action.Equip).Handle(winner);
            winner.Entity.MyAchievement=new Achievement(winner.Entity);winner.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(winner);
            Check(OwnedTitles(winner).ContainsKey(60126012) && winner.Entity.EquippedWing==60126012,"2017 persisted permanent wing remains owned and equipped after wardrobe reconstruction");
        } finally {clock.SetValue(null,previous);titleClock.SetValue(null,previousTitle);}
    }

    static void VerifyGrandmasterEarning(string runtime,Map town,string fixture)
    {
        Query("CREATE TABLE IF NOT EXISTS chi (LIKE public.chi INCLUDING ALL)");
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        var p=StoredProgressionPlayer(town);p.Entity.Level=110;p.Entity.Reborn=2;
        Check(EntityTable.SaveEntity(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==0,
            "ordinary qualified Grandmaster candidate starts without CP funding");
        ChiTable.Load(p);new TitleStorage().Login(p);
        Check(p.ChiPoints==0 && p.ChiPowers.Count==0 && !OwnedTitles(p).ContainsKey(20180001),"candidate starts without Chi powers or Grandmaster title");
        var priorClock=Daily.GetField("Clock",Hidden).GetValue(null);string priorDirectory=Environment.CurrentDirectory;
        var next=typeof(GeneralData).GetField("ChiValueNext",Hidden);var rate=typeof(GeneralData).GetField("ChiValueRate",Hidden);
        var priorNext=next.GetValue(null);var priorRate=rate.GetValue(null);
        Action<Chi> dispatch=request=>{
            byte[] bytes=request.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,16,bytes);PacketHandler.HandlePacket(bytes,p);
        };
        try {
            Directory.CreateDirectory(Path.Combine(fixture,"database"));Environment.CurrentDirectory=fixture;
            dailyNow=new DateTime(2032,2,1,0,0,0,DateTimeKind.Local).ToUniversalTime();
            Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));Call(Daily,null,"Create");
            DailyTick(DailySchedule().First());var boss=DailyActive();if(boss==null)throw new Exception("Grandmaster funding boss absent");
            p.Entity.MapID=boss.MapID;p.Entity.X=boss.X;p.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
            Check(DailyActive()==null && !Fenced(p) && p.Entity.ConquerPoints==1000000
                && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID)==1,
                "scheduled boss combat earns durable ordinary CP funding for Chi");
            p.Entity.MapID=1002;p.Entity.X=327;p.Entity.Y=303;
            dispatch(new Chi(true){Type=Chi.BuyStrength});
            Check(!Fenced(p) && p.ChiPoints==4000 && p.Entity.ConquerPoints==999000 && JadeChi(p)==4000,
                "normal Chi refill buys 4000 points with 1000 earned CPs");
            // Select legal maximum outcomes through the production value-range sampler.
            // Level/rebirth, clock and random outcomes are controlled prerequisites.
            next.SetValue(null,(Func<int,int,int>)((low,high)=>high-1));rate.SetValue(null,(Func<int,int,bool>)((chance,total)=>true));
            for(int power=1;power<=4;power++) {
                var mode=(Enums.ChiPowerType)power;
                dispatch(new Chi(true){Type=Chi.Unlock,Mode=mode});
                Check(!Fenced(p) && p.ChiPowers.Count==power && p.ChiPoints==4000-350*(power-1)-300,
                    "ordinary ordered Chi unlock with prior power qualification: "+mode);
                dispatch(new Chi(true){Type=Chi.Study,Mode=mode});
                Check(!Fenced(p) && p.ChiPowers[power-1].Points==400 && p.ChiPoints==4000-350*power
                    && OwnedTitles(p).ContainsKey(20180001)==(power==4),"full legal pursuit immediately reconciles exact Grandmaster threshold: "+mode);
            }
            TitleRequest(20180001,TitleStorage.Action.Equip).Handle(p);
            Check(p.Entity.EquippedTitle==20180001 && OwnedTitles(p)[20180001] && p.Entity.ConquerPoints==999000 && JadeChi(p)==2600,
                "normal wardrobe request equips the actually earned Grandmaster title after exact payment");
            Check(EntityTable.SaveEntity(p),"qualified Grandmaster character saves normally");
            p=ReloadBall(p);ChiTable.Load(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.ChiPowers.Count==4 && p.ChiPowers.All(power=>power.Points==400) && p.ChiPoints==2600
                && p.Entity.ConquerPoints==999000 && p.Entity.EquippedTitle==20180001 && OwnedTitles(p)[20180001],
                "full character and Chi reload preserve powers, payments and equipped Grandmaster");
            next.SetValue(null,(Func<int,int,int>)((low,high)=>low));rate.SetValue(null,(Func<int,int,bool>)((chance,total)=>false));
            dispatch(new Chi(true){Type=Chi.Study,Mode=Enums.ChiPowerType.Dragon});
            Check(!Fenced(p) && p.ChiPowers.Sum(power=>power.Points)<1600 && p.Entity.EquippedTitle==0
                && !OwnedTitles(p).ContainsKey(20180001) && p.ChiPoints==2550,
                "ordinary lower pursuit immediately removes and unequips ineligible Grandmaster");
            p=ReloadBall(p);ChiTable.Load(p);new TitleStorage().Login(p);
            Check(p.ChiPoints==2550 && !OwnedTitles(p).ContainsKey(20180001) && p.Entity.EquippedTitle==0,
                "lost Grandmaster eligibility stays removed after full reload");
        } finally {next.SetValue(null,priorNext);rate.SetValue(null,priorRate);Daily.GetField("Clock",Hidden).SetValue(null,priorClock);Environment.CurrentDirectory=priorDirectory;}
    }


    sealed class JiangOutcomeRandom : Random {
        readonly bool maximum;
        internal JiangOutcomeRandom(bool maximum) { this.maximum=maximum; }
        public override int Next(int min,int max) { return maximum && max>min?max-1:min; }
        public override int Next(int max) { return 0; }
    }
    static void VerifyTalentEarning(string runtime,Map town,string fixture) {
        Query("CREATE TABLE IF NOT EXISTS jianghu (LIKE public.jianghu INCLUDING ALL)");
        Revora.Database.JiangHu.LoadStatus();
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        var p=StoredProgressionPlayer(town);p.Entity.Level=140;p.Entity.Reborn=2;
        Check(EntityTable.SaveEntity(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==0,
            "ordinary qualified Talent candidate starts without CP funding");
        new TitleStorage().Login(p);
        Check(p.Entity.MyJiang==null && !OwnedTitles(p).ContainsKey(20040005),"Talent candidate starts without Jiang progression or title");
        var priorClock=Daily.GetField("Clock",Hidden).GetValue(null);string priorDirectory=Environment.CurrentDirectory;
        try {
            Directory.CreateDirectory(Path.Combine(fixture,"database"));Environment.CurrentDirectory=fixture;
            dailyNow=new DateTime(2032,3,1,0,0,0,DateTimeKind.Local).ToUniversalTime();
            Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));Call(Daily,null,"Create");
            DailyTick(DailySchedule().First());var boss=DailyActive();if(boss==null)throw new Exception("Talent funding boss absent");
            p.Entity.MapID=boss.MapID;p.Entity.X=boss.X;p.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
            Check(DailyActive()==null && !Fenced(p) && p.Entity.ConquerPoints==1000000
                && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID)==1,
                "scheduled boss combat earns durable ordinary CP funding for Jiang training");
            p.Entity.MapID=1002;p.Entity.X=327;p.Entity.Y=303;
            PacketHandler.HandlePacket(JiangCreationPacket("TalentPath"),p);
            Check(!Fenced(p) && p.Entity.MyJiang!=null && p.Entity.SubClasses.StudyPoints==100,"ordinary packet creates the Talent candidate's Jiang path");
            p.Entity.MyJiang.Random=new JiangOutcomeRandom(true);
            Action<byte,byte> train=(stage,star)=> {
                var bytes=new byte[28];Writer.WriteUInt16(20,0,bytes);Writer.WriteUInt16((ushort)PacketID.JiangHuStudy,2,bytes);
                bytes[9]=1;bytes[10]=star;bytes[11]=stage;Writer.WriteUInt64(PacketHandler.ClientSeal,20,bytes);PacketHandler.HandlePacket(bytes,p);
            };
            for(byte stage=1;stage<=9;stage++) {
                for(byte star=1;star<=9;star++)train(stage,star);
                Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength==stage*9000 && OwnedTitles(p).ContainsKey(20040005)==(stage==9),
                    "ordinary ordered training earns aligned stage and exact Talent eligibility: "+stage);
            }
            TitleRequest(20040005,TitleStorage.Action.Equip).Handle(p);
            Check(p.Entity.EquippedTitle==20040005 && p.Entity.ConquerPoints==971750 && p.Entity.SubClasses.StudyPoints==1720
                && p.Entity.MyJiang.RoundBuyPoints==49,"earned Talent equips after all 81 paid rolls with the capped price ladder");
            Check(EntityTable.SaveEntity(p),"earned Talent character saves normally");Revora.Database.JiangHu.SaveJiangHu(p);
            Revora.Interfaces.IJiangHu removed;Revora.Game.JiangHu.JiangHuClients.TryRemove(p.Entity.UID,out removed);Revora.Database.JiangHu.LoadJiangHu();
            p=ReloadBall(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength==81000 && p.Entity.ConquerPoints==971750
                && p.Entity.SubClasses.StudyPoints==1720 && p.Entity.EquippedTitle==20040005 && OwnedTitles(p)[20040005],
                "full character plus database Jiang reload preserve the earned score, payments and equipped Talent");
            p.Entity.MyJiang.Random=new JiangOutcomeRandom(false);train(1,1);
            Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength<81000 && !OwnedTitles(p).ContainsKey(20040005) && p.Entity.EquippedTitle==0,
                "ordinary lower training immediately removes and unequips ineligible Talent");
            var restore=new byte[16];Writer.WriteUInt16(8,0,restore);Writer.WriteUInt16((ushort)PacketID.JiangHu,2,restore);
            restore[4]=10;Writer.WriteUInt64(PacketHandler.ClientSeal,8,restore);PacketHandler.HandlePacket(restore,p);
            Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength==81000 && OwnedTitles(p).ContainsKey(20040005)
                && p.Entity.ConquerPoints==971230,"paid restore recovers exact Talent eligibility immediately with one 20 CP fee");
            PacketHandler.HandlePacket(restore,p);TitleRequest(20040005,TitleStorage.Action.Equip).Handle(p);
            Check(EntityTable.SaveEntity(p) && p.Entity.ConquerPoints==971230,"repeat restore cannot charge again and earned title re-equips");
            Revora.Database.JiangHu.SaveJiangHu(p);Revora.Game.JiangHu.JiangHuClients.TryRemove(p.Entity.UID,out removed);Revora.Database.JiangHu.LoadJiangHu();
            p=ReloadBall(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength==81000 && p.Entity.EquippedTitle==20040005
                && p.Entity.ConquerPoints==971230 && p.Entity.MyJiang.MyOldStar==null,"restored Talent and single payment survive fresh Jiang and character reload");
        }finally {Daily.GetField("Clock",Hidden).SetValue(null,priorClock);Environment.CurrentDirectory=priorDirectory;}
    }


    static byte[] PerfectionRequest(uint target,params uint[] materials) {return PerfectionOperationRequest(ItemRefineOperationProto.Action.Temper,target,materials);}
    static byte[] PerfectionOperationRequest(ItemRefineOperationProto.Action operation,uint target,params uint[] materials) {
        byte[] payload;using(var stream=new MemoryStream()) {ProtoBuf.Serializer.Serialize(stream,new ItemRefineOperationProto {
            Operation=operation,ItemUID=target,Str="",ItemArray=materials});payload=stream.ToArray();}
        var frame=new byte[payload.Length+12];Buffer.BlockCopy(payload,0,frame,4,payload.Length);
        Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(3251,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static byte[] SignatureRequest(uint target,string text) {
        byte[] payload;using(var stream=new MemoryStream()) {ProtoBuf.Serializer.Serialize(stream,new ItemRefineOperationProto {
            Operation=ItemRefineOperationProto.Action.Signature,ItemUID=target,Str=text,ItemArray=new uint[0]});payload=stream.ToArray();}
        var frame=new byte[payload.Length+12];Buffer.BlockCopy(payload,0,frame,4,payload.Length);
        Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(3251,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void VerifyPerfectionSignatures(Func<Tuple<GameState,ConquerItem>> player) {
        var pair=player();var p=pair.Item1;var item=pair.Item2;uint uid=item.UID;
        item.PerfectionLevel=18;item.PerfectionOwnerGuid=p.Entity.UID;item.PerfectionOwner=p.Entity.Name;p.Entity.ConquerPoints=1000;
        Query("UPDATE items SET perfectionlevel=18,perfectionownerguid=@owner,perfectionowner=@name WHERE uid=@uid","uid",(long)uid,"owner",(long)p.Entity.UID,"name",p.Entity.Name);
        PacketHandler.HandlePacket(SignatureRequest(uid,new string('A',32)),p);
        Check(!Fenced(p) && item.Signature==new string('A',32) && p.Entity.ConquerPoints==1000,
            "ordinary owner signs nine-Moon equipment for free within its 32-byte field");
        var shorter=SignatureRequest(uid,"Oak");Parallel.For(0,8,n=>PacketHandler.HandlePacket(shorter,p));
        Check(!Fenced(p) && item.Signature=="Oak" && p.Entity.ConquerPoints==730 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==730,
            "shorter signature clears old bytes and concurrent identical requests charge 270 CPs once");
        p=ReloadBall(p);item=p.Equipment.TryGetItem(uid);
        Check(!Fenced(p) && item.Signature=="Oak" && p.Entity.ConquerPoints==730,"signature and exact CP payment survive production item/character reload");
        foreach(string bad in new[]{"", "  ",new string('B',33),"bad\ntext","bad\0text"})PacketHandler.HandlePacket(SignatureRequest(uid,bad),p);
        Check(!Fenced(p) && item.Signature=="Oak" && p.Entity.ConquerPoints==730,"empty, oversized and control-character signatures cannot charge or erase existing text");
        foreach(uint level in new[]{17u,55u}){
            item.PerfectionLevel=level;Query("UPDATE items SET perfectionlevel=@level WHERE uid=@uid","level",(long)level,"uid",(long)uid);
            PacketHandler.HandlePacket(SignatureRequest(uid,"Denied"),p);
            Check(item.Signature=="Oak" && p.Entity.ConquerPoints==730,"signature rejects ineligible perfection level "+level);
        }
        item.PerfectionLevel=18;item.PerfectionOwnerGuid=p.Entity.UID+1;
        Query("UPDATE items SET perfectionlevel=18,perfectionownerguid=@owner WHERE uid=@uid","owner",(long)item.PerfectionOwnerGuid,"uid",(long)uid);
        PacketHandler.HandlePacket(SignatureRequest(uid,"Other owner"),p);
        Check(!Fenced(p) && item.Signature=="Oak" && p.Entity.ConquerPoints==730,"held equipment with another perfection owner cannot be signed");
        item.PerfectionOwnerGuid=p.Entity.UID;Query("UPDATE items SET perfectionownerguid=@owner WHERE uid=@uid","owner",(long)p.Entity.UID,"uid",(long)uid);
        Query("ALTER TABLE items ADD CONSTRAINT signature_fixture_failure CHECK(signature IS DISTINCT FROM 'Blocked') NOT VALID");
        try{PacketHandler.HandlePacket(SignatureRequest(uid,"Blocked"),p);}finally{Query("ALTER TABLE items DROP CONSTRAINT signature_fixture_failure");}
        Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==730
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND signature='Oak'",uid)==1,"failed signature write rolls back its CP fee");
        p=ReloadBall(p);item=p.Equipment.TryGetItem(uid);
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.PerfectionTempering");var hook=service.GetField("BeforeInstall",Hidden);
        hook.SetValue(null,(Action<GameState>)(actor=>{throw new InvalidOperationException("signature install fixture");}));
        try{PacketHandler.HandlePacket(SignatureRequest(uid,"Recovered"),p);}finally{hook.SetValue(null,null);}
        Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==460
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND signature='Recovered'",uid)==1,"post-commit signature failure retains both saved text and payment");
        p=ReloadBall(p);item=p.Equipment.TryGetItem(uid);PacketHandler.HandlePacket(SignatureRequest(uid,"Recovered"),p);
        Check(!Fenced(p) && item.Signature=="Recovered" && p.Entity.ConquerPoints==460,"reconnect installs the committed signature and replay cannot charge again");
        Query("UPDATE items SET signature='Saved elsewhere' WHERE uid=@uid","uid",(long)uid);
        PacketHandler.HandlePacket(SignatureRequest(uid,"Stale overwrite"),p);
        Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==460
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND signature='Saved elsewhere'",uid)==1,"stale signature preimage cannot overwrite saved text or charge");
    }
    static byte[] BoostRequest(uint target,params uint[] amounts) {
        // Native builder omits string field3; use the production decoder on that shape.
        byte[] payload;using(var stream=new MemoryStream()){ProtoBuf.Serializer.Serialize(stream,new ItemRefineOperationProto{
            Operation=ItemRefineOperationProto.Action.CpBoost,ItemUID=target,ItemArray=amounts});payload=stream.ToArray();}
        var frame=new byte[payload.Length+12];Buffer.BlockCopy(payload,0,frame,4,payload.Length);
        Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(3251,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void VerifyPerfectionBoost(Func<Tuple<GameState,ConquerItem>> player) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.PerfectionTempering");var clock=service.GetField("Clock",Hidden);var original=clock.GetValue(null);
        var now=new DateTime(2033,1,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        Func<bool,Tuple<GameState,ConquerItem>> prepared=bound=>{var pair=player();pair.Item1.Entity.ConquerPoints=1000;pair.Item1.Entity.BoundCps=10;
            pair.Item2.Bound=bound;Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)pair.Item2.UID);return pair;};
        try{
            foreach(bool bound in new[]{false,true}){
                var pair=prepared(bound);var p=pair.Item1;var item=pair.Item2;uint uid=item.UID,rvp=p.Entity.RevoraPoint;
                var request=BoostRequest(uid,24);var decoded=new CMsgItemRefineOpt(request);
                Check(decoded.Read() && decoded.Data.Str==null && decoded.Data.ItemArray.SequenceEqual(new[]{24u}),"native-shaped Boost omits signature string and decodes confirmed CP spend");
                Parallel.For(0,8,n=>PacketHandler.HandlePacket(request,p));
                Check(!Fenced(p) && item.PerfectionLevel==1 && item.PerfectionProgress==0 && item.PerfectionOwnerGuid==p.Entity.UID
                    && p.Entity.ConquerPoints==(bound?986u:976u) && p.Entity.BoundCps==(bound?0u:10u) && p.Entity.RevoraPoint==rvp,
                    "ordinary player Boost buys 30 progress, assigns owner and charges eligible wallets once: "+bound);
                p=ReloadBall(p);item=p.Equipment.TryGetItem(uid);PacketHandler.HandlePacket(request,p);
                Check(!Fenced(p) && item.PerfectionLevel==1 && item.PerfectionProgress==0 && p.Entity.ConquerPoints==(bound?986u:976u)
                    && Scalar("SELECT count(*) FROM revora_perfection_boosts WHERE uid=@uid",p.Entity.UID)==1,"Boost receipt suppresses immediate replay across item/character reload");
                now=now.AddSeconds(2);PacketHandler.HandlePacket(BoostRequest(uid,8),p);
                Check(!Fenced(p) && item.PerfectionLevel==1 && item.PerfectionProgress==10 && p.Entity.ConquerPoints==(bound?978u:968u),"new Boost after cooldown purchases exact additional progress");
                now=now.AddSeconds(2);
                foreach(var amounts in new[]{new uint[0],new[]{0u},new[]{7u},new[]{8u,8u},new[]{uint.MaxValue-7}})PacketHandler.HandlePacket(BoostRequest(uid,amounts),p);
                Check(item.PerfectionProgress==10 && p.Entity.ConquerPoints==(bound?978u:968u),"invalid counts, non-bundle and excessive amounts cannot charge");
            }
            var capped=prepared(false);capped.Item2.PerfectionLevel=53;capped.Item2.PerfectionProgress=11995;
            Query("UPDATE items SET perfectionlevel=53,perfectionprogress=11995 WHERE uid=@uid","uid",(long)capped.Item2.UID);
            PacketHandler.HandlePacket(BoostRequest(capped.Item2.UID,16),capped.Item1);
            Check(capped.Item1.Entity.ConquerPoints==1000 && capped.Item2.PerfectionLevel==53,"Boost rejects payment above smallest whole bundle to cap");
            PacketHandler.HandlePacket(BoostRequest(capped.Item2.UID,8),capped.Item1);
            Check(!Fenced(capped.Item1) && capped.Item1.Entity.ConquerPoints==992 && capped.Item2.PerfectionLevel==54 && capped.Item2.PerfectionProgress==0,"last Boost bundle caps level54 and clears surplus progress");
            now=now.AddSeconds(2);PacketHandler.HandlePacket(BoostRequest(capped.Item2.UID,8),capped.Item1);
            Check(capped.Item1.Entity.ConquerPoints==992,"max-level Boost cannot consume more CPs");
            var poor=prepared(false);poor.Item1.Entity.ConquerPoints=0;poor.Item1.Entity.BoundCps=1000;
            PacketHandler.HandlePacket(BoostRequest(poor.Item2.UID,8),poor.Item1);
            Check(poor.Item2.PerfectionLevel==0 && poor.Item2.PerfectionProgress==0 && poor.Item1.Entity.BoundCps==1000,"bound CPs cannot boost unbound equipment");
            poor.Item2.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)poor.Item2.UID);
            PacketHandler.HandlePacket(BoostRequest(poor.Item2.UID,8),poor.Item1);
            Check(!Fenced(poor.Item1) && poor.Item2.PerfectionProgress==10 && poor.Item1.Entity.BoundCps==992,"bound equipment can Boost from bound CPs alone");
            foreach(string stale in new[]{"wallet","item","owner","binding"}){
                var pair=prepared(true);var p=pair.Item1;var item=pair.Item2;
                if(stale=="wallet")Query("UPDATE entities SET boundcps=9 WHERE uid=@uid","uid",(long)p.Entity.UID);
                if(stale=="item")Query("UPDATE items SET perfectionprogress=1 WHERE uid=@uid","uid",(long)item.UID);
                if(stale=="owner")Query("UPDATE items SET perfectionownerguid=123 WHERE uid=@uid","uid",(long)item.UID);
                if(stale=="binding")Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)item.UID);
                PacketHandler.HandlePacket(BoostRequest(item.UID,24),p);
                Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==1000
                    && Scalar("SELECT count(*) FROM revora_perfection_boosts WHERE uid=@uid",p.Entity.UID)==0,"stale Boost preimage rejects and rolls back: "+stale);
            }
            foreach(string stage in new[]{"BeforeCommit","BeforeInstall"}){
                var pair=prepared(true);var p=pair.Item1;uint uid=pair.Item2.UID;var hook=service.GetField(stage,Hidden);
                hook.SetValue(null,(Action<GameState>)(actor=>{throw new InvalidOperationException("boost fixture "+stage);}));
                try{PacketHandler.HandlePacket(BoostRequest(uid,24),p);}finally{hook.SetValue(null,null);}
                bool committed=stage=="BeforeInstall";
                Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==(committed?986:1000)
                    && Scalar("SELECT COALESCE(boundcps,0) FROM entities WHERE uid=@uid",p.Entity.UID)==(committed?0:10)
                    && Scalar("SELECT COALESCE(perfectionlevel,0) FROM items WHERE uid=@uid",uid)==(committed?1:0),"Boost payment and progress share transaction boundary: "+stage);
                p=ReloadBall(p);var item=p.Equipment.TryGetItem(uid);
                Check(!Fenced(p) && item.PerfectionLevel==(committed?1u:0u) && p.Entity.ConquerPoints==(committed?986u:1000u),"Boost payment/progress recover on reload: "+stage);
                if(committed){PacketHandler.HandlePacket(BoostRequest(uid,24),p);Check(p.Entity.ConquerPoints==986 && item.PerfectionLevel==1,"post-commit failure replay does not debit twice");}
            }
            var foreign=prepared(false);foreign.Item2.PerfectionOwnerGuid=123;
            PacketHandler.HandlePacket(BoostRequest(foreign.Item2.UID,24),foreign.Item1);
            Check(foreign.Item1.Entity.ConquerPoints==1000 && foreign.Item2.PerfectionLevel==0,"foreign perfection owner rejected before payment");
        }finally{clock.SetValue(null,original);}
    }
    static void VerifyPerfectionQuicken(Func<Tuple<GameState,ConquerItem>> player) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.PerfectionTempering");var rng=service.GetField("QuickenChance",Hidden);var original=rng.GetValue(null);
        Func<Tuple<GameState,ConquerItem>> prepared=()=>{var pair=player();var p=pair.Item1;var item=pair.Item2;
            item.PerfectionLevel=10;item.PerfectionProgress=2500;item.PerfectionOwnerGuid=p.Entity.UID;item.PerfectionOwner=p.Entity.Name;
            Query("UPDATE items SET perfectionlevel=10,perfectionprogress=2500,perfectionownerguid=@owner,perfectionowner=@name WHERE uid=@uid",
                "owner",(long)p.Entity.UID,"name",p.Entity.Name,"uid",(long)item.UID);return pair;};
        try{
            foreach(bool success in new[]{false,true}){
                int rolls=0;double chance=-1;rng.SetValue(null,(Func<double,bool>)(percent=>{rolls++;chance=percent;return success;}));
                var pair=prepared();var p=pair.Item1;var item=pair.Item2;uint cp=p.Entity.ConquerPoints;
                var request=PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,item.UID);
                Parallel.For(0,8,n=>PacketHandler.HandlePacket(request,p));
                Check(!Fenced(p) && rolls==1 && chance==50 && item.PerfectionLevel==(success?11u:10u) && item.PerfectionProgress==0 && p.Entity.ConquerPoints==cp,
                    "quicken rolls once at progress/cost chance and consumes progress atomically: "+success);
                p=ReloadBall(p);item=p.Equipment.TryGetItem(item.UID);PacketHandler.HandlePacket(request,p);
                Check(!Fenced(p) && rolls==1 && item.PerfectionLevel==(success?11u:10u) && item.PerfectionProgress==0,
                    "quicken result survives reload and replay has no remaining attempt: "+success);
            }
            int rejectedRolls=0;rng.SetValue(null,(Func<double,bool>)(percent=>{rejectedRolls++;return true;}));
            var blocked=prepared();blocked.Item2.PerfectionLevel=54;PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,blocked.Item2.UID),blocked.Item1);
            Check(rejectedRolls==0 && blocked.Item2.PerfectionLevel==54 && blocked.Item2.PerfectionProgress==2500,"maxed perfection cannot quicken past level54");
            blocked.Item2.PerfectionLevel=9;
            PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,blocked.Item2.UID),blocked.Item1);
            Check(rejectedRolls==0 && blocked.Item2.PerfectionLevel==9 && blocked.Item2.PerfectionProgress==2500,"client-declared level10 Quicken minimum rejects level9 without consuming progress");
            blocked.Item2.PerfectionLevel=10;blocked.Item2.PerfectionOwnerGuid++;
            PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,blocked.Item2.UID),blocked.Item1);
            Check(rejectedRolls==0 && blocked.Item2.PerfectionProgress==2500,"another perfection owner's progress cannot be consumed");
            var stale=prepared();Query("UPDATE items SET perfectionprogress=2499 WHERE uid=@uid","uid",(long)stale.Item2.UID);
            PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,stale.Item2.UID),stale.Item1);
            Check(Fenced(stale.Item1) && rejectedRolls==0 && Scalar("SELECT perfectionprogress FROM items WHERE uid=@uid",stale.Item2.UID)==2499,
                "stale saved progress is rejected before random selection");
            foreach(string stage in new[]{"BeforeCommit","BeforeInstall"}){
                var pair=prepared();var p=pair.Item1;var item=pair.Item2;uint uid=item.UID;var hook=service.GetField(stage,Hidden);
                hook.SetValue(null,(Action<GameState>)(actor=>{throw new InvalidOperationException("quicken fixture "+stage);}));
                try{PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Quicken,uid),p);}finally{hook.SetValue(null,null);}
                bool committed=stage=="BeforeInstall";
                Check(Fenced(p) && Scalar("SELECT perfectionprogress FROM items WHERE uid=@uid",uid)==(committed?0:2500)
                    && Scalar("SELECT COALESCE(perfectionlevel,0) FROM items WHERE uid=@uid",uid)==(committed?11:10),"quicken failure preserves the correct transaction boundary: "+stage);
                p=ReloadBall(p);item=p.Equipment.TryGetItem(uid);
                Check(!Fenced(p) && item.PerfectionLevel==(committed?11u:10u) && item.PerfectionProgress==(committed?0u:2500u),
                    "quicken failure recovers its saved level/progress on reconnect: "+stage);
            }
        }finally{rng.SetValue(null,original);}
    }
    static void VerifyPerfectionTempering(Map town) {
        Func<Tuple<GameState,ConquerItem>> player=()=>{
            var actor=StoredProgressionPlayer(town);var target=Pack(actor,130009,1);
            actor.Inventory.Remove(target.UID,Enums.ItemUse.None,true);target.Position=3;actor.Equipment.Objects[2]=target;
            Query("UPDATE items SET position=3 WHERE uid=@uid","uid",(long)target.UID);return Tuple.Create(actor,target);
        };
        var perfectionService=typeof(Entity).Assembly.GetType("Revora.Game.Features.PerfectionTempering");Call(perfectionService,null,"Initialize");
        VerifyPerfectionSignatures(player);
        VerifyPerfectionQuicken(player);
        VerifyPerfectionBoost(player);
        var oldExchangeClock=perfectionService.GetField("Clock",Hidden).GetValue(null);
        var exchangeNow=new DateTime(2032,1,1,0,0,0,DateTimeKind.Utc);
        perfectionService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>exchangeNow));
        try{foreach(bool fail in new[]{false,true}){
            var ep=player();var actor=ep.Item1;var first=ep.Item2;var second=Pack(actor,131009,1);actor.Entity.RevoraPoint=300000;
            first.PerfectionLevel=7;first.PerfectionProgress=900;first.PerfectionOwnerGuid=actor.Entity.UID;first.PerfectionOwner=actor.Entity.Name;
            Query("UPDATE items SET perfectionlevel=7,perfectionprogress=900,perfectionownerguid=@owner,perfectionowner=@name WHERE uid=@uid",
                "owner",(long)actor.Entity.UID,"name",actor.Entity.Name,"uid",(long)first.UID);
            var request=PerfectionOperationRequest(ItemRefineOperationProto.Action.Exchange,first.UID,second.UID);
            PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Exchange,first.UID),actor);
            PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Exchange,first.UID,first.UID),actor);
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==300000 && first.PerfectionLevel==7,"empty and self exchange cannot charge or erase perfection");
            var wrongType=Pack(actor,410009,1);PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Exchange,first.UID,wrongType.UID),actor);
            second.Bound=true;PacketHandler.HandlePacket(request,actor);second.Bound=false;
            second.PerfectionOwnerGuid=actor.Entity.UID+1;PacketHandler.HandlePacket(request,actor);second.PerfectionOwnerGuid=0;
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==300000 && first.PerfectionLevel==7,"wrong type, mismatched binding and foreign perfection ownership refuse exchange without payment");
            if(fail)Query("CREATE FUNCTION suppress_exchange() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_exchange BEFORE INSERT ON revora_perfection_exchanges FOR EACH ROW EXECUTE FUNCTION suppress_exchange()");
            try{Parallel.For(0,12,n=>PacketHandler.HandlePacket(request,actor));}
            finally{if(fail)Query("DROP TRIGGER suppress_exchange ON revora_perfection_exchanges;DROP FUNCTION suppress_exchange()");}
            Check(fail?Fenced(actor) && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",actor.Entity.UID)==300000
                && Scalar("SELECT perfectionlevel FROM items WHERE uid=@uid",first.UID)==7
                :!Fenced(actor) && actor.Entity.RevoraPoint==200000 && first.PerfectionLevel==0 && second.PerfectionLevel==7 && second.PerfectionProgress==900,
                "cross-class armor exchange swaps progress once or rolls back fee and both items: failure="+fail);
            actor=ReloadBall(actor);if(fail)PacketHandler.HandlePacket(request,actor);
            PacketHandler.HandlePacket(request,actor);PacketHandler.HandlePacket(PerfectionOperationRequest(ItemRefineOperationProto.Action.Exchange,second.UID,first.UID),actor);
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==200000 && actor.Inventory.Objects.Single(i=>i.UID==second.UID).PerfectionLevel==7
                && Scalar("SELECT count(*) FROM revora_perfection_exchanges WHERE uid=@uid",actor.Entity.UID)==1,
                "saved unordered-pair guard prevents immediate exchange replay after reload");
            exchangeNow=exchangeNow.AddSeconds(2);PacketHandler.HandlePacket(request,actor);
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==100000 && actor.Equipment.TryGetItem(first.UID).PerfectionLevel==7
                && actor.Inventory.Objects.Single(i=>i.UID==second.UID).PerfectionLevel==0
                && Scalar("SELECT count(*) FROM revora_perfection_exchanges WHERE uid=@uid",actor.Entity.UID)==2,
                "a later deliberate exchange can reverse the swap for one new fee");
        }}finally{perfectionService.GetField("Clock",Hidden).SetValue(null,oldExchangeClock);}
        foreach(bool fail in new[]{false,true}){
            var transferPair=player();var actor=transferPair.Item1;var gear=transferPair.Item2;actor.Entity.RevoraPoint=200000;
            gear.PerfectionLevel=7;gear.PerfectionProgress=900;gear.PerfectionOwnerGuid=actor.Entity.UID+1;gear.PerfectionOwner=actor.Entity.Name;
            Query("UPDATE items SET perfectionlevel=7,perfectionprogress=900,perfectionownerguid=@owner,perfectionowner=@name WHERE uid=@uid",
                "owner",(long)gear.PerfectionOwnerGuid,"name",gear.PerfectionOwner,"uid",(long)gear.UID);
            var request=PerfectionOperationRequest(ItemRefineOperationProto.Action.TransferOwnership,gear.UID);
            actor.Trade.InTrade=true;PacketHandler.HandlePacket(request,actor);actor.Trade.InTrade=false;
            Check(actor.Entity.RevoraPoint==200000 && gear.PerfectionOwnerGuid!=actor.Entity.UID,"active trade cannot pay for ownership transfer");
            actor.Entity.RevoraPoint=99999;PacketHandler.HandlePacket(request,actor);
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==99999 && gear.PerfectionOwnerGuid!=actor.Entity.UID,"insufficient ownership fee preserves money and item");
            actor.Entity.RevoraPoint=200000;
            if(fail)Query("CREATE FUNCTION suppress_transfer() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_transfer BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_transfer()");
            try{Parallel.For(0,12,n=>PacketHandler.HandlePacket(request,actor));}
            finally{if(fail)Query("DROP TRIGGER suppress_transfer ON items;DROP FUNCTION suppress_transfer()");}
            Check(fail?Fenced(actor) && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",actor.Entity.UID)==200000
                :!Fenced(actor) && actor.Entity.RevoraPoint==100000 && gear.PerfectionOwnerGuid==actor.Entity.UID,
                "ownership transfer commits one payment or rolls both records back: failure="+fail);
            actor=ReloadBall(actor);if(fail)PacketHandler.HandlePacket(request,actor);
            gear=actor.Equipment.TryGetItem(gear.UID);PacketHandler.HandlePacket(request,actor);
            Check(!Fenced(actor) && actor.Entity.RevoraPoint==100000 && gear.PerfectionOwnerGuid==actor.Entity.UID && gear.PerfectionLevel==7 && gear.PerfectionProgress==900,
                "ownership transfer reload/retry preserves perfection and rejects a duplicate charge: failure="+fail);
        }
        var pair=player();var p=pair.Item1;var item=pair.Item2;var stone=Pack(p,3009003,1);var frame=PerfectionRequest(item.UID,stone.UID);
        PacketHandler.HandlePacket(frame,p);
        Check(!Fenced(p) && item.PerfectionLevel==10 && item.PerfectionProgress==360 && !p.Inventory.ContainsUID(stone.UID)
            && Scalar("SELECT perfectionlevel FROM items WHERE uid=@uid",item.UID)==10,
            "ordinary decoded tempering consumes one owned Splendid stone and saves exact level/progress");
        PacketHandler.HandlePacket(frame,p);
        Check(item.PerfectionProgress==360 && item.PerfectionLevel==10,"replayed consumed-material tempering cannot grant progress twice");
        var bad=Pack(p,1088001,1);PacketHandler.HandlePacket(PerfectionRequest(item.UID,bad.UID),p);
        Check(!Fenced(p) && p.Inventory.ContainsUID(bad.UID) && item.PerfectionProgress==360,"non-star material is rejected without being destroyed");
        var duplicate=Pack(p,3009000,1);PacketHandler.HandlePacket(PerfectionRequest(item.UID,duplicate.UID,duplicate.UID),p);
        Check(p.Inventory.ContainsUID(duplicate.UID) && item.PerfectionProgress==360,"duplicate material UIDs cannot multiply perfection credit");
        duplicate.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)duplicate.UID);PacketHandler.HandlePacket(PerfectionRequest(item.UID,duplicate.UID),p);
        Check(p.Inventory.ContainsUID(duplicate.UID) && item.PerfectionProgress==360,"bound stone cannot improve an unbound target");
        uint[] plusLevels={0,1,2,3,5,7,10,13},plusRemainders={10,10,30,170,340,900,80,20};
        for(byte plus=1;plus<=8;plus++) {
            var plusPair=player();var plusPlayer=plusPair.Item1;var plusTarget=plusPair.Item2;
            var plusStone=Pack(plusPlayer,(uint)(730000+plus),1);plusStone.Plus=plus;
            Query("UPDATE items SET plus=@plus WHERE uid=@uid","plus",(int)plus,"uid",(long)plusStone.UID);
            PacketHandler.HandlePacket(PerfectionRequest(plusTarget.UID,plusStone.UID),plusPlayer);
            Check(!Fenced(plusPlayer) && plusTarget.PerfectionLevel==plusLevels[plus-1] && plusTarget.PerfectionProgress==plusRemainders[plus-1]
                && !plusPlayer.Inventory.ContainsUID(plusStone.UID),"plus-stone composition points produce exact perfection level and remainder: +"+plus);
        }
        foreach(byte plus in new byte[]{0,6,8}) {
            var gearPair=player();var actor=gearPair.Item1;var target=gearPair.Item2;var minor=Pack(actor,130009,1);minor.Plus=plus;
            Query("UPDATE items SET plus=@plus WHERE uid=@uid","plus",(int)plus,"uid",(long)minor.UID);
            var request=PerfectionRequest(target.UID,minor.UID);PacketHandler.HandlePacket(request,actor);
            uint expectedLevel=plus==0?0u:plus==6?7u:13u,expectedProgress=plus==0?1u:plus==6?900u:20u;
            Check(!Fenced(actor) && target.PerfectionLevel==expectedLevel && target.PerfectionProgress==expectedProgress && !actor.Inventory.ContainsUID(minor.UID),
                "same-family equipment composition points temper exactly: +"+plus);
            PacketHandler.HandlePacket(request,actor);actor=ReloadBall(actor);var saved=actor.Equipment.TryGetItem(target.UID);
            Check(saved!=null && saved.PerfectionLevel==expectedLevel && saved.PerfectionProgress==expectedProgress && !actor.Inventory.ContainsUID(minor.UID),
                "equipment minor consumption survives replay and character reload: +"+plus);
        }
        var effectPair=player();var effectActor=effectPair.Item1;var effectTarget=effectPair.Item2;var effectMinor=Pack(effectActor,130009,1);effectMinor.Plus=6;
        Query("UPDATE items SET plus=6 WHERE uid=@uid","uid",(long)effectMinor.UID);
        Query("INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,duration,addedon) VALUES(@uid,0,800014,1,0,@time)","uid",(long)effectMinor.UID,"time",DateTime.UtcNow.ToBinary());
        Query("CREATE FUNCTION suppress_temper_minor() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_temper_minor BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_temper_minor()");
        try{PacketHandler.HandlePacket(PerfectionRequest(effectTarget.UID,effectMinor.UID),effectActor);}
        finally{Query("DROP TRIGGER suppress_temper_minor ON items;DROP FUNCTION suppress_temper_minor()");}
        Check(Fenced(effectActor) && Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid",effectMinor.UID)==1
            && Scalar("SELECT COALESCE(perfectionlevel,0) FROM items WHERE uid=@uid",effectTarget.UID)==0,
            "failed equipment deletion rolls back target progress and attached effects together");
        effectActor=ReloadBall(effectActor);PacketHandler.HandlePacket(PerfectionRequest(effectTarget.UID,effectMinor.UID),effectActor);
        Check(!Fenced(effectActor) && Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid",effectMinor.UID)==0
            && !effectActor.Inventory.ContainsUID(effectMinor.UID) && effectActor.Equipment.TryGetItem(effectTarget.UID).PerfectionLevel==7,
            "successful equipment consumption removes its attached effect rows without orphans");
        var foreignGear=player();var wrong=Pack(foreignGear.Item1,410009,1);wrong.Plus=6;
        Query("UPDATE items SET plus=6 WHERE uid=@uid","uid",(long)wrong.UID);
        PacketHandler.HandlePacket(PerfectionRequest(foreignGear.Item2.UID,wrong.UID),foreignGear.Item1);
        Check(!Fenced(foreignGear.Item1) && foreignGear.Item2.PerfectionLevel==0 && foreignGear.Item1.Inventory.ContainsUID(wrong.UID),
            "different equipment family is not consumed for tempering");
        var mismatchPair=player();var mismatch=mismatchPair.Item1;var mismatchItem=mismatchPair.Item2;var mismatchStone=Pack(mismatch,730006,1);
        mismatchStone.Plus=5;PacketHandler.HandlePacket(PerfectionRequest(mismatchItem.UID,mismatchStone.UID),mismatch);
        Check(!Fenced(mismatch) && mismatchItem.PerfectionLevel==0 && mismatch.Inventory.ContainsUID(mismatchStone.UID),
            "plus-stone ID and plus disagreement cannot grant or destroy a material");
        mismatchStone.Plus=6;PacketHandler.HandlePacket(PerfectionRequest(mismatchItem.UID,mismatchStone.UID),mismatch);
        Check(Fenced(mismatch) && mismatchItem.PerfectionLevel==0 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",mismatchStone.UID)==1,
            "changed durable stone plus rejects the entire temper transaction");
        pair=player();var capped=pair.Item1;var cappedItem=pair.Item2;
        foreach(int amount in new[]{40,12}) {
            var stones=Enumerable.Range(0,amount).Select(n=>Pack(capped,3009003,1).UID).ToArray();
            PacketHandler.HandlePacket(PerfectionRequest(cappedItem.UID,stones),capped);
        }
        Check(!Fenced(capped) && cappedItem.PerfectionLevel==54 && cappedItem.PerfectionProgress==0
            && Scalar("SELECT perfectionlevel FROM items WHERE uid=@uid",cappedItem.UID)==54,
            "large material batches stop at level 54 and clear excess progress without overflow");
        var excess=Pack(capped,3009003,1);PacketHandler.HandlePacket(PerfectionRequest(cappedItem.UID,excess.UID),capped);
        Check(capped.Inventory.ContainsUID(excess.UID) && cappedItem.PerfectionLevel==54,"maxed perfection consumes no additional stone");
        pair=player();var rollback=pair.Item1;var rollbackItem=pair.Item2;var kept=Pack(rollback,3009003,1);
        Query("ALTER TABLE items ADD CONSTRAINT perfection_fixture_failure CHECK(perfectionlevel=0) NOT VALID");
        try {PacketHandler.HandlePacket(PerfectionRequest(rollbackItem.UID,kept.UID),rollback);}finally{Query("ALTER TABLE items DROP CONSTRAINT perfection_fixture_failure");}
        Check(Fenced(rollback) && rollbackItem.PerfectionLevel==0 && rollback.Inventory.ContainsUID(kept.UID)
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",kept.UID)==1,"failed target write rolls back stone consumption and fences stale state");
        pair=player();var concurrent=pair.Item1;var concurrentItem=pair.Item2;var shared=Pack(concurrent,3009003,1);var same=PerfectionRequest(concurrentItem.UID,shared.UID);
        Parallel.For(0,16,n=>PacketHandler.HandlePacket(same,concurrent));
        Check(!Fenced(concurrent) && concurrentItem.PerfectionLevel==10 && concurrentItem.PerfectionProgress==360
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",shared.UID)==0,"concurrent identical tempering consumes and credits the stone once");
        pair=player();var committed=pair.Item1;var committedItem=pair.Item2;var spent=Pack(committed,3009003,1);
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.PerfectionTempering");var hook=service.GetField("BeforeInstall",Hidden);
        hook.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture perfection install");}));
        try {PacketHandler.HandlePacket(PerfectionRequest(committedItem.UID,spent.UID),committed);}finally {hook.SetValue(null,null);}
        Check(Fenced(committed) && committedItem.PerfectionLevel==0 && Scalar("SELECT perfectionlevel FROM items WHERE uid=@uid",committedItem.UID)==10
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",spent.UID)==0,"post-commit tempering failure preserves durable progress and consumption");
        committed=ReloadBall(committed);var loaded=committed.Equipment.TryGetItem(committedItem.UID);
        Check(!Fenced(committed) && loaded!=null && loaded.PerfectionLevel==10 && loaded.PerfectionProgress==360 && !committed.Inventory.ContainsUID(spent.UID),
            "full character reload recovers committed tempering without returning its consumed stone");
    }

    static void VerifyPerfectionMaterialAcquisition(string runtime,Map town,string fixture) {
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        GameState p=null;
        VerifyFamilyFeudQuest(runtime,town,actor=>{if(actor.Entity.Reborn==1)p=actor;},actor=>{
            // The career starts with supplied first-rebirth eligibility. Include
            // valid legacy history before any earning; never forge a second rite.
            if(actor.Entity.Reborn==1){actor.Entity.FirstRebornClass=15;actor.Entity.FirstRebornLevel=130;
                if(!EntityTable.SaveEntity(actor))throw new Exception("Initial career history save failed.");}
        });
        if(p==null)throw new Exception("Earned Family Feud character missing.");
        var reward=p.Inventory.Objects.Single(i=>i.ID==728248);ItemHandler.UseItem(reward,p);
        var target=p.Inventory.Objects.Single(i=>i.ID==120189);
        ItemHandler.EquipItem(new ItemUsage(true){UID=target.UID,dwParam=2,Action=ItemUsage.Mode.EquipItem},p);
        Check(!Fenced(p) && p.Equipment.TryGetItem(target.UID)==target && target.Bound && target.ID%10==9
            && !p.Inventory.ContainsUID(reward.UID),"ordinary Family Feud completion, pack opening and equip earn the actual Super tempering target");
        uint careerUid=p.Entity.UID,necklaceUid=target.UID;var storedRewards=p.Inventory.Objects.Select(i=>i.UID).ToArray();
        p.Entity.Teleport(1002,317,322);
        foreach(uint rewardUid in storedRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
        Check(!Fenced(p) && p.Inventory.Count==0 && storedRewards.All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),"equipment journey banks earned Family Feud rewards without deleting them");
        p=ReloadBall(p);
        VerifyDesertExpeditionQuest(runtime,town,p,actor=>{if(actor.Entity.UID==careerUid)p=actor;});
        target=p.Equipment.TryGetItem(necklaceUid);var ring=p.Inventory.Objects.Single(i=>i.ID==150219);
        ItemHandler.EquipItem(new ItemUsage(true){UID=ring.UID,dwParam=6,Action=ItemUsage.Mode.EquipItem},p);
        Check(!Fenced(p) && p.Entity.UID==careerUid && target!=null && p.Equipment.TryGetItem(ring.UID)==ring && ring.Bound && ring.ID%10==9
            && storedRewards.All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),"same ordinary character earns and equips Desert Expedition Super ring while preserving necklace and stored rewards");
        Check(EntityTable.SaveEntity(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.RevoraPoint==0
            && target.PerfectionLevel==0 && !p.Inventory.Objects.Any(i=>i.ID>=3009000 && i.ID<=3009003),"earned-target character starts tempering without supplied stones or RVPs");
        var priorClock=Daily.GetField("Clock",Hidden).GetValue(null);string priorDirectory=Environment.CurrentDirectory;
        try {
            Directory.CreateDirectory(Path.Combine(fixture,"database"));Environment.CurrentDirectory=fixture;
            dailyNow=new DateTime(2032,4,1,0,0,0,DateTimeKind.Local).ToUniversalTime();
            Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));Call(Daily,null,"Create");
            DailyTick(DailySchedule().First());var boss=DailyActive();if(boss==null)throw new Exception("Perfection funding boss absent");
            p.Entity.MapID=boss.MapID;p.Entity.X=boss.X;p.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
            Check(DailyActive()==null && !Fenced(p) && p.Entity.RevoraPoint==50000
                && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID)==1,
                "actual scheduled boss combat funds star-stone purchases with a saved 50000 RVP bounty");
            p.Entity.MapID=1002;p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=Revora.Game.Features.RevoraPointShopSystem.NpcId;
            town.AddNpc(new NpcSpawn{UID=p.ActiveNpc,MapID=1002,X=396,Y=340,Mesh=50110,Name="Vesper",Type=Enums.NpcType.Talker});
            var stones=new[]{BuyRouteRune(p,11),BuyRouteRune(p,12),BuyRouteRune(p,13)};
            Check(stones.Select(i=>i.ID).SequenceEqual(new uint[]{3009001,3009002,3009003}) && stones.All(i=>i.StackSize==1 && !i.Bound)
                && p.Entity.RevoraPoint==39600 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==39600,
                "ordinary Vesper dialogue buys usable Bright, Radiant and Splendid stones for exactly 10400 earned RVPs");
            uint targetUid=target.UID;var ids=stones.Select(i=>i.UID).ToArray();var packet=PerfectionRequest(targetUid,ids);
            PacketHandler.HandlePacket(packet,p);
            Check(!Fenced(p) && target.PerfectionLevel==10 && target.PerfectionProgress==1460 && target.PerfectionOwnerGuid==p.Entity.UID
                && ids.All(id=>!p.Inventory.ContainsUID(id)) && p.Entity.RevoraPoint==39600,
                "actual purchased stones grant 11100 perfection points without a second currency charge");
            PacketHandler.HandlePacket(packet,p);
            Check(target.PerfectionLevel==10 && target.PerfectionProgress==1460 && p.Entity.RevoraPoint==39600,
                "replaying purchased stone UIDs cannot repeat progress or debit earned RVPs");
            Check(EntityTable.SaveEntity(p),"ordinary save succeeds after earned material purchases and tempering");
            p=ReloadBall(p);target=p.Equipment.TryGetItem(targetUid);
            Check(!Fenced(p) && target!=null && target.PerfectionLevel==10 && target.PerfectionProgress==1460
                && target.PerfectionOwnerGuid==p.Entity.UID && p.Entity.RevoraPoint==39600 && ids.All(id=>!p.Inventory.ContainsUID(id)),
                "full character and item reload preserve the earned RVP purchase, consumed stones and resulting perfection");
            Check(p.Equipment.TryGetItem(ring.UID)!=null && p.Equipment.TryGetItem(ring.UID).ID==150219 && p.Equipment.TryGetItem(ring.UID).Bound
                && storedRewards.All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),"full reload preserves the earned equipped ring and banked Family Feud rewards");
            ulong earnedSilver=p.Entity.Money;
            var expeditionRewards=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            p.Entity.Teleport(1002,317,322);
            foreach(uint rewardUid in expeditionRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
            p=ReloadBall(p);
            Check(p.Inventory.Count==0 && earnedSilver>=10000000 && p.Entity.Money==earnedSilver,
                "boss-earned silver and banked quest rewards fund Desert Treasure without supplied money");
            Environment.CurrentDirectory=priorDirectory;
            VerifyDesertTreasureQuest(runtime,town,p,actor=>{if(actor.Entity.UID==careerUid)p=actor;});
            var bootPack=p.Inventory.Objects.Single(i=>i.ID==728249);ItemHandler.UseItem(bootPack,p);
            var boots=p.Inventory.Objects.Single(i=>i.ID==160199);
            ItemHandler.EquipItem(new ItemUsage(true){UID=boots.UID,dwParam=8,Action=ItemUsage.Mode.EquipItem},p);
            Check(EntityTable.SaveEntity(p),"earned three-slot equipment journey saves normally");
            p=ReloadBall(p);target=p.Equipment.TryGetItem(targetUid);
            Check(!Fenced(p) && p.Entity.UID==careerUid && target!=null && target.PerfectionLevel==10 && target.PerfectionProgress==1460
                && p.Equipment.TryGetItem(ring.UID)!=null && p.Equipment.TryGetItem(boots.UID)!=null && p.Equipment.TryGetItem(boots.UID).Bound
                && p.Entity.Money==earnedSilver-93499 && p.Entity.RevoraPoint==39600
                && storedRewards.Concat(expeditionRewards).All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),
                "same ordinary character earns Super boots with boss silver and retains all three equipped rewards and stored loot after reload");
            var treasureRewards=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            p.Entity.Teleport(1002,317,322);
            foreach(uint rewardUid in treasureRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
            Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL)");
            Query("INSERT INTO npcs SELECT n.* FROM public.npcs n WHERE (id IN (600050,600003) OR (id BETWEEN 600005 AND 600017 AND id%2=1) OR id BETWEEN 600018 AND 600042) AND NOT EXISTS(SELECT 1 FROM npcs x WHERE x.id=n.id)");
            for(ushort map=1042;map<=1050;map++)if(!Kernel.Maps.ContainsKey(map))World(runtime,map);
            Call(Moon,null,"Initialize");Call(Moon,null,"PlaceNpcs");var oldMoon=Moon.GetField("Roll",Hidden).GetValue(null);
            Moon.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==7?moonRoute:0));
            try{
                p.Entity.Teleport(1002,489,622);MoonNpc(p,600050,1);
                for(int stage=0;stage<6;stage++){
                    moonRoute=stage;MoonNpc(p,600003,1);var mob=MoonMob(p);mob.Hitpoints=mob.MaxHitpoints=100;uint moonDamage=100;
                    Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref moonDamage,null);
                    MoonNpc(p,(uint)(600005+stage*2),1);
                }
                MoonNpc(p,600003,1);MoonNpc(p,600017,1);MoonNpc(p,600018,2);
            }finally{Moon.GetField("Roll",Hidden).SetValue(null,oldMoon);}
            p=ReloadBall(p);
            Check(!Fenced(p) && CountId(p,1088001)==3,"equipment journey earns and persists three Meteors through the six-tactic Moon quest");
            var moonRewards=p.Inventory.Objects.Where(i=>i.ID!=1088001).Select(i=>i.UID).ToArray();
            p.Entity.Teleport(1002,317,322);
            foreach(uint rewardUid in moonRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
            VerifyHeroWingsQuest(runtime,town,p,actor=>{if(actor.Entity.UID==careerUid)p=actor;});
            var head=p.Inventory.Objects.Single(i=>i.ID==118089);
            ItemHandler.EquipItem(new ItemUsage(true){UID=head.UID,dwParam=1,Action=ItemUsage.Mode.EquipItem},p);
            Check(EntityTable.SaveEntity(p),"earned four-slot equipment journey saves normally");
            p=ReloadBall(p);target=p.Equipment.TryGetItem(targetUid);
            Check(!Fenced(p) && p.Entity.UID==careerUid && target!=null && target.PerfectionLevel==10 && target.PerfectionProgress==1460
                && new[]{ring.UID,boots.UID,head.UID}.All(id=>p.Equipment.TryGetItem(id)!=null && p.Equipment.TryGetItem(id).Bound)
                && CountId(p,1088001)==2 && p.Entity.Money==earnedSilver-93499+6800 && p.Entity.RevoraPoint==39600
                && storedRewards.Concat(expeditionRewards).Concat(treasureRewards).Concat(moonRewards).All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),
                "same ordinary character retains four earned Super equipment slots, unused Meteors, balances and stored rewards after reload");
            var heroRewards=p.Inventory.Objects.Where(i=>i.ID!=1088001).Select(i=>i.UID).ToArray();
            p.Entity.Teleport(1002,317,322);
            foreach(uint rewardUid in heroRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
            VerifyPowerQuest(town,p,actor=>p=actor);
            var armorPack=p.Inventory.Objects.Single(i=>i.ID==728251);ItemHandler.UseItem(armorPack,p);
            var armor=p.Inventory.Objects.Single(i=>i.ID==130109);
            ItemHandler.EquipItem(new ItemUsage(true){UID=armor.UID,dwParam=3,Action=ItemUsage.Mode.EquipItem},p);
            Check(EntityTable.SaveEntity(p),"earned five-slot equipment journey saves normally");
            p=ReloadBall(p);target=p.Equipment.TryGetItem(targetUid);
            Check(!Fenced(p) && p.Entity.UID==careerUid && target!=null && target.PerfectionLevel==10 && target.PerfectionProgress==1460
                && new[]{ring.UID,boots.UID,head.UID,armor.UID}.All(id=>p.Equipment.TryGetItem(id)!=null && p.Equipment.TryGetItem(id).Bound)
                && CountId(p,1088001)==1 && p.Entity.RevoraPoint==39600
                && storedRewards.Concat(expeditionRewards).Concat(treasureRewards).Concat(moonRewards).Concat(heroRewards).All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),
                "same ordinary character earns Super armor with a Moon Meteor and retains five equipped slots and stored rewards after reload");
            var powerRewards=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            p.Entity.Teleport(1002,317,322);
            foreach(uint rewardUid in powerRewards)Revora.Network.GamePackets.Warehouse.Process(StorageFrame(1,rewardUid),p);
            p=ReloadBall(p);
            Check(p.Inventory.Count==0 && p.Entity.Money>9000000,"earned boss silver remains available for the talisman quest costs");
            VerifyDefenseTalismanQuest(runtime,town,false,true,p,actor=>{if(actor.Entity.UID==careerUid)p=actor;});
            var tower=p.Inventory.Objects.Single(i=>i.ID==202008);var fan=p.Inventory.Objects.Single(i=>i.ID==201009);
            ItemHandler.EquipItem(new ItemUsage(true){UID=tower.UID,dwParam=11,Action=ItemUsage.Mode.EquipItem},p);
            ItemHandler.EquipItem(new ItemUsage(true){UID=fan.UID,dwParam=10,Action=ItemUsage.Mode.EquipItem},p);
            Check(EntityTable.SaveEntity(p),"earned seven-slot equipment journey saves normally");
            p=ReloadBall(p);target=p.Equipment.TryGetItem(targetUid);
            Check(!Fenced(p) && p.Entity.UID==careerUid && target!=null && target.PerfectionLevel==10 && target.PerfectionProgress==1460
                && new[]{ring.UID,boots.UID,head.UID,armor.UID,tower.UID,fan.UID}.All(id=>p.Equipment.TryGetItem(id)!=null && p.Equipment.TryGetItem(id).Bound)
                && p.Entity.RevoraPoint==39600
                && storedRewards.Concat(expeditionRewards).Concat(treasureRewards).Concat(moonRewards).Concat(heroRewards).Concat(powerRewards).All(id=>TownStorage(p).Objects.Any(i=>i.UID==id)),
                "same ordinary character earns and reloads both talismans alongside five equipment rewards without supplied quest silver");
            Environment.CurrentDirectory=runtime;
            var master=typeof(Entity).Assembly.GetType("Revora.Game.Features.RevoraMasterSystem");Call(master,null,"InitializeMountKits");
            uint masterNpc=Revora.Game.Features.RevoraMasterSystem.NpcId;
            town.AddNpc(new NpcSpawn{UID=masterNpc,MapID=1002,X=410,Y=354,Mesh=46030,Type=Enums.NpcType.Talker});
            p.Entity.Teleport(1002,410,353);p.ActiveNpc=masterNpc;uint earnedCp=p.Entity.ConquerPoints;
            var beforeKit=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            Npcs.GetDialog(new NpcRequest(true){NpcID=masterNpc,OptionID=4},p);
            Npcs.GetDialog(new NpcRequest(true){NpcID=masterNpc,OptionID=14},p);
            var kit=p.Inventory.Objects.Where(i=>!beforeKit.Contains(i.UID)).ToArray();
            Check(!Fenced(p) && kit.Length==5 && p.Entity.ConquerPoints==earnedCp-Revora.Game.Features.RevoraMasterSystem.MountKitPrice
                && Scalar("SELECT count(*) FROM revora_mount_kit_purchases WHERE uid=@uid",careerUid)==1,"earned boss CPs purchase one durable mount kit through ordinary dialogue");
            var mountPieces=kit.Where(i=>i.ID==203009 || i.ID==204009 || i.ID==300000).ToArray();
            foreach(var item in mountPieces)ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=(uint)ItemHandler.GetPositionFromID(item.ID),Action=ItemUsage.Mode.EquipItem},p);
            ShopFile.Load();Call(BookShop,null,"Initialize");
            uint weaponType=0,weaponShop=0;ushort weaponMap=0;
            foreach(var candidate in ShopFile.Shops.Values.Where(shop=>shop.MoneyType==ShopFile.MoneyType.Gold)
                .SelectMany(shop=>shop.Items.Where(id=>id/1000==410 && ConquerItemInformation.BaseInformations.ContainsKey(id)
                    && ConquerItemInformation.BaseInformations[id].Level<=70).Select(id=>new{Shop=shop.UID,Item=id}))
                .OrderBy(choice=>ConquerItemInformation.BaseInformations[choice.Item].Level)){
                using(var connection=DataHolder.MySqlConnection){connection.Open();using(var command=new Npgsql.NpgsqlCommand("SELECT mapid FROM public.npcs WHERE id=@id",connection)){
                    command.Parameters.AddWithValue("id",(long)candidate.Shop);object map=command.ExecuteScalar();if(map==null)continue;
                    weaponMap=Convert.ToUInt16(map);weaponShop=candidate.Shop;weaponType=candidate.Item;break;
                }}
            }
            if(weaponShop==0)throw new Exception("No installed low-level silver blade shop");
            var weaponTown=Kernel.Maps.ContainsKey(weaponMap)?Kernel.Maps[weaponMap]:World(runtime,weaponMap);
            MiningNpc(weaponTown,weaponShop);p.Entity.Teleport(weaponMap,weaponTown.Npcs[weaponShop].X,weaponTown.Npcs[weaponShop].Y);ulong weaponSilver=p.Entity.Money;
            ItemHandler.HandleBuyFromNPC(BuyBook(weaponType,2,weaponShop),p);
            var weapons=p.Inventory.Objects.Where(i=>i.ID==weaponType).ToArray();
            Check(!Fenced(p) && weapons.Length==2 && p.Entity.Money==weaponSilver-2UL*ConquerItemInformation.BaseInformations[weaponType].GoldWorth
                && BookPurchases(p)==1,"earned boss silver purchases two installed one-hand weapons atomically");
            for(int hand=0;hand<2;hand++)ItemHandler.EquipItem(new ItemUsage(true){UID=weapons[hand].UID,dwParam=(uint)(4+hand),Action=ItemUsage.Mode.EquipItem},p);
            uint[] earnedTargets=new[]{targetUid,ring.UID,boots.UID,head.UID,armor.UID,fan.UID,tower.UID}.Concat(mountPieces.Select(i=>i.UID)).Concat(weapons.Select(i=>i.UID)).ToArray();
            Check(earnedTargets.Length==12 && earnedTargets.All(id=>p.Equipment.TryGetItem(id)!=null),"all twelve Perfection slots equip through ordinary handlers using earned equipment or purchased rewards: missing="+string.Join(";",earnedTargets.Where(id=>p.Equipment.TryGetItem(id)==null).Select(id=>{var item=p.Inventory.Objects.FirstOrDefault(i=>i.UID==id);return id+":"+(item==null?"absent":item.ID+"/"+item.Position);})));
            Environment.CurrentDirectory=fixture;
            int fundedKills=1,boughtStones=0;uint spentRvp=10400;
            new TitleStorage().Login(p);
            Check(!OwnedTitles(p).ContainsKey(60086008) && !OwnedTitles(p).ContainsKey(60076007),"earned equipment alone does not grant Perfection threshold wings");
            for(int piece=0;piece<earnedTargets.Length;piece++){
                var major=p.Equipment.TryGetItem(earnedTargets[piece]);
                for(int attempt=0;major.PerfectionLevel<54 && attempt<60;attempt++){
                    if(p.Entity.RevoraPoint<8000){
                        if(fundedKills>=120)throw new Exception("Perfection earned funding exceeded bounded boss count");
                        var future=DailySchedule().Where(time=>time>dailyNow).ToArray();
                        if(future.Length==0){DailyTick(dailyNow.AddDays(1).Date);future=DailySchedule().Where(time=>time>dailyNow).ToArray();}
                        if(future.Length==0)throw new Exception("Perfection next scheduled boss missing");
                        DailyTick(future.First());var fundingBoss=DailyActive();if(fundingBoss==null)throw new Exception("Perfection funding boss absent");
                        p.Entity.MapID=fundingBoss.MapID;p.Entity.X=fundingBoss.X;p.Entity.Y=fundingBoss.Y;uint hitPoints=fundingBoss.MaxHitpoints;
                        uint beforeRvp=p.Entity.RevoraPoint;
                        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,fundingBoss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=fundingBoss.UID},ref hitPoints,null);
                        if(Fenced(p) || DailyActive()!=null || p.Entity.RevoraPoint!=beforeRvp+50000)throw new Exception("Perfection scheduled bounty failed");
                        fundedKills++;
                    }
                    p.Entity.MapID=1002;p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=Revora.Game.Features.RevoraPointShopSystem.NpcId;
                    var stone=BuyRouteRune(p,13);boughtStones++;spentRvp+=8000;
                    uint beforeLevel=major.PerfectionLevel,beforeProgress=major.PerfectionProgress;
                    var temper=PerfectionRequest(major.UID,new[]{stone.UID});PacketHandler.HandlePacket(temper,p);
                    if(Fenced(p) || p.Inventory.ContainsUID(stone.UID) || major.PerfectionLevel==beforeLevel && major.PerfectionProgress==beforeProgress)throw new Exception("Earned stone failed to advance equipment");
                    PacketHandler.HandlePacket(temper,p);
                }
                Check(major.PerfectionLevel==54 && major.PerfectionProgress==0 && major.PerfectionOwnerGuid==careerUid,"boss-funded purchased stones max earned equipment piece "+piece);
                int total=(piece+1)*54;RefreshTitles(p);
                Check(p.Equipment.GetTPL()==total && OwnedTitles(p).ContainsKey(60086008)==(total>=216)
                    && OwnedTitles(p).ContainsKey(60076007)==(total>=324) && OwnedTitles(p).ContainsKey(20392039)==(total>=648),
                    "earned Perfection reconciles exact wardrobe eligibility at total "+total);
                if(total==216 || total==324 || total==648){
                    int wing=total==216?60086008:total==324?60076007:20392039;TitleRequest(wing,TitleStorage.Action.Equip).Handle(p);
                    Check((total==648?p.Entity.EquippedTitle:p.Entity.EquippedWing)==wing && EntityTable.SaveEntity(p),"earned Perfection wardrobe reward equips and saves at "+total);
                    p=ReloadBall(p);new TitleStorage().Login(p);
                    Check(p.Equipment.GetTPL()==total && OwnedTitles(p).ContainsKey(wing) && (total==648?p.Entity.EquippedTitle:p.Entity.EquippedWing)==wing,
                        "earned Perfection wardrobe ownership and selection survive full reload at "+total);
                }
            }
            Check(p.Entity.RevoraPoint==(uint)fundedKills*50000-spentRvp
                && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",careerUid)==fundedKills
                && !p.Inventory.Objects.Any(i=>i.ID>=3009000 && i.ID<=3009003),
                "all threshold materials are funded by unique saved boss bounties with exact RVP accounting and no leftover stones");
            System.Console.WriteLine("Perfection earned thresholds: bosses="+fundedKills+" splendid-stones="+boughtStones+" total-rvp-spent="+spentRvp);
            Check(EntityTable.SaveEntity(p),"earned equipment Prestige baseline saves through normal character persistence");
            var earnedPrestige=PrestigeRanking.Update(p);
            uint[] prestigeTitles={20272027,20262026};uint[] prestigeThresholds={400000,600000};
            for(int index=0;index<prestigeTitles.Length;index++)
                Check(OwnedTitles(p).ContainsKey((int)prestigeTitles[index])==(earnedPrestige.Total>=prestigeThresholds[index]),
                    "earned Prestige baseline grants only eligible threshold title "+prestigeTitles[index]);
            Query("CREATE TABLE IF NOT EXISTS subclasses (LIKE public.subclasses INCLUDING ALL)");
            Kernel.GamePool.Remove(careerUid);
            try{
                PrestigeRanking.LoadSavedPlayers();
                var savedPrestige=PrestigeRanking.Snapshot(null).Single(entry=>entry.Profile.UID==careerUid);
                Check(savedPrestige.Total==earnedPrestige.Total && savedPrestige.Abilities.Abilities.Select(ability=>ability.Value)
                    .SequenceEqual(earnedPrestige.Abilities.Abilities.Select(ability=>ability.Value)),
                    "offline Prestige rebuild preserves every category and total from the earned twelve-slot character: online="+earnedPrestige.Total+" saved="+savedPrestige.Total+" differences="
                    +string.Join(";",earnedPrestige.Abilities.Abilities.Where((ability,index)=>ability.Value!=savedPrestige.Abilities.Abilities[index].Value)
                        .Select(ability=>ability.Sort+"="+ability.Value+"/"+savedPrestige.Abilities.Abilities.Single(saved=>saved.Sort==ability.Sort).Value)));
            }finally{Kernel.GamePool[careerUid]=p;}
            System.Console.WriteLine("Earned Prestige baseline: total="+earnedPrestige.Total+" categories="
                +string.Join(";",earnedPrestige.Abilities.Abilities.Select(ability=>ability.Sort+"="+ability.Value)));
            Environment.CurrentDirectory=runtime;
            VerifyEarnedPrestigeCultivation(p);



        }finally {Daily.GetField("Clock",Hidden).SetValue(null,priorClock);Environment.CurrentDirectory=priorDirectory;}
    }

    static void VerifyEarnedPrestigeCultivation(GameState p) {
        Query("CREATE TABLE IF NOT EXISTS chi (LIKE public.chi INCLUDING ALL);CREATE TABLE IF NOT EXISTS jianghu (LIKE public.jianghu INCLUDING ALL)");
        Revora.Database.JiangHu.LoadStatus();ChiTable.Load(p);
        uint cp=p.Entity.ConquerPoints,uid=p.Entity.UID;byte life=p.Entity.Reborn;
        Check(life==1 && p.ChiPowers.Count==0 && p.Entity.MyJiang==null && cp>=29250,
            "earned Prestige character starts cultivation with its original rebirth and boss-funded purse");
        var next=typeof(GeneralData).GetField("ChiValueNext",Hidden);var rate=typeof(GeneralData).GetField("ChiValueRate",Hidden);
        var oldNext=next.GetValue(null);var oldRate=rate.GetValue(null);
        Action<Chi> chi=request=>{byte[] frame=request.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,16,frame);PacketHandler.HandlePacket(frame,p);};
        try{
            next.SetValue(null,(Func<int,int,int>)((low,high)=>high-1));rate.SetValue(null,(Func<int,int,bool>)((chance,total)=>true));
            chi(new Chi(true){Type=Chi.BuyStrength});
            for(int power=1;power<=2;power++){
                var mode=(Enums.ChiPowerType)power;chi(new Chi(true){Type=Chi.Unlock,Mode=mode});chi(new Chi(true){Type=Chi.Study,Mode=mode});
            }
            chi(new Chi(true){Type=Chi.Unlock,Mode=Enums.ChiPowerType.Tiger});
            Check(!Fenced(p) && p.ChiPowers.Count==2 && p.ChiPowers.All(power=>power.Points==400) && p.ChiPoints==3300
                && p.Entity.ConquerPoints==cp-1000 && p.Entity.Reborn==life,
                "earned CP refill and two legal Chi pursuits preserve the second-rebirth gate without charging rejected unlock");
        }finally{next.SetValue(null,oldNext);rate.SetValue(null,oldRate);}
        PacketHandler.HandlePacket(JiangCreationPacket("PrestigePath"),p);
        Check(!Fenced(p) && p.Entity.MyJiang!=null,"same ordinary Prestige character creates Jiang through the packet handler");
        p.Entity.MyJiang.Random=new JiangOutcomeRandom(true);
        for(byte stage=1;stage<=9;stage++)for(byte star=1;star<=9;star++){
            var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16((ushort)PacketID.JiangHuStudy,2,frame);
            frame[9]=1;frame[10]=star;frame[11]=stage;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
        }
        var prestige=PrestigeRanking.Update(p);RefreshTitles(p);
        Check(!Fenced(p) && p.Entity.MyJiang.Inner_Strength==81000 && p.Entity.ConquerPoints==cp-29250 && p.Entity.Reborn==life
            && prestige.Total==396268 && !OwnedTitles(p).ContainsKey(20272027) && !OwnedTitles(p).ContainsKey(20262026),
            "81 paid Jiang rolls and eligible Chi preserve exact payment and do not grant the Prestige title early: score="+prestige.Total);
        VerifyEarnedNobility(p,Environment.CurrentDirectory,actor=>p=actor,100);ChiTable.Load(p);prestige=PrestigeRanking.Update(p);RefreshTitles(p);
        Check(!Fenced(p) && prestige.Total==401268 && p.Entity.ConquerPoints==cp-129250 && p.Entity.Reborn==life
            && OwnedTitles(p).ContainsKey(20272027) && !OwnedTitles(p).ContainsKey(20262026),
            "ordinary earned-CP nobility donation crosses 400000 Prestige and grants only the eligible title");
        new TitleStorage().Login(p);
        TitleRequest(20272027,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20272027 && EntityTable.SaveEntity(p),"earned 400000 Prestige title equips and saves normally");
        Revora.Database.JiangHu.SaveJiangHu(p);Revora.Interfaces.IJiangHu removed;
        Revora.Game.JiangHu.JiangHuClients.TryRemove(uid,out removed);Revora.Database.JiangHu.LoadJiangHu();
        p=ReloadBall(p);ChiTable.Load(p);new TitleStorage().Login(p);
        Check(!Fenced(p) && p.Entity.Reborn==life && p.Entity.ConquerPoints==cp-129250 && p.Entity.MyJiang.Inner_Strength==81000
            && p.ChiPowers.Count==2 && p.ChiPoints==3300 && PrestigeRanking.Update(p).Total==prestige.Total
            && OwnedTitles(p).ContainsKey(20272027) && p.Entity.EquippedTitle==20272027,
            "earned Prestige title and combined equipment/cultivation score survive full character Chi and Jiang reload");
        Kernel.GamePool.Remove(uid);
        try{
            PrestigeRanking.LoadSavedPlayers();var saved=PrestigeRanking.Snapshot(null).Single(entry=>entry.Profile.UID==uid);
            Check(saved.Total==prestige.Total && saved.Abilities.Abilities.Select(ability=>ability.Value)
                .SequenceEqual(prestige.Abilities.Abilities.Select(ability=>ability.Value)),"offline ranking preserves every earned cultivation Prestige category");
        }finally{Kernel.GamePool[uid]=p;}
        System.Console.WriteLine("Earned cultivated Prestige: total="+prestige.Total+" CP-spent="+(cp-p.Entity.ConquerPoints));
        VerifyEarnedPrestigeSecondRite(p);
    }

    static void VerifyEarnedPrestigeSecondRite(GameState p) {
        uint uid=p.Entity.UID,cp=p.Entity.ConquerPoints;var town=Kernel.Maps[1002];
        var perfected=p.Equipment.Objects.Where(i=>i!=null && i.PerfectionLevel==54).Select(i=>i.UID).ToArray();
        Check(p.Entity.Reborn==1 && p.Entity.Level==140 && p.Entity.FirstRebornClass==15 && p.Entity.FirstRebornLevel==130
            && perfected.Length==12 && SecondCount(p)==0,"continuing earned-gear character retains valid supplied first-life history and has no second rite");
        EShopFile.Load();Call(TokenShop,null,"Initialize");
        uint price=ConquerItemInformation.BaseInformations[723701].ConquerPointsWorth;
        ItemUsage.Process(BuyToken().ToArray(),p);
        var token=p.Inventory.Objects.SingleOrDefault(i=>i.ID==723701);
        Check(!Fenced(p) && price==888 && token!=null && TokenPurchases(p)==1 && p.Entity.ConquerPoints==cp-price,
            "same Prestige character buys one Exemption Token for 888 earned boss CPs through the ordinary mall request");
        p=ReloadBall(p);ChiTable.Load(p);
        p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;
        var priorItems=p.Inventory.Objects.Select(i=>i.UID).ToArray();
        SecondOffer(p);Parallel.For(0,8,n=>StoneNpc(p,180));
        Check(!Fenced(p) && p.Entity.UID==uid && p.Entity.Reborn==2 && p.Entity.Level==15 && p.Entity.Class==11
            && p.Entity.SecondRebornClass==15 && p.Entity.SecondRebornLevel==140 && SecondCount(p)==1
            && p.Entity.ConquerPoints==cp-price && !p.Inventory.ContainsUID(token.UID),
            "concurrent ordinary second-rite confirmations consume earned token once and preserve the actual level/class reset");
        var earnedSoul=p.Inventory.Objects.Single(i=>!priorItems.Contains(i.UID) && i.ID>=800000 && i.ID<900000);
        Check(earnedSoul.Bound && ConquerItemInformation.BaseInformations[earnedSoul.ID].PurificationLevel==7
            && Scalar("SELECT soul_uid FROM revora_second_rebirths WHERE uid=@uid",uid)==earnedSoul.UID,
            "one-time second rite grants its own bound P7 soul without confusing earlier owned quest souls");
        var owned=p.Inventory.Objects.Concat(p.Equipment.Objects.Where(i=>i!=null)).ToArray();
        Check(perfected.All(id=>owned.Any(i=>i.UID==id && i.PerfectionLevel==54 && i.PerfectionProgress==0 && i.PerfectionOwnerGuid==uid)),
            "second rite preserves all twelve earned perfection identities including returned offhand equipment");
        Check(HasAchievement(p,10316) && !HasAchievement(p,11031),"token-funded second rite grants rebirth without claiming Abyss quest completion");
        Check(EntityTable.SaveEntity(p),"second-reborn Prestige character saves before reconnect");
        p=ReloadBall(p);ChiTable.Load(p);new TitleStorage().Login(p);var score=PrestigeRanking.Update(p);
        Check(!Fenced(p) && p.Entity.Reborn==2 && p.Entity.Level==15 && p.Entity.Class==11 && p.Entity.ConquerPoints==cp-price
            && p.ChiPowers.Count==2 && p.Entity.MyJiang.Inner_Strength==81000 && SecondCount(p)==1,
            "second rite history, payment and earlier Chi/Jiang survive ordinary entity inventory and wardrobe reload");
        Check(!OwnedTitles(p).ContainsKey(20262026) && score.Total<600000,"level reset does not manufacture the unfinished 600000 Prestige title");
        Kernel.GamePool.Remove(uid);
        try{
            PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
            Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                "all post-rebirth Prestige categories agree online and from saved character data");
        }finally{Kernel.GamePool[uid]=p;}
        System.Console.WriteLine("Earned second-rite Prestige: total="+score.Total+" token-CP="+price+" level="+p.Entity.Level+" class="+p.Entity.Class);
        VerifyEarnedInnerPower(p);
    }

    sealed class SupremePotencyRandom:Random {
        public override int Next(int maxValue){return maxValue==10000?1500:base.Next(maxValue);}
    }
    static void VerifyEarnedInnerPower(GameState p) {
        string previous=Environment.CurrentDirectory;var town=Kernel.Maps[1002];uint uid=p.Entity.UID;
        var shop=typeof(Entity).Assembly.GetType("Revora.Game.Features.InnerPowerPurchases");Call(shop,null,"Initialize");ShopFile.Load();
        // ReloadBall restores entity/items only. Complete the normal login binding
        // and HP setup before combat; never retain its placeholder character's Inner Power.
        if(!InnerPower.InnerPowerPolle.TryGetValue(uid,out p.InnerPower))p.InnerPower=new InnerPower(p.Entity.Name,uid);
        Call(Packs,null,"RecoverPlayer",p.InnerPower);Call(shop,null,"RecoverPlayer",p.InnerPower);
        p.LoadItemStats();p.CalculateHPBonus();p.Entity.Hitpoints=p.Entity.MaxHitpoints;
        var rules=InnerPowerTable.Stages.SelectMany(s=>s.NeiGongAtributes).OrderBy(g=>g.ID).ToArray();
        Check(p.Entity.Reborn==2 && p.Entity.Level==15 && p.InnerPower.TotalScore==0 && p.InnerPower.Potency==0
            && rules.Length==17 && rules.All(g=>g.CheckAccount(p.Entity.Reborn,p.Entity.Level)),"earned second rite admits all installed Inner Power techniques without supplied progress");
        foreach(uint npc in new uint[]{91009}) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(
                "SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=@id UNION ALL SELECT mapid,cellx,celly,lookface FROM public.sobnpcs WHERE id=@id",c)){
                q.Parameters.AddWithValue("id",(long)npc);using(var r=q.ExecuteReader()){
                    if(!r.Read() || Convert.ToUInt16(r[0])!=1002)throw new Exception("Inner Power installed merchant missing: "+npc);
                    town.AddNpc(new NpcSpawn{UID=npc,MapID=1002,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),
                        Type=npc==91009?Enums.NpcType.Shop:Enums.NpcType.Talker});
                }
            }}
        }
        Check(ShopFile.Shops[91009].MoneyType==ShopFile.MoneyType.ConquerPoints && rules.All(g=>ShopFile.Shops[91009].Items.Contains(g.ItemID)),
            "installed ordinary Inner Power merchant stocks all seventeen required books for CPs");
        uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;int spent=0,levels=0;uint bookCost=0;
        string fixture=Path.GetFullPath(Path.Combine(previous,"../../obj/inner-earned-file-test"));Directory.CreateDirectory(Path.Combine(fixture,"database"));
        try {
            Environment.CurrentDirectory=fixture;
            var map=Kernel.Maps.ContainsKey(1217)?Kernel.Maps[1217]:World(previous,1217);
            Call(Hunt,null,"Create");Call(Guide,null,"Create");
            p.Entity.Teleport(1002,town.Npcs[900000018].X,town.Npcs[900000018].Y);p.ActiveNpc=900000018;
            Npcs.GetDialog(new NpcRequest(true){NpcID=900000018,OptionID=1},p);
            Check(!Fenced(p) && p.Entity.MapID==1217,"same ordinary character enters Potency Hunt through its public guide");
            var population=typeof(Map).GetField("PotencyPopulation",Instance).GetValue(map);
            var random=Hunt.GetField("random",Instance);var priorRandom=random.GetValue(population);
            try{
                // A legal Supreme outcome is controlled; this is not a drop-rate or combat-balance claim.
                random.SetValue(population,new SupremePotencyRandom());
                var monsters=map.Entities.Values.Where(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==9901217 && !m.Dead).Take(175).ToArray();
                if(monsters.Length!=175)throw new Exception("Potency population missing");
                foreach(var monster in monsters){
                    p.Entity.X=monster.X;p.Entity.Y=monster.Y;var before=map.FloorItems.Keys.ToArray();uint damage=monster.MaxHitpoints;
                    Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,monster,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=monster.UID},ref damage,null);
                    var drop=map.FloorItems.Values.Single(d=>!before.Contains(d.UID) && d.ItemID==3007110);drop.SendSpawn(p,true);Loot(p,drop);
                    if(!p.Inventory.ContainsUID(drop.Item.UID)){FloorItem visible;
                        throw new Exception("Potency pickup failed: visible="+p.Screen.TryGetFloorItem(drop.UID,out visible)+" action="+p.Action+" dead="+p.Entity.Dead+" count="+p.Inventory.Count+" trade="+p.Trade.InTrade+" fenced="+Fenced(p)+" map="+p.Map.ID+" dropmap="+drop.MapID+" mob="+drop.Item.MobDropped+" bound="+drop.Item.Bound+" stack="+drop.Item.StackSize+" points="+p.InnerPower.Potency);}

                    ItemHandler.UseItem(drop.Item,p);
                    if(Fenced(p) || p.Inventory.ContainsUID(drop.Item.UID))throw new Exception("Earned potency pack failed");
                }
            }finally{random.SetValue(population,priorRandom);}
            Check(!Fenced(p) && p.InnerPower.Potency==175000 && Scalar("SELECT points FROM revora_potency_awards WHERE entityid=@uid",uid)==175000,
                "175 real managed deaths drops pickups and pack uses earn 175000 potency under controlled legal Supreme rolls");
            p.Entity.X=map.Npcs[900000019].X;p.Entity.Y=map.Npcs[900000019].Y;p.ActiveNpc=900000019;
            Npcs.GetDialog(new NpcRequest(true){NpcID=900000019,OptionID=1},p);
            Check(p.Entity.MapID==1002,"public field guide returns the earning character to the book shop city");
            foreach(var rule in rules){
                p.Entity.Teleport(1002,town.Npcs[91009].X,town.Npcs[91009].Y);
                uint before=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[rule.ItemID].ConquerPointsWorth;
                ItemUsage.Process(BuyBook(rule.ItemID,1,91009).ToArray(),p);bookCost+=price;
                var book=p.Inventory.Objects.SingleOrDefault(i=>i.ID==rule.ItemID);
                Check(!Fenced(p) && price>0 && book!=null && p.Entity.ConquerPoints==before-price,
                    "ordinary item request buys Inner Power book using earned boss CPs: "+rule.ID);
                InnerRequest(p,4,rule.ID);InnerRequest(p,4,rule.ID);
                if(Fenced(p) || p.Inventory.ContainsUID(book.UID))throw new Exception("Earned book failed to unlock once: "+rule.ID);
                for(int level=0;level<rule.MaxLevel;level++){
                    int cost=(int)rule.ProgressNeiGongValue[Math.Min(level,rule.ProgressNeiGongValue.Length-1)];
                    int prior=p.InnerPower.Potency;InnerRequest(p,3,rule.ID);spent+=cost;levels++;
                    InnerPower.Stage stage;InnerPower.Stage.NeiGong gong;p.InnerPower.TryGetStageAndGong(rule.ID,out stage,out gong);
                    if(Fenced(p) || p.InnerPower.Potency!=prior-cost || gong.level!=level+1)throw new Exception("Earned Inner Power training failed: "+rule.ID);
                }
            }
            Check(bookCost==3600 && levels==111 && spent==174200 && p.InnerPower.TotalScore==1700 && p.InnerPower.Stages.All(s=>s.Complete)
                && p.InnerPower.Potency==800 && p.Entity.ConquerPoints==cp-bookCost && p.Entity.RevoraPoint==rvp,
                "all seventeen purchased books and 111 paid levels reach full Inner Power for 3600 CP and 174200 hunted potency");
            Check(Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",uid)==spent
                && Scalar("SELECT points FROM revora_potency_awards WHERE entityid=@uid",uid)==175000 && EntityTable.SaveEntity(p),
                "earned Inner Power ledger saves exact training debit and hunted pack awards");
            var score=PrestigeRanking.Update(p);var cold=ReloadInner(p);p=ReloadBall(p);p.InnerPower=cold;InnerPower.InnerPowerPolle[uid]=cold;
            ChiTable.Load(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.Reborn==2 && p.Entity.Level>=15 && cold.TotalScore==1700 && cold.Potency==800
                && p.Entity.ConquerPoints==cp-bookCost && p.Entity.RevoraPoint==rvp && PrestigeRanking.Update(p).Total==score.Total,
                "full entity inventory and cold Inner Power recovery preserve earned score and both currency balances: fenced="+Fenced(p)+" life="+p.Entity.Reborn+" level="+p.Entity.Level+" inner="+cold.TotalScore+" potency="+cold.Potency+" cp="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" before="+score.Total+" after="+PrestigeRanking.Update(p).Total);
            Kernel.GamePool.Remove(uid);
            try{PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
                Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                    "offline Prestige preserves every category after full earned Inner Power");
            }finally{Kernel.GamePool[uid]=p;}
            Check(score.Total<600000 && !OwnedTitles(p).ContainsKey(20262026),"earned Inner Power still does not grant an unearned 600000 title");
            System.Console.WriteLine("Earned Inner Power Prestige: total="+score.Total+" CP-books="+bookCost+" RVP=0 potency-spent="+spent+" level="+p.Entity.Level);
            Environment.CurrentDirectory=previous;
            VerifyEarnedPrestigeRunes(p);
        }finally{Environment.CurrentDirectory=previous;}
    }

    static void VerifyEarnedPrestigeRunes(GameState p,bool alternate=false) {
        if(alternate)EarnPrestigeBounty(p);
        int sets=alternate?2:1,expectedScore=67000*sets;
        EShopFile.Load();Call(TokenShop,null,"Initialize");RunesSystem.LoadRunesEXP();SpellTable.Load();
        var clock=TokenShop.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);
        var now=new DateTime(2035,1,alternate?2:1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        try{
            uint uid=p.Entity.UID,cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
            Check(p.Account.State==AccountTable.AccountState.Player && p.RunesItem.Count==(alternate?5:0) && rvp>=3250 && cp>=629856,
                "same ordinary career character has earned currency and only previously earned runes");
            var town=Kernel.Maps[1002];p.Entity.Teleport(1002,396,340);p.LoadItemStats();p.CalculateHPBonus();p.Entity.Hitpoints=p.Entity.MaxHitpoints;
            p.ActiveNpc=Revora.Game.Features.RevoraPointShopSystem.NpcId;
            town.AddNpc(new NpcSpawn{UID=p.ActiveNpc,MapID=1002,X=396,Y=340,Mesh=50110,Name="Vesper",Type=Enums.NpcType.Talker});
            byte[] choices={180,190,200,201,202};var runes=new ConquerItem[5];
            for(int i=0;i<5;i++){runes[i]=BuyRouteRune(p,choices[i]);RuneMove(p,runes[i],61);}
            Check(!Fenced(p) && runes.All(i=>i.Position==211) && p.Entity.RevoraPoint==rvp-3250,
                "earned RVP buys five class-compatible runes through Vesper and transfers them to the rune bag");
            for(int i=1;i<5;i++){
                now=now.AddSeconds(2);uint quantity=i==1?12u:4u;
                var before=p.Inventory.Objects.Select(v=>v.UID).ToArray();uint balance=p.Entity.ConquerPoints;
                var purchase=BuyBook(730008,quantity,2888);ItemUsage.Process(purchase.ToArray(),p);ItemUsage.Process(purchase.ToArray(),p);
                var stones=p.Inventory.Objects.Where(v=>!before.Contains(v.UID)).ToArray();
                Check(!Fenced(p) && stones.Length==quantity && stones.All(v=>v.ID==730008 && v.Plus==8 && !v.Bound)
                    && p.Entity.ConquerPoints==balance-quantity*26244,"mall delivers paid +8 stones once under immediate replay: "+i);
                var packet=ComposePacket(runes[i].UID,stones.Select(v=>v.UID).ToArray());RunesSystem.HandlePacket1009(p,packet);
                RunesSystem.HandlePacket1009(p,packet);
                Check(!Fenced(p) && runes[i].ID%100==(i==1?27u:9u) && runes[i].RunesEXP==0
                    && stones.All(v=>!p.Inventory.ContainsUID(v.UID)) && SavedRune(runes[i],"id")==runes[i].ID,
                    "purchased stones reach persisted maximum and consumed-material replay is inert: "+i);
            }
            for(int i=0;i<5;i++)RuneMove(p,runes[i],57,(uint)((alternate?121:101)+i));
            new TitleStorage().Login(p);
            Check(!Fenced(p) && RuneScore(p)==expectedScore && OwnedTitles(p).ContainsKey(90001) && HasAchievement(p,11312)
                && p.Entity.ConquerPoints==cp-629856 && p.Entity.RevoraPoint==rvp-3250,
                "earned maximum equipped rune set contributes its expected Prestige and grants Chosen One at exact catalog cost");
            Check(Scalar("SELECT count(*) FROM revora_exemption_purchases WHERE uid=@uid AND item_type=730008",uid)==4*sets && EntityTable.SaveEntity(p),
                "durable stone purchase receipts back all consumed materials across earned sets");
            var score=PrestigeRanking.Update(p);var inner=ReloadInner(p);p=ReloadBall(p);p.InnerPower=inner;InnerPower.InnerPowerPolle[uid]=inner;
            ChiTable.Load(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && RuneScore(p)==expectedScore && p.RunesItem.Count==5*sets && p.Entity.ConquerPoints==cp-629856
                && p.Entity.RevoraPoint==rvp-3250 && PrestigeRanking.Update(p).Total==score.Total && OwnedTitles(p).ContainsKey(90001),
                "character item and wardrobe reload preserve paid runes exact balances and full Prestige");
            Kernel.GamePool.Remove(uid);
            try{PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
                Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                    "all offline Prestige categories match after purchased stone upgrades");
            }finally{Kernel.GamePool[uid]=p;}
            System.Console.WriteLine("Earned rune Prestige: total="+score.Total+" rune-score="+expectedScore+" set-CP=629856 set-RVP=3250");
        }finally{clock.SetValue(null,oldClock);}
        if(!alternate)VerifyEarnedPrestigeRunes(p,true);
        else VerifyEarnedRemainingChi(p);
    }

    static void EarnPrestigeBounty(GameState p) {
        p.LoadItemStats();p.CalculateHPBonus();p.Entity.Hitpoints=p.Entity.MaxHitpoints;
        var future=DailySchedule().Where(time=>time>dailyNow).ToArray();
        if(future.Length==0){DailyTick(dailyNow.AddDays(1).Date);future=DailySchedule().Where(time=>time>dailyNow).ToArray();}
        if(future.Length==0)throw new Exception("Prestige progression funding schedule missing");
        DailyTick(future.First());var boss=DailyActive();if(boss==null)throw new Exception("Prestige progression funding boss missing");
        uint cpBefore=p.Entity.ConquerPoints,rvpBefore=p.Entity.RevoraPoint;ulong goldBefore=p.Entity.Money;
        long receipts=Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID);
        p.Entity.Teleport(boss.MapID,boss.X,boss.Y);uint damage=boss.MaxHitpoints;
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
        Check(!Fenced(p) && DailyActive()==null && p.Entity.ConquerPoints==cpBefore+1000000 && p.Entity.RevoraPoint==rvpBefore+50000
            && p.Entity.Money==goldBefore+10000000 && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID)==receipts+1,
            "one additional scheduled boss death funds continuing progression with exact durable bounty under controlled damage");
    }



    static void VerifyEarnedRemainingChi(GameState p) {
        byte startingLevel=p.Entity.Level;int bosses=0;uint uid=p.Entity.UID;
        while(p.Entity.Level<110 && bosses<100){EarnPrestigeBounty(p);bosses++;}
        Check(p.Entity.Level>=110 && startingLevel<110 && bosses>0 && EntityTable.SaveEntity(p),
            "scheduled boss combat earns the level-110 gate without assigned experience: start="+startingLevel+" end="+p.Entity.Level+" kills="+bosses);
        var inner=ReloadInner(p);p=ReloadBall(p);p.InnerPower=inner;InnerPower.InnerPowerPolle[uid]=inner;ChiTable.Load(p);
        uint cp=p.Entity.ConquerPoints,points=p.ChiPoints;
        var next=typeof(GeneralData).GetField("ChiValueNext",Hidden);var rate=typeof(GeneralData).GetField("ChiValueRate",Hidden);
        var oldNext=next.GetValue(null);var oldRate=rate.GetValue(null);
        try{
            // Control legal maximum outcomes; the character pays the ordinary point cost.
            next.SetValue(null,(Func<int,int,int>)((low,high)=>high-1));rate.SetValue(null,(Func<int,int,bool>)((chance,total)=>true));
            for(int power=3;power<=4;power++)foreach(ushort action in new ushort[]{Chi.Unlock,Chi.Study}){
                var packet=new Chi(true){Type=action,Mode=(Enums.ChiPowerType)power}.ToArray();
                Writer.WriteUInt64(PacketHandler.ClientSeal,16,packet);PacketHandler.HandlePacket(packet,p);
            }
        }finally{next.SetValue(null,oldNext);rate.SetValue(null,oldRate);}
        Check(!Fenced(p) && p.ChiPowers.Count==4 && p.ChiPowers.All(power=>power.Points==400)
            && p.ChiPoints==points-700 && p.Entity.ConquerPoints==cp,
            "earned second-rebirth and level gate unlock Tiger and Turtle using 700 previously paid Chi points");
        Check(EntityTable.SaveEntity(p),"remaining earned Chi progression saves normally");
        var score=PrestigeRanking.Update(p);inner=ReloadInner(p);p=ReloadBall(p);p.InnerPower=inner;InnerPower.InnerPowerPolle[uid]=inner;
        ChiTable.Load(p);new TitleStorage().Login(p);
        Check(!Fenced(p) && p.ChiPowers.Count==4 && p.ChiPowers.All(power=>power.Points==400)
            && p.ChiPoints==points-700 && p.Entity.ConquerPoints==cp && PrestigeRanking.Update(p).Total==score.Total,
            "earned levels four Chi powers exact points and Prestige survive full reload");
        Kernel.GamePool.Remove(uid);
        try{PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
            Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                "remaining earned Chi matches all offline Prestige categories");
        }finally{Kernel.GamePool[uid]=p;}
        System.Console.WriteLine("Earned four-power Prestige: total="+score.Total+" level="+p.Entity.Level+" additional-bosses="+bosses+" chi-points="+p.ChiPoints);
        VerifyEarnedRestoredEquipment(p);
    }

    static void VerifyEarnedRestoredEquipment(GameState p) {
        uint uid=p.Entity.UID;var offhand=p.Inventory.Objects.Single(i=>i.ID/1000==410 && i.PerfectionLevel==54);
        uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
        ItemHandler.EquipItem(new ItemUsage(true){UID=offhand.UID,dwParam=5,Action=ItemUsage.Mode.EquipItem},p);
        Check(!Fenced(p) && p.Equipment.TryGetItem(offhand.UID)!=null && offhand.Position==5
            && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp,
            "ordinary equip restores the same earned perfected offhand returned by rebirth without another charge");
        int bosses=0;while(p.Entity.Level<140 && bosses<100){EarnPrestigeBounty(p);bosses++;}
        Check(!Fenced(p) && p.Entity.Level==140 && EntityTable.SaveEntity(p),
            "ordinary boss experience earns the stored second-life level recovery: bosses="+bosses+" level="+p.Entity.Level);
        var score=PrestigeRanking.Update(p);var inner=ReloadInner(p);p=ReloadBall(p);p.InnerPower=inner;InnerPower.InnerPowerPolle[uid]=inner;
        ChiTable.Load(p);new TitleStorage().Login(p);
        Check(!Fenced(p) && p.Entity.Level==140 && p.Equipment.TryGetItem(offhand.UID)!=null
            && p.Equipment.TryGetItem(offhand.UID).PerfectionLevel==54 && PrestigeRanking.Update(p).Total==score.Total,
            "earned level recovery and original perfected offhand survive character item and progression reload");
        Kernel.GamePool.Remove(uid);
        try{PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
            Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                "all offline Prestige categories match after earned recovery and offhand equip");
        }finally{Kernel.GamePool[uid]=p;}
        System.Console.WriteLine("Earned restored equipment Prestige: total="+score.Total+" bosses="+bosses+" categories="+string.Join(",",score.Abilities.Abilities.Select(a=>a.Sort+":"+a.Value)));
        VerifyPlusTransactions(Kernel.Maps[1002]);
        VerifyEarnedPlusEquipment(p);
    }

    static readonly Type PlusUpgrades=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");
    static byte[] PlusPacket(uint target,params uint[] materials) {
        var frame=new byte[20+4*materials.Length];Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(2036,2,frame);
        frame[5]=(byte)(materials.Length+1);Writer.WriteUInt32(target,8,frame);
        for(int i=0;i<materials.Length;i++)Writer.WriteUInt32(materials[i],12+4*i,frame);
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void PlusCompose(GameState p,byte[] frame){var request=new Compose(false);request.Deserialize(frame);ItemHandler.ComposePlus(request,p);}
    static long PlusReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid",p.Entity.UID);}
    static void VerifyPlusTransactions(Map town) {
        Call(PlusUpgrades,null,"InitializePlus");
        foreach(string mode in new[]{"success","bound-success","items","receipt","foreign","stale","duplicate","bound","seal","count","short","trade","dead","fake","postcommit"}){
            var p=BallPlayer(town);var target=Pack(p,130009,1);var stone=RuneItem(p,730008,0,2);
            var packet=PlusPacket(target.UID,stone.UID);bool trigger=mode=="items" || mode=="receipt";
            string table=mode=="items"?"items":"revora_plus_compositions";
            if(mode=="foreign")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)stone.UID);
            if(mode=="stale")Query("UPDATE items SET plusprogress=1 WHERE uid=@uid","uid",(long)target.UID);
            if(mode=="duplicate")packet=PlusPacket(target.UID,stone.UID,stone.UID);
            if(mode=="bound"){stone.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)stone.UID);}
            if(mode=="bound-success"){stone.Bound=target.Bound=true;Query("UPDATE items SET bound=1 WHERE entityid=@uid","uid",(long)p.Entity.UID);}
            if(mode=="seal")packet[packet.Length-1]^=1;if(mode=="count")packet[5]++;
            if(mode=="short")Array.Resize(ref packet,12);if(mode=="trade")p.Trade.InTrade=true;
            if(mode=="dead")p.Entity.Hitpoints=0;if(mode=="fake")p.Fake=true;
            if(trigger)Query("CREATE FUNCTION suppress_plus() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_plus BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_plus()");
            var hook=PlusUpgrades.GetField("BeforePlusProjection",Hidden);
            if(mode=="postcommit")hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("controlled projection failure");}));
            try{Parallel.For(0,6,i=>PlusCompose(p,packet));}finally{
                hook.SetValue(null,null);if(trigger)Query("DROP TRIGGER suppress_plus ON "+table+";DROP FUNCTION suppress_plus()");
            }
            bool committed=mode=="success" || mode=="bound-success" || mode=="postcommit";
            Check(Fenced(p)==(trigger || mode=="foreign" || mode=="stale" || mode=="postcommit")
                && PlusReceipts(p)==(committed?1:0) && SavedRune(target,"plus")== (committed?8:0)
                && SavedRune(stone,"stacksize")== (committed?1:2),"plus composition commits one upgrade/debit or remains inert: "+mode);
            if(committed){p=ReloadBall(p);PlusCompose(p,packet);
                Check(!Fenced(p) && PlusReceipts(p)==1 && p.Inventory.Objects.Single(i=>i.UID==target.UID).Plus==8
                    && p.Inventory.Objects.Single(i=>i.UID==stone.UID).StackSize==1,"reconnect restores committed enhancement and suppresses stack replay: "+mode);}
        }
        var capped=BallPlayer(town);var gear=Pack(capped,130009,1);
        var stones=Enumerable.Range(0,5).Select(n=>RuneItem(capped,730008,0)).ToArray();
        PlusCompose(capped,PlusPacket(gear.UID,stones.Select(i=>i.UID).ToArray()));
        Check(!Fenced(capped) && gear.Plus==12 && gear.PlusProgress==0 && stones.Take(4).All(i=>!capped.Inventory.ContainsUID(i.UID))
            && capped.Inventory.ContainsUID(stones[4].UID),"plus cap consumes four needed stones and preserves the unused fifth");
        var zero=BallPlayer(town);var zeroGear=Pack(zero,130009,1);var minor=Pack(zero,130009,1);
        PlusCompose(zero,PlusPacket(zeroGear.UID,minor.UID));
        Check(!Fenced(zero) && zeroGear.Plus==0 && zeroGear.PlusProgress==1 && !zero.Inventory.ContainsUID(minor.UID),
            "ordinary zero-enhancement equipment retains the installed one-point material credit");
    }
    static void VerifyEarnedPlusEquipment(GameState p) {
        Call(PlusUpgrades,null,"InitializePlus");uint uid=p.Entity.UID,cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
        p.LoadItemStats();p.CalculateHPBonus();p.Entity.Hitpoints=p.Entity.MaxHitpoints;
        var targets=new[]{p.Equipment.TryGetItem(1),p.Equipment.TryGetItem(2),p.Equipment.TryGetItem(3)};
        Check(targets.All(i=>i!=null && i.Plus==0 && i.PerfectionLevel==54 && i.Bound),"three earned quest pieces retain original enhancement before paid composition");
        var clock=TokenShop.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);DateTime now=new DateTime(2036,1,1,0,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));
        try{foreach(var target in targets){
            now=now.AddSeconds(2);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();ItemUsage.Process(BuyBook(730008,4,2888).ToArray(),p);
            var stones=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
            if(stones.Length!=4 || stones.Any(i=>i.ID!=730008 || i.Plus!=8))throw new Exception("Earned enhancement stones missing");
            var request=PlusPacket(target.UID,stones.Select(i=>i.UID).ToArray());PlusCompose(p,request);PlusCompose(p,request);
            Check(!Fenced(p) && target.Plus==12 && target.PlusProgress==0 && target.PerfectionLevel==54 && stones.All(i=>!p.Inventory.ContainsUID(i.UID)),
                "ordinary purchased-stone composition reaches +12 on earned gear and replay cannot consume twice: "+target.Position);
        }}finally{clock.SetValue(null,oldClock);}
        new TitleStorage().Login(p);var score=PrestigeRanking.Update(p);
        Check(PlusReceipts(p)==3 && p.Entity.ConquerPoints==cp-314928 && p.Entity.RevoraPoint==rvp && score.Total>=600000
            && OwnedTitles(p).ContainsKey(20262026),"three paid enhancements cross 600000 Prestige with exact 314928 CP cost and earned title");
        TitleRequest(20262026,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20262026 && EntityTable.SaveEntity(p),"ordinary wardrobe request equips the earned 600000 Prestige title");
        var inner=ReloadInner(p);p=ReloadBall(p);p.InnerPower=inner;InnerPower.InnerPowerPolle[uid]=inner;ChiTable.Load(p);new TitleStorage().Login(p);
        Check(!Fenced(p) && p.Entity.EquippedTitle==20262026 && OwnedTitles(p)[20262026] && PrestigeRanking.Update(p).Total==score.Total
            && p.Entity.ConquerPoints==cp-314928 && targets.All(i=>p.Equipment.TryGetItem(i.UID).Plus==12),
            "earned Superman title enhancements exact purse and Prestige survive full reload");
        Kernel.GamePool.Remove(uid);
        try{PrestigeRanking.LoadSavedPlayers();var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==uid);
            Check(offline.Total==score.Total && offline.Abilities.Abilities.Select(a=>a.Value).SequenceEqual(score.Abilities.Abilities.Select(a=>a.Value)),
                "all offline categories preserve the fully earned 600000 Prestige threshold");
        }finally{Kernel.GamePool[uid]=p;}
        System.Console.WriteLine("Earned Superman Prestige: total="+score.Total+" CP-enhancement=314928");
    }

    static readonly Type InnerProgress=typeof(Entity).Assembly.GetType("Revora.Game.Features.InnerPowerProgress");
    static void InnerRequest(GameState p,byte action,uint gong,int length=13) {
        var frame=new byte[length+8];Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2610,2,frame);
        if(length>=13){frame[4]=action;Writer.WriteUInt32(gong,5,frame);}
        Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);PacketHandler.HandlePacket(frame,p);
    }
    static InnerPower ReloadInner(GameState p) {
        var inner=new InnerPower(p.Entity.Name,p.Entity.UID);
        Call(Packs,null,"RecoverPlayer",inner);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.InnerPowerPurchases"),null,"RecoverPlayer",inner);
        return inner;
    }
    static void VerifyInnerProgress(string runtime,Map town) {
        var shop=typeof(Entity).Assembly.GetType("Revora.Game.Features.InnerPowerPurchases");Call(shop,null,"Initialize");
        var rule=InnerPowerTable.Stages[0].NeiGongAtributes[0];
        Check(rule.ID==1 && rule.MaxLevel==5 && rule.ProgressNeiGongValue.All(n=>n==1500),"Inner Power test uses installed Universal Concept A costs");
        string previous=Environment.CurrentDirectory;
        string fixture=Path.GetFullPath(Path.Combine(runtime,"../../obj/inner-progress-file-test"));Directory.CreateDirectory(Path.Combine(fixture,"database"));
        try {
            Environment.CurrentDirectory=fixture;
            var p=WelcomePlayer(town,140,2,15);var book=Pack(p,rule.ItemID,2);var packs=Pack(p,3007110,4);
            ItemHandler.UseItem(packs,p);ItemHandler.UseItem(packs,p);
            p.Entity.RevoraPoint=500;BuyInner(p,1);
            Check(p.Account.State==AccountTable.AccountState.Player && p.InnerPower.Potency==4000,"ordinary supplied eligibility uses real pack and NPC potency award ledgers");
            InnerRequest(p,4,rule.ID,12);InnerRequest(p,4,rule.ID,65);InnerRequest(p,4,257);
            Check(book.StackSize==2 && !p.InnerPower.Stages[0].NeiGongs[0].Unlocked,"short oversized and aliased gong requests cannot unlock");
            Parallel.For(0,12,n=>InnerRequest(p,4,rule.ID));
            Check(!Fenced(p) && book.StackSize==1 && SavedRune(book,"stacksize")==1 && p.InnerPower.Stages[0].NeiGongs[0].Unlocked
                && Scalar("SELECT revision FROM revora_inner_progress WHERE entityid=@uid",p.Entity.UID)==1,"concurrent unlock consumes exactly one owned book with one durable revision");
            InnerRequest(p,3,rule.ID);InnerRequest(p,3,rule.ID);
            Check(!Fenced(p) && p.InnerPower.Potency==1000 && p.InnerPower.Stages[0].NeiGongs[0].level==2
                && Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",p.Entity.UID)==3000,"two paid training operations spend exact potency and persist both levels");
            InnerRequest(p,3,rule.ID);
            Check(p.InnerPower.Potency==1000 && p.InnerPower.Stages[0].NeiGongs[0].level==2,"insufficient potency does not advance training");
            var cold=ReloadInner(p);Call(Packs,null,"RecoverPlayer",cold);Call(shop,null,"RecoverPlayer",cold);
            Check(cold.Potency==1000 && cold.Stages[0].NeiGongs[0].level==2 && InnerField(cold,"AppliedTrainingPoints")==3000,
                "cold ledger recovery without a progression file restores levels and spends once after both award sources");
            ItemHandler.UseItem(packs,p);
            InnerProgress.GetField("AfterCommit",Hidden).SetValue(null,(Action<GameState>)(_=>{throw new InvalidOperationException("projection");}));
            try{InnerRequest(p,3,rule.ID);}finally{InnerProgress.GetField("AfterCommit",Hidden).SetValue(null,null);}
            Check(Fenced(p) && p.InnerPower.Potency==2000 && p.InnerPower.Stages[0].NeiGongs[0].level==2
                && Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",p.Entity.UID)==4500,"postcommit projection failure fences the session while retaining durable training");
            cold=ReloadInner(p);p=ReloadBall(p);p.InnerPower=cold;InnerPower.InnerPowerPolle[p.Entity.UID]=cold;
            Check(!Fenced(p) && cold.Potency==500 && cold.Stages[0].NeiGongs[0].level==3 && p.Inventory.Objects.Single(i=>i.UID==book.UID).StackSize==1,
                "inventory reconnect and cold progression recovery retain one book and exact committed third level");
            InnerPowerTable.Save();InnerPower removed;InnerPower.InnerPowerPolle.TryRemove(p.Entity.UID,out removed);InnerPowerTable.Load();
            var saved=InnerPower.InnerPowerPolle[p.Entity.UID];Call(Packs,null,"RecoverPlayer",saved);Call(shop,null,"RecoverPlayer",saved);
            Check(saved.Potency==500 && saved.Stages[0].NeiGongs[0].level==3 && InnerField(saved,"AppliedTrainingPoints")==4500,
                "new file spending checkpoint prevents a second debit after saved-file restart");
            p.InnerPower=saved;InnerRequest(p,4,rule.ID);
            Check(p.Inventory.Objects.Single(i=>i.UID==book.UID).StackSize==1,"unlock replay after file and database reload retains spare book");
            foreach(string fault in new[]{"item","progress","owner","stale"}) {
                var q=WelcomePlayer(town,140,2,15);var b=Pack(q,rule.ItemID,1);
                if(fault=="item" || fault=="progress") {
                    Query("CREATE FUNCTION inner_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
                    Query(fault=="item"?"CREATE TRIGGER inner_fault BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION inner_fault()"
                        :"CREATE TRIGGER inner_fault BEFORE INSERT ON revora_inner_progress FOR EACH ROW EXECUTE FUNCTION inner_fault()");
                }
                if(fault=="owner")Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)b.UID);
                if(fault=="stale")Query("INSERT INTO revora_potency_awards(entityid,points) VALUES(@uid,400)","uid",(long)q.Entity.UID);
                try{InnerRequest(q,4,rule.ID);}finally{
                    if(fault=="item" || fault=="progress") {Query(fault=="item"?"DROP TRIGGER inner_fault ON items":"DROP TRIGGER inner_fault ON revora_inner_progress");Query("DROP FUNCTION inner_fault()");}
                }
                Check(Fenced(q) && !q.InnerPower.Stages[0].NeiGongs[0].Unlocked && Scalar("SELECT count(*) FROM items WHERE uid=@uid",b.UID)==1
                    && Scalar("SELECT count(*) FROM revora_inner_progress WHERE entityid=@uid",q.Entity.UID)==0,"unlock rolls back both state and item on "+fault);
            }
            var denied=WelcomePlayer(town,140,1,15);var deniedBook=Pack(denied,rule.ItemID,1);InnerRequest(denied,4,rule.ID);
            Check(!Fenced(denied) && denied.Inventory.ContainsUID(deniedBook.UID) && !denied.InnerPower.Stages[0].NeiGongs[0].Unlocked,
                "first-reborn character cannot bypass installed second-rebirth Inner Power admission");
            var unlockCrash=WelcomePlayer(town,140,2,15);var crashBook=Pack(unlockCrash,rule.ItemID,1);
            InnerProgress.GetField("AfterCommit",Hidden).SetValue(null,(Action<GameState>)(_=>{throw new InvalidOperationException("projection");}));
            try{InnerRequest(unlockCrash,4,rule.ID);}finally{InnerProgress.GetField("AfterCommit",Hidden).SetValue(null,null);}
            var crashCold=ReloadInner(unlockCrash);
            Check(Fenced(unlockCrash) && !unlockCrash.InnerPower.Stages[0].NeiGongs[0].Unlocked && crashCold.Stages[0].NeiGongs[0].Unlocked
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",crashBook.UID)==0,"committed unlock recovers after interruption before memory update");
            var training=WelcomePlayer(town,140,2,15);var trainingBook=Pack(training,rule.ItemID,1);var trainingPacks=Pack(training,3007110,10);
            for(int n=0;n<10;n++)ItemHandler.UseItem(trainingPacks,training);
            InnerRequest(training,4,rule.ID);
            Query("CREATE FUNCTION inner_train_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER inner_train_fault BEFORE UPDATE ON revora_inner_progress FOR EACH ROW EXECUTE FUNCTION inner_train_fault()");
            try{InnerRequest(training,3,rule.ID);}finally{Query("DROP TRIGGER inner_train_fault ON revora_inner_progress");Query("DROP FUNCTION inner_train_fault()");}
            Check(Fenced(training) && training.InnerPower.Potency==10000 && training.InnerPower.Stages[0].NeiGongs[0].level==0
                && Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",training.Entity.UID)==0,"training write failure preserves both potency and level");
            var trainingCold=ReloadInner(training);training=ReloadBall(training);training.InnerPower=trainingCold;InnerPower.InnerPowerPolle[training.Entity.UID]=trainingCold;
            Parallel.For(0,12,n=>InnerRequest(training,3,rule.ID));
            Check(!Fenced(training) && training.InnerPower.Potency==2500 && training.InnerPower.Stages[0].NeiGongs[0].level==5
                && training.InnerPower.Stages[0].NeiGongs[0].Score==100 && Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",training.Entity.UID)==7500,
                "concurrent training commits at most the five legal paid levels and never overspends beyond max");
            var online=PrestigeRanking.Update(training);uint trainingUid=training.Entity.UID;
            Check(online.Abilities.Abilities.Single(a=>a.Sort==AbilitiesSort.InnerPower).Value>0,"committed Inner Power contributes to ordinary Prestige");
            Query("CREATE TABLE IF NOT EXISTS subclasses (LIKE public.subclasses INCLUDING ALL)");EntityTable.SaveEntity(training);
            Kernel.GamePool.Remove(trainingUid);InnerPower.InnerPowerPolle.TryRemove(trainingUid,out removed);
            try{
                Call(InnerProgress,null,"RecoverAll");PrestigeRanking.LoadSavedPlayers();
                var offline=PrestigeRanking.Snapshot(null).Single(e=>e.Profile.UID==trainingUid);
                Check(offline.Abilities.Abilities.Single(a=>a.Sort==AbilitiesSort.InnerPower).Value==online.Abilities.Abilities.Single(a=>a.Sort==AbilitiesSort.InnerPower).Value
                    && InnerPower.InnerPowerPolle[trainingUid].Potency==2500,"startup discovers ledger-only character and offline Prestige matches committed training");
            }finally{Kernel.GamePool[trainingUid]=training;}
            var stale=WelcomePlayer(town,140,2,15);var staleBook=Pack(stale,rule.ItemID,1);var stalePacks=Pack(stale,3007110,2);
            ItemHandler.UseItem(stalePacks,stale);ItemHandler.UseItem(stalePacks,stale);InnerRequest(stale,4,rule.ID);
            Query("UPDATE revora_inner_progress SET revision=revision+1 WHERE entityid=@uid","uid",(long)stale.Entity.UID);
            InnerRequest(stale,3,rule.ID);
            Check(Fenced(stale) && stale.InnerPower.Potency==2000 && Scalar("SELECT spent FROM revora_inner_progress WHERE entityid=@uid",stale.Entity.UID)==0,
                "stale progression revision cannot spend potency or overwrite newer progress");
            // Legacy files have no spending field; absent field reads as zero.
            var legacy=WelcomePlayer(town,140,2,15);string line=legacy.InnerPower.ToString();line=line.Substring(0,line.LastIndexOf('/'));
            File.WriteAllLines(Path.Combine(fixture,"database","InnerPower.txt"),new[]{"Count=1",line});
            InnerPower.InnerPowerPolle.TryRemove(legacy.Entity.UID,out removed);InnerPowerTable.Load();
            Check(InnerField(InnerPower.InnerPowerPolle[legacy.Entity.UID],"AppliedTrainingPoints")==0,"legacy file without training checkpoint still loads");
        }finally{Environment.CurrentDirectory=previous;InnerProgress.GetField("AfterCommit",Hidden).SetValue(null,null);}
    }

    static void VerifyHomecomingPublicTiers(Map map)
    {
        Query(@"CREATE TABLE IF NOT EXISTS accounts(username text PRIMARY KEY,password text NOT NULL,ip text DEFAULT '',
entityid bigint DEFAULT 0,lastcheck bigint DEFAULT 0,state smallint DEFAULT 2,email text DEFAULT '',idreferido bigint DEFAULT 0)");
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity",true);Call(activity,null,"Initialize");
        var previous=(Func<DateTime>)TitleRules.GetField("Clock",Hidden).GetValue(null);
        DateTime titleNow=DateTime.UtcNow;TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>titleNow));
        int[] all={20282028,20302030,20292029,20312031};
        try {
            for(byte vip=0;vip<=7;vip++) {
                var p=StoredProgressionPlayer(map);uint uid=p.Entity.UID;string name="public-return-"+uid;
                Query("INSERT INTO accounts(username,password,entityid,state) VALUES(@name,'!synthetic-unusable!',@uid,2)","name",name,"uid",(long)uid);
                // Eligibility profile is a fixture prerequisite; no return or title receipt is supplied.
                Query("UPDATE entities SET level=@level,reborn=@reborn,viplevel=@vip,vipdays='0',vipleveldays=0,lastresettime=0 WHERE uid=@uid",
                    "level",vip%2==0?120:119,"reborn",vip%2==0?0:1,"vip",(int)vip,"uid",(long)uid);
                p.Account=new AccountTable(name);
                Check(p.Account.exists && p.Account.State==AccountTable.AccountState.Player && p.Account.EntityID==uid,
                    "Homecoming tier uses an ordinary stored account: "+vip);
                Call(activity,null,"Authenticated",p.Account);Guid presence=Guid.NewGuid();Call(activity,null,"SessionOpened",p.Account,presence);
                Call(activity,null,"SessionClosed",presence);
                Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",uid)==0,
                    "ordinary first authentication/session cannot fabricate a return: "+vip);
                // Simulate elapsed inactivity on real producer rows in the disposable schema.
                Query("UPDATE revora_account_activity SET first_auth=now()-interval '40 days',last_auth=now()-interval '31 days' WHERE account_name=@name","name",name);
                Query("UPDATE revora_account_sessions SET opened_at=now()-interval '40 days',closed_at=now()-interval '31 days' WHERE session_id=@session","session",presence);
                Parallel.For(0,4,n=>Call(activity,null,"Authenticated",new AccountTable(name.ToUpperInvariant())));
                int key=vip==0?all[0]:vip<=4?all[1]:vip<=6?all[2]:all[3];
                Check(Scalar("SELECT count(*) FROM revora_account_returns WHERE character_uid=@uid",uid)==1
                    && Scalar("SELECT title_key FROM revora_account_returns WHERE character_uid=@uid",uid)==key,
                    "qualifying authentication freezes exactly one correct VIP-tier return: "+vip);
                Query("UPDATE entities SET viplevel=@vip,level=119,reborn=0 WHERE uid=@uid","vip",(int)(7-vip),"uid",(long)uid);
                p=ReloadBall(p);p.Account=new AccountTable(name);titleNow=DateTime.UtcNow;new TitleStorage().Login(p);
                TitleRequest(key,TitleStorage.Action.Equip).Handle(p);
                Check(OwnedTitles(p).ContainsKey(key) && all.Where(k=>k!=key).All(k=>!OwnedTitles(p).ContainsKey(k)) && p.Entity.EquippedTitle==key,
                    "normal character and wardrobe login honors frozen tier despite later profile changes: "+vip);
                DateTime expiry;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT expires_at FROM revora_account_returns WHERE character_uid=@uid",c)) {
                    q.Parameters.AddWithValue("uid",(long)uid);using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Return missing");expiry=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
                }}
                Check(Scalar(@"SELECT count(*) FROM revora_event_awards a JOIN revora_account_returns r ON r.character_uid=a.uid
WHERE a.uid=@uid AND a.source='homecoming-revora-v1' AND a.title_key=r.title_key AND a.earned_at=r.returned_at
AND a.expires_at=r.expires_at AND r.expires_at=r.returned_at+interval '720 hours' AND r.award_recorded",uid)==1,
                    "wardrobe grant keeps exact return timestamp, tier and thirty-day deadline: "+vip);
                p=ReloadBall(p);p.Account=new AccountTable(name);new TitleStorage().Login(p);
                Check(p.Entity.EquippedTitle==key && OwnedTitles(p).ContainsKey(key),"Homecoming tier and selection survive a second full reload: "+vip);
                titleNow=expiry;new TitleStorage().Login(p);
                Check(!OwnedTitles(p).ContainsKey(key) && p.Entity.EquippedTitle==0,"Homecoming tier expires and unequips at its original deadline: "+vip);
                new TitleStorage().Login(p);
                Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='homecoming-revora-v1'",uid)==1
                    && !OwnedTitles(p).ContainsKey(key),"expired receipt cannot extend or regrant a Homecoming title: "+vip);
                titleNow=DateTime.UtcNow;
            }
        } finally {TitleRules.GetField("Clock",Hidden).SetValue(null,previous);}
    }

    static void VerifyMrMs2016PublicEarning(Map map)
    {
        var eventType=typeof(Entity).Assembly.GetType("Revora.Game.Features.MrMs2016",true);
        DateTime now=new DateTime(2036,1,15,12,0,0,DateTimeKind.Utc);
        var titleClock=(Func<DateTime>)TitleRules.GetField("Clock",Hidden).GetValue(null);
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        eventType.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Revora.Interfaces.INpc original;map.Npcs.TryGetValue(380,out original);
        try {
            Call(eventType,null,"Initialize");Query("DELETE FROM revora_mrms_calendar");Call(eventType,null,"Initialize");
            var entrants=new GameState[4];
            for(int i=0;i<4;i++) {
                entrants[i]=ProgressPlayer(map,"");entrants[i].Entity.DailySignDays=new byte[0];entrants[i].Entity.Body=(ushort)(i<2?2001:1003);
                EntityTable.SaveEntity(entrants[i]);
            }
            var actor=new NpcSpawn{UID=380,MapID=map.ID,X=entrants[0].Entity.X,Y=entrants[0].Entity.Y};map.Npcs[380]=actor;
            Action<GameState,byte> talk=(p,option)=>{p.ActiveNpc=380;p.Entity.X=actor.X;p.Entity.Y=actor.Y;Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=option},p);};
            foreach(var p in entrants){talk(p,0);talk(p,220);talk(p,222);}
            Check(Scalar("SELECT count(*) FROM revora_mrms_entrants WHERE season='2036-01'",0)==4,
                "ordinary guild-envoy dialogue registers both Mr and Ms contestants without supplied entrant rows");
            Action<GameState,GameState,byte> vote=(voter,candidate,flower)=>{
                talk(voter,flower);var ids=(uint[])Call(eventType,null,"PinnedCandidates",voter);
                int index=Array.IndexOf(ids,candidate.Entity.UID);if(index<0)throw new Exception("Public ballot candidate missing");
                talk(voter,(byte)(226+index));
            };
            var voters=new[]{ProgressPlayer(map,""),ProgressPlayer(map,""),ProgressPlayer(map,"")};
            foreach(int first in new[]{0,2}) {
                vote(voters[0],entrants[first],224);vote(voters[1],entrants[first],224);
                vote(voters[0],entrants[first],225);vote(voters[2],entrants[first+1],224);
            }
            Check(Scalar("SELECT count(*) FROM revora_mrms_votes WHERE season='2036-01'",0)==8,
                "ordinary pinned NPC choices record separate orchid and tulip support for both contests");
            Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE season='2036-01'",0)==0,
                "registration and voting cannot award a wardrobe prize before judging");
            now=new DateTime(2036,2,1,8,0,0,DateTimeKind.Utc);Call(eventType,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE season='2036-01'",0)==6,
                "monthly tick freezes actual votes into all five supported reward identities");
            int[] keys={20202020,20212021,20232023,20242024};
            for(int i=0;i<4;i++) {
                var p=entrants[i];new TitleStorage().Login(p);TitleRequest(keys[i],TitleStorage.Action.Equip).Handle(p);
                if(i==0 || i==2)TitleRequest(60056005,TitleStorage.Action.Equip).Handle(p);
                Check(OwnedTitles(p).ContainsKey(keys[i]) && p.Entity.EquippedTitle==keys[i]
                    && (i%2!=0 || OwnedTitles(p).ContainsKey(60056005) && p.Entity.EquippedWing==60056005),
                    "ordinary monthly winner receives and equips exact permanent rewards: "+keys[i]);
                p=ReloadBall(p);new TitleStorage().Login(p);
                Check(OwnedTitles(p).ContainsKey(keys[i]) && p.Entity.EquippedTitle==keys[i]
                    && (i%2!=0 || p.Entity.EquippedWing==60056005),
                    "earned monthly title and wing survive production reload: "+keys[i]);
            }
            Call(eventType,null,"Tick");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE source='mrms-2016-revora-v1' AND source_key LIKE '2036-01:%' AND expires_at IS NULL",0)==6,
                "monthly replay preserves exactly six permanent earned grants");
        } finally {
            TitleRules.GetField("Clock",Hidden).SetValue(null,titleClock);
            eventType.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
            if(original==null)map.Npcs.Remove(380);else map.Npcs[380]=original;
        }
    }

    static void VerifyMrMs2016(Map map)
    {
        var mrms=typeof(Entity).Assembly.GetType("Revora.Game.Features.MrMs2016");
        Call(mrms,null,"Initialize");
        const string season="2016-03";
        DateTime closed=DateTime.UtcNow.AddMinutes(-1);
        closed=new DateTime(closed.Ticks-closed.Ticks%10,DateTimeKind.Utc);
        var fairy=ProgressPlayer(map,"");fairy.Entity.Body=2001;
        var goddess=ProgressPlayer(map,"");goddess.Entity.Body=2001;
        var scholar=ProgressPlayer(map,"");scholar.Entity.Body=1003;
        var voterA=ProgressPlayer(map,"");var voterB=ProgressPlayer(map,"");var voterC=ProgressPlayer(map,"");var voterD=ProgressPlayer(map,"");
        Call(mrms,null,"Register",season,fairy.Entity.UID,"ms",(ushort)2001);
        Call(mrms,null,"Register",season,goddess.Entity.UID,"ms",(ushort)2001);
        Call(mrms,null,"Register",season,scholar.Entity.UID,"mr",(ushort)1003);
        bool gender=false;try{Call(mrms,null,"Register",season,scholar.Entity.UID,"ms",(ushort)1003);}catch(InvalidOperationException){gender=true;}
        Check(gender,"male character cannot enter Ms Conquer");
        Call(mrms,null,"Vote",season,voterA.Entity.UID,"orchid","ms",fairy.Entity.UID);
        Call(mrms,null,"Vote",season,voterD.Entity.UID,"orchid","ms",fairy.Entity.UID);
        Call(mrms,null,"Vote",season,voterA.Entity.UID,"tulip","ms",fairy.Entity.UID);
        Call(mrms,null,"Vote",season,voterB.Entity.UID,"orchid","ms",goddess.Entity.UID);
        Call(mrms,null,"Vote",season,voterC.Entity.UID,"orchid","mr",scholar.Entity.UID);
        Call(mrms,null,"Vote",season,voterC.Entity.UID,"tulip","mr",scholar.Entity.UID);
        bool twice=false;try{Call(mrms,null,"Vote",season,voterA.Entity.UID,"orchid","ms",goddess.Entity.UID);}catch(InvalidOperationException){twice=true;}
        Check(twice,"a voter cannot recast the same flower category");
        bool self=false;try{Call(mrms,null,"Vote",season,fairy.Entity.UID,"orchid","ms",fairy.Entity.UID);}catch(Exception){self=true;}
        Check(self,"a contestant cannot vote for themselves");
        Check((bool)Call(mrms,null,"Freeze",season,closed),"Mr./Ms. freeze commits local standings");
        Check(!(bool)Call(mrms,null,"Freeze",season,closed),"replaying a frozen Mr./Ms. season does not rewrite awards");
        int freezes=0;Parallel.For(0,4,i=>{if((bool)Call(mrms,null,"Freeze",season,closed))System.Threading.Interlocked.Increment(ref freezes);});
        Check(freezes==0,"concurrent Mr./Ms. freeze after commit does not duplicate results");
        var lateVoter=ProgressPlayer(map,"");
        bool late=false;try{Call(mrms,null,"Vote",season,lateVoter.Entity.UID,"orchid","ms",fairy.Entity.UID);}catch(InvalidOperationException){late=true;}
        Check(late && Scalar("SELECT count(*) FROM revora_mrms_votes WHERE season='2016-03' AND voter="+lateVoter.Entity.UID,0)==0,
            "a voter cannot record a ballot after freeze");
        const string race="2033-05";
        DateTime raceClose=DateTime.UtcNow.AddMinutes(-1);
        raceClose=new DateTime(raceClose.Ticks-raceClose.Ticks%10,DateTimeKind.Utc);
        var raced=ProgressPlayer(map,"");raced.Entity.Body=2001;
        Call(mrms,null,"Register",race,raced.Entity.UID,"ms",(ushort)2001);
        var raceVoter=ProgressPlayer(map,"");
        int raceFrozen=0;
        Parallel.For(0,8,i=>{
            if((i&1)==0){if((bool)Call(mrms,null,"Freeze",race,raceClose))System.Threading.Interlocked.Increment(ref raceFrozen);}
            else{try{Call(mrms,null,"Vote",race,raceVoter.Entity.UID,"orchid","ms",raced.Entity.UID);}catch(InvalidOperationException){}}
        });
        Check(raceFrozen==1,"one freeze commits against concurrent votes");
        long stored=Scalar("SELECT count(*) FROM revora_mrms_votes WHERE season='2033-05'",0);
        long fairyCount=Scalar("SELECT count(*) FROM revora_mrms_awards WHERE season='2033-05' AND title_key=20202020",0);
        Check((stored==0 && fairyCount==0) || (stored==1 && fairyCount==1),
            "a vote cannot commit after the frozen snapshot");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND title_key=20202020",fairy.Entity.UID)==1 &&
            Scalar("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND title_key=60056005",fairy.Entity.UID)==1,
            "orchid Ms winner receives Fairy and tulip winner receives Fairy Wings");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND title_key=20212021",goddess.Entity.UID)==1,
            "second-place Ms receives Goddess");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND title_key=20232023",scholar.Entity.UID)==1 &&
            Scalar("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND title_key=60056005",scholar.Entity.UID)==1,
            "orchid Mr winner receives Scholar and tulip winner receives Fairy Wings");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE title_key IN (20222022,20252025)",0)==0,
            "seasons with fewer than nine ranked candidates grant neither Beauty nor Wise");
        Query("CREATE FUNCTION reject_mrms() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_mrms BEFORE UPDATE ON revora_mrms_awards FOR EACH ROW EXECUTE FUNCTION reject_mrms()");
        bool failed=false;try{EvidenceRefresh(fairy);}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER reject_mrms ON revora_mrms_awards; DROP FUNCTION reject_mrms()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='mrms-2016-revora-v1'",fairy.Entity.UID)==0,
            "failed Mr./Ms. acknowledgement rolls back the wardrobe grant");
        Parallel.For(0,4,i=>Call(EvidenceType,null,"Read",fairy.Entity.UID,"",false,new int[0],closed.AddSeconds(1)));
        EvidenceRefresh(fairy);new TitleStorage().Login(fairy);RefreshTitles(fairy);
        EvidenceRefresh(goddess);new TitleStorage().Login(goddess);RefreshTitles(goddess);
        EvidenceRefresh(scholar);new TitleStorage().Login(scholar);RefreshTitles(scholar);
        Check(OwnedTitles(fairy).ContainsKey(20202020) && OwnedTitles(fairy).ContainsKey(60056005) && !OwnedTitles(fairy).ContainsKey(20212021),
            "ordinary Ms orchid/tulip winner owns Fairy and Fairy Wings");
        Check(OwnedTitles(goddess).ContainsKey(20212021) && !OwnedTitles(goddess).ContainsKey(20202020),
            "ordinary second Ms owns Goddess only");
        Check(OwnedTitles(scholar).ContainsKey(20232023) && OwnedTitles(scholar).ContainsKey(60056005),
            "ordinary Mr orchid/tulip winner owns Scholar and Fairy Wings");
        TitleRequest(20202020,TitleStorage.Action.Equip).Handle(fairy);
        TitleRequest(60056005,TitleStorage.Action.Equip).Handle(fairy);
        Check(fairy.Entity.EquippedTitle==20202020 && fairy.Entity.EquippedWing==60056005,"ordinary winner can equip Fairy and Fairy Wings");
        fairy.Entity.MyAchievement=new Achievement(fairy.Entity);fairy.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(fairy);
        Check(OwnedTitles(fairy).ContainsKey(20202020) && OwnedTitles(fairy).ContainsKey(60056005) && fairy.Entity.EquippedTitle==20202020 && fairy.Entity.EquippedWing==60056005,
            "Mr./Ms. awards survive database reload");
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='mrms-2016-revora-v1' AND expires_at IS NULL",fairy.Entity.UID)==2,
            "Mr./Ms. 2016 wardrobe awards are permanent");
        const string tied="2016-04";
        DateTime tiedClose=DateTime.UtcNow.AddMinutes(-1);
        tiedClose=new DateTime(tiedClose.Ticks-tiedClose.Ticks%10,DateTimeKind.Utc);
        var lead=ProgressPlayer(map,"");lead.Entity.Body=2001;
        Call(mrms,null,"Register",tied,lead.Entity.UID,"ms",(ushort)2001);
        var tiedMs=new GameState[8];
        for(int i=0;i<8;i++){tiedMs[i]=ProgressPlayer(map,"");tiedMs[i].Entity.Body=2001;Call(mrms,null,"Register",tied,tiedMs[i].Entity.UID,"ms",(ushort)2001);}
        var extra=new GameState[3];
        for(int i=0;i<3;i++){extra[i]=ProgressPlayer(map,"");Call(mrms,null,"Vote",tied,extra[i].Entity.UID,"orchid","ms",lead.Entity.UID);}
        for(int i=0;i<8;i++) Call(mrms,null,"Vote",tied,tiedMs[i].Entity.UID,"orchid","ms",tiedMs[(i+1)%8].Entity.UID);
        Check((bool)Call(mrms,null,"Freeze",tied,tiedClose),"tied Mr./Ms. freeze uses deterministic character order");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE season='2016-04' AND title_key=20212021",0)==7,
            "at most seven Goddess awards are granted when eight contestants tie behind first");
        Check(Scalar("SELECT count(*) FROM revora_mrms_awards WHERE season='2016-04' AND title_key=20202020",0)==1,
            "orchid ties resolve to one Fairy by lowest character number");
        DateTime month=new DateTime(2034,1,15,12,0,0,DateTimeKind.Utc);
        mrms.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>month));
        try {
            Query("DELETE FROM revora_mrms_calendar");Call(mrms,null,"Initialize");
            Check(Scalar("SELECT count(*) FROM revora_mrms_calendar WHERE next_close='2034-02-01T08:00:00Z'::timestamptz",0)==1,
                "Mr./Ms. calendar starts at the next Pacific month close");
            Check(!(bool)Call(mrms,null,"ProcessDue"),"Mr./Ms. calendar does not freeze before close");
            month=new DateTime(2034,2,1,8,0,0,DateTimeKind.Utc);
            int advanced=0;Parallel.For(0,4,i=>{if((bool)Call(mrms,null,"ProcessDue"))System.Threading.Interlocked.Increment(ref advanced);});
            Check(advanced==1 && Scalar("SELECT count(*) FROM revora_mrms_seasons WHERE season='2034-01' AND closed_at!='infinity'",0)==1,
                "Mr./Ms. coordinator freezes the elapsed month once");
        } finally {mrms.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid FROM public.npcs WHERE id=380",c))
        using(var r=q.ExecuteReader()) Check(r.Read() && Convert.ToInt32(r.GetValue(0))==1002,"Twin City guild envoy 380 is present for Mr./Ms. dialogue");}
        bool had=map.Npcs.ContainsKey(380);var saved=had?map.Npcs[380]:null;uint active=fairy.ActiveNpc;
        ushort x=fairy.Entity.X,y=fairy.Entity.Y;
        try {
            map.Npcs[380]=new NpcSpawn{UID=380,MapID=map.ID,X=x,Y=y};fairy.ActiveNpc=380;
            string rules=(string)Call(mrms,null,"Page",fairy,(byte)221);
            Check(rules.Contains("orchid") && rules.Contains("tulip") && rules.Contains("character number") && rules.Length<400,
                "ordinary nearby player can read brief Mr./Ms. rules");
            Check((bool)Call(mrms,null,"HandleNpc",fairy,new NpcRequest(true){NpcID=380,OptionID=221}),
                "Twin City guild envoy serves Mr./Ms. dialogue");
            fairy.ActiveNpc=381;
            Check(Call(mrms,null,"Page",fairy,(byte)221)==null,"wrong active NPC cannot serve Mr./Ms. dialogue");
        } finally {fairy.ActiveNpc=active;if(had)map.Npcs[380]=saved;else map.Npcs.Remove(380);}
        DateTime voteAt=new DateTime(2035,6,15,12,0,0,DateTimeKind.Utc);
        mrms.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>voteAt));
        try {
            var pool=new GameState[10];
            for(int i=0;i<10;i++){pool[i]=ProgressPlayer(map,"");pool[i].Entity.Body=2001;Call(mrms,null,"Register","2035-06",pool[i].Entity.UID,"ms",(ushort)2001);}
            var voter=ProgressPlayer(map,"");var voter2=ProgressPlayer(map,"");
            uint first=(uint)Scalar("SELECT min(uid) FROM revora_mrms_entrants WHERE season='2035-06'",0);
            uint ninth=(uint)Scalar("SELECT uid FROM revora_mrms_entrants WHERE season='2035-06' ORDER BY uid OFFSET 8 LIMIT 1",0);
            bool hadVote=map.Npcs.ContainsKey(380);var savedVote=hadVote?map.Npcs[380]:null;
            try {
                map.Npcs[380]=new NpcSpawn{UID=380,MapID=map.ID,X=voter2.Entity.X,Y=voter2.Entity.Y};voter.ActiveNpc=380;voter2.ActiveNpc=380;
                Check(Scalar("SELECT count(*) FROM revora_mrms_entrants WHERE season='2035-06'",0)==10,
                    "ten contestants are entered for the HandleNpc vote season");
                Check((bool)Call(mrms,null,"HandleNpc",voter2,new NpcRequest(true){NpcID=380,OptionID=224}),
                    "orchid choice opens a contestant page");
                var menu=(System.Collections.Generic.List<Tuple<string,byte>>)Call(mrms,null,"LastMenu",voter2);
                string ids=menu==null?"null":string.Join(",",menu.ConvertAll(o=>((int)o.Item2).ToString()).ToArray());
                Check(menu!=null && menu.Exists(o=>o.Item2==226) && menu.Exists(o=>o.Item2==233) && menu.Exists(o=>o.Item2==234),
                    "flower page emits eight candidate options and a more-contestants control ["+ids+"]");
                uint[] page1=(uint[])Call(mrms,null,"PinnedCandidates",voter2);
                Check(page1.Length==8 && page1[0]==first,"first page pins the lowest eight character numbers");
                map.Npcs[380].X=voter.Entity.X;map.Npcs[380].Y=voter.Entity.Y;
                Check((bool)Call(mrms,null,"HandleNpc",voter,new NpcRequest(true){NpcID=380,OptionID=224}),
                    "first voter pins the original first eight contestants");
                map.Npcs[380].X=voter2.Entity.X;map.Npcs[380].Y=voter2.Entity.Y;
                uint spoiler=11;
                while(spoiler<first && Scalar("SELECT count(*) FROM entities WHERE uid=@uid",spoiler)!=0) spoiler++;
                Check(spoiler<first,"a lower unused character number exists for the intervening registration");
                Query("INSERT INTO entities(uid,name,unionexploits) VALUES(@uid,'spoiler',0)","uid",(long)spoiler);
                Call(mrms,null,"Register","2035-06",spoiler,"ms",(ushort)2001);
                Check((bool)Call(mrms,null,"HandleNpc",voter2,new NpcRequest(true){NpcID=380,OptionID=234}),
                    "second page lists remaining contestants");
                var page2=(System.Collections.Generic.List<Tuple<string,byte>>)Call(mrms,null,"LastMenu",voter2);
                Check(page2.Exists(o=>o.Item2==226) && page2.Exists(o=>o.Item2==235) && !page2.Exists(o=>o.Item2==234),
                    "the second page has remaining contestants and a previous-contestants control");
                Check(((uint[])Call(mrms,null,"PinnedCandidates",voter2))[0]==ninth,
                    "second page pins contestants beyond the first eight");
                Check((bool)Call(mrms,null,"HandleNpc",voter2,new NpcRequest(true){NpcID=380,OptionID=235}),
                    "previous page restores the first eight contestants");
                uint[] restored=(uint[])Call(mrms,null,"PinnedCandidates",voter2);
                var back=(System.Collections.Generic.List<Tuple<string,byte>>)Call(mrms,null,"LastMenu",voter2);
                Check(restored.Length==8 && restored[0]==first && restored[0]!=spoiler && Enumerable.SequenceEqual(restored,page1),
                    "previous page restores the original pinned uids");
                Check(back.Exists(o=>o.Item2==226) && back.Exists(o=>o.Item2==234) && !back.Exists(o=>o.Item2==235),
                    "restored first page offers more contestants without a previous control");
                Check((bool)Call(mrms,null,"HandleNpc",voter2,new NpcRequest(true){NpcID=380,OptionID=234}),
                    "forward pagination after previous returns to the remaining contestants");
                Check((bool)Call(mrms,null,"HandleNpc",voter2,new NpcRequest(true){NpcID=380,OptionID=226}),
                    "second-page first option records a vote");
                Check(Scalar("SELECT candidate FROM revora_mrms_votes WHERE season='2035-06' AND voter="+voter2.Entity.UID+" AND flower='orchid'",0)==ninth,
                    "pagination reaches contestants beyond the first eight");
                map.Npcs[380].X=voter.Entity.X;map.Npcs[380].Y=voter.Entity.Y;
                Check((bool)Call(mrms,null,"HandleNpc",voter,new NpcRequest(true){NpcID=380,OptionID=226}),
                    "pinned first option still records a vote after a lower-uid registration");
                Check(Scalar("SELECT candidate FROM revora_mrms_votes WHERE season='2035-06' AND voter="+voter.Entity.UID+" AND flower='orchid'",0)==first,
                    "HandleNpc vote uses the displayed uid rather than a live offset");
                var voter3=ProgressPlayer(map,"");
                map.Npcs[380].X=voter3.Entity.X;map.Npcs[380].Y=voter3.Entity.Y;voter3.ActiveNpc=380;
                Check((bool)Call(mrms,null,"HandleNpc",voter3,new NpcRequest(true){NpcID=380,OptionID=224}),
                    "a later voter can open the June contestant page");
                voteAt=new DateTime(2035,7,1,7,0,0,DateTimeKind.Utc);
                Check((string)Call(mrms,null,"SeasonOf",voteAt)=="2035-07","July midnight Pacific is the next Mr./Ms. season");
                Check((bool)Call(mrms,null,"HandleNpc",voter3,new NpcRequest(true){NpcID=380,OptionID=226}),
                    "a click after month end is served without writing the elapsed season");
                Check(Scalar("SELECT count(*) FROM revora_mrms_votes WHERE season='2035-06' AND voter="+voter3.Entity.UID,0)==0,
                    "a ballot opened in June cannot vote after Pacific month rollover");
                var rolled=(System.Collections.Generic.List<Tuple<string,byte>>)Call(mrms,null,"LastMenu",voter3);
                Check(rolled==null || !rolled.Exists(o=>o.Item2==226),
                    "elapsed-month candidate options are cleared after rollover");
            } finally {if(hadVote)map.Npcs[380]=savedVote;else map.Npcs.Remove(380);}
        } finally {mrms.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }

    static void VerifyOriginCtf(Map map)
    {
        var origin=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginRealms");
        var tourn=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginTournaments");
        var ctf=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginCtf");
        Call(origin,null,"Initialize");Call(tourn,null,"Initialize");Call(ctf,null,"Initialize");
        var champ=ProgressPlayer(map,"");var second=ProgressPlayer(map,"");
        Call(origin,null,"RegisterRealm","origin-a");Call(origin,null,"RegisterRealm","origin-b");
        Call(origin,null,"RegisterIdentity","origin-a",champ.Entity.UID,"Champ");
        Call(origin,null,"RegisterIdentity","origin-b",second.Entity.UID,"Second");
        Call(origin,null,"RegisterIdentity","origin-b",champ.Entity.UID,"Twin");
        Guid ownerA=Guid.NewGuid(),ownerB=Guid.NewGuid(),ownerTwin=Guid.NewGuid(),coordinator=Guid.NewGuid();
        Call(origin,null,"OpenSession","origin-a",champ.Entity.UID,ownerA);
        Call(origin,null,"OpenSession","origin-b",second.Entity.UID,ownerB);
        Call(origin,null,"OpenSession","origin-b",champ.Entity.UID,ownerTwin);
        Guid run=(Guid)Call(tourn,null,"OpenRun","cs-ctf-v1",coordinator);
        Call(tourn,null,"Enroll",run,"origin-a",champ.Entity.UID,ownerA);
        Call(tourn,null,"Enroll",run,"origin-b",second.Entity.UID,ownerB);
        Call(tourn,null,"Start",run,coordinator);
        Call(tourn,null,"Finalize",run,new string[]{"origin-a:"+champ.Entity.UID+":1","origin-b:"+second.Entity.UID+":2"},coordinator);
        Check((bool)Call(ctf,null,"Capture",run,coordinator),"CS-CTF capture records the origin-qualified champion");
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE uid=@uid AND NOT award_recorded",champ.Entity.UID)==2
            && !OwnedTitles(champ).ContainsKey(20030004) && !OwnedTitles(champ).ContainsKey(60020021),
            "wardrobe refresh without origin authentication leaves CS-CTF awards pending");
        Call(ctf,null,"Authenticate",champ,"origin-b",ownerTwin);
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE uid=@uid AND NOT award_recorded",champ.Entity.UID)==2
            && !OwnedTitles(champ).ContainsKey(20030004) && !OwnedTitles(champ).ContainsKey(60020021),
            "equal uid on another origin cannot consume the champion awards");
        Call(ctf,null,"Authenticate",champ,"origin-a",ownerA);
        EvidenceRefresh(champ);new TitleStorage().Login(champ);RefreshTitles(champ);
        Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021)
            && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE uid=@uid AND NOT award_recorded",champ.Entity.UID)==0,
            "ordinary champion receives Conqueror and Radiant Wings through origin-authenticated wardrobe refresh");
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
            "replayed origin-authenticated wardrobe refresh does not duplicate CS-CTF grants");
        TitleRequest(20030004,TitleStorage.Action.Equip).Handle(champ);
        Check(champ.Entity.EquippedTitle==20030004,"ordinary champion can equip Conqueror");
        EvidenceRefresh(second);new TitleStorage().Login(second);RefreshTitles(second);
        Check(!OwnedTitles(second).ContainsKey(20030004) && !OwnedTitles(second).ContainsKey(60020021),
            "placement 2 does not receive Conqueror or Radiant Wings");
        Check(StorageManager.GetTitlePoints(2003,4)==300 && StorageManager.GetTitlePoints(6002,21)==0,
            "CS-CTF catalog points remain Conqueror 300 and Radiant Wings 0");
        var other=ProgressPlayer(map,"");
        Call(origin,null,"RegisterIdentity","origin-a",other.Entity.UID,"Other");
        Guid ownerOther=Guid.NewGuid();
        Call(origin,null,"OpenSession","origin-a",other.Entity.UID,ownerOther);
        Guid later=(Guid)Call(tourn,null,"OpenRun","cs-ctf-v1",coordinator);
        Call(tourn,null,"Enroll",later,"origin-b",champ.Entity.UID,ownerTwin);
        Call(tourn,null,"Enroll",later,"origin-a",other.Entity.UID,ownerOther);
        Call(tourn,null,"Start",later,coordinator);
        Call(tourn,null,"Finalize",later,new string[]{"origin-b:"+champ.Entity.UID+":1","origin-a:"+other.Entity.UID+":2"},coordinator);
        Check((bool)Call(ctf,null,"Capture",later,coordinator),"equal-uid origin-b captures a later CS-CTF champion pair");
        Call(ctf,null,"Authenticate",champ,"origin-b",ownerTwin);
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
            "origin-b authentication shows the later equal-uid CS-CTF pair");
        Call(ctf,null,"Authenticate",champ,"origin-a",ownerA);
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
            "origin-a authentication keeps its CS-CTF pair while origin-b is later");
        Query("UPDATE revora_event_awards SET earned_at=earned_at + interval '3 days' WHERE uid=@uid AND source='cs-ctf-revora-v1' AND source_key LIKE 'origin-a:%'","uid",(long)champ.Entity.UID);
        Call(ctf,null,"Authenticate",champ,"origin-b",ownerTwin);
        EvidenceRefresh(champ);RefreshTitles(champ);
        Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
            "origin-b authentication keeps its CS-CTF pair while origin-a is later");
        var held=ProgressPlayer(map,"");
        Call(origin,null,"RegisterIdentity","origin-a",held.Entity.UID,"Held");
        Guid ownerOld=Guid.NewGuid(),ownerNew=Guid.NewGuid();
        Guid heldSession=(Guid)Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerOld);
        Guid holdRun=(Guid)Call(tourn,null,"OpenRun","cs-ctf-v1",coordinator);
        Call(tourn,null,"Enroll",holdRun,"origin-a",held.Entity.UID,ownerOld);
        Call(tourn,null,"Enroll",holdRun,"origin-b",second.Entity.UID,ownerB);
        Call(tourn,null,"Start",holdRun,coordinator);
        Call(tourn,null,"Finalize",holdRun,new string[]{"origin-a:"+held.Entity.UID+":1","origin-b:"+second.Entity.UID+":2"},coordinator);
        Check((bool)Call(ctf,null,"Capture",holdRun,coordinator),"restart fixture captures pending CS-CTF awards");
        Call(origin,null,"CloseSession","origin-a",held.Entity.UID,ownerOld,heldSession);
        Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerNew);
        Call(ctf,null,"Authenticate",held,"origin-a",ownerNew);
        EvidenceRefresh(held);new TitleStorage().Login(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20030004) && OwnedTitles(held).ContainsKey(60020021)
            && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE uid=@uid AND award_recorded",held.Entity.UID)==2,
            "a later process owner receives Conqueror and Radiant Wings through wardrobe refresh");
        EvidenceRefresh(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20030004) && OwnedTitles(held).ContainsKey(60020021),
            "replayed restart wardrobe refresh does not duplicate CS-CTF grants");
    }

    static void VerifyOriginElitePk(Map map)
    {
        var origin=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginRealms");
        var tourn=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginTournaments");
        var ctf=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginCtf");
        var epk=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginElitePk");
        Call(origin,null,"Initialize");Call(tourn,null,"Initialize");Call(ctf,null,"Initialize");Call(epk,null,"Initialize");
        var first=ProgressPlayer(map,"");var second=ProgressPlayer(map,"");var third=ProgressPlayer(map,"");var fourth=ProgressPlayer(map,"");
        Call(origin,null,"RegisterRealm","origin-a");Call(origin,null,"RegisterRealm","origin-b");
        Call(origin,null,"RegisterIdentity","origin-a",first.Entity.UID,"First");
        Call(origin,null,"RegisterIdentity","origin-b",second.Entity.UID,"Second");
        Call(origin,null,"RegisterIdentity","origin-a",third.Entity.UID,"Third");
        Call(origin,null,"RegisterIdentity","origin-b",fourth.Entity.UID,"Fourth");
        Call(origin,null,"RegisterIdentity","origin-b",first.Entity.UID,"Twin");
        Guid ownerA=Guid.NewGuid(),ownerB=Guid.NewGuid(),ownerC=Guid.NewGuid(),ownerD=Guid.NewGuid(),ownerTwin=Guid.NewGuid(),coordinator=Guid.NewGuid();
        Call(origin,null,"OpenSession","origin-a",first.Entity.UID,ownerA);
        Call(origin,null,"OpenSession","origin-b",second.Entity.UID,ownerB);
        Call(origin,null,"OpenSession","origin-a",third.Entity.UID,ownerC);
        Call(origin,null,"OpenSession","origin-b",fourth.Entity.UID,ownerD);
        Call(origin,null,"OpenSession","origin-b",first.Entity.UID,ownerTwin);
        Guid run=(Guid)Call(tourn,null,"OpenRun","cs-epk-v1",coordinator);
        Call(tourn,null,"Enroll",run,"origin-a",first.Entity.UID,ownerA);
        Call(tourn,null,"Enroll",run,"origin-b",second.Entity.UID,ownerB);
        Call(tourn,null,"Enroll",run,"origin-a",third.Entity.UID,ownerC);
        Call(tourn,null,"Enroll",run,"origin-b",fourth.Entity.UID,ownerD);
        Call(tourn,null,"Start",run,coordinator);
        Call(tourn,null,"Finalize",run,new string[]{"origin-a:"+first.Entity.UID+":1","origin-b:"+second.Entity.UID+":2","origin-a:"+third.Entity.UID+":3","origin-b:"+fourth.Entity.UID+":4"},coordinator);
        Check((bool)Call(epk,null,"Capture",run,coordinator),"CS Elite PK capture records origin-qualified placements");
        EvidenceRefresh(first);RefreshTitles(first);
        Check(Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE uid=@uid AND NOT award_recorded",first.Entity.UID)==1
            && !OwnedTitles(first).ContainsKey(20352035),
            "wardrobe refresh without origin authentication leaves CS Elite PK awards pending");
        Call(ctf,null,"Authenticate",first,"origin-b",ownerTwin);
        EvidenceRefresh(first);RefreshTitles(first);
        Check(Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE uid=@uid AND NOT award_recorded",first.Entity.UID)==1
            && !OwnedTitles(first).ContainsKey(20352035),
            "equal uid on another origin cannot consume the Elite PK awards");
        Call(ctf,null,"Authenticate",first,"origin-a",ownerA);
        EvidenceRefresh(first);new TitleStorage().Login(first);RefreshTitles(first);
        Check(OwnedTitles(first).ContainsKey(20352035) && !OwnedTitles(first).ContainsKey(20382038)
            && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE uid=@uid AND NOT award_recorded",first.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='cs-epk-revora-v1' AND title_key=20352035 AND expires_at-earned_at=interval '336 hours'",first.Entity.UID)==1,
            "ordinary champion receives fourteen-day Legendary through origin-authenticated wardrobe refresh");
        EvidenceRefresh(first);RefreshTitles(first);
        Check(OwnedTitles(first).ContainsKey(20352035),"replayed origin-authenticated wardrobe refresh does not duplicate Elite PK grants");
        TitleRequest(20352035,TitleStorage.Action.Equip).Handle(first);
        Check(first.Entity.EquippedTitle==20352035,"ordinary champion can equip Legendary");
        Call(ctf,null,"Authenticate",second,"origin-b",ownerB);
        EvidenceRefresh(second);new TitleStorage().Login(second);RefreshTitles(second);
        Check(OwnedTitles(second).ContainsKey(20362036) && !OwnedTitles(second).ContainsKey(20352035),
            "placement 2 receives Peerless and not Legendary");
        Call(ctf,null,"Authenticate",third,"origin-a",ownerC);
        EvidenceRefresh(third);new TitleStorage().Login(third);RefreshTitles(third);
        Check(OwnedTitles(third).ContainsKey(20372037),"placement 3 receives Outstanding");
        Call(ctf,null,"Authenticate",fourth,"origin-b",ownerD);
        EvidenceRefresh(fourth);new TitleStorage().Login(fourth);RefreshTitles(fourth);
        Check(OwnedTitles(fourth).ContainsKey(20382038),"placement 4 receives Expert");
        Check((int)Call(epk,null,"TitleFor",4)==20382038 && (int)Call(epk,null,"TitleFor",8)==20382038 && (int)Call(epk,null,"TitleFor",9)==0,
            "Expert covers placements 4 through 8");
        Check(StorageManager.GetTitlePoints(2035,2035)==500 && StorageManager.GetTitlePoints(2036,2036)==450
            && StorageManager.GetTitlePoints(2037,2037)==400 && StorageManager.GetTitlePoints(2038,2038)==300,
            "CS Elite PK catalog points remain 500, 450, 400 and 300");
        DateTime earned;
        using(var c=DataHolder.MySqlConnection)
        {
            c.Open();
            using(var q=new Npgsql.NpgsqlCommand("SELECT earned_at FROM revora_event_awards WHERE uid=@uid AND source='cs-epk-revora-v1' AND title_key=20352035",c))
            {
                q.Parameters.AddWithValue("uid",(long)first.Entity.UID);
                object raw=q.ExecuteScalar();
                if(raw is DateTimeOffset)earned=((DateTimeOffset)raw).UtcDateTime;
                else earned=((DateTime)raw).ToUniversalTime();
            }
        }
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>earned.AddDays(14)));
        try
        {
            RefreshTitles(first);
            Check(!OwnedTitles(first).ContainsKey(20352035) && first.Entity.EquippedTitle==0,
                "Legendary expires and unequips at fourteen days from finalize");
        }
        finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        var held=ProgressPlayer(map,"");
        Call(origin,null,"RegisterIdentity","origin-a",held.Entity.UID,"Held");
        Guid ownerOld=Guid.NewGuid(),ownerNew=Guid.NewGuid();
        Guid heldSession=(Guid)Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerOld);
        Guid holdRun=(Guid)Call(tourn,null,"OpenRun","cs-epk-v1",coordinator);
        Call(tourn,null,"Enroll",holdRun,"origin-a",held.Entity.UID,ownerOld);
        Call(tourn,null,"Enroll",holdRun,"origin-b",second.Entity.UID,ownerB);
        Call(tourn,null,"Start",holdRun,coordinator);
        Call(tourn,null,"Finalize",holdRun,new string[]{"origin-a:"+held.Entity.UID+":1","origin-b:"+second.Entity.UID+":2"},coordinator);
        Check((bool)Call(epk,null,"Capture",holdRun,coordinator),"restart fixture captures pending CS Elite PK awards");
        Call(origin,null,"CloseSession","origin-a",held.Entity.UID,ownerOld,heldSession);
        Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerNew);
        Call(ctf,null,"Authenticate",held,"origin-a",ownerNew);
        EvidenceRefresh(held);new TitleStorage().Login(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20352035)
            && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE uid=@uid AND award_recorded",held.Entity.UID)==1,
            "a later process owner receives Legendary through wardrobe refresh");
        EvidenceRefresh(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20352035),
            "replayed restart wardrobe refresh does not duplicate Elite PK grants");
    }

    static void VerifyOriginTeamPk(Map map)
    {
        var origin=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginRealms");
        var tourn=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginTournaments");
        var ctf=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginCtf");
        var tpk=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginTeamPk");
        Call(origin,null,"Initialize");Call(tourn,null,"Initialize");Call(ctf,null,"Initialize");Call(tpk,null,"Initialize");
        var home=new GameState[5];var away=new GameState[5];
        for(int i=0;i<5;i++){home[i]=ProgressPlayer(map,"");away[i]=ProgressPlayer(map,"");}
        Call(origin,null,"RegisterRealm","origin-a");Call(origin,null,"RegisterRealm","origin-b");
        Guid ownerA=Guid.NewGuid(),ownerB=Guid.NewGuid(),ownerTwin=Guid.NewGuid(),coordinator=Guid.NewGuid();
        var homeIds=new uint[5];var awayIds=new uint[5];
        for(int i=0;i<5;i++)
        {
            homeIds[i]=home[i].Entity.UID;awayIds[i]=away[i].Entity.UID;
            Call(origin,null,"RegisterIdentity","origin-a",homeIds[i],"H"+i);
            Call(origin,null,"RegisterIdentity","origin-b",awayIds[i],"A"+i);
            Call(origin,null,"OpenSession","origin-a",homeIds[i],ownerA);
            Call(origin,null,"OpenSession","origin-b",awayIds[i],ownerB);
        }
        Call(origin,null,"RegisterIdentity","origin-b",homeIds[0],"Twin");
        Call(origin,null,"OpenSession","origin-b",homeIds[0],ownerTwin);
        Guid run=(Guid)Call(tourn,null,"OpenRun","cs-team-pk-v1",coordinator);
        Call(tpk,null,"FormTeam",run,"origin-a",homeIds,ownerA);
        Call(tpk,null,"FormTeam",run,"origin-b",awayIds,ownerB);
        Call(tourn,null,"Start",run,coordinator);
        Call(tourn,null,"Finalize",run,new string[]{"origin-a:"+homeIds[0]+":1","origin-b:"+awayIds[0]+":2"},coordinator);
        Check((bool)Call(tpk,null,"Capture",run,coordinator),"CS Team PK capture records origin-qualified rosters");
        EvidenceRefresh(home[0]);RefreshTitles(home[0]);
        Check(Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE uid=@uid AND NOT award_recorded",homeIds[0])==1
            && !OwnedTitles(home[0]).ContainsKey(20592059),
            "wardrobe refresh without origin authentication leaves CS Team PK awards pending");
        Call(ctf,null,"Authenticate",home[0],"origin-b",ownerTwin);
        EvidenceRefresh(home[0]);RefreshTitles(home[0]);
        Check(Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE uid=@uid AND NOT award_recorded",homeIds[0])==1
            && !OwnedTitles(home[0]).ContainsKey(20592059),
            "equal uid on another origin cannot consume the Team PK awards");
        Call(ctf,null,"Authenticate",home[0],"origin-a",ownerA);
        EvidenceRefresh(home[0]);new TitleStorage().Login(home[0]);RefreshTitles(home[0]);
        Check(OwnedTitles(home[0]).ContainsKey(20592059) && !OwnedTitles(home[0]).ContainsKey(20622062)
            && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='cs-tpk-revora-v1' AND title_key=20592059 AND expires_at-earned_at=interval '336 hours'",homeIds[0])==1,
            "ordinary captain receives fourteen-day King Team through origin-authenticated wardrobe refresh");
        Call(ctf,null,"Authenticate",home[1],"origin-a",ownerA);
        EvidenceRefresh(home[1]);new TitleStorage().Login(home[1]);RefreshTitles(home[1]);
        Check(OwnedTitles(home[1]).ContainsKey(20592059),"ordinary roster member receives King Team");
        TitleRequest(20592059,TitleStorage.Action.Equip).Handle(home[0]);
        Check(home[0].Entity.EquippedTitle==20592059,"ordinary captain can equip King Team");
        Call(ctf,null,"Authenticate",away[0],"origin-b",ownerB);
        EvidenceRefresh(away[0]);new TitleStorage().Login(away[0]);RefreshTitles(away[0]);
        Check(OwnedTitles(away[0]).ContainsKey(20602060) && !OwnedTitles(away[0]).ContainsKey(20592059),
            "placement 2 receives Dominator Team and not King Team");
        Check((int)Call(tpk,null,"TitleFor",3)==20612061 && (int)Call(tpk,null,"TitleFor",4)==20622062 && (int)Call(tpk,null,"TitleFor",8)==20622062,
            "Elite Team covers team placements 4 through 8");
        Check(StorageManager.GetTitlePoints(2059,2059)==500 && StorageManager.GetTitlePoints(2060,2060)==450
            && StorageManager.GetTitlePoints(2061,2061)==400 && StorageManager.GetTitlePoints(2062,2062)==300,
            "CS Team PK catalog points remain 500, 450, 400 and 300");
        DateTime earned;
        using(var c=DataHolder.MySqlConnection)
        {
            c.Open();
            using(var q=new Npgsql.NpgsqlCommand("SELECT earned_at FROM revora_event_awards WHERE uid=@uid AND source='cs-tpk-revora-v1' AND title_key=20592059",c))
            {
                q.Parameters.AddWithValue("uid",(long)homeIds[0]);
                object raw=q.ExecuteScalar();
                if(raw is DateTimeOffset)earned=((DateTimeOffset)raw).UtcDateTime;
                else earned=((DateTime)raw).ToUniversalTime();
            }
        }
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>earned.AddDays(14)));
        try
        {
            RefreshTitles(home[0]);
            Check(!OwnedTitles(home[0]).ContainsKey(20592059) && home[0].Entity.EquippedTitle==0,
                "King Team expires and unequips at fourteen days from finalize");
        }
        finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        var held=ProgressPlayer(map,"");
        var mate=ProgressPlayer(map,"");
        var r3=ProgressPlayer(map,"");
        var r4=ProgressPlayer(map,"");
        var r5=ProgressPlayer(map,"");
        Call(origin,null,"RegisterIdentity","origin-a",held.Entity.UID,"Held");
        Call(origin,null,"RegisterIdentity","origin-a",mate.Entity.UID,"Mate");
        Call(origin,null,"RegisterIdentity","origin-a",r3.Entity.UID,"R3");
        Call(origin,null,"RegisterIdentity","origin-a",r4.Entity.UID,"R4");
        Call(origin,null,"RegisterIdentity","origin-a",r5.Entity.UID,"R5");
        Guid ownerOld=Guid.NewGuid(),ownerNew=Guid.NewGuid();
        Guid heldSession=(Guid)Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerOld);
        Call(origin,null,"OpenSession","origin-a",mate.Entity.UID,ownerOld);
        Call(origin,null,"OpenSession","origin-a",r3.Entity.UID,ownerOld);
        Call(origin,null,"OpenSession","origin-a",r4.Entity.UID,ownerOld);
        Call(origin,null,"OpenSession","origin-a",r5.Entity.UID,ownerOld);
        Guid holdRun=(Guid)Call(tourn,null,"OpenRun","cs-team-pk-v1",coordinator);
        Call(tpk,null,"FormTeam",holdRun,"origin-a",new uint[]{held.Entity.UID,mate.Entity.UID,r3.Entity.UID,r4.Entity.UID,r5.Entity.UID},ownerOld);
        Call(tpk,null,"FormTeam",holdRun,"origin-b",awayIds,ownerB);
        Call(tourn,null,"Start",holdRun,coordinator);
        Call(tourn,null,"Finalize",holdRun,new string[]{"origin-a:"+held.Entity.UID+":1","origin-b:"+awayIds[0]+":2"},coordinator);
        Check((bool)Call(tpk,null,"Capture",holdRun,coordinator),"restart fixture captures pending CS Team PK awards");
        Call(origin,null,"CloseSession","origin-a",held.Entity.UID,ownerOld,heldSession);
        Call(origin,null,"OpenSession","origin-a",held.Entity.UID,ownerNew);
        Call(ctf,null,"Authenticate",held,"origin-a",ownerNew);
        EvidenceRefresh(held);new TitleStorage().Login(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20592059)
            && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE uid=@uid AND award_recorded",held.Entity.UID)==1,
            "a later process owner receives King Team through wardrobe refresh");
        EvidenceRefresh(held);RefreshTitles(held);
        Check(OwnedTitles(held).ContainsKey(20592059),
            "replayed restart wardrobe refresh does not duplicate Team PK grants");
    }

    static void VerifyOriginBind(Map map)
    {
        var origin=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginRealms");
        var tourn=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginTournaments");
        var ctf=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginCtf");
        var bind=typeof(Entity).Assembly.GetType("Revora.Game.Features.OriginBind");
        Call(origin,null,"Initialize");Call(tourn,null,"Initialize");Call(ctf,null,"Initialize");
        var champ=ProgressPlayer(map,"");var second=ProgressPlayer(map,"");
        Guid process=Guid.NewGuid(),ownerB=Guid.NewGuid(),coordinator=Guid.NewGuid();
        bind.GetField("Owner",Hidden).SetValue(null,process);
        try
        {
            Call(bind,null,"Configure","origin-a");
            Call(origin,null,"RegisterRealm","origin-b");
            Call(origin,null,"RegisterIdentity","origin-b",second.Entity.UID,"Second");
            Call(origin,null,"OpenSession","origin-b",second.Entity.UID,ownerB);
            Call(bind,null,"Login",champ);
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL",champ.Entity.UID)==1,
                "production login opens this origin's session");
            Guid run=(Guid)Call(tourn,null,"OpenRun","cs-ctf-v1",coordinator);
            Call(tourn,null,"Enroll",run,"origin-a",champ.Entity.UID,process);
            Call(tourn,null,"Enroll",run,"origin-b",second.Entity.UID,ownerB);
            Call(tourn,null,"Start",run,coordinator);
            Call(tourn,null,"Finalize",run,new string[]{"origin-a:"+champ.Entity.UID+":1","origin-b:"+second.Entity.UID+":2"},coordinator);
            Check((bool)Call(ctf,null,"Capture",run,coordinator),"origin-bound login can capture CS-CTF placement 1");
            EvidenceRefresh(champ);new TitleStorage().Login(champ);RefreshTitles(champ);
            Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
                "ordinary login bind grants Conqueror and Radiant Wings through wardrobe refresh");
            Call(bind,null,"Logout",champ);
            EvidenceRefresh(champ);RefreshTitles(champ);
            Check(!OwnedTitles(champ).ContainsKey(20030004) && !OwnedTitles(champ).ContainsKey(60020021),
                "logout clears origin bind and hides CS-CTF awards");
            Call(bind,null,"Login",champ);
            EvidenceRefresh(champ);RefreshTitles(champ);
            Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021),
                "reconnect restores origin bind and CS-CTF awards");
            Guid restarted=Guid.NewGuid();
            bind.GetField("Owner",Hidden).SetValue(null,restarted);
            Call(bind,null,"Configure","origin-a");
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL",champ.Entity.UID)==0,
                "process restart recovers leftover origin sessions for this realm");
            Call(bind,null,"Login",champ);
            EvidenceRefresh(champ);RefreshTitles(champ);
            Check(OwnedTitles(champ).ContainsKey(20030004) && OwnedTitles(champ).ContainsKey(60020021)
                && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL",champ.Entity.UID)==1,
                "login after restart rebinds this origin without duplicating CS-CTF grants");
            var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
            Call(activity,null,"Initialize");
            var renamed=ProgressPlayer(map,"");
            string account="obind-"+renamed.Entity.UID;
            Query("INSERT INTO accounts(username,password,entityid) VALUES(@name,'!synthetic-unusable!',@uid)","name",account,"uid",(long)renamed.Entity.UID);
            renamed.Account=new AccountTable(account);
            renamed.Fake=false;
            Call(activity,null,"Authenticated",renamed.Account);
            Call(activity,null,"Open",renamed);
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL",renamed.Entity.UID)==1,
                "AccountActivity.Open binds this origin for the original name");
            Guid renameRun=(Guid)Call(tourn,null,"OpenRun","cs-ctf-v1",coordinator);
            Call(tourn,null,"Enroll",renameRun,"origin-a",renamed.Entity.UID,(Guid)bind.GetField("Owner",Hidden).GetValue(null));
            Call(tourn,null,"Enroll",renameRun,"origin-b",second.Entity.UID,ownerB);
            Call(tourn,null,"Start",renameRun,coordinator);
            Call(tourn,null,"Finalize",renameRun,new string[]{"origin-a:"+renamed.Entity.UID+":1","origin-b:"+second.Entity.UID+":2"},coordinator);
            Check((bool)Call(ctf,null,"Capture",renameRun,coordinator),"Open-bound login can capture CS-CTF placement 1");
            EvidenceRefresh(renamed);new TitleStorage().Login(renamed);RefreshTitles(renamed);
            Check(OwnedTitles(renamed).ContainsKey(20030004) && OwnedTitles(renamed).ContainsKey(60020021),
                "Open hook grants Conqueror and Radiant Wings through wardrobe refresh");
            Call(activity,null,"Close",renamed);
            string newName="N"+renamed.Entity.UID;
            if(newName.Length>16)newName=newName.Substring(0,16);
            renamed.Entity.Name=newName;
            var relog=(GameState)typeof(CompanionTests).GetMethod("Player",Hidden).Invoke(null,new object[]{map,false,(ushort)561,(ushort)549,(byte)15});
            relog.Fake=false;relog.Action=2;relog.ItemSyncRoot=new object();
            relog.Trade=new Revora.Game.ConquerStructures.Trade();
            relog.Entity=renamed.Entity;relog.Entity.Owner=relog;relog.Account=renamed.Account;
            relog.Entity.FullyLoaded=true;relog.Entity.MyAchievement=new Achievement(relog.Entity);
            Kernel.GamePool[renamed.Entity.UID]=relog;
            Call(activity,null,"Open",relog);
            Check(Scalar("SELECT count(*) FROM revora_origin_identities WHERE realm='origin-a' AND uid=@uid AND name='"+newName+"'",renamed.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL",renamed.Entity.UID)==1,
                "Close then Open after rename rebinds the same uid");
            EvidenceRefresh(relog);new TitleStorage().Login(relog);RefreshTitles(relog);
            Check(OwnedTitles(relog).ContainsKey(20030004) && OwnedTitles(relog).ContainsKey(60020021)
                && (int)Call(ctf,null,"VisibleCount",renamed.Entity.UID,"origin-a")==2,
                "renamed relog authenticates exactly one Conqueror and Radiant Wings pair");
            Call(activity,null,"Close",relog);
        }
        finally
        {
            try { Call(typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity"),null,"Relinquish"); } catch { }
            Call(bind,null,"Configure","");
            bind.GetField("Owner",Hidden).SetValue(null,Guid.NewGuid());
        }
    }

}
