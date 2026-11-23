using System;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed class MachinistSummon(SimEnemy actor, bool queen, byte level)
{
    private const uint VolleyFire = 2891, RookOverload = 7416;
    private const uint ArmPunch = 16504, RollerDash = 17206, PileBunker = 16503, CrownedCollider = 25787;
    private const float MeleeRange = 3f, RangedRange = 30f;
    private const float ColliderDelay = 2.04f, ShutdownDelay = 1.5f;

    private enum Phase { Attacking, FinisherRequested, ColliderPending, ShuttingDown }

    private Phase phase;
    private float nextAction = queen ? 4f : 2f;
    private SimEnemy? target;

    internal void RequestFinisher()
    {
        if (phase == Phase.Attacking) phase = Phase.FinisherRequested;
    }

    // True while there is still a visible summon or a pending spawn.
    internal bool Tick(SimWorld world, float delta)
    {
        if (!actor.IsActive || actor.PacketSpawnFailed) return false;
        if (actor.PacketSpawnPending) return true;

        nextAction -= delta;
        if (phase != Phase.FinisherRequested && nextAction > 0f) return true;
        if (phase == Phase.ShuttingDown) return false;

        target = FindTarget(world);
        if (phase == Phase.ColliderPending)
        {
            if (target != null) Play(CrownedCollider, target);
            phase = Phase.ShuttingDown;
            nextAction = ShutdownDelay;
            return true;
        }

        if (phase == Phase.FinisherRequested)
        {
            if (target == null) return false;
            if (queen) MoveIntoMelee(target);
            Play(queen ? PileBunker : RookOverload, target);
            phase = queen && level >= 86 ? Phase.ColliderPending : Phase.ShuttingDown;
            nextAction = phase == Phase.ColliderPending ? ColliderDelay : ShutdownDelay;
            return true;
        }

        if (target == null) return true;
        if (queen && DistanceTo(target) > MeleeRange + target.HitboxRadius)
        {
            Play(RollerDash, target, animationLock: 0f);
            var landing = MeleePlacement(target);
            actor.MoveTo(landing.Position, speed: 24f, finalRotation: landing.Rotation);
            nextAction = 3f;
        }
        else
        {
            Play(queen ? ArmPunch : VolleyFire, target);
            nextAction = queen ? 1.56f : 1.6f;
        }
        return true;
    }

    private SimEnemy? FindTarget(SimWorld world)
    {
        if (target is { } current && CanAttack(current)) return current;
        return Plugin.TargetManager?.Target is { } selected
            ? world.Children.OfType<SimEnemy>()
                .FirstOrDefault(enemy => (ulong)enemy.GameObjectId == selected.GameObjectId && CanAttack(enemy))
            : null;
    }

    private bool CanAttack(SimEnemy enemy)
        => enemy.IsActive && !enemy.PacketSpawnPending && enemy.Targetable && !enemy.Dead &&
            enemy.SpawnConfig.IsHostile && DistanceTo(enemy) <= RangedRange + enemy.HitboxRadius;

    private float DistanceTo(SimEnemy enemy)
        => MathF.Sqrt(new Placement(actor.Position, actor.Rotation).DistanceSq(enemy));

    private Placement MeleePlacement(SimEnemy enemy)
        => new Placement(enemy.Position, new Placement(actor.Position, actor.Rotation).Face(enemy.Position).Rotation)
            .MoveForward(-(enemy.HitboxRadius + 1.5f));

    private void MoveIntoMelee(SimEnemy enemy)
    {
        if (DistanceTo(enemy) > MeleeRange + enemy.HitboxRadius) actor.SetPosition(MeleePlacement(enemy));
    }

    private void Play(uint action, SimEnemy enemy, float animationLock = 0.6f)
    {
        actor.SetTarget(enemy, follow: false);
        actor.Face(enemy);
        actor.PlayAction(action, animationLock, enemy);
    }

    internal void Despawn() => actor.Despawn();
}
