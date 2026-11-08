using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.TaoistEpicRules;
using S=Revora.Game.Features.TaoistEpicStore;

namespace Revora.Game.Features
{
    // Mandatory startup integration; world/catalog identity is checked before listeners open.
    internal static class TaoistEpicQuest
    {
        internal const ushort Temple=3933;
        private static readonly ConditionalWeakTable<GameState,S.Offer> offers=new ConditionalWeakTable<GameState,S.Offer>();
        internal static bool Enabled {get;private set;}
        private static bool Legacy(uint npc){return npc==46554 || npc==47554 || npc==48554 || npc==48654;}
        internal static bool Handles(uint npc){return Enabled && (npc>=199580 && npc<=199584 || Legacy(npc));}
        private static void Terrain(){
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","3933.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="E0AD4F2C8242E8722F7A12679946012C5D972CBD0339D563D2DC8EA9E09E9716")throw new InvalidOperationException("Taoist temple terrain changed.");
        }
        internal static void PrepareMaps(){
            Terrain();MapsTable.MapInformation info;
            if(MapsTable.MapInformations.TryGetValue(Temple,out info)){
                if(info.ID!=Temple || info.BaseID!=Temple || info.Owner!=0)throw new InvalidOperationException("Taoist temple identity conflicts.");
            }else MapsTable.MapInformations.Add(Temple,new MapsTable.MapInformation{ID=Temple,BaseID=Temple,Status=8200});
        }
        internal static void Initialize(){
            Terrain();Map map,job;INpc moon;
            if(!Kernel.Maps.TryGetValue(Temple,out map) || map.ID!=Temple || map.BaseID!=Temple || map.Floor==null
                || !Kernel.Maps.TryGetValue(1004,out job) || job.BaseID!=1004 || !job.Npcs.TryGetValue(10000,out moon)
                || moon.MapID!=1004 || moon.Type!=Enums.NpcType.Talker)throw new InvalidOperationException("Taoist passage actors missing.");
            uint[][] sites={new uint[]{199580,81,75,50820},new uint[]{199581,80,80,35930},new uint[]{199582,64,64,35940},new uint[]{199583,48,48,35950},new uint[]{199584,76,83,35960}};
            string[] names={"SunKing","RefinedPureOven","ElitePureOven","SuperPureOven","PureTaoist"};
            foreach(var site in sites)foreach(var world in Kernel.Maps.Values){
                INpc found;if(world.Npcs.TryGetValue(site[0],out found) && (world.ID!=Temple || found.MapID!=Temple || found.X!=site[1] || found.Y!=site[2] || found.Mesh!=site[3] || found.Type!=Enums.NpcType.Talker))
                    throw new InvalidOperationException("Taoist temple NPC identity conflicts.");
                if(world.ID==Temple && world.Npcs.Values.Any(n=>n.UID!=site[0] && Math.Abs((int)n.X-(int)site[1])<=2 && Math.Abs((int)n.Y-(int)site[2])<=2))
                    throw new InvalidOperationException("Taoist temple NPC footprint conflicts.");
            }
            using(var c=DataHolder.MySqlConnection){c.Open();foreach(var site in sites){
                using(var q=Q.Command(c,null,"SELECT mapid,cellx,celly,lookface,type FROM npcs WHERE id=@id","id",(long)site[0]))using(var r=q.ExecuteReader())
                    if(r.Read() && (Convert.ToInt32(r[0])!=Temple || Convert.ToInt32(r[1])!=site[1] || Convert.ToInt32(r[2])!=site[2] || Convert.ToInt32(r[3])!=site[3] || Convert.ToInt32(r[4])!=(int)Enums.NpcType.Talker))
                        throw new InvalidOperationException("Saved Taoist temple NPC conflicts.");
            }}
            foreach(uint id in new uint[]{R.RedFlower,R.WhiteFlower,R.Leaf,R.Crystal,R.Fish,R.Panacea,R.Hossu,3006017,3006018,3006019,3006020})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Taoist quest catalog missing.");
            S.Initialize();TaoistEpicJournal.Initialize();TaoistEpicTraining.Initialize();
            for(int i=0;i<sites.Length;i++)if(!map.Npcs.ContainsKey(sites[i][0]))map.AddNpc(new NpcSpawn{UID=sites[i][0],MapID=Temple,X=(ushort)sites[i][1],Y=(ushort)sites[i][2],Mesh=(ushort)sites[i][3],Type=Enums.NpcType.Talker,Name=names[i]});
            if(!map.Floor[84,77,MapObjectType.Player])throw new InvalidOperationException("Taoist landing blocked.");
            HomePoint();Enabled=true;
        }
        private static ushort[] HomePoint(){
            var job=Kernel.Maps[1004];var moon=job.Npcs[10000];
            for(int distance=2;distance<=6;distance++)for(int dx=-distance;dx<=distance;dx++)for(int dy=-distance;dy<=distance;dy++){
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=distance)continue;int x=moon.X+dx,y=moon.Y+dy;
                if(x>=0 && y>=0 && x<=ushort.MaxValue && y<=ushort.MaxValue && job.Floor[x,y,MapObjectType.Player])return new[]{(ushort)x,(ushort)y};
            }throw new InvalidOperationException("Taoist Moon return blocked.");
        }
        internal static void Return(GameState p){var point=HomePoint();p.Entity.Teleport(1004,point[0],point[1]);if(p.Entity.MapID!=1004 || p.Entity.X!=point[0] || p.Entity.Y!=point[1])throw new InvalidOperationException("Taoist return failed.");}
        private static bool Near(GameState p,uint npc){ushort map=npc==10000?(ushort)1004:Temple;return Q.Near(p,npc,map) && p.Map.ID==map && p.Map.BaseID==map && p.Entity.PokerTable==0;}
        private static void Prepare(GameState p,S.Kind kind,byte choice,Npcs d){
            offers.Remove(p);string error;var offer=S.Prepare(p,kind,p.ActiveNpc,choice,out error);
            if(offer==null){d.Text(error);return;}offers.Add(p,offer);
            switch(kind){
                case S.Kind.Flowers:d.Text("Offer three Red and three White Flowers. The oven has a one-in-ten chance of revealing a Treasure Leaf.");break;
                case S.Kind.Paid:d.Text("Offer 27 CPs. The oven has a one-in-ten chance of revealing a Treasure Leaf.");break;
                case S.Kind.Elite:d.Text("Offer a Treasure Leaf. A one-in-ten chance reveals a Star Crystal; otherwise, one Yin Yang Fish remains.");break;
                case S.Kind.Super:d.Text("Offer a Star Crystal. A one-in-ten chance reveals Divine Panacea; otherwise, three Yin Yang Fish remain.");break;
                case S.Kind.Scroll:d.Text("Trade ten Yin Yang Fish for this scroll?");break;
                case S.Kind.Hossu:d.Text("Trade fifty Yin Yang Fish for a Hossu?");break;
                case S.Kind.Awaken:d.Text("Offer Divine Panacea to awaken your right-hand backsword. Its upgrades remain, and a bound P7 soul is yours.");break;
            }
            if(offer.Bound)d.Text("A bound offering binds its result to you.");d.Option("Proceed.",20);
        }
        internal static bool HandleNpc(GameState p,NpcRequest req){
            if(p==null || req==null)return false;
            if(TaoistEpicTraining.HandleNpc(p,req))return true;
            if(!Enabled)return false;
            if(NinjaFlowerHunt.HandleNpc(p,req))return true;
            bool moon=p.ActiveNpc==10000 && (req.OptionID>=150 && req.OptionID<=157 || req.OptionID==86);
            if(!Handles(p.ActiveNpc) && !moon)return false;
            if(p.ItemSyncRoot==null || req.NpcID!=0 && req.NpcID!=p.ActiveNpc)return true;
            lock(p.ItemSyncRoot){
                uint npc=p.ActiveNpc;
                if(Legacy(npc)){if(Q.Near(p,npc,p.Entity.MapID))p.Send("Seek Taoist Moon in the Job Center to enter the temple trials.");return true;}
                if(!Near(p,npc))return true;
                try{
                    var d=new Npcs(p);byte option=req.OptionID;
                    if(option==0 || option==255)offers.Remove(p);if(option==255)return true;
                    if(option==254){offers.Remove(p);Return(p);return true;}
                    if(!S.Eligible(p))d.Text("These trials await second-reborn Taoists of level 70.");
                    else if(moon){
                        offers.Remove(p);
                        if(option==151 || option==86){if(!Kernel.Maps[Temple].Floor[84,77,MapObjectType.Player])throw new InvalidOperationException("Taoist landing blocked.");p.Entity.Teleport(Temple,84,77);return true;}
                        d.Text("Seek the three ovens, then bring Divine Panacea to Sun King. Red and White Flowers grow among the Abyss demons.");
                        d.Option("Enter the temple.",151);d.Option("Hunt for flowers.",154);
                    }else if(option==20){
                        S.Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Let us review the offering first.");}
                        else{offers.Remove(p);if(offer.Npc!=npc || S.Clock()>=offer.Until || offer.Cache!=FirstRebirth.Cache(p) || offer.BeforeCp!=p.Entity.ConquerPoints || offer.Vip!=p.Entity.VIPLevel)d.Text("Let us review your offering again.");
                            else{var result=S.Commit(p,offer,()=>Near(p,npc));d.Text(offer.Kind==S.Kind.Awaken?"Your backsword has awakened. A bound P7 soul awaits in your bag.":result.Count==0?"The oven yields nothing. Return with another offering.":"Your offering is accepted. Take what the oven has revealed.");}}
                    }else if(npc==199580){if(option==4)Prepare(p,S.Kind.Awaken,0,d);else if(option==0){d.Text("Pass the three ovens. Bring Divine Panacea and a backsword whose epic form is known.");d.Option("Awaken my backsword.",4);}}
                    else if(npc==199584){if(option>=1 && option<=3 || option==5)Prepare(p,S.Kind.Scroll,option==5?(byte)4:option,d);else if(option==4)Prepare(p,S.Kind.Hossu,0,d);else if(option==0){d.Text("Even a failed trial leaves a gift. Bring Yin Yang Fish to trade.");d.Option("Flame Lotus scroll: 10 Fish.",1);d.Option("Aurora Lotus scroll: 10 Fish.",2);d.Option("Breaking Touch scroll: 10 Fish.",3);d.Option("Blessing Touch scroll: 10 Fish.",5);d.Option("Hossu: 50 Fish.",4);}}
                    else if(option==1)Prepare(p,npc==199581?S.Kind.Flowers:npc==199582?S.Kind.Elite:S.Kind.Super,0,d);
                    else if(npc==199581 && option==2)Prepare(p,S.Kind.Paid,0,d);
                    else if(option==0){d.Text(npc==199581?"Offer flowers or CPs to seek a Treasure Leaf.":npc==199582?"Offer a Treasure Leaf to seek a Star Crystal.":"Offer a Star Crystal to seek Divine Panacea.");d.Option("Review my offering.",1);if(npc==199581)d.Option("Offer 27 CPs.",2);}
                    if(!moon)d.Option("Return to Taoist Moon.",254);d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){offers.Remove(p);Q.Fail(p,"taoist-epic",ex);}
            }return true;
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(!Enabled || p==null || p.Inventory==null || packet==null || packet.Length<12
                || !p.Inventory.TryGetItem(System.BitConverter.ToUInt32(packet,8),out item) || item.ID!=R.Panacea)return false;
            if(packet.Length<32 || packet.Length>128 || System.BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || System.BitConverter.ToUInt16(packet,2)!=1009 || System.BitConverter.ToUInt16(packet,20)!=4
                || System.BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=R.Panacea || !Enabled)return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;if(!S.Eligible(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !S.Usable(item))return true;
                string error;var offer=S.Prepare(p,S.Kind.Awaken,0,0,out error,item.UID);if(offer==null){p.Send(error);return true;}
                S.Commit(p,offer,()=>S.Eligible(p));p.Send("Your backsword has awakened. A bound P7 soul awaits in your bag.");
            }catch(Exception ex){Q.Fail(p,"taoist-panacea",ex);}return true;
        }
    }
}
