using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type TrojanScenes = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicScenes");
    static object TrojanScene(GameState p) {
        lock (p.ItemSyncRoot) return Call(TrojanScenes, null, "Create", p, Field(TrojanProgress(p), "Stage"));
    }
    static void TrojanTravel(GameState p, object scene, string action) {
        lock (p.ItemSyncRoot) Call(TrojanScenes, null, action, scene, p);
    }
    static void VerifyTrojanCapacity(Map home) {
        var heroes=new List<GameState>();var reserved=new List<object>();
        try {
            for(int n=0;n<34;n++) {
                var hero=TrojanPlayer(home);heroes.Add(hero);
                TrojanCall(hero,"Begin",0,TrojanContext);
                // Earn storage prerequisites without replaying the separately tested
                // 90-day NPC story for every capacity contender.
                for(int day=0;day<27;day++) {
                    trojanNow=trojanNow.AddDays(1);
                    TrojanCall(hero,"Advance",TrojanValue(hero,"Revision"),null,TrojanContext);
                }
                TrojanCall(hero,"Advance",TrojanValue(hero,"Revision"),null,TrojanContext);
                if(TrojanValue(hero,"Stage")!=3)throw new Exception("Capacity fixture failed to earn arrival.");
                hero.Entity.Teleport(1002,425,302);EntityTable.SaveEntity(hero);
                TrojanTalk(hero,10579,0);TrojanTalk(hero,10579,1);
                var scene=Call(TrojanScenes,null,"Find",hero);if(scene!=null)reserved.Add(scene);
            }
            Check(reserved.Count==32 && heroes.Take(32).All(p=>!Fenced(p) && p.Map.BaseID==3825)
                && heroes.Take(32).Select(p=>p.Entity.MapID).Distinct().Count()==32,
                "thirty-two ordinary Trojan entrants own distinct private rooms");
            var waiting=heroes.Skip(32).ToArray();
            Check(waiting.All(p=>!Fenced(p) && p.Entity.MapID==1002 && TrojanValue(p,"Stage")==3 && TrojanValue(p,"Energy")==27),
                "full Trojan capacity leaves both waiting players usable without spending energy or advancing");
            var freed=(Map)Field(reserved[0],"Map");TrojanTravel(heroes[0],reserved[0],"Exit");
            Check(!Revora.Kernel.Maps.ContainsKey(freed.ID) && !MapsTable.MapInformations.ContainsKey(freed.ID),
                "leaving a full Trojan room releases terrain and metadata together");
            Parallel.ForEach(waiting,p=>{TrojanTalk(p,10579,0);TrojanTalk(p,10579,1);});
            Check(waiting.Count(p=>p.Map.BaseID==3825)==1 && waiting.All(p=>!Fenced(p) && TrojanValue(p,"Energy")==27),
                "concurrent public retries claim exactly one free room without fencing the other contender");
            foreach(var hero in waiting){var scene=Call(TrojanScenes,null,"Find",hero);if(scene!=null)reserved.Add(scene);}
        } finally {
            foreach(var scene in reserved)if(!(bool)Field(scene,"Released"))TrojanTravel((GameState)Field(scene,"Owner"),scene,"Exit");
        }
        Check(heroes.All(p=>Call(TrojanScenes,null,"Find",p)==null && !Fenced(p))
            && reserved.All(s=>!Revora.Kernel.Maps.ContainsKey(((Map)Field(s,"Map")).ID)),
            "capacity stress releases every owned room without orphaned instances");
    }
    static GameState VerifyTrojanScene(GameState p) {
        var home = Revora.Kernel.Maps[1004]; int stage = TrojanValue(p, "Stage"), revision = TrojanValue(p, "Revision");
        var scene = TrojanScene(p); var map = (Map)Field(scene, "Map");
        uint[] actors = stage == 3 ? new uint[] { 10581,10582,10583 } : stage == 9 ? new uint[] { 10600,10602 } : new uint[] { 10603 };
        Check(map.BaseID == 3825 && map.Floor.Bounds.Width == 96 && map.Npcs.Keys.OrderBy(x => x).SequenceEqual(actors.OrderBy(x => x))
            && map.Portals.Count == 0 && map.Entities.Count == 0 && MapsTable.MapInformations[map.ID].Owner == p.Entity.UID,
            "private Trojan part has installed NPC profiles, isolated terrain and no inherited catalog actors: " + stage);
        bool denied = false; try { TrojanScene(p); } catch (InvalidOperationException) { denied = true; }
        Check(denied, "duplicate Trojan scene reservation is rejected");
        var other = TrojanPlayer(home);
        Check(!(bool)Call(TrojanScenes, null, "AllowTeleport", other, map.ID, (ushort)45, (ushort)45)
            && !(bool)Call(TrojanScenes, null, "AllowTeleport", p, map.ID, (ushort)45, (ushort)45),
            "outsider and untransferred owner cannot enter reserved Trojan terrain");
        other.Entity.Teleport(map.ID, 45, 45);
        Check(other.Entity.MapID == 1004, "actual teleport handler rejects foreign private-map entry");
        TrojanTravel(p, scene, "Enter");
        Check(p.Entity.MapID == map.ID && (bool)Call(TrojanScenes, null, "Owns", scene, p)
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid", p.Entity.UID) == map.ID,
            "owned Trojan transfer persists arrival on real terrain");
        denied = false; try { Call(TrojanScenes, null, "Release", scene); } catch (InvalidOperationException) { denied = true; }
        Check(denied && Revora.Kernel.Maps.ContainsKey(map.ID), "occupied private terrain cannot be disposed");
        foreach (var npc in map.Npcs.Values) {
            var tile = FlowerTile(map, npc.X, npc.Y); AdventureWalk(p, tile.Item1, tile.Item2);
            Check(Revora.Kernel.GetDistance(p.Entity.X, p.Entity.Y, npc.X, npc.Y) <= 6,
                "ordinary walking reaches Trojan actor " + npc.UID);
        }
        AdventureWalk(p, 45, 40);
        Check(TrojanValue(p, "Revision") == revision, "scene travel and walking grant no quest progression");
        if (stage == 3) {
            p = ReloadBall(p); Call(TrojanScenes, null, "Tick");
            Check(!Fenced(p) && p.Entity.MapID == 1002 && !Revora.Kernel.Maps.ContainsKey(map.ID),
                "production reload uses its Twin City private-map fallback and releases the old room");
            scene = TrojanScene(p); map = (Map)Field(scene, "Map"); TrojanTravel(p, scene, "Enter");
            Check(TrojanValue(p, "Revision") == revision, "fresh owner resumes its stored stage without extra energy");
        }
        if (stage == 9) {
            scene.GetType().GetField("Until", Instance).SetValue(scene, DateTime.UtcNow.AddSeconds(-1));
            Check(!(bool)Call(TrojanScenes, null, "Owns", scene, p), "expired Trojan scene revokes ownership immediately");
            Call(TrojanScenes, null, "Tick");
        } else TrojanTravel(p, scene, "Exit");
        Call(TrojanScenes, null, "Release", scene);
        Check(p.Entity.MapID == 1004 && Revora.Kernel.GetDistance(p.Entity.X, p.Entity.Y, home.Npcs[10022].X, home.Npcs[10022].Y) <= 4
            && !Revora.Kernel.Maps.ContainsKey(map.ID) && !MapsTable.MapInformations.ContainsKey(map.ID)
            && TrojanValue(p, "Revision") == revision, "Trojan scene returns near its trainer and releases once without losing progress");
        return p;
    }
}
