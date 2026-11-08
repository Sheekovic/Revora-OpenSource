// Revora mining entry point used by the existing player timer.
namespace Revora.Game.ConquerStructures
{
    public static class Mining
    {
        public static void Mine(Client.GameState client)
        {
            Features.MiningRewards.Tick(client);
        }
    }
}
