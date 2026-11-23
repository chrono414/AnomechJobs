using System;
using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Implementations;

internal readonly record struct RecastSeed(uint ActionId, int MaxCharges, int Available, int RechargeSeconds, int SecondsToNext)
{
    internal float Total => MaxCharges * RechargeSeconds;
    internal float Elapsed => (Available + 1) * RechargeSeconds - SecondsToNext;
}

// The local player's recast groups, set the way the server does: its full recast-times packet.
internal static unsafe class RecastTimes
{
    // What the server sends at zone-in: every group 0 / 0.
    public static void ClearAll()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } player) return;
        var packet = new UpdateRecastTimesPacket();
        PacketDispatcherPointers.HandleUpdateRecastTimesPacket(player.EntityId, &packet);
    }

    internal static void Advance(uint actionId, float seconds)
    {
        var manager = ActionManager.Instance();
        if (manager == null) return;
        var group = manager->GetRecastGroup((int)ActionType.Action, actionId);
        if ((uint)group >= UpdateRecastTimesPacket.Capacity) return;
        var recast = manager->GetRecastGroupDetail(group);
        if (recast == null || !recast->IsActive) return;
        recast->Elapsed = Math.Min(recast->Total, recast->Elapsed + seconds);
        if (recast->Elapsed >= recast->Total) recast->IsActive = false;
    }

    // The run reset has already cleared every group. Submit the overrides together.
    internal static void SeedAfterReset(ReadOnlySpan<RecastSeed> seeds)
    {
        var manager = ActionManager.Instance();
        if (manager == null || Plugin.ObjectTable.LocalPlayer is not { } player) return;
        var packet = new UpdateRecastTimesPacket();
        foreach (var seed in seeds)
        {
            if (seed.SecondsToNext == 0) continue;
            var actionId = manager->GetAdjustedActionId(seed.ActionId);
            var group = manager->GetRecastGroup((int)ActionType.Action, actionId);
            if ((uint)group >= UpdateRecastTimesPacket.Capacity) continue;
            // Each full recharge interval in Elapsed represents one usable charge.
            packet.Total[group] = seed.Total;
            packet.Elapsed[group] = seed.Elapsed;
        }
        PacketDispatcherPointers.HandleUpdateRecastTimesPacket(player.EntityId, &packet);
        foreach (var seed in seeds)
        {
            if (seed.SecondsToNext == 0) continue;
            var actionId = manager->GetAdjustedActionId(seed.ActionId);
            var group = manager->GetRecastGroup((int)ActionType.Action, actionId);
            if ((uint)group >= UpdateRecastTimesPacket.Capacity) continue;
            var detail = manager->GetRecastGroupDetail(group);
            if (detail == null) continue;
            // The timer packet has no ActionId. Charge handling needs the action that owns
            // the active recast, including the currently unlocked form of upgraded actions.
            detail->ActionId = actionId;
            detail->Total = packet.Total[group];
            detail->Elapsed = packet.Elapsed[group];
            detail->IsActive = true;
#if DEBUG
            DiagnosticLog.Debug($"[Job settings] Action {actionId} (group {group}): available charges {manager->GetCurrentCharges(actionId)}/{seed.Available}, recast {detail->Elapsed:F2}/{detail->Total:F2}s, requested next charge {seed.SecondsToNext}s.");
#endif
        }
    }
}
