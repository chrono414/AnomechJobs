using System.Collections.Generic;
using System.Linq;
using static AnoMech.Core.UserActions.StartingResource;

namespace AnoMech.Core.UserActions;

internal sealed partial record CombatStartingRules
{
    private static readonly StartingCooldownRule[] HealerRole =
    [
        new(7561, "Swiftcast", 18, 60, Trait: 94, TraitRecharge: 40),
        new(7562, "Lucid Dreaming", 14, 60), new(7559, "Surecast", 44, 120), new(7571, "Rescue", 48, 120),
    ];

    private static readonly StartingCooldownRule[] TankRole =
    [
        new(7531, "Rampart", 8, 90), new(7535, "Reprisal", 22, 60), new(7548, "Arm's Length", 32, 120),
        new(7533, "Provoke", 15, 30), new(7537, "Shirk", 48, 120), new(7538, "Interject", 18, 30),
    ];

    private static readonly StartingCooldownRule[] MeleeRole =
    [
        new(7549, "Feint", 22, 90), new(7546, "True North", 50, 45, 2), new(7548, "Arm's Length", 32, 120),
    ];

    private static readonly IReadOnlyDictionary<JobId, CombatStartingRules> Jobs = new Dictionary<JobId, CombatStartingRules>
    {
        [JobId.WhiteMage] = new(
            [new(Mp, "MP", 1, 10000), new(Lilies, "Lilies", 52, 3, 3), new(BloodLily, "Blood Lily", 74, 3, 3),
             new(LilyProgress, "Lily progress", 52, 19)],
            [new(136, "Presence of Mind", 30, 120), new(3571, "Assize", 56, 40), new(37008, "Aetherial Shift", 40, 60),
             new(3569, "Asylum", 52, 90), new(140, "Benediction", 50, 180), new(7430, "Thin Air", 58, 60, 2),
             new(7432, "Divine Benison", 66, 30, Trait: 88, TraitCharges: 2), new(3570, "Tetragrammaton", 60, 60, Trait: 98, TraitCharges: 2),
             new(7433, "Plenary Indulgence", 70, 60), new(16536, "Temperance", 80, 120), new(25861, "Aquaveil", 86, 60),
             new(25862, "Liturgy of the Bell", 90, 180), ..HealerRole],
            [new(157, "Presence of Mind", 30, 15), new(3879, "Sacred Sight", 92, 30, 3),
             new(155, "Freecure", 32, 15), new(1217, "Thin Air", 58, 12), new(3881, "Divine Grace", 100, 30)]),

        [JobId.Sage] = new(
            [new(Mp, "MP", 1, 10000), new(Addersgall, "Addersgall", 45, 3, 3), new(Addersting, "Addersting", 66, 3, 3),
             new(AddersgallProgress, "Addersgall progress", 45, 19)],
            [new(24285, "Kardia", 4, 5), new(24288, "Physis", 20, 60), new(24289, "Phlegma", 26, 40, 2),
             new(24294, "Soteria", 35, 90, Trait: 94, TraitRecharge: 60), new(24295, "Icarus", 40, 45),
             new(24296, "Druochole", 45, 1), new(24298, "Kerachole", 50, 30), new(24299, "Ixochole", 52, 30),
             new(24300, "Zoe", 56, 120, Trait: 88, TraitRecharge: 90), new(24301, "Pepsis", 58, 30),
             new(24303, "Taurochole", 62, 45), new(24305, "Haima", 70, 120), new(24309, "Rhizomata", 74, 90),
             new(24310, "Holos", 76, 120), new(24311, "Panhaima", 80, 120), new(24317, "Krasis", 86, 60),
             new(24318, "Pneuma", 90, 120), new(37033, "Psyche", 92, 60), new(37035, "Philosophia", 100, 180), ..HealerRole],
            [new(2606, "Eukrasia armed", 30, 0), new(2610, "Soteria", 35, 15, 4)]),

        [JobId.Warrior] = new(
            [new(Beast, "Beast Gauge", 35, 100)],
            [new(38, "Berserk / Inner Release", 6, 60), new(52, "Infuriate", 50, 60, 2),
             new(7386, "Onslaught", 62, 30, 2, Trait: 88, TraitCharges: 3), new(7387, "Upheaval / Orogeny", 64, 30),
             new(40, "Thrill of Battle", 30, 90), new(44, "Vengeance / Damnation", 38, 120), new(43, "Holmgang", 42, 240),
             new(3551, "Raw Intuition / Bloodwhetting / Nascent Flash", 56, 25), new(3552, "Equilibrium", 58, 60),
             new(7388, "Shake It Off", 68, 90), ..TankRole],
            [new(91, "Defiance", 10, 0), new(2677, "Surging Tempest", 40, 60), new(1897, "Nascent Chaos", 72, 30),
             new(1177, "Inner Release", 70, 15, 3), new(2663, "Inner Strength", 70, 15),
             new(3833, "Burgeoning Fury", 96, 30, 2), new(3901, "Wrathful", 96, 30),
             new(2624, "Primal Rend Ready", 90, 30), new(3834, "Primal Ruination Ready", 100, 20)])
        { Combos = [new(31, "After Heavy Swing", 4), new(37, "After Maim", 26), new(41, "After Overpower", 40)] },

        [JobId.Paladin] = new(
            [new(Mp, "MP", 1, 10000), new(Oath, "Oath Gauge", 35, 100, 100),
             new(ConfiteorStep, "Confiteor chain step", 90, 3), new(ConfiteorTime, "Confiteor chain time", 90, 30)],
            [new(20, "Fight or Flight", 2, 60), new(7383, "Requiescat / Imperator", 68, 60),
             new(16461, "Intervene", 66, 30, 2), new(29, "Spirits Within / Expiacion", 30, 30), new(23, "Circle of Scorn", 50, 30),
             new(3542, "Sheltron / Holy Sheltron", 35, 5), new(27, "Cover", 45, 120), new(7382, "Intervention", 62, 10),
             new(17, "Sentinel / Guardian", 38, 120), new(22, "Bulwark", 52, 90), new(30, "Hallowed Ground", 50, 420),
             new(3540, "Divine Veil", 56, 90), new(7385, "Passage of Arms", 70, 120), ..TankRole],
            [new(79, "Iron Will", 10, 0), new(2673, "Divine Might", 64, 30), new(76, "Fight or Flight", 2, 20),
             new(3847, "Goring Blade Ready", 54, 30), new(1902, "Atonement Ready", 76, 30),
             new(3827, "Supplication Ready", 76, 30), new(3828, "Sepulchre Ready", 76, 30),
             new(1368, "Requiescat", 68, 30, 4), new(3019, "Confiteor Ready", 80, 30), new(3831, "Blade of Honor Ready", 100, 30)])
        { Combos = [new(9, "After Fast Blade", 4), new(15, "After Riot Blade", 26), new(7381, "After Total Eclipse", 40)] },

        [JobId.Reaper] = new(
            [new(Soul, "Soul Gauge", 50, 100), new(Shroud, "Shroud Gauge", 80, 100),
             new(LemureShroud, "Lemure Shroud", 80, 5), new(VoidShroud, "Void Shroud", 86, 4)],
            [new(24380, "Soul Slice / Soul Scythe", 60, 30, Trait: 78, TraitCharges: 2), new(24393, "Gluttony", 76, 60),
             new(24405, "Arcane Circle", 72, 120), new(24401, "Hell's Ingress / Egress", 20, 20),
             new(24404, "Arcane Crest", 40, 30), new(24394, "Enshroud", 80, 5), ..MeleeRole],
            [new(2594, "Soulsow armed", 82, 0), new(2587, "Soul Reaver", 70, 30, 2), new(3858, "Executioner", 96, 30, 2),
             new(2588, "Enhanced Gibbet", 70, 60), new(2589, "Enhanced Gallows", 70, 60), new(2592, "Immortal Sacrifice", 88, 30, 8),
             new(2601, "Bloodsown Circle", 88, 6), new(3905, "Ideal Host", 88, 30), new(3859, "Perfectio Occulta", 100, 30),
             new(3860, "Perfectio Parata", 100, 30), new(2845, "Enhanced Harpe", 20, 10), new(2593, "Enshrouded", 80, 30),
             new(2590, "Enhanced Void Reaping", 80, 30), new(2591, "Enhanced Cross Reaping", 80, 30), new(3857, "Oblatio", 92, 30)])
        { Combos = [new(24373, "After Slice", 5), new(24374, "After Waxing Slice", 30), new(24376, "After Spinning Scythe", 45)] },

        [JobId.Viper] = new(
            [new(RattlingCoils, "Rattling Coils", 65, 3), new(SerpentOffering, "Serpent Offerings", 90, 100), new(AnguineTribute, "Anguine Tribute", 90, 5)],
            [new(34620, "Vicewinder / Vicepit", 65, 40, 2), new(34646, "Slither", 40, 30, 3),
             new(34647, "Serpent's Ire", 86, 120), ..MeleeRole],
            [new(3668, "Hunter's Instinct", 5, 40), new(3669, "Swiftscaled", 20, 40), new(3671, "Ready to Reawaken", 90, 30), new(3670, "Reawakened", 90, 30)])
        { Combos = [new(34606, "After Steel Fangs", 5), new(34607, "After Reaving Fangs", 10),
            new(34608, "After Hunter's Sting", 30), new(34609, "After Swiftskin's Sting", 30),
            new(34614, "After Steel Maw", 40), new(34615, "After Reaving Maw", 40),
            new(34616, "After Hunter's Bite", 50), new(34617, "After Swiftskin's Bite", 50)] },

        [JobId.Machinist] = new(
            [new(Heat, "Heat", 30, 100), new(Battery, "Battery", 40, 100),
             new(MachinistSummonSeconds, "Time remaining on Rook / Queen", 40, 12),
             new(MachinistSummonBattery, "Battery spent deploying Rook / Queen", 40, 100, 50)],
            [new(2876, "Reassemble", 10, 55, Trait: 84, TraitCharges: 2),
             new(2874, "Gauss Round / Double Check", 15, 30, 2, Trait: 74, TraitCharges: 3),
             new(2890, "Ricochet / Checkmate", 50, 30, 2, Trait: 74, TraitCharges: 3),
             new(2872, "Hot Shot / Air Anchor", 4, 40), new(16498, "Drill / Bioblaster", 58, 20, Trait: 94, TraitCharges: 2),
             new(25788, "Chain Saw", 90, 60), new(7414, "Barrel Stabilizer", 66, 120), new(17209, "Hypercharge", 30, 10),
             new(2878, "Wildfire", 45, 120), new(16889, "Tactician", 56, 120, Trait: 88, TraitRecharge: 90),
             new(2887, "Dismantle", 62, 120), new(7418, "Flamethrower", 70, 60), new(2864, "Rook / Queen", 40, 6),
             new(7551, "Head Graze", 24, 30), new(7548, "Arm's Length", 32, 120)],
            [new(851, "Reassembled", 10, 5), new(3864, "Hypercharged", 66, 30), new(2688, "Overheated", 30, 10, 5),
             new(3865, "Excavator Ready", 96, 30), new(3866, "Full Metal Machinist", 100, 30), new(1205, "Flamethrower", 70, 10)])
        { Combos = [new(2866, "After Split Shot", 2), new(2868, "After Slug Shot", 26)] },
    };

    internal static CombatStartingRules? ForJob(JobId job) => Jobs.GetValueOrDefault(job);
}
