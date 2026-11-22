namespace AnoMech.Scenarios.Umad.P5Maddening;

public enum MaddeningTankAssignment
{
    Flare,
    Holy,
}

public sealed class UmadP5MaddeningStateOverrides
{
    public MaddeningTankAssignment MainTankAssignment { get; set; } = MaddeningTankAssignment.Flare;
}
