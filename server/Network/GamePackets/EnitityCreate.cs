using System;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class EnitityCreate : Interfaces.IPacket
    {
        public string Name;
        public ushort Body;
        public byte Class;
        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }
        public void Deserialize(byte[] buffer)
        {
            Name = Encoding.Default.GetString(buffer, 24, 16).Replace("\0", "");
            Body = BitConverter.ToUInt16(buffer, 72);
            Class = (byte)BitConverter.ToUInt16(buffer, 74);
        }
        public void Send(Client.GameState client)
        {
            throw new NotImplementedException();
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action == 1)
            {
                EnitityCreate EC = new EnitityCreate();
                EC.Deserialize(packet);
                string rawName = EC.Name ?? "";
                EC.Name = EC.Name.Remove("pm").Remove("gm").Remove("guard").Remove("owner");
                string createLine = "[game] 1001 name=" + EC.Name
                    + " rawlen=" + rawName.Length
                    + " body=" + EC.Body
                    + " class=" + EC.Class;
                Console.WriteLine(createLine);
                System.IO.File.AppendAllText("game-create.log",
                    DateTime.UtcNow.ToString("o") + " " + createLine + Environment.NewLine);
                if (string.IsNullOrEmpty(EC.Name) || EC.Body == 0)
                    return;
                string Message = "";
                Boolean Created = false;
                try
                {
                    Created = Database.EntityTable.CreateEntity(EC, client, ref Message);
                }
                catch (Exception ex)
                {
                    Created = false;
                    Message = "Could not create the character.";
                    Console.WriteLine("[game] create-throw " + ex.GetType().Name + " " + ex.Message);
                    System.IO.File.AppendAllText("game-create.log",
                        DateTime.UtcNow.ToString("o") + " create-throw " + ex.GetType().Name + " " + ex.Message + Environment.NewLine);
                }
                Console.WriteLine("[game] create ok=" + Created + " msg=" + Message);
                System.IO.File.AppendAllText("game-create.log",
                    DateTime.UtcNow.ToString("o") + " create ok=" + Created + " msg=" + Message + Environment.NewLine);
                if (Created)
                {
                    // The character row was saved only after this session owned the id.
                    // Hold ownership through success and login, including the success packet.
                    uint creationUid = client.Account.EntityID;
                    if (!Revora.Game.Features.SessionInstall.EnterCurrent(creationUid, client))
                    {
                        client.JustCreated = false;
                        client.Send(new Message("Could not create the character.", "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                        return;
                    }
                    try
                    {
                        client.JustCreated = true;
                        client.Send(new Message("ANSWER_OK", "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.PopUP));
                        GeneralData.DoLogin(client);
                    }
                    finally { Revora.Game.Features.SessionInstall.Exit(creationUid); }
                    Console.WriteLine("[game] created " + client.Account.Username + " " + EC.Name + " uid=" + client.Entity.UID);
                }
                else
                {
                    if (string.IsNullOrEmpty(Message))
                        Message = "Could not create the character.";
                    client.Send(new Message(Message, "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                }
            }
        }
    }
}
