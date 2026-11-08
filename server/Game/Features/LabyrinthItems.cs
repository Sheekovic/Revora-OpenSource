using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class LabyrinthItems
    {
        internal sealed class Change {internal ConquerItem Item;internal ushort Before,After;internal bool Created;}
        internal static bool Material(uint id) {return id>=721533 && id<=721539;}
        internal static List<Change> Take(GameState p,uint type,int count) {
            if(!Material(type) || count<1 || count>10000)throw new ArgumentException("Invalid labyrinth material.");
            var result=new List<Change>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && i.Position==0 && i.Bound && i.Lock==0 && i.StackSize>0).OrderBy(i=>i.UID)) {
                int n=Math.Min(count,item.StackSize);result.Add(new Change{Item=item,Before=item.StackSize,After=(ushort)(item.StackSize-n)});count-=n;if(count==0)return result;
            }
            return null;
        }
        internal static Change Give(GameState p,uint type,int count) {
            if(!Material(type) || count<1 || count>10000)throw new ArgumentException("Invalid labyrinth material.");
            var item=p.Inventory.Objects.Where(i=>i.ID==type && i.Bound && i.Position==0 && i.Lock==0 && i.StackSize>0 && i.StackSize+count<=10000).OrderBy(i=>i.UID).FirstOrDefault();
            if(item!=null)return new Change{Item=item,Before=item.StackSize,After=(ushort)(item.StackSize+count)};
            if(p.Inventory.Count>=40)return null;item=Q.New(type);item.StackSize=(ushort)count;return new Change{Item=item,Created=true,After=(ushort)count};
        }
        internal static void Write(NpgsqlConnection c,NpgsqlTransaction t,GameState p,IEnumerable<Change> changes) {
            foreach(var a in changes.OrderBy(a=>a.Item.UID)) {
                var item=a.Item;
                if(a.Created){Q.Insert(c,t,p.Entity.UID,item);Q.Execute(c,t,"UPDATE items SET stacksize=@n WHERE uid=@item","n",(int)a.After,"item",(long)item.UID);continue;}
                ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.StackSize!=a.Before || !item.Bound || item.Lock!=0)throw new InvalidOperationException("Labyrinth material ownership changed.");
                string sql=a.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@n";
                if(Q.Execute(c,t,sql+" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND bound=1 AND stacksize=@before",
                    "n",(int)a.After,"item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"before",(int)a.Before)!=1)throw new InvalidOperationException("Labyrinth persisted material changed.");
            }
        }
        internal static void Install(GameState p,IEnumerable<Change> changes) {
            var all=changes.ToArray();Q.Install(p,all.Where(a=>!a.Created && a.After==0).Select(a=>a.Item.UID),all.Where(a=>a.Created).Select(a=>a.Item));
            foreach(var a in all.Where(a=>!a.Created && a.After>0)){a.Item.StackSize=a.After;a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
        }
        internal static readonly uint[] Gems={700001,700011,700021,700031,700041,700051,700061,700071};
        internal static string Exchange(GameState p,int kind) {
            if(kind<0 || kind>3)return "Choose a diamond exchange.";
            var takes=Take(p,(uint)(721533+kind),17);if(takes==null)return "Bring seventeen bound diamonds of that kind.";
            int count=kind==0?2:kind==1?4:1;
            if(p.Inventory.Count-takes.Count(a=>a.After==0)+count+(kind==3?1:0)>40)return "Make room for your reward first.";
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Write(c,t,p,takes);uint type=kind<2?1088001u:kind==2?Gems[LabyrinthQuest.Roll(Gems.Length)]:721540u;
                var rewards=Enumerable.Range(0,count).Select(i=>Q.New(type)).Concat(kind==3?new[]{QuestSoulReward.Create()}:new ConquerItem[0]).ToArray();foreach(var reward in rewards)Q.Insert(c,t,p.Entity.UID,reward);
                Guid id=Guid.NewGuid();if(Q.Execute(c,t,"INSERT INTO revora_lab_exchanges(id,uid,kind,rewards,reward_version) VALUES(@id,@uid,@kind,@rewards,1)","id",id,"uid",(long)p.Entity.UID,"kind",kind,"rewards",rewards.Select(i=>(long)i.UID).ToArray())!=1)throw new InvalidOperationException("Labyrinth exchange was not saved.");
                if(kind==3)ProgressionEvidence.Complete(c,t,p.Entity.UID,11027,"classic-labyrinth-v1",id.ToString("N"),1);
                t.Commit();Install(p,takes);Q.Install(p,new uint[0],rewards);Q.Refresh(p);return kind==3?"An Ancestor Box and a bound P7 soul. Leave one empty place beside the box before opening.":"Your reward is ready. May your next expedition be fruitful.";
            }}
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(!LabyrinthQuest.Enabled || item==null || item.ID!=721540)return false;
            if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return true;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return true;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    if(!Q.Owns(c,t,p,item.UID,721540))return true;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_lab_exchanges WHERE uid=@uid AND kind=3 AND @box=ANY(rewards))","uid",(long)p.Entity.UID,"box",(long)item.UID))
                        if(!Equals(q.ExecuteScalar(),true)){p.Send("Simon must issue this Ancestor Box before it can be opened.");return true;}
                    // Require the maximum result capacity before rolling, avoiding bag-dependent rerolls.
                    if(p.Inventory.Count>39){p.Send("Make one empty place beside your box for its possible two-item prize.");return true;}
                    int roll=LabyrinthQuest.Roll(100);uint type=roll<60?1088001u:roll<90?Gems[LabyrinthQuest.Roll(Gems.Length)]:1088000u;
                    var rewards=Enumerable.Range(0,roll<60?2:1).Select(i=>Q.New(type)).ToArray();Q.Consume(c,t,p,item.UID,721540);foreach(var reward in rewards)Q.Insert(c,t,p.Entity.UID,reward);
                    Q.Execute(c,t,"INSERT INTO revora_lab_boxes(box_uid,uid,roll,rewards) VALUES(@box,@uid,@roll,@rewards)","box",(long)item.UID,"uid",(long)p.Entity.UID,"roll",roll,"rewards",rewards.Select(i=>(long)i.UID).ToArray());
                    t.Commit();Q.Install(p,new[]{item.UID},rewards);
                }}
            }catch(Exception ex){Q.Fail(p,"labyrinth-box",ex);}return true;
        }
    }
}
