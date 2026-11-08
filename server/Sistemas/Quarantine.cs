using Q = Revora.Game.Features.QuestInventory;
using System;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using System.Collections.Generic;

namespace Revora.Sistemas
{
    public unsafe class Quarantine
    {
        public static ThreadSafeDictionary<uint, GameState> White = new ThreadSafeDictionary<uint, GameState>(50);
        public static ThreadSafeDictionary<uint, GameState> Black = new ThreadSafeDictionary<uint, GameState>(50);
        public static int BlackScore, WhiteScore = 0;
        public static volatile bool Started = false;
        private static int lifecycleOwner;
        private static bool TryOwnLifecycle() { return System.Threading.Interlocked.CompareExchange(ref lifecycleOwner, 1, 0) == 0; }
        private static void ReleaseLifecycle() { System.Threading.Volatile.Write(ref lifecycleOwner, 0); }
        private static bool ledgerReady;
        private static Guid roundId;
        private static DateTime roundStarted;
        private sealed class Result { internal int White, Black; internal long[] Winners; internal uint[] Souls; internal DateTime Ended; }
        private static Result pendingResult;
        private static bool scoringFault;
        private static readonly Dictionary<uint,Tuple<GameState,ConquerItem>> uniforms=new Dictionary<uint,Tuple<GameState,ConquerItem>>();
        internal static bool CurrentParticipant(GameState p) {
            GameState member;
            return Started && p.Entity.MapID==Map &&
                ((White.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p)) ||
                 (Black.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p)));
        }
        private static void RememberUniform(GameState p,ConquerItem item) { uniforms[p.Entity.UID]=Tuple.Create(p,item); }
        private static bool RemoveUniform(GameState p) {
            Tuple<GameState,ConquerItem> lease;
            if(!uniforms.TryGetValue(p.Entity.UID,out lease) || !ReferenceEquals(lease.Item1,p))return false;
            lock(p.ItemSyncRoot) {
                var item=lease.Item2;bool equipped=ReferenceEquals(p.Equipment.Objects[8],item);
                if(equipped)p.Equipment.UnequipCommittedCosmetic(ConquerItem.Garment,item);
                ConquerItem bagItem;
                if(p.Inventory.TryGetItem(item.UID,out bagItem) && ReferenceEquals(bagItem,item))
                    if(!p.Inventory.Remove(item.UID,Enums.ItemUse.None,false))throw new InvalidOperationException("Team uniform cleanup requires reload.");
                p.Send(new ItemUsage(true){UID=item.UID,Action=ItemUsage.Mode.RemoveInventory});
                uniforms.Remove(p.Entity.UID);return equipped;
            }
        }

        public const uint NpcId=900183;
        internal static void PlaceNpc() {
            Map town;
            if(!Kernel.Maps.TryGetValue(1002,out town) || town.BaseID!=1002 || Game.Features.TwinCityPlaza.IsClosed(NpcId))throw new InvalidOperationException("Team Tournament public clerk is unavailable.");
            Map retiredMap=null;Revora.Interfaces.INpc retired=null;int identities=0;
            foreach(var world in Kernel.Maps.Values){
                Revora.Interfaces.INpc existing;
                if(!world.Npcs.TryGetValue(NpcId,out existing))continue;
                if(++identities>1)throw new InvalidOperationException("Team Tournament duplicate clerk identity.");
                if(world.ID==1002 && existing.X==273 && existing.Y==140 && existing.Mesh==50340 && existing.Type==Enums.NpcType.Talker && existing.Name=="TeamMarshal")continue;
                // Both exact retired plaza slots exist in deployed 6711 catalogs.
                if(world.ID==6000 && (existing.X==54 || existing.X==66) && existing.Y==82 && existing.Mesh==6056 && existing.Type==Enums.NpcType.Talker && existing.Name=="PK Black&White"){retiredMap=world;retired=existing;continue;}
                throw new InvalidOperationException("Team Tournament clerk identity collision: map="+world.ID+" x="+existing.X+" y="+existing.Y+" mesh="+existing.Mesh+" type="+existing.Type+" name="+existing.Name);
            }
            if(town.Npcs.ContainsKey(NpcId))return;
            if(!town.Floor[273,140,MapObjectType.Player] || town.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,273,140)<2))throw new InvalidOperationException("Team Tournament clerk site is occupied.");
            if(retired!=null){
                Revora.Interfaces.INpc secondary;
                if(Game.Map.SecoundNpc.TryGetValue(NpcId,out secondary) && !ReferenceEquals(secondary,retired))throw new InvalidOperationException("Team Tournament secondary clerk identity collision.");
                retiredMap.RemoveNpc(retired);Game.Map.SecoundNpc.Remove(NpcId);
            }
            town.AddNpc(new NpcSpawn{UID=NpcId,MapID=1002,X=273,Y=140,Mesh=50340,Name="TeamMarshal",Type=Enums.NpcType.Talker});
            Console.WriteLine("[team-tournament] clerk_ready map=1002 x=273 y=140");
        }
        public static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=NpcId)return false;
            if(!Q.Near(p,NpcId,1002))return true;
            if(request.OptionID==1){Join(p);return true;}
            var dialog=new Npcs(p);
            dialog.Text(Started?"Black against white. First to 100 hits wins; otherwise, the leading team wins at the bell. Ready your blade.":"We muster at eight in the evening on Sunday, Monday, Tuesday, Thursday and Friday. Return then, blade in hand.");
            if(Started)dialog.Option("Join the tournament.",1);
            dialog.Option("Farewell.",255);dialog.Send();return true;
        }
        internal static void Initialize()
        {
            if(ledgerReady)return;
            using (var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_quarantine_rounds(
 id uuid PRIMARY KEY,started_at timestamptz NOT NULL,ended_at timestamptz,
 state text NOT NULL CHECK(state IN('active','won','draw','cancelled')),
 white_score integer NOT NULL DEFAULT 0 CHECK(white_score>=0),black_score integer NOT NULL DEFAULT 0 CHECK(black_score>=0),
 winners bigint[] NOT NULL DEFAULT '{}',CHECK((state='active')=(ended_at IS NULL)),CHECK(ended_at IS NULL OR ended_at>=started_at));
 UPDATE revora_quarantine_rounds SET state='cancelled',ended_at=GREATEST(started_at,clock_timestamp()) WHERE state='active';");
                QuarantinePurses.Initialize(c,t);
                QuarantineAdmissions.Initialize(c,t);
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_quarantine_hits(
 round_id uuid NOT NULL REFERENCES revora_quarantine_rounds(id),sequence integer NOT NULL CHECK(sequence>0),
 attacker bigint NOT NULL,target bigint NOT NULL,spell integer NOT NULL,white_team boolean NOT NULL,
 occurred_at timestamptz NOT NULL,PRIMARY KEY(round_id,sequence),CHECK(attacker<>target),
 FOREIGN KEY(round_id,attacker) REFERENCES revora_quarantine_admissions(round_id,uid),
 FOREIGN KEY(round_id,target) REFERENCES revora_quarantine_admissions(round_id,uid));");
                t.Commit();
            }}
            ledgerReady=true;
        }
        private static void SaveStart()
        {
            if(!ledgerReady)throw new InvalidOperationException("Team Tournament ledger is unavailable.");
            if(roundId==Guid.Empty){roundId=Guid.NewGuid();roundStarted=DateTime.UtcNow;}
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,"INSERT INTO revora_quarantine_rounds(id,started_at,state) VALUES(@id,@start,'active') ON CONFLICT(id) DO NOTHING","id",roundId,"start",roundStarted);
                using(var q=Q.Command(c,null,"SELECT count(*) FROM revora_quarantine_rounds WHERE id=@id AND started_at=@start AND state='active' AND white_score=0 AND black_score=0 AND cardinality(winners)=0","id",roundId,"start",roundStarted))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Team Tournament start changed.");
            }
        }
        private static void SaveResult()
        {
            if(scoringFault && pendingResult==null)ReconcileScore();
            if(pendingResult==null){
                var winning=WhiteScore>BlackScore?White:BlackScore>WhiteScore?Black:null;
                var ids=winning==null?new long[0]:winning.Values.Where(p=>Q.Player(p,false) && p.Entity.MapID==Map &&
                    p.Map!=null && p.Map.ID==Map).Select(p=>(long)p.Entity.UID).Distinct().OrderBy(id=>id).ToArray();
                pendingResult=new Result{White=WhiteScore,Black=BlackScore,Winners=ids,Souls=ids.Select(id=>QuarantinePurses.ChooseSoul()).ToArray(),Ended=DateTime.UtcNow};
            }
            var result=pendingResult;string state=result.White==result.Black?"draw":"won";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,"UPDATE revora_quarantine_rounds SET state=@state,ended_at=@end,white_score=@white,black_score=@black,winners=@winners WHERE id=@id AND state='active'",
                    "state",state,"end",result.Ended,"white",result.White,"black",result.Black,"winners",result.Winners,"id",roundId);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_quarantine_rounds WHERE id=@id AND state=@state AND ended_at=@end AND white_score=@white AND black_score=@black AND winners=@winners",
                    "id",roundId,"state",state,"end",result.Ended,"white",result.White,"black",result.Black,"winners",result.Winners))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Team Tournament result changed.");
                QuarantinePurses.Freeze(c,t,roundId,result.Ended,result.Winners,result.Souls);
                QuarantineAdmissions.End(c,t,roundId);
                t.Commit();
            }}
        }
        private static void ReconcileScore() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT white_score,black_score FROM revora_quarantine_rounds WHERE id=@id AND state='active'","id",roundId))
            using(var r=q.ExecuteReader()){
                if(!r.Read())throw new InvalidOperationException("Team Tournament score recovery requires an active round.");
                WhiteScore=r.GetInt32(0);BlackScore=r.GetInt32(1);
            }}
            // Remain frozen until End completes; a failed/ambiguous hit is never replayed.
        }
        private static bool SaveHit(Entity attacker,Entity target,SpellInformation spell) {
            if(!Q.Player(attacker.Owner) || !Q.Player(target.Owner))return false;
            bool white=White.ContainsKey(attacker.UID);
            uint sourceUniform=attacker.Owner.Equipment.Objects[8].UID,targetUniform=target.Owner.Equipment.Objects[8].UID;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,"SET LOCAL lock_timeout='1s'");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM (SELECT id FROM revora_quarantine_rounds WHERE id=@id AND state='active' AND white_score=@w AND black_score=@b FOR UPDATE) r","id",roundId,"w",WhiteScore,"b",BlackScore))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Team Tournament saved score changed.");
                using(var q=Q.Command(c,t,@"SELECT uid FROM revora_quarantine_admissions WHERE round_id=@id AND entered AND NOT ended AND NOT returned
AND ((uid=@a AND white_team=@white AND uniform_uid=@au) OR (uid=@b AND white_team<>@white AND uniform_uid=@bu)) ORDER BY uid FOR UPDATE",
                    "id",roundId,"a",(long)attacker.UID,"b",(long)target.UID,"white",white,"au",(long)sourceUniform,"bu",(long)targetUniform))
                using(var r=q.ExecuteReader()){int count=0;while(r.Read())count++;if(count!=2)return false;}
                Q.Execute(c,t,@"INSERT INTO revora_quarantine_hits(round_id,sequence,attacker,target,spell,white_team,occurred_at)
VALUES(@id,@seq,@a,@b,@spell,@white,@now)","id",roundId,"seq",checked(WhiteScore+BlackScore+1),"a",(long)attacker.UID,"b",(long)target.UID,"spell",(int)spell.ID,"white",white,"now",DateTime.UtcNow);
                Q.Execute(c,t,"UPDATE revora_quarantine_rounds SET white_score=@w,black_score=@b WHERE id=@id","id",roundId,"w",checked(WhiteScore+(white?1:0)),"b",checked(BlackScore+(white?0:1)));
                if(!Q.Player(attacker.Owner) || !Q.Player(target.Owner))throw new InvalidOperationException("Team Tournament scoring session changed.");
                t.Commit();return true;
            }}
        }
        public static ushort Map = 10360, X = 119, Y = 159;
        public static void AddQuarantineKill(GameState client)
        {
            client.quarantineKill++;
            UpdateQuarantineScore(client);
        }
        public static void AddQuarantineDeath(GameState client)
        {
            client.quarantineDeath++;
            UpdateQuarantineScore(client);
            Random random = new Random();
            int r = random.Next(1, 17);
            if (r == 1) client.Entity.Teleport(10360, 117, 203);
            if (r == 2) client.Entity.Teleport(10360, 127, 281);
            if (r == 3) client.Entity.Teleport(10360, 071, 253);
            if (r == 4) client.Entity.Teleport(10360, 174, 271);
            if (r == 5) client.Entity.Teleport(10360, 176, 334);
            if (r == 6) client.Entity.Teleport(10360, 239, 330);
            if (r == 7) client.Entity.Teleport(10360, 230, 270);
            if (r == 8) client.Entity.Teleport(10360, 241, 381);
            if (r == 9) client.Entity.Teleport(10360, 306, 383);
            if (r == 10) client.Entity.Teleport(10360, 290, 318);
            if (r == 11) client.Entity.Teleport(10360, 360, 229);
            if (r == 12) client.Entity.Teleport(10360, 361, 253);
            if (r == 13) client.Entity.Teleport(10360, 274, 221);
            if (r == 14) client.Entity.Teleport(10360, 198, 212);
            if (r == 15) client.Entity.Teleport(10360, 190, 149);
            if (r == 16) client.Entity.Teleport(10360, 262, 150);
            if (r == 17) client.Entity.Teleport(10360, 328, 157);


        }
        public static void UpdateQuarantineScore(GameState client)
        {
            string[] scores = new string[3];
            scores[0] = "Black Team: " + BlackScore.ToString() + " Hit";
            scores[1] = "White Team: " + WhiteScore.ToString() + " Hit";
            scores[2] = "My Hits: " + client.quarantineKill + " Won, " + client.quarantineDeath + " Lost";
            for (int i = 0; i < scores.Length; i++)
            {
                Message msg = new Message(scores[i], System.Drawing.Color.Red, i == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                client.Send(msg);
            }
        }
        internal static bool EligibleHit(Entity attacker, Entity attacked, SpellInformation spell)
        {
            if (!Started || scoringFault || pendingResult != null || attacker == null || attacked == null || spell == null ||
                !AllowedSpells.Contains(spell.ID) || attacker.EntityFlag != EntityFlag.Player ||
                attacked.EntityFlag != EntityFlag.Player || attacker.UID == attacked.UID ||
                attacker.MapID != Map || attacked.MapID != Map || attacker.Hitpoints == 0 || attacked.Hitpoints == 0)
                return false;
            var source = attacker.Owner;
            var target = attacked.Owner;
            if (source == null || target == null || !ReferenceEquals(source.Entity, attacker) ||
                !ReferenceEquals(target.Entity, attacked) || source.Equipment == null || target.Equipment == null)
                return false;
            GameState sourceWhite, sourceBlack, targetWhite, targetBlack;
            bool sw = White.TryGetValue(attacker.UID, out sourceWhite);
            bool sb = Black.TryGetValue(attacker.UID, out sourceBlack);
            bool tw = White.TryGetValue(attacked.UID, out targetWhite);
            bool tb = Black.TryGetValue(attacked.UID, out targetBlack);
            if (sw == sb || tw == tb || sw == tw ||
                !ReferenceEquals(sw ? sourceWhite : sourceBlack, source) ||
                !ReferenceEquals(tw ? targetWhite : targetBlack, target)) return false;
            var sourceGarment = source.Equipment.Objects[8];
            var targetGarment = target.Equipment.Objects[8];
            return sourceGarment != null && targetGarment != null &&
                sourceGarment.ID == (sw ? 181325u : 181525u) &&
                targetGarment.ID == (tw ? 181325u : 181525u);
        }

        public static bool HandleQuarantine(Entity attacker, Entity attacked, SpellInformation spell)
        {
            if (!TryOwnLifecycle()) return false;
            try { return HandleOwned(attacker, attacked, spell); }
            finally { ReleaseLifecycle(); }
        }
        private static bool HandleOwned(Entity attacker, Entity attacked, SpellInformation spell)
        {
            if (!EligibleHit(attacker, attacked, spell)) return false;
            try { if(!SaveHit(attacker,attacked,spell))return false; }
            catch(Exception error){scoringFault=true;Console.WriteLine("[team-tournament] scoring_paused="+error.GetType().Name);return false;}
            if (White.ContainsKey(attacker.UID)) WhiteScore++;
            else BlackScore++;
            AddQuarantineKill(attacker.Owner);
            AddQuarantineDeath(attacked.Owner);
            if (BlackScore >= 100 || WhiteScore >= 100) EndOwned();
            return true;
        }
        private static DateTime nextSettlement;
        internal static void Tick(DateTime now) {
            if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Team Tournament requires UTC time.");
            if(!Started || !TryOwnLifecycle())return;
            try {
                if(now<nextSettlement)return;
                if(!scoringFault && pendingResult==null && WhiteScore<100 && BlackScore<100 && now<roundStarted.AddMinutes(30))return;
                nextSettlement=now.AddSeconds(10);
                try { EndOwned(); }
                catch(Exception error){Console.WriteLine("[team-tournament] settlement_pending="+error.GetType().Name);}
            } finally {ReleaseLifecycle();}
        }
        public static void StartScheduled(DateTime localNow) {
            // Keep the existing server-local calendar; one persisted identity per date.
            if(localNow.Hour!=20 || localNow.Minute!=0 || localNow.Second>55 ||
                localNow.DayOfWeek==DayOfWeek.Wednesday || localNow.DayOfWeek==DayOfWeek.Saturday)return;
            if(!TryOwnLifecycle())return;
            try {
                if(Started)return;
                Guid scheduledId;
                using(var hash=System.Security.Cryptography.SHA256.Create())
                    scheduledId=new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("revora.team-tournament.v1:"+localNow.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture))).Take(16).ToArray());
                // Do not overwrite a pending start from another lifecycle owner/slot.
                if(roundId!=Guid.Empty && roundId!=scheduledId)return;
                if(roundId==Guid.Empty){
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT count(*) FROM revora_quarantine_rounds WHERE id=@id","id",scheduledId))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0)return;}
                    roundId=scheduledId;roundStarted=DateTime.UtcNow;
                }
                StartOwned();
            } finally {ReleaseLifecycle();}
        }
        public static void Start()
        {
            if (!TryOwnLifecycle()) return;
            try { StartOwned(); }
            finally { ReleaseLifecycle(); }
        }
        private static void StartOwned()
        {
            if (!Started)
            {
                SaveStart();
                pendingResult=null;scoringFault=false;nextSettlement=DateTime.MinValue;uniforms.Clear();
                Started = true;
                WhiteScore = 0;
                BlackScore = 0;
                White = new ThreadSafeDictionary<uint, GameState>(50);
                Black = new ThreadSafeDictionary<uint, GameState>(50);
                foreach (var client in Server.GamePool)
                   client.MessageBox("The Team Tournament has begun. The purse: CPs, gold, and Revora Points. Will you attend?", p => { p.Entity.Teleport(1002, 275, 140); }, null);
            }
        }
        public static void End()
        {
            if (!TryOwnLifecycle()) return;
            try { EndOwned(); }
            finally { ReleaseLifecycle(); }
        }
        private static void EndOwned()
        {
            if (Started)
            {
                SaveResult();
                Started = false;
                foreach (var client in Server.GamePool)
                {
                    CheckOwned(client);
                    if (client.Entity.MapID == Map)
                    {
                        if(pendingResult.Winners.Contains((long)client.Entity.UID)) GiveReward(client);
                        if(!QuarantineAdmissions.Return(client))continue;
                        client.Send(new Message("", System.Drawing.Color.Red, Message.FirstRightCorner));
                        client.Send(new Message("This event has ended. Return tomorrow!", System.Drawing.Color.Red, Message.TopLeft));
                        client.quarantineDeath = 0;
                        client.quarantineKill = 0;
                        ClientEquip equips = new ClientEquip();
                        equips.DoEquips(client);
                        client.Send(equips);
                        UpdateQuarantineScore(client);
                    }
                }
                WhiteScore = 0;
                BlackScore = 0;
                Black = new ThreadSafeDictionary<uint, GameState>(50);
                White = new ThreadSafeDictionary<uint, GameState>(50);
                roundId=Guid.Empty;pendingResult=null;scoringFault=false;nextSettlement=DateTime.MinValue;uniforms.Clear();
            }
        }
        public static void Check(GameState client)
        {
            if (!TryOwnLifecycle()) return;
            try { CheckOwned(client); }
            finally { ReleaseLifecycle(); }
        }
        private static void CheckOwned(GameState client)
        {
            if (client.Entity.MapID != Map)
            {
                if (White.ContainsKey(client.Entity.UID))
                    White.Remove(client.Entity.UID);
                if (Black.ContainsKey(client.Entity.UID))
                    Black.Remove(client.Entity.UID);
            }
        }
        private static void GiveReward(GameState client)
        {
            string result=QuarantinePurses.Claim(client);
            if(result!=null)client.Send(result);
        }
        public static void Join(GameState client)
        {
            if (client == null || client.Entity == null || client.Equipment == null || client.Inventory == null ||
                client.ItemSyncRoot == null || !ReferenceEquals(client.Entity.Owner, client)) return;
            if (!TryOwnLifecycle()) return;
            try { lock (client.ItemSyncRoot) JoinOwned(client); }
            finally { ReleaseLifecycle(); }
        }
        private static void JoinOwned(GameState client)
        {
            // Never replace a garment or rebalance an existing participant on a repeated dialog.
            if (scoringFault || pendingResult!=null || White.ContainsKey(client.Entity.UID) || Black.ContainsKey(client.Entity.UID)) return;
            if (Started)
            {
                if (client.Equipment.Free(3))
                {
                    Npcs dialog = new Npcs(client);
                    dialog.Text("You must wear armor that can take a team color.");
                    dialog.Option("Very well.", 255);
                    dialog.Send();
                    return;
                }
                // Removal must succeed before PK mode, team membership or location changes.
                // In particular, a full bag must never be followed by clearing the occupied slot.
                var original=client.Equipment.Objects[8];
                if(original!=null && client.Inventory.Count>=40){client.Send("Make a place in your bag before joining the tournament.");return;}
                bool white=Black.Count>White.Count;
                var newItem=new ConquerItem(true){ID=white?181325u:181525u,UID=ConquerItem.ItemUID.Next,Durability=1000,MaximDurability=1000,Position=9,Bound=true};
                if(!QuarantineAdmissions.Reserve(roundId,client,white,newItem.UID,original==null?0:original.UID))return;
                if(original!=null && !client.Equipment.Remove(9))return;
                if (white)
                {
                    client.Entity.PKMode = Enums.PKMode.PK;
                    client.Send(new Data(true)
                    {
                        UID = client.Entity.UID,
                        ID = Data.ChangePKMode,
                        dwParam = (uint)client.Entity.PKMode
                    });
                    White.Add(client.Entity.UID, client);
                    client.Entity.Teleport(10360, 052, 196);
                }
                else
                {
                    client.Entity.PKMode = Enums.PKMode.PK;
                    client.Send(new Data(true)
                    {
                        UID = client.Entity.UID,
                        ID = Data.ChangePKMode,
                        dwParam = (uint)client.Entity.PKMode
                    });
                    Black.Add(client.Entity.UID, client);
                    client.Entity.Teleport(10360, 350, 156);
                }
                // Teleport may be refused or redirected when the arena is unavailable.
                // Never acknowledge admission or issue a uniform outside the selected spawn.
                ushort spawnX=white?(ushort)52:(ushort)350,spawnY=white?(ushort)196:(ushort)156;
                if(client.Entity.MapID!=10360 || client.Entity.X!=spawnX || client.Entity.Y!=spawnY)
                {
                    White.Remove(client.Entity.UID);
                    Black.Remove(client.Entity.UID);
                    QuarantineAdmissions.Return(client);
                    client.Send("The arena is unavailable. Your tournament entry was cancelled.");
                    return;
                }
                RememberUniform(client,newItem);
                if(!client.Equipment.Add(newItem))throw new InvalidOperationException("Team uniform admission changed.");
                newItem.Mode = Enums.ItemMode.Update;
                newItem.Send(client);
                ClientEquip equips = new ClientEquip();
                equips.DoEquips(client);
                client.Send(equips);
                client.Equipment.UpdateEntityPacket();
                QuarantineAdmissions.Entered(roundId,client.Entity.UID);
            }
            else
            {
                Npcs dialog = new Npcs(client);
                dialog.Text("This tournament is closed. It is not yet time!");
                dialog.Option("Thank you", 255);
                dialog.Send();
            }
        }
        public static readonly List<ushort> AllowedSpells = new List<ushort>()
        {
            1045,//Fastblade
            1046,//scentsword
            11005,//viperfang
            12350,//dragonslash
            12930//rageofwar
        };
    }
    internal static class QuarantinePurses
    {
        private static readonly object pollGate=new object();
        private static DateTime nextPoll;
        private static long cursor;
        internal static uint ChooseSoul() {
            var pool=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel==7).Select(i=>i.ID).OrderBy(id=>id).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("Team Tournament P7 catalog missing.");
            return pool[Kernel.Random.Next(pool.Length)];
        }
        internal static void Initialize(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t) {
            Q.Execute(c,t,@"ALTER TABLE revora_quarantine_rounds ADD COLUMN IF NOT EXISTS purses_frozen boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_quarantine_purses(
 round_id uuid NOT NULL REFERENCES revora_quarantine_rounds(id),uid bigint NOT NULL CHECK(uid>0),
 cp bigint NOT NULL CHECK(cp>=0),rvp bigint NOT NULL CHECK(rvp>=0),gold bigint NOT NULL CHECK(gold>=0),
 soul_type integer NOT NULL CHECK(soul_type BETWEEN 800000 AND 899999),earned_at timestamptz NOT NULL,
 claimed_at timestamptz,soul_uid bigint,PRIMARY KEY(round_id,uid),CHECK((claimed_at IS NULL)=(soul_uid IS NULL)));
CREATE INDEX IF NOT EXISTS revora_quarantine_pending ON revora_quarantine_purses(uid,earned_at) WHERE claimed_at IS NULL;
UPDATE revora_quarantine_rounds SET purses_frozen=true WHERE state<>'active' AND NOT purses_frozen;");
        }
        internal static void Freeze(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid round,DateTime earned,long[] ids,uint[] souls) {
            using(var q=Q.Command(c,t,"SELECT purses_frozen FROM revora_quarantine_rounds WHERE id=@id FOR UPDATE","id",round))
                if(Equals(q.ExecuteScalar(),true))return;
            if(ids.Length!=souls.Length || ids.Any(id=>id<=0) || ids.Distinct().Count()!=ids.Length)throw new InvalidOperationException("Team Tournament reward roster invalid.");
            for(int i=0;i<ids.Length;i++) {
                ConquerItemBaseInformation info;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(souls[i],out info) || info.PurificationLevel!=7)throw new InvalidOperationException("Team Tournament soul changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_quarantine_purses(round_id,uid,cp,rvp,gold,soul_type,earned_at)
SELECT @round,uid,300000000,50000,100000000000,@soul,@earned FROM entities WHERE uid=@uid",
                    "round",round,"uid",ids[i],"soul",(int)souls[i],"earned",earned)!=1)throw new InvalidOperationException("Team Tournament winner missing.");
            }
            if(Q.Execute(c,t,"UPDATE revora_quarantine_rounds SET purses_frozen=true WHERE id=@id AND NOT purses_frozen AND state IN('won','draw')","id",round)!=1)
                throw new InvalidOperationException("Team Tournament purse snapshot not sealed.");
        }
        internal static string Claim(GameState p) {
            if(!Q.Player(p,false) || !Game.Features.SessionInstall.TryEnter(p.Entity.UID))return null;
            try {
                if(!System.Threading.Monitor.TryEnter(p.ItemSyncRoot))return null;
                try {return ClaimOwned(p);} finally {System.Threading.Monitor.Exit(p.ItemSyncRoot);}
            }catch(Exception error){Q.Fail(p,"team-tournament-purse",error);return "Your tournament purse will wait. Please reconnect.";}
            finally {Game.Features.SessionInstall.Exit(p.Entity.UID);}
        }
        private static string ClaimOwned(GameState p) {
            if(!Q.Player(p,false) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return null;
            ConquerItem item;long nextCp,nextRvp;decimal nextGold;Guid round;uint soul;long cp,rvp,gold;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team Tournament owner missing.");
                using(var q=Q.Command(c,t,@"SELECT round_id,cp,rvp,gold,soul_type FROM revora_quarantine_purses
WHERE uid=@uid AND claimed_at IS NULL AND earned_at<=@now ORDER BY earned_at,round_id LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"now",DateTime.UtcNow))
                using(var r=q.ExecuteReader()) {if(!r.Read())return null;round=r.GetGuid(0);cp=r.GetInt64(1);rvp=r.GetInt64(2);gold=r.GetInt64(3);soul=checked((uint)r.GetInt32(4));}
                ConquerItemBaseInformation info;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(soul,out info) || info.PurificationLevel!=7)throw new InvalidOperationException("Team Tournament soul catalog changed.");
                if(p.Inventory.Count>=40)return "Make a place in your bag. Your tournament purse is safe.";
                nextCp=checked(p.Entity.ConquerPoints+cp);nextRvp=checked(p.Entity.RevoraPoint+rvp);nextGold=p.Entity.Money+(decimal)gold;
                if(nextCp>uint.MaxValue || nextRvp>uint.MaxValue || nextGold>Entity.MaximumSilver)return "Make room in your purse before claiming your tournament reward.";
                Game.Features.SilverBalance.AssertSaved(c,t,p);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Team Tournament inventory changed.");
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp",
                    "cp",nextCp,"rvp",nextRvp,"gold",nextGold,"uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)
                    throw new InvalidOperationException("Team Tournament purse changed.");
                item=Q.New(soul,true);Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_quarantine_purses SET claimed_at=@now,soul_uid=@item WHERE round_id=@round AND uid=@uid AND claimed_at IS NULL",
                    "now",DateTime.UtcNow,"item",(long)item.UID,"round",round,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Team Tournament claim changed.");
                if(!Q.Player(p,false) || p.Trade.InTrade || p.Booth!=null)throw new InvalidOperationException("Team Tournament session changed.");
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)nextCp);p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);Game.Features.SilverBalance.ApplyCommitted(p,(ulong)nextGold);
            }}
            Q.Install(p,new uint[0],new[]{item});return "Your tournament purse and bound soul are yours.";
        }
        internal static void Tick() {
            if(!System.Threading.Monitor.TryEnter(pollGate))return;
            try {
                var now=DateTime.UtcNow;if(now<nextPoll)return;nextPoll=now.AddSeconds(10);
                var ids=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>(long)p.Entity.UID).ToArray();if(ids.Length==0)return;
                var due=new List<uint>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT DISTINCT uid FROM revora_quarantine_purses WHERE uid=ANY(@ids) AND uid>@cursor AND claimed_at IS NULL AND earned_at<=@now ORDER BY uid LIMIT 32","ids",ids,"cursor",cursor,"now",now))
                using(var r=q.ExecuteReader())while(r.Read())due.Add(Convert.ToUInt32(r[0]));}
                if(due.Count==0){cursor=0;return;}cursor=due.Last();
                foreach(uint uid in due){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p)){string message=Claim(p);if(message=="Your tournament purse and bound soul are yours.")p.Send(message);}}
            }catch(Exception error){Console.WriteLine("[team-tournament-purse] poll_failure="+error.GetType().Name);}
            finally {System.Threading.Monitor.Exit(pollGate);}
        }
    }

    internal static class QuarantineAdmissions
    {
        internal static void Initialize(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t) {
            Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_quarantine_admissions(
 round_id uuid NOT NULL REFERENCES revora_quarantine_rounds(id),uid bigint NOT NULL CHECK(uid>0),white_team boolean NOT NULL,
 pkmode integer NOT NULL CHECK(pkmode BETWEEN 0 AND 255),uniform_uid bigint NOT NULL UNIQUE CHECK(uniform_uid>0),
 original_garment bigint NOT NULL CHECK(original_garment>=0),entered boolean NOT NULL DEFAULT false,
 ended boolean NOT NULL DEFAULT false,returned boolean NOT NULL DEFAULT false,PRIMARY KEY(round_id,uid),CHECK(NOT returned OR ended));
CREATE UNIQUE INDEX IF NOT EXISTS revora_quarantine_return_owner ON revora_quarantine_admissions(uid) WHERE NOT returned;
UPDATE revora_quarantine_admissions a SET ended=true FROM revora_quarantine_rounds r WHERE a.round_id=r.id AND r.state<>'active' AND NOT a.ended;");
        }
        internal static bool Reserve(Guid round,GameState p,bool white,uint uniform,uint original) {
            if(round==Guid.Empty || uniform==0 || !Q.Player(p) || !System.Threading.Monitor.IsEntered(p.ItemSyncRoot))return false;
            if(!Enum.IsDefined(typeof(Enums.PKMode),p.Entity.PKMode))throw new InvalidOperationException("Team Tournament PK mode invalid.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team Tournament entrant missing.");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_quarantine_admissions WHERE uid=@uid AND (NOT returned OR round_id=@round)","uid",(long)p.Entity.UID,"round",round))
                    if(Convert.ToInt64(q.ExecuteScalar())!=0)return false;
                using(var q=Q.Command(c,t,"SELECT state FROM revora_quarantine_rounds WHERE id=@id FOR UPDATE","id",round))
                    if(!Equals(q.ExecuteScalar(),"active"))return false;
                var garment=p.Equipment.Objects[8];
                if((garment==null?0:garment.UID)!=original)throw new InvalidOperationException("Team Tournament garment changed.");
                if(original!=0)using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND position=9 AND warehouse=0",
                    "item",(long)original,"uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Team Tournament garment ownership changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_quarantine_admissions(round_id,uid,white_team,pkmode,uniform_uid,original_garment)
VALUES(@round,@uid,@white,@mode,@uniform,@original)","round",round,"uid",(long)p.Entity.UID,"white",white,"mode",(int)p.Entity.PKMode,"uniform",(long)uniform,"original",(long)original)!=1)
                    throw new InvalidOperationException("Team Tournament reservation not saved.");
                t.Commit();return true;
            }}
        }
        internal static void Entered(Guid round,uint uid) {
            using(var c=DataHolder.MySqlConnection){c.Open();
                if(Q.Execute(c,null,"UPDATE revora_quarantine_admissions SET entered=true WHERE round_id=@round AND uid=@uid AND NOT ended AND NOT returned",
                    "round",round,"uid",(long)uid)!=1)throw new InvalidOperationException("Team Tournament entry acknowledgement changed.");
            }
        }
        internal static void End(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid round) {
            Q.Execute(c,t,"UPDATE revora_quarantine_admissions SET ended=true WHERE round_id=@round AND NOT ended","round",round);
        }
        private static readonly object returnGate=new object();
        private static DateTime nextReturn;
        private static long returnCursor;
        internal static bool Return(GameState p) {
            if(!Q.Player(p,false) || !Game.Features.SessionInstall.TryEnter(p.Entity.UID))return false;
            try { return ReturnOwned(p); }
            finally { Game.Features.SessionInstall.Exit(p.Entity.UID); }
        }
        private static bool RestoreGarment(GameState p,uint original) {
            if(!Q.Player(p,false) || !System.Threading.Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Team Tournament garment owner changed.");
            if(original==0 || p.Equipment.Objects[8]!=null)return false;
            ConquerItem item;
            if(!p.Inventory.TryGetItem(original,out item))return false;
            if(item.Position!=0 || item.Warehouse!=0)throw new InvalidOperationException("Team Tournament original garment projection changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team Tournament garment owner missing.");
                // Position 9 permits retry after a committed move whose cache install failed.
                if(Q.Execute(c,t,@"UPDATE items SET position=9 WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position IN(0,9)
AND NOT EXISTS(SELECT 1 FROM items WHERE entityid=@uid AND position=9 AND warehouse=0 AND uid<>@item)",
                    "item",(long)original,"uid",(long)p.Entity.UID,"type",(int)item.ID)!=1)
                    throw new InvalidOperationException("Team Tournament original garment ownership changed.");
                if(!Q.Player(p,false))throw new InvalidOperationException("Team Tournament garment session changed.");
                t.Commit();
            }}
            if(!p.Inventory.Remove(original,Enums.ItemUse.None,false))throw new InvalidOperationException("Team Tournament garment inventory requires reload.");
            item.Position=ConquerItem.Garment;item.IsWorn=true;item.StatsLoaded=false;
            if(!p.Equipment.Add(item,Enums.ItemUse.None))throw new InvalidOperationException("Team Tournament garment equipment requires reload.");
            p.Equipment.UpdateItemview(item);item.Send(p);
            p.Send(new ItemUsage(true){UID=original,dwParam=ConquerItem.Garment,Action=ItemUsage.Mode.EquipItem});
            return true;
        }
        private static bool ReturnOwned(GameState p) {
            if(!Q.Player(p,false) || !System.Threading.Monitor.TryEnter(p.ItemSyncRoot))return false;
            bool pk=false;
            try {
                pk=System.Threading.Monitor.TryEnter(p.PkSyncRoot);if(!pk)return false;
                Guid round;uint uniform,original;bool white,ended,entered;int mode;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT round_id,uniform_uid,white_team,pkmode,ended,entered,original_garment FROM revora_quarantine_admissions WHERE uid=@uid AND NOT returned","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()){if(!r.Read())return true;round=r.GetGuid(0);uniform=checked((uint)r.GetInt64(1));white=r.GetBoolean(2);mode=r.GetInt32(3);ended=r.GetBoolean(4);entered=r.GetBoolean(5);original=checked((uint)r.GetInt64(6));}}
                if(!ended && entered && Quarantine.CurrentParticipant(p))return true;
                if(mode<0 || mode>255 || !Enum.IsDefined(typeof(Enums.PKMode),(byte)mode))throw new InvalidOperationException("Team Tournament return mode invalid.");
                using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_quarantine_admissions SET ended=true WHERE round_id=@round AND uid=@uid AND NOT returned","round",round,"uid",(long)p.Entity.UID);}
                if(p.Entity.MapID!=1002 || p.Entity.X!=327 || p.Entity.Y!=303)p.Entity.Teleport(1002,327,303);
                if(p.Entity.MapID!=1002 || p.Entity.X!=327 || p.Entity.Y!=303)throw new InvalidOperationException("Team Tournament return did not reach town.");
                uint type=white?181325u:181525u;
                // Remove only the recorded uniform from this owner, never an unrelated garment.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team Tournament returning owner missing.");
                    Q.Execute(c,t,"DELETE FROM items WHERE uid=@uniform AND entityid=@uid AND id=@type","uniform",(long)uniform,"uid",(long)p.Entity.UID,"type",(int)type);
                    if(Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=327,y=303 WHERE uid=@uid","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Team Tournament return location not saved.");
                    t.Commit();
                }}
                bool changed=false;var worn=p.Equipment.Objects[8];
                if(worn!=null && worn.UID==uniform && worn.ID==type){p.Equipment.UnequipCommittedCosmetic(ConquerItem.Garment,worn);changed=true;}
                ConquerItem bag;
                if(p.Inventory.TryGetItem(uniform,out bag) && bag.ID==type && !p.Inventory.Remove(uniform,Enums.ItemUse.None,false))throw new InvalidOperationException("Team Tournament uniform projection requires reload.");
                p.Send(new ItemUsage(true){UID=uniform,Action=ItemUsage.Mode.RemoveInventory});
                changed=RestoreGarment(p,original) || changed;
                if(changed){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                p.Entity.PKMode=(Enums.PKMode)mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)p.Entity.PKMode});
                if(!Q.Player(p,false))throw new InvalidOperationException("Team Tournament returning session changed.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND mapid=1002 AND x=327 AND y=303 FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team Tournament saved destination changed.");
                    if(Q.Execute(c,t,"UPDATE revora_quarantine_admissions SET returned=true WHERE round_id=@round AND uid=@uid AND ended AND NOT returned","round",round,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Team Tournament return acknowledgement changed.");
                    t.Commit();
                }}
                return true;
            }catch(Exception error){Q.Fail(p,"team-tournament-return",error);return false;}
            finally {if(pk)System.Threading.Monitor.Exit(p.PkSyncRoot);System.Threading.Monitor.Exit(p.ItemSyncRoot);}
        }
        internal static void Tick() {
            if(!System.Threading.Monitor.TryEnter(returnGate))return;
            try {
                var now=DateTime.UtcNow;if(now<nextReturn)return;nextReturn=now.AddSeconds(10);
                var ids=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>(long)p.Entity.UID).ToArray();if(ids.Length==0)return;
                var due=new List<uint>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_quarantine_admissions WHERE uid=ANY(@ids) AND uid>@cursor AND NOT returned ORDER BY uid LIMIT 32","ids",ids,"cursor",returnCursor))
                using(var r=q.ExecuteReader())while(r.Read())due.Add(Convert.ToUInt32(r[0]));}
                if(due.Count==0){returnCursor=0;return;}returnCursor=due.Last();
                foreach(uint uid in due){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))Return(p);}
            }catch(Exception error){Console.WriteLine("[team-tournament-return] poll_failure="+error.GetType().Name);}
            finally {System.Threading.Monitor.Exit(returnGate);}
        }
    }

}
