# Enemy Traits, Per-Enemy Blocking & Wound Plumbing — Design

**Date:** 2026-07-29
**Status:** Approved for planning
**Supersedes:** nothing. Extends the phased combat engine (spec 2026-07-21) and the
Toughness-as-divisor decision (2026-07-06).

---

## 1. Problem

Enemies are three numbers — `enemyHP`, `enemyAttack`, `influenceCost` — plus a tier. Every fight
therefore asks the same question ("do I have enough Attack?") and the only variable is scale. The
phased engine (Siege → Defend → Attack) built a decision *structure* in M2 that the content has
never given the player a reason to use: with no reason to prefer one target over another, the Siege
phase degenerates into "spend Siege if you happen to have some."

The Defend phase has the same problem in a second form. Siege and Attack are per-enemy click
actions, but Defend is a single global threshold — one number you either clear or don't. It is the
only phase with no per-enemy decision, and no summed comparison can ever show the player what an
individual enemy's traits actually cost them.

This spec therefore has three parts:

1. **Enemy traits** — authored flags that bend the combat math (§3–5).
2. **Per-enemy blocking** — the Defend phase becomes an allocation decision, which is also what
   gives every trait a per-enemy readout (§7).
3. **Wound plumbing** — rebuilt into something the next phase (unit wounds) can extend without
   rework (§6).

The three are one spec because they are one mechanism: traits set what an enemy costs to block,
blocking decides which enemies reach the wound math, and the wound math decides where the wounds
land.

### Why the wound plumbing is in the same spec

Archon's Rise has no HP. Wounds *are* the health system: they are the lifeline, the loss condition,
and the game's definition of "dead." The Doom Clock supplies tension, but the run is lost by wounds
becoming unmanageable. Any change that multiplies or redirects wounds is therefore touching the
game's core survival axis, and doing that on plumbing that hardcodes "wounds go to hand" would
have to be undone immediately. The traits below need a discard destination on day one; units need
a third destination next. Build the concept once.

---

## 2. Design principles

Three rules govern what may be authored as a trait.

**Traits attach to a pipeline stage, never to ad-hoc code.** Section 4 defines one resolution
pipeline. A trait names a stage and a modifier. If an idea cannot be expressed as a stage modifier,
it is not a trait — it is a feature, and it gets its own spec.

**Self traits and aura traits do different jobs.** A *self* trait makes one enemy harder to remove.
An *aura* trait makes the whole fight worse while its owner lives. Auras are the reason the Siege
phase exists: a 2 HP Miasma herald beside a 6 HP ogre is the correct Siege target, because killing
the cheap enemy before Engage halves the wounds from the *ogre's* attack. That inverts the natural
"kill the big one" instinct and is a decision that can only exist because Siege resolves before the
counterattack. Target ratio: roughly **one aura per three self traits**. Too many auras and every
fight collapses into a single forced kill order.

**At least half of all fights are solo.** Field enemies are single-enemy encounters and are the
majority of combats in a run. A trait that is inert or degenerate in a solo fight is only half a
trait. Nine of the thirteen below are fully live solo; the four that are not are auras, and auras
are reserved for guardian rosters by authoring rule (§5.3).

### The cap/surcharge symmetry

Two traits define opposite ends of the wound math and should be read together:

- **Swift** raises the bar without raising the punishment. Its threat doubles, but wounds are
  **capped** at its base Attack — so failing to block a Swift enemy completely costs exactly what
  failing to block an ordinary one does. Swift punishes the near-miss, not the total whiff.
- **Brutal** raises the punishment without raising the bar. Its threat is normal, so the Defend
  needed to block it is unchanged — but going unblocked adds a **surcharge** that deliberately
  bypasses the cap.

One is a wall, the other is a cliff. A Swift+Brutal elite is genuinely dangerous while remaining
completely readable to a player who has met each trait alone.

---

## 3. Data model

### 3.1 `EnemyTrait`

```csharp
[Flags]                              // APPEND-ONLY: new members go at the end
public enum EnemyTrait
{
    None      = 0,
    // self
    Armored   = 1,     Elusive = 2,     Hulking  = 4,     Swift    = 8,
    Brutal    = 16,    Toxic   = 32,    Leech    = 64,    Harrying = 128,
    Vengeful  = 256,
    // aura
    Warlord   = 512,   Miasma  = 1024,  Ironclad = 2048,  Outrider = 4096,
}
```

One new field on `EnemiesSO`: `public EnemyTrait traits;`

Traits are **not saved**. They are read from the SO via the enemy's stable `AllCards.id`, exactly
like `enemyHP`. **No save schema bump.**

