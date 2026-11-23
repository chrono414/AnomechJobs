using System;
using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed unsafe class ReaperStateHandler : IUserActionHandler
{
    private const ushort CircleOfSacrifice = 2600, ImmortalSacrifice = 2592, Enshrouded = 2593;
    private const ushort SoulReaver = 2587, Executioner = 3858;
    internal const uint HellsIngress = 24401, HellsEgress = 24402, Regress = 24403;
    internal const ushort Threshold = 2595;
    internal const int HellsgateLevel = 74;
    private const uint HellsgateAreaObject = 1219;
    // UNVERIFIED: the retail radius for this point visual.
    private const float HellsgateRadius = 1f;
    private IDisposable? hellsgate;
    private bool wasEnshrouded;

    public void OnScenarioStart() => OnSessionEnd();
    public void OnSessionEnd()
    {
        if (hellsgate != null) Plugin.Game?.Player?.RemoveStatus(Threshold);
        ClearHellsgate();
        wasEnshrouded = false;
    }

    public void OnAction(LocalActionExecution execution)
    {
        var (_, actionId, targetId, _, _) = execution;
        if (execution.Type != ActionType.Action || PlayerJob.Current != JobId.Reaper || Plugin.Game?.Player is not { } player)
            return;

        var action = Natives.Data.Action(actionId);
        var weaponskill = action?.ActionCategory == 3;
        var offensiveSpell = action?.ActionCategory == 2 && actionId != 24387;
        if ((weaponskill || offensiveSpell) && player.HasStatus(CircleOfSacrifice) && HasEnemyTarget(player, targetId, actionId))
        {
            player.RemoveStatus(CircleOfSacrifice);
            AddSacrifice(player);
        }

        switch (actionId)
        {
            case HellsIngress or HellsEgress when PlayerJob.Level >= HellsgateLevel && execution.Origin is { } origin:
                ClearHellsgate();
                hellsgate = AreaObjects.Spawn(HellsgateAreaObject, player.EntityId, HellsgateRadius, origin);
                break;
            case 24405 when PlayerJob.Level >= 88:
                foreach (var member in ActionTargets.FriendlyWithin(player, 30f))
                {
                    if (member == player || member.Dead) continue;
                    member.RemoveStatus(CircleOfSacrifice, player.GameObjectId);
                    member.AddStatusParam(CircleOfSacrifice, 0, 5f, player.GameObjectId);
                }
                break;
            case 24378:
            case 24379:
                foreach (var target in ActionTargets.Hostile(actionId, targetId, player, targetCentered: false))
                {
                    var duration = Math.Min(60f, 30f + (target.FindStatus(2586, player.GameObjectId)?.RemainingTime ?? 0f));
                    target.RemoveStatus(2586, player.GameObjectId);
                    target.AddStatusParam(2586, 0, duration, player.GameObjectId);
                }
                break;
            case 24385:
                player.RemoveStatus(ImmortalSacrifice);
                break;
            case 24398:
                if (player.HasStatus(3859))
                {
                    player.RemoveStatus(3860);
                    player.AddStatusParam(3860, 0, 30f);
                }
                break;
            case 24386 when player.HasStatus(2845):
                RecastTimes.Advance(HellsIngress, 5f);
                break;
            case 24389 or 24390 or 24391 or 24392:
                player.RemoveStatus(Executioner);
                break;
            case 24393:
                var stacks = player.HasStatus(SoulReaver) ? 1 : 2;
                player.RemoveStatus(SoulReaver);
                if (PlayerJob.Level < 96) player.AddStatusParam(SoulReaver, stacks, 30f);
                break;
        }

        if (!weaponskill && !offensiveSpell) return;
        if (actionId is not (24382 or 24383 or 24384)) player.RemoveStatus(SoulReaver);
        if (actionId is not (36970 or 36971 or 36972)) player.RemoveStatus(Executioner);
    }

    public void OnTick(float deltaSeconds)
    {
        if (hellsgate != null && (PlayerJob.Current != JobId.Reaper ||
            Plugin.Game?.Player is not { Dead: false } reaper || !reaper.HasStatus(Threshold)))
            ClearHellsgate();

        if (PlayerJob.Current != JobId.Reaper || Plugin.Game?.Player is not { } player) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge == null) return;
        var status = player.FindStatus(Enshrouded);
        if (!player.Dead && status != null && edit.Gauge->Reaper.LemureShroud > 0)
        {
            edit.Gauge->Reaper.EnshroudedTimeRemaining = (ushort)Math.Clamp(status.RemainingTime * 1000f, 0f, ushort.MaxValue);
            wasEnshrouded = true;
            return;
        }

        if (!wasEnshrouded && status == null) return;
        wasEnshrouded = false;
        edit.Gauge->Reaper.EnshroudedTimeRemaining = 0;
        edit.Gauge->Reaper.LemureShroud = 0;
        edit.Gauge->Reaper.VoidShroud = 0;
        foreach (var proc in new ushort[] { Enshrouded, 2590, 2591, 3857 }) player.RemoveStatus(proc);
    }

    private void ClearHellsgate()
    {
        hellsgate?.Dispose();
        hellsgate = null;
    }

    internal void OnPartyAction(SimCharacter member, uint actionId)
    {
        if (PlayerJob.Current != JobId.Reaper || Plugin.Game?.Player is not { Dead: false } player ||
            !player.HasStatus(2601) || member.FindStatus(CircleOfSacrifice, player.GameObjectId) == null ||
            Natives.Data.Action(actionId)?.ActionCategory is not (2 or 3)) return;
        member.RemoveStatus(CircleOfSacrifice, player.GameObjectId);
        AddSacrifice(player);
    }

    private static void AddSacrifice(SimPlayer player)
    {
        var stacks = Math.Min(8, (player.FindStatus(ImmortalSacrifice)?.Stacks ?? 0) + 1);
        player.RemoveStatus(ImmortalSacrifice);
        player.AddStatusParam(ImmortalSacrifice, stacks, 30f);
    }

    internal static void OnEnemyDefeated(SimEnemy enemy)
    {
        if (PlayerJob.Current != JobId.Reaper || PlayerJob.Level < 50 || Plugin.Game?.Player is not { Dead: false } player ||
            enemy.FindStatus(2586, player.GameObjectId) == null) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) edit.Gauge->Reaper.Soul = (byte)Math.Min(100, edit.Gauge->Reaper.Soul + 10);
    }

    private static bool HasEnemyTarget(SimPlayer player, ulong targetId, uint actionId)
        => actionId is 24386 or 24388 or 24398
            ? ActionTargets.Hostile(actionId, targetId, player, targetCentered: true).Count != 0
            : ActionTargets.Hostile(actionId, targetId, player, targetCentered: false).Count != 0 ||
              Plugin.Game?.World.Children.OfType<SimEnemy>()
                  .Any(target => target.Targetable && !target.Dead && (ulong)target.GameObjectId == targetId) == true;
}
