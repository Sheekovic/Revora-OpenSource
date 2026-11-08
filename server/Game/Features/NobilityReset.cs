using System;
using Revora.Database;
using Revora.Game.ConquerStructures;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class NobilityReset
    {
        internal static Action BeforeProjection;
        internal static bool Advance(DateTime week,DateTime legacy) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SELECT pg_advisory_xact_lock(67112064)");
                string seed=(legacy==DateTime.MinValue?week:legacy).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
                Q.Execute(c,t,"INSERT INTO revora_nobility_reset(id,week) VALUES(true,CAST(@week AS date)) ON CONFLICT(id) DO NOTHING","week",seed);
                DateTime saved;using(var q=Q.Command(c,t,"SELECT week FROM revora_nobility_reset WHERE id=true FOR UPDATE")) {
                    object value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Nobility reset marker missing.");saved=Convert.ToDateTime(value);
                }
                if(saved.Date>=week.Date){t.Commit();return false;}
                long expected;using(var q=Q.Command(c,t,"SELECT count(*) FROM nobility"))expected=Convert.ToInt64(q.ExecuteScalar());
                if(Q.Execute(c,t,"UPDATE nobility SET donation=0,rey=0")!=expected)throw new InvalidOperationException("Nobility reset did not clear every entry.");
                if(Q.Execute(c,t,"UPDATE revora_nobility_reset SET week=CAST(@week AS date) WHERE id=true","week",week.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture))!=1)
                    throw new InvalidOperationException("Nobility reset marker was not saved.");
                t.Commit();return true;
            }}
        }
        internal static void Project() {
            var hook=BeforeProjection;if(hook!=null)hook();
            var board=new System.Collections.Generic.SafeDictionary<uint,NobilityInformation>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT entityuid,entityname,donation,gender,mesh,rey FROM nobility"))
            using(var r=q.ExecuteReader())while(r.Read()) {
                var entry=new NobilityInformation{EntityUID=checked(Convert.ToUInt32(r[0])),Name=Convert.ToString(r[1]),Donation=checked(Convert.ToUInt64(r[2])),
                    Gender=checked(Convert.ToByte(r[3])),Mesh=checked(Convert.ToUInt32(r[4])),Rey=Convert.ToInt32(r[5]),Position=-1};
                if(board.ContainsKey(entry.EntityUID))throw new InvalidOperationException("Duplicate nobility board entry.");board.Add(entry.EntityUID,entry);
            }}
            Nobility.Board=board;
            foreach(var p in Kernel.GamePool.Values) {
                if(p==null || p.Entity==null || p.NobilityInformation==null)continue;
                NobilityInformation entry;
                if(!board.TryGetValue(p.Entity.UID,out entry))entry=new NobilityInformation{EntityUID=p.Entity.UID,Name=p.Entity.Name,Mesh=p.Entity.Mesh,Position=-1};
                p.NobilityInformation=entry;p.Entity.NobalityDonation=entry.Donation;p.Entity.NobilityRank=NobilityRank.Duke;
            }
            Nobility.Sort(0);
        }
    }
}
