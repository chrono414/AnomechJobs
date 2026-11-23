using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.UserActions;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Windows;

internal sealed class JobPresetEditor<T> where T : class
{
    private const string DefaultName = "Default";
    private StartingStatePresets<T>? current;
    private T? draft;
    private string name = "";
    private string? pendingDelete;

    internal void Close() => pendingDelete = null;

    internal void Draw(StartingStatePresets<T> presets, Func<T> defaultState, Func<T, T> edit)
    {
        if (!ReferenceEquals(current, presets))
        {
            current = presets;
            LoadDraft();
        }

        var selected = presets.Active != null ? presets.Selected : "";
        var presetOpen = ImGui.BeginCombo("Preset", selected.Length == 0 ? DefaultName : selected);
        JobPresetHelp.Tooltip("Choose a saved starting state for this job and level. Default uses the built-in settings.");
        if (presetOpen)
        {
            if (ImGui.Selectable(DefaultName, selected.Length == 0)) Select("");
            foreach (var key in presets.Presets.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                if (ImGui.Selectable(key, key == selected)) Select(key);
            ImGui.EndCombo();
        }

        ImGui.InputText("Preset name", ref name, 65);
        JobPresetHelp.Tooltip("Use a new name to save a separate preset, or keep an existing name to update it.");
        var keyToSave = name.Trim();
        var existing = presets.Presets.Keys.FirstOrDefault(key => string.Equals(key, keyToSave, StringComparison.OrdinalIgnoreCase));
        ImGui.BeginDisabled(keyToSave.Length == 0 || string.Equals(keyToSave, DefaultName, StringComparison.OrdinalIgnoreCase));
        var save = ImGui.Button(existing == null ? "Save preset" : "Update preset");
        JobPresetHelp.Tooltip(existing == null ? "Save these settings as a new preset and select it."
            : "Save these settings to the named preset and select it.");
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Reset edits")) LoadDraft();
        JobPresetHelp.Tooltip("Discard unsaved edits and reload the selected preset.");
        ImGui.SameLine();
        ImGui.BeginDisabled(presets.Active == null);
        if (ImGui.Button("Delete preset")) pendingDelete = presets.Selected;
        JobPresetHelp.Tooltip("Delete the selected saved preset and return to Default.");
        ImGui.EndDisabled();

        if (pendingDelete is { } pending && presets.Selected == pending && presets.Active != null)
        {
            ImGui.TextWrapped($"Delete preset \"{pending}\"?");
            if (ImGui.Button("Delete##confirm-job-preset"))
            {
                presets.Presets.Remove(pending);
                Select("");
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##cancel-delete-job-preset")) pendingDelete = null;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped("Click update preset for changes to apply on next restart");
        ImGui.PopStyleColor();
        ImGui.Separator();

        draft ??= defaultState();
        if (ImGui.BeginChild("Starting settings", Vector2.Zero))
            draft = edit(draft);
        ImGui.EndChild();

        if (save)
        {
            var key = existing ?? keyToSave;
            presets.Presets[key] = draft;
            Select(key);
        }

        void Select(string key)
        {
            presets.Selected = key;
            LoadDraft();
            Plugin.Config.Save();
        }

        void LoadDraft()
        {
            pendingDelete = null;
            name = presets.Active != null ? presets.Selected : "";
            draft = presets.Active ?? defaultState();
        }
    }
}
