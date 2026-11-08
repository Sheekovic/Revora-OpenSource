using System;
using Revora.Client;

namespace Revora.Database
{
    public unsafe class DialyQuestTable
    {
        public static void Load(GameState Client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("DialyQuests").Where("EntityID", Client.Entity.UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    Client.Entity.HoringThe = reader.ReadBoolean("HoringThe");
                    Client.Entity.EvreyThink = reader.ReadBoolean("EvreyThink");
                    Client.Entity.PriceName = reader.ReadString("PriceName");
                    Client.Entity.PriceMonsterName = reader.ReadString("PriceMonsterName");
                    Client.Entity.PriceLoction = reader.ReadString("PriceLoction");
                    Client.Entity.Priceamount = reader.ReadByte("Priceamount");
                    Client.Entity.PriceQuestID = reader.ReadByte("PriceQuestID");
                    Client.Entity.PriceTimes = reader.ReadByte("PriceTimes");
                    Client.Entity.PriceItemId = reader.ReadUInt32("PriceItemId");
                    Client.Entity.RelasesTheSoul = reader.ReadBoolean("RelasesTheSoul");
                    Client.Entity.RelasesGuard = reader.ReadByte("RelasesGuard");
                    Client.Entity.RelasesSaqueo = reader.ReadByte("RelasesSaqueo");
                    Client.Entity.RelasesTheSaqueo = reader.ReadBoolean("RelasesTheSaqueo");
                    Client.Entity.MagniloQuest = reader.ReadBoolean("MagniloQuest");
                    Client.Entity.ThirllingSpook = reader.ReadBoolean("ThirllingSpook");
                    Client.Entity.ThirlSpookLevel = reader.ReadByte("ThirlSpookLevel");
                    Client.Entity.SpiritBeads = reader.ReadBoolean("SpiritBeads");
                    Client.Entity.spiritBeadsCount = reader.ReadUInt32("spiritBeadsCount");
                    Client.Entity.MaterialsTimes = reader.ReadByte("MaterialsTimes");
                    Client.Entity.MaterialsName = reader.ReadString("MaterialsName");
                    Client.Entity.MaterialstemId = reader.ReadUInt32("MaterialstemId");
                    Client.Entity.PriceMaterialsID = reader.ReadByte("PriceMaterialsID");
                    Client.Entity.RareMaterials = reader.ReadBoolean("RareMaterials");
                    Client.Entity.ThirllingSpookPrice = reader.ReadBoolean("ThirllingSpookPrice");
                    return;
                }
                DialyQuestTable.Insert(Client);
            }
        }
        public static void Save(GameState Client)
        {
            if (Client.Entity == null) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("DialyQuests"))
                cmd.Set("HoringThe", Client.Entity.HoringThe)
                    .Set("EvreyThink", Client.Entity.EvreyThink)
                    .Set("PriceName", Client.Entity.PriceName)
                    .Set("PriceMonsterName", Client.Entity.PriceMonsterName)
                    .Set("PriceLoction", Client.Entity.PriceLoction)
                    .Set("Priceamount", Client.Entity.Priceamount)
                    .Set("PriceQuestID", Client.Entity.PriceQuestID)
                    .Set("PriceTimes", Client.Entity.PriceTimes)
                    .Set("PriceItemId", Client.Entity.PriceItemId)
                    .Set("RelasesTheSoul", Client.Entity.RelasesTheSoul)
                    .Set("RelasesGuard", Client.Entity.RelasesGuard)
                    .Set("RelasesTheSaqueo", Client.Entity.RelasesTheSaqueo)
                    .Set("RelasesSaqueo", Client.Entity.RelasesSaqueo)
                    .Set("MagniloQuest", Client.Entity.MagniloQuest)
                    .Set("ThirllingSpook", Client.Entity.ThirllingSpook)
                    .Set("ThirlSpookLevel", Client.Entity.ThirlSpookLevel)
                    .Set("SpiritBeads", Client.Entity.SpiritBeads)
                    .Set("spiritBeadsCount", Client.Entity.spiritBeadsCount)
                    .Set("MaterialsTimes", Client.Entity.MaterialsTimes)
                    .Set("MaterialsName", Client.Entity.MaterialsName)
                    .Set("MaterialstemId", Client.Entity.MaterialstemId)
                    .Set("PriceMaterialsID", Client.Entity.PriceMaterialsID)
                    .Set("RareMaterials", Client.Entity.RareMaterials)
                    .Set("ThirllingSpookPrice", Client.Entity.ThirllingSpookPrice)
                    .Where("EntityID", Client.Entity.UID)
                    .Execute();
        }
        public static void Reset(GameState Client)
        {
            if (Client.Entity == null) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("DialyQuests"))
                cmd.Set("HoringThe", 0).Set("EvreyThink", 0)
                    .Set("PriceName", "").Set("PriceMonsterName", "")
                    .Set("PriceLoction", "").Set("Priceamount", 0)
                    .Set("PriceQuestID", 0).Set("PriceTimes", 0)
                    .Set("PriceItemId", 0).Set("RelasesTheSoul", 0)
                    .Set("RelasesGuard", 0).Set("RelasesTheSaqueo", 0)
                    .Set("RelasesSaqueo", 0).Set("MagniloQuest", 0)
                    .Set("ThirllingSpook", 0).Set("ThirlSpookLevel", 0)
                    .Set("SpiritBeads", 0).Set("PriceMaterialsID", 0)
                    .Set("MaterialsName", "").Set("MaterialsTimes", 0)
                    .Set("MaterialstemId", 0).Set("RareMaterials", 0)
                    .Where("EntityID", Client.Entity.UID)
                    .Execute();
        }
        public static void ResetFromNpc(GameState Client)
        {
            Client.Entity.CliamSeed = false;
            Client.Entity.SpiritBeads = false;
            Client.Entity.HoringThe = false;
            Client.Entity.EvreyThink = false;
            Client.Entity.RareMaterials = false;
            Client.Entity.PriceName = "";
            Client.Entity.MaterialsName = "";
            Client.Entity.PriceMonsterName = "";
            Client.Entity.PriceLoction = "";
            Client.Entity.Priceamount = 0;
            Client.Entity.PriceQuestID = 0;
            Client.Entity.PriceMaterialsID = 0;
            Client.Entity.PriceTimes = 0;
            Client.Entity.MaterialsTimes = 0;
            Client.Entity.PriceItemId = 0;
            Client.Entity.MaterialstemId = 0;
            Client.Entity.RelasesTheSoul = false;
            Client.Entity.RelasesGuard = 0;
            /////Loot
            Client.Entity.RelasesTheSaqueo = false;
            Client.Entity.RelasesSaqueo = 0;
            /////
            Client.Entity.MagniloQuest = false;
            Client.Entity.ThirllingSpook = false;
            Client.Entity.ThirlSpookLevel = 0;
        }
        public static void Insert(GameState Client)
        {
            if (!DialyQuestTable.Exists(Client.Entity.UID))
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("DialyQuests"))
                    cmd.Insert("EntityID", Client.Entity.UID)
                    .Insert("HoringThe", 0)
                    .Insert("EvreyThink", 0)
                    .Insert("PriceName", "")
                    .Insert("PriceMonsterName", "")
                    .Insert("PriceLoction", "")
                    .Insert("Priceamount", 0)
                    .Insert("PriceQuestID", 0)
                    .Insert("PriceTimes", 0)
                    .Insert("PriceItemId", 0)
                    .Insert("RelasesTheSoul", 0)
                    .Insert("RelasesGuard", 0)
                    .Insert("RelasesTheSaqueo", 0)
                    .Insert("RelasesSaqueo", 0)
                    .Insert("MagniloQuest", 0)
                    .Insert("ThirllingSpook", 0)
                    .Insert("ThirlSpookLevel", 0)
                    .Insert("SpiritBeads", 0)
                    .Insert("spiritBeadsCount", 0)
                    .Insert("MaterialsName", "")
                    .Insert("PriceMaterialsID", 0)
                    .Insert("MaterialsTimes", 0)
                    .Insert("MaterialstemId", 0)
                    .Insert("RareMaterials", 0)
                    .Insert("ThirllingSpookPrice", 0)
                    .Execute();
            }
        }
        private static bool Exists(uint id)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("DialyQuests").Where("EntityID", id))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    reader.Close();
                    reader.Dispose();
                    return true;
                }
                return false;
            }
        }
    }
}