### 3.2 `EnemyTraitTuningSO`

One shared asset holding every number, wired onto `CombatController` — the same pattern as
`RewardTuningSO` on `Rewards` and `DoomTuning` on the doom system. Enemies tick boxes; the asset
owns the magnitudes. This is deliberate: **`Armored` means one fixed thing game-wide**, so the
keyword is learnable after a single fight, and a playtest retune of all armor is one field.

| Field | Start | Meaning |
|---|---|---|
| `armorSiegeMult` | 2 | Armored: Siege cost multiplier |
| `hulkAttackMult` | 2 | Hulking: Attack cost multiplier |
| `swiftThreatMult` | 2 | Swift: threat multiplier |
| `brutalSurchargeMult` | 1 | Brutal: multiples of base Attack added past the cap |
| `warlordBonus` | 1 | Warlord: Attack granted to each *other* survivor |
| `toxicCopies` | 1 | Toxic: discard copies per wound in its share |
| `leechCrystals` | 1 | Leech: crystals stolen per wound in its share |
| `vengefulWounds` | 1 | Vengeful: wounds on an Attack-phase kill |
| `harryHandPenalty` | 1 | Harrying: hand-size reduction next turn |

### 3.3 `EnemyTraitRules`

New pure static class in `Assets/Scripts/CardPlay/` beside `CombatRules`, in **its own folder
asmdef** with the EditMode tests asmdef referencing it (per the established pure-class placement
rule — a pure class without its own asmdef fails EditMode tests with CS0103).

**`CombatRules` is not modified.** Its four functions and every existing assertion in
`CombatRulesTests` stay exactly as they are. `GroupWoundCount` gains a sibling, not a rewrite.
`EnemyTraitRules` calls into `CombatRules.WoundCount(Normal, 0, x, toughness)` for its bite loop
so there is one and only one implementation of "divide a shortfall into Toughness-sized bites."

---

## 4. The resolution pipeline

### 4.1 Aura resolution

Auras resolve into per-enemy trait masks **before** anything else runs:

```
EffectiveTraits(e, roster) = e.traits | GrantedByAuras(roster)

GrantedByAuras(roster) = (any survivor has Miasma   ? Toxic   : 0)
                       | (any survivor has Ironclad ? Armored : 0)
                       | (any survivor has Outrider ? Swift   : 0)
```

Everything downstream reads effective traits and needs no aura awareness whatsoever.

Two consequences worth stating. **The player learns one vocabulary:** `Toxic` is learned once, and
`Miasma` is then simply "everyone is Toxic" — granting auras cost zero new concepts. And **granting
is a bitwise OR, so auras cannot double-stack by construction**: two Miasma enemies are idempotent.
`Warlord` is the exception — its bonus is *additive* and does stack (two Warlords = +2 Attack to
everyone else). That difference is deliberate, and Warlord's aura is small (1) because of it.

### 4.2 Per-enemy values

```
base_e      = EffectiveAttack(e) + warlordAura(e, roster)
warlordAura = warlordBonus * count(survivors w != e where Warlord in EffectiveTraits(w))

threat_e    = Swift ? base_e * swiftThreatMult : base_e      // bar to clear
basis_e     = base_e                                          // normal punishment

siegeCost_e = Elusive ? int.MaxValue                          // unreachable by any pool
            : Armored ? EffectiveHP(e) * armorSiegeMult
            : EffectiveHP(e)
atkCost_e   = Hulking ? EffectiveHP(e) * hulkAttackMult : EffectiveHP(e)
```

**No trait modifies Influence cost.** See §5.5 — Influence is already the strongest removal in the
game and its balance lever is scarcity, not price.

`Elusive` uses `int.MaxValue` rather than a separate bool so the Siege phase keeps exactly one
comparison. The Siege button is `UiLock`-dimmed for an Elusive enemy rather than silently failing.

`base_e` uses `EnemyCard.EffectiveAttack` / `EffectiveHP`, which already fold in doom-scaling
bonuses and are already read by both display and combat. Traits stack on top of the existing
indirection rather than introducing a parallel one.

### 4.3 Group counterattack

Replaces the `GroupWoundCount` call at `CombatController.cs:343`.

Every sum runs over **unblocked survivors only** (§7). `defendLeft` is the Defend pool *after* blocks
were paid for. With no blocks placed, `unblocked` is the whole roster and `defendLeft` is the whole
pool — making this **identical to today's behaviour**, which is the compatibility guarantee that
lets blocking be added without re-tuning any enemy.

