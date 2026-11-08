using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MonkTraining
    {
        private sealed class Art {
            internal readonly ushort Spell; internal readonly byte Level; internal readonly uint Scroll; internal readonly string Name;
            internal Art(ushort spell,byte level,uint scroll,string name){Spell=spell;Level=level;Scroll=scroll;Name=name;}
        }
        // Logical 6711 spell IDs and level-zero requirements, matched to the installed scroll descriptions.
        private static readonly Dictionary<byte,Art> arts=new Dictionary<byte,Art> {
            {13,new Art(10395,20,0,"Tyrant Aura")},
            {14,new Art(10410,20,0,"Fend Aura")},
            {15,new Art(10420,100,0,"Metal Aura")},
            {16,new Art(10421,100,0,"Wood Aura")},
            {17,new Art(10422,100,0,"Water Aura")},
            {18,new Art(10423,100,0,"Fire Aura")},
            {19,new Art(10424,100,0,"Earth Aura")},
            {23,new Art(12550,40,3007568,"Infernal Echo")},
            {24,new Art(12560,50,3007566,"Grace of Heaven")},
            {25,new Art(12570,15,3007567,"Wrath of the Emperor")}
        };
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_monk_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,spell integer NOT NULL,class integer NOT NULL CHECK(class BETWEEN 60 AND 65),
level integer NOT NULL CHECK(level BETWEEN 1 AND 140),item_uid bigint NOT NULL,item_type integer NOT NULL,
stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),bound boolean NOT NULL,
created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_monk_training_owner ON revora_monk_training(uid);");}
        }
        private static bool Near(GameState p) {
            return Q.Near(p,4271,1004) && p.Map.ID==1004 && p.Map.BaseID==1004
                && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);
        }
        private static bool Usable(ConquerItem i) {
            return i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            Art art;
            if(p==null || p.ActiveNpc!=4271 || request==null || !arts.TryGetValue(request.OptionID,out art))return false;
            if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=4271)return true;
            lock(p.ItemSyncRoot) {
                if(!Near(p))return true;
                try {
                    var dialog=new Npcs(p);
                    if(p.Entity.Class<60 || p.Entity.Class>65)dialog.Text("These arts belong to the Monk. Seek your own master.");
                    else if(p.Spells.ContainsKey(art.Spell))dialog.Text("You already know this art. Keep honing your skill.");
                    else if(p.Entity.Level<art.Level)dialog.Text("Return at level "+art.Level+" to learn "+art.Name+".");
                    else Learn(p,art,dialog);
                    dialog.Option("Farewell.",255);dialog.Send();
                }catch(Exception ex){Q.Fail(p,"monk-training",ex);}
            }
            return true;
        }
        private static void Learn(GameState p,Art art,Npcs dialog) {
            var item=art.Scroll==0?null:p.Inventory.Objects.Where(i=>i.ID==art.Scroll && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
            if(art.Scroll!=0 && item==null){dialog.Text("Bring an unlocked "+art.Name+" Scroll, and I will teach you its secret.");return;}
            if(SpellTable.GetSpell(art.Spell,0)==null)throw new InvalidOperationException("Monk art catalog missing.");
            uint uid=item==null?0:item.UID;ushort before=item==null?(ushort)0:item.StackSize;bool bound=item!=null && item.Bound;
            var profile=CharacterProgression.Capture(p);var skill=new Spell(true){ID=art.Spell,Level=0};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);CharacterProgression.Persist(c,t,p,profile);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=1004)throw new InvalidOperationException("Training location changed.");
                if(item!=null)
                {
                    string debit=before<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'","item",(long)uid,"uid",(long)p.Entity.UID,"type",(int)art.Scroll,"before",(int)before,"bound",bound?1:0)!=1)
                        throw new InvalidOperationException("Training scroll ownership changed.");
                }
                SkillTable.Persist(c,t,p,skill);
                if(Q.Execute(c,t,@"INSERT INTO revora_monk_training(id,uid,spell,class,level,item_uid,item_type,stack_before,bound)
VALUES(@id,@uid,@spell,@class,@level,@item,@type,@before,@bound)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                    "spell",(int)art.Spell,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level,"item",(long)uid,"type",(int)art.Scroll,"before",(int)before,"bound",bound)!=1)
                    throw new InvalidOperationException("Training receipt not saved.");
                ConquerItem owned;
                if(!Near(p) || p.Entity.Class!=profile.Values[7] || p.Entity.Level!=profile.Values[0] || p.Spells.ContainsKey(art.Spell)
                    || (item!=null && (!p.Inventory.TryGetItem(uid,out owned) || !ReferenceEquals(owned,item) || !Usable(item)
                        || item.UID!=uid || item.ID!=art.Scroll || item.StackSize!=before || item.Bound!=bound)))
                    throw new InvalidOperationException("Training session changed.");
                t.Commit();
            }}
            CharacterProgression.Committed(p,profile);
            if(item!=null)
            {
                if(before<=1)Q.Install(p,new[]{uid},new ConquerItem[0]);
                else{item.StackSize=(ushort)(before-1);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }
            skill.Available=true;p.Spells.Add(skill.ID,skill);skill.Send(p);Q.Refresh(p);
            dialog.Text("You have learned "+art.Name+". Let stillness guide your strength.");
        }
    }
}
