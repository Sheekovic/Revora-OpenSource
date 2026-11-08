namespace Revora.Gameplay.Models;

public enum StaminaActivity : byte
{
    Standing = 0,
    Sitting = 1,
    Moving = 2,
    Attacking = 3,
    Dead = 4
}

public readonly record struct StaminaSnapshot(
    ushort Current,
    ushort Maximum,
    StaminaActivity Activity,
    long LastActivityMilliseconds,
    long LastRegenerationMilliseconds)
{
    public StaminaSnapshot Validate()
    {
        if (Maximum == 0 || Current > Maximum || LastActivityMilliseconds < 0 ||
            LastRegenerationMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(Current));
        return this;
    }
}

public readonly record struct StaminaTickResult(StaminaSnapshot Snapshot, bool Changed);
