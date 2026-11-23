using System.Numerics;
using AnoMech.Scenarios.Umad;

namespace AnoMech.Tests;

public class UmadP5ForsakenNullScenarioTests
{
    [Test]
    public void BotDrivenPartySurvivesAllWaves()
    {
        var run = ScenarioRun.Execute(typeof(UmadP5ForsakenNull), 0, seed: 0);

        Assert.That(run.Passed, Is.True, run.ToString);
    }

    [Test]
    public void PlayerDiesOnEnteringPersistentBlackHole()
    {
        var run = ScenarioRun.Execute(typeof(UmadP5ForsakenNull), 0, seed: 0, new ScenarioRunOptions
        {
            Takeovers = [new(20f, Vector2.Zero)],
            WriteArtifactsOnFailure = false,
        });

        Assert.That(run.Failure, Is.Null, run.ToString);
        Assert.That(run.Deaths.Select(death => (death.Role, death.ActionId)),
            Is.EqualTo(new[] { (run.PlayerRole, (uint?)UmadP5ForsakenNull.Constants.ActionId.ForsakenGround) }),
            run.ToString);
    }
}
