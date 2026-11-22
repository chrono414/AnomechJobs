using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;
using AnoMech.Core.EnemyActions;
using AnoMech.Scenarios.Umad.P5Celestriad;

namespace AnoMech.Scenarios.Umad.P5Full;

internal sealed partial class UmadP5FullRun
{
    private readonly List<SimEventObject?> towers = [];

    private void ScheduleCelestriad(float start)
    {
        world.Events.Add(start + 1.0f, () => kefka?.Cast(ActionId.Celestriad, animationLock: 3.1f));
        world.Events.Add(start + 6.1f, ApplyDebuffs);
        world.Events.Add(start + 6.22f, SpawnAllTowers);
        world.Events.Add(start + 9.05f, () => LightTowers(0));
        world.Events.Add(start + 10.17f, () => LaunchChoice(0));
        world.Events.Add(start + 15.06f, () => SpendTowers(0));
        world.Events.Add(start + 15.06f, () => ResolveTowers(0));
        world.Events.Add(start + 15.06f, () => LightTowers(1));
        world.Events.Add(start + 15.26f, () => ResolveChoice(0));
        world.Events.Add(start + 21.01f, () => SpendTowers(1));
        world.Events.Add(start + 21.01f, () => LightTowers(2));
        world.Events.Add(start + 21.11f, () => ResolveTowers(1));
        world.Events.Add(start + 22.30f, () => LaunchChoice(2));
        world.Events.Add(start + 27.10f, () => SpendTowers(2));
        world.Events.Add(start + 27.10f, () => ResolveTowers(2));
        world.Events.Add(start + 27.39f, () => ResolveChoice(2));
        world.Events.Add(start + 29.44f, DespawnAll);
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.CelestriadFireIII, UmadActions.StardustFireIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadBlizzardIII, UmadActions.StardustBlizzardIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadThunderIII, UmadActions.StardustThunderIII, minSoakers: 1);
    }

    // Silent: no cast or animation on the player when the initial debuff lands, just the status.
    private void ApplyDebuffs()
    {
        foreach (var (role, element) in celestriad.PlayerDebuffElement)
        {
            if (element is not { } e) continue;
            party.Get(role)?.AddStatus(e.VulnUpStatusId, 20f);
        }
    }

    private void LaunchChoice(int set)
    {
        if (celestriad.AeroVariant[set] is not { } choice || kefka is null) return;
        kefka.Cast(choice.CastActionId, animationLock: 3.8f);
    }

    private void ResolveChoice(int set)
    {
        if (celestriad.AeroVariant[set] is { } choice)
            kefka?.CastFromHelper(choice.Resolution, at: new Placement(Vector3.Zero, MathF.PI));
    }

    private void SpawnAllTowers()
    {
        foreach (var tower in celestriad.AllTowers)
        {
            towers.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                LayoutId = EObjId.CelestriadTowerLayoutIds[tower.RingIndex],
                EventId = EObjId.PropEventId,
                TargetableStatus = 5,
                Arg2 = EObjId.CelestriadTowerArg2 + ((uint)tower.RingIndex << 16),
                Placement = new Placement(tower.Position, MathF.Atan2(-tower.Position.X, -tower.Position.Z)),
                TimelineState = EObjState.CelestriadTowerDormant,
            }));
        }
    }

    private void LightTowers(int set)
    {
        foreach (var towerIndex in celestriad.SetActiveTowers[set])
            towers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerLit, EObjState.CelestriadTowerLitMask);
    }

    private void SpendTowers(int set)
    {
        foreach (var towerIndex in celestriad.SetActiveTowers[set])
            towers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerSpent, EObjState.CelestriadTowerSpentMask);
    }

    private void ResolveTowers(int set)
    {
        foreach (var towerIndex in celestriad.SetActiveTowers[set])
        {
            var tower = celestriad.AllTowers[towerIndex];
            kefka?.CastFromHelper(tower.Element.Tower, at: new Placement(tower.Position, 0f));
        }
    }

    private void DespawnAll()
    {
        foreach (var tower in towers) tower?.Despawn();
        towers.Clear();

    }
}
