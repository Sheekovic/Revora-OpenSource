using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;

namespace Revora.Database
{
    public unsafe class MerchantTable
    {
        public static void Load(GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("merchant").Where("UID", client.Entity.UID))
            using (MySqlReader reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    client.Entity.Merchant = reader.ReadUInt16("Merchant");
                    DateTime Time = Kernel.FromDateTimeInt(reader.ReadUInt64("Date"));
                    if (DateTime.Now > Time)
                    {
                        MerchantTable.Update(client);
                    }
                }
            }
        }
        public static void Insert(GameState client)
        {
            client.Entity.Merchant = 1;
            ulong value = Kernel.ToDateTimeInt(DateTime.Now.AddDays(1));
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("merchant").Insert("UID", client.Entity.UID)
                   .Insert("Merchant", client.Entity.Merchant)
                   .Insert("Date", value).Execute();
        }
        public static void Update(GameState client)
        {
            client.Entity.Merchant = 255;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("merchant"))
                cmd.Set("Merchant", client.Entity.Merchant)/*.Set("Date", 0)*/.Where("UID", client.Entity.UID).Execute();
        }
        public static void Cancel(GameState client)
        {
            client.Entity.Merchant = 0;
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("merchant", "UID", client.Entity.UID).Execute();
        }
    }
}
