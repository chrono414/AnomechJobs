using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using AnoMech.Scenarios.Umad.P5Maddening;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullStateOverrides
{
    public UmadP5MaddeningStateOverrides Orchestra { get; } = new();
    public UmadP5FloodStateOverrides Flood { get; } = new();
    public UmadP5CelestriadStateOverrides Celestriad { get; }
    public UmadP5ExaflaresStateOverrides Exaflares { get; }

    internal UmadP5FullStateOverrides(UmadP5CelestriadStateOverrides celestriad, UmadP5ExaflaresStateOverrides exaflares)
    {
        Celestriad = celestriad;
        Exaflares = exaflares;
    }
}
