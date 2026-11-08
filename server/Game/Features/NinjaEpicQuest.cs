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
using R=Revora.Game.Features.NinjaEpicRules;
using S=Revora.Game.Features.NinjaEpicStore;

namespace Revora.Game.Features
{
    internal static class NinjaEpicQuest
    {
        private static readonly ConditionalWeakTable<GameState,S.Offer> offers=new ConditionalWeakTable<GameState,S.Offer>();
        internal static bool Enabled {get;private set;}
        internal static bool Handles(uint npc){return Enabled && (NinjaFlowerHunt.Handles(npc) || npc!=4720 && R.MapForNpc(npc)!=0);}
        internal static void Initialize() {
            // Validate the existing actors and actual runtime floor before enabling the quest.
            string[] hashes={"28717B072EF67478060DDC4F3A5ACB1547FA9DB67DDACB09C1173F985DEB7CD6","88AE0BB59A5F64D1594366C7268DEA566EDF28B48E05EADC0D66147E9F7C3D19","DBBC55C34419080392174E2601585F42040442D828E0FBEC94690A94C757C636"};
            for(ushort id=3849;id<=3851;id++) {
                Map map;if(!Kernel.Maps.TryGetValue(id,out map) || map.ID!=id || map.BaseID!=id || map.Floor==null)throw new InvalidOperationException("Ninja trial map missing.");
                var raw=File.ReadAllBytes(Path.Combine(Constants.DMapsPath,"maps",id+".map"));
                using(var sha=SHA256.Create())if(System.BitConverter.ToString(sha.ComputeHash(raw)).Replace("-","")!=hashes[id-3849])throw new InvalidOperationException("Ninja trial terrain changed.");
            }
            uint[][] sites={new uint[]{199445,3849,28,33},new uint[]{199464,3849,39,37},new uint[]{199546,3849,28,40},
                new uint[]{199444,3850,22,34},new uint[]{199465,3850,33,36},new uint[]{199544,3850,24,41},
                new uint[]{199446,3851,26,29},new uint[]{199466,3851,35,30},new uint[]{199545,3851,27,34}};
            foreach(var site in sites) {
                INpc actor;
                if(!Kernel.Maps[(ushort)site[1]].Npcs.TryGetValue(site[0],out actor) || actor.MapID!=site[1] || actor.Type!=Enums.NpcType.Talker
                    || actor.X!=site[2] || actor.Y!=site[3])throw new InvalidOperationException("Ninja trial actor differs from reviewed world.");
            }
            for(ushort id=3849;id<=3851;id++) {
                var point=R.Landing(id);if(!Kernel.Maps[id].Floor[point[0],point[1],MapObjectType.Player])throw new InvalidOperationException("Ninja landing is blocked.");
            }
            S.Initialize();NinjaFlowerHunt.Initialize();
            Enabled=true;
        }
        private static bool Near(GameState p,uint npc) {
            ushort map=R.MapForNpc(npc);
            return map!=0 && Q.Near(p,npc,map) && p.Map.ID==map && p.Map.BaseID==map && p.Map.Npcs[npc].MapID==map && p.Entity.PokerTable==0;
        }
        private static void Move(GameState p,ushort map) {
            ushort[] point=map==1004?new ushort[]{38,42}:R.Landing(map);Map target;
            if(!Kernel.Maps.TryGetValue(map,out target) || target.BaseID!=map || target.Floor[point[0],point[1],MapObjectType.InvalidCast])
                throw new InvalidOperationException("Ninja passage is not walkable.");
            p.Entity.Teleport(map,point[0],point[1]);
            if(p.Entity.MapID!=map || p.Entity.X!=point[0] || p.Entity.Y!=point[1])throw new InvalidOperationException("Ninja passage requires resume.");
        }
        private static void ResumeReturn(GameState p) {
            Guid operation=Guid.Empty;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT id FROM revora_ninja_epic_operations
WHERE uid=@uid AND return_map=3849 AND returned_at IS NULL ORDER BY created_at DESC LIMIT 1","uid",(long)p.Entity.UID)){
                var found=q.ExecuteScalar();if(found!=null)operation=(Guid)found;}}
            if(operation==Guid.Empty)return;
            if(p.Entity.MapID==3850 || p.Entity.MapID==3851)Move(p,3849);
            // Already leaving the trial by another legal route also resolves the return.
            using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,@"UPDATE revora_ninja_epic_operations SET returned_at=@now
WHERE uid=@uid AND return_map=3849 AND returned_at IS NULL","uid",(long)p.Entity.UID,"now",S.Clock())<1)
                throw new InvalidOperationException("Ninja return acknowledgement was not saved.");}
        }
        internal static void OnLogin(GameState p) {
            if(!Enabled || p==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot){if(!Q.Player(p))return;try{ResumeReturn(p);}catch(Exception ex){Q.Fail(p,"ninja-epic-return",ex);}}
        }
        private static void Prepare(GameState p,S.Kind kind,byte choice,Npcs d) {
            offers.Remove(p);string error;var o=S.Prepare(p,kind,p.ActiveNpc,choice,out error);
            if(o==null){d.Text(error);return;}offers.Add(p,o);
            switch(kind){case S.Kind.Single:d.Text("One attempt costs 27 CPs. The seal answers one time in ten.");break;
                case S.Kind.Bulk:d.Text("Spend 270 CPs for ten attempts. Each has a one-in-ten chance.");break;
                case S.Kind.Flowers:d.Text("Offer four Red and four White Flowers for one attempt. The seal answers one time in ten.");break;
                case S.Kind.Elemental:d.Text("Offer one YinYang Seal. Win a jade, or return with one Stardust. The chance is one in ten.");break;
                case S.Kind.Wheel:d.Text("Offer one Elemental Jade. Win Life's Eye, or return with five Stardust. The chance is one in ten.");break;
                case S.Kind.Scroll:d.Text("Trade ten Stardust for this scroll?");break;
                case S.Kind.Awaken:d.Text("Spend one Life's Eye to awaken this katana? Its strength and quality will remain. You also receive a bound P7 soul.");break;}
            if(o.Bound && (kind==S.Kind.Scroll || kind==S.Kind.Awaken))d.Text("Your bound offering binds the result to you.");
            d.Option("Proceed.",20);
        }
        private static void Confirm(GameState p,Npcs d) {
            S.Offer o;if(!offers.TryGetValue(p,out o)){d.Text("Let us review the offering first.");return;}offers.Remove(p);
            if(o.Npc!=p.ActiveNpc || S.Clock()>=o.Until || o.BeforeCp!=p.Entity.ConquerPoints || o.Vip!=p.Entity.VIPLevel
                || o.Cache!=FirstRebirth.Cache(p)){d.Text("Let us review your offering again.");return;}
            var result=S.Commit(p,o,()=>Near(p,o.Npc));
            if(result.ReturnMap!=0)ResumeReturn(p);
            d.Text(o.Kind==S.Kind.Awaken?"Your katana has awakened. A bound P7 soul is yours.":result.Count==0?"The seal remains silent. Your attempt is complete."
                :result.Product==R.Dust?"The trial yields Stardust. Return when you are ready.":"The offering is accepted. Your reward is in your bag.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest req) {
            if(Enabled && NinjaFlowerHunt.HandleNpc(p,req))return true;
            if(!Enabled || p==null || req==null || !(p.ActiveNpc!=3604 && Handles(p.ActiveNpc) || p.ActiveNpc==4720 && req.OptionID==86))return false;
            if(p.ItemSyncRoot==null || req.NpcID!=0 && req.NpcID!=p.ActiveNpc)return true;
            lock(p.ItemSyncRoot) {
                uint npc=p.ActiveNpc;if(!Near(p,npc))return true;
                try {
                    var d=new Npcs(p);byte option=req.OptionID;
                    if(option==0 || option==255)offers.Remove(p);
                    if(option==255)return true;
                    if(option==254 || (npc==199444 || npc==199445 || npc==199446) && option==1){offers.Remove(p);Move(p,1004);return true;}
                    if(!S.Eligible(p)){d.Text("These trials await second-reborn Ninjas of level 70.");d.Option("Return to Wind Sage.",254);}
                    else if(npc==4720){offers.Remove(p);Move(p,3849);return true;}
                    else if(option==20)Confirm(p,d);
                    else if(npc==199464) {
                        if(option==1)Prepare(p,S.Kind.Single,0,d);else if(option==2)Prepare(p,S.Kind.Bulk,0,d);else if(option==4)Prepare(p,S.Kind.Flowers,0,d);
                        else if(option==3){offers.Remove(p);if(S.Has(p,R.Seal)){Move(p,3850);return true;}d.Text("Bring a YinYang Seal to enter the next trial.");}
                        else if(option==0){d.Text("Balance the opposing forces. The seal may answer your offering.");d.Option("Offer four of each flower.",4);d.Option("One attempt: 27 CPs.",1);d.Option("Ten attempts: 270 CPs.",2);d.Option("Enter with my seal.",3);}
                    } else if(npc==199465) {
                        if(option==1)Prepare(p,S.Kind.Elemental,0,d);
                        else if(option==3){offers.Remove(p);if(S.Has(p,R.Jade)){Move(p,3851);return true;}d.Text("Bring an Elemental Jade to enter the final trial.");}
                        else if(option==0){d.Text("Offer your seal and awaken the Elemental Guard.");d.Option("Offer one seal.",1);d.Option("Enter with my jade.",3);}
                    } else if(npc==199466) {
                        if(option==1)Prepare(p,S.Kind.Wheel,0,d);else if(option==0){d.Text("The wheel may reveal Life's Eye. Will you test your fate?");d.Option("Offer one jade.",1);}
                    } else if(npc==199544 || npc==199545 || npc==199546) {
                        if(option>=1 && option<=3)Prepare(p,S.Kind.Scroll,option,d);
                        else if(option==0){d.Text("Even a failed trial leaves a gift. I trade ten Stardust for a scroll.");d.Option("Shadow Scroll.",1);d.Option("Twilight Scroll.",2);d.Option("Fatal Scroll.",3);}
                    } else {
                        if(option==3 || option==4 || option==5)Prepare(p,S.Kind.Awaken,option==3?(byte)0:option,d);
                        else if(option==0 || option==2){d.Text("The three trials reveal Life's Eye. Bring it with your katana, and I will awaken the blade.");d.Option("Awaken my right katana.",4);d.Option("Awaken my left katana.",5);d.Option("Return to Wind Sage.",1);}
                    }
                    if(npc!=4720)d.Option("Return to Wind Sage.",254);d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){offers.Remove(p);Q.Fail(p,"ninja-epic",ex);}
            }return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=R.Eye)return false;
            if(!Enabled || p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                ConquerItem owned;if(!S.Eligible(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || !S.Usable(item))return true;
                try{string error;var o=S.Prepare(p,S.Kind.Awaken,0,0,out error,item.UID);if(o==null){p.Send(error);return true;}
                    var result=S.Commit(p,o,()=>S.Eligible(p));if(!result.Duplicate)p.Send("Your katana has awakened. A bound P7 soul is yours.");
                }catch(Exception ex){Q.Fail(p,"ninja-epic-eye",ex);}
            }return true;
        }
    }
}
