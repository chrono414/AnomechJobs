using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Umad.P5Maddening;

internal sealed class UmadP5MaddeningState
{
    public PartyRole FlareTank { get; }
    public PartyRole HolyTank { get; }
    public IReadOnlyList<PartyRole> FirstHolyPriority { get; }

    public UmadP5MaddeningState(Rng rng, MaddeningTankAssignment mainTankAssignment)
    {
        (FlareTank, HolyTank) = mainTankAssignment == MaddeningTankAssignment.Flare
            ? (MainTank, OffTank)
            : (OffTank, MainTank);
        FirstHolyPriority = rng.Shuffle<PartyRole>([RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps]);
    }
}
