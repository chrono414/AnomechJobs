using AnoMech.Core.UserActions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AnoMech.Windows;

internal static class JobPresetHelp
{
    internal const string Mana = "MP available when the scenario starts.";

    internal static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 360f * ImGuiHelpers.GlobalScale);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    internal static string Resource(StartingResource key) => key switch
    {
        StartingResource.Mp => Mana,
        StartingResource.Lilies => "Healing lilies available to spend on Afflatus Solace or Rapture.",
        StartingResource.BloodLily => "Blood Lily progress. Three stacks bloom the Blood Lily and allow Afflatus Misery.",
        StartingResource.LilyProgress => "Seconds already accumulated toward the next lily. The timer starts over when your lilies are full.",
        StartingResource.Addersgall => "Addersgall stacks available for actions such as Druochole and Kerachole.",
        StartingResource.Addersting => "Addersting stacks available for Toxikon.",
        StartingResource.AddersgallProgress => "Seconds already accumulated toward the next Addersgall stack. The timer starts over at full stacks.",
        StartingResource.Beast => "Beast Gauge available for Warrior gauge spenders.",
        StartingResource.Oath => "Oath Gauge available for Sheltron, Intervention, or Cover at the start.",
        StartingResource.ConfiteorStep =>
            "Sets what the Confiteor button shows at the start.\n" +
            "0 - No chain. Carry in Confiteor Ready to cast Confiteor.\n" +
            "1 - After Confiteor. Next is Blade of Faith.\n" +
            "2 - After Blade of Faith. Next is Blade of Truth.\n" +
            "3 - After Blade of Truth. Next is Blade of Valor.\n\n" +
            "Steps 1-3 require a positive chain time and clear Confiteor Ready. Set Requiescat separately under Carry-in buffs. " +
            "To start after Blade of Valor, use step 0 and Blade of Honor Ready.",
        StartingResource.ConfiteorTime => "Seconds left to press the next blade. Zero clears the chain. " +
            "Requiescat and the basic weaponskill combo have separate timers.",
        StartingResource.Soul => "Soul Gauge available to spend on actions such as Blood Stalk or Gluttony.",
        StartingResource.Shroud => "Shroud Gauge available to spend on Enshroud.",
        StartingResource.LemureShroud => "Lemure Shroud stacks left in an active Enshroud window. Set Enshrouded under Carry-in buffs as well.",
        StartingResource.VoidShroud => "Void Shroud stacks available for Lemure's Slice or Scythe. " +
            "The preset caps this at five minus your remaining Lemure Shroud and clears it outside Enshroud.",
        StartingResource.RattlingCoils => "Rattling Coils available to spend on Uncoiled Fury.",
        StartingResource.SerpentOffering => "Serpent Offerings Gauge available to spend on Reawaken.",
        StartingResource.AnguineTribute => "Anguine Tribute remaining in an active Reawaken sequence. Set Reawakened under Carry-in buffs as well.",
        StartingResource.Heat => "Heat Gauge available to spend on Hypercharge.",
        StartingResource.Battery => "Battery Gauge available for a new Rook or Queen deployment.",
        StartingResource.MachinistSummonSeconds => "Seconds left on a Rook or Queen that was already deployed. Zero starts without a summon.",
        StartingResource.MachinistSummonBattery => "Battery that was spent deploying the active Rook or Queen. " +
            "This field is separate from your current Battery Gauge and applies only if the summon has time left.",
        _ => "Amount available when the scenario starts.",
    };

    internal static string Buff(StartingStatusRule rule)
    {
        var duration = rule.Duration == 0
            ? "Enable to start with this effect active."
            : "Set the seconds remaining at the start. Zero leaves it inactive.";
        var effect = rule.Id switch
        {
            157 => "Carries in the Presence of Mind speed buff.",
            3879 => "Each Sacred Sight stack allows one Glare IV.",
            155 => "Lets the next Cure II cost no MP.",
            1217 => "Lets the next spell cost no MP.",
            3881 => "Lets you use Divine Caress after Temperance.",
            2606 => "Starts with Eukrasia armed so the next compatible spell is already changed to its Eukrasian version.",
            2610 => "Carries in stacks that strengthen Kardion healing.",
            91 or 79 => "Starts with your tank stance on for increased enmity.",
            2677 => "Carries in Warrior's damage buff.",
            1897 => "Enables the upgraded Beast Gauge spender for your level.",
            1177 => "Carries in the remaining free Beast Gauge spender stacks.",
            2663 => "Carries in the immunity to the applicable control and forced-movement effects granted by Inner Release.",
            3833 => "Carries in progress toward Wrathful. Wrathful takes precedence when both are set.",
            3901 => "Lets you use Primal Wrath. Takes precedence over Burgeoning Fury.",
            2624 => "Lets you use Primal Rend.",
            3834 => "Lets you follow Primal Rend with Primal Ruination.",
            2673 => "Empowers the next Holy Spirit or Holy Circle and makes it instant.",
            76 => "Carries in Paladin's Fight or Flight damage buff.",
            3847 => "Lets you use Goring Blade.",
            1902 => "Lets you open the Atonement chain. A preset uses the latest Atonement-chain step you enable.",
            3827 => "Starts after Atonement with Supplication ready.",
            3828 => "Starts after Supplication with Sepulchre ready.",
            1368 => "Carries in spell empowerment. Its timer and stacks are separate from the Confiteor chain step and time.",
            3019 => "Lets you open with Confiteor. Use chain step 0. An active chain with time left clears this buff.",
            3831 => "Lets you use the level 100 follow-up to Blade of Valor. To start just after Valor, use chain step 0.",
            2594 => "Starts with Soulsow prepared so Harvest Moon is ready.",
            2587 => "Carries in Soul Reaver stacks for its follow-up weaponskills. Executioner takes precedence if both are set.",
            3858 => "Carries in Executioner stacks for its follow-up weaponskills. Takes precedence over Soul Reaver.",
            2588 => "Carries in the bonus for Gibbet. Choose the enhancement earned by your previous action.",
            2589 => "Carries in the bonus for Gallows. Choose the enhancement earned by your previous action.",
            2592 => "Carries in Immortal Sacrifice stacks for Plentiful Harvest.",
            2601 => "Carries in Bloodsown Circle, the waiting period before Plentiful Harvest.",
            3905 => "Allows Enshroud without spending Shroud Gauge.",
            3859 => "Carries in Perfectio Occulta, which Communio converts to Perfectio Parata.",
            3860 => "Lets you use Perfectio after Communio. Starting inside Enshroud clears this ready buff.",
            2845 => "Makes the next Harpe instant and reduces the shared dash recast when spent.",
            2593 => "Starts inside Enshroud when you also set at least one Lemure Shroud stack.",
            2590 => "Carries in the bonus for Void Reaping while Enshrouded.",
            2591 => "Carries in the bonus for Cross Reaping while Enshrouded.",
            3857 => "Lets you use Sacrificium while Enshrouded.",
            3668 => "Carries in Viper's damage buff.",
            3669 => "Carries in Viper's speed buff.",
            3671 => "Allows Reawaken without spending Serpent Offerings.",
            3670 => "Starts inside Reawaken when you also set at least one Anguine Tribute.",
            851 => "Carries in the guaranteed critical direct hit for the next eligible weaponskill.",
            3864 => "Allows Hypercharge without spending Heat.",
            2688 => "Carries in the remaining Overheated stacks for Heat Blast or Auto Crossbow.",
            3865 => "Lets you use Excavator after Chain Saw.",
            3866 => "Lets you use Full Metal Field.",
            1205 => "Starts with Flamethrower already channeling. Moving or using another action ends the channel.",
            _ => string.Empty,
        };
        return effect.Length > 0 ? $"{effect}\n\n{duration}" : duration;
    }

    internal static string BuffStacks(StartingStatusRule rule) => rule.Id == 1368
        ? "Unspent Requiescat stacks. A normal burst has 4 before Confiteor, 3 after Confiteor, " +
          "2 after Blade of Faith, and 1 after Blade of Truth."
        : $"Number of {rule.Name} stacks you start with while the buff is active.";
}
