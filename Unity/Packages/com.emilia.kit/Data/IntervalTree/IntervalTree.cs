using System;
using System.Collections.Generic;

namespace Emilia.Data
{
    public partial class IntervalTree<T> where T : IInterval
    {
        const int MinNodeSize = 10; // the minimum number of entries to have subnodes
        const int InvalidNode = -1;
        const long CenterUnknown = long.MaxValue; // center hasn't been calculated. indicates no children

        readonly List<Entry> _entries = new List<Entry>();
        readonly List<IntervalTreeNode> _nodes = new List<IntervalTreeNode>();

        /// <summary>
        /// Whether the tree will be rebuilt on the next query
        /// </summary>
        public bool dirty { get; internal set; }

        /// <summary>
        /// Add an IInterval to the tree
        /// </summary>
        public void Add(T item)
        {
            if (item == null) return;

            this._entries.Add(
                new Entry() {
                    intervalStart = item.intervalStart,
                    intervalEnd = item.intervalEnd,
                    item = item
                }
            );
            dirty = true;
        }

        /// <summary>
        /// Query the tree at a particular time
        /// </summary>
        /// <param name="value"></param>
        /// <param name="results"></param>
        public void IntersectsWith(long value, List<T> results)
        {
            if (this._entries.Count == 0) return;

            if (dirty)
            {
                Rebuild();
                dirty = false;
            }

            if (this._nodes.Count > 0) Query(this._nodes[0], value, results);
        }

        /// <summary>
        /// Query the tree at a particular range of time
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="results"></param>
        public void IntersectsWithRange(long start, long end, List<T> results)
        {
            if (start > end) return;

            if (this._entries.Count == 0) return;

            if (dirty)
            {
                Rebuild();
                dirty = false;
            }

            if (this._nodes.Count > 0) QueryRange(this._nodes[0], start, end, results);
        }

        /// <summary>
        /// Updates the intervals from their source. Use this to detect if the data in the tree
        /// has changed.
        /// </summary>
        public void UpdateIntervals()
        {
            bool isDirty = false;
            for (int i = 0; i < this._entries.Count; i++)
            {
                var n = this._entries[i];
                var s = n.item.intervalStart;
                var e = n.item.intervalEnd;

                isDirty |= n.intervalStart != s;
                isDirty |= n.intervalEnd != e;

                this._entries[i] = new Entry() {
                    intervalStart = s,
                    intervalEnd = e,
                    item = n.item
                };
            }

            dirty |= isDirty;
        }

        private void Query(IntervalTreeNode intervalTreeNode, long value, List<T> results)
        {
            for (int i = intervalTreeNode.first; i <= intervalTreeNode.last; i++)
            {
                var entry = this._entries[i];
                if (value >= entry.intervalStart && value < entry.intervalEnd)
                {
                    results.Add(entry.item);
                }
            }

            if (intervalTreeNode.center == CenterUnknown) return;
            if (intervalTreeNode.left != InvalidNode && value < intervalTreeNode.center) Query(this._nodes[intervalTreeNode.left], value, results);
            if (intervalTreeNode.right != InvalidNode && value > intervalTreeNode.center) Query(this._nodes[intervalTreeNode.right], value, results);
        }

        private void QueryRange(IntervalTreeNode intervalTreeNode, long start, long end, List<T> results)
        {
            for (int i = intervalTreeNode.first; i <= intervalTreeNode.last; i++)
            {
                var entry = this._entries[i];
                if (end >= entry.intervalStart && start < entry.intervalEnd)
                {
                    results.Add(entry.item);
                }
            }

            if (intervalTreeNode.center == CenterUnknown) return;
            if (intervalTreeNode.left != InvalidNode && start < intervalTreeNode.center) QueryRange(this._nodes[intervalTreeNode.left], start, end, results);
            if (intervalTreeNode.right != InvalidNode && end > intervalTreeNode.center) QueryRange(this._nodes[intervalTreeNode.right], start, end, results);
        }

        private void Rebuild()
        {
            this._nodes.Clear();
            this._nodes.Capacity = this._entries.Capacity;
            Rebuild(0, this._entries.Count - 1);
        }

        private int Rebuild(int start, int end)
        {
            IntervalTreeNode intervalTreeNode = new IntervalTreeNode();

            // minimum size, don't subdivide
            int count = end - start + 1;
            if (count < MinNodeSize)
            {
                intervalTreeNode = new IntervalTreeNode() {center = CenterUnknown, first = start, last = end, left = InvalidNode, right = InvalidNode};
                this._nodes.Add(intervalTreeNode);
                return this._nodes.Count - 1;
            }

            var min = long.MaxValue;
            var max = long.MinValue;

            for (int i = start; i <= end; i++)
            {
                var o = this._entries[i];
                min = Math.Min(min, o.intervalStart);
                max = Math.Max(max, o.intervalEnd);
            }

            var center = (max + min) / 2;
            intervalTreeNode.center = center;

            // first pass, put every thing left of center, left
            int x = start;
            int y = end;
            while (true)
            {
                while (x <= end && this._entries[x].intervalEnd < center) x++;

                while (y >= start && this._entries[y].intervalEnd >= center) y--;

                if (x > y) break;

                var nodeX = this._entries[x];
                var nodeY = this._entries[y];

                this._entries[y] = nodeX;
                this._entries[x] = nodeY;
            }

            intervalTreeNode.first = x;

            // second pass, put every start passed the center right
            y = end;
            while (true)
            {
                while (x <= end && this._entries[x].intervalStart <= center) x++;

                while (y >= start && this._entries[y].intervalStart > center) y--;

                if (x > y) break;

                var nodeX = this._entries[x];
                var nodeY = this._entries[y];

                this._entries[y] = nodeX;
                this._entries[x] = nodeY;
            }

            intervalTreeNode.last = y;

            // reserve a place
            this._nodes.Add(new IntervalTreeNode());
            int index = this._nodes.Count - 1;

            intervalTreeNode.left = InvalidNode;
            intervalTreeNode.right = InvalidNode;

            if (start < intervalTreeNode.first) intervalTreeNode.left = Rebuild(start, intervalTreeNode.first - 1);

            if (end > intervalTreeNode.last) intervalTreeNode.right = Rebuild(intervalTreeNode.last + 1, end);

            this._nodes[index] = intervalTreeNode;
            return index;
        }

        public void Clear()
        {
            this._entries.Clear();
            this._nodes.Clear();
        }
    }
}