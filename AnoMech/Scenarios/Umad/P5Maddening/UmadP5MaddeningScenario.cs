using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Maddening;

public sealed class UmadP5MaddeningScenario : IScenario
{
    public string Name => "Maddening Orchestra";
    public IPhase Phase => UmadZone.P5;
    public IReadOnlyList<IScenarioAi> AiStrats => UmadP5MaddeningAi.Strategies;

    private readonly UmadP5MaddeningSettingsWindow settings = new();
    public void DrawSettings() => settings.Draw();
    public object SettingsOverrides => settings.Overrides;

    internal readonly record struct Beat(float Cue, float Snapshot);
    internal sealed record Timing(Beat FirstHit, float Surprise, Beat SecondHit, Beat FinalHit);

    internal static readonly Timing First = new(new(5.872f, 6.538f), 6.493f, new(9.031f, 9.657f), new(12.551f, 13.174f));
    internal static readonly Timing Second = new(new(5.882f, 6.507f), 6.462f, new(9.050f, 9.675f), new(12.535f, 13.161f));

    internal const float StandaloneCastAt = 1f;

    public void Run(SimWorld world, int? selectedAi)
    {
        var state = new UmadP5MaddeningState(world.Rng, settings.Overrides.MainTankAssignment);
        SimEnemy? kefka = null;
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            LayoutId: BNpcLayoutId.KefkaP5,
            Placement: new Placement(Vector3.Zero, MathF.PI))));

        if (selectedAi is { } index)
            UmadP5MaddeningAi.Strategies[index].Schedule(world, state, StandaloneCastAt, First);

        Schedule(world, () => kefka, state, StandaloneCastAt, First);
    }

    internal static void Schedule(SimWorld world, Func<SimEnemy?> kefka, UmadP5MaddeningState state, float castAt, Timing timing)
    {
        var party = world.Party;
        SimCharacter? FlareTank() => party.Get(state.FlareTank);
        SimCharacter? HolyTank() => party.Get(state.HolyTank);

        SimCharacter[] firstHolyTargets = [];
        SimCharacter[] secondHolyTargets = [];

        world.Events.Add(castAt, () => kefka()?.Cast(ActionId.MaddeningOrchestra));
        world.Events.Add(castAt + timing.FirstHit.Cue, () =>
        {
            firstHolyTargets = state.FirstHolyPriority
                .Select(role => party.Get(role))
                .OfType<SimCharacter>()
                .Where(member => member.IsAlive())
                .Take(3)
                .ToArray();
            FlareTank()?.PlayActorVfx(VfxPath.OrchestraFlare);
            HolyTank()?.PlayActorVfx(VfxPath.OrchestraFlare);
            foreach (var member in firstHolyTargets) member.PlayActorVfx(VfxPath.OrchestraHoly);
        });
        world.Events.Add(castAt + timing.Surprise, () =>
        {
            FlareTank()?.AddStatus(StatusId.SurpriseFlare, 6f);
            HolyTank()?.AddStatus(StatusId.SurpriseHoly, 6f);
        });
        world.Events.Add(castAt + timing.FirstHit.Snapshot, () =>
        {
            Hit(UmadActions.OrchestraFlare, FlareTank());
            Hit(UmadActions.OrchestraFlare, HolyTank());
            foreach (var member in firstHolyTargets) Hit(UmadActions.OrchestraHoly, member);
        });

        world.Events.Add(castAt + timing.SecondHit.Cue, () =>
        {
            if (kefka() is not { } boss) return;
            secondHolyTargets = party.ActiveMembers()
                .Where(member => member is ISimPartyMember seat && !seat.Role.IsTank())
                .OrderBy(member => Vector3.DistanceSquared(member.Position, boss.Position))
                .Take(3)
                .ToArray();
            FlareTank()?.PlayActorVfx(VfxPath.OrchestraChaoticFlare);
            HolyTank()?.PlayActorVfx(VfxPath.OrchestraChaoticFlare);
            foreach (var member in secondHolyTargets) member.PlayActorVfx(VfxPath.OrchestraHoly);
        });
        world.Events.Add(castAt + timing.SecondHit.Snapshot, () =>
        {
            Hit(UmadActions.OrchestraChaoticFlare, FlareTank());
            foreach (var member in secondHolyTargets) Hit(UmadActions.OrchestraHoly, member);
        });

        world.Events.Add(castAt + timing.FinalHit.Cue, () =>
        {
            FlareTank()?.PlayActorVfx(VfxPath.OrchestraFlareDiffusion, nativeScale: true);
        });
        world.Events.Add(castAt + timing.FinalHit.Snapshot, () =>
        {
            Hit(UmadActions.OrchestraFlareDiffusion, FlareTank());
            Hit(UmadActions.OrchestraChaoticHoly, HolyTank());
        });

        void Hit(EnemyAction action, SimCharacter? member)
        {
            if (member is not null) kefka()?.CastFromHelper(action, member);
        }
    }
}
