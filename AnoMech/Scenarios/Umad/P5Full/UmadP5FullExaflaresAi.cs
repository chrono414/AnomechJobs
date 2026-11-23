using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

using AnoMech.Scenarios.Umad.P5Exaflares;

namespace AnoMech.Scenarios.Umad.P5Full;
internal sealed class UmadP5FullExaflaresAi
{
    private const float LocusBase = -35f, LocusStep = 10f;
    private const float FireRadius = 6f;
    private static readonly float FireClearU = FireRadius * MathF.Sqrt(2f);
    private static readonly float StripInset = FireClearU + 1.5f;
    private const float OuterReach = 19f;
    private const float DistFill = 0.8f;
    private const float DodgeSpeed = 8.3f;
    private const float MeleeComfort = 2.5f;
    private const float SpreadComfort = 6f;
    private const float TargetJitter = 0.6f;
    private const int   DeClumpIters = 6;
    private const float DeClumpRate = 0.5f;
    private const float FanOutAt = 0.1f;
    private const float FanSpeed = 6.5f;
    private const float FanInnerMargin = 0.75f;
    private const float FanAngleJitter = 0.5f;
    private const float SpreadKill = 5f;
    private const float Comfort = 9.75f;
    private const float OuterRing = 13f;
    private const float InnerFallback = 6.5f;
    private const float NoGoRadius = 3.5f;
    private const float ArenaMax = 19f;
    private const float RelaxSpeed = 6.5f;
    private const float DeadZone = 0.25f;
    private const float WRepel = 1.0f, WArena = 1.5f;
    private const float WRadialInner = 0.5f, WRadialOuter = 0.30f;
    private const float FirstHitDelay = 4.582f;
    private const float BloomDelay = 14f / 30f;
    private const float ArriveLead = 0.4f;
    private static readonly float[] WaveStart = { 3.0f, 5.5f, 8.0f, 10.5f, 13.0f, 15.5f };

    private UmadP5ExaflaresState state = null!;
    private SimWorld world = null!;
    private readonly bool[] relaxMoving = new bool[8];
    private bool spreadPhase;
    private float anchor;
    private float movementHold;
    private float innerRing = InnerFallback;

