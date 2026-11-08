using System;
using Revora;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type Activity = typeof(Entity).Assembly.GetType("Revora.Database.CharacterActivity");
    static void VerifyCharacterActivity()
    {
        foreach(long? raw in new long?[]{null,0,-1,1,uint.MaxValue,20260230010101L,20261301010101L,100000000000000L,long.MaxValue})
            Check((DateTime)Call(Activity,null,"Decode",(object)raw)==DateTime.MinValue,"unknown or malformed activity never invents historical absence: "+raw);
        var prior=new DateTime(2024,2,29,17,28,39,DateTimeKind.Local);
        Check((DateTime)Call(Activity,null,"Decode",(object)(long?)prior.Ticks)==prior,"64-bit activity ticks preserve their full local date");
        Check((DateTime)Call(Activity,null,"Decode",(object)(long?)(long)Kernel.ToDateTimeInt(prior))==prior,"supported legacy decimal activity date decodes without 32-bit truncation");
        var clock=Activity.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);var now=new DateTime(2040,2,1,12,0,0,DateTimeKind.Local);
        try
        {
            clock.SetValue(null,(Func<DateTime>)(()=>now));
            var p=PrizeLeader(222);
            Check(p.Entity.LastLogin==now && p.AsMember.LastLogin==(ulong)now.Ticks
                && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",p.Entity.UID)==now.Ticks,
                "first successful save persists this observation and both runtime activity projections");
            now=now.AddHours(2);Check(EntityTable.SaveEntity(p) && p.Entity.LastLogin==now
                && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",p.Entity.UID)==now.Ticks,"save records current activity rather than the previous save's timestamp");
            var loaded=ReloadGold(p);Check(loaded.Entity.LastLogin==now && loaded.Entity.LastLogin.Kind==DateTimeKind.Local,
                "ordinary character loading restores activity without replacing it with login time");
            var durable=now;now=now.AddHours(-1);Check(EntityTable.SaveEntity(loaded) && loaded.Entity.LastLogin==durable
                && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",loaded.Entity.UID)==durable.Ticks,"clock rollback cannot reduce saved last activity");
            now=durable.AddHours(1);ulong memberBefore=loaded.AsMember.LastLogin;
            Query("CREATE FUNCTION suppress_activity_save() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_activity_save BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_activity_save()");
            bool success;try{success=EntityTable.SaveEntity(loaded);}finally{Query("DROP TRIGGER suppress_activity_save ON entities;DROP FUNCTION suppress_activity_save()");}
            Check(!success && Fenced(loaded) && loaded.Entity.LastLogin==durable && loaded.AsMember.LastLogin==memberBefore
                && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",loaded.Entity.UID)==durable.Ticks,"failed save preserves durable and character/guild activity timestamps");DemonReset(loaded);
            Query("UPDATE entities SET lastlogin=@time WHERE uid=@uid","time",now.AddHours(1).Ticks,"uid",(long)loaded.Entity.UID);
            Check(!EntityTable.SaveEntity(loaded) && Fenced(loaded) && loaded.Entity.LastLogin==durable
                && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",loaded.Entity.UID)==now.AddHours(1).Ticks,"stale session cannot overwrite newer saved activity");
            Query("UPDATE entities SET lastlogin=@time WHERE uid=@uid","time",(long)Kernel.ToDateTimeInt(prior),"uid",(long)p.Entity.UID);
            var legacy=ReloadGold(p);Check(legacy.Entity.LastLogin==prior,"production loader accepts a valid legacy packed calendar value");
            Check(EntityTable.SaveEntity(legacy) && Scalar("SELECT lastlogin FROM entities WHERE uid=@uid",legacy.Entity.UID)==now.Ticks,
                "first new save normalizes a legacy calendar value into current 64-bit ticks");
            Query("UPDATE entities SET lastlogin=0 WHERE uid=@uid","uid",(long)p.Entity.UID);var unknown=ReloadGold(p);
            Check(unknown.Entity.LastLogin==DateTime.MinValue,"missing activity remains explicitly unknown on character load");
            Check(EntityTable.SaveEntity(unknown) && unknown.Entity.LastLogin==now,"ordinary save starts reliable history for an unknown legacy row");
        }
        finally {clock.SetValue(null,oldClock);}
    }
}
