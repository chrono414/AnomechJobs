using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Polyglot generation and spending are owned by JobActions.
internal sealed unsafe class BlackMageStateHandler : IUserActionHandler
{
    private const uint Fire = 141, FireII = 147, HighFireII = 25794, FireIII = 152, FireIV = 3577;
    private const uint Blizzard = 142, BlizzardII = 25793, HighBlizzardII = 25795, BlizzardIII = 154, BlizzardIV = 3576;
    private const uint Freeze = 159, Flare = 162, FlareStar = 36989, Despair = 16505;
    private const uint UmbralSoul = 16506, Transpose = 149, Manafont = 158, Paradox = 25797;
    internal const uint LeyLines = 3573, Retrace = 36988;
    internal const ushort LeyLinesStatus = 737;
    private const float LeyLinesRadius = 3f;
    private const uint LeyLinesAreaObject = 377;
    private const uint BetweenTheLines = 7419, AetherialManipulation = 155;
    private const uint Scathe = 156, Sleep = 25880;
    internal const ushort CircleOfPower = 738;
    private const ushort Firestarter = 165, Thunderhead = 3870, Medicated = 49;
    private const float LeyLinesDuration = 20f;
    private const float LeyLinesVisualDelay = 0.8f;
    private const float CircleOfPowerDuration = 5f;
    private const float CircleOfPowerRefreshThreshold = 2f;

    private int? mana;
    private float manaTick;
    private Vector3? leyLinesCenter;
    private IDisposable? leyLinesVisual;
    private float leyLinesVisualDelay;
    private bool wasDead;
    private readonly Random rng = new();

    private static int Level => Plugin.ObjectTable.LocalPlayer?.Level ?? 0;
    private static bool InCombat => Plugin.Condition[ConditionFlag.InCombat];

    private Vector3? ActiveLeyLinesCenter(SimPlayer player)
        => !player.Dead && player.HasStatus(LeyLinesStatus) ? leyLinesCenter : null;

    public void OnScenarioStart() => Reset();
    public void OnSessionEnd() => Reset();

    internal void ApplyStartingState(BlackMageStartingState preset, BlackMageStartingRules rules, TimedGaugeHandler timedGauges)
    {
        if (Plugin.Game?.Player is not { Dead: false } player) return;
        var chara = Natives.BattleCharas.LocalPlayer;
        if (!chara.Exists || chara.MaxMana == 0) return;
        var maxMp = (int)chara.MaxMana;
        var state = rules.Normalize(preset, maxMp);
        using (var edit = JobGaugeEdit.Begin())
        {
            if (edit.Gauge == null) return;
            ref var gauge = ref edit.Gauge->BlackMage;
            gauge.ElementStance = (sbyte)state.Phase;
            gauge.EnochianActive = state.Phase != BlackMageStartingPhase.None;
            gauge.ParadoxActive = state.Paradox;
            gauge.UmbralHearts = (byte)state.UmbralHearts;
            gauge.AstralSoulStacks = state.AstralSoul;
            gauge.PolyglotStacks = (byte)state.Polyglot;
            timedGauges.Seed(JobActions.BlackMagePolyglotTimer, edit.Gauge, state.EnochianProgress);
        }

        if (state.Firestarter) GrantProc(player, Firestarter);
        if (state.Thundercloud) GrantProc(player, Thunderhead);
        SetMana(player, state.Mp, maxMp);
        RecastTimes.SeedAfterReset(
        [
            new(25796, 1, 0, rules.Amplifier, state.Amplifier),
            new(LeyLines, rules.LeyLinesCharges, state.LeyLines.Available, rules.LeyLinesRecharge, state.LeyLines.SecondsToNext),
            new(7560, 1, 0, rules.Addle, state.Addle),
            new(157, 1, 0, rules.Manaward, state.Manaward),
            new(7421, rules.TriplecastCharges, state.Triplecast.Available, rules.TriplecastRecharge, state.Triplecast.SecondsToNext),
            new(7561, 1, 0, rules.Swiftcast, state.Swiftcast),
        ]);
    }

    private void Reset()
    {
        mana = null;
        manaTick = 0;
        ClearLeyLines();
        wasDead = false;
    }

    private void ClearLeyLines()
    {
        leyLinesVisual?.Dispose();
        leyLinesVisual = null;
        leyLinesVisualDelay = 0f;
        leyLinesCenter = null;
    }

    private void PlaceLeyLines(SimPlayer player)
    {
        leyLinesCenter = player.Position;
        // The action response and status arrive before the floor object.
        leyLinesVisualDelay = LeyLinesVisualDelay;
    }

