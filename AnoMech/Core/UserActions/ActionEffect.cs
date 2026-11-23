using System;
using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// What a player action does client-side, standing in for the firewalled server response.
// Composable: leaf effects (Gauge / Status) do the work; wrappers (Combo / Random) gate
// them. Authored via the factory helpers in JobActions (Gauge(...), Status(...), etc.).
internal interface IActionEffect
{
    void Apply(ActionContext ctx);
}

// Per-dispatch state an effect may need. The caster is the local player for a real press, or
// a bot for SimPartyNpc.UseAction.
internal sealed class ActionContext(uint actionId, ulong targetId, SimCharacter caster, Random rng)
{
    public ulong TargetId { get; } = targetId;
    public uint ActionId { get; } = actionId;
    public SimCharacter Caster { get; } = caster;
    // The job gauge and combo state are the local player's alone.
    public bool CasterIsPlayer => Caster is SimPlayer;
    public Random Rng { get; } = rng;
}

// Leaf: add an amount to a job gauge (ResourceGauge.Add clamps to [0, Max]).
internal sealed unsafe class GaugeEffect(ResourceGauge gauge, int amount) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) gauge.Add(edit.Gauge, amount);
    }
}

// Leaf: set a job gauge to an absolute value, clamped like Add.
internal sealed unsafe class SetGaugeEffect(ResourceGauge gauge, int value) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) gauge.Add(edit.Gauge, value - gauge.Read(edit.Gauge));
    }
}

// Leaf: grant a status to the player for `duration` seconds (replaces any existing copy).
// `stacks` sets Status.Param — 0 for a plain buff, or the stack count for a stacking buff
// (Requiescat, Sacred Sight) that JobActions consumes when their actions resolve.
internal sealed class StatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        ctx.Caster.RemoveStatus(statusId);
        ctx.Caster.AddStatusParam(statusId, stacks, duration);
        if (ctx.Caster is ISimPartyMember member) HostReport.RoleStatus([member.Role], statusId, duration, stacks);
    }
}

// A peer runs no scenario logic: the host decides who lives, from the statuses on its own copy
// of each character. Send statuses the host uses for survival or movement, plus visible Sprint.
internal static class HostReport
{
    public static bool IsReported(ushort statusId) => Mitigation.ByStatusId.ContainsKey(statusId) || statusId == SprintHandler.SprintStatusId || Mitigation.IsKnockbackImmunity(statusId);

    public static void RoleStatus(IReadOnlyList<PartyRole> roles, ushort statusId, float duration, int stacks)
    {
        if (IsReported(statusId))
            Plugin.Multiplayer?.ReportAppliedRoleStatus(roles, statusId, duration, (ushort)stacks);
    }
}

// Leaf: grant a status to the action's friendly recipients (see ActionTargets).
internal sealed class TargetStatusEffect(ushort statusId, float duration, int stacks = 0, ushort incompatible = 0, float? radius = null, ushort secondIncompatible = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var roles = new List<PartyRole>();
        foreach (var member in radius is { } range ? ActionTargets.FriendlyWithin(ctx.Caster, range) : ActionTargets.Friendly(ctx))
        {
            if (incompatible != 0 && member.HasStatus(incompatible) ||
                secondIncompatible != 0 && member.HasStatus(secondIncompatible)) continue;
            member.RemoveStatus(statusId);
            member.AddStatusParam(statusId, stacks, duration);
            if (member is ISimPartyMember slot) roles.Add(slot.Role);
        }
        HostReport.RoleStatus(roles, statusId, duration, stacks);
    }
}

internal sealed class DamageOverTimeEffect(ushort statusId, float duration, ushort incompatible = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        foreach (var target in ActionTargets.Hostile(ctx))
        {
            if (incompatible != 0) target.RemoveStatus(incompatible, ctx.Caster.GameObjectId);
            target.RemoveStatus(statusId, ctx.Caster.GameObjectId);
            target.AddStatusParam(statusId, 0, duration, ctx.Caster.GameObjectId);
        }
    }
}

