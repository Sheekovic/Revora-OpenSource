//using System;
//using System.Collections.Generic;
//using ProtoBuf;
//namespace Revora
//{
//    public class MyGroupServers
//    {

//        public static List<ServerGroupProto> ServerList = new List<ServerGroupProto>();
//        public static void Load()
//        {
//            ServerList = new List<ServerGroupProto>();
//            Console.WriteLine("Loading Servers informations...  ");
//            var baseText = System.IO.File.ReadAllLines(Constants.CrossConfig);
//            foreach (string line in baseText)
//            {
//                ServerGroupProto server = new ServerGroupProto();
//                var xline = line.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
//                server.ServerID = uint.Parse(xline[0]);
//                server.Name = xline[1];
//                server.MapID = ushort.Parse(xline[2]);
//                server.X = ushort.Parse(xline[3]);
//                server.Y = ushort.Parse(xline[4]);
//                if (MyGroupServer.Contains(server.ServerID))
//                {
//                    server.MyGroup = true;
//                    if (server.ServerID > 100)
//                    {
//                        server.Unknowen = 38;
//                    }
//                    ServerList.Add(server);
//                }
//            }
//        }
//        [ProtoContract]
//        public class ServerGroupProto
//        {
//            [ProtoMember(1, IsRequired = true)]
//            public uint ServerID = 0;
//            [ProtoMember(2, IsRequired = true)]
//            public bool MyGroup = false;
//            [ProtoMember(3, IsRequired = true)]
//            public string Name = "";
//            [ProtoMember(4, IsRequired = true)]
//            public uint MapID = 0;
//            [ProtoMember(5, IsRequired = true)]
//            public uint X = 0;
//            [ProtoMember(6, IsRequired = true)]
//            public uint Y = 0;
//            [ProtoMember(7, IsRequired = true)]
//            public uint Unknowen = 0;
//        }
//        public static byte[] SendPacket()
//        {
//            byte[] buffer;
//            using (var ms = new System.IO.MemoryStream())
//            {
//                ProtoBuf.Serializer.Serialize<List<ServerGroupProto>>(ms, ServerList);
//                buffer = ms.ToArray();
//            }
//            byte[] array = new byte[4 + 8 + buffer.Length];
//            System.Buffer.BlockCopy(buffer, 0, array, 4, buffer.Length);
//            Network.Writer.WriteUInt16((ushort)(array.Length - 8), 0, array);
//            Network.Writer.WriteUInt16(2500, 2, array);
//            return array;
//        }
//        public static List<uint> MyGroupServer = new List<uint>()
//        {
//           3,
//            34,
//            6,
//            18,
//            14,
//            24,
//            42,
//            32,
//            47,
//            25,
//            38,
//            83,
//            86,
//            104
//        };
//    }
//}
