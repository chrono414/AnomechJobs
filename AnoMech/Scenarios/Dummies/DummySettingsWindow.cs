using System.Linq;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Uwu;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dummies;

internal sealed class DummySettingsWindow
{
    private static readonly (string Label, IPhase Phase, EnemySpawnConfig Target)[] Arenas =
    [
        DummyArena.Create("TOP", TopZone.P6, TopConstants.BNpcBaseId.AlphaOmega,
            TopConstants.BNpcNameId.AlphaOmega, TopConstants.BNpcLayoutId.AlphaOmega),
        DummyArena.Create("DMU", UmadZone.P5, UmadConstants.BNpcBaseId.KefkaP5,
            UmadConstants.BNpcNameId.Kefka, UmadConstants.BNpcLayoutId.KefkaP5),
        DummyArena.Create("FRU", FruZone.P5, FruConstants.BNpcBaseId.Pandora,
            FruConstants.BNpcNameId.Pandora, FruConstants.LayoutId.PandoraP5),
        DummyArena.Create("UWU", UwuZone.Ultima, UwuConstants.BNpcBaseId.UltimaWeapon,
            UwuConstants.BNpcNameId.UltimaWeapon, UwuConstants.BNpcLayoutId.UltimaWeapon, modeFlags: 0x11),
    ];

    private static readonly string[] Labels = Arenas.Select(arena => arena.Label).ToArray();
    private int selected;

    internal IPhase Phase => Arenas[selected].Phase;
    internal EnemySpawnConfig Target => Arenas[selected].Target;

    internal void Draw(bool locked)
    {
        if (!SettingsGrid.Begin("##dummy-settings")) return;
        SettingsGrid.Row("Arena");
        SettingsGrid.ItemWidth(150);
        ImGui.BeginDisabled(locked);
        ImGui.Combo("##dummy-arena", ref selected, Labels, Labels.Length);
        ImGui.EndDisabled();
        if (locked && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Leave the current arena to choose another.");
        SettingsGrid.End();
    }
}
