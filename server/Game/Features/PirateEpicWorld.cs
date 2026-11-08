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
using R=Revora.Game.Features.PirateEpicRules;
using S=Revora.Game.Features.PirateEpicStore;

namespace Revora.Game.Features
{
    // Private Revora hub choice; original hub identity and native acceptance remain unverified.
    internal static class PirateEpicWorld
    {
        internal const ushort Hub=10271;
        internal static bool Enabled {get;private set;}
        private static void Terrain(){
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","10271.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="268E254AF092F8CEF3E7ED71F69A658467ACA6225C335A1BB77241BD2113E065")throw new InvalidOperationException("Pirate temple terrain changed.");
        }
        internal static void PrepareMaps(){
            Terrain();MapsTable.MapInformation info;
            if(MapsTable.MapInformations.TryGetValue(Hub,out info)){
                if(info.ID!=Hub || info.BaseID!=Hub || info.Owner!=0)throw new InvalidOperationException("Pirate temple identity conflicts.");
            }else MapsTable.MapInformations.Add(Hub,new MapsTable.MapInformation{ID=Hub,BaseID=Hub,Status=8200});
        }
        internal static void Initialize(){
            Terrain();Map map,job;INpc moon;
            // The approved server catalog omits this hub. PrepareMaps establishes
            // its identity; instantiate the same hash-checked grid used by the quest.
            if(!Kernel.Maps.ContainsKey(Hub))new Map(Hub,"map/map/beach.7z");
            if(!Kernel.Maps.TryGetValue(Hub,out map) || map.ID!=Hub || map.BaseID!=Hub || map.Floor==null
                || !Kernel.Maps.TryGetValue(1004,out job) || job.BaseID!=1004 || !job.Npcs.TryGetValue(4272,out moon)
                || moon.MapID!=1004 || moon.Type!=Enums.NpcType.Talker)throw new InvalidOperationException("Pirate passage actors missing.");
            uint[][] sites={new uint[]{199590,116,191,59830},new uint[]{199591,128,187,6180},new uint[]{199592,107,178,59810}};
            string[] names={"JackSparrow","Mermaid","Luffy"};
            foreach(var site in sites)foreach(var world in Kernel.Maps.Values){
                INpc found;if(world.Npcs.TryGetValue(site[0],out found) && (world.ID!=Hub || found.MapID!=Hub || found.X!=site[1] || found.Y!=site[2] || found.Mesh!=site[3] || found.Type!=Enums.NpcType.Talker))
                    throw new InvalidOperationException("Pirate temple NPC identity conflicts.");
                if(world.ID==Hub && world.Npcs.Values.Any(n=>n.UID!=site[0] && Math.Abs((int)n.X-(int)site[1])<=2 && Math.Abs((int)n.Y-(int)site[2])<=2))
                    throw new InvalidOperationException("Pirate temple NPC footprint conflicts.");
            }
            using(var c=DataHolder.MySqlConnection){c.Open();foreach(var site in sites){
                using(var q=Q.Command(c,null,"SELECT mapid,cellx,celly,lookface,type FROM npcs WHERE id=@id","id",(long)site[0]))using(var r=q.ExecuteReader())
                    if(r.Read() && (Convert.ToInt32(r[0])!=Hub || Convert.ToInt32(r[1])!=site[1] || Convert.ToInt32(r[2])!=site[2] || Convert.ToInt32(r[3])!=site[3] || Convert.ToInt32(r[4])!=(int)Enums.NpcType.Talker))
                        throw new InvalidOperationException("Saved Pirate temple NPC conflicts.");
            }}
            S.Initialize();
            for(int i=0;i<sites.Length;i++)if(!map.Npcs.ContainsKey(sites[i][0]))map.AddNpc(new NpcSpawn{UID=sites[i][0],MapID=Hub,X=(ushort)sites[i][1],Y=(ushort)sites[i][2],Mesh=(ushort)sites[i][3],Type=Enums.NpcType.Talker,Name=names[i]});
            if(!map.Floor[114,188,MapObjectType.Player])throw new InvalidOperationException("Pirate landing blocked.");
            HomePoint();Enabled=true;
        }
        private static ushort[] HomePoint(){
            var job=Kernel.Maps[1004];var moon=job.Npcs[4272];
            for(int distance=2;distance<=6;distance++)for(int dx=-distance;dx<=distance;dx++)for(int dy=-distance;dy<=distance;dy++){
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=distance)continue;int x=moon.X+dx,y=moon.Y+dy;
                if(x>=0 && y>=0 && x<=ushort.MaxValue && y<=ushort.MaxValue && job.Floor[x,y,MapObjectType.Player])return new[]{(ushort)x,(ushort)y};
            }throw new InvalidOperationException("Pirate trainer return blocked.");
        }
        internal static void Return(GameState p){var point=HomePoint();p.Entity.Teleport(1004,point[0],point[1]);if(p.Entity.MapID!=1004 || p.Entity.X!=point[0] || p.Entity.Y!=point[1])throw new InvalidOperationException("Pirate return failed.");}
        private sealed class Pending {
            internal uint Npc;
            internal S.MapOffer Map;
            internal S.FlowerOffer Flowers;
            internal S.ExchangeOffer Exchange;
            internal S.Offer Awakening;
            internal S.SearchOffer Search;
        }
        private static readonly ConditionalWeakTable<GameState,Pending> offers=new ConditionalWeakTable<GameState,Pending>();
        private static bool Near(GameState p,uint npc){ushort map=npc==4272?(ushort)1004:Hub;return Q.Near(p,npc,map) && p.Map.ID==map && p.Map.BaseID==map && p.Entity.PokerTable==0;}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;
            if(!Enabled)return p.ActiveNpc==4272 && (request.OptionID==15 || request.OptionID>=23 && request.OptionID<=25);
            if(TravelNpc(p,request))return true;
            if(NinjaFlowerHunt.HandleNpc(p,request))return true;
            uint npc=p.ActiveNpc;byte option=request.OptionID;
            bool trainer=npc==4272 && (option==15 || option==150 || option>=23 && option<=25);
            if(!trainer && (npc<199590 || npc>199592))return false;
            if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=npc)return true;
            lock(p.ItemSyncRoot){
                if(!Near(p,npc)){offers.Remove(p);return true;}
                try{
                    if(trainer && option==15){
                        offers.Remove(p);var lesson=new Npcs(p);
                        if(S.LearnWindstorm(p,()=>Near(p,4272)))lesson.Text("You have learned Windstorm. Let your blade follow the wind.");
                        else lesson.Text(p.Spells.ContainsKey(11140)?"You already know Windstorm. Practice it well.":"Return as a Pirate of level 15 to learn Windstorm.");
                        lesson.Option("Farewell.",255);lesson.Send();return true;
                    }
                    if(trainer && option!=150){offers.Remove(p);var trainerDialog=new Npcs(p);trainerDialog.Text("These arts live within the sea scrolls. Trade Water of the Sea with Mermaid, then use the scroll to learn.");trainerDialog.Option("Farewell.",255);trainerDialog.Send();return true;}
                    if(trainer){offers.Remove(p);if(!Enter(p))p.Send("The sea calls second-reborn Pirates of level 70.");return true;}
                    var d=new Npcs(p);
                    if(option==0 || option==255)offers.Remove(p);
                    if(option==255)return true;
                    if(option==254){offers.Remove(p);Return(p);return true;}
                    if(option==20){
                        Pending pending;
                        if(!offers.TryGetValue(p,out pending)){d.Text("Tell me which trade you seek first.");}
                        else{
                            offers.Remove(p);
                            if(pending.Npc!=npc)d.Text("Let us review the trade here.");
                            else if(pending.Search!=null){S.CommitSearch(p,pending.Search,()=>Near(p,npc));d.Text("The search is done. Take what the sea has yielded.");}
                            else if(pending.Awakening!=null){S.Commit(p,pending.Awakening,()=>Near(p,npc));d.Text("Your weapon has awakened. A bound P7 soul awaits in your bag.");}
                            else if(pending.Map!=null){S.CommitMap(p,pending.Map,()=>Near(p,npc));d.Text("Your chart is ready. May the sea favor you.");}
                            else if(pending.Flowers!=null){S.CommitFlowers(p,pending.Flowers,()=>Near(p,npc));d.Text("Take this bottle. Perhaps it holds your fortune.");}
                            else if(pending.Exchange!=null){S.CommitExchange(p,pending.Exchange,()=>Near(p,npc));d.Text("The scroll is yours.");}
                        }
                    }else if(npc==199592 && option>=1 && option<=6){
                        offers.Remove(p);uint type=option==6?R.Bottle:R.FirstMap+(uint)option-1;
                        var input=p.Inventory.Objects.Where(i=>i.ID==type && S.SearchInput(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
                        string error="Bring that chart or bottle first.";
                        var search=input==null?null:S.PrepareSearch(p,input.UID,out error);
                        if(search==null)d.Text(error);
                        else{
                            offers.Add(p,new Pending{Npc=npc,Search=search});
                            d.Text(option==6?"Open one bottle for a chart of one to three stars?":option==5?"Use one five-star chart to claim a Heart of the Sea and Water of the Sea?":"Use one "+option+"-star chart? The chance of another chart is "+(option==1?30:option==2?25:20)+" percent. Otherwise, you receive Water of the Sea.");
                            if(search.Bound)d.Text("This offering is bound; its treasure remains bound.");
                            d.Option("Proceed.",20);
                        }
                    }else if(npc==199590 && option==3){
                        offers.Remove(p);
                        var heart=p.Inventory.Objects.Where(S.Heart).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
                        string error="Bring a Heart of the Sea.";
                        var awakening=heart==null?null:S.Prepare(p,heart.UID,out error);
                        if(awakening==null)d.Text(error);
                        else{
                            offers.Add(p,new Pending{Npc=npc,Awakening=awakening});
                            d.Text("Offer one Heart of the Sea to awaken your "+(awakening.Weapon.Position==4?"rapier":"pistol")+"? Its upgrades remain, and you receive one bound P7 soul.");
                            d.Text(awakening.HeartBound?"This Heart is bound; the awakened weapon will be bound.":awakening.WeaponBound?"Your weapon remains bound.":"Your weapon remains unbound.");
                            d.Option("Awaken my weapon.",20);
                        }
                    }else if(npc==199590 && (option==1 || option==2) || npc==199591 && option>=1 && option<=3){
                        offers.Remove(p);string error;var pending=new Pending{Npc=npc};
                        if(npc==199591){pending.Exchange=S.PrepareExchange(p,option,out error);}
                        else if(option==1){pending.Map=S.PrepareMap(p,out error);}
                        else{pending.Flowers=S.PrepareFlowers(p,out error);}
                        if(pending.Map==null && pending.Flowers==null && pending.Exchange==null)d.Text(error);
                        else{
                            offers.Add(p,pending);
                            d.Text(pending.Map!=null?"Buy one treasure chart for 27 CPs?":pending.Flowers!=null?"Offer three Red and three White Flowers for today's bottle? Bound flowers bind the bottle.":"Trade ten Water of the Sea for this scroll? Bound Water binds the scroll.");
                            d.Option("Agreed.",20);
                        }
                    }else if(option==0){
                        if(npc==199590){d.Text("Fortune favors those who brave the sea. A chart or a daily flower offering starts your journey.");d.Option("Buy a chart: 27 CPs.",1);d.Option("Offer three flowers of each color.",2);d.Option("Awaken my weapon.",3);}
                        else if(npc==199591){d.Text("Bring ten Water of the Sea for a scroll.");d.Option("Sea Burial.",1);d.Option("Bloom of Death.",2);d.Option("Immortal Force.",3);}
                        else{d.Text("I can search a chart for you, or open a bottle. Each search consumes one offering.");for(byte star=1;star<=5;star++)d.Option("Search a "+star+"-star chart.",star);d.Option("Open a bottle.",6);}
                    }
                    d.Option("Return to the Job Center.",254);d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){offers.Remove(p);Q.Fail(p,"pirate-dialogue",ex);}
            }return true;
        }
        // Private base-grid prototype. Never called by normal startup; native collision remains pending.
        private sealed class TravelSite {internal ushort Map,X,Y,NpcX,NpcY;internal string Hash;internal uint Npc {get{return 199600u+(uint)(Map-10266);}}}
        private static readonly TravelSite[] travelSites={new TravelSite{Map=10266,X=29,Y=56,NpcX=33,NpcY=56,Hash="79D7BA03561F0B00F38B7BF59BEEF4301C459423DBB90E237DE40DB086C4992D"},new TravelSite{Map=10267,X=56,Y=78,NpcX=60,NpcY=78,Hash="98DFA1214ADA8DD08DA5278C2725F95947AC339CAC585C0A4FFB599E5340597D"},new TravelSite{Map=10268,X=25,Y=42,NpcX=29,NpcY=42,Hash="6996EB221B1501169B7A4DBEEF4B1B754925E11CA7046B9B42A6971D05016331"},new TravelSite{Map=10269,X=46,Y=56,NpcX=50,NpcY=56,Hash="69870933ADFBC519C22B5B117A22FF9A5C06AC3A9DB2F10B54A7883A56C6BBF9"},new TravelSite{Map=10270,X=26,Y=45,NpcX=30,NpcY=45,Hash="052BE3A2D737F541EE9B431ACD985ECBA93942FA38D11652E70D44BBA6073537"}};
        private static bool travelEnabled;
        private static readonly ConditionalWeakTable<GameState,S.SearchOffer> travelOffers=new ConditionalWeakTable<GameState,S.SearchOffer>();
        internal static void InitializeTravel(){
            if(!Enabled)throw new InvalidOperationException("Pirate hub required.");
            foreach(var site in travelSites){
                using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",site.Map+".map")))
                    if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!=site.Hash)throw new InvalidOperationException("Pirate travel terrain differs.");
                MapsTable.MapInformation info;
                if(MapsTable.MapInformations.TryGetValue(site.Map,out info)){
                    if(info.ID!=site.Map || info.BaseID!=site.Map || info.Owner!=0)throw new InvalidOperationException("Pirate travel map conflict.");
                }else MapsTable.MapInformations.Add(site.Map,new MapsTable.MapInformation{ID=site.Map,BaseID=site.Map,Status=8200});
                Map map;if(!Kernel.Maps.TryGetValue(site.Map,out map))map=new Map(site.Map,"map/map/pirate0"+(site.Map-10264)+"_new.7z");
                if(map.ID!=site.Map || map.BaseID!=site.Map || !map.Floor[site.X,site.Y,MapObjectType.Player])throw new InvalidOperationException("Pirate travel landing differs.");
                foreach(var world in Kernel.Maps.Values){INpc npc;if(world.Npcs.TryGetValue(site.Npc,out npc) && (world.ID!=site.Map || npc.MapID!=site.Map || npc.X!=site.NpcX || npc.Y!=site.NpcY || npc.Mesh!=59810 || npc.Type!=Enums.NpcType.Talker))throw new InvalidOperationException("Pirate travel NPC conflict.");}
                if(map.Npcs.Values.Any(n=>n.UID!=site.Npc && Math.Abs((int)n.X-site.NpcX)<=2 && Math.Abs((int)n.Y-site.NpcY)<=2))throw new InvalidOperationException("Pirate travel footprint conflict.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT count(*) FROM npcs WHERE id=@id","id",(long)site.Npc))if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Pirate travel NPC reserved in catalog.");}
                if(!map.Npcs.ContainsKey(site.Npc))map.AddNpc(new NpcSpawn{UID=site.Npc,MapID=site.Map,X=site.NpcX,Y=site.NpcY,Mesh=59810,Type=Enums.NpcType.Talker,Name="TreasureGuide"});
            }travelEnabled=true;
        }
        internal static bool RestoresLocation(GameState p){
            if(!Enabled || p==null || p.Entity==null)return false;
            ushort id=p.Entity.MapID;
            if(id!=Hub && (!travelEnabled || !travelSites.Any(a=>a.Map==id)))return false;
            Map map;return Kernel.Maps.TryGetValue(id,out map) && map.ID==id && map.BaseID==id && map.Floor!=null && map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player];
        }
        private static bool TravelNear(GameState p,TravelSite site){return Q.Near(p,site.Npc,site.Map) && p.Map.ID==site.Map && p.Map.BaseID==site.Map && p.Entity.PokerTable==0;}
        private static void TravelHome(GameState p){p.Entity.Teleport(Hub,114,188);if(p.Entity.MapID!=Hub || p.Entity.X!=114 || p.Entity.Y!=188)throw new InvalidOperationException("Pirate travel return failed.");}
        private static bool TravelNpc(GameState p,NpcRequest request){
            if(!travelEnabled)return false;var site=travelSites.FirstOrDefault(a=>a.Npc==p.ActiveNpc);if(site==null)return false;
            if(request.NpcID!=0 && request.NpcID!=site.Npc || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try{
                if(!TravelNear(p,site)){travelOffers.Remove(p);return true;}
                if(request.OptionID==254){travelOffers.Remove(p);TravelHome(p);return true;}
                if(request.OptionID==0 || request.OptionID==255)travelOffers.Remove(p);
                if(request.OptionID==255)return true;
                var d=new Npcs(p);
                if(request.OptionID==1){
                    travelOffers.Remove(p);var item=p.Inventory.Objects.Where(i=>i.ID==R.FirstMap+(uint)(site.Map-10266) && S.SearchInput(i)).OrderBy(i=>i.UID).FirstOrDefault();string error="Bring this sea's chart.";
                    var offer=item==null?null:S.PrepareSearch(p,item.UID,out error);
                    if(offer==null)d.Text(error);else{travelOffers.Add(p,offer);d.Text("Use one chart to search here? The chart is consumed when you confirm.");d.Option("Search now.",20);}
                }else if(request.OptionID==20){S.SearchOffer offer;if(travelOffers.TryGetValue(p,out offer)){travelOffers.Remove(p);S.CommitSearch(p,offer,()=>TravelNear(p,site));TravelHome(p);return true;}d.Text("Show me your chart first.");}
                else{d.Text("Your chart points to these waters. Shall we search?");d.Option("Review my chart.",1);}
                d.Option("Return to Sea of Rage.",254);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){travelOffers.Remove(p);Q.Fail(p,"pirate-travel",ex);}return true;
        }
        internal static bool TryStack(GameState p,byte[] packet){
            ConquerItem first,second;
            if(!Enabled || p==null || p.Inventory==null || packet==null || packet.Length<16)return false;
            uint a=System.BitConverter.ToUInt32(packet,8),b=System.BitConverter.ToUInt32(packet,12);
            bool known=p.Inventory.TryGetItem(a,out first) && S.StackMaterial(first.ID);
            ushort action=packet.Length>=22?System.BitConverter.ToUInt16(packet,20):(ushort)0;
            if(action==48)known|=p.Inventory.TryGetItem(b,out second) && S.StackMaterial(second.ID);if(!known)return false;
            if(packet.Length<32 || packet.Length>128 || System.BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || System.BitConverter.ToUInt16(packet,2)!=1009 || (action!=48 && action!=49)
                || System.BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try{if(action==48)S.Merge(p,a,b);else S.Split(p,a,b);}catch(Exception ex){Q.Fail(p,"pirate-stack",ex);}return true;
        }
        private static bool UseItemType(uint id){return id==R.Bottle || id==R.Heart || R.ScrollSpell(id)!=0 || travelEnabled && R.MapStars(id)>0;}
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(!Enabled || p==null || p.Inventory==null || packet==null || packet.Length<12
                || !p.Inventory.TryGetItem(System.BitConverter.ToUInt32(packet,8),out item) || !UseItemType(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || System.BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || System.BitConverter.ToUInt16(packet,2)!=1009 || System.BitConverter.ToUInt16(packet,20)!=4
                || System.BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || !UseItemType(item.ID))return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;
                if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                if(travelEnabled && R.MapStars(item.ID)>0){
                    if(p.Entity.MapID!=Hub || p.Map.ID!=Hub || p.Map.BaseID!=Hub){p.Send("Set sail with your chart from the Sea of Rage.");return true;}
                    string error;if(S.PrepareSearch(p,item.UID,out error)==null){p.Send(error);return true;}
                    var site=travelSites[R.MapStars(item.ID)-1];travelOffers.Remove(p);p.Entity.Teleport(site.Map,site.X,site.Y);
                    if(p.Entity.MapID!=site.Map || p.Entity.X!=site.X || p.Entity.Y!=site.Y)throw new InvalidOperationException("Pirate travel entry failed.");return true;
                }
                if(item.ID==R.Bottle){
                    string error;var offer=S.PrepareSearch(p,item.UID,out error);
                    if(offer==null){p.Send(error);return true;}
                    S.CommitSearch(p,offer,()=>Enabled && Q.Player(p));p.Send("A treasure chart lies within the bottle.");return true;
                }
                if(item.ID==R.Heart){
                    string error;var offer=S.Prepare(p,item.UID,out error);
                    if(offer==null){p.Send(error);return true;}
                    S.Commit(p,offer,()=>Enabled && Q.Player(p));p.Send("Your weapon has awakened. A bound P7 soul awaits in your bag.");return true;
                }
                if(S.LearnScroll(p,item.UID,()=>Enabled && Q.Player(p)))p.Send("The scroll's secrets are now yours. Practice your new art.");
                else p.Send("This art requires a Pirate of level 40 and an unlocked permanent scroll. If you know it already, keep your scroll.");
            }catch(Exception ex){Q.Fail(p,"pirate-item",ex);}return true;
        }
        internal static bool Enter(GameState p) {
            if(!Enabled || !S.Eligible(p) || !Q.Near(p,4272,1004) || p.Map.BaseID!=1004)return false;
            p.Entity.Teleport(Hub,114,188);
            if(p.Entity.MapID!=Hub || p.Entity.X!=114 || p.Entity.Y!=188)throw new InvalidOperationException("Pirate entry failed.");return true;
        }
    }
}
