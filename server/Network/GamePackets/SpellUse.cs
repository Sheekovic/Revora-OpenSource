using System.Linq;
using System.Collections.Generic;
using ProtoBuf;

namespace Revora.Network.GamePackets
{
    public unsafe class SpellUse : Interfaces.IPacket
    {
        public class DamageClass
        {
            public uint Damage;
            public bool Hit;
            public ushort newX, newY;
            public Attack.AttackEffects Eff1;
            public Attack.AttackEffects2 Eff2;
            public static implicit operator uint(DamageClass dmg)
            {
                return dmg.Damage;
            }
            public static implicit operator DamageClass(uint dmg)
            {
                return new DamageClass
                {
                    Damage = dmg,
                    Hit = true
                };
            }
        }
        [ProtoContract]
        public class CMsgMagicEffect
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Attacker;
            [ProtoMember(2, IsRequired = false)]
            public uint Attacker1;
            [ProtoMember(3, IsRequired = true)]
            public uint X;
            [ProtoMember(4, IsRequired = true)]
            public uint Y;
            [ProtoMember(5, IsRequired = true)]
            public uint SpellID;
            [ProtoMember(6, IsRequired = true)]
            public uint SpellLevel;
            [ProtoMember(7, IsRequired = false)]
            public uint LevelHu;
            [ProtoMember(8, IsRequired = true)]
            public uint SpellEffect;
            [ProtoMember(9, IsRequired = true)]
            public uint unkown;
            [ProtoMember(10, IsRequired = true)]
            public List<CMsgMagicattacked> Targets;
        }
        [ProtoContract]
        public class CMsgMagicattacked
        {
            [ProtoMember(1, IsRequired = true)]
            public uint UID;
            [ProtoMember(2, IsRequired = true)]
            public uint Damage;
            [ProtoMember(3, IsRequired = true)]
            public uint Hit;
            [ProtoMember(4, IsRequired = true)]
            public uint Effect;
            [ProtoMember(5, IsRequired = true)]
            public uint Effect2;
            [ProtoMember(6, IsRequired = true)]
            public uint SpellLevel;
            [ProtoMember(7, IsRequired = false)]
            public uint newX;
            [ProtoMember(8, IsRequired = true)]
            public uint newY;
        }
        CMsgMagicEffect Buffer;
        public SpellUse(bool Create)
        {
            if (Create)
            {
                Buffer = new CMsgMagicEffect();
                Buffer.Targets = new List<CMsgMagicattacked>();
            }
        }
        public uint Attacker
        {
            get { return Buffer.Attacker; }
            set { Buffer.Attacker = value; }
        }
        public uint Attacker1
        {
            get { return Buffer.Attacker1; }
            set { Buffer.Attacker1 = value; }
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
        public ushort SpellID
        {
            get { return (ushort)Buffer.SpellID; }
            set { Buffer.SpellID = value; }
        }
        public ushort SpellLevel
        {
            get { return (ushort)Buffer.SpellLevel; }
            set { Buffer.SpellLevel = value; }
        }
        public byte LevelJH
        {
            get { return (byte)Buffer.LevelHu; }
            set { Buffer.LevelHu = value; }
        }
        public ushort SpellEffect
        {
            get { return (byte)Buffer.unkown; }
            set { Buffer.unkown = value; }
        }
        public Attack.AttackEffects Effect
        {
            get;
            set;
        }
        public SafeDictionary<uint, DamageClass> Targets = new SafeDictionary<uint, DamageClass>();
        ushort PacketID = 1105;
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer.ConvertToProto<CMsgMagicEffect>();
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer.ToConquerPacket(PacketID));
        }
        const int TargetLimit = 30;
        public byte[] ToArray()
        {
            Buffer.Targets.Clear();
            if (Kernel.GamePool.ContainsKey(Attacker))
                if (Kernel.GamePool[Attacker].Spells != null)
                    if (Kernel.GamePool[Attacker].Spells[SpellID] != null)
                        Buffer.SpellEffect = Kernel.GamePool[Attacker].Spells[SpellID].LevelHu2;

            foreach (KeyValuePair<uint, DamageClass> target in Targets.Take(30))
            {
                if (SpellID == 10490 || SpellID == 11140)
                {
                    Buffer.Targets.Add(new CMsgMagicattacked() { UID = target.Key, Damage = target.Value.Damage / 3, Effect = (uint)target.Value.Eff1, SpellLevel = target.Value.newX, newX = target.Value.newY });
                    Buffer.Targets.Add(new CMsgMagicattacked() { UID = target.Key, Damage = target.Value.Damage / 3, Effect = (uint)target.Value.Eff1, SpellLevel = target.Value.newX, newX = target.Value.newY });
                    Buffer.Targets.Add(new CMsgMagicattacked() { UID = target.Key, Damage = target.Value.Damage / 3, Effect = (uint)target.Value.Eff1, SpellLevel = target.Value.newX, newX = target.Value.newY });
                }
                else
                    Buffer.Targets.Add(new CMsgMagicattacked() { UID = target.Key, Damage = target.Value.Damage, Effect = (uint)target.Value.Eff1, SpellLevel = target.Value.newX, newX = target.Value.newY });
            }
            return Buffer.ToConquerPacket(PacketID);
        }


        internal SpellUse AddTarget(uint uid, DamageClass damage, Attack attack)
        {
            if (attack != null)
            {
                damage.Eff1 = attack.Effect;
            }
            Targets.Add(uid, damage);
            return this;
        }
    }
}