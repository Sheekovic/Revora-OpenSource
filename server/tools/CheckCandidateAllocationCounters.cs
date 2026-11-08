using System;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Database;
internal static partial class CheckCandidateArenaLifecycle {
    static void AllocationCounterProbe(){
        var method=typeof(Server).GetMethod("SaveAllocationCounters",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(NpgsqlConnection),typeof(uint),typeof(uint),typeof(string)},null);Require(method!=null);
        using(var c=DataHolder.MySqlConnection){c.Open();
            using(var q=new NpgsqlCommand("CREATE TEMP TABLE configuration(server text PRIMARY KEY,guildid bigint,itemuid bigint);CREATE TEMP TABLE guilds(id bigint PRIMARY KEY);INSERT INTO configuration VALUES('counter-test',500,900),('other',8,9);INSERT INTO guilds VALUES(600)",c))q.ExecuteNonQuery();
            Action<uint,uint> save=(guild,item)=>method.Invoke(null,new object[]{c,guild,item,"counter-test"});
            Func<long,long,bool> same=(guild,item)=>{using(var q=new NpgsqlCommand("SELECT guildid=@guild AND itemuid=@item FROM configuration WHERE server='counter-test'",c)){q.Parameters.AddWithValue("guild",guild);q.Parameters.AddWithValue("item",item);return (bool)q.ExecuteScalar();}};
            save(100,200);Require(same(600,900));save(700,1000);Require(same(700,1000));save(100,200);Require(same(700,1000));
            using(var q=new NpgsqlCommand("SELECT guildid=8 AND itemuid=9 FROM configuration WHERE server='other'",c))Require((bool)q.ExecuteScalar());
            bool missing=false;try{method.Invoke(null,new object[]{c,1u,1u,"missing' OR true --"});}catch(TargetInvocationException e){missing=e.InnerException is InvalidOperationException;}Require(missing && same(700,1000));
        }
        typeof(Server).GetMethod("SaveAllocationCounters",BindingFlags.Static|BindingFlags.NonPublic,null,Type.EmptyTypes,null).Invoke(null,null);
        System.Console.WriteLine("[career-process] career-allocation-counter-save-passed stale,advance,repair,scope,missing-row");Environment.Exit(0);
    }
}
