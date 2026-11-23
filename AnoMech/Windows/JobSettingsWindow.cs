using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Core.UserActions.Jobs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

internal sealed class JobSettingsWindow : Window
{
    private readonly MainWindow mainWindow;
    private readonly JobPresetEditor<BlackMageStartingState> blackMageEditor = new();
    private readonly JobPresetEditor<CombatStartingState> combatEditor = new();

    internal JobSettingsWindow(MainWindow mainWindow) : base("Job Settings###AnomechJobsStartingSettings")
    {
        this.mainWindow = mainWindow;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        var scale = ImGuiHelpers.GlobalScale;
        Size = new Vector2(440, 620) * scale;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 220) * scale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnClose()
    {
        blackMageEditor.Close();
        combatEditor.Close();
    }

    public override void Draw()
    {
        if (mainWindow.SelectedScenario is not { } scenario ||
            JobStartingState.ForScenario(scenario.Phase.Zone) is not { } context)
        {
            ImGui.TextWrapped("Equip a supported job and select a scenario at a level you have reached.");
            return;
        }

        var jobsSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        var job = jobsSheet.TryGetRow((uint)context.Job, out var row) ? row.Name.ExtractText() : context.Job.ToString();
        ImGui.TextUnformatted($"{job} (Lv. {context.Level})");
        if (!Natives.UserActions.Enabled) ImGui.TextDisabled("Enable job actions to apply a preset.");

        if (context.Job == JobId.Sage)
        {
            var unlimitedToxikon = Plugin.Config.UnlimitedToxikon;
            if (ImGui.Checkbox("Unlimited Toxikon", ref unlimitedToxikon))
            {
                Plugin.Config.UnlimitedToxikon = unlimitedToxikon;
                Plugin.Config.Save();
            }
            JobPresetHelp.Tooltip("Keeps Addersting full and lets you cast Toxikon without spending it once unlocked. " +
                "Full Addersting generation from Eukrasian Prognosis and Eukrasian Diagnosis is not currently implemented.");
        }

        var maxMp = (int)Natives.BattleCharas.LocalPlayer.MaxMana;
        if (maxMp <= 0) maxMp = 10000;
        var level = context.Level;
        if (context.Job == JobId.BlackMage && BlackMageStartingRules.ForLevel(level) is { } blackMageRules)
        {
            var levels = Plugin.Config.BlackMageStartingPresets;
            if (!levels.TryGetValue(level, out var presets)) levels[level] = presets = new();
            blackMageEditor.Draw(presets, () => new BlackMageStartingState { Mp = maxMp },
                state => EditBlackMage(state, blackMageRules, maxMp));
        }
        else if (CombatStartingRules.ForJob(context.Job) is { } combatRules)
        {
            var jobs = Plugin.Config.CombatStartingPresets;
            if (!jobs.TryGetValue(context.Job, out var levels)) jobs[context.Job] = levels = new();
            if (!levels.TryGetValue(level, out var presets)) levels[level] = presets = new();
            combatEditor.Draw(presets, () => new CombatStartingState(),
                state => EditCombat(state, combatRules, context.Job, level, maxMp));
        }
        else ImGui.TextWrapped("Starting presets are unavailable for this job and level.");
    }

    private static BlackMageStartingState EditBlackMage(BlackMageStartingState draft, BlackMageStartingRules rules, int maxMp)
    {
        draft = rules.Normalize(draft, maxMp);
        var phase = DrawPhase(draft.Phase, rules.PhaseRanks);
        var paradox = phase != BlackMageStartingPhase.None && draft.Paradox;
        ImGui.BeginDisabled(!rules.HasParadox || phase == BlackMageStartingPhase.None);
        ImGui.Checkbox("Paradox available", ref paradox);
        ImGui.EndDisabled();
        JobPresetHelp.Tooltip("Start with Paradox ready to cast. Requires Astral Fire or Umbral Ice.");
        var firestarter = draft.Firestarter;
        var thundercloud = draft.Thundercloud;
        ImGui.Checkbox("Firestarter available", ref firestarter);
        JobPresetHelp.Tooltip("Carry in Firestarter for an instant, MP-free Fire III.");
        ImGui.Checkbox("Thundercloud available", ref thundercloud);
        JobPresetHelp.Tooltip("Carry in Thunderhead for the next Thunder spell.");

        return rules.Normalize(draft with
        {
            Phase = phase,
            Paradox = paradox,
            Firestarter = firestarter,
            Thundercloud = thundercloud,
            Mp = Slider("MP", draft.Mp, maxMp, tooltip: JobPresetHelp.Mana),
            UmbralHearts = Slider("Umbral Hearts", draft.UmbralHearts, rules.Hearts,
                tooltip: "Umbral Hearts available at the start to offset the MP cost increase of fire spells."),
            AstralSoul = Slider("Astral Soul", (int)phase > 0 ? draft.AstralSoul : 0, rules.Soul, (int)phase <= 0,
                tooltip: "Astral Soul stacks toward Flare Star. This field requires Astral Fire."),
            EnochianProgress = Slider("Enochian progress", phase != BlackMageStartingPhase.None ? draft.EnochianProgress : 0,
                rules.EnochianSeconds, phase == BlackMageStartingPhase.None, "%d s",
                tooltip: "Seconds already accumulated toward the next Polyglot stack while in Astral Fire or Umbral Ice."),
            Polyglot = Slider("Polyglot", draft.Polyglot, rules.Polyglot,
                tooltip: "Polyglot stacks available to spend on Foul or Xenoglossy."),
            Amplifier = Cooldown("Amplifier", draft.Amplifier, rules.Amplifier),
            LeyLines = DrawCharges("Ley Lines", draft.LeyLines, rules.LeyLinesCharges, rules.LeyLinesRecharge),
            Addle = Cooldown("Addle", draft.Addle, rules.Addle),
            Manaward = Cooldown("Manaward", draft.Manaward, rules.Manaward),
            Triplecast = DrawCharges("Triplecast", draft.Triplecast, rules.TriplecastCharges, rules.TriplecastRecharge),
            Swiftcast = Cooldown("Swiftcast", draft.Swiftcast, rules.Swiftcast),
        }, maxMp);
    }

