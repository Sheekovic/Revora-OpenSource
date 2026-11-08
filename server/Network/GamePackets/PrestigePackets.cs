using System;
using System.IO;
using System.Linq;
using System.Text;
using ProtoBuf;

namespace Revora.Network.GamePackets
{
    public static class PrestigePackets
    {
        private static byte[] Ranking(uint mode, uint category, ushort page, int count, int registered)
        {
            if (count < 0 || count > 11) throw new ArgumentOutOfRangeException("count");
            // Genuine receiver steps 80 bytes. The old 744-byte buffer lost row ten.
            var packet = new byte[24 + count * 80 + 8];
            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            Writer.WriteUInt16(1151, 2, packet);
            Writer.WriteUInt32(mode, 4, packet);
            Writer.WriteUInt32(category, 8, packet);
            Writer.WriteUInt16((ushort)Math.Min(registered, ushort.MaxValue), 12, packet);
            Writer.WriteUInt16(page, 14, packet);
            Writer.WriteUInt32((uint)count, 16, packet);
            return packet;
        }
        private static void Row(byte[] packet, int index, uint rank, PrestigeEntry e, uint category = 0)
        {
            int offset = 24 + index * 80;
            Writer.WriteUInt32(rank, offset, packet);
            Writer.WriteUInt32(e.Total, offset + 8, packet);
            Writer.WriteUInt32(category == 0 ? e.Profile.UID : category, offset + 16, packet);
            Writer.WriteUInt32(e.Profile.UID, offset + 20, packet);
            var name = Encoding.ASCII.GetBytes(e.Profile.Name ?? "");
            Array.Copy(name, 0, packet, offset + 24, Math.Min(15, name.Length));
            Array.Copy(name, 0, packet, offset + 40, Math.Min(15, name.Length));
            Writer.WriteUInt32(e.Profile.Level, offset + 56, packet);
            Writer.WriteUInt32(e.Profile.Class, offset + 60, packet);
            Writer.WriteUInt64(e.Profile.Mesh, offset + 64, packet);
        }
        public static byte[] Self(PrestigeEntry[] all, PrestigeEntry self, uint category = 80000000)
        {
            var packet = Ranking(2, category, 0, 1, Math.Min(100, all.Length));
            // The client reads rank as a byte; the supported board is top 100.
            int place = Array.FindIndex(all, e => e.Profile.UID == self.Profile.UID) + 1;
            Row(packet, 0, (uint)(place <= 100 ? place : 0), self);
            return packet;
        }
        public static byte[] Page(PrestigeEntry[] ranked, uint category, ushort page)
        {
            if (!PrestigeRanking.IsPrestige(category)) throw new ArgumentOutOfRangeException("category");
            var top = ranked.Take(100).ToArray();
            var rows = top.Skip(page * 10).Take(10).ToArray();
            var packet = Ranking(1, category, page, rows.Length, top.Length);
            for (int i = 0; i < rows.Length; i++) Row(packet, i, (uint)(page * 10 + i + 1), rows[i]);
            return packet;
        }
        public static byte[] Winners(PrestigeEntry[] all)
        {
            var winners = new System.Collections.Generic.List<PrestigeEntry>();
            if (all.Length > 0) winners.Add(all[0]);
            winners.AddRange(all.Where(e => PrestigeRanking.ClassCategory(e.Profile.Class) != 0)
                .GroupBy(e => PrestigeRanking.ClassCategory(e.Profile.Class)).Select(g => g.First())
                .OrderBy(e => PrestigeRanking.ClassCategory(e.Profile.Class)));
            var packet = Ranking(6, 0, 0, winners.Count, all.Length);
            for (int i = 0; i < winners.Count; i++)
                Row(packet, i, 1, winners[i], i == 0 ? 80000000 : PrestigeRanking.ClassCategory(winners[i].Profile.Class));
            return packet;
        }

