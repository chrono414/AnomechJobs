using System.Collections.Generic;
using System.Linq;
using AnoMech.Scenarios.Dummies;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using AnoMech.Scenarios.Fru.P2LightRampant;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using AnoMech.Scenarios.Fru.P5FulgentBlade;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using AnoMech.Scenarios.Top.P2PartySynergy;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Omega;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P6WaveCannon2;
using AnoMech.Scenarios.Ucob.P5Exaflares;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using AnoMech.Scenarios.Umad.P2Forsaken;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Scenarios.Umad.P3LimitCut;
using AnoMech.Scenarios.Umad.P4KefkaSays;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Full;
using AnoMech.Scenarios.Umad.P5Maddening;
using AnoMech.Scenarios.Umad.P5Flood;
using AnoMech.Scenarios.Uwu.UltimatePredation;
using AnoMech.Scenarios.Uwu.UltimateSuppression;

namespace AnoMech.Scenarios;

// First-appearance order is also the menu order. Keep scenario indices stable for multiplayer.
public sealed class ScenarioCatalog
{
    public IReadOnlyList<IScenario> Scenarios { get; }
    public IReadOnlyList<IGrouping<string, IScenario>> Categories { get; }

    private ScenarioCatalog(IReadOnlyList<IScenario> scenarios)
    {
        Scenarios = scenarios;
        Categories = scenarios.GroupBy(scenario => scenario.Category).ToArray();
    }

    public static ScenarioCatalog Create() => new(
    [
        new UmadP1TeleTrouncingScenario(),
        new UmadP2ForsakenScenario(),
        new UmadP3LimitCutScenario(),
        new UmadP3BlackHoleScenario(),
        new UmadP4KefkaSaysScenario(),
        new UmadP5FloodScenario(),
        new UmadP5CelestriadScenario(),
        new UmadP5ExaflaresScenario(),
        new UmadP5MaddeningScenario(),
        new UmadP5FullScenario(),
        new UmadP5ForsakenNull(),
        new FruP2DiamondDustScenario(),
        new FruP2LightRampantScenario(),
        new FruP3UltimateRelativityScenario(),
        new FruP3ApocalypseScenario(),
        new FruP4DarklitDragonsongScenario(),
        new FruP4CrystallizeTimeScenario(),
        new FruP5FulgentBladeScenario(),
        new FruP5ParadiseRegainedScenario(),
        new TopP2PartySynergyScenario(),
        new TopP5DeltaScenario(),
        new TopP5SigmaScenario(),
        new TopP5OmegaScenario(),
        new TopP6WaveCannon2Scenario(),
        new UltimatePredationScenario(),
        new UltimateSuppressionScenario(),
        new UcobP5ExaflaresScenario(),
        new Level100DummyScenario(),
    ]);
}
