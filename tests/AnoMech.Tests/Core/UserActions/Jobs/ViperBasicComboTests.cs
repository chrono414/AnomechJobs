using AnoMech.Core.UserActions;
using AnoMech.Core.UserActions.Jobs;

namespace AnoMech.Tests;

public sealed class ViperBasicComboTests
{
    [Test]
    public void BothStartersCanReachEveryFinisherInTheirComboFamily()
    {
        foreach (var starter in new uint[] { 34606, 34607, 34614, 34615 })
        {
            Assert.That(ViperBasicCombo.RoleOf(starter, 0, 0f), Is.EqualTo(ComboRole.Starter));
            var middles = starter is 34606 or 34607 ? new uint[] { 34608, 34609 } : [34616, 34617];
            foreach (var middle in middles)
            {
                Assert.That(ViperBasicCombo.RoleOf(middle, starter, 1f), Is.EqualTo(ComboRole.Continuation));
                var finishers = middle switch
                {
                    34608 => new uint[] { 34610, 34611 },
                    34609 => [34612, 34613],
                    _ => [34618, 34619],
                };
                foreach (var finisher in finishers)
                    Assert.That(ViperBasicCombo.RoleOf(finisher, middle, 1f), Is.EqualTo(ComboRole.Continuation));
            }
        }
    }

    [Test]
    public void AFinisherRequiresAnUnexpiredMatchingMiddleHit()
    {
        Assert.That(ViperBasicCombo.RoleOf(34610, 34609, 1f), Is.EqualTo(ComboRole.None));
        Assert.That(ViperBasicCombo.RoleOf(34610, 34608, 0f), Is.EqualTo(ComboRole.None));
        Assert.That(ViperBasicCombo.RoleOf(34608, 34615, 1f), Is.EqualTo(ComboRole.None));
        Assert.That(ViperBasicCombo.RoleOf(34618, 34609, 1f), Is.EqualTo(ComboRole.None));
    }

    [Test]
    public void BasicComboRetainsItsTimerThroughInterleavedActionsAndResetsOnCompletion()
    {
        foreach (var interleaved in new uint[] { 34620, 34623, 34626, 34633, 34632 })
            Assert.That(ViperBasicCombo.NextState(interleaved, ComboRole.None, 34608, 12f), Is.EqualTo((34608u, 12f)));
        Assert.That(ViperBasicCombo.NextState(34610, ComboRole.Continuation, 34608, 12f), Is.EqualTo((0u, 0f)));
        Assert.That(ViperBasicCombo.NextState(34614, ComboRole.Starter, 34608, 12f), Is.EqualTo((34614u, 30f)));
    }
}
