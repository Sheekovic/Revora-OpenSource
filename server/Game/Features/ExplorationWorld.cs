using System;
using System.IO;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ExplorationWorld
    {
        internal const uint GrottoEnvoy=27837, GardenKeeper=44100;
        // Runtime defaults for existing client maps; explicit compatible owner
        // metadata is preserved. These are Revora map policies, not new wire facts.
        internal static void PrepareMaps() {
            Terrain(1762,"9ee2b27d737769c53575a1862b821e950af855b6fc680d03b041f56f15f04873");
            Terrain(1806,"0a1a8b62b23da2060858fdb7f7270163c4b6cc03c8d6328b7012919ab05fb5e3");
            Terrain(1927,"5f4b3f51a8a022c217cf553909d96583d05d489953128a4fbf599b088611cbce");
            Validate(1806);Validate(1927);
            Install(1806,2);Install(1927,8200);
        }
        private static void Terrain(ushort id,string expected) {
            using(var hash=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=expected)
                    throw new InvalidOperationException("Exploration terrain differs from the reviewed map: "+id);
        }
        private static void Validate(ushort id) {
            MapsTable.MapInformation current;
            if(MapsTable.MapInformations.TryGetValue(id,out current) && (current.ID!=id || current.BaseID!=id || current.Owner!=0))
                throw new InvalidOperationException("Exploration destination identity conflicts: "+id);
        }
        private static void Install(ushort id,uint status) {
            if(!MapsTable.MapInformations.ContainsKey(id))
                MapsTable.MapInformations.Add(id,new MapsTable.MapInformation{ID=id,BaseID=id,Status=status,Weather=0});
        }
        private static void Travel(GameState p,ushort id,ushort x,ushort y) {
            Map map;MapsTable.MapInformation info;
            if(!Kernel.Maps.TryGetValue(id,out map) || map.ID!=id || map.BaseID!=id
                || !MapsTable.MapInformations.TryGetValue(id,out info) || info.ID!=id || info.BaseID!=id || info.Owner!=0
                || !PotencyGuide.FindLanding(map,ref x,ref y)) {
                p.Send("The path is closed for now. Please come back later.");return;
            }
            p.Entity.Teleport(id,x,y);
        }
        internal static void Garden(GameState p) {
            if(p==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot) {
                if(!Q.Near(p,DailyQuestEnvoySystem.NpcId,1002) || !Config.DailyEnvoyShowMagnolia)return;
                if(p.Entity.Level<Config.DailyMagnoliaMinLevel) {
                    p.Send("Come back when you reach level "+Config.DailyMagnoliaMinLevel+".");return;
                }
                if(Config.DailyMagnoliaMap!=1806){p.Send("The garden path is closed for now.");return;}
                Travel(p,1806,Config.DailyMagnoliaX,Config.DailyMagnoliaY);
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null)return false;
            bool grotto=p.ActiveNpc==GrottoEnvoy;
            bool returning=p.ActiveNpc==GardenKeeper && request.OptionID==2;
            if(!grotto && !returning)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                ushort origin=grotto?(ushort)1000:(ushort)1806;
                if((request.NpcID!=0 && request.NpcID!=p.ActiveNpc) || !Q.Near(p,p.ActiveNpc,origin))return true;
                if(returning){Travel(p,1002,319,310);return true;}
                switch(request.OptionID) {
                    case 0:
                        var dialog=new Npcs(p);dialog.Avatar(183);
                        dialog.Text("The frozen caverns lie beyond the desert. Which floor will you brave?");
                        dialog.Option("The first floor.",1);dialog.Option("The second floor.",2);
                        dialog.Option("Not today.",255);dialog.Send();break;
                    case 1: Travel(p,1762,334,354);break;
                    case 2: Travel(p,1927,388,628);break;
                }
            }
            return true;
        }
    }
}
