using System;

namespace ArchonsRise.SaveData
{
    // Upgrades old save files in place. Idempotent; UnityEngine-free.
    public static class SaveMigrator
    {
        public static SaveFile Migrate(SaveFile file)
        {
            // v1 -> v2: places array did not exist; absent means nothing conquered.
            if (file.run.places == null)
                file.run.places = Array.Empty<PlaceConquest>();

            // v2 -> v3: skill arrays did not exist; absent means no skills owned.
            // (handSize was dropped from the model: JsonUtility ignores the stale
            // key in old files, and the value is derived from level on load.)
            if (file.run.player.ownedSkillIds == null)
                file.run.player.ownedSkillIds = Array.Empty<string>();
            if (file.run.player.exhaustedSkillIds == null)
                file.run.player.exhaustedSkillIds = Array.Empty<string>();

            // v3 -> v4: doom/spawn fields did not exist; absent means doom 0
            // and no mid-run spawns (ints already default to 0 via JsonUtility).
            if (file.run.spawnedEnemies == null)
                file.run.spawnedEnemies = Array.Empty<SpawnedEnemy>();

            // v4 -> v5: unitExhausted did not exist; absent means all units fresh.
            if (file.run.unitExhausted == null)
                file.run.unitExhausted = Array.Empty<bool>();

            // v5 -> v6: dungeon fields did not exist; absent means fresh,
            // unflagged dungeons (bools already default false via JsonUtility).
            if (file.run.dungeons == null)
                file.run.dungeons = Array.Empty<DungeonState>();

            // v6 -> v7: characterId did not exist; empty means "pre-v7", which
            // DataManager resolves to the default character.
            if (file.run.characterId == null)
                file.run.characterId = "";

            // v6 -> v7: `hp` became `toughness`. Copy the legacy value across —
            // without this a v6 save loads at toughness 0, which is both wrong
            // and the CombatRules hang case.
            if (file.run.player.toughness == 0 && file.run.player.hp > 0)
                file.run.player.toughness = file.run.player.hp;

            // v7 -> v8: hotspots array did not exist; absent means no hotspots
            // harvested (fresh, full-charge tiles re-derive from the map seed).
            if (file.run.hotspots == null)
                file.run.hotspots = Array.Empty<HotspotState>();

            // v8 -> v9: aggroedEnemies did not exist; absent means no token is armed.
            // A pre-v9 save therefore loads with every enemy inert until the player
            // steps away and back — the state it was already in before v9.
            if (file.run.map.aggroedEnemies == null)
                file.run.map.aggroedEnemies = Array.Empty<Cell>();

            // v9 -> v10: shrines array did not exist; absent means all shrines Live.
            if (file.run.shrines == null)
                file.run.shrines = Array.Empty<ShrineState>();

            // v9 -> v10: SpawnedEnemy gained a shrine-reward tag. Every spawn in a
            // pre-v10 file is an ordinary map spawn, so force the tag to -1 (0 would
            // read as ShrineReward.CardPick). Must run BEFORE the version bump — it
            // guards on `< 10`. New-run spawns set the tag explicitly at creation,
            // so they never depend on this migration.
            if (file.schemaVersion < 10 && file.run.spawnedEnemies != null)
                for (int i = 0; i < file.run.spawnedEnemies.Length; i++)
                {
                    var s = file.run.spawnedEnemies[i];
                    s.shrineRewardType = -1;
                    file.run.spawnedEnemies[i] = s;
                }

            if (file.schemaVersion < 10)
                file.schemaVersion = 10;

            // v10 -> v11: deckShortfallPending did not exist; absent means false
            // (bools already default false via JsonUtility) — no forced-rest
            // carried into the load, which is correct: a pre-v11 save predates
            // the deck-shortfall buffer entirely.
            if (file.schemaVersion < 11)
                file.schemaVersion = 11;

            // v11 -> v12: the bad-roll guardian stopped being a map token and
            // became shrine state. Two jobs, both guarded on `< 12`:
            //   1. owedReward is absent in a pre-v12 file, so JsonUtility leaves
            //      it 0 — which reads as ShrineReward.CardPick. Force -1.
            //   2. Fold any in-flight guardian's debt back onto its shrine and
            //      drop the token, so a save taken mid-Guarding still pays out.
            if (file.schemaVersion < 12)
            {
                if (file.run.shrines == null)
                    file.run.shrines = Array.Empty<ShrineState>();
                for (int i = 0; i < file.run.shrines.Length; i++)
                {
                    var s = file.run.shrines[i];
                    s.owedReward = -1;
                    file.run.shrines[i] = s;
                }
                FoldGuardiansIntoShrines(file.run);
                file.schemaVersion = 12;
            }

            // v12 -> v13: conquest progress was a bare count, which silently
            // assumed guardians die in roster order. Widen each count into the
            // leading slots — the identities the pre-v13 game would itself have
            // inferred, so a loaded save behaves no worse than it did before.
            if (file.schemaVersion < 13)
            {
                if (file.run.places == null)
                    file.run.places = Array.Empty<PlaceConquest>();
                for (int i = 0; i < file.run.places.Length; i++)
                {
                    var p = file.run.places[i];
                    if (p.defeatedIndices == null || p.defeatedIndices.Length == 0)
                    {
                        p.defeatedIndices = new int[p.defeatedCount];
                        for (int g = 0; g < p.defeatedCount; g++) p.defeatedIndices[g] = g;
                        file.run.places[i] = p;
                    }
                }
                file.schemaVersion = 13;
            }
            return file;
        }

        // Moves each legacy guardian's owed reward onto the shrine it was bound
        // to, then removes it from the spawn list — guardians are not board
        // entities any more, so a restored token would be an ordinary enemy
        // carrying a debt nothing can collect.
        //
        // A Guarding shrine is always exported (only Live shrines are omitted),
        // so the entry is there in any well-formed file. If it somehow isn't,
        // the token is still dropped: the concept it depended on is gone.
        static void FoldGuardiansIntoShrines(RunState run)
        {
            if (run.spawnedEnemies == null || run.spawnedEnemies.Length == 0) return;

            var kept = new System.Collections.Generic.List<SpawnedEnemy>();
            foreach (var e in run.spawnedEnemies)
            {
                if (e.shrineRewardType < 0) { kept.Add(e); continue; }

                for (int i = 0; i < run.shrines.Length; i++)
                {
                    var s = run.shrines[i];
                    if (s.x != e.shrineCellX || s.y != e.shrineCellY) continue;
                    s.state = 2;                          // Guarding
                    s.owedReward = e.shrineRewardType;
                    run.shrines[i] = s;
                    break;
                }
            }
            run.spawnedEnemies = kept.ToArray();
        }
    }
}