internal sealed class OwnedBarrierEffect(ushort statusId) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var roles = new List<PartyRole>();
        foreach (var target in ActionTargets.Friendly(ctx))
        {
            var owner = ctx.Caster.GameObjectId;
            if (statusId == Jobs.SageStateHandler.PrognosisBarrier &&
                target.FindStatus(Jobs.SageStateHandler.DiagnosisBarrier, owner) != null) continue;
            if (statusId == Jobs.SageStateHandler.DiagnosisBarrier)
                target.RemoveStatus(Jobs.SageStateHandler.PrognosisBarrier, owner);
            target.RemoveStatus(statusId, owner);
            target.AddStatusParam(statusId, 0, 30f, owner);
            if (target is ISimPartyMember member) roles.Add(member.Role);
        }
        HostReport.RoleStatus(roles, statusId, 30f, 0);
    }
}

internal sealed class AddleEffect : IActionEffect
{
    private static readonly EnemyStatusEffect Normal = new(Mitigation.AddleStatusId, 10f);
    private static readonly EnemyStatusEffect Enhanced = new(Mitigation.AddleStatusId, 15f);

    public void Apply(ActionContext ctx)
        => (ctx.CasterIsPlayer && (Plugin.ObjectTable.LocalPlayer?.Level ?? 0) >= 98 ? Enhanced : Normal).Apply(ctx);
}

// Leaf: debuff the enemies the action hits (Reprisal, Addle).
internal sealed class EnemyStatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var enemies = ActionTargets.Hostile(ctx);
        foreach (var enemy in enemies)
        {
            enemy.RemoveStatus(statusId);
            enemy.AddStatusParam(statusId, stacks, duration);
        }
        Plugin.Multiplayer?.ReportAppliedEnemyStatus(enemies, statusId, duration, (ushort)stacks);
    }
}

internal sealed class UnlocksEffect(uint actionId, IActionEffect[] inner) : IActionEffect
{
    internal static bool IsUnlocked(uint actionId)
        => Natives.Data.Action(actionId) is { ClassJobLevel: > 0 } action && PlayerJob.Level >= action.ClassJobLevel;

    public void Apply(ActionContext ctx)
    {
        if (ctx.CasterIsPlayer && !IsUnlocked(actionId)) return;
        foreach (var effect in inner) effect.Apply(ctx);
    }
}

internal sealed class BelowLevelEffect(int level, IActionEffect effect) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (ctx.CasterIsPlayer && PlayerJob.Level < level) effect.Apply(ctx);
    }
}

internal sealed class LevelEffect(int level, IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (ctx.CasterIsPlayer && PlayerJob.Level < level) return;
        foreach (var effect in inner) effect.Apply(ctx);
    }
}

internal sealed class StatusExtensionEffect(ushort statusId, float seconds, float max, bool existingOnly) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var status = ctx.Caster.FindStatus(statusId);
        if (status == null && existingOnly) return;
        var remaining = status?.RemainingTime ?? 0f;
        ctx.Caster.RemoveStatus(statusId);
        ctx.Caster.AddStatusParam(statusId, 0, Math.Min(remaining + seconds, max));
    }
}

internal sealed class RecastReductionEffect(float seconds, uint[] actionIds) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer || ActionTargets.Hostile(ctx).Count == 0) return;
        foreach (var actionId in actionIds) RecastTimes.Advance(actionId, seconds);
    }
}

// Wrapper: apply the inner effects only when the action lands as a valid combo continuation.
internal sealed class ComboEffect(IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer || !PlayerCombo.IsActiveContinuation(ctx.ActionId)) return;
        foreach (var e in inner) e.Apply(ctx);
    }
}

// Wrapper: apply the inner effects with `chance` probability (procs, e.g. DNC feathers).
internal sealed class RandomEffect(float chance, IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (ctx.Rng.NextSingle() >= chance) return;
        foreach (var e in inner) e.Apply(ctx);
    }
}
