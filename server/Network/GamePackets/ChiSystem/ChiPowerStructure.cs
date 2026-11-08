using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Revora.Game.ConquerStructures
{
    public struct ChiAttribute
    {
        public Enums.ChiAttribute Type;
        public ushort Value;

        public ChiAttribute(Enums.ChiAttribute type, ushort value)
        {
            Type = type;
            Value = value;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Value);
        }

        public void Deserialize(BinaryReader reader)
        {
            Type = (Enums.ChiAttribute)reader.ReadByte();
            Value = reader.ReadUInt16();
        }

        public static implicit operator int(ChiAttribute attribute)
        {
            return (int)attribute.Type * 10000 + attribute.Value;
        }
    }
    public class ChiPowerStructure
    {
        public Enums.ChiPowerType Power;
        public ChiAttribute[] Attributes;
        public DateTime Retreat_TimeLeft;
        public int Points;
        public void CalculatePoints()
        {
            int score = 0;
            foreach (var attribute in Attributes)
            {
                int max = Game.Enums.ChiMaxValues(attribute.Type);
                score += (int)(100 * attribute.Value / max);
            }
            Points = score;
        }
        public ChiPowerStructure(Enums.ChiPowerType power = Enums.ChiPowerType.None)
        {
            Power = power;
            Attributes = new ChiAttribute[4];
        }
        public ChiPowerStructure Deserialize(BinaryReader reader)
        {
            Power = (Enums.ChiPowerType)reader.ReadByte();
            for (int i = 0; i < Attributes.Length; i++)
                Attributes[i].Deserialize(reader);
            CalculatePoints();
            return this;
        }
        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Power);
            for (int i = 0; i < Attributes.Length; i++)
                Attributes[i].Serialize(writer);
        }
        public ChiPowerStructure Deserialize(BinaryReader reader, bool retreat = false)
        {
            Power = (Enums.ChiPowerType)reader.ReadByte();
            if (retreat) Retreat_TimeLeft = DateTime.FromBinary(reader.ReadInt64());
            for (int i = 0; i < Attributes.Length; i++) Attributes[i].Deserialize(reader);
            CalculatePoints(); return this;
        }
        public void Serialize(BinaryWriter writer, bool retreat = false)
        {
            writer.Write((byte)Power);
            if (retreat) writer.Write(Retreat_TimeLeft.Ticks);
            for (int i = 0; i < Attributes.Length; i++) Attributes[i].Serialize(writer);
        }
    }
}