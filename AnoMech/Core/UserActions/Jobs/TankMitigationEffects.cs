using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed class ShakeItOffEffect : IActionEffect
{
    private const ushort Thrill = 87, Vengeance = 89, Damnation = 3832, Bloodwhetting = 2678, RawIntuition = 735;

    public void Apply(ActionContext ctx)
    {
        var caster = ctx.Caster;
        var strength = 15 + 2 * (Consume(caster, Thrill) + Consume(caster, Vengeance) +
                                 Consume(caster, Damnation) + Consume(caster, Bloodwhetting) + Consume(caster, RawIntuition));
        new TargetStatusEffect(1457, 30f, strength).Apply(ctx);
    }

    private static int Consume(SimCharacter caster, ushort status)
    {
        if (!caster.HasStatus(status)) return 0;
        caster.RemoveStatus(status);
        return 1;
    }
}

internal sealed class InterventionEffect : IActionEffect
{
    private static readonly TargetStatusEffect Normal = new(1174, 8f);
    private static readonly TargetStatusEffect Enhanced = new(1174, 8f, 20);
    private static readonly TargetStatusEffect Resolve = new(2675, 4f, 10);
    private static readonly TargetStatusEffect Benediction = new(2676, 12f);

    public void Apply(ActionContext ctx)
    {
        var strengthened = ctx.Caster.HasStatus(1191) || ctx.Caster.HasStatus(74) || ctx.Caster.HasStatus(3829);
        (strengthened ? Enhanced : Normal).Apply(ctx);
        if (ctx.CasterIsPlayer && PlayerJob.Level < 82) return;
        Resolve.Apply(ctx);
        Benediction.Apply(ctx);
    }
}