    private static CombatStartingState EditCombat(CombatStartingState draft, CombatStartingRules rules, JobId job, int level, int maxMp)
    {
        var resources = new Dictionary<StartingResource, int>();
        foreach (var rule in rules.Resources.Where(rule => rule.Level <= level))
        {
            var max = CombatStartingRules.ResourceMax(rule, level, maxMp);
            var capped = rule.Key switch
            {
                StartingResource.LilyProgress => resources.GetValueOrDefault(StartingResource.Lilies) >= 3,
                StartingResource.AddersgallProgress => resources.GetValueOrDefault(StartingResource.Addersgall) >= 3,
                _ => false,
            };
            var value = capped ? 0 : rules.ReadResource(draft, rule, level, maxMp);
            var format = rule.Key is StartingResource.LilyProgress or StartingResource.AddersgallProgress
                or StartingResource.ConfiteorTime or StartingResource.MachinistSummonSeconds ? "%d s" : "%d";
            resources[rule.Key] = Slider(rule.Name, value, max, capped, format, tooltip: JobPresetHelp.Resource(rule.Key));
        }

        var recasts = new Dictionary<uint, StartingCharges>(draft.Recasts);
        var cooldownsOpen = ImGui.CollapsingHeader("Cooldowns");
        JobPresetHelp.Tooltip("Set which actions are ready and how far along their recasts are at the start.");
        if (cooldownsOpen)
        {
            foreach (var rule in rules.Recasts.Where(rule => rule.Level <= level))
            {
                var max = rule.ChargesAt(level);
                var recharge = rule.RechargeAt(level);
                var value = rule.Normalize(draft, level);
                if (max > 1) recasts[rule.Action] = DrawCharges(rule.Name, value, max, recharge);
                else
                {
                    var remaining = Cooldown(rule.Name, value.SecondsToNext, recharge);
                    recasts[rule.Action] = new(remaining == 0 ? 1 : 0, remaining);
                }
            }
        }

        var statuses = new Dictionary<ushort, StartingStatus>(draft.Statuses);
        var buffsOpen = ImGui.CollapsingHeader("Carry-in buffs");
        JobPresetHelp.Tooltip("Start with status effects that were already active before this part of the fight.");
        if (buffsOpen)
        {
            foreach (var rule in rules.Statuses.Where(rule => rule.Level <= level))
            {
                ImGui.PushID(rule.Id);
                var value = rule.Normalize(draft);
                if (rule.Duration == 0)
                {
                    var active = rule.IsActive(value);
                    ImGui.Checkbox(rule.Name, ref active);
                    JobPresetHelp.Tooltip(JobPresetHelp.Buff(rule));
                    statuses[rule.Id] = new(0, active ? 1 : 0);
                }
                else
                {
                    var remaining = Slider(rule.Name, value.Remaining, rule.Duration,
                        format: value.Remaining == 0 ? "Inactive" : "%d s", tooltip: JobPresetHelp.Buff(rule));
                    var stacks = rule.MaxStacks == 0 || remaining == 0 ? 0 :
                        Slider($"{rule.Name} stacks", value.Stacks == 0 ? rule.MaxStacks : value.Stacks, rule.MaxStacks,
                            min: 1, tooltip: JobPresetHelp.BuffStacks(rule));
                    statuses[rule.Id] = new(remaining, stacks);
                }
                ImGui.PopID();
            }
        }

        var combo = draft.ComboAction;
        var comboTime = draft.ComboTime;
        var kardion = draft.KardionTarget;
        var comboOptions = rules.Combos.Where(rule => rule.Level <= level).ToArray();
        if (comboOptions.Length > 0)
        {
            var comboOpen = ImGui.CollapsingHeader("Carry-in combo");
            JobPresetHelp.Tooltip("Start partway through a basic weaponskill combo.");
            if (comboOpen)
            {
                var optionsOpen = ImGui.BeginCombo("While combo is active",
                    comboOptions.FirstOrDefault(rule => rule.Action == combo).Name ?? "None");
                JobPresetHelp.Tooltip("Choose the last weaponskill already completed so you can start with its combo follow-up.");
                if (optionsOpen)
                {
                    if (ImGui.Selectable("None", combo == 0)) combo = 0;
                    foreach (var rule in comboOptions)
                        if (ImGui.Selectable(rule.Name, combo == rule.Action)) combo = rule.Action;
                    ImGui.EndCombo();
                }
                if (combo != 0)
                    comboTime = Slider("Combo time remaining", comboTime > 0 ? comboTime : 30, 30, format: "%d s", min: 1,
                        tooltip: "Seconds left to continue the basic weaponskill combo when the scenario starts.");
            }
        }
        if (job == JobId.Sage && level >= 4)
        {
            var targetsOpen = ImGui.BeginCombo("Kardion target", kardion?.ToString() ?? "None");
            JobPresetHelp.Tooltip("Choose the party role that already has Kardion at the start. Choose None to start without a target.");
            if (targetsOpen)
            {
                if (ImGui.Selectable("None", kardion == null)) kardion = null;
                foreach (var role in Enum.GetValues<PartyRole>())
                    if (ImGui.Selectable(role.ToString(), kardion == role)) kardion = role;
                ImGui.EndCombo();
            }
        }

        return draft with { Resources = resources, Recasts = recasts, Statuses = statuses,
            ComboAction = combo, ComboTime = comboTime, KardionTarget = kardion };
    }

