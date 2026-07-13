using System;
using System.Collections.Generic;

// Pure FIFO modal sequencer (spec 2026-07-13): one modal in flight, the rest
// wait. A job opens its modal when run and calls the supplied done exactly
// once when the modal resolves. Unity-free so it is mcs-testable; RewardQueue
// wraps one instance for the scene.
public class ModalQueueCore
{
    private readonly Queue<Action<Action>> pending = new Queue<Action<Action>>();
    private bool inFlight;
    // Bumped on Flush so a done captured before the flush can't advance the
    // queue a run-end teardown already reset.
    private int generation;

    public bool Busy => inFlight || pending.Count > 0;

    public void Enqueue(Action<Action> job)
    {
        pending.Enqueue(job);
        TryNext();
    }

    public void Flush()
    {
        pending.Clear();
        inFlight = false;
        generation++;
    }

    private void TryNext()
    {
        if (inFlight || pending.Count == 0) return;
        inFlight = true;
        var gen = generation;
        var job = pending.Dequeue();
        bool done = false;
        job(() =>
        {
            if (done || gen != generation) return;
            done = true;
            inFlight = false;
            TryNext();
        });
    }
}