```
 0. unblocked   = survivors where not Blocked(e)                FILTER    (§7)
 1. totalThreat = Σ threat_e            over unblocked
 2. totalBasis  = Σ basis_e             over unblocked
 3. shortfall   = max(0, totalThreat - defendLeft)
 4. if shortfall == 0  →  no wounds, no surcharge, no toxic, no leech. DONE.
 5. capped      = min(shortfall, totalBasis)                    CAP       (Swift's guarantee)
 6. surcharge   = Σ (base_e * brutalSurchargeMult) for Brutal ∈ unblocked
 7. effective   = capped + surcharge                            SURCHARGE (Brutal's punishment)
 8. handWounds  = bite(effective, toughness)
```

**Auras are computed over all living survivors, not just unblocked ones** (§4.1 is unchanged).
A blocked enemy is alive — see §7.4.

Step 5 before step 7 is load-bearing. The cap exists to stop Swift inflating the punishment; the
surcharge exists to let Brutal inflate exactly that. Applying them in the other order would let the
cap swallow the surcharge and Brutal would do nothing.

### 4.4 Share attribution (Toxic and Leech)

The summed counterattack has no notion of whose wound is whose, so per-enemy payout traits claim a
**share of the contribution**:

```
contribution_e    = basis_e + (Brutal ? base_e * brutalSurchargeMult : 0)    // unblocked only
totalContribution = Σ contribution_e     over unblocked

Share(trait) = effective * (Σ contribution_e where trait ∈ EffectiveTraits(e)) / totalContribution
               // integer division, floor. Guard totalContribution == 0 → 0.

discardWounds  = bite(Share(Toxic), toughness) * toxicCopies
crystalsStolen = bite(Share(Leech), toughness) * leechCrystals    // clamped to crystals held
```

One function serves both traits. `totalContribution` cannot be zero when `effective > 0` (a nonzero
shortfall implies a nonzero threat implies a nonzero basis), but the guard is written anyway so the
rule is safe on its own regardless of caller.

### 4.5 Worked examples

`bite(x, t)` = `CombatRules.WoundCount(Normal, 0, x, t)`. All at Toughness 2.

| # | Roster | Defend | Result | Check |
|---|---|---|---|---|
| 1 | Swift (Atk 3) | 5 | threat 6, shortfall 1, cap 3→1 → **1 hand** | Swift punishes the near-miss |
| 2 | Swift (Atk 3) | 0 | threat 6, shortfall 6, cap 3 → **2 hand** | never worse than non-Swift |
| 3 | Brutal (Atk 4) | 4 | shortfall 0 → **0** | blocked is blocked |
| 4 | Brutal (Atk 4) | 2 | cap 2, +4 = 6 → **3 hand** | (8−2)/2 = 3 |
| 5 | Brutal (Atk 4) | 0 | cap 4, +4 = 8 → **4 hand** | (8−0)/2 = 4 |
| 6 | Toxic (Atk 2) | 0 | effective 2 → 1 hand; share 2 → **1 hand + 1 discard** | exact doubling |
| 7 | Toxic Spider (2) + Ogre (4) | 0 | effective 6 → 3 hand; share 6·2/6=2 → **3 hand + 1 discard** | the ogre's club isn't poisoned |
| 8 | #7 + Miasma Herald (1) | 0 | all Toxic; effective 7 → 4 hand; share 7 → **4 hand + 4 discard** | full doubling, zero special-casing |
| 9 | Warlord (2) + Ogre (4) | 0 | Ogre base 5; threat 7 → **4 hand** | aura is real Attack |
| 10 | Swift+Brutal (Atk 4) | 5 | threat 8, shortfall 3, cap 4→3, +4 = 7 → **4 hand** | wall and cliff compose |

Row 8 is the payoff of §4.1: the Miasma case required no code beyond the OR.

---

## 5. Trait catalogue

### 5.1 Self traits (all fully live in a solo fight)

| Trait | Rule | Stage |
|---|---|---|
| **Armored** | Siege must cover **2× HP** | `siegeCost` |
| **Elusive** | **Siege cannot remove it** — Attack or Influence only | `siegeCost` |
| **Hulking** | Attack must cover **2× HP**; Siege unaffected | `atkCost` |
| **Swift** | threat ×2; wounds still capped at base Attack | `threat` |
| **Brutal** | while unblocked, adds its Attack again **past the cap** | `surcharge` |
| **Toxic** | its share of wounds is doubled; copies go to **discard** | `payout` |
| **Leech** | each wound in its share also **steals 1 crystal** | `payout` |
| **Vengeful** | defeating it **in the Attack phase costs 1 wound** (to `Hand`, per kill) | `payout` |
| **Harrying** | fleeing costs **−1 hand size** on the next turn's top-up | `flee` |

