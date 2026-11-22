using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.UserActions.Jobs;

namespace AnoMech.Core.UserActions;

public sealed class StartingStatePresets<T> where T : class
{
    public string Selected { get; set; } = "";
    public Dictionary<string, T> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal T? Active => Presets.GetValueOrDefault(Selected);
}

internal static class JobStartingState
{
    internal static (JobId Job, int Level)? ForScenario(uint territoryId)
    {
        if (ZoneControl.GetContentFinderCondition(territoryId) is not { } duty) return null;
        var dutyLevel = duty.ClassJobLevelSync != 0 ? duty.ClassJobLevelSync : duty.ClassJobLevelRequired;
        var level = Math.Min(dutyLevel, Plugin.PlayerState.Level);
        return level == dutyLevel ? (PlayerJob.Current, level) : null;
    }

    internal static void Apply(uint territoryId, BlackMageStateHandler blackMage, TimedGaugeHandler timedGauges)
    {
        if (ForScenario(territoryId) is not { } context || Plugin.ObjectTable.LocalPlayer?.Level != context.Level)
            return;

        if (context.Job == JobId.BlackMage && BlackMageStartingRules.ForLevel(context.Level) is { } rules &&
            Plugin.Config.BlackMageStartingPresets.GetValueOrDefault(context.Level)?.Active is { } preset)
            blackMage.ApplyStartingState(preset, rules, timedGauges);
    }
}
