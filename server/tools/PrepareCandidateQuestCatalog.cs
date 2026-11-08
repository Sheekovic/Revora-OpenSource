using System;
using Npgsql;
internal static class PrepareCandidateQuestCatalog
{
    static int Main() {
        try {
            var b=new NpgsqlConnectionStringBuilder { Host="127.0.0.1", Port=25439, Database="revora_candidate_game", Username="revora_candidate_admin", Timeout=5, CommandTimeout=10, Pooling=false,
                Password=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PASSWORD") };
            Environment.SetEnvironmentVariable("REVORA_CANDIDATE_PASSWORD",null);
            using(var c=new NpgsqlConnection(b.ConnectionString)) {
                b.Clear();c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=new NpgsqlCommand("SELECT current_database()='revora_candidate_game' AND current_user='revora_candidate_admin' AND inet_server_addr()='127.0.0.1'::inet AND inet_server_port()=25439 AND current_setting('data_directory')='C:/Revora/DisposablePostgres/moon-box-candidate/data' AND NOT EXISTS(SELECT 1 FROM public.entities)",c,t))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Candidate identity failed");
                    int map,x,y,mesh;
                    using(var q=new NpgsqlCommand("SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=@id FOR UPDATE",c,t)) {
                        q.Parameters.AddWithValue("id",4487);
                        using(var r=q.ExecuteReader()) {
                            if(!r.Read())throw new InvalidOperationException("Missing General Amber");
                            map=Convert.ToInt32(r.GetValue(0));x=Convert.ToInt32(r.GetValue(1));y=Convert.ToInt32(r.GetValue(2));mesh=Convert.ToInt32(r.GetValue(3));
                            if(r.Read())throw new InvalidOperationException("Duplicate NPC");
                        }
                    }
                    if(map!=1020 || mesh!=6700 || !((x==879 && y==562)||(x==865 && y==540)))throw new InvalidOperationException("Unknown placement preimage");
                    using(var q=new NpgsqlCommand("UPDATE public.npcs SET cellx=@x,celly=@y WHERE id=@id AND mapid=@map AND cellx=@oldx AND celly=@oldy AND lookface=@mesh",c,t)) {
                        q.Parameters.AddWithValue("x",865);q.Parameters.AddWithValue("y",540);q.Parameters.AddWithValue("id",4487);
                        q.Parameters.AddWithValue("map",map);q.Parameters.AddWithValue("oldx",x);q.Parameters.AddWithValue("oldy",y);q.Parameters.AddWithValue("mesh",mesh);
                        if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Placement preimage changed");
                    }
                    t.Commit();Console.WriteLine("Private General Amber: 1020 ("+x+","+y+") -> (865,540).");
                }
            }
            return 0;
        }catch(Exception e){Console.Error.WriteLine("Private quest catalog failed: "+e.GetType().Name);return 1;}
    }
}
