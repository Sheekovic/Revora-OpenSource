using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static partial class ExpeditionEvent
    {
        internal static Func<DateTime> Clock = () => DateTime.Now;
        internal static string StatusPath = "database/extraction-status.json";
        internal const int SatchelLimit=12;
        private const uint ExitFirst=91110, CacheFirst=91120;
        private static readonly int[] ExitMinutes={5,8,12};
        private static readonly string[] ExitNames={"West Ferryman","East Ferryman","South Ferryman"};
        private static readonly List<Point> FieldCells=new List<Point>();
        private static readonly List<Point> Entries=new List<Point>();
        private static readonly Point[] Exits=new Point[3], Camps=new Point[3];
        private static Point Lair;
        private static bool LateBossSpawned;
        private static DateTime NextFieldTick;
        private static readonly Dictionary<uint, Encounter> Encounters=new Dictionary<uint, Encounter>();
        private static readonly bool[] CacheClaimed=new bool[3];
        private sealed class Encounter
        {
            internal Entity Body;
            internal int Camp; // -1 scout, 0..2 cache guardian, 3 late boss
            internal bool Dead, LootRolled;
            internal DateTime Respawn;
        }
        internal static List<Point> ReachableField(Floor floor)
        {
            var result=new List<Point>();var seen=new HashSet<Point>();var queue=new Queue<Point>();
            Point start=new Point(500,500);
            if(!floor[start.X,start.Y,MapObjectType.Player]) {
                bool found=false;
                for(int radius=1;radius<=30 && !found;radius++)
                    for(int x=500-radius;x<=500+radius && !found;x++)for(int y=500-radius;y<=500+radius;y++)
                        if(floor[x,y,MapObjectType.Player]){start=new Point(x,y);found=true;break;}
                if(!found)throw new InvalidOperationException("Expedition center is not walkable");
            }
            queue.Enqueue(start);seen.Add(start);
            while(queue.Count>0) {
                Point p=queue.Dequeue();if(p.X%4==0 && p.Y%4==0)result.Add(p);
                foreach(var n in new[]{new Point(p.X-1,p.Y),new Point(p.X+1,p.Y),new Point(p.X,p.Y-1),new Point(p.X,p.Y+1)})
                    if(n.X>=270 && n.X<=730 && n.Y>=270 && n.Y<=730 && floor[n.X,n.Y,MapObjectType.Player] && seen.Add(n))queue.Enqueue(n);
            }
            return result;
        }
        private static int Distance(Point a,Point b) { return Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)); }
        private static Point Nearest(Point wanted) { return FieldCells.OrderBy(p=>Distance(p,wanted)).First(); }
        internal static void InitializeField()
        {
            FieldCells.Clear();FieldCells.AddRange(ReachableField(EventMap.Floor));
            if(FieldCells.Count<2000)throw new InvalidOperationException("Expedition field is too small or disconnected");
            Point[] exits={new Point(300,300),new Point(700,300),new Point(500,700)};
            Point[] camps={new Point(380,430),new Point(650,490),new Point(450,610)};
            for(int i=0;i<3;i++) {
                Exits[i]=Nearest(exits[i]);Camps[i]=Nearest(camps[i]);
                CityGuardSystem.SpawnTalker(ExitFirst+(uint)i,EventMap.ID,(ushort)Exits[i].X,(ushort)Exits[i].Y,Mesh,ExitNames[i]);
                CityGuardSystem.SpawnTalker(CacheFirst+(uint)i,EventMap.ID,(ushort)Camps[i].X,(ushort)Camps[i].Y,RelicRushSystem.RelicMesh,"Relic Cache");
            }
            Lair=Nearest(new Point(530,530));Entries.Clear();
            var objectives=Exits.Concat(Camps).Concat(new[]{Lair}).ToArray();
            for(int i=0;i<Capacity;i++) {
                var point=FieldCells.Where(p=>objectives.All(o=>Distance(p,o)>=15))
                    .OrderByDescending(p=>Entries.Count==0?Distance(p,Lair):Entries.Min(e=>Distance(p,e))).First();
                if(Entries.Any(e=>Distance(point,e)<35))throw new InvalidOperationException("Expedition entries overlap");
                Entries.Add(point);
            }
            EventMap.Portals.Clear();
            var mapInfo=MapsTable.MapInformations[1015];mapInfo.ID=EventMap.ID;mapInfo.BaseID=1015;
            MapsTable.MapInformations[EventMap.ID]=mapInfo;
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var q=new NpgsqlCommand("CREATE TABLE IF NOT EXISTS revora_expedition_claims(cycle text NOT NULL,uid bigint NOT NULL,items integer NOT NULL,rvp bigint NOT NULL,PRIMARY KEY(cycle,uid)); ALTER TABLE revora_expedition_claims ADD COLUMN IF NOT EXISTS soul_type bigint NOT NULL DEFAULT 0; ALTER TABLE revora_expedition_claims ADD COLUMN IF NOT EXISTS soul_uid bigint NOT NULL DEFAULT 0",c))q.ExecuteNonQuery();}
            WriteFieldStatus();
        }
        private static void ClearEncounters()
        {
            foreach(var e in Encounters.Values) EventMap.RemoveEntity(e.Body);
            Encounters.Clear();Array.Clear(CacheClaimed,0,3);LateBossSpawned=false;
        }
        private static Point FreeEncounterCell(Point preferred,int radius)
        {
            foreach(var p in FieldCells.OrderBy(c=>Distance(c,preferred)))
                if(Distance(p,preferred)<=radius && EventMap.Floor[p.X,p.Y,MapObjectType.Monster]
                    && Exits.Concat(Camps).All(n=>Distance(p,n)>=4)
                    && Kernel.GamePool.Values.All(c=>c==null || c.Entity==null || c.Entity.MapID!=EventMap.ID || Distance(p,new Point(c.Entity.X,c.Entity.Y))>=5))return p;
            return Point.Empty;
        }
        private static void AddEncounter(Point preferred,int camp)
        {
            Point p=FreeEncounterCell(preferred,30);if(p==Point.Empty)return;
            uint template=camp==-1?1413u:camp==3?2719u:39u;
            var info=MonsterInformation.MonsterInformations[template].Copy();
            info.ID=(uint)(9913000+(camp==-1?0:camp==3?2:1));info.Guard=info.Reviver=false;
            info.Boss=camp!=-1;info.RespawnTime=int.MaxValue/2;info.SpellID=0;
            info.BoundX=(ushort)p.X;info.BoundY=(ushort)p.Y;info.BoundCX=info.BoundCY=1;
            info.AttackSpeed=2000;info.ViewRange=12;info.AttackRange=3;
            var e=new Entity(EntityFlag.Monster,false){UID=EventMap.EntityUIDCounter.Next,MapID=EventMap.ID,MapObjType=MapObjectType.Monster,
                MonsterInfo=info,Name=info.Name,Body=info.Mesh,Level=info.Level,X=(ushort)p.X,Y=(ushort)p.Y,
                Defence=(ushort)(camp==-1?500:3000),MagicDefence=30,MinAttack=2000,MaxAttack=5000,MagicAttack=5000};
            info.Owner=e;e.MaxHitpoints=info.Hitpoints;e.Hitpoints=info.Hitpoints;e.ApplyMonsterBossHud();e.SendUpdates=true;
            EventMap.AddEntity2(e);Encounters.Add(e.UID,new Encounter{Body=e,Camp=camp});
            foreach(var c in Kernel.GamePool.Values)if(c!=null && c.Entity!=null && InEvent(c.Entity) && Kernel.GetDistance(e.X,e.Y,c.Entity.X,c.Entity.Y)<Constants.nScreenDistance)e.SendSpawn(c,false);
        }
        private static void PrepareField()
        {
            ClearEncounters();NextFieldTick=DateTime.MinValue;
            for(int i=0;i<72;i++)AddEncounter(FieldCells[Kernel.Random.Next(FieldCells.Count)],-1);
            for(int i=0;i<3;i++)AddEncounter(Camps[i],i);
            if(Encounters.Values.Count(e=>e.Camp>=0)!=3)throw new InvalidOperationException("Expedition guardians could not spawn");
        }
        internal static bool IsEncounter(Entity e)
        {return e!=null && e.EntityFlag==EntityFlag.Monster && InEvent(e) && e.MonsterInfo!=null && e.MonsterInfo.ID>=9913000 && e.MonsterInfo.ID<=9913002;}
        internal static bool Die(Entity e,Entity killer)
        {
            if(!IsEncounter(e))return false;
            lock(Gate) {
                Encounter encounter;if(!Encounters.TryGetValue(e.UID,out encounter) || encounter.Dead)return true;
                if(!ReferenceEquals(encounter.Body,e))return true;
                if(e.MonsterInfo.Level>=120 && killer!=null) {
                    Member member;var player=killer.Owner;Entity owned;
                    if(!Running || Clock()>=CycleStart.Add(Duration) || !Ready(player) || !QuestInventory.Player(player)
                        || !ReferenceEquals(player.Entity.Owner,player) || !InEvent(player.Entity)
                        || !Members.TryGetValue(player.Entity.UID,out member) || member.State!="entered" && member.State!="extracting"
                        || Kernel.GetDistance(player.Entity.X,player.Entity.Y,e.X,e.Y)>Constants.pScreenDistance
                        || !EventMap.Entities.TryGetValue(e.UID,out owned) || !ReferenceEquals(owned,e))return true;
                    if(ReferenceEquals(killer,player.Entity))WildMonsterVictory.Die(e,killer);
                    else if(killer.Companion && ReferenceEquals(player.Companion,killer) && killer.MapID==EventMap.ID
                        && EventMap.Companions.TryGetValue(killer.UID,out owned) && ReferenceEquals(owned,killer))e.DieCore(killer);
                    else return true;
                    // Failed/unresolved accepted-life writes leave both the
                    // encounter and its temporary rewards untouched for retry.
                    if(!e.Dead)return true;
                    encounter.Dead=true;encounter.Respawn=Clock().AddSeconds(45);
                    TryCollectKill(player,e.MonsterInfo);return true;
                }
                encounter.Dead=true;encounter.Respawn=Clock().AddSeconds(45);
                if(killer!=null && killer.Owner!=null)TryCollectKill(killer.Owner,e.MonsterInfo);
                if(killer!=null)e.DieCore(killer);else {e.Hitpoints=0;EventMap.RemoveEntity(e);}
                return true;
            }
        }
        internal static bool TryHit(Entity attacker,Entity target,out uint damage)
        {
            damage=0;if(!IsEncounter(attacker) || target==null || target.EntityFlag!=EntityFlag.Player || !InEvent(target))return false;
            lock(Gate) {Encounter e;if(!Running || !Encounters.TryGetValue(attacker.UID,out e) || e.Dead)return false;
                damage=(uint)Kernel.Random.Next(e.Camp==-1?1200:e.Camp==3?4000:2500,e.Camp==-1?2001:e.Camp==3?5501:3501);return true;}
        }
        private static bool NearOpenExit(GameState c)
        {for(int i=0;i<3;i++)if(Clock()>=CycleStart.AddMinutes(ExitMinutes[i]) && Distance(new Point(c.Entity.X,c.Entity.Y),Exits[i])<=5)return true;return false;}
        private static bool Ready(GameState c)
        {return c!=null && !c.Fake && c.Action==2 && c.Entity!=null && !c.Entity.Dead && !c.Entity.PersistenceRequiresReload && c.ItemSyncRoot!=null;}
        private static bool HandleFieldNpc(GameState c,NpcRequest request)
        {
            bool exit=c.ActiveNpc>=ExitFirst && c.ActiveNpc<ExitFirst+3;
            bool cache=c.ActiveNpc>=CacheFirst && c.ActiveNpc<CacheFirst+3;
            if(!exit && !cache)return false;
            if(!Ready(c) || !InEvent(c.Entity))return true;
            int index=(int)(c.ActiveNpc-(exit?ExitFirst:CacheFirst));Point p=exit?Exits[index]:Camps[index];
            if(Distance(new Point(c.Entity.X,c.Entity.Y),p)>5)return true;
            var dialog=new Npcs(c);
            lock(Gate) {
                if(exit) {
                    if(request.OptionID==2 && BeginExtract(c))return true;
                    bool open=Running && Clock()>=CycleStart.AddMinutes(ExitMinutes[index]);
                    dialog.Text(open?"The boat is ready. Hold here for ten seconds, and I'll take you home.":"The crossing is watched. Return at "+CycleStart.AddMinutes(ExitMinutes[index]).ToString("HH:mm")+".");
                    if(open)dialog.Option("Take me home.",2);
                } else {
                    if(request.OptionID==3)dialog.Text(ClaimCache(c,index));
                    else {dialog.Text("A guardian watches this cache. Bring it down, then claim what it guards.");dialog.Option("Open the cache.",3);}
                }
            }
            dialog.Option("Leave.",255);dialog.Send();return true;
        }
        internal static string ClaimCache(GameState c,int index)
        {
            lock(Gate) {
                Member m;
                if(index<0 || index>=3 || !Ready(c) || !Running || !InEvent(c.Entity) || !Members.TryGetValue(c.Entity.UID,out m) || m.State!="entered"
                    || Distance(new Point(c.Entity.X,c.Entity.Y),Camps[index])>5)return "There is nothing for you here.";
                if(CacheClaimed[index])return "Someone has already emptied the cache.";
                if(!Encounters.Values.Any(e=>e.Camp==index) || Encounters.Values.Any(e=>e.Camp==index && !e.Dead))return "The guardian still stands.";
                if(m.Loot.Count>SatchelLimit-3)return "Your satchel is too full. Reach a boat first.";
                CacheClaimed[index]=true;AddTemporaryLoot(c,m,3);m.BonusPoints+=250;
                FieldMessage(c.Entity.Name+" has claimed a relic cache. "+CacheClaimed.Count(claimed=>!claimed)+" caches remain.");
                return "Three relics and 250 RVPs await you, if you reach a boat alive.";
            }
        }
        private static void AddTemporaryLoot(GameState c,Member m,int count)
        {
            uint[] pool=Revora.Config.ExpeditionLootItems.Where(id=>ConquerItemInformation.BaseInformations.ContainsKey(id)).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("Expedition loot catalog missing");
            for(int i=0;i<count && m.Loot.Count<SatchelLimit;i++)m.Loot.Add(pool[Kernel.Random.Next(pool.Length)]);
            c.Send(new Message("Satchel: "+m.Loot.Count+"/"+SatchelLimit+". Reach an open boat to keep your loot.",Color.LightGreen,Message.TopLeft));
        }
        private static void FieldMessage(string text)
        {foreach(var c in Kernel.GamePool.Values)if(c!=null && c.Entity!=null && InEvent(c.Entity))c.Send(new Message(text,Color.Gold,Message.Center));}
        private static void TickField(DateTime now)
        {
            if(now<NextFieldTick)return;NextFieldTick=now.AddSeconds(5);
            foreach(var e in Encounters.Values.ToArray())if(e.Dead && e.Camp==-1 && now>=e.Respawn) {
                EventMap.RemoveEntity(e.Body);Encounters.Remove(e.Body.UID);AddEncounter(FieldCells[Kernel.Random.Next(FieldCells.Count)],-1);
            }
            if(!LateBossSpawned && now>=CycleStart.AddMinutes(10)) {
                AddEncounter(Lair,3);LateBossSpawned=Encounters.Values.Any(e=>e.Camp==3);
                if(LateBossSpawned)FieldMessage("HorrifyingSpecter haunts the old trail ("+Lair.X+","+Lair.Y+"). Its slayer carries three relics and 500 RVPs to the boats.");
            }
            foreach(var m in Members.Values) {
                GameState c;if(!Kernel.GamePool.TryGetValue(m.Uid,out c) || !Ready(c) || ((m.State=="entered" || m.State=="extracting") && !InEvent(c.Entity))) {
                    if(m.State=="entered" || m.State=="extracting"){m.State="dead";m.Loot.Clear();m.BonusPoints=0;}continue;
                }
                if(m.State!="entered" && m.State!="extracting") {
                    if(InEvent(c.Entity))c.Entity.Teleport(1002,408,330);
                    continue;
                }
                c.Send(new Message("Extraction  "+Math.Max(0,(int)(CycleStart.Add(Duration)-now).TotalMinutes)+"m  Satchel "+m.Loot.Count+"/12",Color.Yellow,Message.FirstRightCorner));
                for(int i=0;i<3;i++)c.Send(new Message(ExitNames[i]+" "+Exits[i].X+","+Exits[i].Y+" "+(now>=CycleStart.AddMinutes(ExitMinutes[i])?"OPEN":"at "+CycleStart.AddMinutes(ExitMinutes[i]).ToString("HH:mm")),Color.White,Message.ContinueRightCorner));
                c.Send(new Message("Camps: "+string.Join(" | ",Enumerable.Range(0,3).Select(i=>Camps[i].X+","+Camps[i].Y+(CacheClaimed[i]?" looted":""))),Color.White,Message.ContinueRightCorner));
            }
            WriteFieldStatus();
        }
        private static void WriteFieldStatus()
        {
            string path=StatusPath;
            string json="{\"utc\":\""+Clock().ToUniversalTime().ToString("o")+"\",\"map\":"+EventMap.ID+",\"baseMap\":1015,\"width\":"+EventMap.Floor.Bounds.Width+",\"height\":"+EventMap.Floor.Bounds.Height+",\"reachableSpawnCells\":"+FieldCells.Count+",\"entries\":"+Entries.Count+",\"exits\":3,\"camps\":3,\"running\":"+(Running?"true":"false")+",\"encounters\":"+Encounters.Count+"}";
            System.IO.File.WriteAllText(path+".pending",json);
            if(System.IO.File.Exists(path))System.IO.File.Replace(path+".pending",path,null);else System.IO.File.Move(path+".pending",path);
        }
        private static bool CommitExtraction(GameState c,Member m)
        {
            if (!Ready(c) || !QuestInventory.Player(c,false) || !SessionInstall.TryEnter(c.Entity.UID)) return false;
            try
            {
                if (!System.Threading.Monitor.TryEnter(c.ItemSyncRoot)) return false;
                try { if (!Ready(c) || !QuestInventory.Player(c,false)) return false; return CommitExtractionOwned(c,m); }
                finally { System.Threading.Monitor.Exit(c.ItemSyncRoot); }
            }
            finally { SessionInstall.Exit(c.Entity.UID); }
        }
        private static bool CommitExtractionOwned(GameState c,Member m)
        {
            if(c.Inventory.Count+m.Loot.Count+(m.SoulType==0?0:1)>40){m.State="entered";c.Send("Make room in your bag before boarding. Your satchel is safe for now.");return false;}
            uint reward=checked(Revora.Config.ExpeditionExtractRevoraPoints+(uint)m.Loot.Count*Revora.Config.ExpeditionRevoraPointsPerItem+m.BonusPoints);
            if(c.Entity.RevoraPoint>uint.MaxValue-reward){m.State="entered";return false;}
            var relicCount=m.Loot.Count;
            try {
                ConquerItemBaseInformation soulInfo;
                if(m.SoulType!=0 && (m.SoulType<800000 || m.SoulType>=900000
                    || !ConquerItemInformation.BaseInformations.TryGetValue(m.SoulType,out soulInfo) || soulInfo.PurificationLevel!=7))
                    throw new InvalidOperationException("Extraction soul catalog unavailable");
                var ids=m.SoulType==0?m.Loot.ToArray():m.Loot.Concat(new[]{m.SoulType}).ToArray();
                var items=ids.Select(id=>{var info=ConquerItemInformation.BaseInformations[id];return new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=id,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability,Color=Enums.Color.Orange,Bound=id>=800000 && id<900000 && info.PurificationLevel==7};}).ToArray();
                uint soulUid=m.SoulType==0?0:items[items.Length-1].UID;
                using(var connection=DataHolder.MySqlConnection) {
                    connection.Open();using(var tx=connection.BeginTransaction()) {
                        long? savedCp,savedRvp;PurseBalance.CopySaved(c,out savedCp,out savedRvp);
                        using(var q=QuestInventory.Command(connection,tx,"SELECT revorapoint FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)c.Entity.UID))
                        using(var reader=q.ExecuteReader()) {
                            if(!reader.Read())throw new InvalidOperationException("Extraction owner missing");
                            long? stored=reader.IsDBNull(0)?(long?)null:Convert.ToInt64(reader.GetValue(0));
                            if(stored!=savedRvp)throw new InvalidOperationException("Extraction purse changed; reload required");
                        }
                        using(var q=QuestInventory.Command(connection,tx,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)c.Entity.UID))
                            if(Convert.ToInt64(q.ExecuteScalar())+items.Length>40){m.State="entered";return false;}
                        using(var q=new NpgsqlCommand("INSERT INTO revora_expedition_claims(cycle,uid,items,rvp,soul_type,soul_uid) VALUES(@cycle,@uid,@items,@rvp,@soul,@souluid) ON CONFLICT DO NOTHING",connection,tx)) {
                            q.Parameters.AddWithValue("cycle",CycleStart.ToUniversalTime().ToString("o"));q.Parameters.AddWithValue("uid",(long)c.Entity.UID);q.Parameters.AddWithValue("items",items.Length);q.Parameters.AddWithValue("rvp",(long)reward);q.Parameters.AddWithValue("soul",(long)m.SoulType);q.Parameters.AddWithValue("souluid",(long)soulUid);
                            if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Extraction already committed; reload required");
                        }
                        foreach(var item in items)using(var q=new NpgsqlCommand("INSERT INTO items(uid,id,entityid,stacksize,durability,maximdurability,color,bound) VALUES(@uid,@id,@owner,1,@dur,@dur,@color,@bound)",connection,tx)) {
                            q.Parameters.AddWithValue("uid",(long)item.UID);q.Parameters.AddWithValue("id",(int)item.ID);q.Parameters.AddWithValue("owner",(long)c.Entity.UID);q.Parameters.AddWithValue("dur",(int)item.Durability);q.Parameters.AddWithValue("color",(int)item.Color);q.Parameters.AddWithValue("bound",item.Bound?1:0);if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Extraction reward item was not saved");
                        }
                        using(var q=new NpgsqlCommand("UPDATE entities SET revorapoint=@rp WHERE uid=@uid",connection,tx)) {
                            q.Parameters.AddWithValue("rp",(long)(c.Entity.RevoraPoint+reward));q.Parameters.AddWithValue("uid",(long)c.Entity.UID);if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Extraction owner missing");
                        }
                        if(!Ready(c) || !QuestInventory.Player(c,false))throw new InvalidOperationException("Extraction session changed");
                        tx.Commit();
                    }
                }
                m.State="extracted";m.Loot.Clear();m.BonusPoints=0;c.Entity.ApplyCommittedRevoraPoints(c.Entity.RevoraPoint+reward);
                QuestInventory.Install(c,new uint[0],items);
                c.Send("You brought home "+relicCount+" relics, "+reward+" RVPs"+(m.SoulType==0?"":" and a bound P7 soul")+".");return true;
            } catch(Exception error) {
                m.State="dead";m.Loot.Clear();m.BonusPoints=0;c.Entity.PersistenceRequiresReload=true;c.Action=0;
                Console.WriteLine("[expedition] claim_failure uid="+c.Entity.UID+" type="+error.GetType().Name);c.Send("Please reconnect to recover your saved inventory safely.");c.Disconnect();return false;
            }
        }
    }
}
