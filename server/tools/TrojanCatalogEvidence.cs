using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Npgsql;
using Revora;
using Revora.Database;

internal static class TrojanCatalogEvidence
{
    private static string step = "settings";
    private static List<Dictionary<string, object>> Read(NpgsqlConnection c, NpgsqlTransaction t, string sql, string parameter, object value)
    {
        using(var q = new NpgsqlCommand(sql, c, t)) {
            q.CommandTimeout = 10;
            q.Parameters.AddWithValue(parameter, value);
            var rows = new List<Dictionary<string, object>>();
            using(var r=q.ExecuteReader()) while(r.Read()) {
                var row=new Dictionary<string, object>();
                for(int i=0;i<r.FieldCount;i++) row[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }
    }
    private static int Main()
    {
        try {
            var ini=new IniFile("Configuracion.ini");
            string host=ini.ReadString("MySql","Host"),port=ini.ReadString("MySql","Port");
            if(!string.IsNullOrEmpty(port) && host.IndexOf("Port=",StringComparison.OrdinalIgnoreCase)<0)host=host.Trim().TrimEnd(';')+";Port="+port;
            DataHolder.CreateConnection(ini.ReadString("MySql","Username"),ini.ReadString("MySql","Password"),ini.ReadString("MySql","Database"),host);
            var result=new Dictionary<string,object>();
            var npcIds=new[]{10579,10580,10581,10582,10583,10600,10602,10603,66712,127881,127871,127861,122861};
            result["requested_npcs"]=npcIds;
            using(var c=DataHolder.MySqlConnection) {
                c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=new NpgsqlCommand("SET TRANSACTION READ ONLY",c,t))q.ExecuteNonQuery();
                    step="maps";result[step]=Read(c,t,"SELECT id,mapdoc FROM public.maps WHERE id=ANY(@ids) OR mapdoc=3825 ORDER BY id","ids",new[]{3825,3830,3831,3832,4121,4122,4123,4124,6000});
                    step="npcs";result[step]=Read(c,t,"SELECT id,mapid,cellx,celly,lookface FROM public.npcs WHERE id=ANY(@ids) ORDER BY id","ids",npcIds);
                    step="monsters";var monsters=Read(c,t,"SELECT id,name,lookface,life,attack_min,attack_max FROM public.monsterinfos WHERE name=ANY(@names) ORDER BY id","names",new[]{"FuriousFlameDevastator","AwakeFlameDevastator","EvilMonkMisery","GhostReaver","FlameDevastator"});result[step]=monsters;
                    step="spawns";result[step]=Read(c,t,"SELECT mapid,npctype,count(*) AS spawn_groups FROM public.monsterspawns WHERE npctype=ANY(@ids) GROUP BY mapid,npctype ORDER BY mapid,npctype","ids",monsters.Select(m=>Convert.ToInt32(m["id"])).ToArray());
                    t.Rollback();
                }
            }
            result["read_only"]=true;result["observed_utc"]=DateTime.UtcNow.ToString("o");
            System.Console.WriteLine(new JavaScriptSerializer().Serialize(result));return 0;
        }catch(Exception ex) {
            var pg=ex as PostgresException;
            System.Console.Error.WriteLine("Trojan catalog audit failed at "+step+": "+ex.GetType().Name+(pg==null?"":" SQLSTATE="+pg.SqlState));return 1;
        }
    }
}