    public void Schedule(UmadP5ExaflaresState stateParam, SimWorld worldParam, float start, float holdUntil)
    {
        state = stateParam;
        world = worldParam;
        anchor = start;
        movementHold = holdUntil - start;

        ScheduleInitialFanOut();
        for (int n = 0; n < 6; n++) ScheduleWaveDodge(n);
        world.Events.Add(anchor + 22.75f, () => { spreadPhase = true; innerRing = ResolveInnerRing(); Array.Clear(relaxMoving); });
        world.Events.Add(anchor + 25.09f, () => spreadPhase = false);
        state.SpreadTick = RelaxStep;
    }
    private void ScheduleWaveDodge(int n)
    {
        float startT = WaveStart[n];
        float firstSnapshot = startT + FirstHitDelay;
        float arriveBy = firstSnapshot - ArriveLead;
        float safeMove = n == 0 ? 2.0f : startT + 1.7f + BloomDelay;
        safeMove = MathF.Max(safeMove, movementHold);
        float budget = arriveBy - safeMove;

        world.Events.Add(anchor + safeMove, () => PlaceWave(n, budget));
    }
    private void PlaceWave(int n, float budget)
    {
        bool leftWave = (n % 2) == 0;
        var lanes = UsableBands(n);
        if (lanes.Count == 0) return;
        var picks = new List<Pick>(8);
        for (int slot = 0; slot < 8; slot++)
        {
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;

            bool inner = IsInnerRole((PartyRole)slot);
            var (crit, perp) = ToCritPerp(bot.Position, leftWave);
            float want = (inner ? 0f : crit) + Jitter();
            picks.Add(new Pick(bot, PlaceInLanes(lanes, want), perp, inner));
        }

        DeClump(picks, lanes);

        foreach (var pk in picks)
        {
            var dest = FromCritPerp(pk.Crit, pk.Perp, leftWave);
            float delay = budget - FlatDist(pk.Bot.Position, dest) / DodgeSpeed;
            if (delay > 0f) world.Events.Add(delay, () => pk.Bot.MoveTo(dest, DodgeSpeed));
            else pk.Bot.MoveTo(dest, DodgeSpeed);
        }
    }
    private void ScheduleInitialFanOut()
    {
        world.Events.Add(anchor + MathF.Max(FanOutAt, movementHold), () =>
        {
            float outer = ResolveInnerRing();
            float inner = MathF.Min(NoGoRadius + FanInnerMargin, outer - 0.5f);
            for (int slot = 0; slot < 8; slot++)
            {
                var bot = world.Party.Get(slot);
                if (bot is null || !bot.IsAlive()) continue;

                var cur = bot.Position;
                float baseAngle = MathF.Atan2(cur.X, cur.Z);
                float angle = baseAngle + ((float)world.Rng.NextDouble() - 0.5f) * FanAngleJitter;
                float radius = inner + (float)world.Rng.NextDouble() * (outer - inner);
                var target = new Vector3(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
                bot.MoveTo(target, FanSpeed);
            }
        });
    }
    private List<(float lo, float hi)> UsableBands(int n)
    {
        var bands = new List<(float lo, float hi)> { Fill(Band(n, 0)) };
        if (OuterAvailable(n)) bands.Add(Fill(Band(n, 1)));
        return bands;
    }
    private static float PlaceInLanes(List<(float lo, float hi)> lanes, float want)
    {
        float best = want; float bestD = float.MaxValue;
        foreach (var (lo, hi) in lanes)
        {
            float c = Math.Clamp(want, lo, hi);
            float d = MathF.Abs(c - want);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }
    private void DeClump(List<Pick> picks, List<(float lo, float hi)> lanes)
    {
        for (int iter = 0; iter < DeClumpIters; iter++)
            for (int i = 0; i < picks.Count; i++)
            {
                var pi = picks[i];
                float push = 0f;
                for (int j = 0; j < picks.Count; j++)
                {
                    if (j == i) continue;
                    var pj = picks[j];
                    float comfort = pi.Inner && pj.Inner ? MeleeComfort : SpreadComfort;
                    float dc = pi.Crit - pj.Crit, dp = pi.Perp - pj.Perp;
                    float dist = MathF.Sqrt(dc * dc + dp * dp);
                    if (dist >= comfort) continue;
                    float dir = dist > 1e-3f ? dc / dist : MathF.Sign(i - j);
                    push += dir * (comfort - dist);
                }
                if (MathF.Abs(push) < 1e-3f) continue;
                pi.Crit = PlaceInLanes(lanes, pi.Crit + push * DeClumpRate);
            }
    }

    private float Jitter() => ((float)world.Rng.NextDouble() - 0.5f) * TargetJitter;
    private static (float crit, float perp) ToCritPerp(Vector3 pos, bool leftWave)
    {
        float a = pos.X - pos.Z, b = pos.X + pos.Z;
        return leftWave ? (a, b) : (b, a);
    }

    private static Vector3 FromCritPerp(float crit, float perp, bool leftWave)
    {
        float a = leftWave ? crit : perp, b = leftWave ? perp : crit;
        return new Vector3((a + b) * 0.5f, 0f, (b - a) * 0.5f);
    }
    private sealed class Pick
    {
        public readonly SimCharacter Bot;
        public float Crit;
        public readonly float Perp;
        public readonly bool Inner;
        public Pick(SimCharacter bot, float crit, float perp, bool inner) { Bot = bot; Crit = crit; Perp = perp; Inner = inner; }
    }

    private static float FlatDist(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
    private void RelaxStep(float dt)
    {
        if (!spreadPhase) return;

        var agents = new List<(SimCharacter c, Vector2 p)>(8);
        for (int i = 0; i < 8; i++)
            if (world.Party.Get(i) is { } m && m.IsAlive())
                agents.Add((m, new Vector2(m.Position.X, m.Position.Z)));

        for (int slot = 0; slot < 8; slot++)
        {
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;
            var p = new Vector2(bot.Position.X, bot.Position.Z);

            var desired = Vector2.Zero;
            foreach (var (c, q) in agents)
            {
                if (ReferenceEquals(c, bot)) continue;
                var d = p - q;
                var dist = d.Length();
                if (dist >= Comfort) continue;
                var dir = dist > 1e-3f ? d / dist : Spoke(slot);
                desired += dir * (Comfort - dist) * WRepel;
            }
            bool inner = IsInnerRole((PartyRole)slot);
            float r = p.Length();
            var rdir = r > 1e-3f ? p / r : Spoke(slot);
            if (inner)
            {
                if (r > innerRing) desired += rdir * (innerRing - r) * WRadialInner;
                else if (r < NoGoRadius) desired += rdir * (NoGoRadius - r) * WRadialInner;
            }
            else
            {
                desired += rdir * (OuterRing - r) * WRadialOuter;
            }
            if (r > ArenaMax) desired += rdir * (ArenaMax - r) * WArena;

            if (desired.Length() > DeadZone)
            {
                var step = desired.Length() > 5f ? desired * (5f / desired.Length()) : desired;
                var t = p + step;
                if (t.Length() > ArenaMax) t *= ArenaMax / t.Length();
                bot.MoveTo(new Vector3(t.X, 0f, t.Y), RelaxSpeed);
                relaxMoving[slot] = true;
            }
            else if (relaxMoving[slot])
            {
                bot.MoveTo(new Vector3(p.X, 0f, p.Y), RelaxSpeed);
                relaxMoving[slot] = false;
            }
        }
    }
    private float ResolveInnerRing()
    {
        var boss = world.Children.OfType<SimEnemy>()
                        .FirstOrDefault(e => e.IsAlive() && e.BNpcBaseId == BNpcBaseId.KefkaP5);
        float hitbox = boss?.HitboxRadius ?? 3.5f;
        return MathF.Max(hitbox + 3f, 6f);
    }

    private static bool IsInnerRole(PartyRole r) =>
        r.IsTank() || r == PartyRole.MeleeDpsA || r == PartyRole.MeleeDpsB;
    private bool OuterAvailable(int n)
    {
        var (lo, hi) = PairLoHi(n);
        return MathF.Abs((lo + hi) * 0.5f) > 1f;
    }
    private (float lo, float hi) PairLoHi(int n)
    {
        var order = (n % 2) == 0 ? state.LeftOrder : state.RightOrder;
        int pairBase = (n / 2) * 2;
        float l1 = Locus(order[pairBase]), l2 = Locus(order[pairBase + 1]);
        return (MathF.Min(l1, l2), MathF.Max(l1, l2));
    }
    private (float lo, float hi) Band(int n, int idx)
    {
        var (lo, hi) = PairLoHi(n);
        if (idx == 0) return (lo + StripInset, hi - StripInset);
        return (lo + hi) * 0.5f < 0f
            ? (hi + StripInset, OuterReach)
            : (-OuterReach, lo - StripInset);
    }

    private static float Locus(int lineIdx) => LocusBase + LocusStep * lineIdx;
    private static (float lo, float hi) Fill((float lo, float hi) s)
    {
        float mid = (s.lo + s.hi) * 0.5f, half = (s.hi - s.lo) * 0.5f * DistFill;
        return (mid - half, mid + half);
    }
    private static Vector2 Spoke(int slot) => new(MathF.Cos(slot * 0.785f), MathF.Sin(slot * 0.785f));
}
