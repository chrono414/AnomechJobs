using System;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Read waiver and follow-up eligibility before consuming statuses. The native combo
// still reflects the pre-action step here, before ActionEffectHandler advances it.
internal sealed class JobActionHandler : IUserActionHandler
{
    private readonly Random rng = new();

    public void OnAction(ActionType actionType, uint actionId, ulong targetId)
    {
        if (actionType != ActionType.Action) return;
        var player = Plugin.Game?.Player;
        if (player == null) return;

        var earnsFury = PlayerJob.Level >= 96 && actionId is 3549 or 3550 && player.HasStatus(1177);
        var waiver = GaugeCostWaiver(actionId);
        if (waiver == 0 || !player.HasStatus(waiver)) JobActions.ApplyCost(actionId);
        JobActions.ClearStatuses(player, actionId);

        JobActions.ApplyEffects(player, actionId, targetId, rng);
        if (earnsFury && ActionTargets.Hostile(new ActionContext(actionId, targetId, player, rng)).Count > 0)
            AddBurgeoningFury(player);
    }

    private static void AddBurgeoningFury(SimPlayer player)
    {
        const ushort burgeoningFury = 3833, wrathful = 3901;
        var stacks = (player.FindStatus(burgeoningFury)?.Stacks ?? 0) + 1;
        player.RemoveStatus(burgeoningFury);
        if (stacks < 3) player.AddStatusParam(burgeoningFury, stacks, 30f);
        else
        {
            player.RemoveStatus(wrathful);
            player.AddStatusParam(wrathful, 0, 30f);
        }
    }

    internal static ushort GaugeCostWaiver(uint actionId) => actionId switch
    {
        3549 or 3550 => 1177, // Inner Release
        17209 => 3864,         // Hypercharged
        24394 => 3905,         // Ideal Host
        34626 => 3671,         // Ready to Reawaken
        _ => 0,
    };

    internal static bool UsesDivineMight(uint actionId, SimPlayer player)
        => actionId is 7384 or 16458 && player.HasStatus(2673);
}
