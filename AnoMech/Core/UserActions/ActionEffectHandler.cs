using System.Numerics;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions.Jobs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.UserActions;

// The server's ActionEffect for the player's own actions, which the sim firewall eats: replayed so
// the client plays the action landing (the hit on its target) and, for a linear combo step, carries
// the Type-0x1B marker that advances the client's own combo. Combo routes (Gnashing Fang etc.) use
// a separate system. Dedup of queued/re-entrant executions is UserActions' job, so OnAction only
// sees actual, once-per-execution calls.
internal sealed unsafe class ActionEffectHandler : IUserActionHandler
{
    public void OnAction(LocalActionExecution action)
    {
        if (action.Type != ActionType.Action) return;
        var (_, actionId, targetId, location, sequence) = action;
        var role = PlayerCombo.RoleOf(actionId);

        var player = BattleCharaProxy.LocalPlayer;
        if (!player.Exists) return;
        var am = ActionManager.Instance();
        if (am == null) return;
        if (actionId is BlackMageStateHandler.LeyLines or BlackMageStateHandler.Retrace)
        {
            player.ReceiveActionEffect(LeyLinesEffect(actionId, player.GameObjectId, player.Position, player.Rotation,
                sequence));
            return;
        }
        if (actionId == PaladinStateHandler.PassageAction)
        {
            player.ReceiveActionEffect(PaladinStateHandler.PassageEffect(player.GameObjectId, player.Position,
                player.Rotation, sequence));
            return;
        }

        // SourceSequence makes the client treat it as that action resolving.
        var previousCombo = am->Combo.Action;
        var comboRemaining = am->Combo.Timer;
        var groundTargeted = location.HasValue && Natives.Data.Action(actionId) is { TargetArea: true };
        var target = groundTargeted ? default : DeliveryTarget(player, targetId);
        // A zero-lock reply clears the lock the client started for an instant action.
        player.ReceiveActionEffect(new ActionEffectData(
            actionId, ActionType.Action, System.Math.Max(0f, am->AnimationLock), (ushort)actionId, 0, 0, player.Rotation,
            groundTargeted ? location!.Value : player.Position,
            AnimationTarget: groundTargeted ? null : target,
            // A linear combo step needs its marker on the delivered target.
            Targets: groundTargeted ? [] : [role == ComboRole.None
                ? new ActionEffectTarget(target)
                : new ActionEffectTarget(target, [ActionEffectEntry.ActionMarker(actionId, role == ComboRole.Continuation)])],
            SourceSequence: sequence));

        // Use the native combo for button swaps and Vipersight, with Viper's full route rules
        // taking precedence over the sheet's single-prerequisite model.
        if (PlayerJob.Current == JobId.Viper &&
            ViperBasicCombo.NextState(actionId, role, previousCombo, comboRemaining) is { } combo)
            PlayerCombo.Seed(combo.Action, combo.Remaining);
        else if (PlayerJob.Current == JobId.Machinist &&
            PlayerCombo.MachinistNextState(actionId, role) is { } shot)
            PlayerCombo.Seed(shot.Action, shot.Remaining);
        else if (PaladinStateHandler.IsConfiteorCombo(actionId) && comboRemaining > 0f)
            PlayerCombo.Seed(previousCombo, comboRemaining);
    }

    internal static ActionEffectData LeyLinesEffect(uint actionId, GameObjectId self, Vector3 worldPosition,
        float rotation, ushort sourceSequence)
    {
        // Ley Lines applies its status to the source. Retrace reports the existing status on the target.
        var status = actionId == BlackMageStateHandler.LeyLines
            ? new ActionEffectEntry(0x0F, 0, 0, 0, 0, 0x80, BlackMageStateHandler.LeyLinesStatus)
            : ActionEffectEntry.StatusGain(BlackMageStateHandler.LeyLinesStatus);
        return new ActionEffectData(actionId, ActionType.Action, 0.6f, (ushort)actionId, 0, 0, rotation, worldPosition,
            Targets: [new ActionEffectTarget(self, [status])], SourceSequence: sourceSequence);
    }

    private static GameObjectId DeliveryTarget(BattleCharaProxy player, ulong targetId)
    {
        var target = (GameObjectId)targetId;
        // ActionEffect can only deliver to a BattleChara registered with the client.
        return targetId is not (0 or 0xE0000000) && Natives.BattleCharas.IsInCharacterManager(target.ObjectId)
            ? target : player.GameObjectId;
    }
}
