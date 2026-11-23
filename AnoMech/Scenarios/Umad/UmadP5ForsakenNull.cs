using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad;

public sealed class UmadP5ForsakenNull : IScenario
{
    public string Name => "Forsaken Null";
    public IPhase Phase => UmadZone.P5;
    public IReadOnlyList<IScenarioAi> AiStrats => [];

    private Action? checkBlackHoleContact;

    public void Tick(float delta, float elapsed) => checkBlackHoleContact?.Invoke();

    public void Run(SimWorld world, int? selectedAi)
    {
        SimEnemy? kefka = null;
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.KefkaP5,
            NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            LayoutId: UmadConstants.BNpcLayoutId.KefkaP5,
            Placement: new Placement(Vector3.Zero, MathF.PI))));
        world.Events.Add(1.08f, () => kefka?.Cast(Constants.ActionId.UnknownC652, animationLock: 1.1f));
        checkBlackHoleContact = Schedule(world, () => kefka, 0f);
    }

    private readonly record struct Wave(float Marker, float Cast, float GroundRelease, float Bonds);

    private static readonly Wave[] Waves =
    [
        new(13.20f, 13.28f, 18.28f, 18.32f),
        new(21.39f, 21.48f, 26.48f, 26.47f),
        new(29.55f, 29.63f, 34.63f, 34.62f),
        new(37.69f, 37.78f, 42.78f, 42.78f),
    ];

    internal static Action Schedule(SimWorld world, Func<SimEnemy?> kefka, float start)
    {
        var party = world.Party;
        var state = new UmadP5ForsakenState(world.Rng.Fork("p5-forsaken"));
        var activeBlackHoles = new List<UmadP5ForsakenState.BlackHole>();
        var contactRecorded = false;

        world.Events.Add(start + 3.13f, () => DirectorUpdate(0x28));
        world.Events.Add(start + 3.21f, () =>
        {
            if (kefka() is not { } boss) return;
            boss.Cast(Constants.ActionId.Forsaken);
            UmadP5ForsakenAi.MoveToCenter(party);
        });
        world.Events.Add(start + 21.30f, () => DirectorUpdate(0x29));
        world.Events.Add(start + 29.46f, () => DirectorUpdate(0x2A));
        world.Events.Add(start + 37.61f, () => DirectorUpdate(0x2B));

        for (var index = 0; index < Waves.Length; index++)
            ScheduleWave(index, Waves[index]);

        world.Events.Add(start + 47.809f, () => kefka()?.Cast(Constants.ActionId.ForsakenNull));
        world.Events.Add(start + 52.13f, () =>
        {
            activeBlackHoles.Clear();
            DirectorUpdate(0x2C);
        });

        return CheckBlackHoleContact;

        void DirectorUpdate(uint step)
        {
            if (kefka() is { } boss)
                world.Map.DirectorUpdate(0x80000027U, step, 0x2U, 0x1BDBU, boss.EntityId);
        }

        void ScheduleWave(int index, Wave wave)
        {
            Vector3? bait = null;
            world.Events.Add(start + wave.Marker, () =>
            {
                party.Player?.AttachLockonVfx(Constants.LockonId.X_A1);
                if (index == 0)
                    kefka()?.AddStatus(Constants.StatusId.DirectionalDisregard);
                else
                    kefka()?.Cast(Constants.ActionId.Forsaken_BB36);
            });
            world.Events.Add(start + wave.Cast - 0.08f, () =>
            {
                bait = party.Player?.Position;
                if (index == 2)
                    state.SelectFinalBlackHoles(bait ?? Vector3.Zero);
            });
            world.Events.Add(start + wave.Cast, () =>
            {
                if (kefka() is not { } boss) return;
                if (bait is { } position)
                    boss.CastFromHelper(UmadActions.ForsakenBait, at: new Placement(position, 0f));
                foreach (var hole in state.Waves[index])
                    boss.CastFromHelper(UmadActions.ForsakenBlackHole, target: hole.Position, at: new Placement(hole.Position, 0f));
            });
            world.Events.Add(start + wave.GroundRelease, () =>
            {
                foreach (var hole in state.Waves[index])
                {
                    activeBlackHoles.Add(hole);
                    world.Map.AddEffect(packetFlags: 0x00200010U, index: hole.MapIndex);
                }
            });
            world.Events.Add(start + wave.Bonds, () =>
            {
                if (party.Player is { } target)
                    kefka()?.CastFromHelper(Constants.ActionId.ForsakenBonds, target);
            });
        }

        void CheckBlackHoleContact()
        {
            if (contactRecorded || party.Player is not { } player || !player.IsAlive() || party.IsBotDriven(player)) return;
            foreach (var hole in activeBlackHoles)
            {
                if (Vector3.DistanceSquared(player.Position, hole.Position) >
                    UmadP5ForsakenState.BlackHoleRadius * UmadP5ForsakenState.BlackHoleRadius) continue;
                contactRecorded = true;
                player.Die(Constants.ActionId.ForsakenGround, "Touched a persistent black hole");
                break;
            }
        }
    }

    public static class Constants
    {
        public static class BNpcBaseId
        {
            public const uint Kefka = 19511;
        }

        public static class BNpcNameId
        {
            public const uint Kefka = 7131;
        }

        public static class ActionId
        {
            public const uint BlizzardIII = 0xBB44U;
            public const uint CatastrophicChoice = 0xC24EU;
            public const uint CatastrophicChoice_C24F = 0xC24FU;
            public const uint Celestriad = 0xBB42U;
            public const uint ChaoticFlare = 0xBB53U;
            public const uint ChaoticFlood = 0xBB4FU;
            public const uint ChaoticHoly = 0xBB56U;
            public const uint FellForces = 0xC654U;
            public const uint FellForces_C653 = 0xC653U;
            public const uint FellForces_C655 = 0xC655U;
            public const uint FireIII = 0xBB43U;
            public const uint Flare = 0xBB52U;
            public const uint FlareDiffusion = 0xBB55U;
            public const uint Flood = 0xC13FU;
            public const uint Flood_C269 = 0xC269U;
            public const uint Forsaken = 0xBB35U;
            public const uint ForsakenBonds = 0xBB39U;
            public const uint ForsakenGround = 0xBB37U;
            public const uint ForsakenNull = 0xBB3AU;
            public const uint Forsaken_BB36 = 0xBB36U;
            public const uint Forsaken_BB38 = 0xBB38U;
            public const uint Holy = 0xBB54U;
            public const uint MaddeningOrchestra = 0xBB50U;
            public const uint MaddeningOrchestra_BB51 = 0xBB51U;
            public const uint StrayApocalypse = 0xBB3BU;
            public const uint StrayApocalypse_BB3C = 0xBB3CU;
            public const uint StrayApocalypse_BB3D = 0xBB3DU;
            public const uint StrayEntropy = 0xBB3EU;
            public const uint StrayEntropy_BB3F = 0xBB3FU;
            public const uint ThunderIII = 0xBB45U;
            public const uint Tornado = 0xBB4BU;
            public const uint UltimaRepeater = 0xBB40U;
            public const uint UltimaRepeater_BB41 = 0xBB41U;
            public const uint UnknownC183 = 0xC183U;
            public const uint UnknownC61a = 0xC61AU;
            public const uint UnknownC652 = 0xC652U;
        }

        public static class StatusId
        {
            public const ushort DamageDown = (ushort)0xB5F;
            public const ushort DirectionalDisregard = (ushort)0xEE0;
            public const ushort FireResistanceDownII = (ushort)0xB56;
            public const ushort Forsaken = (ushort)0x1418;
            public const ushort IceResistanceDownII = (ushort)0xB57;
            public const ushort LightningResistanceDownII = (ushort)0xBB6;
            public const ushort MagicVulnerabilityUp = (ushort)0xB7D;
            public const ushort Transcendent = (ushort)0x1A2;
        }

        public static class LockonId
        {
            public const uint X_A1 = 161;
        }
    }
}
