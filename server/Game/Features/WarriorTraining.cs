using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WarriorTraining
    {
        private sealed class Art
        {
            internal readonly ushort Spell; internal readonly byte Level, Rank; internal readonly string Name;
            internal Art(ushort spell, byte level, string name, byte rank = 20) { Spell = spell; Level = level; Name = name; Rank = rank; }
        }
        private static readonly Dictionary<byte, Art> arts = new Dictionary<byte, Art> {
            {6,new Art(12770,60,"Pounce")}, {9,new Art(10470,40,"Shield Block")}, {10,new Art(11160,70,"Defensive Stance",23)}, {11,new Art(11200,40,"Magic Defender",22)},
            {20,new Art(12660,15,"Twist of War")}, {21,new Art(12670,15,"Scar of Earth")}, {22,new Art(12680,60,"Backfire")},
            {23,new Art(12690,15,"Wave of Blood")}, {24,new Art(12700,15,"Maniac Dance")}
        };
        private static readonly Art[] xp = { new Art(1025,3,"Superman"),new Art(1015,15,"Accuracy"),new Art(1020,15,"Shield"),new Art(1040,40,"Roar") };
        internal static void Initialize()
        {
            foreach (var art in arts.Values.Concat(xp)) if (SpellTable.GetSpell(art.Spell,0) == null) throw new InvalidOperationException("Warrior training catalog missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_warrior_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,spell integer NOT NULL,class integer NOT NULL CHECK(class BETWEEN 20 AND 25),
level integer NOT NULL CHECK(level BETWEEN 1 AND 140),created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_warrior_training_owner ON revora_warrior_training(uid);"); }
        }
        private static bool Near(GameState p)
        {
            return Q.Near(p,10001,1004) && p.Map.ID == 1004 && p.Map.BaseID == 1004 && p.Entity.PokerTable == 0 && CharacterProgression.Managed(p) && p.Spells != null;
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (p == null || p.ActiveNpc != 10001 || request == null) return false;
            byte option = request.OptionID;
            if (option != 0 && option != 2 && option != 5 && option != 10 && option != 12 && !arts.ContainsKey(option)) return false;
            if (p.ItemSyncRoot == null || request.NpcID != 0 && request.NpcID != 10001) return true;
            lock (p.ItemSyncRoot) try
            {
                if (!Near(p)) return true;
                var d = new Npcs(p); d.Avatar(8);
                if (p.Entity.Class < 20 || p.Entity.Class > 25) d.Text("Only a Warrior may learn these arts. Seek your own master.");
                else if (option == 0) {
                    d.Text("A steady hand and an iron will. What will you train today?"); d.Option("Promote me.",1); d.Option("Shield and XP arts.",2); d.Option("Fist arts.",12);
                    if (WarriorEpicQuest.Enabled) d.Option("The ancient fists.",86);
                } else if (option == 2) {
                    d.Text("Choose your art. A shield is needed to use the shield arts."); d.Option("XP arts.",5); d.Option("Pounce (60).",6); d.Option("Shield Block (40).",9); d.Option("Defensive Stance (70).",10); d.Option("Magic Defender (40).",11);
                } else if (option == 12) {
                    d.Text("These arts belong to the paired fists. Choose your lesson.");
                    foreach (var entry in arts.Where(a => a.Key >= 20).OrderBy(a => a.Key)) d.Option(entry.Value.Name+" ("+entry.Value.Level+").",entry.Key);
                }
                else if (option == 5) Learn(p,xp,d);
                else {
                    var art = arts[option];
                    if (p.Entity.Class < art.Rank) d.Text("Earn your level "+(art.Rank == 23 ? 70 : 40)+" Warrior promotion before studying this art.");
                    else if (p.Entity.Level < art.Level) d.Text("Return at level "+art.Level+" to learn "+art.Name+".");
                    else Learn(p,new[]{art},d);
                }
                d.Option("Farewell.",255); d.Send();
            }
            catch (Exception ex) { Q.Fail(p,"warrior-training",ex); }
            return true;
        }
        private static void Learn(GameState p, IEnumerable<Art> offered, Npcs dialog)
        {
            var ready = offered.Where(a => p.Entity.Level >= a.Level && p.Entity.Class >= a.Rank && !p.Spells.ContainsKey(a.Spell)).ToArray();
            if (ready.Length == 0) { dialog.Text("No new lesson awaits you yet. Practice your arts and return stronger."); return; }
            var skills = ready.Select(a => new Spell(true){ID=a.Spell,Level=0}).ToArray(); var profile = CharacterProgression.Capture(p);
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p); CharacterProgression.Persist(c,t,p,profile);
                using (var q = Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 1004) throw new InvalidOperationException("Warrior training location changed.");
                foreach (var skill in skills) {
                    SkillTable.Persist(c,t,p,skill);
                    if (Q.Execute(c,t,"INSERT INTO revora_warrior_training(id,uid,spell,class,level) VALUES(@id,@uid,@spell,@class,@level)",
                        "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"spell",(int)skill.ID,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level) != 1)
                        throw new InvalidOperationException("Warrior training receipt missing.");
                }
                if (!Near(p) || p.Entity.Class != profile.Values[7] || p.Entity.Level != profile.Values[0] || skills.Any(s => p.Spells.ContainsKey(s.ID)))
                    throw new InvalidOperationException("Warrior training owner changed."); t.Commit();
            }}
            CharacterProgression.Committed(p,profile);
            foreach (var skill in skills) { skill.Available=true; p.Spells.Add(skill.ID,skill); skill.Send(p); }
            dialog.Text("You have learned "+string.Join(", ",ready.Select(a => a.Name))+". Train with discipline.");
        }
    }
}
