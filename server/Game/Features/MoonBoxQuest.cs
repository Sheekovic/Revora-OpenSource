using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MoonBoxQuest
    {
        internal const uint Fortune=600050,Maggie=600003,Jade=721072,Box=721090;
        private static readonly ConcurrentDictionary<uint,byte> active=new ConcurrentDictionary<uint,byte>();
        private static readonly ConcurrentDictionary<uint,GameState> deaths=new ConcurrentDictionary<uint,GameState>();
        [ThreadStatic] private static HashSet<uint> transfers;
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static bool enabled;
        private sealed class Run {
            internal Guid Id;internal string Phase;internal ushort Area;internal long[] Tokens;internal int[] Kills;
            internal uint JadeUid,Ghost;internal bool Valuable;
        }
        internal static bool Material(uint id) {return id>=721010 && id<=721015 || id==Jade;}
        internal static bool InMaze(Entity e) {return enabled && e!=null && e.MapID>=1042 && e.MapID<=1050 && e.Owner!=null && active.ContainsKey(e.UID);}
        private static bool GhostNpc(uint id) {return id>=600005 && id<=600017 && id%2==1;}
        private static bool LifeNpc(uint id) {return id>=600018 && id<=600042;}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT run,phase,area,tokens,kills,jade_uid,ghost,valuable_reward FROM revora_moon_runs WHERE uid=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Id=r.GetGuid(0),Phase=r.GetString(1),Area=Convert.ToUInt16(r[2]),Tokens=r.GetFieldValue<long[]>(3),
                Kills=r.GetFieldValue<int[]>(4),JadeUid=Convert.ToUInt32(r[5]),Ghost=Convert.ToUInt32(r[6]),Valuable=r.GetBoolean(7)}:null;
        }
        private static bool Open(Run r) {return r!=null && r.Phase!="complete" && r.Phase!="abandoned";}
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Run r) {
            if(Q.Execute(c,t,@"UPDATE revora_moon_runs SET phase=@phase,area=@area,tokens=@tokens,kills=@kills,jade_uid=@jade,ghost=@ghost WHERE uid=@uid AND run=@run",
                "phase",r.Phase,"area",(int)r.Area,"tokens",r.Tokens,"kills",r.Kills,"jade",(long)r.JadeUid,"ghost",(long)r.Ghost,"uid",(long)uid,"run",r.Id)!=1)throw new InvalidOperationException("Moon progress was not saved.");
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_moon_runs(
uid bigint PRIMARY KEY CHECK(uid>0),run uuid NOT NULL UNIQUE,phase varchar(16) NOT NULL CHECK(phase IN ('portal','tactic','life','complete','abandoned')),
area integer NOT NULL CHECK(area=1002 OR area BETWEEN 1042 AND 1050),tokens bigint[] NOT NULL,kills integer[] NOT NULL,
jade_uid bigint NOT NULL DEFAULT 0,ghost bigint NOT NULL DEFAULT 0,CHECK(array_length(tokens,1)=6 AND array_length(kills,1)=6));
ALTER TABLE revora_moon_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_moon_results(run uuid PRIMARY KEY,uid bigint NOT NULL,choice varchar(16) NOT NULL,
rewards bigint[] NOT NULL,completed_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                active.Clear();using(var q=Q.Command(c,t,"SELECT uid FROM revora_moon_runs WHERE phase NOT IN ('complete','abandoned')"))
                using(var r=q.ExecuteReader())while(r.Read())active[Convert.ToUInt32(r[0])]=0;
                t.Commit();enabled=true;
            }}
            for(ushort map=1043;map<=1050;map++)if(!Constants.PKFreeMaps.Contains(map))Constants.PKFreeMaps.Add(map);
            if(!Constants.PKForbiddenMaps.Contains(1042))Constants.PKForbiddenMaps.Add(1042);
        }
        internal static void PlaceNpcs() {
            var rows=new List<NpcSpawn>();
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var q=Q.Command(c,null,@"SELECT id,lookface,mapid,cellx,celly FROM npcs
WHERE id=@fortune OR id=@maggie OR (id BETWEEN 600005 AND 600017 AND id%2=1) OR id BETWEEN 600018 AND 600042","fortune",(long)Fortune,"maggie",(long)Maggie))
            using(var r=q.ExecuteReader())while(r.Read())rows.Add(new NpcSpawn{UID=Convert.ToUInt32(r[0]),Mesh=Convert.ToUInt16(r[1]),MapID=Convert.ToUInt16(r[2]),X=Convert.ToUInt16(r[3]),Y=Convert.ToUInt16(r[4]),Type=Enums.NpcType.Talker});}
            if(rows.Count!=34)throw new InvalidOperationException("Installed Moon Box NPC set is incomplete.");
            foreach(var npc in rows) {
                if(npc.UID==Fortune) {npc.MapID=1002;npc.X=489;npc.Y=622;npc.Name="FortuneTeller";}
                else npc.Name=npc.UID==Maggie?"Maggie":LifeNpc(npc.UID)?"LonelyGhost":"Ghost";
                Map map;if(!Kernel.Maps.TryGetValue(npc.MapID,out map))throw new InvalidOperationException("Moon Box map is missing.");
                foreach(var m in Kernel.Maps.Values.ToArray()){INpc old;if(m.Npcs!=null && m.Npcs.TryGetValue(npc.UID,out old))m.RemoveNpc(old);}
                ushort x=npc.X,y=npc.Y;
                if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Moon Box NPC landing unavailable.");
                npc.X=x;npc.Y=y;map.AddNpc(npc);
            }
        }
        internal static bool AllowTeleport(GameState p,ushort destination) {
            if(!enabled || p==null || p.Entity==null || p.Entity.MapID!=1049 || destination==1049 || p.Entity.Dead || !active.ContainsKey(p.Entity.UID))return true;
            if(transfers!=null && transfers.Contains(p.Entity.UID))return true;
            p.Send("The Death Tactic holds you. Speak to its Ghost if you wish to surrender your tokens.");return false;
        }
        private static void Travel(GameState p,ushort map,bool heal=false) {
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try {
                ushort x=map==1002?(ushort)489:map==1042?(ushort)28:(ushort)32;
                ushort y=map==1002?(ushort)622:map==1042?(ushort)33:(ushort)32;
                if(!PotencyGuide.FindLanding(Kernel.Maps[map],ref x,ref y))throw new InvalidOperationException("Moon Box travel landing unavailable.");
                p.Entity.Teleport(map,x,y);if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}
            } finally {transfers.Remove(p.Entity.UID);}
        }
        private static string Begin(GameState p) {
            if(p.Entity.Level<70)return "Reach level 70 before entering the Eight-Diagram Tactics.";
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest before entering.";
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Missing Moon Box character.");
                var r=Read(c,t,p.Entity.UID);
                if(!Open(r)) {
                    r=new Run{Valuable=true,Id=Guid.NewGuid(),Phase="portal",Area=1042,Tokens=new long[6],Kills=new int[6]};
                    Q.Execute(c,t,@"INSERT INTO revora_moon_runs(uid,run,phase,area,tokens,kills,valuable_reward) VALUES(@uid,@run,'portal',1042,@tokens,@kills,true)
ON CONFLICT(uid) DO UPDATE SET run=EXCLUDED.run,phase='portal',area=1042,tokens=EXCLUDED.tokens,kills=EXCLUDED.kills,jade_uid=0,ghost=0,valuable_reward=true",
                        "uid",(long)p.Entity.UID,"run",r.Id,"tokens",r.Tokens,"kills",r.Kills);
                }
                t.Commit();active[p.Entity.UID]=0;Travel(p,r.Area);return "Maggie and the wandering souls hold the answers. Keep every different token you find.";
            }}
        }
        private static string Enter(GameState p) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Open(r) || r.Area!=1042)return "The Fortune Teller must first open your path.";
                if(r.JadeUid!=0) {r.Area=1050;r.Phase="life";}
                else {r.Area=(ushort)(r.Tokens.All(id=>id!=0)?1049:1043+Roll(7));r.Phase="tactic";}
                Save(c,t,p.Entity.UID,r);t.Commit();Travel(p,r.Area);return "Every tactic has its own token. The Ghost can guide you back, unless Death claims your path.";
            }}
        }
        private static string Portal(GameState p) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Open(r) || r.Area!=p.Entity.MapID || r.Area==1049)return "This path is closed.";
                r.Area=1042;r.Phase="portal";Save(c,t,p.Entity.UID,r);t.Commit();Travel(p,1042);return "Maggie awaits you beyond the portal.";
            }}
        }
        private static string Exchange(GameState p) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Open(r) || r.Area!=1049 || r.Phase!="tactic" || r.Tokens.Any(id=>id==0))return "Bring me all six different tactic tokens.";
                for(int i=0;i<6;i++)if(!Q.Owns(c,t,p,(uint)r.Tokens[i],721010u+(uint)i))return "Your six earned tokens must be in your bag.";
                var removed=r.Tokens.Select(id=>(uint)id).ToArray();
                for(int i=0;i<6;i++)Q.Consume(c,t,p,(uint)r.Tokens[i],721010u+(uint)i);
                var jade=Q.New(Jade);Q.Insert(c,t,p.Entity.UID,jade);r.Tokens=new long[6];r.JadeUid=jade.UID;
                r.Ghost=(uint)(600018+Roll(25));r.Area=1050;r.Phase="life";Save(c,t,p.Entity.UID,r);t.Commit();
                Q.Install(p,removed,new[]{jade});Travel(p,1050);return "The Soul Jade will answer one lonely spirit. Seek it among the wandering ghosts.";
            }}
        }
        private static string Finish(GameState p,int choice) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);
                if(!Open(r) || r.Area!=1050 || r.Phase!="life" || r.Ghost!=p.ActiveNpc || r.JadeUid==0)return "Another spirit awaits your help. My path is not yours.";
                if(!Q.Owns(c,t,p,r.JadeUid,Jade))return "Bring the Soul Jade that the Ghost made for you.";
                int count=choice==1?1:choice==2?3:0;
                bool valuable=r.Valuable && count>0;
                if(p.Inventory.Count-1+count+(valuable?1:0)>40)return "Make room in your bag, and I will wait for you.";
                uint jade=r.JadeUid;Q.Consume(c,t,p,jade,Jade);var rewards=new List<ConquerItem>();
                for(int i=0;i<count;i++){var item=Q.New(choice==1?Box:1088001,false);Q.Insert(c,t,p.Entity.UID,item);rewards.Add(item);}
                if(valuable){var soul=QuestSoulReward.Create();Q.Insert(c,t,p.Entity.UID,soul);rewards.Add(soul);}
                if(Q.Execute(c,t,"INSERT INTO revora_moon_results(run,uid,choice,rewards) VALUES(@run,@uid,@choice,@rewards)",
                    "run",r.Id,"uid",(long)p.Entity.UID,"choice",choice==1?"moon-box":choice==2?"meteors":"refused","rewards",rewards.Select(i=>(long)i.UID).ToArray())!=1)throw new InvalidOperationException("Moon reward was not saved.");
                if(choice==1)ProgressionEvidence.Complete(c,t,p.Entity.UID,11022,"moon-box-v1",r.Id.ToString("N"),1);
                r.JadeUid=0;r.Area=1002;r.Phase=choice==3?"abandoned":"complete";Save(c,t,p.Entity.UID,r);t.Commit();
                byte ignored;active.TryRemove(p.Entity.UID,out ignored);Q.Install(p,new[]{jade},rewards);if(choice==1)Q.Refresh(p);Travel(p,1002);
                return choice==3?"Then I release you from your promise. Farewell.":"My spirit is free. Take this gift, and may your road be bright.";
            }}
        }
        private static string Surrender(GameState p) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Open(r) || r.Area!=1049)return "Death no longer holds your path.";
                var removed=new List<uint>();
                for(int i=0;i<6;i++)if(r.Tokens[i]!=0){Q.Consume(c,t,p,(uint)r.Tokens[i],721010u+(uint)i);removed.Add((uint)r.Tokens[i]);}
                r.Tokens=new long[6];r.Area=1002;r.Phase="abandoned";Save(c,t,p.Entity.UID,r);t.Commit();
                byte ignored;active.TryRemove(p.Entity.UID,out ignored);Q.Install(p,removed,new ConquerItem[0]);Travel(p,1002);return "Your tokens remain with Death. You may begin anew with the Fortune Teller.";
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null)return false;
            uint npc=p.ActiveNpc;if(npc!=Fortune && npc!=Maggie && !GhostNpc(npc) && !LifeNpc(npc))return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                ushort map=npc==Fortune?(ushort)1002:npc==Maggie?(ushort)1042:LifeNpc(npc)?(ushort)1050:(ushort)(1043+(npc-600005)/2);
                if(!Q.Near(p,npc,map))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(npc==Fortune && option==1)d.Text(Begin(p));
                    else if(npc==Maggie && option==1)d.Text(Enter(p));
                    else if(GhostNpc(npc) && map!=1049 && option==1)d.Text(Portal(p));
                    else if(map==1049 && option==1)d.Text(Exchange(p));
                    else if(map==1049 && option==3){d.Text("Leave every earned token behind, and I will release you without a reward.");d.Option("Surrender my tokens.",4);}
                    else if(map==1049 && option==4)d.Text(Surrender(p));
                    else if(LifeNpc(npc) && option>=1 && option<=3)d.Text(Finish(p,option));
                    else if(npc==Fortune){d.Text("Beyond the plains lie the Eight-Diagram Tactics. Six tokens may lead you to a lost soul's treasure.");d.Option("Enter or resume the trial.",1);}
                    else if(npc==Maggie){d.Text("Each path is uncertain. Bring back the six different tokens, and Death will open the way to Life.");d.Option("Open a tactic.",1);}
                    else if(map==1049){d.Text("Six different tokens become one Soul Jade. Without them, you must face defeat or leave your tokens behind.");d.Option("Make the Soul Jade.",1);d.Option("I wish to surrender.",3);}
                    else if(GhostNpc(npc)){d.Text("Keep this tactic's token. Maggie can guide you to the next path.");d.Option("Return to Maggie.",1);}
                    else {using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p.Entity.UID);d.Text("Does your Soul Jade answer my sorrow? Release me, and choose a gift."+(r!=null && r.Valuable?" Either gift includes a bound P7 soul.":""));}d.Option("A Moon Box.",1);d.Option("Three Meteors.",2);d.Option("I refuse your request.",3);}
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"moon-box",ex);}
            }
            return true;
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    var r=Read(c,t,p.Entity.UID);t.Commit();byte ignored;
                    if(Open(r)){active[p.Entity.UID]=0;if(p.Entity.MapID>=1042 && p.Entity.MapID<=1050)Travel(p,r.Area,p.Entity.Dead);}
                    else {active.TryRemove(p.Entity.UID,out ignored);if(p.Entity.MapID>=1042 && p.Entity.MapID<=1050)Travel(p,1002,true);}
                }}
            }catch(Exception ex){Q.Fail(p,"moon-box-login",ex);}
        }
        internal static void OnDeath(GameState p) {
            if(!enabled || !Q.Player(p,false) || !InMaze(p.Entity))return;
            deaths[p.Entity.UID]=p;
        }
        internal static void Tick() {
            foreach(var entry in deaths.ToArray()) {
                GameState p;if(!deaths.TryRemove(entry.Key,out p) || !Q.Player(p,false))continue;
                ReturnAfterDeath(p);
            }
        }
        private static void ReturnAfterDeath(GameState p) {
            lock(p.ItemSyncRoot)try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    var r=Read(c,t,p.Entity.UID);if(!Open(r) || r.Area!=p.Entity.MapID)return;
                    r.Area=1042;r.Phase="portal";Save(c,t,p.Entity.UID,r);t.Commit();Travel(p,1042,true);
                }}
            }catch(Exception ex){Q.Fail(p,"moon-box-death",ex);}
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!enabled || mob==null || mob.EntityFlag!=EntityFlag.Monster || mob.Companion || mob.MonsterInfo==null
                || mob.MapID<1043 || mob.MapID>1049 || mob.MonsterInfo.ID!=(uint)(6000+mob.MapID-1043))return false;
            Map map;Entity owned;if(!Kernel.Maps.TryGetValue(mob.MapID,out map) || !map.Entities.TryGetValue(mob.UID,out owned) || !ReferenceEquals(owned,mob))return false;
            ushort area,x,y;bool died;
            lock(mob){if(mob.Dead)return true;area=mob.MapID;x=mob.X;y=mob.Y;mob.DieCore(killer);died=mob.Dead;}
            if(died && area<1049)Credit(killer,area,x,y);return true;
        }
        private static void Credit(Entity killer,ushort area,ushort x,ushort y) {
            if(killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return;
            var p=killer.Owner;if(!Q.Player(p) || p.Entity.MapID!=area || !active.ContainsKey(p.Entity.UID)
                || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance)return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    var r=Read(c,t,p.Entity.UID);int index=area-1043;
                    if(!Open(r) || r.Phase!="tactic" || r.Area!=area || r.Tokens[index]!=0)return;
                    r.Kills[index]=Math.Min(20,r.Kills[index]+1);ConquerItem token=null;
                    if(p.Inventory.Count<40 && (r.Kills[index]==20 || Roll(100)<20)) {
                        token=Q.New(721010u+(uint)index);Q.Insert(c,t,p.Entity.UID,token);r.Tokens[index]=token.UID;
                    }
                    Save(c,t,p.Entity.UID,r);t.Commit();
                    if(token!=null){Q.Install(p,new uint[0],new[]{token});p.Send("You found this tactic's token. Seek the Ghost when you are ready to return.");}
                    else if(r.Kills[index]==20 && p.Inventory.Count>=40)p.Send("Make room for this tactic's token before your next kill.");
                }}
            }catch(Exception ex){Q.Fail(p,"moon-box-token",ex);}
        }
    }
}
