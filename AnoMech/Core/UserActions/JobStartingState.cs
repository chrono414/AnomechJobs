using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.UserActions.Jobs;
using AnoMech.Scenarios;

namespace AnoMech.Core.UserActions;

public sealed class StartingStatePresets<T> where T : class
{
    public string Selected { get; set; } = "";
    public Dictionary<string, T> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal T? Active => Presets.GetValueOrDefault(Selected);
}

internal static class JobStartingState
{
    internal static (JobId Job, int Level)? ForScenario(IZone zone)
    {
        var level = zone.PracticeLevel;
        if (level is null && ZoneControl.GetContentFinderCondition(zone.TerritoryId) is { } duty)
            level = duty.ClassJobLevelSync != 0 ? duty.ClassJobLevelSync : duty.ClassJobLevelRequired;
        return level is { } cap && Plugin.PlayerState.Level >= cap ? (PlayerJob.Current, cap) : null;
    }

    internal static void Apply(IZone zone, BlackMageStateHandler blackMage, TimedGaugeHandler timedGauges)
    {
        if (ForScenario(zone) is not { } context || Plugin.ObjectTable.LocalPlayer?.Level != context.Level)
            return;

        if (context.Job == JobId.BlackMage && BlackMageStartingRules.ForLevel(context.Level) is { } rules &&
            Plugin.Config.BlackMageStartingPresets.GetValueOrDefault(context.Level)?.Active is { } preset)
            blackMage.ApplyStartingState(preset, rules, timedGauges);
        else if (CombatStartingRules.ForJob(context.Job) is { } combatRules &&
            Plugin.Config.CombatStartingPresets.GetValueOrDefault(context.Job)?.GetValueOrDefault(context.Level)?.Active is { } combat)
            combatRules.Apply(combat, context.Level, timedGauges);
    }
}
