using System.Collections.Generic;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dummies;

public sealed class Level100DummyScenario : IScenario
{
    private const float PracticeSeconds = 30f * 60f;
    private readonly DummySettingsWindow settings = new();

    public string Name => "Level 100 Dummy";
    public string Category => "Dummies";
    public IPhase Phase => settings.Phase;
    public bool SupportsSolo => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [];

    public void DrawSettings() => settings.Draw(Plugin.Game?.Zone != null);

    public void Run(SimWorld world, int? selectedAi)
    {
        if (world.SpawnEnemy(settings.Target) is not { } dummy)
        {
            world.Party.SetInCombat(false);
            world.Fail("The striking dummy could not spawn.");
            return;
        }

        world.Events.Add(PracticeSeconds, () =>
        {
            dummy.Despawn();
            world.Party.SetInCombat(false);
            world.Announce("Dummy practice complete.");
        });
    }
}
