using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions.Jobs;

namespace AnoMech.Core.UserActions;

public enum StartingResource
{
    Mp,
    Lilies,
    BloodLily,
    LilyProgress,
    Addersgall,
    Addersting,
    AddersgallProgress,
    Beast,
    Oath,
    Soul,
    Shroud,
    RattlingCoils,
    SerpentOffering,
    Heat,
    Battery,
    ConfiteorStep,
    ConfiteorTime,
    LemureShroud,
    VoidShroud,
    AnguineTribute,
    MachinistSummonSeconds,
    MachinistSummonBattery,
}

public sealed record CombatStartingState
{
    public Dictionary<StartingResource, int> Resources { get; init; } = [];
    public Dictionary<uint, StartingCharges> Recasts { get; init; } = [];
    public Dictionary<ushort, StartingStatus> Statuses { get; init; } = [];
    public uint ComboAction { get; init; }
    public int ComboTime { get; init; }
    public PartyRole? KardionTarget { get; init; }
}

public readonly record struct StartingStatus(int Remaining, int Stacks = 0);

internal readonly record struct StartingComboRule(uint Action, string Name, int Level);

internal readonly record struct StartingResourceRule(StartingResource Key, string Name, int Level, int Max, int Default = 0);

internal readonly record struct StartingCooldownRule(uint Action, string Name, int Level, int Recharge, int Charges = 1,
    int Trait = 0, int TraitRecharge = 0, int TraitCharges = 0)
{
    internal int RechargeAt(int level) => TraitRecharge > 0 && level >= Trait ? TraitRecharge : Recharge;
    internal int ChargesAt(int level) => TraitCharges > 0 && level >= Trait ? TraitCharges : Charges;

    internal StartingCharges Normalize(CombatStartingState state, int level)
    {
        var max = ChargesAt(level);
        var value = state.Recasts.GetValueOrDefault(Action, new(max, 0));
        var available = Math.Clamp(value.Available, 0, max);
        return available == max ? new(max, 0) : new(available, Math.Clamp(value.SecondsToNext, 1, RechargeAt(level)));
    }
}

internal readonly record struct StartingStatusRule(ushort Id, string Name, int Level, int Duration, int MaxStacks = 0)
{
    internal StartingStatus Normalize(CombatStartingState state)
    {
        var value = state.Statuses.GetValueOrDefault(Id);
        return Duration == 0 ? new(0, Math.Clamp(value.Stacks, 0, 1))
            : new(Math.Clamp(value.Remaining, 0, Duration), Math.Clamp(value.Stacks, 0, MaxStacks));
    }

    internal bool IsActive(StartingStatus value)
        => Duration == 0 ? value.Remaining != 0 || value.Stacks != 0
            : value.Remaining > 0 && (MaxStacks == 0 || value.Stacks > 0);
}

