using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions.Jobs;

internal sealed unsafe class MachinistStateHandler : IUserActionHandler
{
    internal const ushort Flamethrower = 1205;
    private const ushort Overheated = 2688, Wildfire = 1946, WildfireOnEnemy = 861;
    private const byte OverheatActive = 1, SummonActive = 2;
    private static readonly EnemySpawnConfig Rook = new(3256, 3666, EnemyList: EnemyListMode.Never,
        Engaged: false, IsHostile: false);
    private static readonly EnemySpawnConfig Queen = new(10490, 8230, EnemyList: EnemyListMode.Never,
        Engaged: false, IsHostile: false);

    private float summonRemaining;
    private byte summonBattery;
    private MachinistSummon? summon;
    private ActionChannel? channelOrigin;
    private SimEnemy? wildfireTarget;
    private int wildfireHits;
    private bool readStartingState;

    public void OnScenarioStart()
    {
        Reset();
        readStartingState = true;
    }
    public void OnSessionEnd() => Reset();

    private void Reset()
    {
        DespawnSummon();
        readStartingState = false;
        summonRemaining = 0;
        summonBattery = 0;
        channelOrigin = null;
        wildfireTarget = null;
        wildfireHits = 0;
    }

    // Deployment reads Battery before the shared cost pass empties it.
    public void OnAction(ActionType type, uint actionId, ulong targetId)
    {
        if (PlayerJob.Current != JobId.Machinist || Plugin.Game?.Player is not { } player) return;
        if (channelOrigin.HasValue) EndChannel(player);
        if (type != ActionType.Action) return;
        switch (actionId)
        {
            case 2864 or 16501:
                using (var edit = JobGaugeEdit.Begin())
                {
                    if (edit.Gauge == null || edit.Gauge->Machinist.Battery < 50) return;
                    summonBattery = edit.Gauge->Machinist.Battery;
                    summonRemaining = actionId == 16501 ? 12f : 10f;
                    WriteSummon(edit.Gauge, summonRemaining, summonBattery);
                }
                SpawnSummon(Plugin.Game.World, player, actionId == 16501, (byte)PlayerJob.Level);
                break;
            case 7415 or 16502:
                summonRemaining = 0f;
                summon?.RequestFinisher();
                using (var edit = JobGaugeEdit.Begin())
                    if (edit.Gauge != null) WriteSummon(edit.Gauge, summonRemaining, summonBattery);
                break;
            case 7418:
                channelOrigin = ActionChannel.From(player);
                player.AddStatusParam(Flamethrower, 0, 10f);
                break;
            case 2878:
                ClearWildfire(player);
                wildfireTarget = Plugin.Game.World.Children.OfType<SimEnemy>()
                    .FirstOrDefault(enemy => enemy.Targetable && !enemy.Dead && (ulong)enemy.GameObjectId == targetId);
                if (wildfireTarget != null) wildfireTarget.AddStatusParam(WildfireOnEnemy, 0, 10f, player.GameObjectId);
                break;
            case 16766:
                ClearWildfire(player);
                break;
            default:
                if (wildfireTarget is { } enemy && player.HasStatus(Wildfire) && wildfireHits < 6 &&
                    Natives.Data.Action(actionId)?.ActionCategory == 3 &&
                    (ActionTargets.Hostile(actionId, targetId, player, targetCentered: true).Contains(enemy) ||
                     ActionTargets.Hostile(actionId, targetId, player, targetCentered: false).Contains(enemy)))
                    wildfireHits++;
                break;
        }
    }

    public void OnTick(float deltaSeconds)
    {
        if (PlayerJob.Current != JobId.Machinist || Plugin.Game is not { Player: { } player } game) return;
        if (channelOrigin is { } origin && (!player.HasStatus(Flamethrower) || origin.IsInterrupted(player)))
            EndChannel(player);

        if (wildfireTarget != null && (!player.HasStatus(Wildfire) || player.Dead || wildfireTarget.Dead || !wildfireTarget.IsActive))
        {
            ClearWildfire(player);
            player.RemoveStatus(Wildfire);
        }

        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge == null) return;
        ref var gauge = ref edit.Gauge->Machinist;
        if (readStartingState)
        {
            readStartingState = false;
            if ((gauge.TimerActive & SummonActive) != 0)
            {
                (summonRemaining, summonBattery) = (gauge.SummonTimeRemaining / 1000f, gauge.LastSummonBatteryPower);
                if (!player.Dead && summonRemaining > 0f)
                    SpawnSummon(game.World, player, PlayerJob.Level >= 80, (byte)PlayerJob.Level);
            }
            if (player.HasStatus(Flamethrower)) channelOrigin ??= ActionChannel.From(player);
        }
        var simulationDelta = game.SimulationDelta(deltaSeconds);
        summonRemaining = player.Dead ? 0f : Math.Max(0f, summonRemaining - simulationDelta);
        if (player.Dead) DespawnSummon();
        else if (summon is { } deployed)
        {
            if (summonRemaining <= 0f) deployed.RequestFinisher();
            if (!game.Paused && !deployed.Tick(game.World, simulationDelta)) DespawnSummon();
        }
        var overheat = player.Dead ? null : player.FindStatus(Overheated);
        gauge.OverheatTimeRemaining = (short)((overheat?.RemainingTime ?? 0f) * 1000f);
        gauge.TimerActive = SetFlag(gauge.TimerActive, OverheatActive, overheat != null);
        WriteSummon(edit.Gauge, summonRemaining, summonBattery);
    }

    public void OnCastStart(ActionType type, uint actionId)
    {
        if (channelOrigin.HasValue && Plugin.Game?.Player is { } player) EndChannel(player);
    }

    public uint? AdjustActionId(uint actionId)
    {
        if (PlayerJob.Current != JobId.Machinist || summonRemaining <= 0f || actionId is not (2864 or 16501)) return null;
        return PlayerJob.Level >= 80 ? 16502u : 7415u;
    }

    private void SpawnSummon(SimWorld world, SimPlayer player, bool queen, byte level)
    {
        DespawnSummon();
        var angle = player.Rotation - MathF.PI / 3f;
        var position = player.Position + 2f * new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
        var config = queen ? Queen : Rook;
        var actor = world.SpawnEnemy(config with
        {
            Level = level,
            Placement = new Placement(position, player.Rotation),
        });
        if (actor != null) summon = new MachinistSummon(actor, queen, level);
    }

    private void DespawnSummon()
    {
        summon?.Despawn();
        summon = null;
    }

    private void ClearWildfire(SimPlayer player)
    {
        wildfireTarget?.RemoveStatus(WildfireOnEnemy, player.GameObjectId);
        wildfireTarget = null;
        wildfireHits = 0;
    }

    internal static void WriteSummon(JobGaugeManager* gaugeManager, float summonRemaining, byte summonBattery)
    {
        ref var gauge = ref gaugeManager->Machinist;
        gauge.SummonTimeRemaining = (short)(summonRemaining * 1000f);
        gauge.LastSummonBatteryPower = summonBattery;
        gauge.TimerActive = SetFlag(gauge.TimerActive, SummonActive, summonRemaining > 0f);
    }

    private static byte SetFlag(byte value, byte flag, bool active)
        => (byte)(active ? value | flag : value & ~flag);

    private void EndChannel(SimPlayer player)
    {
        channelOrigin = null;
        player.RemoveStatus(Flamethrower);
    }
}
