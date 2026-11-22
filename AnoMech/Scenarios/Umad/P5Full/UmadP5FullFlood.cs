using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Full;

internal sealed partial class UmadP5FullRun
{
    private static readonly (float Telegraph, float StackCue, float Wave, float Stack)[] FloodBeats =
    [
        (0.044f, 6.014f, 6.103f, 6.637f),
        (1.114f, 7.038f, 7.126f, 7.662f),
        (2.094f, 8.019f, 8.152f, 8.642f),
        (3.117f, 9.042f, 9.176f, 9.709f),
    ];

    private void ScheduleFlood(float start, float fellForcesEnd)
    {
        world.Events.Add(start, () => kefka?.Cast(ActionId.FloodCast, animationLock: 3.1f));
        world.Events.Add(fellForcesEnd + 0.10f, () => UmadP5FullAi.RotateFlood(party, flood.StartQuadrant));

        for (var tick = 0; tick < FloodBeats.Length; tick++)
        {
            var beat = FloodBeats[tick];
            var neSw = tick % 2 == 0 ? flood.NeSwFirst : !flood.NeSwFirst;
            var reverse = neSw ? flood.NeSwReversed : flood.NwSeReversed;
            var firstPair = (tick < 2) != reverse;
            var rotation = (neSw ? 1f : -1f) * MathF.PI / 4f;
            var a = FloodAnchor(firstPair ? 5f : 15f, neSw);
            var b = FloodAnchor(firstPair ? -15f : -5f, neSw);
            var offset = new Vector3(MathF.Sin(rotation), 0f, MathF.Cos(rotation)) * 20f;
            var fromA = new Placement(a + offset, rotation);
            var fromB = new Placement(b + offset, rotation);

            world.Events.Add(start + beat.Telegraph, () =>
            {
                kefka?.CastFromHelper(ActionId.FloodTelegraph, a, at: fromA, animationLock: 1.1f);
                kefka?.CastFromHelper(ActionId.FloodTelegraph, b, at: fromB, animationLock: 1.1f);
            });

            SimCharacter? stackTarget = null;
            world.Events.Add(start + beat.StackCue, () =>
            {
                for (var attempt = 0; attempt < 8 && stackTarget is null; attempt++)
                    if (party.Get(flood.PickStackTarget()) is { } member && member.IsAlive())
                        stackTarget = member;
                if (stackTarget is not null)
                {
                    var stack = UmadActions.ChaoticFlood with
                    {
                        Timing = new() { ResolveSnapshotOffset = beat.Stack - beat.StackCue, DamageDelay = 0.64f },
                    };
                    kefka?.CastFromHelper(stack, stackTarget, at: new Placement(Vector3.Zero, MathF.PI));
                }
            });
            world.Events.Add(start + beat.Wave, () =>
            {
                kefka?.CastFromHelper(UmadActions.FloodWave, a, at: fromA);
                kefka?.CastFromHelper(UmadActions.FloodWave, b, at: fromB);
            });

            if (tick is 1 or 2)
            {
                var step = flood.RotationClockwise ? tick : -tick;
                var quadrant = (flood.StartQuadrant + step + 4) % 4;
                world.Events.Add(start + beat.Stack + 0.04f, () => UmadP5FullAi.RotateFlood(party, quadrant));
            }
        }
    }

    private static Vector3 FloodAnchor(float perp, bool neSw) =>
        new Vector3((neSw ? 1f : -1f) * (-20f + perp), 0f, -20f - perp) / MathF.Sqrt(2f);
}
