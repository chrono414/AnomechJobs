using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using AnoMech.Scenarios.Umad.P5Maddening;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P5Full;

internal sealed class UmadP5FullSettingsWindow
{
    private readonly UmadP5CelestriadSettingsWindow celestriad = new();
    private readonly UmadP5ExaflaresSettingsWindow exaflares = new();
    private static readonly string[] TankAssignments = ["Surprise Flare", "Surprise Holy"];
    private static readonly string[] WaveOrders = ["Random", "Forward", "Reversed"];
    private static readonly string[] LeadingDiagonals = ["Random", "NE-SW", "NW-SE"];

    public UmadP5FullSettingsWindow() => Overrides = new(celestriad.Overrides, exaflares.Overrides);
    public UmadP5FullStateOverrides Overrides { get; }

    public void Draw()
    {
        if (ImGui.CollapsingHeader("Maddening Orchestra"))
        {
            var assignment = (int)Overrides.Orchestra.MainTankAssignment;
            if (ImGui.Combo("First-set MT assignment", ref assignment, TankAssignments, TankAssignments.Length))
                Overrides.Orchestra.MainTankAssignment = (MaddeningTankAssignment)assignment;
        }

        if (ImGui.CollapsingHeader("Flood"))
        {
            var flood = Overrides.Flood;
            var leading = flood.NeSwFirst switch { true => 1, false => 2, _ => 0 };
            if (ImGui.Combo("Leading diagonal", ref leading, LeadingDiagonals, LeadingDiagonals.Length))
                flood.NeSwFirst = leading switch { 1 => true, 2 => false, _ => null };

            var neSw = (int)flood.LineNeSw;
            if (ImGui.Combo("NE-SW wave order", ref neSw, WaveOrders, WaveOrders.Length))
                flood.LineNeSw = (FloodDirection)neSw;

            var nwSe = (int)flood.LineNwSe;
            if (ImGui.Combo("NW-SE wave order", ref nwSe, WaveOrders, WaveOrders.Length))
                flood.LineNwSe = (FloodDirection)nwSe;
        }

        if (ImGui.CollapsingHeader("Celestriad"))
        {
            ImGui.PushID("FullP5Celestriad");
            celestriad.Draw();
            ImGui.PopID();
        }

        if (ImGui.CollapsingHeader("Exaflares"))
        {
            ImGui.PushID("FullP5Exaflares");
            exaflares.Draw();
            ImGui.PopID();
        }
    }

    public void DrawPerPlayer() => celestriad.DrawPerPlayer();
}
