// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Game.ConquerStructures
{
    public unsafe class CheckAlive
    {
        public static int Alive = 0;
        public static void CouplesPk()
        {
            Alive = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 1090 && client.Entity.Hitpoints >= 1)
                {
                    if (client.Entity.Body == 1003 || client.Entity.Body == 1004)
                    {
                        Alive += 1;
                        Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Teams Alive in CouplesPk: " + Alive + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);

                    }
                }
            }
        }
    }
}