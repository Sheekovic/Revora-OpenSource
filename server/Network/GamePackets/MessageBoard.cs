// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using Revora.Client;
using System.Runtime.InteropServices;
using Revora.Network.GamePackets;

namespace Revora.Network
{
    public unsafe class MessageBoard
    {
        public partial class Msg
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MsgHeader
            {
                public Int16 Length;
                public Int16 Type;
            }
        }
        public enum Action
        {
            None = 0,
            Del = 1,
            GetList = 2,
            List = 3,
            GetWords = 4,
        };
        public enum Channel
        {
            None = 0,
            MsgTrade = 2201,
            MsgFriend = 2202,
            MsgTeam = 2203,
            MsgSyn = 2204,
            MsgOther = 2205,
            MsgSystem = 2206,
        };
        public struct MsgInfo
        {
            public MessageBoard.Msg.MsgHeader Header;
            public UInt16 Index;
            public UInt16 Channel;
            public Byte Action;
            public String Param;
        };
        public static void Create(GameState client, UInt16 Index, ushort Channel, String[] Params, Action Action)
        {
            try
            {
                int StrLength = 0;
                if (Params != null)
                {
                    for (int i = 0; i < Params.Length; i++)
                    {
                        if (Params[i] == null || Params[i].Length > 255)
                            return;

                        StrLength += Params[i].Length + 1;
                    }
                }
                byte[] Out = new byte[14 + StrLength + 8];
                Writer.WriteUInt16((ushort)(Out.Length - 8), 0, Out);
                Writer.WriteUInt16(1111, 2, Out);
                Writer.WriteUInt16(Index, 8, Out);
                Writer.WriteUInt16(Channel, 10, Out);
                Writer.WriteByte((byte)Action, 12, Out);
                if (Params != null)
                {
                    int Pos = 14;
                    Writer.WriteByte((byte)Params.Length, 13, Out);
                    for (int x = 0; x < Params.Length; x++)
                    {
                        Writer.WriteByte((byte)Params[x].Length, Pos, Out);
                        for (byte i = 0; i < (byte)Params[x].Length; i++)
                            Writer.WriteByte((byte)Params[x][i], (Pos + 1 + i), Out);
                        Pos += Params[x].Length + 1;
                    }
                }
                client.Send(Out);
                return;

            }
            catch (Exception Exc)
            {
                Console.WriteLine(Exc);
                return;
            }
        }
        public static Encoding Encoding = Encoding.GetEncoding("iso-8859-1");
        public static void Process(GameState Client, Byte[] Buffer)
        {
            try
            {
                ushort MsgLength = Revora.BitConverter.ToUInt16(Buffer, 0);
                ushort MsgId = Revora.BitConverter.ToUInt16(Buffer, 2);
                ushort Index = Revora.BitConverter.ToUInt16(Buffer, 8);
                ushort Channel = Revora.BitConverter.ToUInt16(Buffer, 10);
                Action Action = (Action)Buffer[12];
                string Param = null;
                Param = Encoding.Default.GetString(Buffer, 15, Buffer[14]);
                switch (Action)
                {
                    case Action.Del:
                        {
                            if (Param != Client.Entity.Name) return;
                            Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(Param, (ushort)Channel);
                            Message.MessageBoard.Delete(Info, (ushort)Channel);
                            return;
                        }
                    case Action.GetList:
                        {
                            String[] List = Message.MessageBoard.GetList(Index, (ushort)Channel);
                            MessageBoard.Create(Client, Index, Channel, List, Action.List);
                            return;
                        }
                    case Action.GetWords:
                        {
                            String Words = Message.MessageBoard.GetWords(Param, (UInt16)Channel);
                            Client.Send(new Message(Words, Client.Entity.Name, Param, System.Drawing.Color.White, Channel));
                            return;
                        }
                    default:
                        {
                            System.Console.WriteLine("Msg[{0}], Action[{1}] not implemented yet!", MsgId, (Int16)Action);
                            return;
                        }
                    //default: Console.WriteLine("Msg[{0}], Action[{1}] not implemented yet!", MsgId, (Int16)Action); return;
                }
            }
            catch (Exception Exc)
            {
                Console.WriteLine(Exc);
            }
        }
    }
}