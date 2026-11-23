using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed class WhiteMageStateHandler : IUserActionHandler
{
    private const ushort Bell = 2709, Asylum = 739;
    internal const uint Temperance = 16536;
    internal const ushort TemperanceHealing = 1872, TemperanceMitigation = 1873;
    private const float AsylumRadius = 15f;
    private const uint PlaceBell = 25862, BellPulse = 25863, BellBloom = 25864, DetonateBell = 28509;
    private static readonly EnemySpawnConfig BellActor = new(13961, 10349, EnemyList: EnemyListMode.Never,
        Engaged: false, IsHostile: false);
    private (Vector3 Center, float Remaining)? asylum;
    private Vector3? bellCenter;
    private SimEnemy? bell;
    private float bellFade;
    private float timeToBellPulse;

    public void OnScenarioStart() => Reset();
    public void OnSessionEnd() => Reset();

    private void Reset()
    {
        asylum = null;
        ClearBell();
    }

    private void ClearBell()
    {
        bell?.Despawn();
        bell = null;
        bellCenter = null;
        bellFade = 0f;
        timeToBellPulse = 0f;
    }

    public void OnAction(ActionType type, uint actionId, ulong targetId, Vector3? location)
    {
        if (type != ActionType.Action || PlayerJob.Current != JobId.WhiteMage ||
            Plugin.Game is not { Player: { } player } game) return;
        switch (actionId)
        {
            case 3569 when location is { } asylumLocation:
                asylum = (game.World.Coordinates.ToLocal(asylumLocation), 24f);
                break;
            case PlaceBell when location is { } bellLocation:
                ClearBell();
                var center = game.World.Coordinates.ToLocal(bellLocation);
                bellCenter = center;
                bell = game.World.SpawnEnemy(BellActor with
                {
                    Level = (byte)PlayerJob.Level,
                    Placement = new Placement(center, player.Rotation),
                });
                player.RemoveStatus(Bell);
                player.AddStatusParam(Bell, 5, 20f);
                break;
            case DetonateBell when player.HasStatus(Bell):
                player.RemoveStatus(Bell);
                Bloom(game.World);
                break;
            case Temperance:
                RefreshTemperance(player);
                break;
        }
    }

    private bool CanDetonateBell => PlayerJob.Current == JobId.WhiteMage && bellCenter != null &&
        Plugin.Game?.Player is { Dead: false } player && player.HasStatus(Bell);

    public uint? AdjustActionId(uint actionId)
        => actionId == PlaceBell && CanDetonateBell ? DetonateBell : null;

    public unsafe uint? AdjustActionStatus(ActionType actionType, uint actionId, bool checkRecast)
    {
        if (actionType != ActionType.Action || actionId is not (PlaceBell or DetonateBell) || !CanDetonateBell)
            return null;
        var manager = ActionManager.Instance();
        return manager != null && (!checkRecast || !manager->IsRecastTimerActive(actionType, DetonateBell))
            ? 0u : null;
    }

    public void OnTick(float deltaSeconds)
    {
        if (PlayerJob.Current != JobId.WhiteMage || Plugin.Game is not { Player: { } player } game) return;
        if (player.Dead)
        {
            Reset();
            return;
        }

        var dt = game.SimulationDelta(deltaSeconds);
        timeToBellPulse = Math.Max(0f, timeToBellPulse - dt);
        if (bellFade > 0f)
        {
            bellFade -= dt;
            if (bellFade <= 0f) ClearBell();
        }
        else if (bellCenter != null && !player.HasStatus(Bell)) ClearBell();

        if (player.HasStatus(TemperanceHealing)) RefreshTemperance(player);
        if (asylum is not { } area) return;
        asylum = area.Remaining > dt ? (area.Center, area.Remaining - dt) : null;
        foreach (var member in game.World.Party.ActiveMembers())
        {
            var status = member.FindStatus(Asylum, player.GameObjectId);
            var offset = member.Position - area.Center;
            var inside = asylum != null && !member.Dead && offset.X * offset.X + offset.Z * offset.Z <= AsylumRadius * AsylumRadius;
            if (!inside)
            {
                if (status != null) member.RemoveStatus(Asylum, player.GameObjectId);
                continue;
            }
            if (status is { RemainingTime: > 1f }) continue;
            member.RemoveStatus(Asylum, player.GameObjectId);
            member.AddStatusParam(Asylum, 0, Math.Min(area.Remaining, 3f), player.GameObjectId);
        }
    }

    private static void RefreshTemperance(SimPlayer player)
    {
        const float duration = 5f;
        var affected = new List<PartyRole>();
        foreach (var member in ActionTargets.FriendlyWithin(player, 50f))
        {
            if (member.Dead) continue;
            var status = member.FindStatus(TemperanceMitigation);
            if (status is { RemainingTime: > 2f }) continue;
            member.RemoveStatus(TemperanceMitigation);
            member.AddStatusParam(TemperanceMitigation, 0, duration).ShownAsFrom(player);
            if (member is ISimPartyMember partyMember) affected.Add(partyMember.Role);
        }
        if (affected.Count > 0) HostReport.RoleStatus(affected, TemperanceMitigation, duration, 0);
    }

    internal void OnStatusExpired(SimCharacter target, ushort statusId)
    {
        if (statusId == Bell && target is SimPlayer { Dead: false } && Plugin.Game is { } game)
            Bloom(game.World);
    }

    private void Bloom(SimWorld world)
    {
        PlayBellEffect(world, BellBloom);
        // Keep the actor alive through the release animation.
        bellCenter = null;
        bellFade = 1f;
    }

    private void PlayBellEffect(SimWorld world, uint actionId)
    {
        if (bellCenter is not { } center || bell is not { IsActive: true, PacketSpawnPending: false }) return;
        var origin = new Placement(center, bell.Rotation);
        var targets = new List<ActionEffectTarget>();
        foreach (var member in world.Party.ActiveMembers())
        {
            if (!member.Dead && origin.DistanceSq(member) <= 20f * 20f)
                targets.Add(new ActionEffectTarget(member.GameObjectId, [ActionEffectEntry.ActionMarker(actionId)]));
        }
        if (targets.Count == 0)
            targets.Add(new ActionEffectTarget(bell.GameObjectId, [ActionEffectEntry.ActionMarker(actionId)]));
        bell.Casting.NativeActionEffect(actionId, 0f, (ushort)actionId, 0, ActionType.Action, 0,
            position: center, animationTargetId: bell.GameObjectId, targets: targets);
    }

    internal static void ApplyDivineAura(SimCharacter target)
    {
        if (target.Dead) return;
        target.RemoveStatus(3904);
        target.AddStatusParam(3904, 0, 15f);
    }

    internal void OnDamageTaken(SimCharacter target)
    {
        if (PlayerJob.Current != JobId.WhiteMage || target is not SimPlayer || timeToBellPulse > 0f ||
            bellCenter == null || !target.HasStatus(Bell) || Plugin.Game is not { } game) return;
        PlayBellEffect(game.World, BellPulse);
        target.AddStatus(Bell, 0f, -1);
        if (!target.HasStatus(Bell))
        {
            bellCenter = null;
            bellFade = 1f;
        }
        timeToBellPulse = 1f;
    }
}
