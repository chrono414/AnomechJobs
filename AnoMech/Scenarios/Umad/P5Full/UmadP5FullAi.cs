using System;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P5Full;

internal static class UmadP5FullAi
{
    private static readonly Vector3[] FellForcesPositions =
    [
        new(0.13f, 0f, -7.53f), new(0f, 0f, -7.61f),
        new(-5.58f, 0f, 5.79f), new(-5.71f, 0f, 5.71f),
        new(5.84f, 0f, 5.79f), new(5.77f, 0f, 5.59f),
        new(5.62f, 0f, 5.75f), new(5.71f, 0f, 5.71f),
    ];

    private static readonly Vector3[] FloodQuadrants =
    [
        new(0f, 0f, -4f), new(4f, 0f, 0f), new(0f, 0f, 4f), new(-4f, 0f, 0f),
    ];

    public static void PlaceFellForces(SimParty party, PartyRole? soloTank = null) =>
        Move(party, slot =>
        {
            var role = (PartyRole)slot;
            var position = FellForcesPositions[slot];
            return soloTank.HasValue && role.IsTank() && role != soloTank
                ? position + new Vector3(role == PartyRole.MainTank ? -5f : 5f, 0f, 0f)
                : position;
        }, speed: 14f);

    public static void MoveToCenter(SimParty party) =>
        Move(party, _ => Vector3.Zero, speed: 8.3f);

    public static void RotateFlood(SimParty party, int quadrant) =>
        Move(party, slot =>
        {
            var angle = slot * MathF.PI / 4f;
            return FloodQuadrants[quadrant] + new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * 1.2f;
        }, speed: 14f);

    private static void Move(SimParty party, Func<int, Vector3> destination, float speed)
    {
        for (var slot = 0; slot < 8; slot++)
            if (party.Get(slot) is { } member && member.IsAlive() && party.IsBotDriven(member))
                member.MoveTo(destination(slot), speed);
    }
}
