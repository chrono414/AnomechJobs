using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad;

internal static class UmadP5ForsakenAi
{
    internal static void MoveToCenter(SimParty party)
    {
        foreach (var member in party.ActiveMembers())
            if (member != party.Player && party.IsBotDriven(member))
                member.MoveTo(Vector3.Zero, speed: 14f);
    }
}
