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
using R=Revora.Game.Features.MonkEpicRules;
using S=Revora.Game.Features.MonkEpicStore;
namespace Revora.Game.Features
{
    internal static class MonkEpicQuest
    {
        private static readonly ConditionalWeakTable<GameState,S.Offer> offers=new ConditionalWeakTable<GameState,S.Offer>();
        internal static bool Enabled {get;private set;}
        internal static bool Handles(uint npc){return Enabled && (NinjaFlowerHunt.Handles(npc) || npc>=199483 && npc<=199487);}
        private static void Terrain(){
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","3983.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!=R.TerrainHash)throw new InvalidOperationException("Monk trial terrain changed.");
        }
        internal static void PrepareMaps(){
            Terrain();MapsTable.MapInformation info;
            if(MapsTable.MapInformations.TryGetValue(R.Map,out info)){
                if(info.ID!=R.Map || info.BaseID!=R.Map || info.Owner!=0)throw new InvalidOperationException("Monk trial map identity conflicts.");
            }else MapsTable.MapInformations.Add(R.Map,new MapsTable.MapInformation{ID=R.Map,BaseID=R.Map,Status=8200});
        }
        internal static void Initialize(){
            Terrain();Map map;
            if(!Kernel.Maps.TryGetValue(R.Map,out map) || map.ID!=R.Map || map.BaseID!=R.Map || map.Floor==null)throw new InvalidOperationException("Monk trial map missing.");
            uint[][] sites={new uint[]{199483,86,68,50970},new uint[]{199484,79,81,38420},new uint[]{199485,62,76,38430},new uint[]{199486,60,55,38440},new uint[]{199487,54,63,38450}};
            string[] names={"GoldenCicada","FortuneArhat","AltarCleanser","VictoryBuddha","WhiteDragon"};
            foreach(var site in sites)foreach(var world in Kernel.Maps.Values){
                INpc found;if(world.Npcs.TryGetValue(site[0],out found) && (world.ID!=R.Map || found.MapID!=R.Map || found.X!=site[1] || found.Y!=site[2] || found.Mesh!=site[3] || found.Type!=Enums.NpcType.Talker))
                    throw new InvalidOperationException("Monk trial NPC identity conflicts.");
            }
            using(var c=DataHolder.MySqlConnection){c.Open();foreach(var site in sites){
                using(var q=Q.Command(c,null,"SELECT mapid,cellx,celly,lookface,type FROM npcs WHERE id=@id","id",(long)site[0]))using(var r=q.ExecuteReader())
                    if(r.Read() && (Convert.ToInt32(r[0])!=R.Map || Convert.ToInt32(r[1])!=site[1] || Convert.ToInt32(r[2])!=site[2] || Convert.ToInt32(r[3])!=site[3] || Convert.ToInt32(r[4])!=(int)Enums.NpcType.Talker))
                        throw new InvalidOperationException("Saved Monk trial NPC identity conflicts.");
            }}
            foreach(uint id in new uint[]{R.RedFlower,R.WhiteFlower,R.Gem,R.Necklace,R.Heart,R.Incense,3007566,3007567,3007568})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Monk quest catalog missing.");
            S.Initialize();MonkEpicJournal.Initialize();MonkSutta.Initialize();
            for(int i=0;i<sites.Length;i++)if(!map.Npcs.ContainsKey(sites[i][0]))map.AddNpc(new NpcSpawn{UID=sites[i][0],MapID=R.Map,X=(ushort)sites[i][1],Y=(ushort)sites[i][2],Mesh=(ushort)sites[i][3],Type=Enums.NpcType.Talker,Name=names[i]});
            if(!map.Floor[87,71,MapObjectType.Player])throw new InvalidOperationException("Monk trial landing blocked.");
            Enabled=true;
        }
        private static bool Near(GameState p,uint npc){ushort map=npc==4271?(ushort)1004:R.Map;return Q.Near(p,npc,map) && p.Map.ID==map && p.Map.BaseID==map && p.Entity.PokerTable==0;}
        private static void Move(GameState p,bool home){
            ushort map=home?(ushort)1004:R.Map,x=home?(ushort)78:(ushort)87,y=home?(ushort)42:(ushort)71;
            if(!Kernel.Maps[map].Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Monk passage blocked.");
            p.Entity.Teleport(map,x,y);
            if(p.Entity.MapID!=map || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Monk passage failed.");
        }
        private static void Prepare(GameState p,S.Kind kind,byte choice,Npcs d){
            offers.Remove(p);string error;var o=S.Prepare(p,kind,p.ActiveNpc,choice,out error);
            if(o==null){d.Text(error);return;}offers.Add(p,o);
            switch(kind){
                case S.Kind.Flowers:d.Text("Offer three Red and three White Flowers. There is a one-in-two chance of a Spiritual Gem.");break;
                case S.Kind.Altar:d.Text("Offer one Spiritual Gem. There is a one-in-two chance of a necklace. Failure grants one incense.");break;
                case S.Kind.Victory:d.Text("Offer one necklace. There is a one-in-two chance of a Boundless Heart. Failure grants three incense and a Gem for another Altar trial.");break;
                case S.Kind.Scroll:d.Text("Trade ten Rosewood Incense for this scroll?");break;
                case S.Kind.Awaken:d.Text("Offer one Boundless Heart to awaken these Prayer Beads? Their quality and enhancements will remain. You also receive a bound P7 soul.");break;
            }
            if(o.Bound && (kind==S.Kind.Scroll || kind==S.Kind.Awaken))d.Text("Your bound offering binds the result to you.");
            d.Option("Proceed.",20);
        }
        private static void Confirm(GameState p,Npcs d){
            S.Offer o;if(!offers.TryGetValue(p,out o)){d.Text("Let us review the offering first.");return;}offers.Remove(p);
            if(o.Npc!=p.ActiveNpc || S.Clock()>=o.Until || o.Cache!=FirstRebirth.Cache(p) || o.BeforeCp!=p.Entity.ConquerPoints || o.Vip!=p.Entity.VIPLevel){d.Text("Let us review your offering again.");return;}
            var result=S.Commit(p,o,()=>Near(p,o.Npc));
            d.Text(o.Kind==S.Kind.Awaken?"Your Prayer Beads have awakened. A bound P7 soul is yours.":result.Count==0?"The trial reveals no gem. Return with another offering."
                :result.RetryGem?"Take this incense and Gem. Seek the Altar Cleanser again.":result.Product==R.Incense?"Take this incense. Seek another Gem from Fortune Arhat.":"Your offering is accepted. The reward is in your bag.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest req){
            if(Enabled && NinjaFlowerHunt.HandleNpc(p,req))return true;
            if(!Enabled || p==null || req==null || !(p.ActiveNpc!=3604 && Handles(p.ActiveNpc) || p.ActiveNpc==4271 && (req.OptionID==86 || req.OptionID==88)))return false;
            if(p.ItemSyncRoot==null || req.NpcID!=0 && req.NpcID!=p.ActiveNpc)return true;
            lock(p.ItemSyncRoot){
                uint npc=p.ActiveNpc;if(!Near(p,npc))return true;
                try{
                    var d=new Npcs(p);byte option=req.OptionID;
                    if(option==0 || option==255)offers.Remove(p);if(option==255)return true;
                    if(option==254){offers.Remove(p);Move(p,true);return true;}
                    if(!S.Eligible(p))d.Text("These trials await second-reborn Monks and Saints of level 70.");
                    else if(npc==4271){offers.Remove(p);if(option==88)MonkSutta.Give(p);else Move(p,false);return true;}
                    else if(option==20)Confirm(p,d);
                    else if(npc==199483){
                        if(option==4 || option==5)Prepare(p,S.Kind.Awaken,option,d);
                        else if(option==0){d.Text("Seek Fortune Arhat, then the Altar Cleanser and Victory Buddha. Bring me a Boundless Heart to awaken your Prayer Beads.");d.Option("Awaken my right beads.",4);d.Option("Awaken my left beads.",5);}
                    }else if(npc==199487){
                        if(option>=1 && option<=3)Prepare(p,S.Kind.Scroll,option,d);
                        else if(option==0){d.Text("Even a failed trial leaves wisdom. Ten Rosewood Incense will buy a sacred scroll.");d.Option("Infernal Echo.",1);d.Option("Grace of Heaven.",2);d.Option("Wrath of the Emperor.",3);}
                    }else if(option==1)Prepare(p,npc==199484?S.Kind.Flowers:npc==199485?S.Kind.Altar:S.Kind.Victory,0,d);
                    else if(option==0){
                        d.Text(npc==199484?"Bring three Red and three White Flowers from the Abyss. Test your fortune for a Spiritual Gem.":npc==199485?"Bring a Spiritual Gem. Still your thoughts and seek Buddha's Necklace.":"Bring Buddha's Necklace. Seek the Boundless Heart within.");d.Option("Review my offering.",1);
                    }
                    d.Option("Return to Head Abbot.",254);d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){offers.Remove(p);Q.Fail(p,"monk-epic",ex);}
            }return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=R.Heart)return false;if(!Enabled || p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot){
                ConquerItem owned;if(!S.Eligible(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !S.Usable(item))return true;
                try{string error;var o=S.Prepare(p,S.Kind.Awaken,0,0,out error,item.UID);if(o==null){p.Send(error);return true;}
                    var result=S.Commit(p,o,()=>S.Eligible(p));if(!result.Duplicate)p.Send("Your Prayer Beads have awakened. A bound P7 soul is yours.");
                }catch(Exception ex){Q.Fail(p,"monk-epic-heart",ex);}
            }return true;
        }
    }
}
