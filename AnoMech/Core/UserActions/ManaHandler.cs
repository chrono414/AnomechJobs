using System;
using AnoMech.Core.Native.Interfaces;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

internal sealed class ManaHandler : IUserActionHandler
{
    private const ushort ThinAir = 1217, Freecure = 155;
    private float tick;

    public void OnScenarioStart() => tick = 0f;
    public void OnSessionEnd() => tick = 0f;

    public void OnAction(ActionType type, uint actionId)
    {
        var job = PlayerJob.Current;
        if (!UsesMana(job) || Plugin.Game?.Player is not { Dead: false } player) return;
        var row = type == ActionType.Action && Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()
            .TryGetRow(actionId, out var action) ? action : default;
        var cost = row.PrimaryCostType is 3 or 70 ? row.PrimaryCostValue * 100 : 0;
        if (job == JobId.Paladin && PlayerJob.Level >= 64) cost /= 2;

        if (cost > 0 && job == JobId.WhiteMage)
        {
            if (actionId == 135 && player.HasStatus(Freecure)) cost = 0;
            else if (player.HasStatus(ThinAir))
            {
                player.RemoveStatus(ThinAir);
                cost = 0;
            }
        }

        var max = (int)Natives.BattleCharas.LocalPlayer.MaxMana;
        var restore = actionId switch
        {
            3571 when job == JobId.WhiteMage => max / 20,
            24296 or 24298 or 24299 or 24303 when job == JobId.Sage => max * 7 / 100,
            15 when job == JobId.Paladin && PlayerJob.Level >= 58 && PlayerCombo.IsActiveContinuation(actionId) => 1000,
            16457 when job == JobId.Paladin && PlayerJob.Level >= 72 && PlayerCombo.IsActiveContinuation(actionId) => 1000,
            29 when job == JobId.Paladin && PlayerJob.Level >= 58 => 500,
            16460 or 36918 or 36919 or 25747 when job == JobId.Paladin => 500,
            _ => 0,
        };
        SetMana(restore - cost);
    }

    public void OnTick(float deltaSeconds)
    {
        if (!UsesMana(PlayerJob.Current) || Plugin.Game is not { Player: { Dead: false } player } game)
        {
            tick = 0f;
            return;
        }

        tick += game.SimulationDelta(deltaSeconds);
        if (tick < 3f) return;
        var pulses = (int)(tick / 3f);
        tick %= 3f;
        var baseRegen = Plugin.Condition[ConditionFlag.InCombat] ? 200 : 600;
        var lucid = player.HasStatus(JobActions.LucidDreamingStatusId) ? 550 : 0;
        SetMana(pulses * (baseRegen + lucid));
    }

    private static bool UsesMana(JobId job) => job is JobId.WhiteMage or JobId.Sage or JobId.Paladin;

    private static void SetMana(int delta)
    {
        if (delta == 0 || Plugin.Game?.Player is not { } player) return;
        var native = Natives.BattleCharas.LocalPlayer;
        var value = (uint)Math.Clamp((int)native.Mana + delta, 0, (int)native.MaxMana);
        if (value != native.Mana) player.SetMana(value);
    }
}
