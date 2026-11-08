// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class CloudSaintsJar
    {
        public static void Calculate(Client.GameState client, Attack Packet)
        {
            if (!TitleRequirements.AllowRequest(client)) return;
            client.Send(new Attack(true) {
                AttackType=Attack.CloudSaints, Attacker=client.Entity.UID,
                ResponseDamage=Revora.Game.Features.DemonExterminators.JarCount(client)
            });
        }
    }
}
