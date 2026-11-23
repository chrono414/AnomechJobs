using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed unsafe class SageStateHandler : IUserActionHandler
{
    private const ushort Eukrasia = 2606, Kardia = 2604, Kardion = 2605, Soteria = 2610;
    internal const ushort DiagnosisBarrier = 2607, PrognosisBarrier = 2609;
    internal const int ToxikonLevel = 66, MaxAddersting = 3;

    internal static bool IsToxikon(uint actionId) => actionId is 24304 or 24316;

    internal static bool IsAugmentedSpell(uint actionId)
        => actionId is 24291 or 24292 or 24293 or 24308 or 24314 or 37032 or 37034;

    public void OnAction(ActionType type, uint actionId, ulong targetId)
    {
        if (type != ActionType.Action || PlayerJob.Current != JobId.Sage || Plugin.Game is not { Player: { } player } game)
            return;

        if (actionId == 24290)
        {
            player.RemoveStatus(Eukrasia);
            player.AddStatusParam(Eukrasia, 0);
            SetEukrasia(true);
        }
        else if (IsAugmentedSpell(actionId))
        {
            player.RemoveStatus(Eukrasia);
            SetEukrasia(false);
        }
        else if (actionId == 24301)
        {
            foreach (var member in ActionTargets.FriendlyWithin(player, 15f))
            {
                member.RemoveStatus(DiagnosisBarrier, player.GameObjectId);
                member.RemoveStatus(PrognosisBarrier, player.GameObjectId);
            }
        }
        else if (actionId == 24285)
        {
            var members = game.World.Party.ActiveMembers();
            var recipient = members.FirstOrDefault(member => (ulong)member.GameObjectId == targetId) ?? player;
            ApplyKardia(player, recipient);
        }
        if (player.HasStatus(Kardia) && actionId is 24283 or 24306 or 24312 or 24289 or 24307 or 24313
                 or 24297 or 24315 or 24304 or 24316 or 24318 or 24293 or 24308 or 24314 or 37032 &&
                 ActionTargets.Hostile(actionId, targetId, player, targetCentered: actionId is not (24297 or 24315 or 37032)).Count > 0)
        {
            player.AddStatus(Soteria, 0, -1);
        }
    }

    public void OnTick(float deltaSeconds)
    {
        if (PlayerJob.Current == JobId.Sage && Plugin.Game?.Player is { } player)
            SyncGauge(!player.Dead && player.HasStatus(Eukrasia),
                !player.Dead && Plugin.Config.UnlimitedToxikon && PlayerJob.Level >= ToxikonLevel);
    }

    internal static void OnBarrierSpent(SimCharacter target, ushort statusId,
        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId source)
    {
        if (PlayerJob.Current != JobId.Sage || PlayerJob.Level < ToxikonLevel || Plugin.Game?.Player is not { Dead: false } sage ||
            sage.GameObjectId != source || (statusId != DiagnosisBarrier && (statusId != PrognosisBarrier || target != sage)))
            return;

        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null && edit.Gauge->Sage.Addersting < MaxAddersting) edit.Gauge->Sage.Addersting++;
    }

    internal static void ApplyKardia(SimPlayer player, SimCharacter recipient)
    {
        if (Plugin.Game is not { } game) return;
        foreach (var member in game.World.Party.ActiveMembers()) member.RemoveStatus(Kardion, player.GameObjectId);
        recipient.AddStatusParam(Kardion, 0, sourceObject: player.GameObjectId);
        player.RemoveStatus(Kardia);
        player.AddStatusParam(Kardia, 0);
    }

    internal static void SetEukrasia(bool active) => SyncGauge(active, refillAddersting: false);

    private static void SyncGauge(bool eukrasia, bool refillAddersting)
    {
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge == null) return;
        edit.Gauge->Sage.Eukrasia = eukrasia ? (byte)1 : (byte)0;
        if (refillAddersting) edit.Gauge->Sage.Addersting = MaxAddersting;
    }
}