    public void OnAction(ActionType actionType, uint actionId, ulong targetId, Vector3? location)
    {
        if (PlayerJob.Current != JobId.BlackMage || Plugin.Game?.Player is not { Dead: false } player) return;
        if (actionType == ActionType.Item)
        {
            ApplyIntelligenceMedicine(player, actionId);
            return;
        }
        if (actionType != ActionType.Action) return;

        if (ResolveMovement(player, actionId, targetId)) return;
        if (TryApplyThunder(player, actionId, targetId)) return;

        var chara = Natives.BattleCharas.LocalPlayer;
        var currentMana = mana ?? (int)chara.Mana;
        var maxMana = (int)chara.MaxMana;
        var landed = (IsIceSpell(actionId) || actionId is FireIV or Flare)
            && ActionTargets.Hostile(actionId, targetId, player, targetCentered: true).Count != 0;
        Resolution resolution;
        using (var edit = JobGaugeEdit.Begin())
        {
            if (edit.Gauge == null) return;
            resolution = Resolve(ref edit.Gauge->BlackMage, actionId, Level, currentMana, maxMana,
                player.HasStatus(Firestarter), InCombat, landed);
        }
        if (!resolution.Executed) return;

        // Set the result from the pre-action balance so native prediction cannot debit it twice.
        if (resolution.Mana != currentMana || actionId is Manafont or UmbralSoul)
            SetMana(player, resolution.Mana, maxMana);
        if (resolution.Thunderhead) GrantProc(player, Thunderhead);
        if (resolution.Firestarter || actionId == Fire && Level >= 42 && rng.NextSingle() < 0.4f)
            GrantProc(player, Firestarter);
    }

    internal readonly record struct Resolution(bool Executed, int Mana, bool Thunderhead, bool Firestarter);

    internal static Resolution Resolve(ref BlackMageGauge gauge, uint id, int level, int mp, int maxMp,
        bool firestarter, bool inCombat, bool landed)
    {
        if (!MeetsGaugeRequirements(gauge, id, mp)) return default;

        var before = gauge.ElementStance;
        var hearts = gauge.UmbralHearts;
        var rank = level >= 35 ? 3 : level >= 20 ? 2 : 1;
        var next = NextStance(id, before, rank, inCombat);
        var cost = ManaCost(id, before, hearts, mp, firestarter);
        if (cost > mp) return default;

        mp -= cost;
        if (next != before)
        {
            gauge.ElementStance = (sbyte)next;
            gauge.EnochianActive = next != 0 && level >= 56;
            if (next <= 0) gauge.AstralSoulStacks = 0;
            if (next == 0)
            {
                gauge.UmbralHearts = 0;
                gauge.ParadoxActive = false;
            }
            else if (level >= 90 && ArmsParadox(before, next, hearts))
                gauge.ParadoxActive = true;
        }

        if (id == Flare) gauge.UmbralHearts = 0;
        else if (UsesUmbralHeart(id, before, hearts, cost)) gauge.UmbralHearts--;

        if (IsIceSpell(id) && landed && before < 0) mp += IceRecovery(before);
        if (id is BlizzardIV or Freeze && landed && level >= 58) gauge.UmbralHearts = 3;
        if (id == UmbralSoul)
        {
            if (level >= 58) gauge.UmbralHearts = inCombat ? (byte)Math.Min(hearts + 1, 3) : (byte)3;
            mp = inCombat ? mp + IceRecovery(next) : maxMp;
        }
        if (id == Manafont)
        {
            mp = maxMp;
            if (level >= 58) gauge.UmbralHearts = 3;
            if (level >= 90) gauge.ParadoxActive = true;
        }
        if (id == Paradox) gauge.ParadoxActive = false;
        if (level >= 100 && landed)
        {
            if (id == FireIV) gauge.AstralSoulStacks = Math.Min(6, gauge.AstralSoulStacks + 1);
            if (id == Flare) gauge.AstralSoulStacks = Math.Min(6, gauge.AstralSoulStacks + 3);
        }
        if (id == FlareStar) gauge.AstralSoulStacks = 0;

        var thunderhead = id == Manafont || next != 0 && Math.Sign(before) != Math.Sign(next);
        return new(true, Math.Min(mp, maxMp), thunderhead, id == Paradox && before > 0);
    }

    private static bool ArmsParadox(int before, int after, int hearts)
        => (before == 3 && after < 0) || (before == -3 && after > 0 && hearts == 3);

