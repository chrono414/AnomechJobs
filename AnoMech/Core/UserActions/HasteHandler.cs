using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions.Jobs;

namespace AnoMech.Core.UserActions;

// Job statuses that apply player-wide timing changes through the native stats packet.
internal sealed class HasteHandler : IUserActionHandler
{
    private const int NormalTiming = 100;
    private static readonly AttackTiming Unmodified = new(NormalTiming, NormalTiming);
    private static readonly (JobId Job, ushort Status, AttackTiming Percent)[] StatusHaste =
    [
        (JobId.BlackMage, BlackMageStateHandler.CircleOfPower, new(85, 85)),
    ];

    private (uint PlayerId, AttackTiming Baseline, AttackTiming Applied)? adjustment;

    public float? AutoAttackDelay => adjustment is { } active ? active.Applied.Delay / 1000f : null;

    public void OnTick(float deltaSeconds)
    {
        var percent = Unmodified;
        if (Plugin.Game?.Player is { Dead: false } player)
        {
            var job = PlayerJob.Current;
            foreach (var rule in StatusHaste)
                if (rule.Job == job && player.HasStatus(rule.Status))
                    percent = Scale(percent, rule.Percent);
        }
        Update(percent);
    }

    public void OnScenarioStart() => Update(Unmodified);
    public void OnSessionEnd() => Update(Unmodified);

    private void Update(AttackTiming percent)
    {
        if (percent == Unmodified && adjustment == null) return;
        var player = Natives.BattleCharas.LocalPlayer;
        if (!player.Exists || PlayerStats.ReadAttackTiming() is not { Haste: > 0, Delay: > 0 } current) return;
        var playerId = player.EntityId;

        if (adjustment is { } old && old.PlayerId != playerId) adjustment = null;
        var baseline = adjustment is { } active
            ? ResolveBaseline(current, active.Baseline, active.Applied)
            : current;
        var desired = Scale(baseline, percent);
        if (desired != current && !PlayerStats.TrySetAttackTiming(desired)) return;
        adjustment = percent == Unmodified ? null : (playerId, baseline, desired);
    }

    // Level sync or another native update can replace these attributes while the effect is active.
    internal static AttackTiming ResolveBaseline(AttackTiming current, AttackTiming baseline, AttackTiming applied)
        => new(current.Haste == applied.Haste ? baseline.Haste : current.Haste,
            current.Delay == applied.Delay ? baseline.Delay : current.Delay);

    internal static AttackTiming Scale(AttackTiming baseline, AttackTiming percent)
        => new(baseline.Haste * percent.Haste / NormalTiming, baseline.Delay * percent.Delay / NormalTiming);
}
