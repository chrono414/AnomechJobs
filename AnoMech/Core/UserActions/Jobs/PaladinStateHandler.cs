using System;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed class PaladinStateHandler : IUserActionHandler
{
    internal const uint PassageAction = 7385;
    internal const ushort PassageOfArms = 1175;
    internal const ushort ArmsUp = 1176;
    internal const ushort PassageLoopTimeline = 5188;
    private const float ArmsUpDuration = 5f;
    private const float ArmsUpRefreshInterval = 3f;
    private ActionChannel? passage;
    private float untilArmsUpRefresh;

    internal static bool IsConfiteorCombo(uint actionId)
        => actionId is 16459 or 25748 or 25749 or 25750;

    internal static bool BreaksConfiteorCombo(uint actionId)
        => actionId is 9 or 15 or 21 or 3539 or 7381 or 16457;

    public void OnScenarioStart() => OnSessionEnd();
    public void OnSessionEnd()
    {
        if (Plugin.Game?.Player is { } player) EndPassage(player);
        else passage = null;
    }

    public void OnAction(ActionType type, uint actionId)
    {
        if (PlayerJob.Current != JobId.Paladin || Plugin.Game?.Player is not { } player) return;
        if (type == ActionType.Action && actionId == PassageAction)
        {
            var origin = ActionChannel.From(player);
            passage = origin;
            untilArmsUpRefresh = ArmsUpRefreshInterval;
            ApplyArmsUp(player, origin);
            return;
        }

        EndPassage(player);
        if (type == ActionType.Action && BreaksConfiteorCombo(actionId))
            JobActions.ClearPaladinConfiteorCombo();
    }

    public void OnCastStart(ActionType type, uint actionId)
    {
        if (passage.HasValue && Plugin.Game?.Player is { } player) EndPassage(player);
    }

    public void OnTick(float deltaSeconds)
    {
        if (passage is not { } origin || Plugin.Game is not { Player: { } player } game) return;
        if (PlayerJob.Current != JobId.Paladin || origin.IsInterrupted(player) || !player.HasStatus(PassageOfArms))
        {
            EndPassage(player);
            return;
        }

        untilArmsUpRefresh -= game.SimulationDelta(deltaSeconds);
        if (untilArmsUpRefresh > 0f) return;
        untilArmsUpRefresh = ArmsUpRefreshInterval;
        ApplyArmsUp(player, origin);
    }

    private static void ApplyArmsUp(SimPlayer player, ActionChannel origin)
    {
        foreach (var member in ActionTargets.FriendlyWithin(player, 8f))
        {
            if (member == player || member.Dead || !IsBehind(origin, member.Position)) continue;
            member.RemoveStatus(ArmsUp, player.GameObjectId);
            member.AddStatusParam(ArmsUp, 0, ArmsUpDuration, player.GameObjectId);
        }
    }

    internal static ActionEffectData PassageEffect(GameObjectId self, Vector3 worldPosition, float rotation, ushort sequence)
    {
        // The self status carries the wing loop timeline. Its removal ends the channel visual.
        var status = new ActionEffectEntry(0x0F, 100, 0, (byte)(PassageLoopTimeline & 0xFF), 0, 0x80, PassageOfArms);
        return new(PassageAction, ActionType.Action, 0.6f, (ushort)PassageAction, 0, 0, rotation, worldPosition,
            AnimationTarget: self, Targets: [new ActionEffectTarget(self, [status])], SourceSequence: sequence);
    }

    private static bool IsBehind(ActionChannel origin, Vector3 target)
    {
        var offset = new Vector2(target.X - origin.Position.X, target.Z - origin.Position.Z);
        var back = new Vector2(-MathF.Sin(origin.Rotation), -MathF.Cos(origin.Rotation));
        return Vector2.Dot(offset, back) >= offset.Length() * 0.5f;
    }

    private void EndPassage(SimPlayer player)
    {
        if (!passage.HasValue) return;
        passage = null;
        player.RemoveStatus(PassageOfArms);
    }
}
