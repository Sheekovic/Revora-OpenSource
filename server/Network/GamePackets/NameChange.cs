namespace Revora.Network.GamePackets
{
    using Revora;
    using Revora.Client;
    using Revora.Interfaces;
    using Revora.Network;
    using System;
    using System.Text;

    public class NameChange : Writer, IPacket
    {
        private NameChangeAction _Action;
        public ushort _EditAllowed = 1;
        public ushort _EditCount;
        private string _name;
        private byte[] Buffer;

        public NameChange(bool Create)
        {
            if (Create)
            {
                this.Buffer = new byte[0x22];
                Writer.WriteUInt16((ushort)this.Buffer.Length, 0, this.Buffer);
                Writer.WriteUInt16(0x820, 2, this.Buffer);
            }
        }

        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
        }

        public void Send(GameState client)
        {
            client.Send(this.Buffer);
        }

        public byte[] ToArray()
        {
            return this.Buffer;
        }

        public NameChangeAction Action
        {
            get
            {
                return this._Action;
            }
            set
            {
                this._Action = (NameChangeAction)this.Buffer[4];
            }
        }

        public ushort EditAllowed
        {
            get
            {
                return this._EditAllowed;
            }
            set
            {
                this._EditAllowed = Revora.BitConverter.ToUInt16(this.Buffer, 8);
            }
        }

        public ushort EditCount
        {
            get
            {
                return this._EditCount;
            }
            set
            {
                this._EditCount = Revora.BitConverter.ToUInt16(this.Buffer, 6);
            }
        }

        public string Name
        {
            get
            {
                return this._name;
            }
            set
            {
                this._name = Encoding.ASCII.GetString(this.Buffer, 10, 0x10).TrimEnd(new char[1]);
            }
        }

        public enum NameChangeAction : ushort
        {
            DialogInfo = 3,
            FreeChange = 4,
            NameTaken = 2,
            Request = 0,
            Success = 1
        }
    }
}

