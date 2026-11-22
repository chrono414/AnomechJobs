using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Umad.P5Maddening;

internal sealed class UmadP5MaddeningAi(string name, PartyRole[] spreadOrder) : IScenarioAi<UmadP5MaddeningState>
{
    internal static readonly UmadP5MaddeningAi[] Strategies =
    [
        new("Healers Left (6xv)", [MainTank, OffTank, CasterDps, PhysRangedDps, MeleeDpsB, MeleeDpsA, ShieldHealer, RegenHealer]),
        new("Healers South (du0)", [MainTank, OffTank, MeleeDpsB, CasterDps, ShieldHealer, RegenHealer, PhysRangedDps, MeleeDpsA]),
        new("JP Yan", [MainTank, OffTank, CasterDps, ShieldHealer, MeleeDpsB, MeleeDpsA, RegenHealer, PhysRangedDps]),
    ];

    private static readonly Vector3[] SpreadSlots =
    [
        new(-3.72f, 0f, -8.99f), new(3.72f, 0f, -8.99f), new(8.99f, 0f, -3.72f), new(8.99f, 0f, 3.72f),
        new(4.58f, 0f, 8.57f), new(-2.11f, 0f, 9.73f), new(-7.61f, 0f, 6.34f), new(-9.58f, 0f, -1.69f),
    ];

    private static readonly Vector3[] FinalPositions =
    [
        new(0f, 0f, -19.03f), new(0f, 0f, -9.73f),
        new(-11.42f, 0f, 6.77f), new(-6.34f, 0f, 8.46f),
        new(-2.54f, 0f, 9.30f), new(2.54f, 0f, 9.30f),
        new(8.46f, 0f, 8.03f), new(13.53f, 0f, 6.34f),
    ];

    public string Name { get; } = name;

    public void Run(UmadP5MaddeningState state, SimWorld world) =>
        Schedule(world, state, UmadP5MaddeningScenario.StandaloneCastAt, UmadP5MaddeningScenario.First);

    internal void Schedule(SimWorld world, UmadP5MaddeningState state, float castAt, UmadP5MaddeningScenario.Timing timing)
    {
        var party = world.Party;
        world.Events.Add(castAt - 0.8f, () => MoveBots(party, (role, _) => SpreadPosition(role)));
        world.Events.Add(castAt + timing.FirstHit.Snapshot + 0.10f, () =>
            MoveBots(party, (role, member) => SecondHitPosition(state, role, member)));
        world.Events.Add(castAt + timing.SecondHit.Snapshot + 0.10f, () =>
            MoveBots(party, (role, _) => FinalPosition(state, role)));
        world.Events.Add(castAt + timing.FinalHit.Snapshot - 0.25f, () => UseInvulnerability(party, state.HolyTank));
    }

    private Vector3 SpreadPosition(PartyRole role) => SpreadSlots[Array.IndexOf(spreadOrder, role)];

    private Vector3 SecondHitPosition(UmadP5MaddeningState state, PartyRole role, SimCharacter member)
    {
        if (role == state.FlareTank) return new Vector3(0.13f, 0f, -9.64f);
        if (role == state.HolyTank) return new Vector3(-0.13f, 0f, -9.81f);

        var spread = SpreadPosition(role);
        var wasHit = member.HasStatus(UmadConstants.StatusId.MagicVulnerabilityUp);
        return spread + Vector3.Normalize(spread) * (wasHit ? 2.11f : -2.11f);
    }

    private static Vector3 FinalPosition(UmadP5MaddeningState state, PartyRole role) =>
        FinalPositions[role == state.FlareTank ? 0 : role == state.HolyTank ? 1 : (int)role];

    private static void MoveBots(SimParty party, Func<PartyRole, SimCharacter, Vector3> position)
    {
        foreach (var member in party.ActiveMembers())
            if (member is ISimPartyMember seat && party.IsBotDriven(member))
                member.MoveTo(position(seat.Role, member), speed: 14f);
    }

    private static void UseInvulnerability(SimParty party, PartyRole role)
    {
        if (party.Get(role) is not SimPartyNpc bot || !bot.IsAlive() || bot.ActionsLocked) return;
        var (action, status) = (JobId)bot.ClassJob switch
        {
            JobId.Paladin => (30u, 82),
            JobId.Warrior => (43u, 409),
            JobId.DarkKnight => (3638u, 810),
            JobId.Gunbreaker => (16152u, 1836),
            _ => (0u, 0),
        };
        if (action == 0) return;
        bot.PlayAction(action);
        bot.AddStatus((ushort)status, 10f);
    }
}