    private static BlackMageStartingPhase DrawPhase(BlackMageStartingPhase phase, int ranks)
    {
        var open = ImGui.BeginCombo("Phase", PhaseName(phase));
        JobPresetHelp.Tooltip("Choose the starting rank of Astral Fire or Umbral Ice. Choose None to start outside either phase.");
        if (!open) return phase;
        foreach (var option in Enum.GetValues<BlackMageStartingPhase>().OrderBy(value => (int)value < 0 ? 3 - (int)value : (int)value))
            if (Math.Abs((int)option) <= ranks && ImGui.Selectable(PhaseName(option), option == phase))
                phase = option;
        ImGui.EndCombo();
        return phase;
    }

    private static string PhaseName(BlackMageStartingPhase phase) => phase switch
    {
        BlackMageStartingPhase.AstralFireI => "Astral Fire I",
        BlackMageStartingPhase.AstralFireII => "Astral Fire II",
        BlackMageStartingPhase.AstralFireIII => "Astral Fire III",
        BlackMageStartingPhase.UmbralIceI => "Umbral Ice I",
        BlackMageStartingPhase.UmbralIceII => "Umbral Ice II",
        BlackMageStartingPhase.UmbralIceIII => "Umbral Ice III",
        _ => "None",
    };

    private static StartingCharges DrawCharges(string label, StartingCharges value, int max, int recharge)
    {
        var charges = Slider($"{label} charges", value.Available, max,
            tooltip: $"Number of {label} charges usable immediately at the start.");
        var nextChargeTooltip = $"Seconds until another {label} charge becomes available. Zero starts with all charges ready.";
        if (charges == max)
        {
            var remaining = Cooldown($"{label} next charge", 0, recharge, nextChargeTooltip);
            return remaining == 0 ? new(max, 0) : new(max - 1, remaining);
        }

        var next = value.Available == max ? recharge : value.SecondsToNext;
        next = Cooldown($"{label} next charge", next, recharge, nextChargeTooltip);
        return next == 0 ? new(max, 0) : new(charges, next);
    }

    private static int Cooldown(string label, int value, int max, string? tooltip = null)
        => Slider(label, value, max, format: value == 0 ? "Not on cooldown" : "%d s",
            tooltip: tooltip ?? $"Seconds until {label} is ready at the start. Zero makes it usable immediately.");

    private static int Slider(string label, int value, int max, bool disabled = false, string format = "%d", int min = 0,
        string? tooltip = null)
    {
        ImGui.BeginDisabled(disabled);
        ImGui.SliderInt(label, ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp);
        ImGui.EndDisabled();
        if (tooltip != null) JobPresetHelp.Tooltip(tooltip);
        return value;
    }
}
