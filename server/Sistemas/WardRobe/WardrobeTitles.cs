using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Npgsql;
namespace Revora.Network.GamePackets
{
    public class WardrobeTitles
    {
        public uint Id;
        public byte[] Data = new byte[1];
        public int Points;
        internal readonly object Sync = new object();
        internal Dictionary<int,bool> Records()
        {
            var result=new Dictionary<int,bool>();
            if(Data==null || Data.Length<1 || (Data.Length-1)%5!=0) return result;
            // Recover all physical records, even when the old byte count wrapped.
            for(int offset=1;offset+5<=Data.Length;offset+=5) {
                int type=BitConverter.ToInt16(Data,offset),id=BitConverter.ToInt16(Data,offset+2);
                if(!StorageManager.Known(type,id))continue;
                int key=type*10000+id;bool previous;result.TryGetValue(key,out previous);
                result[key]=previous || Data[offset+4]==1;
            }
            return result;
        }
        internal void Replace(Dictionary<int,bool> records)
        {
            lock(Sync) {
                if(records.Count>255)throw new InvalidOperationException("Too many wardrobe titles.");
                byte[] next;bool wing=false,title=false;int points=0;
                using(var stream=new MemoryStream())
                using(var writer=new BinaryWriter(stream)) {
                    writer.Write((byte)records.Count);
                    foreach(var record in records.OrderBy(r=>r.Key)) {
                        short type=(short)(record.Key/10000),id=(short)(record.Key%10000);
                        if(!StorageManager.Known(type,id))throw new InvalidOperationException("Unknown wardrobe title.");
                        bool isWing=StorageManager.Wing<bool>(type,id);
                        bool equipped=record.Value && !(isWing?wing:title);
                        if(equipped){if(isWing)wing=true;else title=true;}
                        writer.Write(type);writer.Write(id);writer.Write(equipped);
                        points+=StorageManager.GetTitlePoints(type,id);
                    }
                    next=stream.ToArray();
                }
                if(Data!=null && next.SequenceEqual(Data) && points==Points)return;
                Persist(next,points);Data=next;Points=points;
            }
        }
        private void Persist(byte[] data,int points)
        {
            using(var connection=Database.DataHolder.MySqlConnection) {
                connection.Open();
                using(var transaction=connection.BeginTransaction()) {
                    using(var command=new NpgsqlCommand("INSERT INTO revora_title_backup_v1(id,points,data) SELECT id,points,data FROM titles WHERE id=@id ON CONFLICT(id) DO NOTHING",connection,transaction)) {
                        command.Parameters.AddWithValue("id",(long)Id);command.ExecuteNonQuery();
                    }
                    using(var command=new NpgsqlCommand("INSERT INTO titles(id,points,data) VALUES(@id,@points,@data) ON CONFLICT(id) DO UPDATE SET points=EXCLUDED.points,data=EXCLUDED.data",connection,transaction)) {
                        command.Parameters.AddWithValue("id",(long)Id);command.Parameters.AddWithValue("points",points);command.Parameters.AddWithValue("data",data);command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            StorageManager.Data[Id]=this;
        }
        public void Create(){Update();}
        public void Update(){lock(Sync)Persist(Data,Points);}
        public void Update(short type,short id,bool equipped)
        {
            lock(Sync) {
                var records=Records();int key=type*10000+id;if(!records.ContainsKey(key))return;
                bool wing=StorageManager.Wing<bool>(type,id);
                if(equipped)foreach(int other in records.Keys.ToArray())
                    if(StorageManager.Wing<bool>(other/10000,other%10000)==wing)records[other]=false;
                records[key]=equipped;Replace(records);
            }
        }
    }
}
