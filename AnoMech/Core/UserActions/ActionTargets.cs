using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// Who a player action lands on, read from the Action sheet: its single target, or a circle of
// EffectRange around the caster or the executed target. The sheet does not say which side an action affects
// (Reprisal and Heart of Light differ only in radius), so the caller picks Friendly or Hostile.
internal static class ActionTargets
{
    public static IReadOnlyList<SimCharacter> Friendly(ActionContext ctx)
    {
        if (Plugin.Game is not { } game || !TryGetAction(ctx.ActionId, out var action)) return [];
        var members = game.World.Party.ActiveMembers();
        switch (action.CastType)
        {
            case CastType.SingleTarget:
                var target = members.FirstOrDefault(m => (ulong)m.GameObjectId == ctx.TargetId);
                if (target != null && !ReferenceEquals(target, ctx.Caster) && action.CanTargetParty) return [target];
                return action.CanTargetSelf ? [ctx.Caster] : [];
            case CastType.Circle:
                return members.Where(m => InRange(ctx.Caster, m, action.EffectRange)).ToList();
            default:
                Plugin.Log.Warning($"ActionTargets: action {ctx.ActionId} has unsupported CastType {action.CastType}; applied to the caster only");
                return [ctx.Caster];
        }
    }

    public static IReadOnlyList<SimEnemy> Hostile(ActionContext ctx)
        => Hostile(ctx.ActionId, ctx.TargetId, ctx.Caster, targetCentered: false);

    public static IReadOnlyList<SimEnemy> Hostile(uint actionId, ulong targetId, SimCharacter caster, bool targetCentered)
    {
        if (Plugin.Game is not { } game || !TryGetAction(actionId, out var action)) return [];
        var enemies = game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.Targetable && !e.Dead);
        var selected = enemies.FirstOrDefault(e => (ulong)e.GameObjectId == targetId);
        if (targetCentered && selected == null) return [];
        var center = targetCentered ? selected! : caster;

        return action.CastType switch
        {
            CastType.SingleTarget => selected == null ? [] : [selected],
            CastType.Circle or CastType.Circle2 => enemies.Where(e => InRange(center, e, action.EffectRange)).ToList(),
            CastType.Cone or CastType.Cone2 or CastType.Rectangle or CastType.Rectangle2 => DirectionalHits(),
            _ => [],
        };

        IReadOnlyList<SimEnemy> DirectionalHits()
        {
            var origin = selected == null ? caster.Placement() : caster.Placement().Face(selected.Position);
            var query = new AoeQuery(actionId, origin, casterHitboxRadius: caster.HitboxRadius);
            return enemies.Where(enemy => query.SignedDistance(enemy.Position) <= enemy.HitboxRadius).ToList();
        }
    }

    internal static IReadOnlyList<SimCharacter> FriendlyWithin(SimCharacter caster, float range)
        => Plugin.Game?.World.Party.ActiveMembers().Where(member => InRange(caster, member, range)).ToList() ?? [];

    private static bool TryGetAction(uint actionId, [NotNullWhen(true)] out ActionRow? action)
        => (action = Natives.Data.Action(actionId)) != null;

    // An AoE reaches a target whose hitbox edge is inside it, not just its centre.
    private static bool InRange(SimCharacter caster, SimCharacter target, float range)
    {
        var dx = target.Position.X - caster.Position.X;
        var dz = target.Position.Z - caster.Position.Z;
        var reach = range + target.HitboxRadius;
        return dx * dx + dz * dz <= reach * reach;
    }
}
