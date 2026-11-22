using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Core.UserActions.Jobs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

internal sealed class JobSettingsWindow : Window
{
    private const string DefaultName = "Default";
    private readonly MainWindow mainWindow;
    private int editingLevel;
    private string name = "";
    private string? pendingDelete;
    private BlackMageStartingState draft = new();

    internal JobSettingsWindow(MainWindow mainWindow) : base("Job Settings###AnomechJobsStartingSettings")
    {
        this.mainWindow = mainWindow;
        var scale = ImGuiHelpers.GlobalScale;
        Size = new Vector2(440, 620) * scale;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 220) * scale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnClose() => pendingDelete = null;

    public override void Draw()
    {
        if (mainWindow.SelectedScenario is { } scenario &&
            JobStartingState.ForScenario(scenario.Phase.Zone.TerritoryId) is { Job: JobId.BlackMage } context &&
            BlackMageStartingRules.ForLevel(context.Level) is { } rules)
        {
            var maxMp = (int)Natives.BattleCharas.LocalPlayer.MaxMana;
            DrawBlackMage(context.Level, rules, maxMp > 0 ? maxMp : 10000);
        }
        else
        {
            editingLevel = 0;
            pendingDelete = null;
            ImGui.TextWrapped("Equip Black Mage and select a level-100 scenario to edit starting presets.");
        }
    }

    private void DrawBlackMage(int level, BlackMageStartingRules rules, int maxMp)
    {
        ImGui.TextUnformatted($"Black Mage · Lv. {level}");
        if (!Natives.UserActions.Enabled)
            ImGui.TextDisabled("Enable job actions to apply a preset.");

        var levels = Plugin.Config.BlackMageStartingPresets;
        if (!levels.TryGetValue(level, out var presets))
            levels[level] = presets = new();
        if (editingLevel != level)
        {
            editingLevel = level;
            LoadDraft(presets, maxMp);
        }

        var selected = presets.Active != null ? presets.Selected : "";
        if (ImGui.BeginCombo("Preset", selected.Length == 0 ? DefaultName : selected))
        {
            if (ImGui.Selectable(DefaultName, selected.Length == 0)) Select(presets, "", maxMp);
            foreach (var key in presets.Presets.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                if (ImGui.Selectable(key, key == selected)) Select(presets, key, maxMp);
            ImGui.EndCombo();
        }

        draft = rules.Normalize(draft, maxMp);
        var phase = DrawPhase(draft.Phase, rules.PhaseRanks);
        var paradox = phase != BlackMageStartingPhase.None && draft.Paradox;
        ImGui.BeginDisabled(!rules.HasParadox || phase == BlackMageStartingPhase.None);
        ImGui.Checkbox("Paradox available", ref paradox);
        ImGui.EndDisabled();
        var firestarter = draft.Firestarter;
        var thundercloud = draft.Thundercloud;
        ImGui.Checkbox("Firestarter available", ref firestarter);
        ImGui.Checkbox("Thundercloud available", ref thundercloud);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Grants Thunderhead for the next Thunder spell.");

        draft = rules.Normalize(draft with
        {
            Phase = phase,
            Paradox = paradox,
            Firestarter = firestarter,
            Thundercloud = thundercloud,
            Mp = Slider("MP", draft.Mp, maxMp),
            UmbralHearts = Slider("Umbral Hearts", draft.UmbralHearts, rules.Hearts),
            AstralSoul = Slider("Astral Soul", (int)phase > 0 ? draft.AstralSoul : 0, rules.Soul, (int)phase <= 0),
            EnochianProgress = Slider("Enochian progress", phase != BlackMageStartingPhase.None ? draft.EnochianProgress : 0,
                rules.EnochianSeconds, phase == BlackMageStartingPhase.None, "%d s"),
            Polyglot = Slider("Polyglot", draft.Polyglot, rules.Polyglot),
            Amplifier = Cooldown("Amplifier", draft.Amplifier, rules.Amplifier),
            LeyLines = DrawCharges("Ley Lines", draft.LeyLines, rules.LeyLinesCharges, rules.LeyLinesRecharge),
            Addle = Cooldown("Addle", draft.Addle, rules.Addle),
            Manaward = Cooldown("Manaward", draft.Manaward, rules.Manaward),
            Triplecast = DrawCharges("Triplecast", draft.Triplecast, rules.TriplecastCharges, rules.TriplecastRecharge),
            Swiftcast = Cooldown("Swiftcast", draft.Swiftcast, rules.Swiftcast),
        }, maxMp);

        ImGui.InputText("Preset name", ref name, 65);
        var keyToSave = name.Trim();
        var existing = presets.Presets.Keys.FirstOrDefault(key => string.Equals(key, keyToSave, StringComparison.OrdinalIgnoreCase));
        ImGui.BeginDisabled(keyToSave.Length == 0 || string.Equals(keyToSave, DefaultName, StringComparison.OrdinalIgnoreCase));
        if (ImGui.Button(existing == null ? "Save preset" : "Update preset"))
        {
            var key = existing ?? keyToSave;
            presets.Presets[key] = draft;
            presets.Selected = key;
            name = key;
            pendingDelete = null;
            Plugin.Config.Save();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Reset edits")) LoadDraft(presets, maxMp);
        DrawDeletePreset(presets, maxMp);
        ImGui.TextDisabled("Saved selection applies on the next pull.");
    }

    private void DrawDeletePreset(StartingStatePresets<BlackMageStartingState> presets, int maxMp)
    {
        ImGui.SameLine();
        ImGui.BeginDisabled(presets.Active == null);
        if (ImGui.Button("Delete preset")) pendingDelete = presets.Selected;
        ImGui.EndDisabled();

        if (pendingDelete is not { } key || presets.Selected != key || presets.Active == null) return;
        ImGui.TextWrapped($"Delete preset \"{key}\"?");
        if (ImGui.Button("Delete##confirm-job-preset"))
        {
            presets.Presets.Remove(key);
            Select(presets, "", maxMp);
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel##cancel-delete-job-preset")) pendingDelete = null;
    }

    private void Select(StartingStatePresets<BlackMageStartingState> presets, string key, int maxMp)
    {
        presets.Selected = key;
        LoadDraft(presets, maxMp);
        Plugin.Config.Save();
    }

    private void LoadDraft(StartingStatePresets<BlackMageStartingState> presets, int maxMp)
    {
        pendingDelete = null;
        name = presets.Active != null ? presets.Selected : "";
        draft = presets.Active ?? new BlackMageStartingState { Mp = maxMp };
    }

    private static BlackMageStartingPhase DrawPhase(BlackMageStartingPhase phase, int ranks)
    {
        if (!ImGui.BeginCombo("Phase", PhaseName(phase))) return phase;
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
        var charges = Slider($"{label} charges", value.Available, max);
        if (charges == max)
        {
            var remaining = Cooldown($"{label} next charge", 0, recharge);
            return remaining == 0 ? new(max, 0) : new(max - 1, remaining);
        }

        var next = value.Available == max ? recharge : value.SecondsToNext;
        next = Cooldown($"{label} next charge", next, recharge);
        return next == 0 ? new(max, 0) : new(charges, next);
    }

    private static int Cooldown(string label, int value, int max)
        => Slider(label, value, max, format: value == 0 ? "Not on cooldown" : "%d s");

    private static int Slider(string label, int value, int max, bool disabled = false, string format = "%d")
    {
        ImGui.BeginDisabled(disabled);
        ImGui.SliderInt(label, ref value, 0, max, format, ImGuiSliderFlags.AlwaysClamp);
        ImGui.EndDisabled();
        return value;
    }
}
