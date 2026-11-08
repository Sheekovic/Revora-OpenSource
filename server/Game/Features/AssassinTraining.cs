using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AssassinTraining
    {
        private static readonly ushort[] arts = { 11590,11600,11620,11610,11650,11660,11670 };
        private static readonly byte[] levels = { 50,100,40,40,70,40,90 };
        private static readonly string[] names = { "Kinetic Spark", "Dagger Storm", "Path of Shadow", "Blade Flurry", "Blistering Wave", "Mortal Wound", "Spirit Focus" };
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_assassin_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,spell integer NOT NULL,class integer NOT NULL CHECK(class BETWEEN 42 AND 45),
level integer NOT NULL CHECK(level BETWEEN 40 AND 140),created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_assassin_training_owner ON revora_assassin_training(uid);");}
        }
        private static bool Near(GameState p) {
            return Q.Near(p,400,1004) && p.Map.ID==1004 && p.Map.BaseID==1004
                && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.ActiveNpc!=400 || request==null || request.OptionID!=49 && (request.OptionID<12 || request.OptionID>18))return false;
            if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=400)return true;
            lock(p.ItemSyncRoot) {
                if(!Near(p))return true;
                try {
                    var dialog=new Npcs(p);
                    if(p.Entity.Class<42 || p.Entity.Class>45 || p.Entity.Level<40)
                        dialog.Text("Earn your level 40 Archer promotion, and I will teach you the hidden arts.");
                    else if(request.OptionID==49) {
                        dialog.Text("A steady hand serves both bow and blade. Which art will you study?");
                        for(int i=0;i<arts.Length;i++)dialog.Option(names[i]+" ("+levels[i]+").",(byte)(12+i));
                    } else Learn(p,request.OptionID-12,dialog);
                    dialog.Option("Farewell.",255);dialog.Send();
                } catch(Exception ex){Q.Fail(p,"assassin-training",ex);}
            }
            return true;
        }
        private static void Learn(GameState p,int index,Npcs dialog) {
            if(p.Entity.Level<levels[index]){dialog.Text("Return at level "+levels[index]+" to learn "+names[index]+".");return;}
            ushort id=arts[index];
            // Repeated requests must never downgrade an already trained art or its soul.
            if(p.Spells.ContainsKey(id)){dialog.Text("You already know this art. Practice it well.");return;}
            if(SpellTable.GetSpell(id,0)==null)throw new InvalidOperationException("Assassin art catalog missing.");
            var skill=new Spell(true){ID=id,Level=0};
            var profile=CharacterProgression.Capture(p);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);
                CharacterProgression.Persist(c,t,p,profile);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=1004)throw new InvalidOperationException("Training location changed.");
                SkillTable.Persist(c,t,p,skill);
                if(Q.Execute(c,t,@"INSERT INTO revora_assassin_training(id,uid,spell,class,level) VALUES(@id,@uid,@spell,@class,@level)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"spell",(int)id,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level)!=1)
                    throw new InvalidOperationException("Training receipt not saved.");
                if(!Near(p) || p.Entity.Class!=profile.Values[7] || p.Entity.Level!=profile.Values[0] || p.Spells.ContainsKey(id))
                    throw new InvalidOperationException("Training session changed.");
                t.Commit();
            }}
            CharacterProgression.Committed(p,profile);
            skill.Available=true;p.Spells.Add(id,skill);skill.Send(p);
            dialog.Text("You have learned "+names[index]+". Let your blades speak for you.");
        }
    }
}
