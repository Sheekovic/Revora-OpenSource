
using System;
using System.Collections.Generic;
using System.Threading.Generic;

namespace Revora.Network.GamePackets
{
    public class GroundMovement : Writer, Interfaces.IPacket
    {
        public const uint Walk = 0,
                          Run = 1,
                          Slide = 9;

        private byte[] Buffer;
        public GroundMovement(bool CreateInstance)
        {
            if (CreateInstance)
            {
                Buffer = new byte[32];
                WriteUInt32(24, 0, Buffer);
                WriteUInt32(10005, 2, Buffer);
            }
        }
        public byte[] ToArray()
        {
            byte[] ptr = CreateProtocolBuffer((uint)Direction, UID, GroundMovementType, TimeStamp, MapID);
            byte[] Buffer = new byte[11 + ptr.Length];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(10005, 2, Buffer);
            Array.Copy(ptr, 0, Buffer, 4, ptr.Length);
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            var packet = new byte[buffer.Length - 4];
            Array.Copy(buffer, 4, packet, 0, packet.Length);
            var values = Read7BitEncodedInt(packet);
            var offest = 0;
            var direction = (values[offest++] % 24);
            Direction = (Game.Enums.ConquerAngle)direction;
            UID = values[offest++];
            GroundMovementType = values[offest++];
            TimeStamp = values[offest++];
            MapID = values[offest++]; ;
        }
        public static byte[] CreateProtocolBuffer(params uint[] values)
        {
            List<byte> ptr = new List<byte>();
            ptr.Add(8);
            for (int x = 0; x < values.Length; x++)
            {
                uint value = values[x];
                while (value > 0x7F)
                {
                    ptr.Add((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }
                ptr.Add((byte)(value & 0x7F));
                ptr.Add((byte)(8 * (x + 2)));
                if (x + 1 == values.Length)
                    break;
            }
            return ptr.ToArray();
        }
        public static uint[] Read7BitEncodedInt(byte[] buffer)
        {
            List<uint> ptr2 = new List<uint>();

            for (int i = 0; i < buffer.Length; )
            {
                if (i + 2 <= buffer.Length)
                {
                    int tmp = buffer[i++];

                    if (tmp % 8 == 0)
                        while (true)
                        {
                            if (i + 1 > buffer.Length) break;
                            tmp = buffer[i++];
                            if (tmp < 128)
                            {
                                ptr2.Add((uint)tmp);
                                break;
                            }
                            else
                            {
                                int result = tmp & 0x7f;
                                if ((tmp = buffer[i++]) < 128)
                                {
                                    result |= tmp << 7;
                                    ptr2.Add((uint)result);
                                    break;
                                }
                                else
                                {
                                    result |= (tmp & 0x7f) << 7;
                                    if ((tmp = buffer[i++]) < 128)
                                    {
                                        result |= tmp << 14;
                                        ptr2.Add((uint)result);
                                        break;
                                    }
                                    else
                                    {
                                        result |= (tmp & 0x7f) << 14;
                                        if ((tmp = buffer[i++]) < 128)
                                        {
                                            result |= tmp << 21;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                        else
                                        {
                                            result |= (tmp & 0x7f) << 21;
                                            result |= (tmp = buffer[i++]) << 28;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                }
                else break;
            }
            return ptr2.ToArray();
        }
        public Game.Enums.ConquerAngle Direction;
        public uint UID;
        public uint GroundMovementType;
        public uint TimeStamp;
        public uint MapID;
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            Revora.Game.Features.ExpeditionEvent.NotifyAction(client, "you moved");
            GroundMovement groundMovement = new GroundMovement(false);
            groundMovement.Deserialize(packet);
            client.Entity.SpiritFocus = false;
            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
            client.Entity.IntensifyPercent = 0;
            client.Entity.SetAway(false);
            client.LastMove = Time32.Now;
            GeneralData.PlayerGroundMovment(groundMovement, client);
        }
    }
}