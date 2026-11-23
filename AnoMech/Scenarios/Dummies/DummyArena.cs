using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dummies;

// Distinct zone identity prevents starting a raid scenario in a practice stay with different sync.
internal sealed class DummyArena(IPhase source) : IZone
{
    public const byte Level = 100;
    private const float KefkaHitboxRadius = 6f;

    public string Name => source.Zone.Name;
    public uint TerritoryId => source.Zone.TerritoryId;
    public Vector3 Origin => source.Zone.Origin;
    public byte? PracticeLevel => Level;
    public IReadOnlyList<WaymarkLayout> WaymarkPresets => source.Zone.WaymarkPresets;
    public DutyInitData DutyInitData { get; } = source.Zone.DutyInitData with { HelperPool = default };

    public void Run(SimWorld world) => source.Zone.Run(world);

    public static (string Label, IPhase Phase, EnemySpawnConfig Target) Create(
        string label, IPhase source, uint baseId, uint nameId, uint layoutId, byte? modeFlags = null)
        => (label,
            new Phase(new DummyArena(source), "", source.Weather, source.Bgm, source.Run, source.ArenaMapEffectOverrides),
            new EnemySpawnConfig(BNpcBaseId: baseId, NameId: nameId, Level: Level, Targetable: true,
                Placement: new Placement(Vector3.Zero, MathF.PI), InitialModeAttributeFlags: modeFlags,
                Engaged: false, LayoutId: layoutId, HitboxRadiusOverride: KefkaHitboxRadius));
}
