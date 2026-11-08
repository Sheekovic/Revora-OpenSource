using System;

namespace Revora.Network.GamePackets
{
    public unsafe class WindowStats
    {
        byte[] array;

        public WindowStats(Client.GameState client)
        {
            array = new Byte[152];
            Writer.Write((UInt16)(array.Length - 8), 0, array);
            Writer.Write((UInt16)1040, 2, array);
            Writer.Write((UInt32)Time32.timeGetTime().GetHashCode(), 4, array);
            Writer.Write(client.Entity.UID, 8, array);
            Writer.Write(client.Entity.MaxHitpoints, 12, array);
            Writer.Write((UInt32)client.Entity.MaxMana, 16, array);
            Writer.Write((UInt32)client.Entity.MinAttack, 20, array);
            Writer.Write((UInt32)client.Entity.MaxAttack, 24, array);
            Writer.Write((UInt32)client.Entity.Defence, 28, array);
            Writer.Write((UInt32)client.Entity.MagicAttack, 32, array);
            Writer.Write((UInt32)client.Entity.MagicDefence, 36, array);
            Writer.Write((UInt32)client.Entity.Dodge, 40, array);
            Writer.Write((UInt32)client.Entity.Agility + client.Entity.HitRate, 44, array);
            Writer.Write((UInt32)(client.Accuracy), 48, array);
            Writer.Write((UInt32)(client.Entity.Gems[1]), 52, array);
            Writer.Write((UInt32)(client.Entity.Gems[0]), 56, array);
            Writer.Write((UInt32)(client.Entity.MagicDefencePercent), 60, array);
            Writer.Write((UInt32)(client.Entity.Gems[7]), 64, array);
            Writer.Write((UInt32)client.Entity.ItemBless, 68, array);
            Writer.Write((UInt32)client.Entity.CriticalStrike, 72, array);
            Writer.Write((UInt32)client.Entity.SkillCStrike, 76, array);
            Writer.Write((UInt32)client.Entity.Immunity, 80, array);
            Writer.Write((UInt32)client.Entity.Penetration, 84, array);
            Writer.Write((UInt32)client.Entity.Block, 88, array);
            Writer.Write((UInt32)client.Entity.Breaktrough, 92, array);
            Writer.Write((UInt32)client.Entity.Counteraction, 96, array);
            Writer.Write((UInt32)client.Entity.Detoxication, 100, array);
            Writer.Write((UInt32)client.Entity.PhysicalDamageIncrease, 104, array);
            Writer.Write((UInt32)client.Entity.MagicDamageIncrease, 108, array);
            Writer.Write((UInt32)client.Entity.PhysicalDamageDecrease, 112, array);
            Writer.Write((UInt32)client.Entity.MagicDamageDecrease, 116, array);
            Writer.Write((UInt32)client.Entity.MetalResistance, 120, array);
            Writer.Write((UInt32)client.Entity.WoodResistance, 124, array);
            Writer.Write((UInt32)client.Entity.WaterResistance, 128, array);
            Writer.Write((UInt32)client.Entity.FireResistance, 132, array);
            Writer.Write((UInt32)client.Entity.EarthResistance, 136, array);
            Writer.Write((Int32)client.Equipment.GetTPL(), 140, array);
        }
        public void Send(Client.GameState client)
        {
            client.Send(array);
        }
        public byte[] ToArray()
        {
            return array;
        }
        public void Deserialize(byte[] buffer)
        {
            array = buffer;
        }
    }
}