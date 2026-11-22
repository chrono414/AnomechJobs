#if DEBUG
using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Implementations;

internal static unsafe class PlayerActionTrace
{
    internal static Action<string>? Sink { get; set; }

    internal static bool Active => Sink != null && Plugin.Game?.World.Map.IsInInstance == true
        && Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId == 25;

    internal static string Snapshot(ActionManager* manager)
    {
        if (manager == null) return "no action manager";
        const uint fireIV = 3577;
        var group = manager->GetRecastGroup((int)ActionType.Action, fireIV);
        var recast = group >= 0 ? manager->GetRecastGroupDetail(group) : null;
        var gcd = recast != null && recast->IsActive ? Math.Max(0f, recast->Total - recast->Elapsed) : 0f;
        return FormattableString.Invariant(
            $"seqUsed={manager->LastUsedActionSequence} seqHandled={manager->LastHandledActionSequence} queued={manager->ActionQueued}/{manager->QueuedActionId} gcdLeft={gcd * 1000:F0}ms lock={manager->AnimationLock * 1000:F0}ms");
    }

    internal static void Write(string message)
        => Sink?.Invoke(FormattableString.Invariant($"[{DateTime.Now:HH:mm:ss.fff}] run={Plugin.Game?.World.Events.Elapsed:F3}s {message}"));

    internal static string BlackMageState()
    {
        var gauge = JobGaugeManager.Instance();
        var stance = gauge != null && gauge->CurrentGauge != null
            ? $"stance={gauge->BlackMage.ElementStance} paradox={gauge->BlackMage.ParadoxActive}"
            : "gauge=unavailable";
        var player = Plugin.Game?.Player;
        return $"{stance} swiftcast={player?.HasStatus(167)} triplecast={player?.FindStatus(1211)?.Stacks ?? 0}";
    }
}
#endif