internal sealed partial record CombatStartingRules(StartingResourceRule[] Resources, StartingCooldownRule[] Recasts,
    StartingStatusRule[] Statuses)
{
    internal StartingComboRule[] Combos { get; init; } = [];
    internal int ReadResource(CombatStartingState state, StartingResourceRule rule, int level, int maxMp)
        => Math.Clamp(state.Resources.GetValueOrDefault(rule.Key, rule.Key == StartingResource.Mp ? maxMp : rule.Default),
            0, ResourceMax(rule, level, maxMp));

    internal static int ResourceMax(StartingResourceRule rule, int level, int maxMp) => rule.Key switch
    {
        StartingResource.Mp => maxMp,
        StartingResource.RattlingCoils when level < 88 => 2,
        StartingResource.AnguineTribute when level < ViperStateHandler.TributeUpgradeLevel => 4,
        StartingResource.MachinistSummonSeconds when level < 80 => 10,
        _ => rule.Max,
    };

    internal void Apply(CombatStartingState state, int level, TimedGaugeHandler timers)
    {
        if (Plugin.Game?.Player is not { } player) return;
        var maxMp = (int)Natives.BattleCharas.LocalPlayer.MaxMana;
        var resources = Resources.Where(rule => rule.Level <= level)
            .ToDictionary(rule => rule.Key, rule => ReadResource(state, rule, level, maxMp));
        var statuses = Statuses.Where(rule => rule.Level <= level)
            .Select(rule => (Rule: rule, Value: rule.Normalize(state)))
            .Where(pair => pair.Rule.IsActive(pair.Value))
            .ToDictionary(pair => pair.Rule.Id, pair => pair.Value);
        NormalizeRoutes(resources, statuses);
        JobActions.ApplyStartingGauges(resources, timers);
        if (Combos.Any(rule => rule.Action == state.ComboAction && rule.Level <= level) && state.ComboTime > 0)
            PlayerCombo.Seed(state.ComboAction, Math.Clamp(state.ComboTime, 1, 30));
        if (PlayerJob.Current == JobId.Sage && level >= 4 && state.KardionTarget is { } role &&
            Plugin.Game.World.Party.Get(role) is { Dead: false } recipient)
            SageStateHandler.ApplyKardia(player, recipient);
        if (resources.TryGetValue(StartingResource.Mp, out var mp) && maxMp > 0)
            player.SetMana((uint)mp);

        foreach (var (id, value) in statuses)
        {
            player.RemoveStatus(id);
            player.AddStatusParam(id, value.Remaining == 0 ? 0 : value.Stacks, value.Remaining);
        }
        if (PlayerJob.Current == JobId.Sage) SageStateHandler.SetEukrasia(player.HasStatus(2606));
        if (PlayerJob.Current == JobId.Viper && statuses.TryGetValue(3670, out var reawakened))
            ViperStateHandler.SeedReawakened(level, reawakened.Remaining);

        RecastTimes.SeedAfterReset(Recasts.Where(rule => rule.Level <= level)
            .Select(rule =>
            {
                var value = rule.Normalize(state, level);
                return new RecastSeed(rule.Action, rule.ChargesAt(level), value.Available,
                    rule.RechargeAt(level), value.SecondsToNext);
            }).ToArray());
    }

    private static void NormalizeRoutes(Dictionary<StartingResource, int> resources, Dictionary<ushort, StartingStatus> statuses)
    {
        KeepOne(statuses, 3901, 3833);
        KeepOne(statuses, 3828, 3827, 1902);
        KeepOne(statuses, 3858, 2587);
        KeepOne(statuses, 2588, 2589);
        KeepOne(statuses, 3860, 3859);
        KeepOne(statuses, 2590, 2591);
        if (resources.GetValueOrDefault(StartingResource.ConfiteorStep) > 0 && resources.GetValueOrDefault(StartingResource.ConfiteorTime) > 0)
            statuses.Remove(3019);
        else if (resources.ContainsKey(StartingResource.ConfiteorStep)) resources[StartingResource.ConfiteorStep] = 0;
        if (resources.TryGetValue(StartingResource.LemureShroud, out var lemure))
        {
            if (lemure == 0) statuses.Remove(2593);
            if (!statuses.ContainsKey(2593)) resources[StartingResource.LemureShroud] = lemure = 0;
            if (resources.TryGetValue(StartingResource.VoidShroud, out var value))
                resources[StartingResource.VoidShroud] = lemure == 0 ? 0 : Math.Min(value, 5 - lemure);
            if (lemure > 0) statuses.Remove(3860);
            else
            {
                statuses.Remove(2590);
                statuses.Remove(2591);
                statuses.Remove(3857);
            }
        }
        if (resources.TryGetValue(StartingResource.AnguineTribute, out var tribute))
        {
            if (tribute == 0) statuses.Remove(3670);
            if (!statuses.ContainsKey(3670)) resources[StartingResource.AnguineTribute] = 0;
        }
    }

    private static void KeepOne(Dictionary<ushort, StartingStatus> statuses, params ushort[] priority)
    {
        var active = false;
        foreach (var id in priority)
        {
            if (active) statuses.Remove(id);
            else active = statuses.ContainsKey(id);
        }
    }
}
