using System.Collections.Generic;
using UnityEngine;

// What a defeat granted, so GameManager.ResolveDefeat can name it in the
// message and decide whether to open the card pick.
public struct RewardSummary
{
    public int exp;
    public EmpowerType? crystal; // null when no crystal rolled
    public bool cardPick;        // a card choice is pending (roll hit AND pool non-empty)
    public int tier;
}

// Reward-granting service for enemy defeats and dungeon completions (M2.9).
// Every reward derives from a tier (spec 2026-07-10): Experience is granted
// every time (bell-curve sampled from the tier's range), then a crystal and a
// card are rolled independently against the tier's odds. Card rewards offer a
// choice from that tier's pool through RewardCanvas and grant the chosen card
// via the single PlayerDeck.AddCard path.
public class Rewards : MonoBehaviour
{
    public CrystalInventory crystals;
    [SerializeField] Player player;
    [SerializeField] PlayerDeck deck;
    [SerializeField] RewardCanvas rewardCanvas;
    [SerializeField] RewardTuningSO tuning;

    // Wired from GameManager.ResolveDefeat. Applies exp (+ any crystal) instantly
    // and reports the result; the card pick, if any, is opened by the caller so
    // it lands after the defeat message. Dungeon fights pay experience only —
    // the dungeon's reward is completion-gated (spec 2026-07-13).
    // expOnly is set by the caller for dungeon fights (spec 2026-07-21, Spec 2):
    // combat context, not a global delve flag, now decides exp-only routing.
    public RewardSummary GetReward(EnemyCard enemy, bool expOnly)
    {
        if (expOnly) return GrantExpOnly(enemy.enemySO.tier);
        return Grant(enemy.enemySO.tier);
    }

    // Per-fight dungeon grant: the tier's bell exp sample, no bonus rolls.
    public RewardSummary GrantExpOnly(int tier)
    {
        int exp = RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));
        player.PlayerExp += exp;
        return new RewardSummary { exp = exp, crystal = null, cardPick = false, tier = tier };
    }

    // Dungeon completion bundle (spec 2026-07-13): guaranteed, no dice — one
    // exp roll at the dungeon's tier, then rewardCount crystals and
    // rewardCount card picks. Exp/crystals apply instantly; the picks resolve
    // one at a time through the RewardQueue.
    public void GrantDungeonCompletion(int tier, int rewardCount)
    {
        GrantExpOnly(tier);
        for (int i = 0; i < rewardCount; i++)
            crystals.CreateCrystal(RandomCrystalColor());
        for (int i = 0; i < rewardCount; i++)
            OfferCardChoice(tier);
    }

    private static readonly EmpowerType[] crystalColors =
        { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
    private static EmpowerType RandomCrystalColor()
        => crystalColors[Random.Range(0, crystalColors.Length)];

    RewardSummary Grant(int tier)
    {
        int exp = RewardRules.SampleExp(tier, tuning.Data, max => Random.Range(0, max));
        player.PlayerExp += exp;

        EmpowerType? crystal = null;
        if (RewardRules.Roll(tuning.CrystalChance(tier), () => Random.value))
        {
            var color = RandomCrystalColor();
            crystals.CreateCrystal(color);
            crystal = color;
        }

        var pool = tuning.CardPool(tier);
        bool cardPick = RewardRules.Roll(tuning.CardChance(tier), () => Random.value)
                        && pool != null && pool.Count > 0;

        return new RewardSummary { exp = exp, crystal = crystal, cardPick = cardPick, tier = tier };
    }

    // Card pick: choose 1 of 3 from the tier's pool. Public because level-ups
    // and dungeon bundles grant the same pick. Self-enqueues on the RewardQueue
    // (spec 2026-07-13) — callers must never wrap this in their own Enqueue.
    public void OfferCardChoice(int tier, System.Action onClosed = null)
    {
        var pool = tuning.CardPool(tier);
        if (pool == null || pool.Count == 0) { onClosed?.Invoke(); return; }

        RewardQueue.Instance.Enqueue(done =>
        {
            var candidates = ShopRules.PickUnique(pool, 3, max => Random.Range(0, max));

            rewardCanvas.Offer(candidates,
                so => { deck.AddCard(so, toTop: true); done(); onClosed?.Invoke(); },
                () => { done(); onClosed?.Invoke(); });
        });
    }

    // Pay-then-pick: the caller has already charged the purchase, so a skip forfeits it.
    public void OfferTownCards(TownsSO town)
    {
        if (town == null) return;
        var pool = town.purchasableCards;
        if (pool == null || pool.Count == 0) return;

        RewardQueue.Instance.Enqueue(done =>
        {
            var candidates = ShopRules.PickUnique(pool, 3, max => Random.Range(0, max));
            rewardCanvas.Offer(candidates,
                so => { deck.AddCard(so, toTop: true); done(); },
                () => done());
        });
    }

    // Shrine payout (spec 2026-07-24): grant `count` of the rolled type. Safe
    // roll count = 1, guardian defeat count = 2. Card picks self-enqueue on the
    // RewardQueue; units join the army; exp applies instantly. Crystals are NOT
    // a shrine reward — the shrine consumes them.
    //
    // Both payout paths (the safe roll's 1x and the guardian's 2x) come through
    // here, so the message below is the one place a shrine reward is announced.
    // Without it exp and units landed in total silence and a card pick opened a
    // picker that never said where it came from (2026-07-31).
    public void GrantShrineReward(ShrineReward type, int count, ShrineSO shrine)
    {
        if (shrine == null) return;

        int exp = 0;
        var unitNames = new List<string>();
        int cardPicks = 0;

        for (int i = 0; i < count; i++)
        {
            switch (type)
            {
                case ShrineReward.CardPick:
                    cardPicks++;
                    break;
                case ShrineReward.Unit:
                    // A shrine unit is a bonus grant: it bypasses the town
                    // at-cap disband flow (tuning follow-up, not a blocker).
                    if (shrine.unitPool != null && shrine.unitPool.Count > 0)
                    {
                        var unit = shrine.unitPool[Random.Range(0, shrine.unitPool.Count)];
                        var playerRef = FindAnyObjectByType<Player>();
                        if (playerRef != null && unit != null)
                        {
                            playerRef.AddUnit(unit);
                            unitNames.Add(unit.cardName);
                        }
                    }
                    break;
                case ShrineReward.LargeExp:
                    player.PlayerExp += shrine.largeExp;
                    exp += shrine.largeExp;
                    break;
            }
        }

        // Announce BEFORE the picks enqueue, so the toast reads ahead of the
        // picker rather than behind it (the defeat message's ordering rule).
        GameLog.Instance.Post(ShrineRewardMessage.Compose(exp, unitNames, cardPicks));

        for (int i = 0; i < cardPicks; i++)
            OfferCardChoice(shrine.cardTier);
    }

    // Level-up card pick: pool tier scales with player level (spec 2026-07-10).
    public void OfferCardChoiceForLevel(int level, System.Action onClosed = null)
        => OfferCardChoice(RewardRules.CardTierForLevel(level, tuning.Data), onClosed);
}
