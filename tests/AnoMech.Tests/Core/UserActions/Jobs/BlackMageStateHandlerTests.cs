using System.Linq;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using AnoMech.Core.UserActions;
using AnoMech.Core.UserActions.Jobs;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Tests;

public class BlackMageStateHandlerTests
{
    [Test]
    public void StandardLoopSpendsHeartsAndLeavesManaForDespair()
    {
        var gauge = new BlackMageGauge { ElementStance = 3 };
        var mp = 0;
        Use(ref gauge, ref mp, 154);
        Assert.That(gauge.ParadoxActive, Is.True);
        Use(ref gauge, ref mp, 3576);
        Assert.That(mp, Is.EqualTo(10000));
        Assert.That(Use(ref gauge, ref mp, 25797).Firestarter, Is.False);

        Use(ref gauge, ref mp, 152);
        for (var i = 0; i < 3; i++) Use(ref gauge, ref mp, 3577);
        Assert.That(gauge.UmbralHearts, Is.Zero);
        Assert.That(mp, Is.EqualTo(7600));
        Assert.That(Use(ref gauge, ref mp, 25797).Firestarter, Is.True);
        for (var i = 0; i < 3; i++) Use(ref gauge, ref mp, 3577);
        Assert.That(gauge.AstralSoulStacks, Is.EqualTo(6));
        Assert.That(mp, Is.EqualTo(1200));

        Use(ref gauge, ref mp, 36989);
        Use(ref gauge, ref mp, 16505);
        Assert.That(gauge.AstralSoulStacks, Is.Zero);
        Assert.That(mp, Is.Zero);
    }

    [Test]
    public void ManafontSupportsTwoFlareStarsInFivePlusSevenOpener()
    {
        var gauge = new BlackMageGauge();
        var mp = 10000;
        Assert.That(Use(ref gauge, ref mp, 152).Thunderhead, Is.True);
        for (var i = 0; i < 5; i++) Use(ref gauge, ref mp, 3577);
        Assert.That(mp, Is.Zero);
        Assert.That(BlackMageStateHandler.Resolve(ref gauge, 36989, 100, mp, 10000, false, true, true).Executed, Is.False);

        Assert.That(Use(ref gauge, ref mp, 158).Thunderhead, Is.True);
        Assert.That(gauge.AstralSoulStacks, Is.EqualTo(5));
        Use(ref gauge, ref mp, 3577);
        Use(ref gauge, ref mp, 36989);
        for (var i = 0; i < 6; i++) Use(ref gauge, ref mp, 3577);
        Use(ref gauge, ref mp, 36989);
        Assert.That(mp, Is.EqualTo(1200));
        Assert.That(gauge.AstralSoulStacks, Is.Zero);
    }

    [Test]
    public void TransposePreservesHeartsAndEnochianButClearsAstralSoulOnLeavingFire()
    {
        var gauge = new BlackMageGauge { ElementStance = 3, EnochianActive = true, UmbralHearts = 2 };
        gauge.AstralSoulStacks = 5;
        var mp = 1000;
        Assert.That(Use(ref gauge, ref mp, 149).Thunderhead, Is.True);
        Assert.That(gauge.ElementStance, Is.EqualTo(-1));
        Assert.That(gauge.UmbralHearts, Is.EqualTo(2));
        Assert.That(gauge.AstralSoulStacks, Is.Zero);
        Assert.That(gauge.ParadoxActive, Is.True);
        Assert.That(gauge.EnochianActive, Is.True);

        Use(ref gauge, ref mp, 25797);
        Use(ref gauge, ref mp, 16506, inCombat: false);
        Assert.That(gauge.ParadoxActive, Is.False);
        Assert.That(gauge.UmbralHearts, Is.EqualTo(3));
        Assert.That(mp, Is.EqualTo(10000));
        Use(ref gauge, ref mp, 149);
        Assert.That(gauge.ParadoxActive, Is.True);
        Use(ref gauge, ref mp, 152, firestarter: true);
        Assert.That(gauge.UmbralHearts, Is.EqualTo(3));
        Assert.That(mp, Is.EqualTo(10000));
    }

