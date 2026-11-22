using System;
using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Implementations;

internal readonly record struct RecastSeed(uint ActionId, int MaxCharges, int Available, int RechargeSeconds, int SecondsToNext);

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

    // The run reset has already cleared every group. Submit the overrides together.
    internal static void SeedAfterReset(ReadOnlySpan<RecastSeed> seeds)
    {
        var manager = ActionManager.Instance();
        if (manager == null || Plugin.ObjectTable.LocalPlayer is not { } player) return;
        var packet = new UpdateRecastTimesPacket();
        foreach (var seed in seeds)
        {
            if (seed.SecondsToNext == 0) continue;
            var group = manager->GetRecastGroup((int)ActionType.Action, seed.ActionId);
            if ((uint)group >= UpdateRecastTimesPacket.Capacity) continue;
            // Each full recharge interval in Elapsed represents one usable charge.
            packet.Total[group] = seed.MaxCharges * seed.RechargeSeconds;
            packet.Elapsed[group] = (seed.Available + 1) * seed.RechargeSeconds - seed.SecondsToNext;
        }
        PacketDispatcherPointers.HandleUpdateRecastTimesPacket(player.EntityId, &packet);
#if DEBUG
        foreach (var seed in seeds)
            DiagnosticLog.Debug($"[Job settings] Action {seed.ActionId}: available charges {manager->GetCurrentCharges(seed.ActionId)}/{seed.Available}, requested next charge {seed.SecondsToNext}s.");
#endif
    }
}
