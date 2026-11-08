namespace Revora.Game.Features
{
    // Local tournament coordinators share one transition/damage ordering domain.
    // Owner inventory locks precede this gate; timer code never acquires them inside it.
    internal static class TournamentCombat
    {
        internal static readonly object Gate=new object();
    }
}
