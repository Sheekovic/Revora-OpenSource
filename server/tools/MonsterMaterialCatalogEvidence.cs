using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Npgsql;
using Revora;
using Revora.Database;

internal static class MonsterMaterialCatalogEvidence
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
            using(var c=DataHolder.MySqlConnection) {
                c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=new NpgsqlCommand("SET TRANSACTION READ ONLY",c,t))q.ExecuteNonQuery();
                    result["monsters"]=Read(c,t,"SELECT DISTINCT m.id,m.name,m.level,m.ownitem,m.ownitemrate,s.mapid FROM public.monsterinfos m LEFT JOIN public.monsterspawns s ON s.npctype=m.id WHERE m.ownitem=ANY(@ids) ORDER BY m.id,s.mapid","ids",new[]{1088000,1088001});
                    t.Rollback();
                }
            }
            result["read_only"]=true;result["observed_utc"]=DateTime.UtcNow.ToString("o");
            System.Console.WriteLine(new JavaScriptSerializer().Serialize(result));return 0;
        }catch(Exception ex) {
            var pg=ex as PostgresException;
            System.Console.Error.WriteLine("Monster material catalog audit failed at "+step+": "+ex.GetType().Name+(pg==null?"":" SQLSTATE="+pg.SqlState));return 1;
        }
    }
}
