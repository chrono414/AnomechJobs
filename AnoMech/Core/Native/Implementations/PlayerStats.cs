using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.Native.Implementations;

internal readonly record struct AttackTiming(int Haste, int Delay);

internal static unsafe class PlayerStats
{
    public static PlayerStatsPacket Read()
    {
        var packet = new PlayerStatsPacket();
        var playerState = &UIState.Instance()->PlayerState;

        foreach (var row in Plugin.DataManager.GetExcelSheet<BaseParam>())
        {
            if (row.RowId < playerState->Attributes.Length && row.PacketIndex is >= 0 and < PlayerStatsPacket.AttributesCapacity)
            {
                packet.Attributes[row.PacketIndex] = playerState->Attributes[(int)row.RowId];
            }
        }

        var baseStats = &playerState->BaseStrength;
        for (var i = 0; i < PlayerStatsPacket.BaseStatsCapacity; i++)
        {
            packet.BaseStats[i] = baseStats[i];
        }

        return packet;
    }

    public static int? PacketIndex(uint baseParamId)
    {
        if (!Plugin.DataManager.GetExcelSheet<BaseParam>().TryGetRow(baseParamId, out var row)) return null;
        int index = row.PacketIndex;
        return index is >= 0 and < PlayerStatsPacket.AttributesCapacity ? index : null;
    }

    public static void Submit(PlayerStatsPacket packet)
        => PacketDispatcherPointers.HandlePlayerStatsPacket(0, &packet);

    public static AttackTiming? ReadAttackTiming()
    {
        var ui = UIState.Instance();
        if (ui == null) return null;
        return new AttackTiming(ui->PlayerState.Attributes[(int)PlayerAttribute.Haste],
            ui->PlayerState.Attributes[(int)PlayerAttribute.Delay]);
    }

    public static bool TrySetAttackTiming(AttackTiming timing)
    {
        if (PacketIndex((uint)PlayerAttribute.Haste) is not { } hasteIndex
            || PacketIndex((uint)PlayerAttribute.Delay) is not { } delayIndex)
            return false;

        var packet = Read();
        packet.Attributes[hasteIndex] = timing.Haste;
        packet.Attributes[delayIndex] = timing.Delay;
        Submit(packet);
        return true;
    }
}
