using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P5Maddening;

internal sealed class UmadP5MaddeningSettingsWindow
{
    public UmadP5MaddeningStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (!SettingsGrid.Begin("##p5maddening")) return;
        SettingsGrid.FightOnlyNote();
        SettingsGrid.Row("MT assignment");
        if (ImGui.RadioButton("Surprise Flare", Overrides.MainTankAssignment == MaddeningTankAssignment.Flare))
            Overrides.MainTankAssignment = MaddeningTankAssignment.Flare;
        ImGui.SameLine();
        if (ImGui.RadioButton("Surprise Holy", Overrides.MainTankAssignment == MaddeningTankAssignment.Holy))
            Overrides.MainTankAssignment = MaddeningTankAssignment.Holy;
        SettingsGrid.End();
    }
}
