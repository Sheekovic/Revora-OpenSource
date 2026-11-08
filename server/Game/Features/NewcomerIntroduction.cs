using System;
using System.Collections.Generic;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    // Owner-approved Revora introduction. Progress belongs to a character, not a life.
    internal static class NewcomerIntroduction {
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            Map map;if(!Kernel.Maps.TryGetValue(1002,out map) || !ConquerItemInformation.BaseInformations.ContainsKey(4030101))throw new InvalidOperationException("Introduction prerequisites missing.");
            foreach(uint id in new uint[]{8,3}){Revora.Interfaces.INpc npc;if(!map.Npcs.TryGetValue(id,out npc))throw new InvalidOperationException("Introduction service missing.");}
            foreach(uint id in new uint[]{5673,2001})if(map.Npcs.ContainsKey(id))throw new InvalidOperationException("Introduction talker identity already occupied.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_newcomer_intro(
uid bigint PRIMARY KEY,stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 4),reward_uid bigint,
CHECK((stage=4)=(reward_uid IS NOT NULL)));");}
            CityGuardSystem.SpawnTalker(5673,1002,432,397,5060,"KungfuMaster");
            CityGuardSystem.SpawnTalker(2001,1002,439,464,1740,"TCCaptain");
            for(uint id=3478;id<=3481;id++)QuestInfo.Register((QuestID)id,(QuestType)1);Enabled=true;
        }
        private static bool Eligible(GameState p){return Enabled && Q.Player(p) && p.Entity.Level>=1 && p.Entity.Level<=140 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Near(GameState p,uint npc){Revora.Interfaces.INpc actor;return Eligible(p) && p.Entity.MapID==1002 && p.Map.ID==1002 && p.Map.Npcs.TryGetValue(npc,out actor) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=6;}
        private static int Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid){using(var q=Q.Command(c,t,"SELECT stage FROM revora_newcomer_intro WHERE uid=@uid","uid",(long)uid)){var value=q.ExecuteScalar();return value==null?0:Convert.ToInt32(value);}}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p){SkillTable.LockOwner(c,t,p);}
        private static void Advance(NpgsqlConnection c,NpgsqlTransaction t,uint uid,int before,int after){if(Q.Execute(c,t,"UPDATE revora_newcomer_intro SET stage=@after WHERE uid=@uid AND stage=@before","after",after,"uid",(long)uid,"before",before)!=1)throw new InvalidOperationException("Introduction progress changed.");}
        internal static bool Protect(uint id){return Enabled && id>=3478 && id<=3481;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=3478;id<=3481;id++)seen.Add(id);if(!Q.Player(p,false))return;
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();int stage=Read(c,null,p.Entity.UID);if(stage==0 && (p.Entity.Level<1 || p.Entity.Level>140))return;
                for(uint id=3478;id<=3481;id++){int step=(int)id-3478;var status=stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:stage==step && stage>0?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null || p.ActiveNpc!=5673 && p.ActiveNpc!=2001)return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;ConquerItem reward=null;int stage;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);stage=Read(c,t,p.Entity.UID);
                    if(request.OptionID==1 && npc==5673 && stage==0){Q.Execute(c,t,"INSERT INTO revora_newcomer_intro(uid,stage) VALUES(@uid,1)","uid",(long)p.Entity.UID);stage=1;}
                    else if(request.OptionID==1 && npc==2001 && stage==3 && p.Inventory.Count<40){
                        reward=Q.New(4030101,true);Q.Insert(c,t,p.Entity.UID,reward);
                        if(Q.Execute(c,t,"UPDATE revora_newcomer_intro SET stage=4,reward_uid=@reward WHERE uid=@uid AND stage=3","reward",(long)reward.UID,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Introduction reward changed.");
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10930,"newcomer-intro-v1","3478-3481",1);stage=4;
                    }
                    if(!Near(p,npc))throw new InvalidOperationException("Introduction context changed.");t.Commit();
                }}
                if(reward!=null){Q.Install(p,new uint[0],new[]{reward});Q.Refresh(p);}
                var d=new Npcs(p);d.Text(stage==0?"New to Twin City? Learn where to keep your belongings and buy supplies.":stage==1?"Visit the warehouse keeper and open your storage.":stage==2?"Buy one supply from the pharmacist, then report to the captain.":stage==3?"You know your way around. Leave a place in your bag for this rune.":"Twin City welcomes you. Make your own name out there.");
                if(stage==0 && npc==5673 || stage==3 && npc==2001)d.Option(stage==0?"Show me the city.":"Accept the rune.",1);d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"newcomer-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void WarehouseOpened(GameState p,uint npc){
            if(npc!=8 || !Near(p,8) || !Monitor.IsEntered(p.ItemSyncRoot))return;
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);if(Read(c,t,p.Entity.UID)==1){Advance(c,t,p.Entity.UID,1,2);if(!Near(p,8))throw new InvalidOperationException("Introduction warehouse context changed.");}t.Commit();}}Refresh(p);}catch(Exception ex){Q.Fail(p,"newcomer-warehouse",ex);}
        }
        internal static bool TryBuy(GameState p,ItemUsage request){
            if(request==null || request.UID!=3 || !Near(p,3) || request.dwExtraInfo>1)return false;
            lock(p.ItemSyncRoot)try{
                ConquerItem item;ulong money;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);if(Read(c,t,p.Entity.UID)!=2)return false;
                    ShopFile.Shop shop;ConquerItemBaseInformation info;
                    if(!ShopFile.Shops.TryGetValue(3,out shop) || shop.MoneyType!=ShopFile.MoneyType.Gold || !shop.Items.Contains(request.dwParam) || !ConquerItemInformation.BaseInformations.TryGetValue(request.dwParam,out info) || info.GoldWorth==0 || p.Inventory.Count>=40 || p.Entity.Money<info.GoldWorth)return true;
                    money=p.Entity.Money-info.GoldWorth;SilverBalance.Persist(c,t,p,money);item=Q.New(request.dwParam,false);Q.Insert(c,t,p.Entity.UID,item);Advance(c,t,p.Entity.UID,2,3);
                    if(!Near(p,3))throw new InvalidOperationException("Introduction shop context changed.");t.Commit();
                }}SilverBalance.ApplyCommitted(p,money);Q.Install(p,new uint[0],new[]{item});Refresh(p);return true;
            }catch(Exception ex){Q.Fail(p,"newcomer-shop",ex);return true;}
        }
    }
}
