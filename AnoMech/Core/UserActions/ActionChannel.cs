using System;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

internal readonly record struct ActionChannel(Vector3 Position, float Rotation)
{
    internal static ActionChannel From(SimPlayer player) => new(player.Position, player.Rotation);

    internal bool IsInterrupted(SimPlayer player)
        => player.Dead || Vector3.DistanceSquared(Position, player.Position) > 0.0001f ||
           MathF.Abs(MathF.IEEERemainder(Rotation - player.Rotation, 2 * MathF.PI)) > 0.01f ||
           Natives.PlayerInput.MovementInputActive || Natives.PlayerInput.IsJumping;
}
