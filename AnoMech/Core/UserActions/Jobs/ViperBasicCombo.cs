namespace AnoMech.Core.UserActions.Jobs;

internal static class ViperBasicCombo
{
    internal const float Duration = 30f;

    // The sheet records only one prerequisite for each action, even when both routes are legal.
    // Honed and Venom statuses guide the next press without changing these prerequisites.
    internal static ComboRole? RoleOf(uint action, uint previous, float remaining)
    {
        if (remaining <= 0f) previous = 0;
        return action switch
        {
            34606 or 34607 or 34614 or 34615 => ComboRole.Starter,
            34608 or 34609 => Continuation(previous is 34606 or 34607),
            34610 or 34611 => Continuation(previous == 34608),
            34612 or 34613 => Continuation(previous == 34609),
            34616 or 34617 => Continuation(previous is 34614 or 34615),
            34618 or 34619 => Continuation(previous is 34616 or 34617),
            _ => null,
        };
    }

    internal static bool IsFinisher(uint action)
        => action is 34610 or 34611 or 34612 or 34613 or 34618 or 34619;

    internal static (uint Action, float Remaining)? NextState(uint action, ComboRole role, uint previous, float remaining)
    {
        if (role != ComboRole.None && IsOpenStep(action))
            return (action, Duration);
        if (role == ComboRole.Continuation && IsFinisher(action))
            return (0u, 0f);
        if (remaining > 0f && IsOpenStep(previous))
            return (previous, remaining);
        return null;
    }

    private static bool IsOpenStep(uint action)
        => action is 34606 or 34607 or 34608 or 34609 or 34614 or 34615 or 34616 or 34617;

    private static ComboRole Continuation(bool valid) => valid ? ComboRole.Continuation : ComboRole.None;
}
