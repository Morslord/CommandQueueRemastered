using System;
using System.Collections.Generic;

namespace CommandQueueRemastered.Core
{
    // Register once, then visit fairly without copying/sorting the entire live set.
    public sealed class BoundedWorkQueue<T> where T : class
    {
        private readonly LinkedList<T> entries = new LinkedList<T>();
        private readonly Dictionary<T, LinkedListNode<T>> nodes = new Dictionary<T, LinkedListNode<T>>();
        private LinkedListNode<T> next;
        public int Count => nodes.Count;
        public void Add(T item, bool prioritize = false)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (nodes.ContainsKey(item)) return;
            // Optional priority visits new candidates promptly; ordinary work keeps FIFO order.
            var node = next == null ? entries.AddLast(item) : entries.AddBefore(next, item);
            nodes.Add(item, node);
            if (next == null || prioritize) next = node;
        }
        public bool TryVisit(out T item)
        {
            if (next == null) { item = null; return false; }
            var current = next;
            next = current.Next ?? entries.First;
            item = current.Value;
            return true;
        }
        public bool Remove(T item)
        {
            if (item == null || !nodes.TryGetValue(item, out var node)) return false;
            if (next == node) next = node.Next ?? entries.First;
            entries.Remove(node); nodes.Remove(item);
            if (entries.Count == 0) next = null;
            return true;
        }
        public int Drain(int limit, Action<T> process)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            int count = Math.Min(limit, Count);
            int processed = 0;
            for (; processed < count && TryVisit(out var item); processed++)
            {
                Remove(item); // Remove before calling native code: callbacks cannot schedule it twice.
                process(item);
            }
            return processed;
        }
        public void Clear() { entries.Clear(); nodes.Clear(); next = null; }
    }
}
