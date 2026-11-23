using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dummies;
using AnoMech.Scenarios.Top;

namespace AnoMech.Tests;

public sealed class Level100DummyScenarioTests
{
    [Test]
    public void DummyRemainsTargetableForThirtyMinutesAndRestartsCleanly()
    {
        var fake = FakeGame.Install();
        DalamudServices.Install(nameof(Plugin.Config), new Configuration { EnableMechanicResultMarks = false });
        using var game = new Game();
        DalamudServices.Install(nameof(Plugin.Game), game);
        var scenario = game.Catalog.Categories.Single(category => category.Key == "Dummies").Single();
        Assert.That(scenario, Is.TypeOf<Level100DummyScenario>());
        Assert.That(scenario.IsSoloOnly(), Is.True);
        Assert.That(scenario.Phase.Zone.PracticeLevel, Is.EqualTo(100));

        var run = new RunScenarioParams(scenario, new LocalPartyProvider { Solo = true }, null, 0, 0);
        game.ZoneIn(scenario.Phase.Zone, SessionMode.Local);
        game.StartScenario(run);
        fake.Frame(1f, game.Tick);

        Assert.That(fake.Zone.PracticeLevel, Is.EqualTo(100));
        var dummy = game.World.Children.OfType<SimEnemy>().Single();
        Assert.That(dummy.Targetable, Is.True);
        Assert.That(dummy.BNpcBaseId, Is.EqualTo(TopConstants.BNpcBaseId.AlphaOmega));
        Assert.That(dummy.SpawnConfig.Level, Is.EqualTo(100));
        Assert.That(dummy.HitboxRadius, Is.EqualTo(6f));
        for (var second = 1; second < 1799; second++)
            fake.Frame(1f, game.Tick);
        Assert.That(dummy.Targetable && dummy.IsActive, Is.True);
        Assert.That(game.Status, Is.EqualTo(SessionStatus.Running));
        Assert.That(game.LastRun!.Issues, Is.Empty);

        fake.Frame(1f, game.Tick);
        Assert.That(dummy.IsActive, Is.False);
        Assert.That(game.Status, Is.EqualTo(SessionStatus.Succeeded));

        game.StartScenario(run);
        fake.Frame(1f, game.Tick);
        var replacement = game.World.Children.OfType<SimEnemy>().Single();
        Assert.That(replacement, Is.Not.SameAs(dummy));
        Assert.That(replacement.Targetable, Is.True);
        Assert.That(game.Status, Is.EqualTo(SessionStatus.Running));
        game.LeaveNow();
    }
}
