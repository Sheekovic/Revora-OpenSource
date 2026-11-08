using System;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class GuardTraining
    {
        internal const uint Material=1072031,Npc=10000;
        private static bool Ready(GameState p) {return Q.Near(p,Npc,1004) && p.Spells!=null;}
        internal static void RecordSummon(GameState p,Entity actor)
        {
            if(p==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot)try {
                Entity registered;
                if(!Q.Player(p) || p.Map==null || actor==null || !ReferenceEquals(p.Companion,actor)
                    || !ReferenceEquals(actor.Owner,p) || actor.Hitpoints==0 || actor.MapID!=p.Entity.MapID
                    || actor.MonsterInfo==null || !p.Spells.ContainsKey(4000)
                    || !SpellTable.SpellInformations[4000].Values.Any(i=>i.Power==actor.MonsterInfo.ID)
                    || !(actor.UID<800000?p.Map.Entities.TryGetValue(actor.UID,out registered):p.Map.Companions.TryGetValue(actor.UID,out registered))
                    || !ReferenceEquals(actor,registered))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM skills WHERE entityid=@uid AND id=4000","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=1)return;
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10119,"owned-guard-summon",actor.MonsterInfo.ID.ToString(CultureInfo.InvariantCulture),1);
                    t.Commit();
                }}
                Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"guard-summon-evidence",ex);}
        }
        internal static void Learn(GameState p)
        {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p))return;
                if(p.Entity.Reborn==0){p.Send("Return after your first rebirth.");return;}
                if(p.Spells.ContainsKey(4000)){p.Send("You already know how to summon a guard. Keep your ore.");return;}
                var book=p.Inventory.Objects.Where(i=>i.ID==Material && i.Position==0 && i.Warehouse==0 && !i.InWardrobe
                    && i.Lock==0 && i.StackSize>0 && (i.Days==0 || DateTime.Now<i.DayStamp.AddDays(i.Days))).OrderBy(i=>i.UID).FirstOrDefault();
                if(book==null){p.Send("Bring me one Euxenite Ore.");return;}
                if(!SpellTable.SpellInformations.ContainsKey(4000) || !SpellTable.SpellInformations[4000].ContainsKey(0))
                    throw new InvalidOperationException("Guard skill catalog missing.");
                var skill=new Spell(true){ID=4000,Level=0};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT reborn FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())<=0)return;
                    SkillTable.Persist(c,t,p,skill);
                    string change=book.StackSize==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,change+@" WHERE uid=@item AND entityid=@uid AND id=1072031 AND position=0 AND warehouse=0
AND COALESCE(inwardrobe,0)=0 AND locked=0 AND stacksize=@stack AND bound=@bound
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)book.UID,"uid",(long)p.Entity.UID,"stack",(int)book.StackSize,"bound",book.Bound?1:0,
                        "days",book.Days.ToString(CultureInfo.InvariantCulture),"permanent",book.Days==0,"stamp",book.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Guard ore ownership changed.");
                    if(!Ready(p) || p.Entity.Reborn==0 || p.Spells.ContainsKey(4000))throw new InvalidOperationException("Guard student changed.");
                    t.Commit();
                }}
                if(book.StackSize==1)Q.Install(p,new[]{book.UID},new ConquerItem[0]);
                else {book.StackSize--;book.Mode=Enums.ItemMode.Update;book.Send(p);}
                skill.Available=true;
                if(!p.AddSpell(skill))throw new InvalidOperationException("Committed guard skill requires reload.");
                p.Send("Your guard will answer when you call.");
            }catch(Exception ex){Q.Fail(p,"guard-training",ex);}
        }
    }
}
