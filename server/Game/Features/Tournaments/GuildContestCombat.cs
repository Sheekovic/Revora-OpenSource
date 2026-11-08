using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildContestCombat
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Life {
            internal Guid Run,Id;internal Map Map;internal int Band;internal uint Family;
        }
        private static readonly ConditionalWeakTable<Entity,Life> lives=new ConditionalWeakTable<Entity,Life>();
        internal static void Revived(Entity monster){lives.Remove(monster);}
        internal static bool Register(Guid run,Entity monster,int band) {
            if(run==Guid.Empty || monster==null || band<0 || band>3 || monster.EntityFlag!=EntityFlag.Monster || monster.MonsterInfo==null
                || monster.Companion || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver)return false;
            lock(monster.MonsterLifeSyncRoot) {
                Map map;Entity owned;Life prior;
                if(monster.Dead || !Kernel.Maps.TryGetValue(monster.MapID,out map) || map.ID==map.BaseID
                    || !map.Entities.TryGetValue(monster.UID,out owned) || !ReferenceEquals(owned,monster) || lives.TryGetValue(monster,out prior))return false;
                var life=new Life{Run=run,Id=Guid.NewGuid(),Map=map,Band=band,Family=monster.MonsterInfo.ID};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_contests WHERE id=@run AND state='open' AND @now<ends_at FOR UPDATE","run",run,"now",Clock()))
                        if(q.ExecuteScalar()==null)return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_guild_contest_lives(run,life,band,map) VALUES(@run,@life,@band,@map)","run",run,"life",life.Id,"band",band,"map",(long)map.ID)!=1)
                        throw new InvalidOperationException("Guild contest monster life was not registered.");
                    t.Commit();
                }}
                lives.Add(monster,life);return true;
            }
        }
        internal static bool Accept(Entity monster,Entity killer) {
            Life life;if(monster==null || !lives.TryGetValue(monster,out life))return true;
            if(!Monitor.IsEntered(monster.MonsterLifeSyncRoot))throw new InvalidOperationException("Guild contest death requires life ownership.");
            var p=killer==null?null:killer.Owner;Map map;Entity owned;
            Revora.Game.ConquerStructures.Society.Guild guild;
            Revora.Game.ConquerStructures.Society.Guild.Member member;
            if(monster.Dead || monster.Companion || monster.MonsterInfo==null || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || monster.MonsterInfo.ID!=life.Family || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Companion
                || !Q.Player(p) || !ReferenceEquals(p.Entity,killer) || !ReferenceEquals(p.Map,life.Map)
                || !Kernel.Maps.TryGetValue(life.Map.ID,out map) || !ReferenceEquals(map,life.Map)
                || monster.MapID!=map.ID || killer.MapID!=map.ID || !map.Entities.TryGetValue(monster.UID,out owned) || !ReferenceEquals(monster,owned)
                || p.Guild==null || p.AsMember==null || p.AsMember.GuildID!=p.Guild.ID || killer.GuildID!=p.Guild.ID
                || !Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) || !ReferenceEquals(guild,p.Guild)
                || !guild.Members.TryGetValue(killer.UID,out member) || !ReferenceEquals(member,p.AsMember) || killer.Level==0
                || GuildPkRules.ContestBand(killer.Level)!=life.Band)return false;
            if(!Monitor.TryEnter(p.ItemSyncRoot))return false;
            try {return GuildContestTable.Kill(life.Run,life.Id,killer.UID,p.Guild.ID,Clock());}
            catch(Exception ex){Console.WriteLine("[guild-contest] death uid="+killer.UID+" error="+ex.GetType().Name);return false;}
            finally {Monitor.Exit(p.ItemSyncRoot);}
        }
    }
}
