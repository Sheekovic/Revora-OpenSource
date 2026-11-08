// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class SecondaryPassword
    {
        public enum Mode : byte
        {
            Show,
            VerifiedPassword,
            ForgetPassword,
            SetNewPass,
            SendInformation,
            PasswordCorrect,
            PasswordWrong,
            ForgetTime,
            TerminateTime
        }
        private byte[] Packet;
        public SecondaryPassword(bool Create)
        {
            if (Create)
            {
                Packet = new byte[30];
                fixed (byte* Pack = Packet)
                {
                    *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                    *((ushort*)(Pack + 2)) = PacketID.SecondaryPassword;
                }
            }
        }
        public Mode Action
        {
            get { return (Mode)Packet[4]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 4)) = (byte)value; }
        }
        public uint OldPassword
        {
            get { return BitConverter.ToUInt16(Packet, 8); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 8)) = value; }
        }
        public uint NewPassword
        {
            get { return BitConverter.ToUInt16(Packet, 12); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 12)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
        public void Deserialize(byte[] buffer)
        {
            Packet = buffer;
        }
        public static void Process(byte[] Packet, Client.GameState client)
        {
            SecondaryPassword SP = new SecondaryPassword(false);
            SP.Deserialize(Packet);
            switch (SP.Action)
            {
                case SecondaryPassword.Mode.Show:
                    {
                        if (client.WarehousePW != 0)
                        {
                            SP = new SecondaryPassword(true);
                            SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                            SP.OldPassword = 0x01;
                            client.Send(SP.ToArray());
                            client.Send("You have terminated forgetting the secondary password.");
                            client.ForgetPassword = false;
                        }
                        break;
                    }
                case SecondaryPassword.Mode.VerifiedPassword:
                    {
                        if (client.WarehousePW != 0)
                        {
                            if (client.WarehousePW == SP.NewPassword)
                            {
                                client.WarehouseOpen = true;
                                SP = new SecondaryPassword(true);
                                SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                                SP.OldPassword = 0x0101;
                                client.Send(SP.ToArray());
                                client.Send("Password verified!");
                            }
                            else
                            {
                                SP = new SecondaryPassword(true);
                                SP.Action = SecondaryPassword.Mode.PasswordWrong;
                                client.Send(SP.ToArray());
                            }
                        }
                        else
                        {
                            client.Disconnect();
                        }
                        break;
                    }
                case SecondaryPassword.Mode.ForgetPassword:
                    {
                        if (client.WarehousePW != 0)
                        {
                            
                            uint value = Kernel.ToDateTimeUInt(DateTime.Now.AddDays(7));
                            SP = new SecondaryPassword(true);
                            SP.Action = SecondaryPassword.Mode.ForgetTime;
                            SP.OldPassword = 0x1;
                            SP.NewPassword = value;
                            client.Send(SP.ToArray());
                            client.Send("Your secondary password is now in the forgetting phase. it will be cleared in 7 days uf you don't terminate.");
                            client.ForgetPassword = true;
                        }
                        break;
                    }
                case SecondaryPassword.Mode.SetNewPass:
                    {
                        if (client.WarehousePW == 0)
                        {
                            client.WarehouseOpen = true;
                            client.WarehousePW = SP.NewPassword;
                            SP = new SecondaryPassword(true);
                            SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                            SP.OldPassword = 0x0101;
                            client.Send(SP.ToArray());
                            client.Send("Successfully set! Please remember your secondary password");
                        }
                        break;
                    }
                case SecondaryPassword.Mode.SendInformation:
                    {
                        if (client.WarehousePW != 0)
                        {
                            if (client.WarehousePW == SP.OldPassword)
                            {
                                client.WarehouseOpen = true;
                                client.WarehousePW = SP.NewPassword;
                                SP = new SecondaryPassword(true);
                                SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                                SP.OldPassword = 0x0101;
                                client.Send(SP.ToArray());
                                client.Send("Successfully modified! Please remember your secondary password");
                            }
                            else
                            {
                                SP = new SecondaryPassword(true);
                                SP.Action = SecondaryPassword.Mode.PasswordWrong;
                                SP.OldPassword = 0x01;
                                client.Send(SP.ToArray());
                            }
                        }
                        else
                        {
                            client.WarehouseOpen = true;
                            client.WarehousePW = SP.NewPassword;
                            SP = new SecondaryPassword(true);
                            SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                            SP.OldPassword = 0x0101;
                            client.Send(SP.ToArray());
                            client.Send("Successfully set! Please remember your secondary password");
                        }
                        break;
                    }
                default: Console.WriteLine("[SecondaryPassword] Unknown Action " + SP.Action); break;
            }
        }
    }
}