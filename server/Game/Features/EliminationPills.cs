using System;
using System.Runtime.CompilerServices;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.EliminationStore;

namespace Revora.Game.Features
{
    internal static class EliminationPills
    {
        private sealed class Life {internal Time32 Stamp;internal bool Attempted;}
        private static readonly ConditionalWeakTable<Entity,Life> lives=new ConditionalWeakTable<Entity,Life>();
        private static bool enabled;
        internal static void Initialize() {
            MonsterInformation source;
            if(!MonsterInformation.MonsterInformations.TryGetValue(68,out source) || source.Name!="FireSpiritL43" || source.Mesh!=202 || source.Level!=43)
                throw new InvalidOperationException("Elimination Fire Spirit source changed.");
            enabled=true;
        }
        internal static void Collect(Entity monster,Entity killer) {
            if(!enabled || monster==null || killer==null || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity,killer)
                || monster.EntityFlag!=EntityFlag.Monster || monster.Companion || !monster.Dead || monster.MonsterInfo==null
                || monster.MonsterInfo.ID!=68 || monster.MonsterInfo.Name!="FireSpiritL43" || monster.MonsterInfo.Mesh!=202 || monster.MonsterInfo.Level!=43
                || !ReferenceEquals(monster.MonsterInfo.Owner,monster) || monster.MonsterInfo.Boss || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || monster.MapID!=1011 || killer.MapID!=1011)return;
            var p=killer.Owner;var map=p.Map;Entity actual;
            if(map==null || map.ID!=1011 || map.BaseID!=1011 || !map.Entities.TryGetValue(monster.UID,out actual) || !ReferenceEquals(actual,monster)
                || Kernel.GetDistance(killer.X,killer.Y,monster.X,monster.Y)>Constants.pScreenDistance)return;
            lock(p.ItemSyncRoot)lock(monster)try {
                if(!Q.Player(p) || !monster.Dead || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0)return;
                var life=lives.GetValue(monster,_=>new Life());if(life.Attempted && life.Stamp==monster.DeathStamp)return;
                life.Attempted=true;life.Stamp=monster.DeathStamp;
                if(p.Inventory.Count>=40){p.Send("Make room in your bag before collecting Fire Pills.");return;}
                S.Change change;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    change=S.EarnPill(c,t,p,Guid.NewGuid(),monster.UID);t.Commit();
                }}
                S.Apply(p,change);if(change!=null)p.Send("You collected a Fire Pill for Landy's cure.");
            }catch(Exception ex){Q.Fail(p,"elimination-fire-pill",ex);}
        }
    }
}
