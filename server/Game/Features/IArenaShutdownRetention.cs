using Revora.Client;
namespace Revora.Game.Features
{
    internal interface IArenaShutdownRetention
    {
        bool ShouldDefer(GameState player);
    }
}
