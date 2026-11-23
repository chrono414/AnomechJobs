using AnoMech.Core.Native.Implementations;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// SerpentComboState packs the follow-up kind above the low two bits, which count presses left.
// DreadCombo tracks the Coil/Den branch and uses values outside the named enum for Generations.
internal sealed unsafe class ViperStateHandler : IUserActionHandler
{
    private const int CoilTwins = 7, DenTwins = 8, UncoiledTwins = 9;
    private const int TwinPresses = 2, TailPresses = 1;
    private const int ReawakenedStep = 7;
    private const ushort Reawakened = 3670;
    internal const int TributeUpgradeLevel = 96;

    private const ushort HuntersVenom = 3657, SwiftskinsVenom = 3658, FellhuntersVenom = 3659, FellskinsVenom = 3660;
    private const ushort PoisedForTwinfang = 3665, PoisedForTwinblood = 3666;
    private const float VenomSeconds = 30f, PoisedSeconds = 60f;
    private static readonly AnyGcdPredicate Gcd = new();

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Viper || Plugin.Game?.Player is not { Dead: false } player) return;
        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;
        var g = &jgm->Viper;

        g->SerpentComboState = ExpireBasicComboFollowUp(g->SerpentComboState, actionId);

        switch (actionId)
        {
            // Melee finishers arm Serpent's Tail:
            case 34610 or 34611 or 34612 or 34613 when PlayerCombo.IsActiveContinuation(actionId):
                ArmTail(g, 34634, SerpentCombo.DeathRattle);
                break;
            case 34618 or 34619 when PlayerCombo.IsActiveContinuation(actionId):
                ArmTail(g, 34635, SerpentCombo.LastLash);
                break;
            // Reawaken steps through the Generations, each arming its Legacy:
            case 34626:
                g->DreadCombo = (DreadCombo)ReawakenedStep;
                SyncReawakened(g, player.FindStatus(Reawakened)?.RemainingTime ?? 0f);
                break;
            case 34631: SyncReawakened(g, 0f); break;
            case 34627: Generation(g, 34640, SerpentCombo.FirstLegacy); break;
            case 34628: Generation(g, 34641, SerpentCombo.SecondLegacy); break;
            case 34629: Generation(g, 34642, SerpentCombo.ThirdLegacy); break;
            case 34630: Generation(g, 34643, SerpentCombo.FourthLegacy); break;
            case 34634 or 34635 or 34640 or 34641 or 34642 or 34643: SpendPress(g); break;   // Serpent's Tail follow-ups

            // Vicewinder / Vicepit open both coils / dens once each; each leaves two follow-ups:
            case 34620: Open(g, DreadCombo.Dreadwinder); break;    // Vicewinder
            case 34623: Open(g, DreadCombo.PitOfDread); break;     // Vicepit
            case 34621: Window(g, DreadCombo.HuntersCoil, DreadCombo.SwiftskinsCoil, CoilTwins, 34636); break;
            case 34622: Window(g, DreadCombo.SwiftskinsCoil, DreadCombo.HuntersCoil, CoilTwins, 34636); break;
            case 34624: Window(g, DreadCombo.HuntersDen, DreadCombo.SwiftskinsDen, DenTwins, 34638); break;
            case 34625: Window(g, DreadCombo.SwiftskinsDen, DreadCombo.HuntersDen, DenTwins, 34638); break;
            case 34633:
                g->SerpentComboState = 0;
                if (UnlocksEffect.IsUnlocked(34644)) SetFollowUps(g, UncoiledTwins, TwinPresses);
                break;

            case 34636: Twin(g, player, HuntersVenom, SwiftskinsVenom, VenomSeconds); break;          // Twinfang Bite
            case 34637: Twin(g, player, SwiftskinsVenom, HuntersVenom, VenomSeconds); break;          // Twinblood Bite
            case 34638: Twin(g, player, FellhuntersVenom, FellskinsVenom, VenomSeconds); break;       // Twinfang Thresh
            case 34639: Twin(g, player, FellskinsVenom, FellhuntersVenom, VenomSeconds); break;       // Twinblood Thresh
            case 34644: Twin(g, player, PoisedForTwinfang, PoisedForTwinblood, PoisedSeconds); break; // Uncoiled Twinfang
            case 34645: Twin(g, player, PoisedForTwinblood, PoisedForTwinfang, PoisedSeconds); break; // Uncoiled Twinblood
        }

        if (g->AnguineTribute == 0 && player.HasStatus(Reawakened))
        {
            player.RemoveStatus(Reawakened);
            SyncReawakened(g, 0f);
        }
    }

    internal static byte ExpireBasicComboFollowUp(byte followUps, uint actionId)
        => (SerpentCombo)(followUps >> 2) is (SerpentCombo.DeathRattle or SerpentCombo.LastLash) && Gcd.Matches(actionId)
            ? (byte)0 : followUps;

    public void OnTick(float deltaSeconds)
    {
        if (PlayerJob.Current != JobId.Viper || Plugin.Game?.Player is not { } player) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge == null) return;
        var gauge = &edit.Gauge->Viper;
        SyncReawakened(gauge, player.Dead ? 0f : player.FindStatus(Reawakened)?.RemainingTime ?? 0f);
        if (player.Dead)
        {
            gauge->SerpentComboState = 0;
            gauge->DreadCombo = 0;
        }
    }

    internal static void SeedReawakened(int level, float remaining)
    {
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge == null) return;
        var gauge = &edit.Gauge->Viper;
        var maxTribute = level >= TributeUpgradeLevel ? 5 : 4;
        var generationsUsed = maxTribute - gauge->AnguineTribute;
        gauge->DreadCombo = generationsUsed < 4 ? (DreadCombo)(ReawakenedStep + generationsUsed) : 0;
        SyncReawakened(gauge, remaining);
    }

    private static void SyncReawakened(ViperGauge* gauge, float remaining)
    {
        gauge->ReawakenedTimer = (ushort)(remaining * 1000f);
        if (remaining > 0f) return;
        gauge->AnguineTribute = 0;
        if (gauge->DreadCombo >= (DreadCombo)ReawakenedStep) gauge->DreadCombo = 0;
    }

    private static void ArmTail(ViperGauge* gauge, uint followUpAction, SerpentCombo kind)
    {
        gauge->SerpentComboState = 0;
        if (UnlocksEffect.IsUnlocked(followUpAction)) SetFollowUps(gauge, (int)kind, TailPresses);
    }

    private static void SetFollowUps(ViperGauge* g, int kind, int presses)
        => g->SerpentComboState = (byte)((kind << 2) | presses);

    private static void SpendPress(ViperGauge* g)
    {
        var left = (g->SerpentComboState & 3) - 1;
        g->SerpentComboState = left > 0 ? (byte)((g->SerpentComboState & ~3) | left) : (byte)0;
    }

    private static void Generation(ViperGauge* g, uint actionId, SerpentCombo legacy)
    {
        ArmTail(g, actionId, legacy);
        g->DreadCombo = legacy == SerpentCombo.FourthLegacy ? 0 : g->DreadCombo + 1;
    }

    private static void Open(ViperGauge* g, DreadCombo window)
    {
        g->DreadCombo = window;
        g->SerpentComboState = 0;
    }

    private static void Window(ViperGauge* g, DreadCombo branch, DreadCombo other, int twins, uint followUpAction)
    {
        g->DreadCombo = g->DreadCombo == other ? 0 : branch;
        g->SerpentComboState = 0;
        if (UnlocksEffect.IsUnlocked(followUpAction)) SetFollowUps(g, twins, TwinPresses);
    }

    // Only a buffed first press passes its potency bonus to the second press.
    private static void Twin(ViperGauge* g, SimPlayer player, ushort own, ushort partner, float partnerSeconds)
    {
        var handsOver = (g->SerpentComboState & 3) == TwinPresses && player.HasStatus(own);
        player.RemoveStatus(own);
        player.RemoveStatus(partner);
        if (handsOver) player.AddStatusParam(partner, 0, partnerSeconds);
        SpendPress(g);
    }
}
