using AnoMech.Core.SimObjects;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Umad.P5Full;

internal sealed partial class UmadP5FullRun
{
    private readonly record struct FellVolley(float Cue, float Lock, float Snapshot);

    private static bool IsInvulnerable(SimCharacter? tank) => tank is not null &&
        (tank.HasStatus(82) || tank.HasStatus(409) || tank.HasStatus(810) || tank.HasStatus(1836));

    private void ScheduleFellForces(float start, float move, PartyRole tankRole, Rng rng, FellVolley[] volleys,
        bool soloOpening = false)
    {
        world.Events.Add(start + move, () => UmadP5FullAi.PlaceFellForces(party,
            soloOpening && IsInvulnerable(party.Get(tankRole)) ? tankRole : null));

        if (soloOpening && volleys.Length > 2)
            world.Events.Add(start + volleys[1].Snapshot + 0.10f, () => UmadP5FullAi.PlaceFellForces(party));

        foreach (var volley in volleys)
        {
            var healerRole = rng.NextObj(RegenHealer, ShieldHealer);
            var dpsRole = rng.NextObj(MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps);
            SimCharacter? tank = null;
            SimCharacter? healer = null;
            SimCharacter? dps = null;
            world.Events.Add(start + volley.Cue, () => kefka?.Cast(ActionId.FellForcesCue, animationLock: 1.1f));
            world.Events.Add(start + volley.Lock, () =>
            {
                party.ForEachActive(member => member.AddStatus(StatusId.MagicVulnerabilityUp, 0.96f));
                tank = party.Get(tankRole);
                healer = party.Get(healerRole);
                dps = party.Get(dpsRole);
            });
            world.Events.Add(start + volley.Snapshot, () =>
            {
                if (tank is not null)
                {
                    var invuln = IsInvulnerable(tank);
                    kefka?.CastFromHelper(invuln ? UmadActions.FellForcesInvuln : UmadActions.FellForcesTank, tank);
                }
                if (healer is not null) kefka?.CastFromHelper(UmadActions.FellForcesHealer, healer);
                if (dps is not null) kefka?.CastFromHelper(UmadActions.FellForcesDps, dps);
            });
        }
    }
}
