# Black Mage (BLM)

_✅ implemented in the sim, ⚠️ partial or pending native verification._

| Action | ID | Status | Simulated behavior |
|---|---:|:---:|---|
| Blizzard | 142 | ✅ | Stance, MP, and Thunderhead on aspect entry. |
| Fire | 141 | ✅ | Stance, MP, Heart consumption, and Firestarter chance. |
| Transpose | 149 | ✅ | Changes aspect, including qualifying Paradox and Thunderhead grants. |
| Thunder | 144 | ⚠️ | Consumes Thunderhead and tracks the per-caster DoT status. |
| Blizzard II | 25793 | ✅ | Stance and MP, including recovery when it lands in Ice. |
| Scathe | 156 | ⚠️ | MP only. |
| Fire II | 147 | ✅ | Stance and MP. |
| Thunder II | 7447 | ⚠️ | Tracks the AoE DoT status per target. |
| Manaward | 157 | ✅ | Shield in the simulator's mitigation check. |
| Manafont | 158 | ✅ | Restores MP, grants Thunderhead, and grants Hearts and Paradox with the required traits. |
| Fire III | 152 | ✅ | Stance change and MP, including a free Firestarter cast. |
| Blizzard III | 154 | ✅ | Stance change and MP. |
| Umbral Soul | 16506 | ✅ | Builds Ice and Hearts in combat, fills them out of combat, and recovers MP. |
| Freeze | 159 | ✅ | Grants Hearts and restores MP when it lands in Ice with the applicable trait. |
| Thunder III | 153 | ⚠️ | Tracks the single-target DoT status. |
| Aetherial Manipulation | 155 | ⚠️ | Uses the executed party target. Verify native movement and target gating. |
| Flare | 162 | ✅ | Spends MP and Hearts, and grants Astral Soul on hit with the trait. |
| Ley Lines | 3573 | ⚠️ | The Ley Lines status owns the native floor object lifetime. Circle of Power refreshes while inside. Verify the native animation, floor effect, and cast and recast reductions in game. |
| Blizzard IV | 3576 | ✅ | Grants Hearts and restores MP when it lands in Ice with the applicable trait. |
| Fire IV | 3577 | ✅ | Spends MP and grants Astral Soul on hit with the trait. |
| Between the Lines | 7419 | ⚠️ | Moves to active Ley Lines. Verify native availability. |
| Thunder IV | 7420 | ⚠️ | Tracks the AoE DoT status per target. |
| Triplecast | 7421 | ✅ | One stack per eligible hard cast. Instant spells preserve stacks. |
| Foul | 7422 | ⚠️ | Spends Polyglot and accounts for the instant-cast trait. Verify native gates. |
| Despair | 16505 | ⚠️ | Spends remaining MP and accounts for the instant-cast trait. Verify native gates. |
| Xenoglossy | 16507 | ⚠️ | Spends Polyglot. Verify native gates. |
| High Fire II | 25794 | ✅ | Stance and MP. |
| High Blizzard II | 25795 | ✅ | Stance and MP, including recovery when it lands in Ice. |
| Amplifier | 25796 | ✅ | Adds one Polyglot within the level-dependent cap. |
| Paradox | 25797 | ⚠️ | Arms on qualifying aspect switches or Manafont, spends the mark, and grants Firestarter in Fire. Verify native button swaps. |
| High Thunder | 36986 | ⚠️ | Tracks the single-target DoT status. |
| High Thunder II | 36987 | ⚠️ | Tracks the AoE DoT status per target. |
| Retrace | 36988 | ⚠️ | Moves the native floor object to the caster and retains the status expiry. Verify the native animation, floor effect, and availability. |
| Flare Star | 36989 | ⚠️ | Requires and spends six Astral Soul. Verify native gates. |
| Addle | 7560 | ✅ | Shared caster action. Reduces outgoing damage in the simulator's mitigation check. |
| Sleep | 25880 | ⚠️ | MP cost only. Enemy susceptibility is outside the current sim. |
| Lucid Dreaming | 7562 | ✅ | Shared status grant. BLM's MP handler suppresses recovery during Astral Fire. |
| Surecast | 7559 | ⚠️ | Blocks knockbacks explicitly marked resistible. Other mechanics retain their displacement. |
| Swiftcast | 7561 | ✅ | Consumed by the next eligible hard cast. |
| Intelligence tinctures and gemdraughts | Varies | ⚠️ | Grants Medicated. Verify native item recast. |

## In-game verification

Verify the elemental gauge, Polyglot countdown and native resource gates, level sync, cast and recast reductions, and the action queue after a cancelled cast. Numeric damage and Thunder damage ticks remain outside the current simulation.