    [Test]
    public void TwoFlaresUseHeartDiscountAndFinishAtZeroMana()
    {
        var gauge = new BlackMageGauge { ElementStance = -1 };
        var mp = 0;
        Use(ref gauge, ref mp, 159);
        Assert.That(mp, Is.EqualTo(2500));
        Use(ref gauge, ref mp, 149);
        Use(ref gauge, ref mp, 162);
        Assert.That(gauge.UmbralHearts, Is.Zero);
        Assert.That(mp, Is.GreaterThanOrEqualTo(800));
        Use(ref gauge, ref mp, 162);
        Use(ref gauge, ref mp, 36989);
        Assert.That(mp, Is.Zero);
    }

    [Test]
    public void IceAndFireResourcesRequireTheSpellToLand()
    {
        var gauge = new BlackMageGauge { ElementStance = -3 };
        var mp = 0;
        Use(ref gauge, ref mp, 3576, landed: false);
        Assert.That(mp, Is.Zero);
        Assert.That(gauge.UmbralHearts, Is.Zero);

        gauge.ElementStance = 3;
        mp = 10000;
        Use(ref gauge, ref mp, 3577, landed: false);
        Assert.That(gauge.AstralSoulStacks, Is.Zero);
    }

    [Test]
    public unsafe void PolyglotClockRequiresEnochianAndContinuesAtCap()
    {
        var original = Plugin.ObjectTable;
        var objects = DispatchProxy.Create<IObjectTable, PolyglotTestObjects>();
        DalamudServices.Install(nameof(Plugin.ObjectTable), objects);
        try
        {
            var timer = JobActions.TimedGauges.Single(g => g.Job == JobId.BlackMage);
            var manager = new JobGaugeManager();
            ref var gauge = ref manager.BlackMage;
            float elapsed = 0;
            TimedGaugeHandler.TickRegen(timer, ref elapsed, &manager, 40);
            Assert.That(gauge.PolyglotStacks, Is.Zero);
            Assert.That(elapsed, Is.Zero);

            gauge.ElementStance = 3;
            gauge.EnochianActive = true;
            gauge.PolyglotStacks = 3;
            TimedGaugeHandler.TickRegen(timer, ref elapsed, &manager, 50);
            Assert.That(gauge.PolyglotStacks, Is.EqualTo(3));
            Assert.That(gauge.EnochianTimer, Is.EqualTo(10000));

            gauge.ElementStance = -3;
            gauge.PolyglotStacks = 2;
            TimedGaugeHandler.TickRegen(timer, ref elapsed, &manager, 0);
            Assert.That(gauge.EnochianTimer, Is.EqualTo(10000));
            TimedGaugeHandler.TickRegen(timer, ref elapsed, &manager, 10);
            Assert.That(gauge.PolyglotStacks, Is.EqualTo(3));
            Assert.That(gauge.EnochianTimer, Is.EqualTo(30000));

            gauge.EnochianActive = false;
            TimedGaugeHandler.TickRegen(timer, ref elapsed, &manager, 15);
            Assert.That(gauge.EnochianTimer, Is.Zero);
            Assert.That(elapsed, Is.Zero);
        }
        finally
        {
            DalamudServices.Install(nameof(Plugin.ObjectTable), original);
        }
    }

    public class PolyglotTestObjects : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_LocalPlayer" ? Create<IPlayerCharacter, PolyglotTestPlayer>() : null;
    }

    public class PolyglotTestPlayer : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_Level" ? (byte)100 : null;
    }

    private static BlackMageStateHandler.Resolution Use(ref BlackMageGauge gauge, ref int mp, uint action,
        bool firestarter = false, bool inCombat = true, bool landed = true)
    {
        var result = BlackMageStateHandler.Resolve(ref gauge, action, 100, mp, 10000, firestarter, inCombat, landed);
        Assert.That(result.Executed, Is.True, $"Action {action} at {mp} MP");
        mp = result.Mana;
        return result;
    }
}
