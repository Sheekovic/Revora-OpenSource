using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

/// <summary>
/// Generic server-authoritative 5165 Stamina rules adapted from the authorized
/// owner source Character.Stamina/Character.Step implementation. All timing is
/// monotonic and supplied by the caller for deterministic testing.
/// </summary>
public sealed class StaminaResource
{
    public const ushort OwnerSourceMaximum = 100;
    public const ushort StandingRegeneration = 3;
    public const ushort SittingRegeneration = 15;
    public const long RegenerationIntervalMilliseconds = 1_000;
    public const long ActivityQuietPeriodMilliseconds = 500;

    private readonly object gate = new();
    private StaminaSnapshot state;

    public StaminaResource(ushort current, long monotonicMilliseconds,
        ushort maximum = OwnerSourceMaximum)
    {
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        state = new StaminaSnapshot(current, maximum, StaminaActivity.Standing,
            monotonicMilliseconds, monotonicMilliseconds).Validate();
    }

    public StaminaSnapshot Snapshot()
    {
        lock (gate) return state;
    }

    public StaminaSnapshot SetActivity(StaminaActivity activity, long monotonicMilliseconds)
    {
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        lock (gate)
        {
            if (monotonicMilliseconds < state.LastActivityMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
            state = state with { Activity = activity, LastActivityMilliseconds = monotonicMilliseconds };
            return state;
        }
    }

    public StaminaTickResult Regenerate(long monotonicMilliseconds)
    {
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        lock (gate)
        {
            if (monotonicMilliseconds < state.LastRegenerationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
            long elapsed = monotonicMilliseconds - state.LastRegenerationMilliseconds;
            long intervals = elapsed / RegenerationIntervalMilliseconds;
            if (intervals == 0) return new(state, false);
            long consumedThrough = checked(state.LastRegenerationMilliseconds +
                intervals * RegenerationIntervalMilliseconds);
            if (state.Activity == StaminaActivity.Dead ||
                monotonicMilliseconds - state.LastActivityMilliseconds < ActivityQuietPeriodMilliseconds ||
                state.Current == state.Maximum)
            {
                state = state with { LastRegenerationMilliseconds = consumedThrough };
                return new(state, false);
            }
            ushort amount = state.Activity == StaminaActivity.Sitting
                ? SittingRegeneration : StandingRegeneration;
            long increased = Math.Min(state.Maximum, checked(state.Current + intervals * amount));
            bool changed = increased != state.Current;
            state = state with
            {
                Current = checked((ushort)increased),
                // Moving and attacking are momentary activity observations, not durable
                // poses. The owner source resumes standing regeneration once its 500 ms
                // LastMove/AtkMem quiet-period predicate is satisfied.
                Activity = state.Activity is StaminaActivity.Moving or StaminaActivity.Attacking
                    ? StaminaActivity.Standing : state.Activity,
                LastRegenerationMilliseconds = consumedThrough
            };
            return new(state, changed);
        }
    }

    public bool TryConsume(ushort amount, long monotonicMilliseconds, out StaminaSnapshot resulting)
    {
        if (amount == 0 || monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        lock (gate)
        {
            if (monotonicMilliseconds < state.LastActivityMilliseconds || state.Current < amount)
            {
                resulting = state;
                return false;
            }
            state = state with
            {
                Current = checked((ushort)(state.Current - amount)),
                Activity = StaminaActivity.Attacking,
                LastActivityMilliseconds = monotonicMilliseconds
            };
            resulting = state;
            return true;
        }
    }

    public StaminaSnapshot Synchronize(ushort current, StaminaActivity activity,
        long monotonicMilliseconds)
    {
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        lock (gate)
        {
            if (current > state.Maximum) throw new ArgumentOutOfRangeException(nameof(current));
            state = state with
            {
                Current = current,
                Activity = activity,
                LastActivityMilliseconds = monotonicMilliseconds,
                LastRegenerationMilliseconds = monotonicMilliseconds
            };
            return state;
        }
    }

    public StaminaSnapshot Revive(long monotonicMilliseconds)
    {
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        lock (gate)
        {
            state = state with
            {
                Current = state.Maximum,
                Activity = StaminaActivity.Standing,
                LastActivityMilliseconds = monotonicMilliseconds,
                LastRegenerationMilliseconds = monotonicMilliseconds
            };
            return state;
        }
    }
}
