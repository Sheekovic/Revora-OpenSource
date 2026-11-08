using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class InnerPowerStage : Writer
    {
        public enum ActionID : ushort
        {
            UpdateStage = 1,
            UpdateScore = 2
        }
        byte[] stream;


        public InnerPowerStage(ActionID action, uint UID, Game.InnerPower.Stage stage)
        {
            stream = new byte[200];
            WriteUInt16(192, 0, stream);
            WriteUInt16(2611, 2, stream);
            WriteUInt32(UID, 4, stream);
            WriteUInt32(stage.Score, 8, stream);
            WriteUInt16((ushort)action, 16, stream);
            var array_gongs = stage.NeiGongs.Where(p => p.Unlocked).ToArray();
            WriteUInt16((ushort)array_gongs.Length, 18, stream);
            WriteUInt16(stage.GetNoumberAtributes(array_gongs), 20, stream);

            ushort position = 22;
            for (int x = 0; x < array_gongs.Length; x++)
            {
                var element = array_gongs[x];
                WriteUInt16(element.ID, position, stream); position += 2;
                WriteByte(element.level, position, stream); position += 1;
                WriteByte(element.Score, position, stream); position += 1;
                WriteByte((byte)(element.Complete ? 1 : 0), position, stream); position += 1;
            }
            var DBStage = Database.InnerPowerTable.Stages[stage.ID - 1];
            for (int x = 0; x < array_gongs.Length; x++)
            {
                var element = array_gongs[x];
                for (int y = 0; y < DBStage.NeiGongAtributes[x].AtributesType.Length; y++)
                {
                    var atribut = DBStage.NeiGongAtributes[x];
                    WriteUInt16(element.ID, position, stream); position += 2;
                    WriteByte((byte)atribut.AtributesType[y], position, stream); position += 1;
                    WriteUInt32((uint)((atribut.AtributesValues[y] / atribut.MaxLevel) * element.level), position, stream); position += 4;
                }
            }
        }

        public byte[] ToArray()
        {
            return stream;
        }

    }
}
