using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SubclassProgression
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly byte[] Ids={1,2,3,4,5,6,9};
        private static readonly uint[] Materials={721259,721261,711188,723087,1088001,711679,723903};
        private static readonly int[] Counts={5,10,1,20,10,1,40};
        private static readonly byte[] Levels={0,70,90,110,120,90,110,120,120,130};
        private static readonly byte[] Rebirths={0,0,0,0,0,1,1,1,2,2};
        private static readonly ushort[] OrdinaryCosts={0,300,900,1800,2700,3600,5100,6900,8700};
        private static readonly ushort[] OtherCosts={0,400,1200,2400,3600,4800,6800,9200,11600};
        private static readonly ushort[] ApothecaryCosts={0,100,200,300,400,500,1000,4000,9000};
        private sealed class Consumption {internal ConquerItem Item;internal ushort Before,After;}
        internal static bool Frame(byte[] packet) {
            if(packet==null || packet.Length<12 || packet.Length>128)return false;
            int size=BitConverter.ToUInt16(packet,0);
            return size>=12 && size<=120 && (packet.Length==size || packet.Length==size+8) && BitConverter.ToUInt16(packet,2)==2320;
        }
        internal static ushort Cost(byte id,byte level) {
            if(level<1 || level>=9 || !Ids.Contains(id))return 0;
            return id==5?ApothecaryCosts[level]:id==3?(ushort)(OrdinaryCosts[level]*2):id==1 || id==2?OrdinaryCosts[level]:OtherCosts[level];
        }
        private static bool Valid(SubClass s){return s!=null && Enrolled(s.ID,s.Level,s.Phase);}
        internal static bool Enrolled(int id,int level,int phase){
            return id>=0 && id<=255 && Ids.Contains((byte)id) && phase>=1 && phase<=level && level<=9;
        }
        internal static void RestorePerformer(GameState p) {
            SubClass performer;
            if(p==null || p.Fake || p.Entity==null || p.Spells==null || p.Entity.SubClasses==null
                || !p.Entity.SubClasses.Classes.TryGetValue(6,out performer) || !Valid(performer) || performer.ID!=6)return;
            // Preserve the existing server's learned dance set; derive it again after reconnect.
            for(ushort id=1415;id<=1423;id++)if(!p.Spells.ContainsKey(id))p.AddSpell(new Spell(true){ID=id});
        }
        private static void Evidence(NpgsqlConnection c,NpgsqlTransaction t,uint uid,byte id,byte phase) {
            if(phase==1) {
                switch(id) {
                    case 1:ProgressionEvidence.Complete(c,t,uid,10922,"subclass-v1","phase-1",1);break;
                    case 2:ProgressionEvidence.Complete(c,t,uid,11203,"subclass-v1","phase-1",1);break;
                    case 3:ProgressionEvidence.Complete(c,t,uid,10924,"subclass-v1","phase-1",1);break;
                    case 4:ProgressionEvidence.Complete(c,t,uid,10926,"subclass-v1","phase-1",1);break;
                    case 5:ProgressionEvidence.Complete(c,t,uid,10920,"subclass-v1","phase-1",1);break;
                    case 6:ProgressionEvidence.Complete(c,t,uid,10928,"subclass-v1","phase-1",1);break;
                    case 9:ProgressionEvidence.Complete(c,t,uid,11201,"subclass-v1","phase-1",1);break;
                }
            }else if(phase==9) {
                switch(id) {
                    case 1:ProgressionEvidence.Complete(c,t,uid,10923,"subclass-v1","phase-9",1);break;
                    case 2:ProgressionEvidence.Complete(c,t,uid,11204,"subclass-v1","phase-9",1);break;
                    case 3:ProgressionEvidence.Complete(c,t,uid,10925,"subclass-v1","phase-9",1);break;
                    case 4:ProgressionEvidence.Complete(c,t,uid,10927,"subclass-v1","phase-9",1);break;
                    case 5:ProgressionEvidence.Complete(c,t,uid,10921,"subclass-v1","phase-9",1);break;
                    case 6:ProgressionEvidence.Complete(c,t,uid,10929,"subclass-v1","phase-9",1);break;
                    case 9:ProgressionEvidence.Complete(c,t,uid,11202,"subclass-v1","phase-9",1);break;
                }
            }
        }
        private static bool UsableMaterial(ConquerItem item) {
            return item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious && !item.InWardrobe
                && (item.Days==0?item.TimeLeftInMinutes==0:Clock().ToLocalTime()<item.DayStamp.AddDays(item.Days));
        }
        private static List<Consumption> MaterialsFor(GameState p,byte id) {
            int index=Array.IndexOf(Ids,id),needed=Counts[index];var plan=new List<Consumption>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==Materials[index] && UsableMaterial(i)).OrderBy(i=>i.UID)) {
                int units=Math.Max(1,(int)item.StackSize),take=Math.Min(needed,units);
                plan.Add(new Consumption{Item=item,Before=item.StackSize,After=(ushort)(units-take)});needed-=take;if(needed==0)return plan;
            }
            return null;
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Consumption part) {
            if(!UsableMaterial(part.Item))throw new InvalidOperationException("Subclass material is no longer usable.");
            string sql=part.After==0
                ?"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before AND bound=@bound"
                :"UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before AND bound=@bound";
            sql+=" AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)";
            if(Q.Execute(c,t,sql,"after",(int)part.After,"item",(long)part.Item.UID,"uid",(long)p.Entity.UID,"type",(int)part.Item.ID,
                "before",(int)part.Before,"bound",part.Item.Bound?1:0,"days",part.Item.Days.ToString(CultureInfo.InvariantCulture),
                "permanent",part.Item.Days==0,"stamp",part.Item.DayStamp.ToBinary())!=1)throw new InvalidOperationException("Subclass material ownership changed.");
        }
        private static bool Change(GameState p,ushort action,byte id) {
            var state=p.Entity.SubClasses;SubClass old;state.Classes.TryGetValue(id,out old);
            if(action==9 && old!=null)return false;
            if(action!=9 && (!Valid(old) || old.ID!=id))return false;
            if(p.Entity.Level<70){p.Send("Reach level 70 before training a subclass.");return false;}
            byte level=action==9?(byte)1:old.Level,phase=action==9?(byte)1:old.Phase;
            ushort cost=0;
            if(action==2){cost=Cost(id,level);if(cost==0)return false;if(state.StudyPoints<cost){p.Send("You need "+cost+" study points for this training.");return false;}level++;}
            if(action==10){if(phase>=9 || phase>=level)return false;phase++;if(p.Entity.Level<Levels[phase] || p.Entity.Reborn<Rebirths[phase]){p.Send("P"+phase+" requires level "+Levels[phase]+" and "+Rebirths[phase]+" rebirths.");return false;}}
            var plan=action==9?MaterialsFor(p,id):new List<Consumption>();if(plan==null){p.Send("Bring the enrollment materials listed for this subclass.");return false;}
            ushort remaining=(ushort)(state.StudyPoints-cost);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                    var saved=q.ExecuteScalar();if(saved==null)throw new InvalidOperationException("Subclass player missing.");
                    if(cost>0 && Convert.ToInt32(saved)!=state.StudyPoints){p.Send("Your study balance is being saved. Try again shortly.");return false;}
                }
                if(action==9) {
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM subclasses WHERE id=@uid AND uid=@class","uid",(long)p.Entity.UID,"class",(int)id))
                        if(Convert.ToInt32(q.ExecuteScalar())!=0)throw new InvalidOperationException("Subclass enrollment requires reload.");
                    foreach(var part in plan)Consume(c,t,p,part);
                    if(Q.Execute(c,t,"INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,@class,1,1)","uid",(long)p.Entity.UID,"class",(int)id)!=1)
                        throw new InvalidOperationException("Subclass enrollment was not saved.");
                }else if(Q.Execute(c,t,"UPDATE subclasses SET level=@level,phase=@phase WHERE id=@uid AND uid=@class AND level=@oldlevel AND phase=@oldphase",
                    "uid",(long)p.Entity.UID,"class",(int)id,"level",(int)level,"phase",(int)phase,"oldlevel",(int)old.Level,"oldphase",(int)old.Phase)!=1)
                    throw new InvalidOperationException("Subclass progression changed.");
                if(cost>0 && Q.Execute(c,t,"UPDATE entities SET studypoints=@study WHERE uid=@uid","uid",(long)p.Entity.UID,"study",(int)remaining)!=1)
                    throw new InvalidOperationException("Subclass study payment was not saved.");
                if(action==9 || action==10)Evidence(c,t,p.Entity.UID,id,phase);
                t.Commit();
            }}
            state.Classes[id]=new SubClass{ID=id,Level=level,Phase=phase};if(cost>0)state.StudyPoints=remaining;
            foreach(var part in plan) {
                if(part.After==0){if(!p.Inventory.Remove(part.Item.UID,Enums.ItemUse.None,true))throw new InvalidOperationException("Subclass inventory requires reload.");}
                else {part.Item.StackSize=part.After;part.Item.Mode=Enums.ItemMode.Update;part.Item.Send(p);part.Item.Mode=Enums.ItemMode.Default;}
            }
            p.Send(new SubClassShowFull(0){ID=(ushort)(action==9?4:action==2?3:5),Class=id,Level=action==10?phase:level}.ToArray());
            if(cost>0)p.Send(new SubClassShowFull(0){ID=8,Study=remaining}.ToArray());
            Q.Refresh(p);return true;
        }
        internal static void Process(byte[] packet,GameState p) {
            if(!Frame(packet) || !Q.Player(p) || p.Entity.SubClasses==null || p.Entity.SubClasses.Classes==null)return;
            ushort action=BitConverter.ToUInt16(packet,8);byte id=packet[10];
            if(action!=0 && action!=2 && action!=6 && action!=9 && action!=10)return;
            if(action!=6 && id!=0 && !Ids.Contains(id) || action!=0 && action!=6 && id==0)return;
            lock(p.ItemSyncRoot) {
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return;
                try {
                    if(action==0) {
                        SubClass s;if(id!=0 && (!p.Entity.SubClasses.Classes.TryGetValue(id,out s) || !Valid(s) || s.ID!=id))return;
                        p.Entity.SubClass=id;p.Entity.SubClasses.Active=id;p.Entity.SubClassesActive=id;
                        p.Send(new SubClassShowFull(0){ID=1,Class=id,Level=id==0?(byte)0:p.Entity.SubClasses.Classes[id].Phase}.ToArray());
                    }else if(action!=6 && !Change(p,action,id))return;
                    RestorePerformer(p);p.Send(new SendSubClass(p.Entity).ToArray());
                }catch(Exception ex){Q.Fail(p,"subclass",ex);}
            }
        }
    }
}
