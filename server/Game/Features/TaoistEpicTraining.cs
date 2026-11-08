using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TaoistEpicTraining
    {
        private sealed class Art {
            internal readonly ushort Spell; internal readonly byte Level,Rank; internal readonly uint Scroll; internal readonly string Name;
            internal Art(ushort spell,byte level,byte rank,uint scroll,string name){Spell=spell;Level=level;Rank=rank;Scroll=scroll;Name=name;}
        }
        private static readonly Dictionary<byte,Art> arts=new Dictionary<byte,Art> {
            {61,new Art(10309,40,0,0,"Chain Bolt")},
            {208,new Art(10309,40,0,0,"Chain Bolt")},
            {210,new Art(10309,40,0,0,"Chain Bolt")},
            {160,new Art(12400,52,142,3006019,"Breaking Touch")},
            {161,new Art(12380,52,142,3006017,"Flame Lotus")},
            {162,new Art(12390,52,132,3006020,"Blessing Touch")},
            {163,new Art(12370,52,132,3006018,"Aurora Lotus")}
        };
        internal static void Initialize() {
            foreach(var art in arts.Values)if(SpellTable.GetSpell(art.Spell,0)==null || art.Scroll!=0 && !ConquerItemInformation.BaseInformations.ContainsKey(art.Scroll))throw new InvalidOperationException("Taoist training catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_taoist_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,spell integer NOT NULL,class integer NOT NULL CHECK(class BETWEEN 132 AND 135 OR class BETWEEN 142 AND 145),
level integer NOT NULL CHECK(level BETWEEN 1 AND 140),item_uid bigint NOT NULL,item_type integer NOT NULL,
stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),bound boolean NOT NULL,
created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_taoist_training_owner ON revora_taoist_training(uid);");}
        }
        private static bool Near(GameState p) {
            return TaoistEpicQuest.Enabled && p.Spells!=null && Q.Near(p,10000,1004) && p.Map.ID==1004 && p.Map.BaseID==1004
                && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);
        }
        private static bool Usable(ConquerItem i) {
            return TaoistEpicStore.Usable(i);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            Art art;
            if(p==null || p.ActiveNpc!=10000 || request==null || !arts.TryGetValue(request.OptionID,out art))return false;
            if(!TaoistEpicQuest.Enabled)return true;
            if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=10000)return true;
            lock(p.ItemSyncRoot) {
                if(!Near(p))return true;
                try {
                    var dialog=new Npcs(p);
                    if(art.Rank==0 ? !(p.Entity.Class>=132 && p.Entity.Class<=135 || p.Entity.Class>=142 && p.Entity.Class<=145) : p.Entity.Class<art.Rank || p.Entity.Class>art.Rank+3)dialog.Text("This art belongs to the other Taoist path.");
                    else if(p.Spells.ContainsKey(art.Spell))dialog.Text(art.Scroll==0?"You already know this art. Hone your skill.":"You already know this art. Keep your scroll and hone your skill.");
                    else if(p.Entity.Level<art.Level)dialog.Text("Return at level "+art.Level+" to learn "+art.Name+".");
                    else Learn(p,art,dialog);
                    dialog.Option("Farewell.",255);dialog.Send();
                }catch(Exception ex){Q.Fail(p,"taoist-training",ex);}
            }
            return true;
        }
        private static void Learn(GameState p,Art art,Npcs dialog) {
            var item=art.Scroll==0?null:p.Inventory.Objects.Where(i=>i.ID==art.Scroll && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
            if(art.Scroll!=0 && item==null){dialog.Text("Bring an unlocked "+art.Name+" scroll, and I will teach you its secret.");return;}
            if(SpellTable.GetSpell(art.Spell,0)==null)throw new InvalidOperationException("Taoist art catalog missing.");
            uint uid=item==null?0:item.UID;ushort before=item==null?(ushort)0:item.StackSize;bool bound=item!=null && item.Bound;
            var profile=CharacterProgression.Capture(p);var skill=new Spell(true){ID=art.Spell,Level=0};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);CharacterProgression.Persist(c,t,p,profile);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=1004)throw new InvalidOperationException("Training location changed.");
                if(item!=null) {
                    string debit=before<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'","item",(long)uid,"uid",(long)p.Entity.UID,"type",(int)art.Scroll,"before",(int)before,"bound",bound?1:0)!=1)
                        throw new InvalidOperationException("Training scroll ownership changed.");
                }
                SkillTable.Persist(c,t,p,skill);
                if(Q.Execute(c,t,@"INSERT INTO revora_taoist_training(id,uid,spell,class,level,item_uid,item_type,stack_before,bound)
VALUES(@id,@uid,@spell,@class,@level,@item,@type,@before,@bound)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                    "spell",(int)art.Spell,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level,"item",(long)uid,"type",(int)art.Scroll,"before",(int)before,"bound",bound)!=1)
                    throw new InvalidOperationException("Training receipt not saved.");
                ConquerItem owned;
                if(!Near(p) || p.Entity.Class!=profile.Values[7] || p.Entity.Level!=profile.Values[0] || p.Spells.ContainsKey(art.Spell)
                    || item!=null && (!p.Inventory.TryGetItem(uid,out owned) || !ReferenceEquals(owned,item) || !Usable(item)
                        || item.UID!=uid || item.ID!=art.Scroll || item.StackSize!=before || item.Bound!=bound))
                    throw new InvalidOperationException("Training session changed.");
                t.Commit();
            }}
            CharacterProgression.Committed(p,profile);
            if(item!=null) {
                if(before<=1)Q.Install(p,new[]{uid},new ConquerItem[0]);
                else{item.StackSize=(ushort)(before-1);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }
            skill.Available=true;p.Spells.Add(skill.ID,skill);skill.Send(p);Q.Refresh(p);
            dialog.Text("You have learned "+art.Name+". Practice until its secrets become clear.");
        }
    }
}
