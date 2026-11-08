// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System.IO;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class MailboxTable
    {
        public static void Load(Client.GameState client)
        {
            Revora.Game.Features.MailRewards.Load(client);
        }
        public static void Save(Client.GameState client)
        {
            try { Revora.Game.Features.MailRewards.Save(client); }
            catch (System.Exception ex) { Revora.Game.Features.QuestInventory.Fail(client, "mail-save", ex); }
        }
    }
}
