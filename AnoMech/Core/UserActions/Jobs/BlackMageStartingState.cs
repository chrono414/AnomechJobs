using System;

namespace AnoMech.Core.UserActions.Jobs;

public enum BlackMageStartingPhase
{
    None = 0,
    AstralFireI = 1,
    AstralFireII = 2,
    AstralFireIII = 3,
    UmbralIceI = -1,
    UmbralIceII = -2,
    UmbralIceIII = -3,
}

public readonly record struct StartingCharges(int Available, int SecondsToNext);

public sealed record BlackMageStartingState
{
    public int Mp { get; init; } = 10000;
    public BlackMageStartingPhase Phase { get; init; }
    public bool Paradox { get; init; }
    public bool Firestarter { get; init; }
    public bool Thundercloud { get; init; }
    public int UmbralHearts { get; init; }
    public int AstralSoul { get; init; }
    public int EnochianProgress { get; init; }
    public int Polyglot { get; init; }
    public int Amplifier { get; init; }
    public StartingCharges LeyLines { get; init; } = new(2, 0);
    public int Addle { get; init; }
    public int Manaward { get; init; }
    public StartingCharges Triplecast { get; init; } = new(2, 0);
    public int Swiftcast { get; init; }
}

internal sealed record BlackMageStartingRules(
    int PhaseRanks, int Hearts, int Soul, int Polyglot, int EnochianSeconds, bool HasParadox,
    int Amplifier, int LeyLinesCharges, int LeyLinesRecharge, int Addle, int Manaward,
    int TriplecastCharges, int TriplecastRecharge, int Swiftcast)
{
    private static readonly BlackMageStartingRules Level100 = new(3, 3, 6, 3, 30, true, 120, 2, 120, 90, 120, 2, 60, 40);

    internal static BlackMageStartingRules? ForLevel(int level) => level == 100 ? Level100 : null;

    internal BlackMageStartingState Normalize(BlackMageStartingState state, int maxMp)
    {
        var phase = (BlackMageStartingPhase)Math.Clamp((int)state.Phase, -PhaseRanks, PhaseRanks);
        return state with
        {
            Mp = Math.Clamp(state.Mp, 0, maxMp),
            Phase = phase,
            Paradox = HasParadox && phase != BlackMageStartingPhase.None && state.Paradox,
            UmbralHearts = Math.Clamp(state.UmbralHearts, 0, Hearts),
            AstralSoul = (int)phase > 0 ? Math.Clamp(state.AstralSoul, 0, Soul) : 0,
            EnochianProgress = phase != BlackMageStartingPhase.None ? Math.Clamp(state.EnochianProgress, 0, EnochianSeconds) : 0,
            Polyglot = Math.Clamp(state.Polyglot, 0, Polyglot),
            Amplifier = Math.Clamp(state.Amplifier, 0, Amplifier),
            LeyLines = NormalizeCharges(state.LeyLines, LeyLinesCharges, LeyLinesRecharge),
            Addle = Math.Clamp(state.Addle, 0, Addle),
            Manaward = Math.Clamp(state.Manaward, 0, Manaward),
            Triplecast = NormalizeCharges(state.Triplecast, TriplecastCharges, TriplecastRecharge),
            Swiftcast = Math.Clamp(state.Swiftcast, 0, Swiftcast),
        };
    }

    private static StartingCharges NormalizeCharges(StartingCharges state, int max, int recharge)
    {
        var available = Math.Clamp(state.Available, 0, max);
        return available == max ? new(max, 0) : new(available, Math.Clamp(state.SecondsToNext, 1, recharge));
    }
}