    private static int NextStance(uint actionId, int stance, int rank, bool inCombat) => actionId switch
    {
        Fire => stance < 0 ? 0 : Math.Min(stance + 1, rank),
        Blizzard => stance > 0 ? 0 : Math.Max(stance - 1, -rank),
        FireII when rank < 3 => stance < 0 ? 0 : Math.Min(stance + 1, rank),
        BlizzardII when rank < 3 => stance > 0 ? 0 : Math.Max(stance - 1, -rank),
        FireII or HighFireII or FireIII or Despair or Flare or Manafont => rank,
        BlizzardII or HighBlizzardII or BlizzardIII => -rank,
        UmbralSoul => inCombat ? Math.Max(stance - 1, -rank) : -rank,
        Transpose => -Math.Sign(stance),
        _ => stance,
    };

    private static int ManaCost(uint actionId, int stance, int hearts, int mp, bool firestarter)
    {
        if (actionId == Flare) return hearts > 0 ? mp - mp / 3 : mp;
        if (actionId == Despair) return mp;
        if (actionId == Paradox) return stance > 0 ? 1600 : 0;
        if (IsIceSpell(actionId) && stance != 0) return 0;
        if (IsFireSpell(actionId) && (stance < 0 || actionId == FireIII && firestarter)) return 0;
        var cost = actionId switch
        {
            Blizzard => 400,
            Freeze => 1000,
            FireII or HighFireII => 1500,
            FireIII => 2000,
            Fire or FireIV or BlizzardII or HighBlizzardII or BlizzardIII or BlizzardIV or Scathe or Sleep => 800,
            _ => 0,
        };
        return IsFireSpell(actionId) && stance > 0 && hearts == 0 ? cost * 2 : cost;
    }

    private static bool UsesUmbralHeart(uint actionId, int stance, int hearts, int cost)
        => IsFireSpell(actionId) && stance > 0 && hearts > 0 && cost > 0;

    private static bool IsFireSpell(uint id) => id is Fire or FireII or HighFireII or FireIII or FireIV;
    private static bool IsIceSpell(uint id) => id is Blizzard or BlizzardII or HighBlizzardII or BlizzardIII or BlizzardIV or Freeze;
    private static int IceRecovery(int stance) => stance == -3 ? 10000 : stance == -2 ? 5000 : 2500;

    private static bool MeetsGaugeRequirements(BlackMageGauge gauge, uint actionId, int currentMana) => actionId switch
    {
        Flare or Despair => gauge.ElementStance > 0 && currentMana >= 800,
        Manafont or FireIV => gauge.ElementStance > 0,
        Freeze or BlizzardIV or UmbralSoul => gauge.ElementStance < 0,
        Transpose => gauge.ElementStance != 0,
        Paradox => gauge.ParadoxActive,
        FlareStar => gauge.ElementStance > 0 && gauge.AstralSoulStacks == 6,
        _ => true,
    };

    public void OnTick(float deltaSeconds)
    {
        if (Plugin.Game is not { Player: { } player } game || PlayerJob.Current != JobId.BlackMage)
        {
            if (leyLinesCenter != null) ClearLeyLines();
            return;
        }
        int stance;
        using (var edit = JobGaugeEdit.Begin())
        {
            if (edit.Gauge == null) return;
            ref var gauge = ref edit.Gauge->BlackMage;
            if (player.Dead)
            {
                if (!wasDead)
                {
                    edit.Clear();
                    Reset();
                    player.RemoveStatus(LeyLinesStatus);
                    player.RemoveStatus(CircleOfPower);
                    wasDead = true;
                }
                return;
            }
            wasDead = false;
            stance = gauge.ElementStance;
        }
        var chara = Natives.BattleCharas.LocalPlayer;
        mana ??= (int)chara.Mana;
        var simDelta = game.SimulationDelta(deltaSeconds);
        manaTick += simDelta;
        if (manaTick >= 3f)
        {
            var ticks = (int)(manaTick / 3f);
            manaTick %= 3f;
            if (stance <= 0)
            {
                var lucidRecovery = player.HasStatus(JobActions.LucidDreamingStatusId) ? 550 : 0;
                mana = Math.Min((int)chara.MaxMana, mana.Value + ticks * ((InCombat ? 200 : 600) + lucidRecovery));
            }
        }
        if (chara.Mana != mana.Value) SetMana(player, mana.Value, (int)chara.MaxMana);

        if (leyLinesCenter != null) UpdateCircleBuff(player);
        if (leyLinesCenter is { } center && leyLinesVisualDelay > 0f)
        {
            leyLinesVisualDelay -= simDelta;
            if (leyLinesVisualDelay <= 0f)
            {
                leyLinesVisual?.Dispose();
                leyLinesVisual = AreaObjects.Spawn(LeyLinesAreaObject, player.EntityId, LeyLinesRadius,
                    game.World.Coordinates.ToGlobal(center));
            }
        }
    }

