using System;
using System.Collections.Generic;

namespace CommandQueueRemastered.Core
{
    // Pending data belongs to the queue until creation has completed, not to a live scene object.
    public sealed class DeferredWorkQueue<T>
    {
        private readonly Queue<T> entries = new Queue<T>();
        private readonly double minimumInterval;
        private double nextDelivery = double.NegativeInfinity;
        private int lastFrame = -1;
        private bool processing;
        public DeferredWorkQueue(double minimumInterval = 0)
        {
            if (double.IsNaN(minimumInterval) || double.IsInfinity(minimumInterval) || minimumInterval < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumInterval));
            this.minimumInterval = minimumInterval;
        }
        public int Count => entries.Count;
        public void Add(T entry) => entries.Enqueue(entry);
        public bool TryBegin(int frame, double now, out T entry)
        {
            if (processing || entries.Count == 0 || lastFrame == frame || now < nextDelivery) { entry = default; return false; }
            // Schedule from now: a frame hitch must never cause a catch-up burst.
            nextDelivery = now + minimumInterval;
            lastFrame = frame; processing = true; entry = entries.Peek(); return true;
        }
        public bool Complete()
        {
            if (!processing) return false; // A run reset may cancel work during a native callback.
            entries.Dequeue(); processing = false; return true;
        }
        public void Retry()
        {
            processing = false;
        }
        public void Clear() { entries.Clear(); processing = false; lastFrame = -1; nextDelivery = double.NegativeInfinity; }
    }
}
