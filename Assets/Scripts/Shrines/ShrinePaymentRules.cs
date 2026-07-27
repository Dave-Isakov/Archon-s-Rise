namespace ArchonsRise.Shrines
{
    // What a shrine's crystal slots may legally hold (spec 2026-07-27). The panel
    // owns pixels; this owns "what can this slot become next". Unity-free →
    // mcs-testable.
    //
    // A "bucket" is an index into the payment buckets the inventory reports:
    // the concrete colors in order, then a trailing wild slot. Empty (-1) means
    // the slot is offering nothing yet.
    //
    // The invariant that makes the confirm button unable to fail: a bucket is
    // only ever offered while a crystal for it is genuinely spare, so no
    // reachable combination of picks can exceed what the player holds.
    public static class ShrinePaymentRules
    {
        public const int Empty = -1;

        // Crystals of `bucket` left after every slot OTHER than `slot` takes its
        // claim. A slot never counts its own claim against itself — otherwise the
        // last crystal of a color could never be re-selected once chosen.
        public static int Spare(int[] holdings, int[] picks, int slot, int bucket)
        {
            if (holdings == null || picks == null) return 0;
            if (bucket < 0 || bucket >= holdings.Length) return 0;

            int claimed = 0;
            for (int i = 0; i < picks.Length; i++)
                if (i != slot && picks[i] == bucket) claimed++;
            return holdings[bucket] - claimed;
        }

        // The slot's next selection when the player clicks it. Walks the cycle
        // 0, 1, … n-1, Empty starting just after the current pick and stops at
        // the first bucket with a spare crystal.
        //
        // Empty sits at the end of the cycle, so it is always reached before the
        // walk could loop back to where it started: every click resolves, and a
        // slot can always be un-set without a Cancel button. With nothing spare
        // at all this simply returns Empty.
        public static int NextPick(int[] holdings, int[] picks, int slot)
        {
            if (holdings == null || picks == null) return Empty;
            if (slot < 0 || slot >= picks.Length) return Empty;

            int n = holdings.Length;
            int pos = picks[slot] == Empty ? n : picks[slot];   // n == the Empty seat

            for (int step = 1; step <= n + 1; step++)
            {
                int p = (pos + step) % (n + 1);
                if (p == n) return Empty;
                if (Spare(holdings, picks, slot, p) > 0) return p;
            }
            return Empty; // unreachable: the Empty seat always intercepts first
        }

        // Every slot holds a crystal — the confirm button's gate. No slots at all
        // is not "complete": there is nothing to pay with.
        public static bool IsComplete(int[] picks)
        {
            if (picks == null || picks.Length == 0) return false;
            for (int i = 0; i < picks.Length; i++)
                if (picks[i] == Empty) return false;
            return true;
        }
    }
}