**Armored / Hulking** are exact mirrors, which is why they cost one concept between them: one says
"don't Siege this," the other says "Siege this." Solo, they pose a sharp resource question rather
than a bigger number.

**Elusive** is the counter to Siege trivializing the Siege phase — it cannot be pre-cleared, so the
player either eats the counterattack or pays it off.

**Vengeful** is the trait that makes Siege matter in a *solo* fight, where Siege is otherwise just a
differently-coloured currency. It punishes the Attack phase only, so the Siege wound-free promise
in mechanics.md is preserved and rewarded rather than broken.

**Harrying** replaced an earlier Explore-tax design. Fleeing should be a strategic choice made with
full information, and a player who flees is usually willing to re-engage next turn — so an Explore
cost taxed a decision the player had already made correctly. A hand-size cut compounds properly
instead: you flee, you take a wound *into hand*, and you draw one fewer card next turn.

### 5.2 Aura traits (guardian rosters only)

| Trait | Effect |
|---|---|
| **Miasma** | grants **Toxic** to every survivor |
| **Ironclad** | grants **Armored** to every survivor |
| **Outrider** | grants **Swift** to every survivor |
| **Warlord** | every *other* survivor gets **+1 Attack** (threat and basis both) |

### 5.3 Authoring rules

- **Field (solo) enemies draw only from self traits.** A granting aura on a solo enemy silently
  degenerates into its own self trait (solo Miasma ≡ Toxic), which is wasted authoring and a
  confusing first encounter with the keyword.
- **Auras are reserved for guardian rosters**, which keeps Keeps and Castles structurally different
  from map fights rather than merely bigger. A Castle's two guardians make an ideal aura+bruiser pair.
- **Aura enemies are authored at low HP (2–4)** so the Siege targeting puzzle is winnable.
- **Tier 1:** at most one self trait, never an aura. **Tier 2:** one or two self traits, or one weak
  aura. **Tier 3:** an aura plus a self trait.
- **Never author Elusive with Armored** (the second is dead text), or **Vengeful/Elusive on tier 1**.
- **An Elusive enemy must have `canInfluence = true`.** Elusive removes the Siege option, so without
  an Influence out it is simply "you will eat the counterattack" — a removed choice rather than a
  redirected one. Pairing them gives Elusive a clear identity ("bribe it, don't besiege it") and
  points the player at the Influence economy instead of into a wall.
- **Traits are additive to the tier system, not a replacement.** Tier still drives rewards and doom
  gating; traits drive texture.

### 5.4 Explicitly rejected

**Regenerator / chip damage / anything reading a wounded enemy's state.** Kills are **binary**:
`Player.ResolveAttack` runs `CanDefeat` then `playerAttack -= hp`. Enemies are never "hurt," only
removed. These traits are not expressible without adding a real enemy HP pool — a much larger change
than this spec, and one that would rewrite the phase machine.

**Proud ("Influence cost doubles once you press Engage").** Cut during spec review as **inert under
the actual phase rules**: Influence is a Siege-phase action and Engage is the commit that *ends* the
Siege phase, so there is no window in which the doubled cost could ever be paid. Any "cost rises
under pressure" trait requires Influence to remain available past the Siege phase — a change to the
phase machine, not a trait. No replacement — see §5.5, no trait touches Influence.

**Venal ("Influence cost halved").** Drafted as Proud's replacement and scrapped on review: it
pushes the wrong lever. Influence is already the strongest removal in the game (§5.5), so a trait
making it *cheaper* worsens the imbalance it was meant to add texture to. The catalogue ships at
thirteen rather than padding the count.

**Warded ("only empowered stat points count against it").** The most on-pillar idea considered, and
cut on cost: the stat pools are bare `int`s, so it needs a parallel empowered-origin counter threaded
through `Player` stat banking and the undo stack. That is a subsystem, not a trait. **Leech** is the
crystal-touching trait that ships. Warded is a strong candidate for a later spec.

### 5.5 Influence is not a trait surface — scarcity is its only lever

**No trait in this catalogue modifies Influence cost, and none should.** Recorded here because it
constrains every future trait as well.

Compare the three removals available in the Siege phase:

