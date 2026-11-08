using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owned StageGoal substages. Level tasks read the saved level and rebirth
    // tasks read the saved rebirth. Subclass tasks count distinct saved
    // enrollments. Socket tasks read valid active combat gear and shared slots 10-12.
    // The Fierce BOSS task reads a victory whose killer is this character.
    // The claim packet is not progress. Grants are bound; stones retain their grade.
    // Main-stage prizes require every expected saved substage completion.
    internal static class WayOfHeroesClaims
    {
        internal const ushort PacketId=2830;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        private static readonly Dictionary<uint,Rule> Rules=new Dictionary<uint,Rule>();
        private sealed class Rule
        {
            public uint Id;
            public uint Progress;
            public uint Prize;
            public byte Count;
            public byte Third;
            public uint Task;
            public uint Open;
            public uint Require;
            public bool Owned;
        }
        internal static void Initialize(){
            EnlightenmentProgress.Initialize();
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS wayofheroes(
uid bigint PRIMARY KEY,name text NOT NULL DEFAULT '',wayofheroes bytea);
CREATE TABLE IF NOT EXISTS revora_team_earning(
uid bigint PRIMARY KEY REFERENCES entities(uid),leader bigint NOT NULL,kind smallint NOT NULL CHECK(kind BETWEEN 0 AND 2),
joined_at timestamptz NOT NULL,CHECK((kind=0 AND uid=leader) OR (kind IN(1,2) AND uid<>leader)));
CREATE TABLE IF NOT EXISTS revora_guild_earning(
uid bigint PRIMARY KEY REFERENCES entities(uid),guild_id bigint NOT NULL CHECK(guild_id>0),
founder boolean NOT NULL,joined_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_guild_admissions(
uid bigint PRIMARY KEY REFERENCES entities(uid),guild_id bigint NOT NULL CHECK(guild_id>0),
admission uuid NOT NULL UNIQUE,joined_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_guild_admissions_guild ON revora_guild_admissions(guild_id);
CREATE OR REPLACE FUNCTION revora_guild_admission_departed() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF OLD.guildid IS DISTINCT FROM NEW.guildid OR COALESCE(NEW.guildid,0)=0 OR COALESCE(NEW.guildrank,0)=0 THEN
  DELETE FROM revora_guild_admissions WHERE uid=OLD.uid;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_guild_admission_departed') THEN
CREATE TRIGGER revora_guild_admission_departed AFTER UPDATE OF guildid,guildrank ON entities
FOR EACH ROW EXECUTE FUNCTION revora_guild_admission_departed();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_guild_admission_departed' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_guild_admission_departed()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='guildid' AND NOT attisdropped) || ' ' || (SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='guildrank' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_guild_admission_departed';
END IF; END $revora_trigger$;
CREATE OR REPLACE FUNCTION revora_guild_admission_dissolved() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF TG_OP='DELETE' THEN
  DELETE FROM revora_guild_admissions WHERE guild_id=OLD.id;
  RETURN OLD;
 END IF;
 IF OLD.id IS DISTINCT FROM NEW.id THEN
  DELETE FROM revora_guild_admissions WHERE guild_id=OLD.id;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='guilds'::regclass AND tgname='revora_guild_admission_dissolved') THEN
CREATE TRIGGER revora_guild_admission_dissolved AFTER DELETE OR UPDATE OF id ON guilds
FOR EACH ROW EXECUTE FUNCTION revora_guild_admission_dissolved();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='guilds'::regclass AND tgname='revora_guild_admission_dissolved' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=25 AND tgfoid='revora_guild_admission_dissolved()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='guilds'::regclass AND attname='id' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_guild_admission_dissolved';
END IF; END $revora_trigger$;
CREATE TABLE IF NOT EXISTS revora_guild_pk_participation(
uid bigint PRIMARY KEY REFERENCES entities(uid),run uuid NOT NULL,guild_id bigint NOT NULL,admission uuid NOT NULL,
opponent bigint NOT NULL,opponent_admission uuid NOT NULL,entered_at timestamptz NOT NULL,accepted_at timestamptz NOT NULL,
CHECK(uid<>opponent),CHECK(accepted_at>=entered_at));
CREATE TABLE IF NOT EXISTS revora_way_of_heroes_claims(
uid bigint NOT NULL,stage_uid integer NOT NULL,prize_type integer NOT NULL,prize_count smallint NOT NULL,
legacy boolean NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,stage_uid));");}
            LoadRules();
            uint[] mainGoals={5,7,6,9,7,9,11,8,9,8},mainOpen={1,31,61,101,1015,1101,2015,2121,2121,2121};
            byte[] mainFlags={0,1,1,1,1,1,0,0,0,0};
            for(uint page=1;page<=10;page++){
                Rule main;
                if(!Rules.TryGetValue(page*100,out main) || !main.Owned || main.Progress!=mainGoals[page-1]
                    || main.Prize!=3005106+page || main.Count!=1 || main.Third!=mainFlags[page-1] || main.Open!=mainOpen[page-1]
                    || main.Task!=0 || main.Require!=0 || ExpectedSubstages(main.Id).Length!=main.Progress)
                    throw new InvalidOperationException("Way of Heroes main stage no longer matches the installed catalog.");
            }
            Expect(409,1,1200001,1,40,101);
            Expect(104,1,723017,1,3,1);
            Expect(103,1,723700,1,4,1,20);
            Expect(505,1,723700,1,26,1015);
            Expect(606,1,1088001,3,31,1101);
            Expect(607,1,723700,1,32,1101);
            Expect(303,1,3005123,1,11,61);
            Expect(1001,350,723695,1,43,2121);
            Expect(602,15,730002,1,22,1101);
            Expect(706,50,730003,1,22,2015);
            Expect(802,70,730003,1,22,2121);
            Expect(902,100,3005128,1,22,2121);
            Expect(1007,120,3005129,1,22,2121);
            Expect(609,1,723700,1,33,1101);
            Expect(904,18,728919,3,27,2121);
            Expect(707,8,700072,1,28,2015);
            Expect(203,1,723017,1,12,31);
            Expect(710,1,3005134,1,36,2015);
            Expect(704,1,3005131,1,35,2015);
            Expect(705,1,730002,1,41,2015);
            Expect(404,1,723700,1,39,101);
            Expect(306,1,723700,3,15,61);
            Expect(406,1,723700,1,18,101);
            Expect(407,1,723700,1,19,101);
            Expect(305,1,1200001,1,14,61);
            Expect(507,20,723700,1,14,1015);
            Expect(204,1,1200000,1,7,31);
            Expect(506,20,723700,1,7,1015);
            Expect(304,5,730001,1,13,61);
            Expect(102,6,1088001,1,5,1,6);
            Expect(202,6,1088001,3,5,31,7);
            Expect(402,6,721052,1,5,101,9);
            Expect(302,6,3001264,1,5,61,8);
            Expect(105,1,723700,1,6,1);
            Expect(101,20,3005117,1,1,1);
            Expect(205,1,300000,1,45,31);
            Expect(207,1,1088001,1,44,31);
            Expect(206,1,723017,1,9,31);
            Expect(201,50,3005118,1,1,31);
            Expect(301,80,3005119,1,1,61);
            Expect(401,110,3005120,1,1,101);
            Expect(403,1,1088001,3,16,101);
            Expect(503,3,730002,1,23,1015);
            Expect(405,1,723700,1,17,101);
            Expect(408,2,3005123,1,20,101);
            Expect(603,2,730002,1,28,1101);
            Expect(605,500,3005126,1,30,1101);
            Expect(702,1300,3003126,1,30,2015);
            Expect(804,1400,3003126,3,30,2121);
            Expect(905,1450,3003126,5,30,2121);
            Expect(1003,1580,3003126,10,30,2121);
            Expect(604,1,723694,1,29,1101,3);
            Expect(807,4,3005127,1,29,2121,3);
            Expect(908,6,3005128,1,29,2121,3);
            Expect(1008,7,3005129,1,29,2121,2);
            Expect(808,1,3005127,1,46,2121,3);
            Expect(909,5,3005128,1,46,2121,3);
            Expect(801,1,3005127,1,38,2121,8);
            Expect(901,1,3005128,1,38,2121,3);
            Expect(1002,1,3005129,1,38,2121,1);
            Expect(703,10000,3005126,1,34,2015);
            Expect(805,20000,3005127,1,34,2121);
            Expect(906,30000,3005128,1,34,2121);
            Expect(1004,50000,3005129,1,34,2121);
            Expect(806,30000000,3005127,1,37,2121);
            Expect(907,100000000,3005128,1,37,2121);
            Expect(1005,500000000,3005129,1,37,2121);
            Expect(501,1,3005121,1,2,1015);
            Expect(504,1,3005124,1,24,1015);
            Expect(608,2,3005125,1,24,1101);
            Expect(701,2,3005122,1,2,2015);
            Expect(711,3,3005125,1,24,2015);
            Expect(502,1,3005130,1,27,1015);
            Expect(601,4,3005130,2,27,1101);
            Expect(708,8,3005132,1,27,2015);
            Expect(803,12,3005132,1,27,2121);
            Expect(709,1,3005133,1,42,2015);
            Expect(903,12,3005135,1,28,2121);
            Expect(1006,16,3005135,1,28,2121);
            enabled=true;
        }
        private static void Expect(uint id,uint progress,uint prize,byte count,uint task,uint open,uint require=0){
            Rule rule;
            if(!Rules.TryGetValue(id,out rule) || !rule.Owned || rule.Progress!=progress || rule.Prize!=prize || rule.Count!=count || rule.Task!=task || rule.Open!=open || rule.Third!=1 || rule.Require!=require)
                throw new InvalidOperationException("Way of Heroes stage "+id+" no longer matches the installed prize.");
        }
        private static void LoadRules(){
            Rules.Clear();
            string[] lines=File.ReadAllLines("database\\Slayer\\stagegoal.txt");
            uint open=0;Rule current=null;
            for(int i=0;i<lines.Length;i++){
                string line=lines[i].Trim();
                if(line.Length==0)continue;
                if(line.StartsWith("[") && line.EndsWith("]") && line.IndexOf("StageGoal",StringComparison.Ordinal)<0){
                    Store(current);current=null;
                    string body=line.Substring(1,line.Length-2);
                    string[] numbers=body.Split('-');
                    uint id=uint.Parse(numbers[0])*100;
                    if(numbers.Length>1)id+=uint.Parse(numbers[1]);
                    current=new Rule();current.Id=id;current.Open=open;continue;
                }
                int eq=line.IndexOf('=');
                if(eq<=0 || current==null)continue;
                string key=line.Substring(0,eq).Trim();
                string value=line.Substring(eq+1).Trim();
                if(key=="OpenLev"){open=uint.Parse(value);current.Open=open;}
                else if(key=="Progress" || key=="GoalNum")current.Progress=uint.Parse(value);
                else if(key=="TaskType")current.Task=uint.Parse(value);
                else if(key=="Require")current.Require=uint.Parse(value);
                else if(key=="Prize1"){
                    string[] prize=value.Split(new string[]{"@@"},StringSplitOptions.RemoveEmptyEntries);
                    current.Prize=uint.Parse(prize[0]);
                    current.Count=byte.Parse(prize[1]);
                    current.Third=prize.Length>2?byte.Parse(prize[2]):(byte)0;
                }
            }
            Store(current);
        }
        private static void Store(Rule rule){
            if(rule==null || Rules.ContainsKey(rule.Id))return;
            rule.Owned=(rule.Id>=100 && rule.Id<=1000 && rule.Id%100==0) || rule.Id==409 || rule.Id==104 || rule.Id==607 || rule.Id==103 || rule.Id==505 || rule.Id==606 || rule.Id==1001 || rule.Id==303 || rule.Id==702 || rule.Id==804 || rule.Id==905 || rule.Id==1003 || rule.Id==602 || rule.Id==706 || rule.Id==802 || rule.Id==902 || rule.Id==1007 || rule.Id==609 || rule.Id==904 || rule.Id==707 || rule.Id==203 || rule.Id==710 || rule.Id==704 || rule.Id==705 || rule.Id==404 || rule.Id==306 || rule.Id==406 || rule.Id==407 || rule.Id==305 || rule.Id==507 || rule.Id==204 || rule.Id==506 || rule.Id==304 || rule.Id==402 || rule.Id==102 || rule.Id==202 || rule.Id==302 || rule.Id==105 || rule.Id==205 || rule.Id==207 || rule.Id==206 || rule.Id==403 || rule.Id==503 || rule.Id==603 || rule.Id==405 || rule.Id==801 || rule.Id==901 || rule.Id==1002 || rule.Id==808 || rule.Id==909 || rule.Id==604 || rule.Id==807 || rule.Id==908 || rule.Id==1008 || rule.Id==703 || rule.Id==805 || rule.Id==906 || rule.Id==1004 || rule.Id==605 || rule.Id==806 || rule.Id==907 || rule.Id==1005 || rule.Id==408 || rule.Id==101 || rule.Id==201 || rule.Id==301 || rule.Id==401 || rule.Id==501 || rule.Id==504
                || rule.Id==608 || rule.Id==701 || rule.Id==711
                || rule.Id==502 || rule.Id==601 || rule.Id==708 || rule.Id==803 || rule.Id==709 || rule.Id==903 || rule.Id==1006;
            if(rule.Owned && (rule.Prize==0 || rule.Count==0 || rule.Count>40 || rule.Require!=0 && !(rule.Task==4 && rule.Require==20 || rule.Task==5 && rule.Require>=6 && rule.Require<=9 || rule.Task==29 && (rule.Require==2 || rule.Require==3) || rule.Task==46 && rule.Require==3 || rule.Task==38 && (rule.Require==1 || rule.Require==3 || rule.Require==8))))
                throw new InvalidOperationException("Way of Heroes stage "+rule.Id+" has no payable prize.");
            Rules.Add(rule.Id,rule);
        }
        private static uint[] ExpectedSubstages(uint main){
            return Rules.Values.Where(r=>r.Owned && r.Id>main && r.Id<main+100).Select(r=>r.Id).OrderBy(id=>id).ToArray();
        }
        private static bool MainFinished(SafeDictionary<uint,WayofHeroes.StageInfo> stages,uint main){
            Rule rule;WayofHeroes.StageInfo page;
            if(main<100 || main>1000 || main%100!=0 || !Rules.TryGetValue(main,out rule) || !stages.TryGetValue(main/100,out page))return false;
            var expected=ExpectedSubstages(main);
            return expected.Length==rule.Progress && expected.All(id=>page.SubStages.ContainsKey(id) && page.SubStages[id].Done);
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Say(GameState p,string text){if(p!=null)p.Send(text);var spoken=Spoken;if(spoken!=null)spoken(text);}
        private static bool Open(int reborn,int level,uint gate){
            int needReborn=(int)(gate/1000),needLevel=(int)(gate%1000);
            return reborn>needReborn || (reborn==needReborn && level>=needLevel);
        }
        private static int Score(int gem,bool superOnly){
            if(gem==0 || !Enum.IsDefined(typeof(Revora.Game.Enums.Gem),(byte)gem) || gem<0 || gem>255)return 0;
            if(!superOnly)return 1;
            return gem!=255 && gem%10==3?1:0;
        }
        private static int Equipped(NpgsqlConnection c,NpgsqlTransaction t,GameState p,bool superOnly){
            int count=0,offset=p.AlternateEquipment?20:0;var slots=new HashSet<int>();
            using(var q=Q.Command(c,t,@"SELECT uid,id,position,socketone,sockettwo FROM items WHERE entityid=@uid AND warehouse=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0 AND stacksize=1 AND COALESCE(NULLIF(days,''),'0')='0'
AND (position-@offset IN(1,2,3,4,5,6,8) OR position IN(10,11,12)) ORDER BY position FOR UPDATE","uid",(long)p.Entity.UID,"offset",offset))
            using(var r=q.ExecuteReader())while(r.Read()){
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]);int position=Convert.ToInt32(r[2]),slot=position>=21?position-20:position;
                if(!slots.Add(position))throw new InvalidOperationException("Duplicate equipped slot requires reload.");
                var worn=p.Equipment.TryGetItem((byte)position);int category=(int)ItemHandler.GetPositionFromID(type);
                bool correctSlot=category==slot || slot==5 && category==4 && !ItemHandler.IsTwoHand(type) && type/1000!=421;
                if(worn==null || worn.UID!=item || worn.ID!=type || !BonusEquipment(type) || !correctSlot)continue;
                count+=Score(Convert.ToInt32(r[3]),superOnly);count+=Score(Convert.ToInt32(r[4]),superOnly);
            }
            return count;
        }
        private static bool Table(NpgsqlConnection c,NpgsqlTransaction t,string name){
            using(var q=Q.Command(c,t,"SELECT to_regclass(@name) IS NOT NULL","name",name))return Equals(q.ExecuteScalar(),true);
        }
        private static bool Victory(NpgsqlConnection c,NpgsqlTransaction t,string table,uint uid){
            if(!Table(c,t,table))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM "+table+" WHERE state='victory' AND killer=@uid)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static int Subclasses(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"subclasses"))return 0;
            int count=0;var seen=new HashSet<int>();
            using(var q=Q.Command(c,t,"SELECT uid,level,phase FROM subclasses WHERE id=@player FOR UPDATE","player",(long)uid))
            using(var reader=q.ExecuteReader())while(reader.Read()){
                int classId=Convert.ToInt32(reader[0]),level=Convert.ToInt32(reader[1]),phase=Convert.ToInt32(reader[2]);
                if(!seen.Add(classId))throw new InvalidOperationException("Duplicate subclass requires reload.");
                if(SubclassProgression.Enrolled(classId,level,phase))count++;
            }
            return count;
        }
        private static int SuperTalismans(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            int count=0;
            // Quality comes from the two installed item identities, never Plus.
            // Count owned permanent single items in the bag or their proper slots.
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE entityid=@uid AND warehouse=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0 AND stacksize<=1
AND COALESCE(NULLIF(days,''),'0')='0'
AND ((id=201009 AND position IN(0,10)) OR (id=202009 AND position IN(0,11)))
ORDER BY uid FOR UPDATE","uid",(long)uid))
            using(var reader=q.ExecuteReader())while(reader.Read())count++;
            return count;
        }
        private static long Donation(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            // Read current saved donation, not rank, cached mirrors or lifetime
            // payments that survive a weekly reset.
            long donation=0;
            using(var q=Q.Command(c,t,"SELECT donation FROM nobility WHERE entityuid=@uid FOR UPDATE","uid",(long)uid))
            using(var reader=q.ExecuteReader()){
                if(reader.Read()){
                    donation=Convert.ToInt64(reader[0]);
                    if(donation<0 || reader.Read())throw new InvalidOperationException("Invalid nobility owner requires reload.");
                }
            }
            return donation;
        }
        private static int ChiScore(byte[] blob){
            if(blob==null || blob.Length==0)return 0;
            // One count byte followed by ordered powers, each with four type/value triples.
            int count=blob[0];
            if(count>4 || blob.Length!=1+13*count)throw new InvalidOperationException("Invalid saved Chi powers.");
            int score=0;
            using(var stream=new MemoryStream(blob))using(var reader=new BinaryReader(stream)){
                reader.ReadByte();
                for(int power=1;power<=count;power++){
                    if(reader.ReadByte()!=power)throw new InvalidOperationException("Invalid saved Chi order.");
                    for(int slot=0;slot<4;slot++){
                        int type=reader.ReadByte(),value=reader.ReadUInt16();
                        if(type==0 && value==0)continue; // An unlocked, unstudied slot.
                        if(type<1 || type>13)throw new InvalidOperationException("Invalid saved Chi attribute.");
                        int max=Enums.ChiMaxValues((Enums.ChiAttribute)type);
                        if(value>max)throw new InvalidOperationException("Invalid saved Chi value.");
                        score+=100*value/max;
                    }
                }
            }
            return score;
        }
        private static int SavedChiScore(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            using(var q=Q.Command(c,t,"SELECT chipowers FROM chi WHERE uid=@uid FOR UPDATE","uid",(long)uid)){
                object value=q.ExecuteScalar();
                return value==null || value==DBNull.Value?0:ChiScore((byte[])value);
            }
        }
        internal static void RecordGuildMembership(NpgsqlConnection c,NpgsqlTransaction t,uint uid,uint guild,bool founder){
            if(t==null || uid==0 || guild==0)throw new InvalidOperationException("Guild earning requires membership transaction.");
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank>0 FOR UPDATE",
                "uid",(long)uid,"guild",(long)guild))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild admission requires saved membership.");
            Q.Execute(c,t,@"INSERT INTO revora_guild_earning(uid,guild_id,founder,joined_at)
VALUES(@uid,@guild,@founder,@now) ON CONFLICT(uid) DO NOTHING",
                "uid",(long)uid,"guild",(long)guild,"founder",founder,"now",DateTime.UtcNow);
            // Latest committed admission is separate from the permanent join
            // achievement. Never infer uninterrupted tenure from the latter.
            // No legacy backfill: unknown admission times remain unknown.
            if(Q.Execute(c,t,@"INSERT INTO revora_guild_admissions(uid,guild_id,admission,joined_at)
VALUES(@uid,@guild,@admission,statement_timestamp())
ON CONFLICT(uid) DO UPDATE SET guild_id=EXCLUDED.guild_id,
admission=EXCLUDED.admission,joined_at=EXCLUDED.joined_at",
                "uid",(long)uid,"guild",(long)guild,"admission",Guid.NewGuid())!=1)
                throw new InvalidOperationException("Guild admission was not recorded.");
        }
        // Admission must consume this result within the same transaction. Row
        // locks prevent a departure or guild dissolution racing the decision.
        internal static bool SevenDayGuildMembership(NpgsqlConnection c,NpgsqlTransaction t,uint uid,uint guild,DateTime utc){
            if(t==null || utc.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Guild tenure requires a transaction and UTC clock.");
            if(uid==0 || guild==0 || utc<DateTime.MinValue.AddDays(7))return false;
            using(var q=Q.Command(c,t,@"SELECT a.admission FROM entities e
JOIN guilds g ON g.id=e.guildid
JOIN revora_guild_admissions a ON a.uid=e.uid AND a.guild_id=g.id
WHERE e.uid=@uid AND g.id=@guild AND e.guildrank BETWEEN 1 AND 1000
AND a.joined_at<=@cutoff FOR SHARE OF e,g,a",
                "uid",(long)uid,"guild",(long)guild,"cutoff",utc.AddDays(-7)))return q.ExecuteScalar()!=null;
        }
        private static bool SavedGuild(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_guild_earning WHERE uid=@uid)
OR EXISTS(SELECT 1 FROM entities e JOIN guilds g ON g.id=e.guildid
WHERE e.uid=@uid AND e.guildid>0 AND e.guildrank BETWEEN 1 AND 1000
AND EXISTS(SELECT 1 FROM entities leader WHERE leader.guildid=g.id AND leader.guildrank=1000 AND leader.name=g.leadername))",
                "uid",(long)uid))return (bool)q.ExecuteScalar();
        }
        private static uint JiangScore(string data,uint uid){
            if(string.IsNullOrEmpty(data))return 0;
            string[] fields=data.Split('#');
            if(fields.Length!=184 || fields[183]!="")throw new InvalidOperationException("Invalid saved Jiang length.");
            for(int stage=0;stage<9;stage++){
                int start=12+19*stage;
                if(fields[start]!="0" && fields[start]!="1")throw new InvalidOperationException("Invalid saved Jiang stage.");
                for(int star=0;star<9;star++){
                    string active=fields[start+1+2*star];ushort attribute;
                    if((active!="0" && active!="1") || !ushort.TryParse(fields[start+2+2*star],out attribute))
                        throw new InvalidOperationException("Invalid saved Jiang star.");
                    if(active=="0" && attribute==0)continue;
                    if(active!="1" || fields[start]!="1" || attribute/256<1 || attribute/256>6
                        || !Database.JiangHu.Attributes.ContainsKey(attribute)
                        || !Database.JiangHu.CultivateStatus[(byte)(stage+1)].Contains((byte)(attribute%256)))
                        throw new InvalidOperationException("Invalid saved Jiang attribute.");
                }
            }
            var parsed=new Game.JiangHu(0);parsed.Load(data);
            if(!parsed.HasSavedData || parsed.UID!=uid)throw new InvalidOperationException("Invalid saved Jiang owner.");
            return parsed.Inner_Strength;
        }
        private static uint SavedJiangScore(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            using(var q=Q.Command(c,t,"SELECT data FROM jianghu WHERE uid=@uid FOR UPDATE","uid",(long)uid)){
                object value=q.ExecuteScalar();
                return value==null || value==DBNull.Value?0:JiangScore(Convert.ToString(value),uid);
            }
        }
        private static bool Purified(NpgsqlConnection c,NpgsqlTransaction t,Rule rule,GameState p){
            int offset=p.AlternateEquipment?20:0;bool found=false;var slots=new HashSet<int>();
            DateTime now=Clock().ToUniversalTime();
            using(var q=Q.Command(c,t,@"SELECT i.uid,i.id,i.position,a.addingid,a.addinglevel,a.duration,a.addedon
FROM items i JOIN itemadding a ON a.uid=i.uid AND a.addingtype=0
WHERE i.entityid=@uid AND i.warehouse=0 AND COALESCE(i.inwardrobe,0)=0
AND COALESCE(i.suspicious,0)=0 AND i.stacksize=1 AND COALESCE(NULLIF(i.days,''),'0')='0'
AND i.position-@offset IN(1,2,3,4,5,6,8) ORDER BY i.uid FOR UPDATE OF i,a",
                "uid",(long)p.Entity.UID,"offset",offset))
            using(var r=q.ExecuteReader())while(r.Read()){
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]),soul=Convert.ToUInt32(r[3]);
                int position=Convert.ToInt32(r[2]),slot=position-offset,phase=Convert.ToInt32(r[4]);
                long duration=Convert.ToInt64(r[5]);
                if(!slots.Add(position))throw new InvalidOperationException("Duplicate purified equipment slot.");
                var worn=p.Equipment.TryGetItem((byte)position);
                int category=(int)ItemHandler.GetPositionFromID(type);
                bool correctSlot=category==slot || slot==5 && category==4 && !ItemHandler.IsTwoHand(type) && type/1000!=421;
                if(worn==null || worn.UID!=item || worn.ID!=type || !correctSlot || !ArtifactApplication.Fits(type,soul)
                    || ConquerItemInformation.BaseInformations[soul].PurificationLevel!=phase || phase<rule.Progress || duration<0)continue;
                if(duration==0){found=true;continue;}
                if(rule.Require==2)continue;
                DateTime added=DateTime.FromBinary(Convert.ToInt64(r[6])).ToUniversalTime();
                if(now>=added && now<added.AddSeconds(duration))found=true;
            }
            return found;
        }
        private static bool Refined(NpgsqlConnection c,NpgsqlTransaction t,Rule rule,GameState p){
            int offset=p.AlternateEquipment?20:0;var wornItems=new List<ConquerItem>();var slots=new HashSet<int>();
            using(var q=Q.Command(c,t,@"SELECT uid,id,position FROM items WHERE entityid=@uid AND warehouse=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0 AND stacksize=1 AND COALESCE(NULLIF(days,''),'0')='0'
AND position-@offset IN(1,2,3,4,5,6,8) ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID,"offset",offset))
            using(var r=q.ExecuteReader())while(r.Read()){
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]);int position=Convert.ToInt32(r[2]),slot=position-offset;
                if(!slots.Add(position))throw new InvalidOperationException("Duplicate refinery equipment slot.");
                var worn=p.Equipment.TryGetItem((byte)position);int category=(int)ItemHandler.GetPositionFromID(type);
                bool correctSlot=category==slot || slot==5 && category==4 && !ItemHandler.IsTwoHand(type) && type/1000!=421;
                if(worn!=null && worn.UID==item && worn.ID==type && correctSlot)wornItems.Add(worn);
            }
            DateTime now=Clock().ToLocalTime();bool found=false;
            foreach(var item in wornItems){
                // The active temporary effect masks its permanent predecessor until expiry.
                var effect=RefineryProgress.Read(c,t,item.UID).Current(now);
                Refinery.RefineryItem material;
                if(effect.EffectID!=0 && effect.EffectLevel>=rule.Progress
                    && (effect.EffectDuration==0 || now>=effect.AddedOn && now<effect.AddedOn.AddSeconds(effect.EffectDuration))
                    && Kernel.DatabaseRefinery.TryGetValue(effect.EffectID,out material) && RefineryProgress.Fits(item,material))found=true;
            }
            return found;
        }
        private static bool ElitePlacement(NpgsqlConnection c,NpgsqlTransaction t,uint uid,uint rank){
            if(!Table(c,t,"revora_epk_placements"))return false;
            // Place 5 is the verified tied fifth-through-eighth tier. A completed
            // uncontested run can retain legitimate non-champion placements.
            // Placement expiry governs the event prize, not this one-time milestone.
            using(var q=Q.Command(c,t,@"SELECT EXISTS(
SELECT 1 FROM revora_epk_placements p JOIN revora_epk_runs r ON r.run=p.run
JOIN revora_epk_entries e ON e.run=p.run AND e.uid=p.uid
WHERE p.uid=@uid AND r.state='complete' AND r.outcome IN('champion','uncontested')
AND p.bracket=r.bracket AND p.signup_level=e.level AND p.place BETWEEN 1 AND @rank
AND (p.place<>1 OR r.outcome='champion' AND r.winner=p.uid))",
                "uid",(long)uid,"rank",(int)Math.Min(rank,5u)))return Equals(q.ExecuteScalar(),true);
        }
        private static bool EliteParticipation(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_epk_matches"))return false;
            // A completed contested bout proves participation for either fighter.
            // Signup, byes, no-shows, idle matches and cancelled runs do not.
            using(var q=Q.Command(c,t,@"SELECT EXISTS(
SELECT 1 FROM revora_epk_matches m JOIN revora_epk_runs r ON r.run=m.run
JOIN revora_epk_entries e ON e.run=m.run AND e.uid=@uid
WHERE r.state='complete' AND r.outcome IN('champion','uncontested')
AND e.cycle=r.cycle AND e.uid IN(m.left_uid,m.right_uid)
AND m.left_uid>0 AND m.right_uid>0 AND m.left_uid<>m.right_uid
AND m.winner IN(m.left_uid,m.right_uid) AND m.outcome IN('defeat','timeout')
AND m.left_damage>=0 AND m.right_damage>=0 AND (m.left_damage>0 OR m.right_damage>0))",
                "uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool BonusEquipment(uint type){
            if(!ConquerItemInformation.BaseInformations.ContainsKey(type) || ItemHandler.IsArrow(type))return false;
            int slot=(int)ItemHandler.GetPositionFromID(type);
            return slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==12 || slot==18 || slot==19;
        }
        private static bool ComposedEquipment(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_plus_composition_targets"))return false;
            using(var q=Q.Command(c,t,@"SELECT DISTINCT e.item_type FROM revora_plus_compositions r
JOIN revora_plus_composition_targets e ON e.receipt=r.id WHERE r.uid=@uid
AND cardinality(r.materials)>0 AND (r.after_plus>r.before_plus OR r.after_plus=r.before_plus AND r.after_progress>r.before_progress)","uid",(long)uid))
            using(var r=q.ExecuteReader())while(r.Read())if(BonusEquipment(Convert.ToUInt32(r[0])))return true;
            return false;
        }
        private static bool EquippedBonus(NpgsqlConnection c,NpgsqlTransaction t,Rule rule,GameState p){
            int offset=p.AlternateEquipment?20:0,total=0;bool found=false;var slots=new HashSet<int>();
            using(var q=Q.Command(c,t,@"SELECT uid,id,position,plus FROM items WHERE entityid=@uid AND warehouse=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0 AND stacksize=1 AND COALESCE(NULLIF(days,''),'0')='0'
AND (position-@offset IN(1,2,3,4,5,6,8) OR position IN(10,11,12,18,19)) ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID,"offset",offset))
            using(var r=q.ExecuteReader())while(r.Read()){
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]);int position=Convert.ToInt32(r[2]),slot=position>=21?position-20:position,plus=Convert.ToInt32(r[3]);
                if(!slots.Add(position))throw new InvalidOperationException("Duplicate bonus equipment slot.");
                var worn=p.Equipment.TryGetItem((byte)position);int category=(int)ItemHandler.GetPositionFromID(type);
                bool correctSlot=category==slot || slot==5 && category==4 && !ItemHandler.IsTwoHand(type) && type/1000!=421;
                if(worn!=null && worn.UID==item && worn.ID==type && worn.Plus==plus && BonusEquipment(type) && correctSlot && plus>=0 && plus<=12){
                    total+=plus;if(plus>=rule.Progress)found=true;
                }
            }
            return rule.Task==22?total>=rule.Progress:found;
        }
        private static bool UsedHourPotion(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_exp_potion_uses"))return false;
            // The installed milestone explicitly requires the sixty-minute potion.
            // Existing timer balance (including other grants) is not proof of use.
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017 AND seconds=3600)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool MeteorUpgrade(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_equipment_upgrades"))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success AND after_type<>before_type)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool EquippedSteed(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            bool found=false;int rows=0;var worn=p.Equipment.TryGetItem(ConquerItem.Steed);
            using(var q=Q.Command(c,t,@"SELECT uid,id,stacksize,COALESCE(suspicious,0),COALESCE(inwardrobe,0),COALESCE(NULLIF(days,''),'0')
FROM items WHERE entityid=@uid AND warehouse=0 AND position=12 FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){
                if(++rows>1)throw new InvalidOperationException("Duplicate equipped steed slot.");
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]);
                if(worn!=null && worn.UID==item && worn.ID==type && worn.Position==12
                    && ConquerItemInformation.BaseInformations.ContainsKey(type) && (int)ItemHandler.GetPositionFromID(type)==ConquerItem.Steed
                    && Convert.ToInt32(r[2])==1 && Convert.ToInt32(r[3])==0 && Convert.ToInt32(r[4])==0 && Convert.ToString(r[5])=="0")found=true;
            }
            return found;
        }
        // Called under the existing participant locks only after real team publication.
        internal static bool RecordTeam(GameState p,GameState leader,short kind){
            try{
                Game.ConquerStructures.Team.Member member;
                var team=p.Team;
                if(!enabled || !Q.Player(p) || !Q.Player(leader) || team==null || !ReferenceEquals(team,leader.Team)
                    || !ReferenceEquals(team.Lider,leader) || !team.Members.TryGetValue(p.Entity.UID,out member) || !ReferenceEquals(member.entry,p))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    Q.Execute(c,t,"INSERT INTO revora_team_earning(uid,leader,kind,joined_at) VALUES(@uid,@leader,@kind,@now) ON CONFLICT(uid) DO NOTHING",
                        "uid",(long)p.Entity.UID,"leader",(long)leader.Entity.UID,"kind",kind,"now",Clock());
                    if(!Q.Player(p) || !ReferenceEquals(p.Team,team))throw new InvalidOperationException("Team earning session changed.");
                    t.Commit();
                }}return true;
            }catch(Exception ex){Q.Fail(p,"team-earning",ex);return false;}
        }
        private static bool EnteredCtf(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_ctf_members") || !Table(c,t,"revora_ctf_deliveries"))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_ctf_members m WHERE m.uid=@uid AND
(m.entered_at IS NOT NULL OR EXISTS(SELECT 1 FROM revora_ctf_deliveries d WHERE d.run=m.run AND d.uid=m.uid AND d.guild_id=m.guild_id)))","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool MadeTeam(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_team_earning"))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_earning WHERE uid=@uid)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool Married(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_achievement_evidence"))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129 AND source='mutual-marriage' AND rule_version=1)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool BuiltHouse(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_house_builds") || !Table(c,t,"house"))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_house_builds b JOIN house h ON h.id=b.uid AND h.owner=b.uid
WHERE b.uid=@uid AND b.level=1 AND h.houselevel BETWEEN 1 AND 5)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool AcceptedApprentice(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_mentor_admissions"))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_mentor_admissions WHERE mentor=@uid AND apprentice<>@uid)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool DisCityEntered(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            foreach(string table in new[]{"revora_dis_entries","revora_dis_completions","revora_dis_run_rules"})if(!Table(c,t,table))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_dis_entries e JOIN revora_dis_run_rules r ON r.run=e.run AND r.uid=e.uid
WHERE e.uid=@uid AND r.revision=1) OR EXISTS(SELECT 1 FROM revora_dis_completions e JOIN revora_dis_run_rules r ON r.run=e.run AND r.uid=e.uid
WHERE e.uid=@uid AND r.revision=1)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool LotteryPlayed(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_lottery_draws") || !Table(c,t,"revora_lottery_steps"))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_lottery_draws d
JOIN revora_lottery_steps paid ON paid.draw_id=d.id AND paid.step=0
JOIN revora_lottery_steps final ON final.draw_id=d.id AND final.step=d.step
WHERE d.uid=@uid AND d.claimed AND d.item_uid>0
AND (SELECT sum(units) FROM unnest(paid.source_units) units)=3
AND (d.prize,d.plus,d.sockets,d.bound)=(final.prize,final.plus,final.sockets,final.bound))",
                "uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool ChampionParticipation(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_champion_entries") || !Table(c,t,"revora_champion_entry_players"))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_champion_entries e