        [ProtoContract]
        public sealed class Showcase
        {
            [ProtoMember(1, IsRequired = true)] public uint Action;
            [ProtoMember(2)] public Member[] Members;
        }
        [ProtoContract]
        public sealed class Member
        {
            [ProtoMember(1, IsRequired = true)] public uint Kind = 1;
            [ProtoMember(2, IsRequired = true)] public uint Parameter;
            [ProtoMember(3, IsRequired = true)] public uint UID;
            [ProtoMember(4, IsRequired = true)] public string Name = "";
            [ProtoMember(5, IsRequired = true)] public string Guild = "";
            [ProtoMember(6, IsRequired = true)] public uint Mesh;
            [ProtoMember(7, IsRequired = true)] public uint Hair;
            [ProtoMember(8, IsRequired = true)] public uint Hat;
            [ProtoMember(9, IsRequired = true)] public uint Garment;
            [ProtoMember(10, IsRequired = true)] public uint LeftWeapon;
            [ProtoMember(11, IsRequired = true)] public uint LeftAccessory;
            [ProtoMember(12, IsRequired = true)] public uint RightWeapon;
            [ProtoMember(13, IsRequired = true)] public uint RightAccessory;
            [ProtoMember(14, IsRequired = true)] public uint MountArmor;
            [ProtoMember(15, IsRequired = true)] public uint Armor;
            [ProtoMember(16, IsRequired = true)] public uint Wing;
            [ProtoMember(17, IsRequired = true)] public uint WingPlus;
            [ProtoMember(18, IsRequired = true)] public uint Title;
            [ProtoMember(19, IsRequired = true)] public uint Flag;
        }
        public static byte[] Appearance(PrestigeEntry entry)
        {
            var response = new Showcase { Action = 0, Members = new Member[0] };
            if (entry != null)
            {
                var p = entry.Profile;
                Func<ushort, uint> gear = slot => {
                    var item = p.AlternateEquipment && slot <= 9
                        ? p.Items.FirstOrDefault(i => i.Position == slot + 20) : null;
                    if (item == null) item = p.Items.FirstOrDefault(i => i.Position == slot);
                    return item == null ? 0 : item.ID;
                };
                var wing = p.Items.FirstOrDefault(i => i.Position == ConquerItem.Wing);
                uint cosmeticWing=p.VisibleCosmeticWing(DateTime.UtcNow);
                response.Members = new[] { new Member {
                    UID = p.UID, Name = p.Name, Guild = p.Guild, Mesh = p.Mesh, Hair = p.HairStyle, Title = p.Title,
                    Hat = gear(ConquerItem.Head), Garment = gear(ConquerItem.Garment), Armor = gear(ConquerItem.Armor),
                    LeftWeapon = gear(ConquerItem.LeftWeapon), RightWeapon = gear(ConquerItem.RightWeapon),
                    LeftAccessory = gear(ConquerItem.LeftWeaponAccessory), RightAccessory = gear(ConquerItem.RightWeaponAccessory),
                    MountArmor = gear(ConquerItem.SteedArmor),
                    Wing = cosmeticWing != 0 ? cosmeticWing : wing == null ? 0 : wing.ID,
                    WingPlus = wing == null ? 0u : wing.Plus
                } };
            }
            return EncodeShowcase(response);
        }
        public static byte[] Unavailable()
        {
            // Confirmed action 1 invokes the client's unavailable-player message.
            return EncodeShowcase(new Showcase { Action = 1 });
        }
        private static byte[] EncodeShowcase(Showcase response)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, response);
                var packet = new byte[stream.Length + 12];
                Array.Copy(stream.ToArray(), 0, packet, 4, stream.Length);
                Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
                Writer.WriteUInt16(3257, 2, packet);
                return packet;
            }
        }

        // Only the two confirmed request actions. Bounded protobuf varints/fields;
        // no deserialization of attacker-controlled repeated members or recursion.
        public static bool TryReadRequest(byte[] packet, out uint action, out uint target)
        {
            action = target = 0;
            if (packet == null || packet.Length < 14 || packet.Length > 256
                || BitConverter.ToUInt16(packet, 0) != packet.Length - 8
                || BitConverter.ToUInt16(packet, 2) != 3257) return false;
            int pos = 4, end = packet.Length - 8;
            bool seenAction = false, seenMember = false;
            while (pos < end)
            {
                uint tag, value;
                if (!ReadVarint(packet, ref pos, end, out tag)) return false;
                if (tag == 8 && !seenAction)
                {
                    if (!ReadVarint(packet, ref pos, end, out action) || action > 1) return false;
                    seenAction = true;
                }
                else if (tag == 18 && !seenMember)
                {
                    seenMember = true;
                    if (!ReadVarint(packet, ref pos, end, out value) || value > end - pos) return false;
                    int memberEnd = pos + (int)value;
                    bool seenParameter = false;
                    while (pos < memberEnd)
                    {
                        if (!ReadVarint(packet, ref pos, memberEnd, out tag) || (tag != 8 && tag != 16 && tag != 24)
                            || !ReadVarint(packet, ref pos, memberEnd, out value)) return false;
                        if (tag == 16) { if (seenParameter) return false; target = value; seenParameter = true; }
                    }
                }
                else return false;
            }
            return seenAction;
        }
        private static bool ReadVarint(byte[] data, ref int pos, int end, out uint value)
        {
            value = 0;
            for (int shift = 0; shift <= 28 && pos < end; shift += 7)
            {
                byte b = data[pos++];
                if (shift == 28 && b > 15) return false;
                value |= (uint)(b & 127) << shift;
                if ((b & 128) == 0) return true;
            }
            return false;
        }
    }
}