    private void SetMana(SimPlayer player, int value, int maxMana)
    {
        mana = Math.Clamp(value, 0, maxMana);
        player.SetMana((uint)mana.Value);
    }

    private static void GrantProc(SimPlayer player, ushort status)
    {
        if (!player.HasStatus(status)) player.AddStatusParam(status, 0);
    }

    private static readonly (uint Action, ushort Status, float Seconds)[] ThunderSpells =
    [
        (144, 161, 24f), (7447, 162, 18f),
        (153, 163, 27f), (7420, 1210, 21f),
        (36986, 3871, 30f), (36987, 3872, 24f),
    ];

    private static bool TryApplyThunder(SimPlayer player, uint actionId, ulong targetId)
    {
        foreach (var spell in ThunderSpells)
        {
            if (spell.Action != actionId) continue;
            if (!player.HasStatus(Thunderhead)) return true;
            foreach (var enemy in ActionTargets.Hostile(actionId, targetId, player, targetCentered: true))
            {
                foreach (var old in ThunderSpells) enemy.RemoveStatus(old.Status, player.GameObjectId);
                enemy.AddStatus(spell.Status, spell.Seconds, sourceObject: player.GameObjectId);
            }
            return true;
        }
        return false;
    }

    private static void ApplyIntelligenceMedicine(SimPlayer player, uint itemId)
    {
        if (!Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(ClientLanguage.English)
            .TryGetRow(itemId % 1_000_000, out var item)) return;
        var name = item.Name.ExtractText();
        if (!name.EndsWith("Tincture of Intelligence", StringComparison.Ordinal)
            && !name.EndsWith("Gemdraught of Intelligence", StringComparison.Ordinal)) return;
        player.RemoveStatus(Medicated);
        player.AddStatusParam(Medicated, 0, 30f);
    }

    private bool ResolveMovement(SimPlayer player, uint actionId, ulong targetId)
    {
        switch (actionId)
        {
            case LeyLines:
                if (!player.HasStatus(LeyLinesStatus))
                {
                    // Ley Lines is self-centered even when the native action hook supplies a location.
                    PlaceLeyLines(player);
                    player.AddStatusParam(LeyLinesStatus, 0, LeyLinesDuration).ShownAsFrom(player);
                    UpdateCircleBuff(player);
                }
                return true;
            case Retrace:
                if (player.HasStatus(LeyLinesStatus))
                {
                    PlaceLeyLines(player);
                    UpdateCircleBuff(player);
                }
                return true;
            case BetweenTheLines:
                if (ActiveLeyLinesCenter(player) is { } center && !player.MovementLocked && DistanceSquared(player.Position, center) <= 625f)
                    player.SetPosition(center);
                return true;
            case AetherialManipulation:
                var target = Plugin.Game?.World.Party.ActiveMembers()
                    .FirstOrDefault(m => m != player && !m.Dead && (ulong)m.GameObjectId == targetId);
                if (target != null && !player.MovementLocked && DistanceSquared(player.Position, target.Position) <= 625f)
                    player.SetPosition(target.Position);
                return true;
            default:
                return false;
        }
    }

    private void UpdateCircleBuff(SimPlayer player)
    {
        var leyLines = player.FindStatus(LeyLinesStatus);
        if (leyLines == null) ClearLeyLines();
        if (leyLines is not { RemainingTime: > 0 } || leyLinesCenter is not { } center
            || DistanceSquared(player.Position, center) > LeyLinesRadius * LeyLinesRadius)
        {
            player.RemoveStatus(CircleOfPower);
            return;
        }

        var duration = MathF.Min(leyLines.RemainingTime, CircleOfPowerDuration);
        var buff = player.FindStatus(CircleOfPower);
        if (buff == null)
            player.AddStatusParam(CircleOfPower, 0, duration).ShownAsFrom(player);
        else if (buff.RemainingTime <= CircleOfPowerRefreshThreshold && duration - buff.RemainingTime >= 1f)
        {
            buff.Reapply(duration, 0);
            player.StatusListChanged();
        }
    }

    private static float DistanceSquared(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }
}