JOIN revora_champion_entry_players a ON a.run=e.id AND a.uid=@uid
JOIN revora_champion_entry_players b ON b.run=e.id AND b.uid<>a.uid
WHERE e.state IN('entered','returned','interrupted') AND e.entered_at IS NOT NULL
AND e.entered_at>=e.created_at AND (e.closed_at IS NULL OR e.closed_at>=e.entered_at)
AND a.event_day IS NOT NULL AND a.event_day=b.event_day AND a.round=b.round AND a.round BETWEEN 1 AND 5
AND EXTRACT(ISODOW FROM a.event_day) BETWEEN 1 AND 5
AND (SELECT count(*) FROM revora_champion_entry_players pair WHERE pair.run=e.id)=2)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool TeamTournamentParticipation(NpgsqlConnection c,NpgsqlTransaction t,uint uid,int kind){
            if(!Table(c,t,"revora_local_team_pk_runs") || !Table(c,t,"revora_local_team_pk_admissions"))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_local_team_pk_admissions a
JOIN revora_local_team_pk_runs r ON r.run=a.run WHERE a.uid=@uid AND r.kind=@kind
AND a.participated_at IS NOT NULL AND a.participated_at>=r.started_at)","uid",(long)uid,"kind",kind))return Equals(q.ExecuteScalar(),true);
        }
        private static long TeamArenaWins(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            foreach(string table in new[]{"revora_team_arena_results","revora_team_arena_statistics","revora_team_arena_admissions","revora_team_arena_admission_players"})
                if(!Table(c,t,table))return 0;
            using(var q=Q.Command(c,t,@"SELECT count(*) FROM revora_team_arena_statistics s
JOIN revora_team_arena_results r ON r.id=s.run
JOIN revora_team_arena_admissions a ON a.id=r.id
JOIN revora_team_arena_admission_players p ON p.run=r.id AND p.uid=s.uid
WHERE s.uid=@uid AND s.won AND p.first_side=(a.first_leader=r.winner)
AND ((r.winner=a.first_leader AND r.loser=a.second_leader) OR (r.winner=a.second_leader AND r.loser=a.first_leader))
AND r.participants=(SELECT count(*) FROM revora_team_arena_statistics whole WHERE whole.run=r.id)
AND r.participants=(SELECT count(*) FROM revora_team_arena_admission_players whole WHERE whole.run=r.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_statistics member LEFT JOIN revora_team_arena_admission_players admitted
ON admitted.run=member.run AND admitted.uid=member.uid WHERE member.run=r.id
AND (admitted.uid IS NULL OR member.won<>(admitted.first_side=(a.first_leader=r.winner))))",
                "uid",(long)uid))return Convert.ToInt64(q.ExecuteScalar());
        }
        private static long ArenaWins(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            foreach(string table in new[]{"revora_arena_matches","revora_arena_result_commits","revora_arena_result_statistics"})
                if(!Table(c,t,table))return 0;
            using(var q=Q.Command(c,t,@"SELECT count(*) FROM revora_arena_matches m
WHERE m.state='finished' AND m.winner=@uid AND m.entry_evidence_version=1
AND EXISTS(SELECT 1 FROM revora_arena_result_commits c WHERE c.run=m.id)
AND EXISTS(SELECT 1 FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid=@uid AND s.won)
AND EXISTS(SELECT 1 FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid=m.loser AND NOT s.won)",
                "uid",(long)uid))return Convert.ToInt64(q.ExecuteScalar());
        }
        private static long MutualFriends(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_friend_consents") || !Table(c,t,"friends"))return 0;
            // The claim already holds this owner's entity row, also locked by
            // acceptance/removal. Count distinct current, mutually saved peers.
            using(var q=Q.Command(c,t,@"SELECT count(DISTINCT f.friendid) FROM friends f
WHERE f.entityid=@uid AND f.friendid>0 AND f.friendid<>@uid
AND EXISTS(SELECT 1 FROM entities e WHERE e.uid=f.friendid)
AND EXISTS(SELECT 1 FROM friends r WHERE r.entityid=f.friendid AND r.friendid=@uid)
AND EXISTS(SELECT 1 FROM revora_friend_consents a WHERE a.removed_at IS NULL
AND ((a.requester=@uid AND a.receiver=f.friendid) OR (a.receiver=@uid AND a.requester=f.friendid)))",
                "uid",(long)uid))return Convert.ToInt64(q.ExecuteScalar());
        }
        private static bool Promoted(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            if(!Table(c,t,"revora_achievement_evidence"))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND source='class-promotion-v1' AND rule_version=1)","uid",(long)uid))
                return Equals(q.ExecuteScalar(),true);
        }
        private static bool QualitySet(NpgsqlConnection c,NpgsqlTransaction t,Rule rule,GameState p){
            int offset=p.AlternateEquipment?20:0,required=(1<<1)|(1<<2)|(1<<3)|(1<<4)|(1<<6)|(1<<8),seen=0;
            var occupied=new HashSet<int>();DateTime now=Clock().ToLocalTime();
            using(var q=Q.Command(c,t,@"SELECT uid,id,position,stacksize,COALESCE(suspicious,0),COALESCE(inwardrobe,0),COALESCE(NULLIF(days,''),'0'),daystamp
FROM items WHERE entityid=@uid AND warehouse=0 AND position-@offset IN(1,2,3,4,5,6,8) ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID,"offset",offset))
            using(var r=q.ExecuteReader())while(r.Read()){
                uint item=Convert.ToUInt32(r[0]),type=Convert.ToUInt32(r[1]);int position=Convert.ToInt32(r[2]),slot=position-offset;
                if(!occupied.Add(slot))throw new InvalidOperationException("Duplicate quality equipment slot.");
                if(slot==5 && ItemHandler.IsArrow(type))continue;
                if(slot==5)required|=1<<5;
                var worn=p.Equipment.TryGetItem((byte)position);int category=(int)ItemHandler.GetPositionFromID(type);byte days;
                bool correctSlot=category==slot || slot==5 && category==4 && !ItemHandler.IsTwoHand(type) && type/1000!=421;
                if(worn==null || worn.UID!=item || worn.ID!=type || worn.Position!=position || !correctSlot
                    || !ConquerItemInformation.BaseInformations.ContainsKey(type) || type%10<rule.Require || type%10>9
                    || Convert.ToInt32(r[3])!=1 || Convert.ToInt32(r[4])!=0 || Convert.ToInt32(r[5])!=0
                    || !byte.TryParse(Convert.ToString(r[6]),out days))return false;
                if(days>0 && (r.IsDBNull(7) || now>=DateTime.FromBinary(Convert.ToInt64(r[7])).AddDays(days)))return false;
                seen|=1<<slot;
            }
            return (seen&required)==required;
        }
        // Revora lifetime checklist: the seven regular Daily Quest Envoy services.
        // Paid completion through those services is valid; legacy flags are not receipts.
        private static bool CompletedDailies(NpgsqlConnection c,NpgsqlTransaction t,uint uid){
            foreach(string table in new[]{"revora_worship_claims","revora_price_runs","revora_soul_runs","revora_rare_runs","revora_magnolia_runs","revora_exorcism_daily","revora_soul_ball_runs"})
                if(!Table(c,t,table))return false;
            using(var q=Q.Command(c,t,@"SELECT
EXISTS(SELECT 1 FROM revora_worship_claims WHERE uid=@uid AND NOT legacy AND chi_uid IS NOT NULL)
AND EXISTS(SELECT 1 FROM revora_price_runs WHERE uid=@uid AND completed AND NOT legacy AND reward_uid IS NOT NULL AND completed_at IS NOT NULL)
AND EXISTS(SELECT 1 FROM revora_soul_runs WHERE uid=@uid AND state IN('complete','paid') AND cardinality(rewards)>=2)
AND EXISTS(SELECT 1 FROM revora_rare_runs WHERE uid=@uid AND completed AND NOT legacy AND reward_uid IS NOT NULL AND completed_at IS NOT NULL)
AND EXISTS(SELECT 1 FROM revora_magnolia_runs WHERE uid=@uid AND completed AND NOT legacy AND planted_seed IS NOT NULL AND reward_uid IS NOT NULL AND completed_at IS NOT NULL)
AND EXISTS(SELECT 1 FROM revora_exorcism_daily WHERE uid=@uid AND state='claimed' AND NOT legacy AND completed_day<>'' AND claimed_at IS NOT NULL AND cardinality(reward_uids)>0)
AND EXISTS(SELECT 1 FROM revora_soul_ball_runs WHERE uid=@uid AND NOT legacy AND completed_day<>'' AND completed_at IS NOT NULL AND reward_uid IS NOT NULL)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool Finished(NpgsqlConnection c,NpgsqlTransaction t,Rule rule,uint uid,int reborn,int level,GameState p){
            if(rule.Task==40)return CompletedDailies(c,t,uid);
            // Revora's saved Twin City introduction replaces the retired beginner map.
            // Only the atomically rewarded final step qualifies, including after rebirth.
            if(rule.Task==3){
                if(!Table(c,t,"revora_newcomer_intro"))return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_newcomer_intro WHERE uid=@uid AND stage=4 AND reward_uid IS NOT NULL)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
            }
            if(rule.Task==31){using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_enlight_awards WHERE donor=@uid AND NOT legacy)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);}
            if(rule.Task==43)return p.Entity.Level==level && p.Entity.Reborn==reborn && p.Entity.BattlePower>=rule.Progress;
            if(rule.Task==11)return SavedGuild(c,t,uid);
            if(rule.Task==4){
                if(!Table(c,t,"revora_xp_kills"))return false;
                using(var q=Q.Command(c,t,"SELECT count(*)>=@required FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid,"required",(int)rule.Require))return Equals(q.ExecuteScalar(),true);
            }
            if(rule.Task==32){using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_guild_pk_participation WHERE uid=@uid)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);}
            if(rule.Task==26)return ChampionParticipation(c,t,uid);
            if(rule.Task==33)return EnteredCtf(c,t,uid);
            if(rule.Task==12)return MadeTeam(c,t,uid);
            if(rule.Task==36)return Married(c,t,uid);
            if(rule.Task==35)return BuiltHouse(c,t,uid);
            if(rule.Task==41)return AcceptedApprentice(c,t,uid);
            if(rule.Task==39)return DisCityEntered(c,t,uid);
            if(rule.Task==15)return LotteryPlayed(c,t,uid);
            if(rule.Task==18 || rule.Task==19)return TeamTournamentParticipation(c,t,uid,rule.Task==18?0:1);
            if(rule.Task==14)return TeamArenaWins(c,t,uid)>=rule.Progress;
            if(rule.Task==7)return ArenaWins(c,t,uid)>=rule.Progress;
            if(rule.Task==13)return MutualFriends(c,t,uid)>=rule.Progress;
            if(rule.Task==5)return QualitySet(c,t,rule,p);
            if(rule.Task==6)return Promoted(c,t,uid);
            if(rule.Task==45)return MeteorUpgrade(c,t,uid);
            if(rule.Task==44)return EquippedSteed(c,t,p);
            if(rule.Task==9)return UsedHourPotion(c,t,uid);
            if(rule.Task==16)return ComposedEquipment(c,t,uid);
            if(rule.Task==22 || rule.Task==23)return EquippedBonus(c,t,rule,p);
            if(rule.Task==17)return EliteParticipation(c,t,uid);
            if(rule.Task==38)return ElitePlacement(c,t,uid,rule.Require);
            if(rule.Task==46)return Refined(c,t,rule,p);
            if(rule.Task==29)return Purified(c,t,rule,p);
            if(rule.Task==34)return SavedJiangScore(c,t,uid)>=rule.Progress;
            if(rule.Task==30)return SavedChiScore(c,t,uid)>=rule.Progress;
            if(rule.Task==37)return Donation(c,t,uid)>=rule.Progress;
            if(rule.Task==1)return level>=rule.Progress;
            if(rule.Task==2)return reborn>=rule.Progress;
            if(rule.Task==20)return SuperTalismans(c,t,uid)>=rule.Progress;
            if(rule.Task==24)return Subclasses(c,t,uid)>=rule.Progress;
            if(rule.Task==27)return Equipped(c,t,p,false)>=rule.Progress;
            if(rule.Task==28)return Equipped(c,t,p,true)>=rule.Progress;
            if(rule.Task==42)return Victory(c,t,"revora_snow_banshee_runs",uid) || Victory(c,t,"revora_terato_runs",uid);
            return false;
        }
        private static byte[] Blob(SafeDictionary<uint,WayofHeroes.StageInfo> stages){
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream)){
                if(stages.Count>255)throw new InvalidOperationException("Way of Heroes page limit exceeded.");
                writer.Write((byte)stages.Count);
                foreach(var stage in stages.Values)stage.WriteItem(writer);
                writer.Flush();return stream.ToArray();
            }
        }
        private static void Merge(SafeDictionary<uint,WayofHeroes.StageInfo> stages,byte[] blob){
            if(blob==null || blob.Length==0)return;
            using(var stream=new MemoryStream(blob))using(var reader=new BinaryReader(stream)){
                int count=reader.ReadByte();
                for(int i=0;i<count;i++){
                    var saved=new WayofHeroes.StageInfo().ReadItem(reader);
                    WayofHeroes.StageInfo current;
                    if(!stages.TryGetValue(saved.UID,out current)){stages.Add(saved.UID,saved);continue;}
                    current.Done|=saved.Done;
                    foreach(var sub in saved.SubStages.Values){
                        WayofHeroes.SubStageInfo existing;
                        if(!current.SubStages.TryGetValue(sub.UID,out existing))current.SubStages.Add(sub.UID,sub);
                        else {existing.Done|=sub.Done;existing.points=Math.Max(existing.points,sub.points);}
                    }
                }
                if(stream.Position!=stream.Length)throw new InvalidOperationException("Invalid Way of Heroes progress blob.");
            }
        }
        private static void Remember(SafeDictionary<uint,WayofHeroes.StageInfo> stages,uint stageUid,uint points){
            uint page=stageUid/100;
            WayofHeroes.StageInfo stage;
            if(!stages.TryGetValue(page,out stage)){
                stage=new WayofHeroes.StageInfo();stage.UID=page;stages.Add(page,stage);
            }
            if(stageUid%100==0){stage.Done=true;return;}
            if(!stage.SubStages.ContainsKey(stageUid))stage.SubStages.Add(stageUid,new WayofHeroes.SubStageInfo());
            var sub=stage.SubStages[stageUid];sub.UID=stageUid;sub.Done=true;sub.points=Math.Max(points,sub.points);
        }
        private static bool Already(SafeDictionary<uint,WayofHeroes.StageInfo> stages,uint stageUid){
            WayofHeroes.StageInfo stage;
            return stages.TryGetValue(stageUid/100,out stage) && (stageUid%100==0?stage.Done:stage.SubStages.ContainsKey(stageUid) && stage.SubStages[stageUid].Done);
        }
        private static SafeDictionary<uint,WayofHeroes.StageInfo> ReadState(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var stages=new SafeDictionary<uint,WayofHeroes.StageInfo>();
            using(var q=Q.Command(c,t,"SELECT wayofheroes FROM wayofheroes WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)){
                object value=q.ExecuteScalar();
                if(value==null)throw new InvalidOperationException("Way of Heroes progress row is missing.");
                if(value!=DBNull.Value)Merge(stages,(byte[])value);
            }
            using(var q=Q.Command(c,t,"SELECT stage_uid FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var reader=q.ExecuteReader())while(reader.Read())Remember(stages,Convert.ToUInt32(reader[0]),0);
            return stages;
        }
        private static void WriteBlob(NpgsqlConnection c,NpgsqlTransaction t,GameState p,SafeDictionary<uint,WayofHeroes.StageInfo> stages){
            if(Q.Execute(c,t,"UPDATE wayofheroes SET wayofheroes=@blob WHERE uid=@uid","blob",Blob(stages),"uid",(long)p.Entity.UID)!=1)
                throw new InvalidOperationException("Way of Heroes progress row is missing.");
        }
        internal static void Save(GameState p){
            if(!enabled || p==null || p.Entity==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot){
                if(p.Entity.PersistenceRequiresReload)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))q.ExecuteScalar();
                    var stages=ReadState(c,t,p);
                    Merge(stages,Blob(p.Way2Heroes));
                    WriteBlob(c,t,p,stages);t.Commit();p.Way2Heroes=stages;
                }}
            }
        }
        private static ConquerItem Reward(uint id){
            ushort dur=ConquerItemInformation.BaseInformations[id].Durability;
            return new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Plus=id>=730001 && id<=730008?(byte)(id-730000):(byte)0,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        internal static void Restore(GameState p){
            try{Save(p);}catch(Exception ex){Q.Fail(p,"way-of-heroes-restore",ex);}
        }
        internal static bool ValidPacket(byte[] packet,GameState p){
            if(!Ready(p) || packet==null || packet.Length<17 || packet.Length>64)return false;
            int size=BitConverter.ToUInt16(packet,0);
            return size+8==packet.Length && BitConverter.ToUInt16(packet,2)==PacketId
                && BitConverter.ToUInt64(packet,size)==PacketHandler.ClientSeal;
        }
        internal static void Claim(byte[] packet,GameState p){
            if(!ValidPacket(packet,p))return;
            byte type=packet[4];
            if(type!=1 && type!=2){Say(p,"That Way of Heroes prize cannot be claimed yet.");return;}
            uint raw=BitConverter.ToUInt32(packet,5);
            if(type==2 && (raw<1 || raw>10) || type==1 && raw%100==0){Say(p,"That Way of Heroes prize cannot be claimed yet.");return;}
            uint stageUid=type==2?raw*100:raw;
            lock(p.ItemSyncRoot)try{
                if(!Ready(p))return;
                if(!Rules.ContainsKey(stageUid) || !Rules[stageUid].Owned){Say(p,"That Way of Heroes prize cannot be claimed yet.");return;}
                Rule rule=Rules[stageUid];
                if(!ConquerItemInformation.BaseInformations.ContainsKey(rule.Prize)){Say(p,"That Way of Heroes prize cannot be claimed yet.");return;}
                if(p.Inventory.Count+rule.Count>40){Say(p,"Your inventory is Full.");return;}
                var rewards=new List<ConquerItem>();
                bool paid=false;SafeDictionary<uint,WayofHeroes.StageInfo> stages=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    // Match payment/reset ordering: global database lock before owner rows.
                    if(rule.Task==37)Q.Execute(c,t,"SELECT pg_advisory_xact_lock(67112064)");
                    int reborn=0,level=0;
                    using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var reader=q.ExecuteReader()){
                        if(!reader.Read())throw new InvalidOperationException("Way of Heroes owner missing.");
                        reborn=Convert.ToInt32(reader[0]);level=Convert.ToInt32(reader[1]);
                    }
                    if(!Open(reborn,level,rule.Open)){Say(p,"That Way of Heroes stage is not open.");return;}
                    stages=ReadState(c,t,p);
                    object prior;
                    using(var q=Q.Command(c,t,"SELECT legacy FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage FOR UPDATE","uid",(long)p.Entity.UID,"stage",(int)stageUid))
                        prior=q.ExecuteScalar();
                    if(prior!=null || Already(stages,stageUid)){
                        if(prior==null){
                            Remember(stages,stageUid,0);
                            if(Q.Execute(c,t,@"INSERT INTO revora_way_of_heroes_claims(uid,stage_uid,prize_type,prize_count,legacy,claimed_at)
VALUES(@uid,@stage,@prize,@count,true,@now)","uid",(long)p.Entity.UID,"stage",(int)stageUid,"prize",(int)rule.Prize,"count",(int)rule.Count,"now",Clock())!=1)
                                throw new InvalidOperationException("Way of Heroes legacy claim was not saved.");
                            WriteBlob(c,t,p,stages);
                            t.Commit();p.Way2Heroes=stages;
                        }
                        Say(p,"You already claimed that prize.");return;
                    }
                    if(type==2?!MainFinished(stages,stageUid):!Finished(c,t,rule,p.Entity.UID,reborn,level,p)){Say(p,"You have not finished that Way of Heroes goal.");return;}
                    var ids=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var reader=q.ExecuteReader())while(reader.Read()){if(ids.Count>=40)throw new InvalidOperationException("Way of Heroes inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(reader[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Way of Heroes inventory changed.");
                    for(int n=0;n<rule.Count;n++){
                        var reward=Reward(rule.Prize);rewards.Add(reward);
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,1,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Way of Heroes prize was not saved.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_way_of_heroes_claims(uid,stage_uid,prize_type,prize_count,legacy,claimed_at)
VALUES(@uid,@stage,@prize,@count,false,@now)","uid",(long)p.Entity.UID,"stage",(int)stageUid,"prize",(int)rule.Prize,"count",(int)rule.Count,"now",Clock())!=1)
                        throw new InvalidOperationException("Way of Heroes claim was not saved.");
                    Remember(stages,stageUid,rule.Progress);
                    WriteBlob(c,t,p,stages);
                    if(!Ready(p))throw new InvalidOperationException("Way of Heroes session changed.");
                    t.Commit();paid=true;
                }}
                if(!paid)return;
                p.Way2Heroes=stages;
                var before=BeforeInstall;if(before!=null)before(p);
                Q.Install(p,new uint[0],rewards);
                var response=new WayofHeroes(stageUid/100);response.check(stages[stageUid/100]);p.Send(response);
                Say(p,"You claimed "+rule.Count.ToString()+" bound prize.");
            }catch(Exception ex){Q.Fail(p,"way-of-heroes",ex);}
        }
    }
}
