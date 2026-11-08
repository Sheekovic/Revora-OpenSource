using ProtoBuf;
using System;

namespace Revora.Network.GamePackets
{
    public unsafe class Attack : Interfaces.IPacket
    {
        [ProtoContract]
        public class CMsgInteract
        {
            [ProtoMember(1, IsRequired = false)]
            public ulong TimeStamp;
            [ProtoMember(2, IsRequired = false)]
            public ulong Unkown;
            [ProtoMember(3, IsRequired = false)]
            public ulong Unkown1;
            [ProtoMember(4, IsRequired = false)]
            public uint Attacker;
            [ProtoMember(5, IsRequired = false)]
            public uint Attacked;
            [ProtoMember(6, IsRequired = false)]
            public uint unkown2;
            [ProtoMember(7, IsRequired = false)]
            public uint X;
            [ProtoMember(8, IsRequired = false)]
            public uint Y;
            [ProtoMember(9, IsRequired = false)]
            public uint unkown0;
            [ProtoMember(10, IsRequired = false)]
            public uint unkown3;
            [ProtoMember(11, IsRequired = false)]
            public uint unkown4;
            [ProtoMember(12, IsRequired = false)]
            public uint ResponseDamage;
            [ProtoMember(13, IsRequired = false)]
            public uint Type;
            [ProtoMember(14, IsRequired = false)]
            public uint unkown5;
            [ProtoMember(15, IsRequired = false)]
            public uint Damage;
            [ProtoMember(16, IsRequired = false)]
            public uint unkown7;
            [ProtoMember(17, IsRequired = false)]
            public uint ClientSpellID;
            [ProtoMember(18, IsRequired = false)]
            public uint KOCount;
            [ProtoMember(19, IsRequired = false)]
            public uint EncodingSpellID;
            [ProtoMember(20, IsRequired = false)]
            public uint SpellLevel;
            [ProtoMember(21, IsRequired = false)]
            public uint unkown11;
            [ProtoMember(22, IsRequired = false)]
            public uint unkown12;
            [ProtoMember(23, IsRequired = false)]
            public uint unkown13;
            [ProtoMember(24, IsRequired = false)]
            public uint unkown14;
            [ProtoMember(25, IsRequired = false)]
            public uint Effect1;
            [ProtoMember(26, IsRequired = false)]
            public uint unkown17;
        }
        public const uint

        Melee = 2,
        MarriageRequest = 8,
        MarriageAccept = 9,
        Kill = 14,
        Magic = 24,
        Reflect = 26,
        Dash = 27,
        Ranged = 28,
        MonkMelee = 34,
        CloudSaints = 36,
        ClaimCP = 37,
        Obtained = 39,
        MerchantAccept = 40,
        MerchantRefuse = 41,
        MerchantProgress = 42,
        Scapegoat = 43,
        CounterKillSwitch = 44,
        FatalStrike = 45,
        InteractionRequest = 46,
        InteractionAccept = 47,
        InteractionRefuse = 48,
        InteractionEffect = 49,
        InteractionStopEffect = 50,
        ShowUseSpell = 52,
        SkillMove = 53,
        BlueDamage = 55,
        BackFire = 57;

