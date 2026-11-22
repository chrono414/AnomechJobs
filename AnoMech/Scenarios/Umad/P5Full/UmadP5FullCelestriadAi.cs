using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

using AnoMech.Scenarios.Umad.P5Celestriad;

namespace AnoMech.Scenarios.Umad.P5Full;
internal static class UmadP5FullCelestriadAi
{

    private const float MoveSpeed = 6f;
    private const float PairOffset = 1f;
    private const float HalfOffset = 2f;

    public static void Schedule(UmadP5CelestriadState state, SimWorld world, float start)
    {
        world.Events.Add(start + 7.6f, () => PlaceSet(world, state, 0, half: 0f));
        world.Events.Add(start + 12.18f, () => PlaceSet(world, state, 0, HalfFor(state, 0)));
        world.Events.Add(start + 15.9f, () => PlaceSet(world, state, 1, half: 0f));
        world.Events.Add(start + 22.1f, () => PlaceSet(world, state, 2, half: 0f));
        world.Events.Add(start + 24.34f, () => PlaceSet(world, state, 2, HalfFor(state, 2)));
    }

    private static float HalfFor(UmadP5CelestriadState state, int set) =>
        state.AeroVariant[set] is { } choice
            ? (choice == CatastrophicChoice.Aero ? 1f : -1f)
            : 0f;

    private static void PlaceSet(SimWorld world, UmadP5CelestriadState state, int set, float half)
    {
        var party = world.Party;
        var freeRoles = state.PlayerDebuffElement.Where(kv => kv.Value is null).Select(kv => kv.Key).ToArray();
        foreach (var group in state.SetActiveTowers[set]
                     .Select(towerIndex => state.AllTowers[towerIndex])
                     .GroupBy(tower => tower.Element))
        {
            var debuffedRoles = state.PlayerDebuffElement
                .Where(kv => kv.Value is not null && state.ElementForSet(kv.Key, set) == group.Key)
                .Select(kv => kv.Key)
                .ToArray();

            var towers = group.ToArray();
            for (var i = 0; i < towers.Length; i++)
                PlaceAtTower(party, towers[i], i == 0 ? debuffedRoles : freeRoles, half);
        }
    }

    private static void PlaceAtTower(SimParty party, CelestriadTower tower, IReadOnlyList<PartyRole> roles, float half)
    {
        var inward = Vector3.Normalize(-tower.Position);
        var lateral = new Vector3(-inward.Z, 0f, inward.X);

        for (var i = 0; i < roles.Count; i++)
        {
            var bot = party.Get(roles[i]);
            if (bot is null || !bot.IsAlive()) continue;

            var side = i == 0 ? -1f : 1f;
            var dest = tower.Position + lateral * (side * PairOffset) + inward * (half * HalfOffset);
            bot.MoveTo(dest, MoveSpeed);
        }
    }
}

