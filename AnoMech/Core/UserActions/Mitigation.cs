using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions.Jobs;

namespace AnoMech.Core.UserActions;

// What one status contributes to surviving a hit. Everything is a fraction (0.20f = 20%)
// except ShieldPotency; a status sets only what it grants. ShieldHp and MaxHp are fractions
// of the target's max HP.
public readonly record struct Mitigation(
    float Damage = 0f, float Magic = 0f, float Physical = 0f,
    float ShieldHp = 0f, float ShieldPotency = 0f, float MaxHp = 0f)
{
    // Rough: fitted to a tank's own shields. A healer's potency is worth more HP per point.
    public const float ShieldHpPerPotency = 0.00015f;

    private const float Invulnerable = 1f;

    // Off, no hit checks mitigation, so nothing a bot could press would change the outcome.
    public static bool Required => Plugin.Config.EnableTankMitigation && Natives.UserActions.Enabled;

    public bool IsShield => ShieldHp > 0f || ShieldPotency > 0f;

    private float ShieldFractionOfMaxHp => ShieldHp + ShieldPotency * ShieldHpPerPotency;

    public static readonly IReadOnlyDictionary<ushort, Mitigation> ByStatusId = new Dictionary<ushort, Mitigation>
    {
        // Tank role
        [1191] = new(Damage: 0.20f),   // Rampart
        [1193] = new(Damage: 0.10f),   // Reprisal (on the enemy)

        // Caster role
        [168] = new(ShieldHp: 0.30f),                    // Manaward

        // PLD
        [82]   = new(Damage: Invulnerable),                   // Hallowed Ground
        [74]   = new(Damage: 0.30f),
        [1856] = new(Damage: 0.15f),
        [PaladinStateHandler.PassageOfArms] = new(Damage: 0.15f),
        [PaladinStateHandler.ArmsUp] = new(Damage: 0.15f),
        [3829] = new(Damage: 0.40f),   // Guardian
        [3830] = new(ShieldPotency: 1000f),   // Guardian's Will
        [77]   = new(Damage: 0.20f),   // Bulwark (guaranteed block; the real reduction scales with the shield)
        [2674] = new(Damage: 0.15f),   // Holy Sheltron
        [2675] = new(Damage: 0.15f),   // Knight's Resolve
        [1174] = new(Damage: 0.10f),   // Intervention
        [1362] = new(ShieldHp: 0.10f), // Divine Veil
        [196]  = new(Damage: 0.80f),   // Last Bastion

        // WAR
        [409]  = new(Damage: Invulnerable),   // Holmgang
        [89]   = new(Damage: 0.30f),
        [735]  = new(Damage: 0.10f),
        [3832] = new(Damage: 0.40f),   // Damnation
        [87]   = new(MaxHp: 0.20f),    // Thrill of Battle
        [2678] = new(Damage: 0.10f),   // Bloodwhetting
        [2679] = new(Damage: 0.10f),   // Stem the Flow
        [2680] = new(ShieldPotency: 400f),    // Stem the Tide
        [1858] = new(Damage: 0.10f),   // Nascent Glint
        [1457] = new(ShieldHp: 0.15f), // Shake It Off
        [863]  = new(Damage: 0.80f),   // Land Waker

        // Healer and ranged mitigation
        [1218] = new(ShieldPotency: 500f),
        [1219] = new(Damage: 0.10f),
        [1873] = new(Damage: 0.10f),   // Temperance mitigation aura
        [2708] = new(Damage: 0.15f),
        [3903] = new(ShieldPotency: 400f),
        [2607] = new(ShieldPotency: 540f),
        [2609] = new(ShieldPotency: 320f),
        [2612] = new(ShieldPotency: 300f),
        [2613] = new(ShieldPotency: 200f),
        [2596] = new(ShieldHp: 0.10f),
        [2618] = new(Damage: 0.10f),
        [2619] = new(Damage: 0.10f),
        [3003] = new(Damage: 0.10f),
        [3365] = new(ShieldPotency: 300f),
        [1951] = new(Damage: 0.15f),

        // DRK
        [810]  = new(Damage: Invulnerable),   // Living Dead
        [3835] = new(Damage: 0.40f),   // Shadowed Vigil
        [746]  = new(Magic: 0.20f, Physical: 0.10f),   // Dark Mind
        [2682] = new(Damage: 0.10f),   // Oblation
        [1178] = new(ShieldHp: 0.25f), // The Blackest Night
        [1894] = new(Magic: 0.10f, Physical: 0.05f),   // Dark Missionary
        [864]  = new(Damage: 0.80f),   // Dark Force

        // GNB
        [1836] = new(Damage: Invulnerable),   // Superbolide
        [3838] = new(Damage: 0.40f, MaxHp: 0.20f),     // Great Nebula
        [1832] = new(Damage: 0.10f),   // Camouflage
        [2683] = new(Damage: 0.15f),   // Heart of Corundum
        [2684] = new(Damage: 0.15f),   // Clarity of Corundum
        [1839] = new(Magic: 0.10f, Physical: 0.05f),   // Heart of Light
        [1931] = new(Damage: 0.80f),   // Gunmetal Soul
    };

    internal const ushort SurecastStatusId = 160;
    internal const ushort AddleStatusId = 1203;
    internal const ushort ArmsLengthStatusId = 1209, FeintStatusId = 1195, DismantleStatusId = 860;

    internal static bool IsOutgoingStatus(ushort statusId)
        => statusId is 1193 or AddleStatusId or FeintStatusId or DismantleStatusId;

    internal static bool IsKnockbackImmunity(ushort statusId)
        => statusId is SurecastStatusId or ArmsLengthStatusId or 2663;

    internal static bool ResistsKnockback(SimCharacter target)
        => target.HasStatus(SurecastStatusId) || target.HasStatus(ArmsLengthStatusId) || target.HasStatus(2663);

    internal static float OutgoingReduction(SimCharacter caster, DamageKind kind)
    {
        var taken = 1f;
        if (caster.HasStatus(1193)) taken *= 0.90f;
        if (caster.HasStatus(DismantleStatusId)) taken *= 0.90f;
        if (caster.HasStatus(AddleStatusId)) taken *= kind switch
        {
            DamageKind.Physical => 0.95f,
            DamageKind.Magic => 0.90f,
            _ => 1f,
        };
        if (caster.HasStatus(FeintStatusId)) taken *= kind switch
        {
            DamageKind.Physical => 0.90f,
            DamageKind.Magic => 0.95f,
            _ => 1f,
        };
        return 1f - taken;
    }

    // The single damage reduction that would let a full-HP target survive the same hit, 0..1:
    // 0.5 means a hit of twice its max HP is survivable. Percentage reductions stack
    // multiplicatively and shrink the hit; shields and bonus max HP add to the pool it lands on.
    public static float Effective(IEnumerable<ushort> statusIds, DamageKind kind, float outgoingReduction = 0f)
        => EffectiveValues(statusIds.Select(id => ByStatusId.GetValueOrDefault(id)), kind, outgoingReduction);

    internal static float EffectiveFor(IEnumerable<SimStatus> statuses, DamageKind kind, float outgoingReduction)
        => EffectiveValues(statuses.Select(ForStatus), kind, outgoingReduction);

    private static float EffectiveValues(IEnumerable<Mitigation> mitigation, DamageKind kind, float outgoingReduction)
    {
        var taken = 1f - outgoingReduction;
        var pool = 1f;
        foreach (var m in mitigation)
        {
            taken *= (1f - m.Damage) * (1f - kind switch { DamageKind.Magic => m.Magic, DamageKind.Physical => m.Physical, _ => 0f });
            pool += m.ShieldFractionOfMaxHp + m.MaxHp;
        }
        return 1f - taken / pool;
    }

    private static Mitigation ForStatus(SimStatus status) => status.StatusId switch
    {
        1457 when status.Stacks is 17 or 19 or 21 => new(ShieldHp: status.Stacks / 100f),
        1174 when status.Stacks == 20 => new(Damage: 0.20f),
        2675 when status.Stacks == 10 => new(Damage: 0.10f),
        _ => ByStatusId.GetValueOrDefault(status.StatusId),
    };

    // Banked shield as a fraction of max HP: what the gold overlay on the HP bar shows.
    public static float ShieldFraction(IEnumerable<ushort> statusIds)
        => statusIds.Sum(id => ByStatusId.GetValueOrDefault(id).ShieldFractionOfMaxHp);

    internal static float ShieldFraction(IEnumerable<SimStatus> statuses)
        => statuses.Sum(status => ForStatus(status).ShieldFractionOfMaxHp);

    // A shield is used up by the hit it was counted against. A status carries either a
    // shield or a reduction, never both, so dropping the whole status loses nothing else.
    public static void SpendShields(SimCharacter target)
    {
        foreach (var status in target.ActiveStatuses.ToArray())
        {
            if (!ByStatusId.TryGetValue(status.StatusId, out var m) || !m.IsShield) continue;
            status.End(StatusLossReason.Absorbed);
            Jobs.SageStateHandler.OnBarrierSpent(target, status.StatusId, status.SourceObject);
            if (status.StatusId is 2612 or 2613) RefreshLayeredBarrier(target, status);
            if (status.StatusId == 2596 && target is SimPlayer && PlayerJob.Level >= 84)
                foreach (var member in ActionTargets.FriendlyWithin(target, 15f))
                {
                    member.RemoveStatus(2598);
                    member.AddStatusParam(2598, 0, 15f);
                }
            if (status.StatusId == 3903) Jobs.WhiteMageStateHandler.ApplyDivineAura(target);
        }
    }

    private static void RefreshLayeredBarrier(SimCharacter target, SimStatus barrier)
    {
        var layerId = (ushort)(barrier.StatusId == 2612 ? 2642 : 2643);
        if (target.FindStatus(layerId) is not { RemainingTime: > 0f, Stacks: > 0 } layer) return;
        var remaining = layer.RemainingTime;
        target.AddStatus(layerId, 0f, -1);
        target.AddStatusParam(barrier.StatusId, 0, remaining);
    }
}
