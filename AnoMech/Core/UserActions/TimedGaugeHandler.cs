using System.Collections.Generic;
using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// A gauge with time-based behavior (on top of action-driven writes), ticked each frame
// while the player is on `Job`:
//  - regen (decay=false): +Amount every Seconds, capped at the gauge Max (WHM Lily, SGE Addersgall).
//  - decay (decay=true):  reset to 0 Seconds after the last action write (GNB combo timer).
// Both are server-driven in the real game (the ActorGauge packet the sim firewalls), so we
// run them here and send the ActorGauge ourselves. `writeTimer` (optional) is the gauge's own countdown field
// (WhiteMage.LilyTimer, Sage.AddersgallTimer) — the visible fill ring; kept in sync as ms.
internal unsafe delegate bool GaugeCondition(JobGaugeManager* jgm);

internal sealed unsafe class TimedGauge(JobId job, ResourceGauge gauge, float seconds, bool decay, int amount = 1,
    GaugeWriter? writeTimer = null, GaugeCondition? isActive = null, bool continueAtCap = false,
    bool countdown = false, bool useScenarioTime = false)
{
    public JobId Job => job;
    public ResourceGauge Gauge => gauge;
    public float Seconds => seconds;
    public bool Decay => decay;
    public int Amount => amount;
    public GaugeWriter? WriteTimer => writeTimer;
    public GaugeCondition? IsActive => isActive;
    public bool ContinueAtCap => continueAtCap;
    public bool Countdown => countdown;
    public bool UseScenarioTime => useScenarioTime;
}

internal sealed unsafe class TimedGaugeHandler : IUserActionHandler
{
    private sealed class Tracker { public float Timer; public int LastValue = -1; }
    private readonly Dictionary<TimedGauge, Tracker> trackers = new();

    public void OnScenarioStart() => trackers.Clear();
    public void OnSessionEnd() => trackers.Clear();

    internal void Seed(TimedGauge gauge, JobGaugeManager* jgm, float elapsed)
    {
        var tracker = new Tracker();
        TickRegen(gauge, ref tracker.Timer, jgm, elapsed);
        trackers[gauge] = tracker;
    }

    internal void SeedDecay(TimedGauge gauge, JobGaugeManager* jgm, float remaining)
        => trackers[gauge] = new Tracker { LastValue = gauge.Gauge.Read(jgm), Timer = remaining };

    public void OnTick(float deltaSeconds)
    {
        var job = PlayerJob.Current;
        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        foreach (var tg in JobActions.TimedGauges)
        {
            if (tg.Job != job) continue;
            if (!trackers.TryGetValue(tg, out var tr)) trackers[tg] = tr = new Tracker();
            var dt = tg.UseScenarioTime && Plugin.Game is { } game ? game.SimulationDelta(deltaSeconds) : deltaSeconds;
            if (tg.Decay) TickDecay(tg, tr, jgm, dt);
            else TickRegen(tg, ref tr.Timer, jgm, dt);
        }
    }

    internal static void TickRegen(TimedGauge tg, ref float timer, JobGaugeManager* jgm, float dt)
    {
        if (tg.IsActive?.Invoke(jgm) == false || (!tg.ContinueAtCap && tg.Gauge.Read(jgm) >= tg.Gauge.Max))
        {
            timer = 0f;
            tg.WriteTimer?.Invoke(jgm, 0);
            return;
        }
        timer += dt;
        while (timer >= tg.Seconds)
        {
            timer -= tg.Seconds;
            tg.Gauge.Add(jgm, tg.Amount);
            if (tg.ContinueAtCap || tg.Gauge.Read(jgm) < tg.Gauge.Max) continue;
            timer = 0f;
            tg.WriteTimer?.Invoke(jgm, 0);
            return;
        }
        var displayed = tg.Countdown ? tg.Seconds - timer : timer;
        tg.WriteTimer?.Invoke(jgm, (int)(displayed * 1000f));
    }

    private static void TickDecay(TimedGauge tg, Tracker tr, JobGaugeManager* jgm, float dt)
    {
        var value = tg.Gauge.Read(jgm);
        if (value != tr.LastValue)          // an action wrote the gauge → refresh the window
        {
            if (value > 0) tr.Timer = tg.Seconds;
            tr.LastValue = value;
        }
        if (value <= 0) return;
        tr.Timer -= dt;
        tg.WriteTimer?.Invoke(jgm, (int)(tr.Timer * 1000f));
        if (tr.Timer <= 0f) { tg.Gauge.Write(jgm, 0); tr.LastValue = 0; }
    }
}
