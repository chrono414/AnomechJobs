using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using AnoMech.Scenarios.Umad.P5Maddening;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullScenario : IScenario
{
    private readonly UmadP5FullSettingsWindow settings = new();
    private UmadP5FullRun? run;

    public string Name => "Full P5";
    public IPhase Phase => UmadZone.P5;
    public IReadOnlyList<IScenarioAi> AiStrats => UmadP5MaddeningAi.Strategies;
    public object SettingsOverrides => settings.Overrides;
    public void DrawSettings() => settings.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settings.DrawPerPlayer();
    public IReadOnlyList<string> SettingsConflicts => settings.Overrides.Celestriad.Validate().Problems;

    public void Run(SimWorld world, int? selectedAi)
    {
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
        run = Schedule(world, () => kefka, 0f, selectedAi ?? 0, settings.Overrides);
    }

    internal static UmadP5FullRun Schedule(SimWorld world, Func<SimEnemy?> boss, float start,
        int strategy, UmadP5FullStateOverrides overrides)
    {
        var full = new UmadP5FullRun(world, boss, overrides);
        full.Schedule(start, strategy);
        return full;
    }

    public void Tick(float delta, float elapsed) => run?.Tick(delta);
    public void OnActionResolved(ActionResolved action) => run?.OnActionResolved(action);

    public void RunInstanceEvents(SimWorld world) => new UmadP5FloodScenario().RunInstanceEvents(world);
}

internal sealed partial class UmadP5FullRun(SimWorld world, Func<SimEnemy?> boss, UmadP5FullStateOverrides overrides)
{
    private SimEnemy? kefka => boss();
    private SimParty party => world.Party;
    private readonly UmadP5FloodState flood = new(world.Rng.Fork("full-p5-flood"), overrides.Flood);
    private readonly UmadP5CelestriadState celestriad = new(world.Rng.Fork("full-p5-celestriad"), world.Party, overrides.Celestriad);
    private readonly UmadP5ExaflaresState exaflares = new(world.Rng.Fork("full-p5-exaflares"), overrides.Exaflares);

    private const float BeforeFlood = 19.726f;
    private const float BeforeExaflares = 98.513f;
    private const float BeforeForsaken = 148.686f;

    public void Schedule(float start, int strategy)
    {
        var first = new UmadP5MaddeningState(world.Rng.Fork("full-p5-orchestra-1"), overrides.Orchestra.MainTankAssignment);
        var secondAssignment = overrides.Orchestra.MainTankAssignment == MaddeningTankAssignment.Flare
            ? MaddeningTankAssignment.Holy
            : MaddeningTankAssignment.Flare;
        var second = new UmadP5MaddeningState(world.Rng.Fork("full-p5-orchestra-2"), secondAssignment);
        var orchestra = UmadP5MaddeningAi.Strategies[strategy];
        var fellRolls = world.Rng.Fork("full-p5-fell-forces");

        ScheduleUltima(start, 3f, [8.083f, 8.797f, 9.467f, 10.180f]);
        ScheduleFellForces(start, 11f, PartyRole.MainTank, fellRolls,
            [new(12.139f, 12.943f, 13.433f), new(15.309f, 16.113f, 16.604f), new(18.431f, 19.235f, BeforeFlood)]);
        ScheduleFlood(start + 19.546f, start + BeforeFlood);
        orchestra.Schedule(world, first, start + 32.723f, UmadP5MaddeningScenario.First);
        UmadP5MaddeningScenario.Schedule(world, boss, first, start + 32.723f, UmadP5MaddeningScenario.First);
        ScheduleFellForces(start, 47.9f, first.HolyTank, fellRolls,
            [new(49.065f, 49.871f, 50.362f), new(52.187f, 52.989f, 53.478f)], soloOpening: true);
        ScheduleCelestriad(start + 53.412f);
        UmadP5FullCelestriadAi.Schedule(celestriad, world, start + 53.412f);
        ScheduleUltima(start, 84.874f, [89.973f, 90.646f, 91.362f, 92.074f]);
        ScheduleFellForces(start, 92.9f, first.HolyTank, fellRolls,
            [new(94.042f, 94.847f, 95.337f), new(97.216f, 98.023f, BeforeExaflares)]);
        ScheduleExaflares(start + 96.451f);
        new UmadP5FullExaflaresAi().Schedule(exaflares, world, start + 96.451f, start + BeforeExaflares + 0.05f);
        orchestra.Schedule(world, second, start + 124.778f, UmadP5MaddeningScenario.Second);
        UmadP5MaddeningScenario.Schedule(world, boss, second, start + 124.778f, UmadP5MaddeningScenario.Second);
        ScheduleFellForces(start, 140f, second.HolyTank, fellRolls,
            [new(141.149f, 141.952f, 142.442f), new(144.267f, 145.073f, 145.564f), new(147.394f, 148.196f, BeforeForsaken)], soloOpening: true);
        world.Events.Add(start + BeforeForsaken + 0.10f, () => UmadP5FullAi.MoveToCenter(party));
        ScheduleForsaken(start + 146.321f);
    }

    private void ScheduleUltima(float start, float cast, float[] hits)
    {
        world.Events.Add(start + cast, () => kefka?.Cast(ActionId.UltimaRepeater));
        foreach (var hit in hits)
            world.Events.Add(start + hit, () => kefka?.CastFromHelper(UmadActions.UltimaRepeaterHit));
    }

    public void Tick(float delta) => exaflares.SpreadTick?.Invoke(delta);
}
