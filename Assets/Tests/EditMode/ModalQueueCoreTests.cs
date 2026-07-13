using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ModalQueueCoreTests
{
    [Test]
    public void IdleQueue_RunsJobImmediately()
    {
        var q = new ModalQueueCore();
        bool opened = false;
        q.Enqueue(done => { opened = true; });
        Assert.IsTrue(opened);
        Assert.IsTrue(q.Busy); // in flight until the job calls done
    }

    [Test]
    public void SecondJob_WaitsForFirstDone()
    {
        var q = new ModalQueueCore();
        Action first = null;
        bool secondOpened = false;
        q.Enqueue(done => { first = done; });
        q.Enqueue(done => { secondOpened = true; done(); });
        Assert.IsFalse(secondOpened);
        first();
        Assert.IsTrue(secondOpened);
        Assert.IsFalse(q.Busy);
    }

    [Test]
    public void Jobs_RunInFifoOrder()
    {
        var q = new ModalQueueCore();
        var order = new List<int>();
        q.Enqueue(done => { order.Add(1); done(); });
        q.Enqueue(done => { order.Add(2); done(); });
        q.Enqueue(done => { order.Add(3); done(); });
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
    }

    [Test]
    public void DoneCalledTwice_SecondCallIgnored()
    {
        var q = new ModalQueueCore();
        Action first = null;
        int opens = 0;
        q.Enqueue(done => { first = done; });
        q.Enqueue(done => { opens++; }); // stays in flight
        first();
        first(); // must not advance the queue again
        Assert.AreEqual(1, opens);
        Assert.IsTrue(q.Busy);
    }

    [Test]
    public void Flush_DropsPendingJobs()
    {
        var q = new ModalQueueCore();
        Action first = null;
        bool secondOpened = false;
        q.Enqueue(done => { first = done; });
        q.Enqueue(done => { secondOpened = true; });
        q.Flush();
        Assert.IsFalse(q.Busy);
        first(); // stale done from before the flush
        Assert.IsFalse(secondOpened);
    }

    [Test]
    public void StaleDoneAfterFlush_DoesNotDisruptNewJobs()
    {
        var q = new ModalQueueCore();
        Action stale = null;
        q.Enqueue(done => { stale = done; });
        q.Flush();
        q.Enqueue(done => { }); // new job, stays in flight
        stale();
        Assert.IsTrue(q.Busy); // the new job must still be the one in flight
    }
}