| | wound-free | full rewards | improvisable | can recruit |
|---|---|---|---|---|
| **Attack** (Attack phase) | no | yes | yes | no |
| **Siege** | yes | yes | **no** | no |
| **Influence** | yes | yes | **yes** | yes (w/ Charismatic) |

Influence dominates Siege on every axis. It is wound-free and pays full rewards exactly like Siege,
it can additionally recruit, and unlike Siege it is **improvisable** from `CharacterSO.improvInfluence` —
so a player with no cards can still produce it. Siege's whole identity is that it cannot be
improvised and comes only from advanced cards and units (a stated pillar); Influence quietly has the
same power without that constraint.

**The only thing holding Influence in check is `canInfluence` availability.** That makes availability
the balance lever, and it must be defended:

- **Target ~30% of enemies with `canInfluence = true`.** Below that, Influence builds have nothing to
  buy; much above it and Siege stops being valuable, which collapses the reason non-improvisable
  Siege exists at all.
- A trait that lowers Influence cost pushes directly against this and is forbidden. A trait that
  *raises* it needs Influence to remain available past the Siege phase (§5.4, deferred).
- The 30% figure is a **map-wide authoring target across the enemy pool**, not a per-roster rule.

This also sets the price of the Elusive pairing rule in §5.3: Elusive enemies must be influenceable,
so they consume part of the 30% budget. Author them as a deliberate slice of it rather than on top.

---

## 6. Wound plumbing

### 6.1 Destination

```csharp
public enum WoundDestination { Hand = 0, Discard = 1 }   // APPEND-ONLY. Unit = 2 next phase.
```

`PlayerHand.AddWound(WoundDestination dest = WoundDestination.Hand)`. The `Hand` path is exactly
today's behaviour. The `Discard` path instantiates the wound card and hands it to
`DiscardPile.AddCardToDiscard(Card)` rather than parenting it to `handLayout.Container`.

Note that wounds **already** go to hand, not the deck. `mechanics.md:74` claims they are "shuffled
into the deck"; that is stale and is corrected by this spec (§10).

### 6.2 The placement list is the seam

`CombatController.cs:345` currently runs `for (i < wounds) hand.AddWound();`. It becomes:

```
var placements = producer.Place(...);        // IReadOnlyList<WoundDestination>
foreach (var d in placements) hand.AddWound(d);
```

Today the producer is the pure `WoundPlacementRules`. Next phase it becomes something that can open
the unit picker and return the player's assignment. **`CombatController` never learns what a unit
is** — it consumes a list either way. That is the entire reason to build this now: the next phase
swaps the producer, not the caller.

### 6.3 The wound plumbing contract

Wounds are this game's health system, so the invariants are stated explicitly and are binding on
future work:

1. **Every wound has exactly one destination**, chosen at placement time from `WoundDestination`.
   There is no other way for a wound to enter the run.
2. **`WoundDestination` is append-only.** Wound *placements* are not saved, but wounds land in real
   zones that are.
3. **The placement producer is swappable.** Its consumer depends on `IReadOnlyList<WoundDestination>`
   and nothing else.
4. **Destinations are either counting or non-counting.** `Hand` and `Discard` **count** toward the
   wound-out loss. `Unit` (next phase) will **not**. `TotalWoundCount()` enumerates counting zones
   explicitly, so adding a zone to the loss axis is always a deliberate edit there — never a
   side effect of adding a destination.
5. **Every wound add re-runs the wound-out check** at the moment of add. Preserved from today, and
   correct because wound adds are not undoable and a crossed threshold can never be un-crossed.
6. **Wound adds are not undoable commands.** Preserved from today.
7. **A placement producer that cannot place must fall back to `Hand`.** A wound is never dropped.

### 6.4 Forward compatibility with unit wounds

Recorded here so the next spec inherits a settled frame rather than relitigating it:

**Unit wounds will not count toward wound-out.** This is what makes the whole feature work. If they
counted, units would be a liability and no player would ever assign a wound to one. Because they do
not, **units become a wound sink that trades run-loss pressure for army capability**: park the wound
on the Knight, the Knight is disabled until healed, the deck stays clean, and the loss clock does not
advance.

That opens a **second healing channel priced in Influence**. Disband a wounded unit, recruit a fresh
one, and you have converted Influence directly into survival without touching a Heal card. An
Influence build is therefore buying *staying alive*, not just buying recruits — which makes Influence
a survival stat and serves pillar 1 (army and territory as first-class) rather than leaving recruits
as a stat-option sidecar.

Two mechanical notes for that spec:

- **Unit armor should be a divisor, not a pool** — the same shape as `PlayerToughness` and the
  2026-07-06 decision. Units and the player then share one mental model ("mitigation never depletes;
  it makes each bad outcome smaller"), and `CombatRules.WoundCount`'s bite loop is reused as-is.
- **Toxic gains a natural second meaning: bypasses unit armor.** Venom ignoring armor is obvious
  flavour and a real counter to a unit-wall strategy.

**`UnitPickerPanel` is the right host.** `OpenForRefresh(budget, onPick)` is already just
*filter + cost + title + callback*; only four things are refresh-specific (the title, the `IsPlayed`
filter, `RefreshRules.PickCost/CanPick`, and "unspent budget is lost on close"). One caveat, flagged
now so it is not discovered late: `Close()` is public **specifically** so `ClickOffCatcher` can
dismiss the panel, and forfeiting the remainder is correct for refresh but **catastrophic for wound
assignment** — the player would dismiss their way out of the counterattack. A wound mode must disable
click-off and rely on contract rule 7.

**Out of scope for this spec:** unit wounds, unit armor, unit healing, and the picker's wound mode.
The seam above means they cost no rework.

---

## 7. Per-enemy blocking (Defend phase)

The Defend phase is currently the only phase with no per-enemy interaction: Siege and Attack are
per-enemy clicks, while Defend is a single global threshold you either clear or don't. This section
makes Defend an **allocation decision** and, in doing so, gives every trait a per-enemy readout.

### 7.1 The interaction

Each enemy card gains a **Defend button**, live only in the Defend phase. It displays that enemy's
**`threat_e`** — the full trait-adjusted number, so a Swift enemy with Attack 3 reads `6`. That
display *is* the answer to "what do this enemy's traits cost me," which no summed comparison could
ever show.

- The button is clickable only when `defendLeft >= threat_e`; otherwise `UiLock`-dimmed at alpha 0.4
  on top of `Button.interactable`, per the UI-language contract.
- Clicking **blocks** that enemy: `defendLeft -= threat_e`, and the enemy leaves the unblocked set.
- Blocking is **all-or-nothing per enemy**. There is no partial payment.
- **Residual soak:** whatever Defend is left over still feeds the group comparison in §4.3 against
  everyone still unblocked. No point of Defend is ever wasted.

### 7.2 Why residual soak rather than pure all-or-nothing

Without it, sub-threshold Defend is worth exactly nothing (enemy Attack 5, Toughness 2: Defend 0 →
3 wounds, Defend 4 → 3 wounds), which turns every Defend card into a lottery ticket and stacks a
silent difficulty spike on top of traits that already multiply wounds. Residual soak keeps the
allocation decision while preserving partial credit, and — critically — makes "block nobody" exactly
identical to today's math.

### 7.3 The payoff: traits decide what is worth blocking

This is the reason to build it. Blocking cost is `threat_e` but blocking *prevents* `contribution_e`,
and traits drive those two apart:

| Enemy | Cost to block | What blocking prevents | Verdict |
|---|---|---|---|
| **Brutal** (Atk 4) | 4 | basis 4 **+ surcharge 4** | **block** — 4 buys 8 |
| **Toxic** (Atk 3) | 3 | basis 3 **+ its discard copies** | **block** — stops the doubling |
| **Leech** (Atk 3) | 3 | basis 3 **+ crystal theft** | **block** — protects the crystals |
| **Swift** (Atk 3) | **6** | basis 3 (already capped) | **soak** — 6 to prevent 3 is a trap |
| **plain** (Atk 4) | 4 | basis 4 | neutral — pure allocation |

Swift now reads as *"fast and hard to pin down, but not that dangerous — don't waste your shield."*
Brutal reads as *"stop this one or else."* The §2 wall/cliff symmetry stops being a math curiosity
and becomes the fight's central decision.

### 7.4 Blocking is not killing

**A blocked enemy is still alive, so its auras still apply.** A blocked Warlord still grants +1
Attack to every other survivor; a blocked Miasma herald still makes the roster Toxic. Only removal
(Siege, Influence, or an Attack-phase kill) strips an aura.

This is deliberate and load-bearing: it keeps **Siege strictly better than blocking** and preserves
the Siege-phase targeting puzzle that the aura traits exist to create (§2). If blocking suppressed
auras, the cheap-herald-first decision would evaporate and the Siege phase would go back to being a
formality.

### 7.5 Phase gating

`CombatPhaseRules` already owns per-phase gating as pure predicates. Blocking follows the pattern
with one added member:

```csharp
public static bool CanBlock(CombatPhase phase) => phase == CombatPhase.Defend;
```

Target button policy — `EnemyCard` routes **all four** buttons through `CombatPhaseRules` uniformly
rather than each managing its own state:

| Phase | Live per-enemy buttons | Advance button |
|---|---|---|
| Siege | Siege, Influence | Engage (hidden while a siege kill is staged) |
| Defend | **Defend** | TakeHit (n wounds) → Counterattack at zero |
| Attack | Attack | hidden; Withdraw is its own control |
| Resolved | none | hidden |

The advance button needs **no new behaviour**: `Advance()` already returns `TakeHit` with a live
wound count and flips to `Counterattack` when Defend covers the attack. Only its *inputs* change —
it reads the unblocked totals and `defendLeft`. Since the parameter list is already six long, the
per-enemy pipeline results should be gathered into a single `CounterattackPreview` struct
(`unblockedThreat`, `unblockedBasis`, `brutalSurcharge`) and passed as one argument rather than
growing the signature further.

### 7.6 Undo and the commit point

Each block is an undoable command (`BlockCommand`) on the existing stack, symmetric with card plays
and unit uses: undo restores `defendLeft` and returns the enemy to the unblocked set. The wound
preview on the advance button re-renders after every block and undo, so the player always sees the
consequence of the current allocation before committing.

Pressing the advance button is the **commit point**, exactly as Engage already is: it resolves the
counterattack and calls `commands.ClearStack()` (the precedent is `CombatController.cs:327`). Blocks
cannot be undone afterwards.

---

## 8. Presentation

**No new icons yet — single-character TMP badges instead.** Thirteen new glyphs is a large art and
registry cost (each needs a sprite asset, an `IconConcept`, an `IconMarkup.TmpName` case, and an
`IconRegistry` entry), but traits are unreadable without *some* per-enemy marker and a full text
line per trait will not fit on an enemy card. A single character is the interim.

### 8.1 The badge and its upgrade seam

```csharp
// IconMarkup — the single owner of every glyph string, badges included.
public static string TraitBadge(EnemyTrait t)   // "A" today; "<sprite=…>" when art lands
```

**Call sites never change.** When the icons are authored, this one function body swaps from a letter
to a sprite tag and every panel updates at once. Nothing else in the codebase learns that traits
became icons. This is the whole reason badges route through `IconMarkup` rather than being formatted
at the point of use — the same rule that already forbids hand-rolled `<sprite=…>` literals.

| Trait | Badge | | Trait | Badge |
|---|---|---|---|---|
| Armored | **A** | | Vengeful | **V** |
| Brutal | **B** | | *aura:* Warlord | **W** |
| Elusive | **E** | | *aura:* Miasma | **M** |
| Harrying | **H** | | *aura:* Ironclad | **I** |
| Hulking | **K** | | *aura:* Outrider | **O** |
| Leech | **L** | | | |
| Swift | **S** | | | |
| Toxic | **T** | | | |

Every badge is its trait's first letter except **Hulking = K** (`hulK`), which yields to Harrying.
Letters are arbitrary and cheap to change; what matters is that they are unique and centrally owned.

**Auras render in a distinct tint** (self traits neutral, auras amber) via `IconMarkup`'s existing
`<color>` handling. One colour pair, no new assets, and it makes the single most important read in a
guarded fight — *which of these is buffing the others* — instant. Auras drive the Siege targeting
puzzle (§2); the player should not have to hover to find them.

### 8.2 Rule text is generated, never authored

A trait's one-line rule is built **from the tuning values at runtime**, not written as a string:

```
Armored → $"Siege must cover {armorSiegeMult}× {hp-icon}"
```

If `armorSiegeMult` is retuned to 3, every readout follows. Authored rule text would silently go
stale the first time a number moves, and §3.2 exists precisely so numbers can move freely.

Longer explanations belong in a **`HelpEntrySO`** for a Traits panel, reached by the standard `?`
— consistent with the existing rule that short reactive copy points at a help entry for the durable
version.

### 8.3 Where badges appear

- **`EnemyCard`** gains a compact badge row beside the existing HP/Attack readouts — badges only,
  no words. Trait count is small enough (§5.3 caps tier 1 at one trait, tier 3 at two) that the row
  never overflows.
- **Enemy preview** (the `PreviewRules.CanPreview` gate) is **the legend**. It lists each trait as
  `badge + name + generated rule line`, which is what makes a bare letter on the card learnable:
  see the badge, hover, read the rule. A badge is never shown anywhere the player cannot reach a
  preview.
  Preview must show **roster-aware effective values** — a Warlord changes the ogre's displayed
  Attack, and an Ironclad changes its Siege cost. Showing raw SO values here would be a lie the
  player only discovers after committing.
- **The Defend button** (§7.1) shows `threat_e`, which is itself a trait readout — it is the one
  place the player sees a trait's cost as a number they must actually pay.
- **Trait effects report through `GameLog`, never a modal.** "The spider's venom festers — 1 wound
  to your discard." This follows the established preference for tooltip+log over per-event popups,
  and it is required by the existing rule that no mid-fight event may pop a modal that interrupts a
  Siege or Attack decision.

---

## 9. Testing

All new rules are pure and testable through the CLI harness (compiled with MonoBleedingEdge `mcs`,
not the legacy Framework `csc`, which is C# 5 and rejects expression-bodied members).

**`EnemyTraitRules`** — every row of the §4.5 table becomes a named test, plus:
cap never exceeds `totalBasis`; surcharge survives the cap; `shortfall == 0` short-circuits all
payout traits; aura OR-ing is idempotent across duplicate aura enemies; Warlord excludes itself;
Warlord stacks additively; `Share` guards `totalContribution == 0`; Elusive's siege cost is
unreachable by any pool; Armored and Hulking touch only their own cost; no trait path alters
`influenceCost` (§5.5).

**Blocking (§7)** — the compatibility guarantee is the headline test: **with no blocks placed, the
pipeline result equals today's `GroupWoundCount` for every roster**, which is what allows blocking to
ship without re-tuning a single enemy. Plus: blocked enemies contribute nothing to threat, basis,
surcharge, or share; `defendLeft` decrements by `threat_e` (not `basis_e`); leftover Defend still
soaks the unblocked remainder; blocking every enemy yields zero wounds; **auras from a blocked enemy
still apply** (§7.4); `CanBlock` is true only in the Defend phase; and the §7.3 verdicts hold
numerically — blocking a Brutal prevents more than it costs, blocking a Swift prevents less.

**Trait badges (§8.1)** — a validation test in the shape of the existing `IconRegistryValidationTests`:
**every `EnemyTrait` member has a badge**, and **all badges are unique**. Written as an exhaustive
sweep over the enum, so adding a fourteenth trait without a badge fails the build rather than
shipping an invisible trait. A second assertion pins that every trait produces a non-empty generated
rule line (§8.2), so a retuned value can never leave a blank readout.

**`WoundPlacementRules`** — placement counts match hand/discard splits; an all-`Hand` list for a
trait-free fight is byte-identical to today's behaviour.

**Regression** — `CombatRulesTests` must pass **unmodified**. If a change to it is proposed, the
pipeline has drifted from §3.3 and that is the bug.

**Not unit-tested (verify in-editor):** the discard visual path, hand-size reduction across a turn
boundary, preview text, the Defend button's prefab wiring, and undo of a block restoring both the
Defend pool and the advance button's wound preview.

---

## 10. Documentation updates (same change)

- **`content-rules.md`** — `EnemyTrait` in the enum list; `traits` on the `EnemiesSO` table; a new
  `EnemyTraitTuningSO` section; §5.3 authoring rules; and a note under the UI-language section that
  **`IconMarkup.TraitBadge` is the sole owner of trait glyphs**, currently letters, later sprites —
  so the "never hand-roll a glyph" rule covers badges too.
- **`mechanics.md`** — a Traits section under Combat; the cap/surcharge rule in the wound math;
  **rewrite the Defend-phase description** — it is no longer a single summed comparison but
  per-enemy blocking plus residual soak (§7), and blocking does not suppress auras;
  **correct line 74** ("shuffles Wound cards into the deck" → wounds are added to the **hand**, and
  may now be placed into the **discard**).
- **`balance.md`** — the §3.2 starting values, the tier guidance, and the **~30% `canInfluence`
  target** (§5.5) as a standing authoring rule for the enemy pool.
- **`decisions-log.md`** — the six decisions of 2026-07-29: traits are flags with shared tuning;
  Swift is capped and Brutal is surcharged; auras grant existing self traits rather than inventing
  new ones; unit wounds will not count toward wound-out; **Influence is balanced by scarcity (~30%
  of enemies), never by trait-modified cost**; **Defend is per-enemy blocking with residual soak,
  and blocking never suppresses auras**.

---

## 11. Open items

None blocking.

- **Trait icons** — thirteen sprite assets replacing the letter badges. Deliberately deferred, not
  dropped: §8.1 exists so the swap is a single function body. Worth doing once the catalogue has
  survived playtest and the letters have proven which traits players actually confuse.