        CMsgInteract Buffer;
        ushort packetID = 1022;
        public Attack(bool Create)
        {
            if (Create)
            {
                Buffer = new CMsgInteract();
                TimeStamp = (uint)Time32.Now.GetHashCode();
            }
        }
        public uint TimeStamp
        {
            get { return (uint)Buffer.TimeStamp; }
            set { Buffer.TimeStamp = value; }
        }
        public uint Attacker
        {
            get { return Buffer.Attacker; }
            set { Buffer.Attacker = value; }
        }
        public ushort MagicType
        {
            get { return (ushort)Damage; }
            set { Damage = (uint)((MagicLevel << 16) | value); }
        }
        public ushort MagicLevel
        {
            get { return (ushort)(Damage >> 16); }
            set { Damage = (uint)((value << 16) | MagicType); }
        }
        public uint Attacked
        {
            get { return Buffer.Attacked; }
            set { Buffer.Attacked = value; }
        }
        public ushort X
        {
            get { return (ushort)Buffer.X; }
            set { Buffer.X = value; }
        }
        public ushort Y
        {
            get { return (ushort)Buffer.Y; }
            set { Buffer.Y = value; }
        }
        public uint AttackType
        {
            get { return Buffer.Type; }
            set { Buffer.Type = value; }
        }
        public uint Damage
        {
            get { return Buffer.Damage; }
            set { Buffer.Damage = value; }
        }
        public ushort SpellID
        {
            get { return (ushort)Buffer.ClientSpellID; }
            set { Buffer.ClientSpellID = value; }
        }
        public uint MonsterSpellID
        {
            get { return Buffer.Damage; }
            set { Buffer.Damage = value; }
        }
        public ushort KOCount
        {
            get { return (ushort)Buffer.KOCount; }
            set { Buffer.KOCount = value; }
        }
        public uint ResponseDamage
        {
            get { return Buffer.Damage; }
            set { Buffer.Damage = value; }
        }
        public uint EncodingSpellID { get { return Buffer.EncodingSpellID; } }
        public bool weaponspell = false;
        public AttackEffects Effect
        {
            get { return (AttackEffects)Buffer.Effect1; }
            set { Buffer.Effect1 = (uint)value; }
        }
        public AttackEffects2 Effect2
        {
            get { return (AttackEffects2)Buffer.Effect1; }
            set { Buffer.Effect1 = (uint)value; }
        }
        [Flags]
        public enum AttackEffects
        {
            None = 0,
            Block = 1 << 0,
            Penetration = 1 << 1,
            CriticalStrike = 1 << 2,
            Unknown = 1 << 3,
            MetalResist = 1 << 4,
            Immu = 8,
            Break = 10,
            WoodResist = 1 << 5,
            WaterResist = 1 << 6,
            FireResist = 1 << 7,
            EarthResist = 1 << 8,
            AddStudyPoint = 1 << 9,
            //Runes
            TortoiseBreaker = 1 << 12,
            Acalanatha = 1 << 13,
            Assassin = 1 << 14,
            BurningSky = 1 << 15,
            FireCurse = 1 << 16
        }
        [Flags]
        public enum AttackEffects2
        {
            None = 0,
            EarthResist = 1 << 0,
            StudyPoints = 1 << 1
        }
        public bool Decoded = false;
        public bool CheckWeponSpell = false;
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }

        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(packetID);
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer.ConvertToProto<CMsgInteract>();
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            client.LastAttack = Time32.Now;
            Attack attack = new Attack(false);
            attack.Deserialize(packet);
            Writer.WriteUInt32(0, 8, attack.ToArray());
            HandleAttack(attack, client);
            switch (attack.AttackType)
            {
                case Attack.CloudSaints:
                    {
                        CloudSaintsJar.Calculate(client, attack);
                        break;
                    }
                case Attack.Obtained:
                    {
                        client.Entity.AddConquerPoints(client.TQPoint);
                        attack.ResponseDamage = client.TQPoint;
                        client.TQPoint -= client.TQPoint;
                        client.Send(packet);
                        break;
                    }
                case Attack.MerchantAccept:
                    {
                        client.Entity.Merchant = 255;
                        client.Entity.Update((byte)FlagsUpdate.DataType.Merchant, 255, 0);
                        client.Send(packet);
                        break;
                    }
                case Attack.MerchantRefuse:
                    {
                        client.Entity.Update((byte)FlagsUpdate.DataType.Merchant, 0, 0);
                        Database.MerchantTable.Cancel(client);
                        client.Send(packet);
                        break;
                    }
                //default: Console.WriteLine("[Attack] Unknown Type: " + attack.AttackType + ""); break;
            }
        }
        public static void HandleAttack(Attack attack, Client.GameState client)
        {
            #region Absolution
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Absolution))
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Absolution);
                FlagsUpdate upgrade2 = new FlagsUpdate(true);
                upgrade2.UID = client.Entity.UID;
                upgrade2.Append(106, 0, 0, 0, 0);
                client.Send(upgrade2.ToArray());
            }
            #endregion
            client.Entity.SpiritFocus = false;
            bool cancelInspection = client.Entity.OnCheckGuard || client.Entity.OnCheckSaqueo;
            client.Entity.OnCheckGuard = false;
            client.Entity.OnCheckSaqueo = false;
            if (cancelInspection) DialyQuestsEffect.CheckCancelGuard(client);
            client.Entity.AttackPacket = attack;
            new Game.Attacking.Handle(attack, client.Entity, null);
        }
    }
}