using System;
using System.Linq;
using System.Numerics;

namespace AnoMech.Scenarios.Umad;

internal sealed class UmadP5ForsakenState
{
    internal readonly record struct BlackHole(Vector3 Position, byte MapIndex);

    private const float Scale = 40f / 94.6f;
    internal const float BlackHoleRadius = 20f * Scale;

    private static readonly BlackHole[] Cardinals =
    [
        new(new(0f, 0f, -32f * Scale), 0x16),
        new(new(32f * Scale, 0f, 0f), 0x15),
        new(new(-32f * Scale, 0f, 0f), 0x17),
        new(new(0f, 0f, 32f * Scale), 0x18),
    ];

    private readonly Rng rng;
    internal BlackHole[][] Waves { get; }

    internal UmadP5ForsakenState(Rng rng)
    {
        this.rng = rng;
        var outer = rng.Shuffle<BlackHole>(
            new BlackHole(new(-32f * Scale, 0f, -32f * Scale), 0x1B),
            new BlackHole(new(32f * Scale, 0f, -32f * Scale), 0x1A),
            new BlackHole(new(-32f * Scale, 0f, 32f * Scale), 0x1C),
            new BlackHole(new(32f * Scale, 0f, 32f * Scale), 0x19));
        Waves =
        [
            [new(Vector3.Zero, 0x14), outer[3]],
            [outer[2], outer[1]],
            [outer[0], default],
            new BlackHole[2],
        ];
    }

    internal void SelectFinalBlackHoles(Vector3 bait)
    {
        const float tieTolerance = 0.0001f;
        var nearestDistance = Cardinals.Min(hole => Vector3.DistanceSquared(hole.Position, bait));
        var nearest = Cardinals.Where(hole =>
            MathF.Abs(Vector3.DistanceSquared(hole.Position, bait) - nearestDistance) <= tieTolerance);
        var selected = rng.NextObj(nearest.ToArray());
        var remaining = rng.Shuffle(Cardinals.Where(hole => hole != selected));
        Waves[2][1] = selected;
        Waves[3][0] = remaining[0];
        Waves[3][1] = remaining[1];
    }
}
