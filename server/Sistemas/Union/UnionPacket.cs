using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Revora.Network;
using Revora.Union;

namespace Revora.Network.GamePackets.Union
{
    public unsafe class UnionClass : Revora.Network.Writer
    {
        public class Member
        {
            public Member(uint UnionID)
            {
                this.UnionID = UnionID;
            }
            public uint Offical = 0;
            public uint Harem = 0;
            public uint Guards = 0;
            public uint UnionExploits = 0;
            public uint ID;
            public string Name;
            public bool IsOnline
            {
                get
                {
                    return Kernel.GamePool.ContainsKey(ID);
                }
            }
            public byte Level;
            public Revora.Game.ConquerStructures.NobilityRank NobilityRank;
            public uint Mesh = 0;
            public byte Class;
            public uint UnionID;
            public uint GuildID;
        }
        public uint ID;
        uint _goldbricks = 0;
        public uint GoldBricks
        {
            get
            {
                return _goldbricks;
            }
            set
            {

                _goldbricks = value;
                UpdateDatabase("GoldBricks", value);
            }
        }
        ulong _UnionFund = 0;
        public ulong UnionFund
        {
            get
            {
                return _UnionFund;
            }
            set
            {
                _UnionFund = value;
                UpdateDatabase("unionFund", value);
            }
        }
        string _declaration = "";
        public string Declaration
        {
            get
            {
                return _declaration;
            }
            set
            {
                _declaration = value;
                UpdateDatabase("Declaration", value);
            }
        }
        string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
                UpdateDatabase("Name", value);
                foreach (var client in Kernel.GamePool.Values)
                {
                    if (client.Union != null)
                        client.Send(SendOverheadLeagueInfo(client));
                }
            }
        }
        string _title = "";
        public string Title
        {
            get
            {

                return _title;
            }
            set
            {

                _title = value;
                UpdateDatabase("Title", value);
                foreach (var client in Kernel.GamePool.Values)
                {
                    if (client.UnionID == this.ID)
                    {
                        client.Send(SendOverheadLeagueInfo(client));
                    }
                }
            }
        }
        public void SengGui(GameState client, byte type)
        {
            byte[] Buffer = new byte[23 + 8];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(2622, 2, Buffer);
            WriteUInt16(type, 4, Buffer);
            WriteUInt16(0, 6, Buffer);
            WriteUInt16(0, 14, Buffer);//ID FOR Union 
            client.Send(Buffer.ToArray());
        }
        public uint[] Coreofficals = new uint[8];
        public uint[] Harem = new uint[10];
        public uint[] Guards = new uint[20];
        public string LeaderName;
        public uint LeaderUID;
        string _bulletin = "";
        public string Bulletin
        {
            get
            {

                return _bulletin;
            }
            set
            {

                _bulletin = value;
                UpdateDatabase("Bulletin", value);
            }
        }
        public List<uint> Guilds;
        public List<Member> Members
        {
            get
            {
                return UnionTable.LoadMembers(this.ID);
            }
        }

        public bool IsLeader(uint uid)
        {
            if (uid == LeaderUID)
            {
                return true;
            }
            return false;
        }
        public bool IsKingdom() {return Revora.Game.Features.KingdomCrown.IsUnion(ID);}
        internal static UnionClass Formed(uint id,string name,uint leader,string leaderName,uint guild) {
            return new UnionClass {ID=id,_Name=name,LeaderUID=leader,LeaderName=leaderName,Guilds=new List<uint>{guild}};
        }
        internal byte[] CommittedOverhead(GameState client) {
            var message=new Union.OverheadLeagueInfo {Type=0,UID=client.Entity.UID,UnionID=ID,
                PlayerRank=IsLeader(client.Entity.UID)?1u:0u,UnionRank=IsKingdom()?1u:0u,
                Name=IsKingdom() && Title!=""?Title+"KDM":(Name??""),ServerID=client.Entity.ServerID};
            using(var stream=new MemoryStream()) {
                Serializer.SerializeWithLengthPrefix(stream,message,PrefixStyle.Fixed32);
                byte[] bytes=new byte[stream.Length+8];Array.Copy(stream.ToArray(),bytes,stream.Length);
                fixed(byte* ptr=bytes){*(ushort*)ptr=(ushort)(bytes.Length-8);*(ushort*)(ptr+2)=(ushort)PacketID.MsgOverheadLeagueInfo;}
                return bytes;
            }
        }
        public UnionClass()
        {
            Guilds = new List<uint>();
        }
        // Startup hydration bypasses persistence setters and network notifications.
        // Parsing completes before UnionTable publishes any member of the snapshot.
        internal static UnionClass Restore(MySqlReader row)
        {
            var culture=System.Globalization.CultureInfo.InvariantCulture;
            var result=new UnionClass {
                ID=uint.Parse(row.ReadString("ID"),System.Globalization.NumberStyles.None,culture),
                _goldbricks=uint.Parse(row.ReadString("GoldBricks"),System.Globalization.NumberStyles.None,culture),
                _UnionFund=ulong.Parse(row.ReadString("UnionFund"),System.Globalization.NumberStyles.None,culture),
                _Name=row.ReadString("Name"),_title=row.ReadString("Title"),
                LeaderName=row.ReadString("LeaderName"),
                LeaderUID=uint.Parse(row.ReadString("LeaderUID"),System.Globalization.NumberStyles.None,culture),
                _bulletin=row.ReadString("Bulletin"),_declaration=row.ReadString("Declaration"),
                Coreofficals=RestoreOffices(row.ReadString("CoreOfficals"),8,true),
                Harem=RestoreOffices(row.ReadString("Harem"),10,false),
                Guards=RestoreOffices(row.ReadString("Guards"),20,false)
            };
            if(result.ID==0 || result.LeaderUID==0 || string.IsNullOrWhiteSpace(result.Name) || string.IsNullOrWhiteSpace(result.LeaderName))
                throw new InvalidOperationException("Saved union identity is invalid.");
            return result;
        }
        internal static uint[] RestoreOffices(string value,int capacity,bool legacySix)
        {
            var fields=value.Split('#');
            if(fields.Length!=capacity && !(legacySix && fields.Length==6))throw new InvalidOperationException("Saved union office count is invalid.");
            var result=new uint[capacity];
            for(int i=0;i<fields.Length;i++)result[i]=uint.Parse(fields[i],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
        public void GetMemberRankGuard(Revora.Client.GameState client)
        {
            if (Guards.Contains(client.Entity.UID))
                client.Guards = 512;
        }
        public void GetMemberRankHarem(Revora.Client.GameState client)
        {
            if (Harem.Contains(client.Entity.UID))
            {
                for (int i = 0; i < 10; i++)
                {
                    if (Harem[i] == client.Entity.UID)
                    {
                        if (i == 0)
                            client.Harem = 1024;
                        else if (i == 1 || i == 2)
                            client.Harem = 2048;
                        else client.Harem = 4096;
                    }
                }
            }
        }
        public void GetMemberRankOffical(Revora.Client.GameState client)
        {
            if (IsLeader(client.Entity.UID))
            {
                client.Offical = 1;
                return;
            }
            if (Coreofficals.Contains(client.Entity.UID))
            {
                for (int i = 0; i < 8; i++)
                {
                    if (Coreofficals[i] == client.Entity.UID)
                    {
                        if (i == 0)
                            client.Offical = 2;
                        else if (i == 1)
                            client.Offical = 4;
                        else if (i == 2)
                            client.Offical = 8;
                        else if (i == 3)
                            client.Offical = 16;
                        else if (i == 4)
                            client.Offical = 32;
                        else if (i == 5)
                            client.Offical = 64;
                        else if (i == 6)
                            client.Offical = 128;
                        else if (i == 7)
                            client.Offical = 256;
                    }
                }
            }
        }
        List<uint> Stipend = new List<uint>();
        public static string MakeName(string Name)
        {
            List<string> Chr = new List<string>();
            for (byte i = (byte)Name.Length; i < 16; i++)
            {
                Chr.Add(i.ToString().Replace(i.ToString(), "\0"));
            }
            StringBuilder builder = new StringBuilder();
            foreach (string N in Chr)
            {
                builder.Append(N);
            }
            return Name + builder.ToString();
        }
        #region Packets
        public byte[] ShowLeaguePalaceGuardsList(byte Page)
        {
            List<UnionClass.Member> list = new List<Member>();
            var Members = this.Members.Where(m => m.Guards == 512 && Guards.Contains(m.ID)).OrderByDescending(n => n.IsOnline).ToArray();
            list.Clear();
            byte P = 0;
            if (Page == 1)
                P = 0;
            else if (Page == 2)
                P = 1;
            for (int i = (int)P * 10; i < P * 10 + 10; i++)
            {
                if (i < Members.Length)
                {
                    list.Add(Members[i]);
                }
            }
            byte[] buffer = new byte[8 + (41 * list.Count) + 10];
            fixed (byte* ptr = buffer)
            {
                *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeaguePalaceGuardsList;
                *(byte*)(ptr + 4) = (byte)list.Count;
                *(byte*)(ptr + 5) = (byte)Page;
                if (list.Count > 0)
                {
                    int Offset = 6;
                    foreach (var Member in list)
                    {
                        if (Member.IsOnline)
                        {
                            var C = GameState.GetClientFromID(Member.ID);
                            *(uint*)(ptr + Offset) = (uint)C.Entity.BattlePower;
                        }
                        else
                        {
                            *(uint*)(ptr + Offset) = (uint)0;
                        }
                        Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Member.Mesh; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Member.UnionExploits; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Member.NobilityRank; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Member.ID; Offset += 4;
                        *(ushort*)(ptr + Offset) = (ushort)Member.Level; Offset += 2;
                        *(ushort*)(ptr + Offset) = (ushort)Member.Class; Offset += 2;
                        if (Member.IsOnline)
                            buffer[Offset] = 1;
                        else buffer[Offset] = 0;
                        Offset += 1;
                        WriteString(Member.Name, Offset, buffer);
                        Offset += 16;
                    }
                }
            }
            return buffer;
        }
        public byte[] SendAllLeagueRank()
        {
            var Unions = Kernel.Unions.Values.Where(i => i.GoldBricks != 0).OrderBy(x => x.GoldBricks).ThenBy(x => x.UnionFund).ThenByDescending(x => x.ID).Reverse().ToArray();

            byte[] buffer = new byte[17 + 8 + Unions.Length * 40];
            Writer.Write(buffer.Length - 8, 0, buffer);
            Writer.Write(2627, 2, buffer);
            buffer[4] = 1;
            buffer[11] = (byte)Math.Min(Unions.Length, 10);
            buffer[10] = 1;
            buffer[6] = (byte)Unions.Length;
            int offset = 13;
            foreach (var union in Unions)
            {
                buffer[offset] = 39; offset += 4;//ServerID
                Writer.Write(union.GoldBricks, offset, buffer); offset += 4;
                Writer.Write(union.Name, offset, buffer); offset += 16;
                Writer.Write(union.LeaderName, offset, buffer); offset += 16;
            }
            return buffer;
        }
        public byte[] SendLeagueRank()
        {
            var Unions = Kernel.Unions.Values.Where(i => i.GoldBricks != 0).OrderBy(x => x.GoldBricks).ThenBy(x => x.UnionFund).ThenByDescending(x => x.ID).Reverse().ToArray();
            int offset = 17;
            byte[] buffer = new byte[17 + 8 + Unions.Length * 40];
            Writer.Write(buffer.Length - 8, 0, buffer);
            Writer.Write(2627, 2, buffer);
            Writer.Write(Unions.Length, 6, buffer);
            Writer.Write(Unions.Length, 7, buffer);
            Writer.Write(Unions.Length, 11, buffer);
            foreach (var union in Unions)
            {
                Writer.Write(union.GoldBricks, offset, buffer); offset += 4;
                Writer.Write(union.Name, offset, buffer); offset += 16;
                Writer.Write(union.LeaderName, offset, buffer); offset += 20;
            }
            return buffer;
        }
        public byte[] ShowLeagueConcubinesList()
        {
            List<Union.MsgImperialHarem> Packets = new List<Union.MsgImperialHarem>();
            byte[] buffer = new byte[0];
            List<Member> Members = this.Members.Where(m => m.Harem > 0 && Harem.Contains(m.ID)).OrderByDescending(n => n.IsOnline).ToList();
            Union.MsgImperialHaremStartPacket Packet = new Union.MsgImperialHaremStartPacket();
            Union.MsgImperialHarem Packett = null;
            uint I = 0;
            foreach (var mem in Members)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (Harem[i] == mem.ID)
                    {
                        if (i == 1 || i == 2)
                            I = (uint)i;
                        else if (i > 2)
                            I = (uint)i - 2;
                    }
                }
                uint ID = 0;
                if (mem.Harem == 1024)
                    ID = 4000;
                if (mem.Harem == 2048)
                    ID = 4010 + I;
                if (mem.Harem == 4096)
                    ID = 4020 + I;
                Packett = new Union.MsgImperialHarem()
                {
                    UID = mem.ID,
                    PositionRank = ID,
                    Name = MakeName(mem.Name),
                    Class = mem.Class,
                    Level = mem.Level,
                    Exploits = mem.UnionExploits,
                    Nobility = (uint)mem.NobilityRank,
                    Online_Offline = mem.IsOnline ? (uint)1 : (uint)0,
                    Mesh = mem.Mesh
                };
                if (mem.IsOnline)
                    Packett.BattlePower = (uint)GameState.GetClientFromID(mem.ID).Entity.BattlePower;
                Packets.Add(Packett);
            }
            if (Packets.Count > 0)
            {
                Packet = new Union.MsgImperialHaremStartPacket()
                {
                    count = (uint)Packets.Count,
                    Accounts = Packets
                };
                using (var stream = new MemoryStream())
                {
                    Serializer.SerializeWithLengthPrefix(stream, Packet, PrefixStyle.Fixed32);
                    buffer = new byte[stream.Length + 8];
                    Array.Copy(stream.ToArray(), 0, buffer, 0, stream.Length);
                    fixed (byte* ptr1 = buffer)
                    {
                        *((ushort*)ptr1 + 0) = (ushort)(buffer.Length - 8);
                        *((ushort*)(ptr1 + 2)) = (ushort)PacketID.MsgLeagueConcubines;
                    }
                }
            }
            else
            {
                using (var stream = new MemoryStream())
                {
                    Serializer.SerializeWithLengthPrefix(stream, Packet, PrefixStyle.Fixed32);
                    buffer = new byte[6 + 8];
                    Array.Copy(stream.ToArray(), 0, buffer, 0, stream.Length);
                    fixed (byte* ptr1 = buffer)
                    {
                        *((ushort*)ptr1 + 0) = (ushort)(buffer.Length - 8);
                        *((ushort*)(ptr1 + 2)) = (ushort)PacketID.MsgLeagueConcubines;
                    }
                }
            }
            return buffer;
        }
        public byte[] ShowLeagueImperialCourt()
        {
            byte[] buffer;
            List<Member> Members = this.Members.Where(m => m.Offical > 0 && Coreofficals.Contains(m.ID) || m.Offical == 1).OrderByDescending(n => n.IsOnline).ToList();
            int count = Members.Count;
            buffer = new byte[(43 * count) + 8 + 10];
            fixed (byte* ptr = buffer)
            {
                *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueImperialCourtList;
                *(byte*)(ptr + 4) = (byte)1;
                *(uint*)(ptr + 5) = (uint)count;
                int Offset = 6;
                foreach (var Member in Members)
                {
                    ushort ID = 0;
                    if (Member.IsOnline)
                    {
                        var C = GameState.GetClientFromID(Member.ID);
                        *(uint*)(ptr + Offset) = (uint)C.Entity.BattlePower;
                    }
                    else
                    {
                        *(uint*)(ptr + Offset) = (uint)0;
                    }
                    Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.Mesh; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.UnionExploits; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.NobilityRank; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.ID; Offset += 4;
                    *(ushort*)(ptr + Offset) = (ushort)Member.Level; Offset += 2;
                    *(ushort*)(ptr + Offset) = (ushort)Member.Class; Offset += 2;
                    if (Member.Offical == 1)
                        ID = 1000;
                    if (Member.Offical == 2)
                        ID = 2000;
                    if (Member.Offical == 4)
                        ID = 2010;
                    if (Member.Offical == 8)
                        ID = 2020;
                    if (Member.Offical == 16)
                        ID = 2030;
                    if (Member.Offical == 32)
                        ID = 2040;
                    if (Member.Offical == 64)
                        ID = 2050;
                    if (Member.Offical == 128)
                        ID = 2060;
                    if (Member.Offical == 256)
                        ID = 2070;
                    *(ushort*)(ptr + Offset) = (ushort)ID; Offset += 2;
                    if (Member.IsOnline)
                        buffer[Offset] = 1;
                    else buffer[Offset] = 0;
                    Offset += 1;
                    WriteString(Member.Name, Offset, buffer);
                    Offset += 16;
                }
            }
            return buffer;
        }
        public byte[] ShowLeagueImperialCourtList()
        {
            byte[] buffer;
            List<Member> Members = this.Members.Where(m => m.Offical > 0 && Coreofficals.Contains(m.ID)).OrderByDescending(n => n.IsOnline).ToList();
            int count = Members.Count;
            buffer = new byte[(43 * count) + 8 + 10];
            fixed (byte* ptr = buffer)
            {
                *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueImperialCourtList;
                *(byte*)(ptr + 4) = (byte)0;
                *(uint*)(ptr + 5) = (uint)count;
                int Offset = 6;
                foreach (var Member in Members)
                {
                    ushort ID = 0;
                    if (Member.IsOnline)
                    {
                        var C = GameState.GetClientFromID(Member.ID);
                        *(uint*)(ptr + Offset) = (uint)C.Entity.BattlePower;
                    }
                    else
                    {
                        *(uint*)(ptr + Offset) = (uint)0;
                    }
                    Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.Mesh; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.UnionExploits; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.NobilityRank; Offset += 4;
                    *(uint*)(ptr + Offset) = (uint)Member.ID; Offset += 4;
                    *(ushort*)(ptr + Offset) = (ushort)Member.Level; Offset += 2;
                    *(ushort*)(ptr + Offset) = (ushort)Member.Class; Offset += 2;
                    if (Member.Offical == 2)
                        ID = 2000;
                    if (Member.Offical == 4)
                        ID = 2010;
                    if (Member.Offical == 8)
                        ID = 2020;
                    if (Member.Offical == 16)
                        ID = 2030;
                    if (Member.Offical == 32)
                        ID = 2040;
                    if (Member.Offical == 64)
                        ID = 2050;
                    if (Member.Offical == 128)
                        ID = 2060;
                    if (Member.Offical == 256)
                        ID = 2070;
                    *(ushort*)(ptr + Offset) = (ushort)ID; Offset += 2;
                    if (Member.IsOnline)
                        buffer[Offset] = 1;
                    else buffer[Offset] = 0;
                    Offset += 1;
                    WriteString(Member.Name, Offset, buffer);
                    Offset += 16;
                }
            }
            return buffer;
        }
        public byte[] ShowLeagueGuildMemList(ushort guildid, ushort page)
        {
            List<UnionClass.Member> list = new List<UnionClass.Member>();
            byte[] buffer;
            byte Page = (byte)page;
            ushort GuildID = guildid;
            var Members = this.Members.Where(m => m.GuildID == GuildID).OrderByDescending(n => n.IsOnline).ToArray();
            list.Clear();
            for (int i = (int)page * 10; i < page * 10 + 10; i++)
            {
                if (i < Members.Length)
                {
                    list.Add(Members[i]);
                }
            }
            buffer = new byte[19 + 8 + 49 * list.Count];
            if (list.Count > 0)
            {
                fixed (byte* ptr = buffer)
                {
                    *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                    *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueMemList;
                    *(uint*)(ptr + 4) = (uint)GuildID;
                    if (Kernel.Guilds[GuildID].Leader != null)
                        *(uint*)(ptr + 8) = (uint)Kernel.Guilds[GuildID].Leader.ID;
                    *(ushort*)(ptr + 12) = (ushort)Members.Length;
                    *(ushort*)(ptr + 16) = (ushort)list.Count;
                    buffer[18] = 1;
                    int Offset = 19;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var Mem = list[i];
                        *(uint*)(ptr + Offset) = (uint)Mem.UnionExploits; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Mem.Class; Offset += 4;
                        *(ushort*)(ptr + Offset) = (ushort)Mem.Level; Offset += 2;
                        *(uint*)(ptr + Offset) = (uint)Mem.Mesh; Offset += 4;
                        if (Mem.IsOnline)
                            buffer[Offset] = 1;
                        else buffer[Offset] = 0;
                        Offset += 1;
                        WriteString(Mem.Name, Offset, buffer); Offset += 16;
                        if (Mem.IsOnline)
                        {
                            var C = GameState.GetClientFromID(Mem.ID);
                            *(uint*)(ptr + Offset) = (uint)C.UnionTitle; Offset += 4;
                        }
                        else
                        {
                            *(uint*)(ptr + Offset) = (uint)0; Offset += 4;
                        }
                        *(uint*)(ptr + Offset) = (uint)Mem.NobilityRank; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Mem.ID; Offset += 4;
                        if (Mem.IsOnline)
                        {
                            var C = GameState.GetClientFromID(Mem.ID);
                            *(ushort*)(ptr + Offset) = (ushort)C.Entity.BattlePower;
                        }
                        else
                        {
                            *(ushort*)(ptr + Offset) = (ushort)0;
                        }
                        Offset += 2;
                    }
                }
            }
            return buffer;
        }
        public byte[] ShowLeagueMemList(ushort guildid, ushort page)
        {
            List<UnionClass.Member> list = new List<UnionClass.Member>();
            byte[] buffer;
            byte Page = (byte)page;
            ushort GuildID = guildid;
            var Members = this.Members.Where(m => m.GuildID == 0).OrderByDescending(n => n.IsOnline).ToArray();
            list.Clear();
            for (int i = (int)page * 10; i < page * 10 + 10; i++)
            {
                if (i < Members.Length)
                {
                    list.Add(Members[i]);
                }
            }
            buffer = new byte[19 + 8 + 49 * list.Count];
            if (list.Count > 0)
            {
                fixed (byte* ptr = buffer)
                {
                    *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                    *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueMemList;
                    *(uint*)(ptr + 4) = (uint)GuildID;
                    *(uint*)(ptr + 8) = (uint)0;
                    *(ushort*)(ptr + 12) = (ushort)Members.Length;
                    *(ushort*)(ptr + 16) = (ushort)list.Count;
                    buffer[18] = 1;
                    int Offset = 19;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var Mem = list[i];
                        *(uint*)(ptr + Offset) = (uint)Mem.UnionExploits; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Mem.Class; Offset += 4;
                        *(ushort*)(ptr + Offset) = (ushort)Mem.Level; Offset += 2;
                        *(uint*)(ptr + Offset) = (uint)Mem.Mesh; Offset += 4;
                        if (Mem.IsOnline)
                            buffer[Offset] = 1;
                        else buffer[Offset] = 0;
                        Offset += 1;
                        WriteString(Mem.Name, Offset, buffer); Offset += 16;
                        if (Mem.IsOnline)
                        {
                            var C = GameState.GetClientFromID(Mem.ID);
                            *(uint*)(ptr + Offset) = (uint)C.UnionTitle; Offset += 4;
                        }
                        else
                        {
                            *(uint*)(ptr + Offset) = (uint)0; Offset += 4;
                        }
                        *(uint*)(ptr + Offset) = (uint)Mem.NobilityRank; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Mem.ID; Offset += 4;
                        if (Mem.IsOnline)
                        {
                            var C = GameState.GetClientFromID(Mem.ID);
                            *(ushort*)(ptr + Offset) = (ushort)C.Entity.BattlePower;
                        }
                        else
                        {
                            *(ushort*)(ptr + Offset) = (ushort)0;
                        }
                        Offset += 2;
                    }
                }
            }
            return buffer;
        }
        public byte[] SendLeagueInfo()
        {
            byte[] buffer = new byte[370];
            fixed (byte* ptr = buffer)
            {
                *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueInfo;
                *(ulong*)(ptr + 4) = (ulong)UnionFund;
                *(ulong*)(ptr + 12) = (ulong)GoldBricks;
                WriteString(Name, 28, buffer);
                WriteString(LeaderName, 44, buffer);
                WriteString(Bulletin, 60, buffer);
                WriteString(Title, 316, buffer);
                WriteString("None", 326, buffer);
                WriteString("None", 342, buffer);
            }
            return buffer;
        }
        public byte[] SendLeagueSynList(byte Page)
        {
            byte[] buffer = new byte[0];
            List<uint> list = new List<uint>();
            list.Clear();
            for (int i = (int)Page * 15; i < Page * 15 + 15; i++)
            {
                if (i < Guilds.Count)
                {
                    list.Add(Guilds[i]);
                }
            }
            buffer = new byte[13 + 8 + 48 * list.Count];
            fixed (byte* ptr = buffer)
            {
                *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueSynList;
                *(uint*)(ptr + 4) = (uint)Guilds.Count;
                buffer[8] = (byte)list.Count;
                buffer[10] = 1;
                int Offset = 11;
                for (int i = 0; i < list.Count; i++)
                {
                    uint ID = Guilds[i];
                    var Guild = Kernel.Guilds[ID];
                    *(uint*)(ptr + Offset) = (uint)Guild.ID; Offset += 4;
                    *(ulong*)(ptr + Offset) = (ulong)Guild.SilverFund; Offset += 8;
                    *(ushort*)(ptr + Offset) = (ushort)Guild.MemberCount; Offset += 2;
                    *(ushort*)(ptr + Offset) = (ushort)Guild.Level; Offset += 2;
                    WriteString(Guild.Name, Offset, buffer); Offset += 16;
                    WriteString(Guild.LeaderName, Offset, buffer); Offset += 16;
                }
                Offset += 4;
            }
            return buffer;
        }
        public static byte[] SendLeagueAllegianceList(byte page)
        {
            byte[] buffer;
            byte Page = (byte)page;
            List<UnionClass> list = new List<UnionClass>();
            var Unions = Kernel.Unions.Values.OrderBy(x => x.ID).ThenBy(x => x.GoldBricks).ThenByDescending(x => x.UnionFund).ToArray();
            list.Clear();
            for (int i = (int)page * 3; i < page * 3 + 3; i++)
            {
                if (i < Unions.Length)
                {
                    list.Add(Unions[i]);
                }
            }
            buffer = new byte[13 + 8 + 306 * list.Count];
            fixed (byte* ptr = buffer)
            {
                if (list.Count > 0)
                {

                    *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                    *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueAllegianceList;
                    buffer[4] = (byte)Unions.Length;
                    buffer[8] = (byte)Page;
                    *(ushort*)(ptr + 10) = (ushort)list.Count;
                    buffer[12] = (byte)1;
                    int Offset = 13;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var Union = list[i];
                        *(ulong*)(ptr + Offset) = (ulong)Union.UnionFund; Offset += 8;
                        *(uint*)(ptr + Offset) = (uint)Union.ID; Offset += 4;
                        *(uint*)(ptr + Offset) = (uint)Union.GoldBricks; Offset += 4;
                        WriteString(Union.Name, Offset, buffer); Offset += 16;
                        WriteString(Union.LeaderName, Offset, buffer); Offset += 16;

                        WriteString(Union.Declaration, Offset, buffer); Offset += 256;

                    }
                }
                else
                {
                    *(ushort*)(ptr + 0) = (ushort)(buffer.Length - 8);
                    *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueAllegianceList;
                }
            }
            return buffer;
        }
        public byte[] SendLeagueOrderStatu()
        {
            return Kernel.FinalizeProtoBuf(new Union.LeagueOrderStatuProto()
            {
                Unknown = 101,
                Unknown2 = 102,
                Unknown3 = 106,
                Unknown4 = 204,
                Unknown5 = 206
            }, 2630);
        }
        public byte[] SendOverheadLeagueInfo(GameState client)
        {
            byte[] buffer = new byte[0];
            Union.OverheadLeagueInfo Packet = new Union.OverheadLeagueInfo();
            if (client.Union != null)
            {
                Packet = new Union.OverheadLeagueInfo()
                {
                    Type = 0,
                    UID = client.Entity.UID,
                    UnionID = client.Union.ID,
                    PlayerRank = client.Union.IsLeader(client.Entity.UID) ? (uint)1 : (uint)0,
                    UnionRank = client.Union.IsKingdom() ? (uint)1 : (uint)0,
                    Name = client.Union.Name,
                    ServerID = client.Entity.ServerID
                };
                if (Packet.UnionRank == 1)
                    Packet.Name = client.Union.Title == "" ? client.Union.Name : client.Union.Title + "KDM";

                client.Entity.UnionName = Packet.Name;
                client.UnionRank = (byte)Packet.PlayerRank;
                client.UnionType = (byte)Packet.UnionRank;
                client.UnionID = Packet.UnionID;
            }
            else
            {
                if (client.Union != null)
                {
                    Packet = new Union.OverheadLeagueInfo()
                    {
                        Type = 0,
                        UID = client.Entity.UID,
                        UnionID = 0,
                        PlayerRank = 0,
                        UnionRank = 0,
                        Name = "",
                        ServerID = client.Entity.ServerID
                    };
                    client.Entity.UnionName = Packet.Name;
                    client.UnionRank = (byte)Packet.PlayerRank;
                    client.UnionType = (byte)Packet.UnionRank;
                    client.UnionID = Packet.UnionID;
                }
            }
            using (var stream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(stream, Packet, PrefixStyle.Fixed32);
                buffer = new byte[stream.Length + 8];
                Array.Copy(stream.ToArray(), 0, buffer, 0, stream.Length);
                fixed (byte* ptr1 = buffer)
                {
                    *((ushort*)ptr1 + 0) = (ushort)(buffer.Length - 8);
                    *((ushort*)(ptr1 + 2)) = (ushort)PacketID.MsgOverheadLeagueInfo;
                }
            }
            if (client.Entity.EntityFlag == Revora.Game.EntityFlag.Player)
                client.Entity.Update((byte)FlagsUpdate.DataType.UnionRank, (ushort)(client.Offical + client.Harem + client.Guards), true);
            return buffer;
        }

        #endregion
        #region UpdateData
        private void UpdateDatabase(string column, byte value)
        {
            new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions").Set(column, value).Where("ID", ID).Execute();
        }
        private void UpdateDatabase(string column, long value)
        {
            new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions").Set(column, value).Where("ID", ID).Execute();
        }
        private void UpdateDatabase(string column, ulong value)
        {
            new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions").Set(column, value).Where("ID", ID).Execute();
        }
        private void UpdateDatabase(string column, string value)
        {
            new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions").Set(column, value).Where("ID", ID).Execute();
        }
        private void UpdateDatabase(string column, bool value)
        {
            new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions").Set(column, value).Where("ID", ID).Execute();
        }
        #endregion

        public static unsafe GameState client { get; set; }
    }
}
